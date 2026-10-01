using AlterCourse.AssetCtl.Publishing;

namespace AlterCourse.AssetCtl.Catalog;

/// <summary>
/// Admits selected local bytes before allocation and rejects incomplete or growing snapshots.
/// Linux openat/statx admit regular files without symbolic-link redirection; callers retain format/pixel admission.
/// </summary>
internal static class SelectedAssetReader
{
    public static byte[] Read(string path, long maximumBytes)
    {
        using var parent = PublishingTypes.StateFile.DirectoryHandle.OpenExisting(
            Path.GetDirectoryName(Path.GetFullPath(path))!,
            "selected file parent"
        );
        return Read(parent, Path.GetFileName(path), maximumBytes);
    }

    internal static byte[] Read(PublishingTypes.StateFile.DirectoryHandle parent, string leaf, long maximumBytes)
    {
        using FileStream stream = parent.OpenReadFile(leaf, "selected file");
        return Read(stream, maximumBytes);
    }

    internal static byte[] Read(Stream stream, long maximumBytes, Action<int>? allocating = null)
    {
        long length = stream.Length;
        if (maximumBytes < 0 || length < 0 || length > maximumBytes || length > Array.MaxLength)
        {
            throw new AssetCtlException("Selected file exceeds the applicable byte limit.", 1);
        }

        // Length rejects known oversize inputs before allocation. The read loop independently
        // caps consumption and requires EOF, so stale/lying length cannot admit a valid prefix.
        int count = checked((int)length);
        allocating?.Invoke(count);
        byte[] bytes = new byte[count];
        int offset = 0;
        while (offset < count)
        {
            int read = stream.Read(bytes.AsSpan(offset, count - offset));
            if (read == 0)
            {
                throw new AssetCtlException("Selected file shortened during the read.", 7);
            }

            offset += read;
        }

        Span<byte> trailing = stackalloc byte[1];
        if (stream.Read(trailing) != 0 || stream.Length != length)
        {
            throw new AssetCtlException("Selected file changed during the read.", 7);
        }

        return bytes;
    }
}
