using Godot;
using GodotFile = Godot.FileAccess;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Exposes native content reads without making the filesystem authoritative simulation state.</summary>
/// <remarks>Godot FileAccess is required to retain packed res:// support. Callers own each opened handle.</remarks>
internal interface IContentFileAccess : IDisposable
{
    ulong GetLength();

    byte[] GetBuffer(long length);

    string GetAsText();

    Error GetError();
}

/// <summary>Preserves native FileAccess decoding, cursor, error, and disposal behavior for content ingress.</summary>
internal sealed class GodotContentFileAccess : IContentFileAccess
{
    private readonly GodotFile _file;

    private GodotContentFileAccess(GodotFile file) => _file = file;

    internal static IContentFileAccess? Open(string path)
    {
        GodotFile? file = GodotFile.Open(path, GodotFile.ModeFlags.Read);
        return file is null ? null : new GodotContentFileAccess(file);
    }

    public ulong GetLength() => _file.GetLength();

    public byte[] GetBuffer(long length) => _file.GetBuffer(length);

    public string GetAsText() => _file.GetAsText();

    public Error GetError() => _file.GetError();

    public void Dispose() => _file.Dispose();
}
