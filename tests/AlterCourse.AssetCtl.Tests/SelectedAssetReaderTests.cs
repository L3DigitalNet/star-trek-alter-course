using System.Diagnostics;
using AlterCourse.AssetCtl.Catalog;
using AlterCourse.AssetCtl.Publishing;
using Microsoft.Win32.SafeHandles;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Proves allocation/read bounds with small streams; Linux mkfifo supplies the nonregular-file fixture.</summary>
public sealed class SelectedAssetReaderTests
{
    /// <summary>Ancestor traversal rejects a link even when the final directory and file are ordinary objects.</summary>
    [Fact]
    public void SymlinkAncestorCannotRedirectDescriptorAdmission()
    {
        using var fixture = new LifecycleBoundaryFixture(new byte[8], maximumBytes: 8);
        string link = Path.Combine(fixture.Root, "linked-parent");
        Directory.CreateSymbolicLink(link, Path.GetDirectoryName(fixture.AssetPath)!);

        Assert.Throws<AssetCtlException>(() =>
            SelectedAssetReader.Read(Path.Combine(link, Path.GetFileName(fixture.AssetPath)), 8)
        );
    }

    /// <summary>A FIFO selection is rejected without waiting for a writer or querying its length.</summary>
    [Fact]
    public async Task FifoSelectionIsRejectedAtNonblockingRegularFileAdmission()
    {
        using var fixture = new LifecycleBoundaryFixture(new byte[8], maximumBytes: 8);
        File.Delete(fixture.AssetPath);
        var start = new ProcessStartInfo("mkfifo") { UseShellExecute = false };
        start.ArgumentList.Add(fixture.AssetPath);
        using Process process = Process.Start(start)!;
        await process.WaitForExitAsync();
        Assert.Equal(0, process.ExitCode);

        Assert.Throws<AssetCtlException>(() => SelectedAssetReader.Read(fixture.AssetPath, 8));
    }

    /// <summary>Failed stream construction cannot leak the admitted native descriptor.</summary>
    [Fact]
    public void StreamConstructionFailureDisposesOwnedHandle()
    {
        using var fixture = new LifecycleBoundaryFixture(new byte[8], maximumBytes: 8);
        using var parent = PublishingTypes.StateFile.DirectoryHandle.OpenExisting(
            Path.GetDirectoryName(fixture.AssetPath)!,
            "fixture"
        );
        SafeFileHandle? captured = null;
        var primary = new IOException("constructor fault");

        Exception? failure = Record.Exception(() =>
            parent.OpenReadFile(
                Path.GetFileName(fixture.AssetPath),
                "fixture",
                handle =>
                {
                    captured = handle;
                    throw primary;
                }
            )
        );

        Assert.Same(primary, failure);
        Assert.True(captured!.IsClosed);
    }

    /// <summary>Accepts partial reads only when the complete admitted snapshot and EOF agree.</summary>
    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(8, 1)]
    [InlineData(8, 3)]
    public void SmallExactAndPartialReadsAreBounded(int length, int chunk)
    {
        using var stream = new ObservedStream(new byte[length], length, chunk);
        List<int> allocations = [];

        byte[] bytes = SelectedAssetReader.Read(stream, 8, allocations.Add);

        Assert.Equal(length, bytes.Length);
        Assert.Equal([length], allocations);
        Assert.InRange(stream.RequestedBytes, length + 1, 37);
        Assert.Equal(length, stream.ConsumedBytes);
        Assert.Equal(1, stream.LastRequestedBytes);
    }

    /// <summary>Known oversize length triggers neither payload allocation nor a read.</summary>
    [Fact]
    public void OversizeMetadataPrecedesAllocationAndRead()
    {
        using var stream = new ObservedStream(new byte[9], 9);
        List<int> allocations = [];

        Assert.Throws<AssetCtlException>(() => SelectedAssetReader.Read(stream, 8, allocations.Add));

        Assert.Empty(allocations);
        Assert.Equal(0, stream.RequestedBytes);
        Assert.Equal(0, stream.ConsumedBytes);
    }

    /// <summary>Metadata is an early admission hint, never permission to accept a valid-looking prefix.</summary>
    [Theory]
    [InlineData(9, 8)]
    [InlineData(8, 9)]
    [InlineData(7, 8)]
    [InlineData(8, 7)]
    public void LyingLengthCannotAdmitTruncatedOrOversizedContent(int actual, int declared)
    {
        using var stream = new ObservedStream(new byte[actual], declared);
        List<int> allocations = [];

        Assert.Throws<AssetCtlException>(() => SelectedAssetReader.Read(stream, 8, allocations.Add));

        Assert.All(allocations, length => Assert.InRange(length, 0, 8));
        Assert.InRange(stream.ConsumedBytes, 0, 9);
    }

    /// <summary>Changing metadata is rejected even when reads happen to reach the original EOF.</summary>
    [Fact]
    public void ChangedLengthAfterReadRejectsSnapshot()
    {
        using var stream = new ObservedStream(new byte[8], 8) { FinalLength = 9 };

        Assert.Throws<AssetCtlException>(() => SelectedAssetReader.Read(stream, 8));

        Assert.Equal(8, stream.ConsumedBytes);
    }

    /// <summary>Read errors at payload and EOF probing retain the original exception.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(8)]
    public void ReadFailuresAreNotAcceptedOrHidden(int failureOffset)
    {
        var failure = new IOException("read fault");
        using var stream = new ObservedStream(new byte[8], 8, 1) { FailureOffset = failureOffset, Failure = failure };

        Exception? observed = Record.Exception(() => SelectedAssetReader.Read(stream, 8));

        Assert.Same(failure, observed);
        Assert.InRange(stream.ConsumedBytes, 0, 8);
    }

    /// <summary>Missing and symbolic-link selections fail at the concrete Linux file-open boundary.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MissingOrSymlinkSelectionIsRefused(bool symbolicLink)
    {
        using var fixture = new LifecycleBoundaryFixture(new byte[8], maximumBytes: 8);
        File.Delete(fixture.AssetPath);
        if (symbolicLink)
        {
            string target = Path.Combine(fixture.Root, "other");
            File.WriteAllBytes(target, new byte[8]);
            File.CreateSymbolicLink(fixture.AssetPath, target);
        }

        Assert.Throws<AssetCtlException>(() => SelectedAssetReader.Read(fixture.AssetPath, 8));
    }

    private sealed class ObservedStream(byte[] bytes, long declaredLength, int chunk = int.MaxValue)
        : MemoryStream(bytes)
    {
        private int _lengthReads;
        public long? FinalLength { get; init; }
        public int? FailureOffset { get; init; }
        public IOException? Failure { get; init; }
        public int RequestedBytes { get; private set; }
        public int ConsumedBytes { get; private set; }
        public int LastRequestedBytes { get; private set; }
        public override long Length => ++_lengthReads > 1 ? FinalLength ?? declaredLength : declaredLength;

        public override int Read(Span<byte> buffer)
        {
            RequestedBytes += buffer.Length;
            LastRequestedBytes = buffer.Length;
            if (Position == FailureOffset)
            {
                throw Failure!;
            }

            int count = base.Read(buffer[..Math.Min(buffer.Length, chunk)]);
            ConsumedBytes += count;
            return count;
        }
    }
}
