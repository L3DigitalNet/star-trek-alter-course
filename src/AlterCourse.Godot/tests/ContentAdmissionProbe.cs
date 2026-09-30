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

    /// <summary>Gets whole-text reads of the redirected resource.</summary>
    public int TextReads { get; private set; }

    /// <summary>Gets byte counts requested from the redirected resource.</summary>
    public long RequestedBytes { get; private set; }

    /// <summary>Gets disposed handles of the redirected resource.</summary>
    public int DisposedHandles { get; private set; }

    /// <summary>Installs recording before the screen enters the tree and performs its actual bootstrap.</summary>
    public void Redirect(Node screen, string canonicalPath, string fixturePath)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(fixturePath);
        ((GameScreen)screen).ContentFileOpener = path =>
        {
            if (!string.Equals(path, canonicalPath, StringComparison.Ordinal))
            {
                return GodotContentFileAccess.Open(path);
            }

            IContentFileAccess? file = GodotContentFileAccess.Open(fixturePath);
            return file is null ? null : new RecordingFile(file, this);
        };
    }

    private sealed class RecordingFile(IContentFileAccess file, ContentAdmissionProbe probe) : IContentFileAccess
    {
        public ulong GetLength() => file.GetLength();

        public byte[] GetBuffer(long length)
        {
            probe.RequestedBytes += length;
            return file.GetBuffer(length);
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
}
