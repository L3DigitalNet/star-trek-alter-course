using System.Security.Cryptography;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies publication staging, rollback, and interruption recovery as one paired-file transaction.</summary>
public sealed class PublicationRecoveryTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "assetctl-publication-recovery-" + Guid.NewGuid().ToString("N")
    );

    /// <summary>Rejects staged asset tampering before either tracked file moves.</summary>
    [Fact]
    public void StagedAssetTamperingFailsBeforePublication()
    {
        EffectiveConfiguration configuration = Configuration();
        (byte[] oldBytes, AssetManifest oldManifest) = WriteOldPair();
        byte[] newBytes = "new-asset"u8.ToArray();
        AssetManifest replacement = Manifest(newBytes, revision: 2);

        Assert.Throws<AssetCtlException>(() =>
            AtomicPublisher.Publish(
                configuration,
                replacement.Request.Output.Path,
                newBytes,
                replacement.ManifestPath,
                ManifestStore.Serialize(replacement),
                new AtomicPublisher.PublicationTestHooks(
                    AfterWorkFilesStaged: (assetStage, _) => File.WriteAllText(assetStage, "tampered")
                )
            )
        );

        AssertPair(oldBytes, oldManifest);
    }

    /// <summary>Publishes a new matching pair and removes all transaction state after final verification.</summary>
    [Fact]
    public void NewPairPublishesWithoutResidualTransactionState()
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] bytes = "first-asset"u8.ToArray();
        AssetManifest manifest = Manifest(bytes, revision: 1);

        AtomicPublisher.Publish(
            configuration,
            manifest.Request.Output.Path,
            bytes,
            manifest.ManifestPath,
            ManifestStore.Serialize(manifest)
        );

        AssertPair(bytes, manifest);
        Assert.Empty(
            Directory.EnumerateFiles(Path.Combine(root, ".assetctl", "state"), "*.json", SearchOption.AllDirectories)
        );
    }

    /// <summary>Rolls back the old pair when any individual live-file move fails.</summary>
    [Theory]
    [InlineData((int)AtomicPublisher.PublicationMove.BackupAsset)]
    [InlineData((int)AtomicPublisher.PublicationMove.BackupManifest)]
    [InlineData((int)AtomicPublisher.PublicationMove.InstallAsset)]
    [InlineData((int)AtomicPublisher.PublicationMove.InstallManifest)]
    public void MoveFailureRestoresOldPair(int failedMoveValue)
    {
        var failedMove = (AtomicPublisher.PublicationMove)failedMoveValue;
        EffectiveConfiguration configuration = Configuration();
        (byte[] oldBytes, AssetManifest oldManifest) = WriteOldPair();
        byte[] newBytes = "new-asset"u8.ToArray();
        AssetManifest replacement = Manifest(newBytes, revision: 2);

        Assert.Throws<IOException>(() =>
            AtomicPublisher.Publish(
                configuration,
                replacement.Request.Output.Path,
                newBytes,
                replacement.ManifestPath,
                ManifestStore.Serialize(replacement),
                new AtomicPublisher.PublicationTestHooks(BeforeMove: move =>
                {
                    if (move == failedMove)
                    {
                        throw new IOException("simulated move failure");
                    }
                })
            )
        );

        AssertPair(oldBytes, oldManifest);
    }

    /// <summary>Recovers an interrupted transaction to a matching old or new pair at every rename boundary.</summary>
    [Theory]
    [InlineData((int)AtomicPublisher.PublicationMove.BackupAsset, false)]
    [InlineData((int)AtomicPublisher.PublicationMove.BackupManifest, false)]
    [InlineData((int)AtomicPublisher.PublicationMove.InstallAsset, false)]
    [InlineData((int)AtomicPublisher.PublicationMove.InstallManifest, true)]
    public void NextPublishRecoveryNeverLeavesMismatchedPair(int interruptedAfterValue, bool completesNewPair)
    {
        var interruptedAfter = (AtomicPublisher.PublicationMove)interruptedAfterValue;
        EffectiveConfiguration configuration = Configuration();
        (byte[] oldBytes, AssetManifest oldManifest) = WriteOldPair();
        byte[] interruptedBytes = "interrupted-new-asset"u8.ToArray();
        AssetManifest interruptedManifest = Manifest(interruptedBytes, revision: 2);

        Assert.Throws<AtomicPublisher.SimulatedPublicationInterruptionException>(() =>
            AtomicPublisher.Publish(
                configuration,
                interruptedManifest.Request.Output.Path,
                interruptedBytes,
                interruptedManifest.ManifestPath,
                ManifestStore.Serialize(interruptedManifest),
                new AtomicPublisher.PublicationTestHooks(AfterMove: move =>
                {
                    if (move == interruptedAfter)
                    {
                        throw new AtomicPublisher.SimulatedPublicationInterruptionException();
                    }
                })
            )
        );

        AtomicPublisher.RecoverPending(configuration);

        if (completesNewPair)
        {
            AssertPair(interruptedBytes, interruptedManifest);
        }
        else
        {
            AssertPair(oldBytes, oldManifest);
        }
        Assert.Empty(
            Directory.EnumerateFiles(Path.Combine(root, ".assetctl", "state"), "*.json", SearchOption.AllDirectories)
        );
    }

    /// <summary>Removes the isolated repository used by each publication test.</summary>
    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private EffectiveConfiguration Configuration()
    {
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        Directory.CreateDirectory(Path.Combine(root, "catalog"));
        return new EffectiveConfiguration(
            root,
            new AssetCtlPaths("assets", "catalog", "styles", ".assetctl/work", "runs", ".assetctl/state", "logs"),
            new AssetCtlPolicy(false, true, true, true, false, "reject"),
            new AssetCtlLimits(1_000_000, 1_000_000, 10, 10, 10, 30, 1_000_000),
            new SpendingLimits(0, 0, 0),
            new Dictionary<string, ProviderInstance>(StringComparer.Ordinal),
            [],
            [],
            new Dictionary<string, QualityTier>(StringComparer.Ordinal),
            new Dictionary<string, StyleProfile>(StringComparer.Ordinal)
            {
                ["engineering-icons"] = new StyleProfile("engineering-icons", "test", [], []),
            },
            new Dictionary<string, string>(StringComparer.Ordinal),
            "hash"
        );
    }

    private (byte[] Bytes, AssetManifest Manifest) WriteOldPair()
    {
        byte[] bytes = "old-asset"u8.ToArray();
        AssetManifest manifest = Manifest(bytes, revision: 1);
        File.WriteAllBytes(Path.Combine(root, manifest.Request.Output.Path), bytes);
        File.WriteAllText(Path.Combine(root, manifest.ManifestPath), ManifestStore.Serialize(manifest));
        return (bytes, manifest);
    }

    private static AssetManifest Manifest(byte[] bytes, int revision) =>
        new(
            "1",
            TestData.Request() with
            {
                Output = TestData.Request().Output with { Path = "assets/asset.png" },
            },
            revision,
            new RightsRecord("original-project-created", "project", null, null, "test"),
            null,
            null,
            null,
            new IntegrityRecord(Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.LongLength, "image/png"),
            new ApprovalRecord(null, null, null),
            null,
            "catalog/test.asset.yaml"
        );

    private void AssertPair(byte[] expectedBytes, AssetManifest expectedManifest)
    {
        Assert.Equal(expectedBytes, File.ReadAllBytes(Path.Combine(root, expectedManifest.Request.Output.Path)));
        AssetManifest actual = ManifestStore.Load(Configuration(), expectedManifest.ManifestPath);
        Assert.Equal(expectedManifest.Integrity!.Sha256, actual.Integrity!.Sha256);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(expectedBytes)), actual.Integrity.Sha256);
    }
}
