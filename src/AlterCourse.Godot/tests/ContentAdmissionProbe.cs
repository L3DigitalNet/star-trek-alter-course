using AlterCourse.Core.Content;
using AlterCourse.Godot.Gameplay;
using Godot;

namespace AlterCourse.Godot.Tests;

/// <summary>Records real native reads in GameScreen bootstrap while redirecting one path to a runtime fixture.</summary>
/// <remarks>The Godot runtime and managed game assembly are required; this probe does not substitute a Core loader.</remarks>
public partial class ContentAdmissionProbe : RefCounted
{
    /// <summary>Gets the existing Core definition envelope for runtime boundary fixtures.</summary>
    public int MaximumDocumentBytes { get; } = SystemDefinitionContent.MaximumDocumentBytes;

    /// <summary>Gets the bounded schema envelope used by the actual composition boundary.</summary>
    public int MaximumSchemaBytes { get; } = GameScreen.MaximumSchemaBytes;

    /// <summary>Gets the composition boundary's largest individual byte-read request.</summary>
    public int MaximumReadChunkBytes { get; } = GameScreen.ContentReadChunkBytes;

    /// <summary>Gets whole-text reads of the redirected resource.</summary>
    public int TextReads { get; private set; }

    /// <summary>Gets byte counts requested from the redirected resource.</summary>
    public long RequestedBytes { get; private set; }

    /// <summary>Gets bytes actually returned to the bootstrap reader.</summary>
    public long ReturnedBytes { get; private set; }

    /// <summary>Gets the largest individual native or fake byte request.</summary>
    public long MaximumRequestBytes { get; private set; }

    /// <summary>Gets requests that exceeded the remaining envelope including its sentinel.</summary>
    public int RequestsBeyondBudget { get; private set; }

    /// <summary>Gets length queries against the redirected handle.</summary>
    public int LengthReads { get; private set; }

    /// <summary>Gets opened redirected handles.</summary>
    public int OpenedHandles { get; private set; }

    /// <summary>Gets or sets a fake length; a negative value reports the actual fixture length.</summary>
    public long ReportedLength { get; set; } = -1;

    /// <summary>Gets or sets the maximum bytes returned by each fake read.</summary>
    public int MaximumChunkBytes { get; set; } = int.MaxValue;

    /// <summary>Gets or sets a fake exhaustion position; a negative value retains all fixture bytes.</summary>
    public int TruncateAfterBytes { get; set; } = -1;

    /// <summary>Gets or sets a fake read-error position; a negative value permits all reads.</summary>
    public int ReadErrorAfterBytes { get; set; } = -1;

    /// <summary>Gets or sets whether the fake opener returns no handle.</summary>
    public bool FailOpen { get; set; }

    /// <summary>Gets or sets whether the fake length query reports an I/O error.</summary>
    public bool FailLength { get; set; }

    /// <summary>Gets or sets whether every partial fake buffer also reports EOF.</summary>
    public bool EofWithPartialReads { get; set; }

    /// <summary>Gets or sets a false zero-read position that leaves a suffix available and does not report EOF.</summary>
    public int ZeroWithoutEofAfterBytes { get; set; } = -1;

    private int _resourceLimit;
    private readonly Dictionary<string, long> _logOffsets = new(StringComparer.Ordinal);
    private string? _logDirectory;

    /// <summary>Gets disposed handles of the redirected resource.</summary>
    public int DisposedHandles { get; private set; }

    /// <summary>Starts a capture of real gameplay log rows before a screen enters the tree.</summary>
    public void BeginLogCapture()
    {
        _logDirectory = ProjectSettings.GlobalizePath("user://logs");
        _logOffsets.Clear();
        if (!Directory.Exists(_logDirectory))
        {
            return;
        }

        foreach (string path in Directory.EnumerateFiles(_logDirectory, "gameplay-*.json"))
        {
            _logOffsets.Add(path, new FileInfo(path).Length);
        }
    }

    /// <summary>Reads only newly appended JSON rows after the captured screen exits the tree and disposes its sink.</summary>
    public string[] ReadCapturedLogRows()
    {
        if (_logDirectory is null)
        {
            throw new InvalidOperationException("Begin a gameplay log capture before reading its rows.");
        }

        if (!Directory.Exists(_logDirectory))
        {
            return [];
        }

        List<string> rows = [];
        // A new scene can get a different file while another sink holds the daily file, or after rollover.
        // Snapshot byte offsets exclude old receipts; session assertions in GameplayShellTest reject other writers.
        foreach (string path in Directory.EnumerateFiles(_logDirectory, "gameplay-*.json"))
        {
            using FileStream stream = new(
                path,
                FileMode.Open,
                System.IO.FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            );
            long offset = _logOffsets.GetValueOrDefault(path);
            if (stream.Length < offset)
            {
                throw new InvalidOperationException("A captured gameplay log was truncated before inspection.");
            }

            stream.Position = offset;
            using StreamReader reader = new(stream);
            while (reader.ReadLine() is { } row)
            {
                rows.Add(row);
            }
        }

        return rows.ToArray();
    }

