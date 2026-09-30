using Godot;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Exposes native content reads without making the filesystem authoritative simulation state.</summary>
/// <remarks>Godot FileAccess is required to retain packed res:// support. Callers own each opened handle.</remarks>
internal interface IContentFileAccess : IDisposable
{
    public ulong GetLength();

    public byte[] GetBuffer(long length);

    public string GetAsText();

    public Error GetError();
}
