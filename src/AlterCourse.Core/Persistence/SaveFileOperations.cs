namespace AlterCourse.Core.Persistence;

/// <summary>
/// Isolates the save transaction's file operations for deterministic failure tests. Production
/// still uses a new sibling file, a disk flush, and same-filesystem replacement visibility.
/// </summary>
internal class SaveFileOperations
{
    internal virtual Stream CreateCandidate(string path) =>
        new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);

    internal virtual void Write(Stream stream, ReadOnlySpan<byte> json) => stream.Write(json);

    internal virtual void Flush(Stream stream) => ((FileStream)stream).Flush(flushToDisk: true);

    internal virtual void Replace(string candidate, string target) => File.Move(candidate, target, overwrite: true);

    internal virtual void DeleteCandidate(string path) => File.Delete(path);
}