    /// <summary>Installs recording before the screen enters the tree and performs its actual bootstrap.</summary>
    public void Redirect(Node screen, string canonicalPath, string fixturePath)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixturePath);
        _resourceLimit = GetResourceLimit(canonicalPath);
        ((GameScreen)screen).ContentFileOpener = path =>
        {
            if (!string.Equals(path, canonicalPath, StringComparison.Ordinal))
            {
                return GodotContentFileAccess.Open(path);
            }

            IContentFileAccess? file = GodotContentFileAccess.Open(fixturePath);
            if (file is null)
            {
                return null;
            }

            OpenedHandles++;
            return new RecordingFile(file, this);
        };
    }

    /// <summary>Substitutes one bounded byte source at the same opener used by GameScreen's native bootstrap.</summary>
    public void Fake(Node screen, string canonicalPath, byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);
        ArgumentNullException.ThrowIfNull(bytes);
        _resourceLimit = GetResourceLimit(canonicalPath);
        ((GameScreen)screen).ContentFileOpener = path =>
        {
            if (!string.Equals(path, canonicalPath, StringComparison.Ordinal))
            {
                return GodotContentFileAccess.Open(path);
            }

            if (FailOpen)
            {
                return null;
            }

            OpenedHandles++;
            return new FakeFile(bytes, this);
        };
    }

    /// <summary>Gets the actual family envelope without maintaining a second test-side ceiling.</summary>
    public int GetResourceLimit(string canonicalPath) =>
        canonicalPath.Contains("/schemas/", StringComparison.Ordinal) ? MaximumSchemaBytes : MaximumDocumentBytes;

    private void RecordRequest(long length)
    {
        RequestedBytes += length;
        MaximumRequestBytes = Math.Max(MaximumRequestBytes, length);
        if (length > _resourceLimit + 1 - ReturnedBytes)
        {
            RequestsBeyondBudget++;
        }
    }

    private sealed class RecordingFile(IContentFileAccess file, ContentAdmissionProbe probe) : IContentFileAccess
    {
        public ulong GetLength()
        {
            probe.LengthReads++;
            return file.GetLength();
        }

        public byte[] GetBuffer(long length)
        {
            probe.RecordRequest(length);
            byte[] bytes = file.GetBuffer(length);
            probe.ReturnedBytes += bytes.Length;
            return bytes;
        }

        public string GetAsText()
        {
            probe.TextReads++;
            return file.GetAsText();
        }

        public Error GetError() => file.GetError();

        public void Dispose()
        {
            probe.DisposedHandles++;
            file.Dispose();
        }
    }

    private sealed class FakeFile(byte[] bytes, ContentAdmissionProbe probe) : IContentFileAccess
    {
        private int _position;
        private Error _error;

        public ulong GetLength()
        {
            probe.LengthReads++;
            _error = probe.FailLength ? Error.FileCantRead : Error.Ok;
            return probe.ReportedLength < 0 ? (ulong)bytes.Length : (ulong)probe.ReportedLength;
        }

        public byte[] GetBuffer(long length)
        {
            probe.RecordRequest(length);
            if (probe.ZeroWithoutEofAfterBytes >= 0 && _position >= probe.ZeroWithoutEofAfterBytes)
            {
                _error = Error.Ok;
                return [];
            }

            if (probe.ReadErrorAfterBytes >= 0 && _position >= probe.ReadErrorAfterBytes)
            {
                _error = Error.FileCantRead;
                return [];
            }

            int end = probe.TruncateAfterBytes < 0 ? bytes.Length : Math.Min(bytes.Length, probe.TruncateAfterBytes);
            if (probe.ZeroWithoutEofAfterBytes >= 0)
            {
                end = Math.Min(end, probe.ZeroWithoutEofAfterBytes);
            }
            int count = Math.Min(checked((int)length), Math.Min(probe.MaximumChunkBytes, end - _position));
            byte[] chunk = bytes.AsSpan(_position, count).ToArray();
            _position += count;
            probe.ReturnedBytes += count;
            _error =
                probe.ZeroWithoutEofAfterBytes >= 0 ? Error.Ok
                : count == 0 || _position == end || probe.EofWithPartialReads ? Error.FileEof
                : Error.Ok;
            return chunk;
        }

        public string GetAsText()
        {
            probe.TextReads++;
            throw new InvalidOperationException("A byte-source fake cannot decode authored content.");
        }

        public Error GetError() => _error;

        public void Dispose() => probe.DisposedHandles++;
    }
}
