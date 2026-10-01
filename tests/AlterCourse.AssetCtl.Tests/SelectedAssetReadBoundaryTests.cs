namespace AlterCourse.AssetCtl.Tests;

/// <summary>Observes the real selected-file read under tiny limits without allocating large fixtures.</summary>
public sealed class SelectedAssetReadBoundaryTests
{
    /// <summary>Characterizes integrity verification at and below the configured byte limit.</summary>
    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public void IntegrityAcceptsSmallAndExactLimitFiles(int length)
    {
        using var fixture = new LifecycleBoundaryFixture(new byte[length], maximumBytes: 8);

        ManifestStore.VerifyIntegrity(fixture.Configuration, fixture.Manifest);
    }

    /// <summary>Requires size admission before materialization even when recorded integrity later rejects the file.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OversizedSelectionIsRejectedBeforeWholeFileRead(bool mismatchedIntegrity)
    {
        using var fixture = new LifecycleBoundaryFixture(new byte[9], maximumBytes: 8);
        AssetManifest manifest = mismatchedIntegrity
            ? fixture.Manifest with { Integrity = fixture.Manifest.Integrity! with { ByteLength = 8 } }
            : fixture.Manifest;
        List<long> materializedLengths = [];

        Exception? failure = Record.Exception(() =>
            ManifestStore.VerifyIntegrity(fixture.Configuration, manifest, path =>
            {
                // This is the original BCL read, not a simulated allocation. Nine bytes are sufficient
                // to prove that configured admission did not precede full materialization.
                byte[] bytes = File.ReadAllBytes(path);
                materializedLengths.Add(bytes.LongLength);
                return bytes;
            })
        );

        Assert.Empty(materializedLengths);
        Assert.IsType<AssetCtlException>(failure);
    }
}
