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

        AtomicPublisher.PublicationResult result = AtomicPublisher.Publish(
            configuration,
            manifest.Request.Output.Path,
            bytes,
            manifest.ManifestPath,
            ManifestStore.Serialize(manifest)
        );

        Assert.True(result.Published);
        Assert.Equal("not-required", result.Rollback);
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

        AtomicPublisher.PublicationRecoveryResult recovery = AtomicPublisher.RecoverPending(configuration);

        Assert.Equal(1, recovery.RecoveredTransactions);
        Assert.Equal(0, recovery.ActiveTransactionsSkipped);
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

    /// <summary>Does not recover a live transaction while another asset publishes concurrently.</summary>
    [Fact]
    public async Task ConcurrentDifferentAssetPublishDoesNotDisturbActiveTransaction()
    {
        EffectiveConfiguration configuration = Configuration();
        using var transactionActive = new ManualResetEventSlim(false);
        using var allowFirstToFinish = new ManualResetEventSlim(false);
        byte[] firstBytes = "first-new"u8.ToArray();
        byte[] secondBytes = "second-new"u8.ToArray();
        AssetManifest first = Manifest(firstBytes, 1, "first");
        AssetManifest second = Manifest(secondBytes, 1, "second");

        Task firstPublish = Task.Run(() =>
            AtomicPublisher.Publish(
                configuration,
                first.Request.Output.Path,
                firstBytes,
                first.ManifestPath,
                ManifestStore.Serialize(first),
                new AtomicPublisher.PublicationTestHooks(AfterMove: move =>
                {
                    if (move == AtomicPublisher.PublicationMove.InstallAsset)
                    {
                        transactionActive.Set();
                        Assert.True(allowFirstToFinish.Wait(TimeSpan.FromSeconds(10)));
                    }
                })
            )
        );
        Assert.True(transactionActive.Wait(TimeSpan.FromSeconds(10)));

        Task<AtomicPublisher.PublicationResult> secondPublish = Task.Run(() =>
            AtomicPublisher.Publish(
                configuration,
                second.Request.Output.Path,
                secondBytes,
                second.ManifestPath,
                ManifestStore.Serialize(second)
            )
        );
        AtomicPublisher.PublicationResult secondResult = await secondPublish.WaitAsync(TimeSpan.FromSeconds(10));
        allowFirstToFinish.Set();
        await firstPublish.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, secondResult.ActiveTransactionsSkipped);
        AssertPair(firstBytes, first);
        AssertPair(secondBytes, second);
    }

    /// <summary>Bounds ignored journal input and quarantines it inside the configured state root.</summary>
    [Fact]
    public void OversizedPublicationJournalIsQuarantined()
    {
        EffectiveConfiguration configuration = Configuration();
        string journalRoot = Path.Combine(root, ".assetctl", "state", "publish-transactions");
        Directory.CreateDirectory(journalRoot);
        string journal = Path.Combine(journalRoot, "oversized.json");
        File.WriteAllText(journal, new string('x', 70_000));

        Assert.Throws<AssetCtlException>(() => AtomicPublisher.RecoverPending(configuration));

        Assert.False(File.Exists(journal));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(journalRoot, "quarantine"), "*.invalid"));
    }

    /// <summary>Rejects JSON nesting beyond the bounded journal contract before any recovery path is resolved.</summary>
    [Fact]
    public void DeeplyNestedPublicationJournalIsQuarantined()
    {
        EffectiveConfiguration configuration = Configuration();
        string journalRoot = Path.Combine(root, ".assetctl", "state", "publish-transactions");
        Directory.CreateDirectory(journalRoot);
        string journal = Path.Combine(journalRoot, Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(journal, "{\"padding\":" + new string('[', 20) + "0" + new string(']', 20) + "}");

        Assert.Throws<AssetCtlException>(() => AtomicPublisher.RecoverPending(configuration));

        Assert.False(File.Exists(journal));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(journalRoot, "quarantine"), "*.invalid"));
    }

    /// <summary>Quarantines a journal symlink without reading or moving its external target.</summary>
    [Fact]
    public void PublicationJournalSymlinkCannotEscapeStateRoot()
    {
        EffectiveConfiguration configuration = Configuration();
        string journalRoot = Path.Combine(root, ".assetctl", "state", "publish-transactions");
        string outside = Path.Combine(root, "outside-journal.json");
        Directory.CreateDirectory(journalRoot);
        File.WriteAllText(outside, "external evidence");
        string journal = Path.Combine(journalRoot, Guid.NewGuid().ToString("N") + ".json");
        File.CreateSymbolicLink(journal, outside);

        Assert.Throws<AssetCtlException>(() => AtomicPublisher.RecoverPending(configuration));

        Assert.Equal("external evidence", File.ReadAllText(outside));
        Assert.False(File.Exists(journal));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(journalRoot, "quarantine"), "*.invalid"));
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

    private static AssetManifest Manifest(byte[] bytes, int revision, string name = "asset") =>
        new(
            "1",
            TestData.Request() with
            {
                Id = $"test.{name}",
                Output = TestData.Request().Output with { Path = $"assets/{name}.png" },
            },
            revision,
            new RightsRecord("original-project-created", "project", null, null, "test"),
            null,
            null,
            null,
            new IntegrityRecord(Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.LongLength, "image/png"),
            new ApprovalRecord(null, null, null),
            null,
            $"catalog/test.{name}.asset.yaml"
        );

    private void AssertPair(byte[] expectedBytes, AssetManifest expectedManifest)
    {
        Assert.Equal(expectedBytes, File.ReadAllBytes(Path.Combine(root, expectedManifest.Request.Output.Path)));
        AssetManifest actual = ManifestStore.Load(Configuration(), expectedManifest.ManifestPath);
        Assert.Equal(expectedManifest.Integrity!.Sha256, actual.Integrity!.Sha256);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(expectedBytes)), actual.Integrity.Sha256);
    }
}
