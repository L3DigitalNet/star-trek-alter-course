using System.Security.Cryptography;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies publication staging, rollback, and interruption recovery as one paired-file transaction.</summary>
public sealed class PublicationRecoveryTests : IDisposable
{
    private readonly string _root = Path.Combine(
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
        AssetManifest request = manifest with { Integrity = null };
        File.WriteAllText(Path.Combine(_root, request.ManifestPath), ManifestStore.Serialize(request));

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
            Directory.EnumerateFiles(Path.Combine(_root, ".assetctl", "state"), "*.json", SearchOption.AllDirectories)
        );
    }

    /// <summary>Rejects a symlinked publication-journal directory without writing to its target.</summary>
    [Fact]
    public void PublicationRejectsSymlinkedJournalRootBeforeWritingExternalTarget()
    {
        EffectiveConfiguration configuration = Configuration();
        string external = Path.Combine(Path.GetTempPath(), "assetctl-journal-external-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, ".assetctl", "state"));
        Directory.CreateDirectory(external);
        Directory.CreateSymbolicLink(Path.Combine(_root, ".assetctl", "state", "publish-transactions"), external);
        byte[] bytes = "first-asset"u8.ToArray();
        AssetManifest manifest = Manifest(bytes, revision: 1);
        File.WriteAllText(
            Path.Combine(_root, manifest.ManifestPath),
            ManifestStore.Serialize(manifest with { Integrity = null })
        );
        try
        {
            Assert.Throws<AssetCtlException>(() =>
                AtomicPublisher.Publish(
                    configuration,
                    manifest.Request.Output.Path,
                    bytes,
                    manifest.ManifestPath,
                    ManifestStore.Serialize(manifest)
                )
            );
            Assert.Empty(Directory.EnumerateFileSystemEntries(external));
        }
        finally
        {
            Directory.Delete(external, recursive: true);
        }
    }

    /// <summary>Rejects a symlinked publish-staging child without writing transaction files outside work state.</summary>
    [Fact]
    public void PublicationRejectsSymlinkedPublishChildBeforeStagingExternalFiles()
    {
        EffectiveConfiguration configuration = Configuration();
        string external = Path.Combine(Path.GetTempPath(), "assetctl-publish-external-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, ".assetctl", "work"));
        Directory.CreateDirectory(external);
        Directory.CreateSymbolicLink(Path.Combine(_root, ".assetctl", "work", "publish"), external);
        byte[] bytes = "first-asset"u8.ToArray();
        AssetManifest manifest = Manifest(bytes, revision: 1);
        File.WriteAllText(
            Path.Combine(_root, manifest.ManifestPath),
            ManifestStore.Serialize(manifest with { Integrity = null })
        );
        try
        {
            Assert.Throws<AssetCtlException>(() =>
                AtomicPublisher.Publish(
                    configuration,
                    manifest.Request.Output.Path,
                    bytes,
                    manifest.ManifestPath,
                    ManifestStore.Serialize(manifest)
                )
            );
            Assert.Empty(Directory.EnumerateFileSystemEntries(external));
        }
        finally
        {
            Directory.Delete(external, recursive: true);
        }
    }

    /// <summary>Fails closed when the transaction directory is replaced after its descriptor is opened.</summary>
    [Fact]
    public void PublicationTransactionSubstitutionCannotRedirectStagedWrites()
    {
        EffectiveConfiguration configuration = Configuration();
        string external = Path.Combine(
            Path.GetTempPath(),
            "assetctl-transaction-race-external-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(external);
        byte[] bytes = "first-asset"u8.ToArray();
        AssetManifest manifest = Manifest(bytes, revision: 1);
        File.WriteAllText(
            Path.Combine(_root, manifest.ManifestPath),
            ManifestStore.Serialize(manifest with { Integrity = null })
        );
        string? displaced = null;
        try
        {
            Assert.Throws<AssetCtlException>(() =>
                AtomicPublisher.Publish(
                    configuration,
                    manifest.Request.Output.Path,
                    bytes,
                    manifest.ManifestPath,
                    ManifestStore.Serialize(manifest),
                    new AtomicPublisher.PublicationTestHooks(BeforeWorkFilesWritten: transactionRoot =>
                    {
                        displaced = transactionRoot + ".displaced";
                        Directory.Move(transactionRoot, displaced);
                        Directory.CreateSymbolicLink(transactionRoot, external);
                    })
                )
            );

            Assert.Empty(Directory.EnumerateFileSystemEntries(external));
            Assert.NotNull(displaced);
            Assert.Empty(Directory.EnumerateFileSystemEntries(displaced));
        }
        finally
        {
            Directory.Delete(external, recursive: true);
        }
    }

    /// <summary>Publishes the first generated output over its authoritative manifest-only request.</summary>
    [Fact]
    public void FirstGenerationPublishesOverManifestOnlyRequest()
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest request = ManifestWithoutOutput();
        File.WriteAllText(Path.Combine(_root, request.ManifestPath), ManifestStore.Serialize(request));
        byte[] bytes = "first-generated-asset"u8.ToArray();
        AssetManifest generated = Manifest(bytes, revision: request.Revision + 1);

        AtomicPublisher.PublicationResult result = AtomicPublisher.Publish(
            configuration,
            generated.Request.Output.Path,
            bytes,
            generated.ManifestPath,
            ManifestStore.Serialize(generated)
        );

        Assert.True(result.Published);
        AssertPair(bytes, generated);
    }

    /// <summary>Repairs a deleted output only when the surviving manifest still owns the output.</summary>
    [Fact]
    public void DeletedPublishedOutputCanBeRegeneratedFromItsOwningManifest()
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] oldBytes = "old-asset"u8.ToArray();
        AssetManifest oldManifest = Manifest(oldBytes, revision: 1);
        File.WriteAllText(Path.Combine(_root, oldManifest.ManifestPath), ManifestStore.Serialize(oldManifest));
        byte[] newBytes = "regenerated-asset"u8.ToArray();
        AssetManifest replacement = Manifest(newBytes, revision: 2);

        AtomicPublisher.Publish(
            configuration,
            replacement.Request.Output.Path,
            newBytes,
            replacement.ManifestPath,
            ManifestStore.Serialize(replacement)
        );

        AssertPair(newBytes, replacement);
    }

    /// <summary>Refuses output ownership collisions before changing approved victim bytes.</summary>
    [Fact]
    public void PublicationRejectsCatalogCollisionWithoutChangingApprovedVictim()
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] victimBytes = "approved-victim"u8.ToArray();
        AssetManifest victim = Manifest(victimBytes, 1, "victim") with
        {
            Request = Manifest(victimBytes, 1, "victim").Request with { Lifecycle = AssetLifecycle.Approved },
            Approval = new ApprovalRecord("owner", DateTimeOffset.UtcNow, "approved"),
        };
        File.WriteAllBytes(Path.Combine(_root, victim.Request.Output.Path), victimBytes);
        File.WriteAllText(Path.Combine(_root, victim.ManifestPath), ManifestStore.Serialize(victim));
        byte[] attackerBytes = "attacker"u8.ToArray();
        AssetManifest attacker = Manifest(attackerBytes, 1, "attacker") with
        {
            Request = Manifest(attackerBytes, 1, "attacker").Request with { Output = victim.Request.Output },
        };
        File.WriteAllText(Path.Combine(_root, attacker.ManifestPath), ManifestStore.Serialize(attacker));

        AssetCtlException exception = Assert.Throws<AssetCtlException>(() =>
            AtomicPublisher.Publish(
                configuration,
                attacker.Request.Output.Path,
                attackerBytes,
                attacker.ManifestPath,
                ManifestStore.Serialize(attacker)
            )
        );

        Assert.Equal(2, exception.ExitCode);
        Assert.Equal(victimBytes, File.ReadAllBytes(Path.Combine(_root, victim.Request.Output.Path)));
    }

    /// <summary>Rejects alternate relative spellings of an approved output before publication changes either file.</summary>
    [Theory]
    [InlineData("assets/./victim.png")]
    [InlineData("assets\\victim.png")]
    public void PublicationRejectsCanonicalCatalogCollisionWithoutChangingApprovedVictim(string attackerPath)
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] victimBytes = "approved-victim"u8.ToArray();
        AssetManifest victim = Manifest(victimBytes, 1, "victim") with
        {
            Request = Manifest(victimBytes, 1, "victim").Request with { Lifecycle = AssetLifecycle.Approved },
            Approval = new ApprovalRecord("owner", DateTimeOffset.UtcNow, "approved"),
        };
        string victimManifestPath = Path.Combine(_root, victim.ManifestPath);
        File.WriteAllBytes(Path.Combine(_root, victim.Request.Output.Path), victimBytes);
        File.WriteAllText(victimManifestPath, ManifestStore.Serialize(victim));
        string victimManifestBefore = File.ReadAllText(victimManifestPath);
        byte[] attackerBytes = "attacker"u8.ToArray();
        AssetManifest attacker = Manifest(attackerBytes, 1, "attacker") with
        {
            Request = Manifest(attackerBytes, 1, "attacker").Request with
            {
                Output = Manifest(attackerBytes, 1, "attacker").Request.Output with { Path = attackerPath },
            },
        };
        File.WriteAllText(Path.Combine(_root, attacker.ManifestPath), ManifestStore.Serialize(attacker));

        Assert.Throws<AssetCtlException>(() =>
            AtomicPublisher.Publish(
                configuration,
                attackerPath,
                attackerBytes,
                attacker.ManifestPath,
                ManifestStore.Serialize(attacker)
            )
        );

        Assert.Equal(victimBytes, File.ReadAllBytes(Path.Combine(_root, victim.Request.Output.Path)));
        Assert.Equal(victimManifestBefore, File.ReadAllText(victimManifestPath));
    }

    /// <summary>Restores the authoritative manifest-only request when a first-publication move fails.</summary>
    [Theory]
    [InlineData((int)AtomicPublisher.PublicationMove.BackupManifest)]
    [InlineData((int)AtomicPublisher.PublicationMove.InstallAsset)]
    [InlineData((int)AtomicPublisher.PublicationMove.InstallManifest)]
    public void FirstGenerationMoveFailureRestoresManifestOnlyRequest(int failedMoveValue)
    {
        var failedMove = (AtomicPublisher.PublicationMove)failedMoveValue;
        EffectiveConfiguration configuration = Configuration();
        AssetManifest request = ManifestWithoutOutput();
        string requestText = ManifestStore.Serialize(request);
        File.WriteAllText(Path.Combine(_root, request.ManifestPath), requestText);
        byte[] bytes = "first-generated-asset"u8.ToArray();
        AssetManifest generated = Manifest(bytes, revision: request.Revision + 1);

        Assert.Throws<IOException>(() =>
            AtomicPublisher.Publish(
                configuration,
                generated.Request.Output.Path,
                bytes,
                generated.ManifestPath,
                ManifestStore.Serialize(generated),
                new AtomicPublisher.PublicationTestHooks(BeforeMove: move =>
                {
                    if (move == failedMove)
                    {
                        throw new IOException("simulated first-publication move failure");
                    }
                })
            )
        );

        Assert.False(File.Exists(Path.Combine(_root, request.Request.Output.Path)));
        Assert.Equal(requestText, File.ReadAllText(Path.Combine(_root, request.ManifestPath)));
    }

    /// <summary>Recovers an interrupted first publication to its manifest-only request or complete new pair.</summary>
    [Theory]
    [InlineData((int)AtomicPublisher.PublicationMove.BackupManifest, false)]
    [InlineData((int)AtomicPublisher.PublicationMove.InstallAsset, false)]
    [InlineData((int)AtomicPublisher.PublicationMove.InstallManifest, true)]
    public void FirstGenerationRecoveryPreservesAValidInitialOrCompleteState(
        int interruptedAfterValue,
        bool completesNewPair
    )
    {
        var interruptedAfter = (AtomicPublisher.PublicationMove)interruptedAfterValue;
        EffectiveConfiguration configuration = Configuration();
        AssetManifest request = ManifestWithoutOutput();
        string requestText = ManifestStore.Serialize(request);
        File.WriteAllText(Path.Combine(_root, request.ManifestPath), requestText);
        byte[] bytes = "first-generated-asset"u8.ToArray();
        AssetManifest generated = Manifest(bytes, revision: request.Revision + 1);

        Assert.Throws<AtomicPublisher.SimulatedPublicationInterruptionException>(() =>
            AtomicPublisher.Publish(
                configuration,
                generated.Request.Output.Path,
                bytes,
                generated.ManifestPath,
                ManifestStore.Serialize(generated),
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
        if (completesNewPair)
        {
            AssertPair(bytes, generated);
        }
        else
        {
            Assert.False(File.Exists(Path.Combine(_root, request.Request.Output.Path)));
            Assert.Equal(requestText, File.ReadAllText(Path.Combine(_root, request.ManifestPath)));
        }
    }

    /// <summary>Rejects an asset-only partial state because no authoritative request can identify it.</summary>
    [Fact]
    public void AssetOnlyInitialStateIsRejected()
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] oldBytes = "orphaned-asset"u8.ToArray();
        AssetManifest generated = Manifest(oldBytes, revision: 2);
        File.WriteAllBytes(Path.Combine(_root, generated.Request.Output.Path), oldBytes);

        AssetCtlException exception = Assert.Throws<AssetCtlException>(() =>
            AtomicPublisher.Publish(
                configuration,
                generated.Request.Output.Path,
                oldBytes,
                generated.ManifestPath,
                ManifestStore.Serialize(generated)
            )
        );

        Assert.Contains("manifest path", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Rejects a manifest-only state whose identity does not authorize the requested output.</summary>
    [Fact]
    public void MismatchedManifestOnlyInitialStateIsRejected()
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest unrelated = ManifestWithoutOutput("other");
        AssetManifest generated = Manifest("new"u8.ToArray(), revision: 2);
        File.WriteAllText(Path.Combine(_root, generated.ManifestPath), ManifestStore.Serialize(unrelated));

        AssetCtlException exception = Assert.Throws<AssetCtlException>(() =>
            AtomicPublisher.Publish(
                configuration,
                generated.Request.Output.Path,
                "new"u8.ToArray(),
                generated.ManifestPath,
                ManifestStore.Serialize(generated)
            )
        );

        Assert.Contains("not owned", exception.Message, StringComparison.Ordinal);
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
            Directory.EnumerateFiles(Path.Combine(_root, ".assetctl", "state"), "*.json", SearchOption.AllDirectories)
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
        await File.WriteAllTextAsync(
            Path.Combine(_root, first.ManifestPath),
            ManifestStore.Serialize(first with { Integrity = null })
        );
        await File.WriteAllTextAsync(
            Path.Combine(_root, second.ManifestPath),
            ManifestStore.Serialize(second with { Integrity = null })
        );

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
        string journalRoot = Path.Combine(_root, ".assetctl", "state", "publish-transactions");
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
        string journalRoot = Path.Combine(_root, ".assetctl", "state", "publish-transactions");
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
        string journalRoot = Path.Combine(_root, ".assetctl", "state", "publish-transactions");
        string outside = Path.Combine(_root, "outside-journal.json");
        Directory.CreateDirectory(journalRoot);
        File.WriteAllText(outside, "external evidence");
        string journal = Path.Combine(journalRoot, Guid.NewGuid().ToString("N") + ".json");
        File.CreateSymbolicLink(journal, outside);

        Assert.Throws<AssetCtlException>(() => AtomicPublisher.RecoverPending(configuration));

        Assert.Equal("external evidence", File.ReadAllText(outside));
        Assert.False(File.Exists(journal));
        Assert.Single(Directory.EnumerateFiles(Path.Combine(journalRoot, "quarantine"), "*.invalid"));
    }

    /// <summary>Rejects a symlinked quarantine child without moving an invalid journal outside state.</summary>
    [Fact]
    public void PublicationQuarantineSymlinkCannotEscapeStateRoot()
    {
        EffectiveConfiguration configuration = Configuration();
        string journalRoot = Path.Combine(_root, ".assetctl", "state", "publish-transactions");
        string external = Path.Combine(
            Path.GetTempPath(),
            "assetctl-quarantine-external-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(journalRoot);
        Directory.CreateDirectory(external);
        Directory.CreateSymbolicLink(Path.Combine(journalRoot, "quarantine"), external);
        string journal = Path.Combine(journalRoot, "invalid.json");
        File.WriteAllText(journal, "not-json");
        try
        {
            Assert.Throws<AssetCtlException>(() => AtomicPublisher.RecoverPending(configuration));
            Assert.True(File.Exists(journal));
            Assert.Empty(Directory.EnumerateFileSystemEntries(external));
        }
        finally
        {
            Directory.Delete(external, recursive: true);
        }
    }

    /// <summary>Keeps a quarantine move bound to its opened directory when its pathname is replaced.</summary>
    [Fact]
    public void PublicationQuarantineSubstitutionCannotRedirectJournalMove()
    {
        EffectiveConfiguration configuration = Configuration();
        string journalRoot = Path.Combine(_root, ".assetctl", "state", "publish-transactions");
        string external = Path.Combine(
            Path.GetTempPath(),
            "assetctl-quarantine-race-external-" + Guid.NewGuid().ToString("N")
        );
        Directory.CreateDirectory(journalRoot);
        Directory.CreateDirectory(external);
        string journal = Path.Combine(journalRoot, "invalid.json");
        File.WriteAllText(journal, "not-json");
        string? displaced = null;
        try
        {
            Assert.Throws<AssetCtlException>(() =>
                AtomicPublisher.RecoverPending(
                    configuration,
                    new AtomicPublisher.PublicationTestHooks(BeforeQuarantineMove: quarantineRoot =>
                    {
                        displaced = quarantineRoot + ".displaced";
                        Directory.Move(quarantineRoot, displaced);
                        Directory.CreateSymbolicLink(quarantineRoot, external);
                    })
                )
            );

            Assert.Empty(Directory.EnumerateFileSystemEntries(external));
            Assert.False(File.Exists(journal));
            Assert.NotNull(displaced);
            Assert.Single(Directory.EnumerateFiles(displaced, "*.invalid"));
        }
        finally
        {
            Directory.Delete(external, recursive: true);
        }
    }

    /// <summary>Removes the isolated repository used by each publication test.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private EffectiveConfiguration Configuration()
    {
        Directory.CreateDirectory(Path.Combine(_root, "assets"));
        Directory.CreateDirectory(Path.Combine(_root, "catalog"));
        return new EffectiveConfiguration(
            _root,
            new AssetCtlPaths("assets", "catalog", "styles", ".assetctl/work", "runs", ".assetctl/state", "logs"),
            new AssetCtlPolicy(false, true, true, true, false, "reject"),
            new AssetCtlLimits(1_000_000, 1_000_000, 10, 10, 10, 30, 1_000_000),
            new SpendingLimits(0, 0, 0),
            new Dictionary<string, ProviderInstance>(StringComparer.Ordinal),
            [],
            [],
            new Dictionary<string, QualityTier>(StringComparer.Ordinal)
            {
                ["development"] = new QualityTier("development", 1, 1, "disabled", true, 0),
            },
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
        File.WriteAllBytes(Path.Combine(_root, manifest.Request.Output.Path), bytes);
        File.WriteAllText(Path.Combine(_root, manifest.ManifestPath), ManifestStore.Serialize(manifest));
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

    private static AssetManifest ManifestWithoutOutput(string name = "asset") =>
        Manifest([], revision: 1, name) with
        {
            Integrity = null,
        };

    private void AssertPair(byte[] expectedBytes, AssetManifest expectedManifest)
    {
        Assert.Equal(expectedBytes, File.ReadAllBytes(Path.Combine(_root, expectedManifest.Request.Output.Path)));
        AssetManifest actual = ManifestStore.Load(Configuration(), expectedManifest.ManifestPath);
        Assert.Equal(expectedManifest.Integrity!.Sha256, actual.Integrity!.Sha256);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(expectedBytes)), actual.Integrity.Sha256);
    }
}
