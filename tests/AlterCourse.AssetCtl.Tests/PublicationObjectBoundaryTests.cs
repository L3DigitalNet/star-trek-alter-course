using System.Security.Cryptography;
using System.Text.Json;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Exercises Linux descriptor publication boundaries inside one disposable sandbox.</summary>
public sealed class PublicationObjectBoundaryTests : IDisposable
{
    private readonly string _sandbox = Path.Combine(
        Path.GetTempPath(),
        "assetctl-object-" + Guid.NewGuid().ToString("N")
    );
    private string Root => Path.Combine(_sandbox, "repository");

    /// <summary>A local recovery journal cannot authorize removal of an unrelated approved pair.</summary>
    [Fact]
    public void ForgedJournalCannotDeleteUnrelatedApprovedPair()
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] victimBytes = "approved-victim"u8.ToArray();
        AssetManifest victim = Manifest(victimBytes, 1, "victim");
        victim = victim with
        {
            Request = victim.Request with { Lifecycle = AssetLifecycle.Approved },
            Approval = new ApprovalRecord("owner", DateTimeOffset.UtcNow, "approved"),
        };
        WritePair(victimBytes, victim);
        string victimText = File.ReadAllText(Path.Combine(Root, victim.ManifestPath));
        byte[] eligibleBytes = "eligible-old"u8.ToArray();
        AssetManifest eligible = Manifest(eligibleBytes, 1);
        WritePair(eligibleBytes, eligible);
        string transaction = Guid.NewGuid().ToString("N");
        string journalRoot = Path.Combine(Root, ".assetctl/state/publish-transactions");
        Directory.CreateDirectory(journalRoot);
        File.WriteAllText(
            Path.Combine(journalRoot, transaction + ".json"),
            JsonSerializer.Serialize(
                new
                {
                    transaction_id = transaction,
                    asset_path = victim.Request.Output.Path,
                    manifest_path = victim.ManifestPath,
                    asset_stage_path = $".assetctl/work/publish/{transaction}/asset.stage",
                    manifest_stage_path = $".assetctl/work/publish/{transaction}/manifest.stage",
                    asset_backup_path = victim.Request.Output.Path + ".assetctl-backup-" + transaction,
                    manifest_backup_path = victim.ManifestPath + ".assetctl-backup-" + transaction,
                    asset_existed = false,
                    manifest_existed = false,
                    asset_hash = new string('a', 64),
                    asset_length = 1,
                    manifest_hash = new string('b', 64),
                }
            )
        );
        byte[] next = "eligible-next"u8.ToArray();
        _ = Record.Exception(() =>
            AtomicPublisher.Publish(
                configuration,
                eligible.Request.Output.Path,
                next,
                eligible.ManifestPath,
                ManifestStore.Serialize(Manifest(next, 2))
            )
        );
        Assert.True(File.Exists(Path.Combine(Root, victim.Request.Output.Path)));
        Assert.Equal(victimBytes, File.ReadAllBytes(Path.Combine(Root, victim.Request.Output.Path)));
        Assert.Equal(victimText, File.ReadAllText(Path.Combine(Root, victim.ManifestPath)));
    }

    /// <summary>Backup moves cannot follow a substituted parent outside the admitted repository.</summary>
    [Theory]
    [InlineData((int)AtomicPublisher.PublicationMove.BackupAsset)]
    [InlineData((int)AtomicPublisher.PublicationMove.BackupManifest)]
    public void BackupParentSubstitutionPreservesBothPredecessors(int moveValue)
    {
        var move = (AtomicPublisher.PublicationMove)moveValue;
        EffectiveConfiguration configuration = Configuration();
        byte[] oldBytes = "old-asset"u8.ToArray();
        AssetManifest old = Manifest(oldBytes, 1);
        WritePair(oldBytes, old);
        string oldText = File.ReadAllText(Path.Combine(Root, old.ManifestPath));
        string parent = Path.Combine(Root, move == AtomicPublisher.PublicationMove.BackupAsset ? "assets" : "catalog");
        string displaced = parent + ".admitted";
        string external = Path.Combine(_sandbox, "external");
        Directory.CreateDirectory(external);
        string leaf = Path.GetFileName(
            move == AtomicPublisher.PublicationMove.BackupAsset ? old.Request.Output.Path : old.ManifestPath
        );
        byte[] outsideBytes = "outside-predecessor"u8.ToArray();
        File.WriteAllBytes(Path.Combine(external, leaf), outsideBytes);
        byte[] next = "new-asset"u8.ToArray();
        Exception? failure = Record.Exception(() =>
            AtomicPublisher.Publish(
                configuration,
                old.Request.Output.Path,
                next,
                old.ManifestPath,
                ManifestStore.Serialize(Manifest(next, 2)),
                new AtomicPublisher.PublicationTestHooks(BeforeMove: current =>
                {
                    if (current == move)
                    {
                        Directory.Move(parent, displaced);
                        Directory.CreateSymbolicLink(parent, external);
                    }
                })
            )
        );
        Assert.NotNull(failure);
        Assert.True(File.Exists(Path.Combine(external, leaf)));
        Assert.Equal(outsideBytes, File.ReadAllBytes(Path.Combine(external, leaf)));
        Assert.Single(Directory.EnumerateFileSystemEntries(external));
        string assetParent =
            move == AtomicPublisher.PublicationMove.BackupAsset ? displaced : Path.Combine(Root, "assets");
        string manifestParent =
            move == AtomicPublisher.PublicationMove.BackupManifest ? displaced : Path.Combine(Root, "catalog");
        Assert.Equal(oldBytes, File.ReadAllBytes(Path.Combine(assetParent, Path.GetFileName(old.Request.Output.Path))));
        Assert.Equal(oldText, File.ReadAllText(Path.Combine(manifestParent, Path.GetFileName(old.ManifestPath))));
    }

    /// <summary>Rollback failure must not replace the original publication failure.</summary>
    [Fact]
    public void RecoveryFailurePreservesPrimaryException()
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] oldBytes = "old"u8.ToArray();
        AssetManifest old = Manifest(oldBytes, 1);
        WritePair(oldBytes, old);
        var primary = new IOException("primary-test-failure");
        string parent = Path.Combine(Root, "assets");
        string external = Path.Combine(_sandbox, "external");
        Directory.CreateDirectory(external);
        byte[] next = "next"u8.ToArray();
        Exception? failure = Record.Exception(() =>
            AtomicPublisher.Publish(
                configuration,
                old.Request.Output.Path,
                next,
                old.ManifestPath,
                ManifestStore.Serialize(Manifest(next, 2)),
                new AtomicPublisher.PublicationTestHooks(BeforeMove: move =>
                {
                    if (move == AtomicPublisher.PublicationMove.BackupAsset)
                    {
                        Directory.Move(parent, parent + ".admitted");
                        Directory.CreateSymbolicLink(parent, external);
                        throw primary;
                    }
                })
            )
        );
        Assert.Same(primary, failure);
        Assert.Equal(oldBytes, File.ReadAllBytes(Path.Combine(parent + ".admitted", "asset.png")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(external));
    }

    /// <summary>Quarantine reporting must not render untrusted names or parser exception prose.</summary>
    [Fact]
    public void InvalidJournalReportsStableCategory()
    {
        EffectiveConfiguration configuration = Configuration();
        string root = Path.Combine(Root, ".assetctl/state/publish-transactions");
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "PRIVATE-JOURNAL-SENTINEL.json"), "{ invalid");
        AssetCtlException failure = Assert.Throws<AssetCtlException>(() =>
            AtomicPublisher.RecoverPending(configuration)
        );
        Assert.Equal("Publication journal is invalid and was quarantined.", failure.Message);
        Assert.Single(Directory.EnumerateFiles(Path.Combine(root, "quarantine")));
    }

    /// <summary>Legacy recovery quarantines its locator while preserving the interrupted publication artifacts.</summary>
    [Fact]
    public void LegacyJournalQuarantinePreservesInterruptedPublicationArtifacts()
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] oldBytes = "legacy-predecessor"u8.ToArray();
        AssetManifest old = Manifest(oldBytes, 1);
        WritePair(oldBytes, old);
        byte[] oldManifestBytes = File.ReadAllBytes(Path.Combine(Root, old.ManifestPath));
        Interrupt(configuration, old);
        string journalRoot = Path.Combine(Root, ".assetctl/state/publish-transactions");
        string journalPath = Directory.GetFiles(journalRoot, "*.json").Single();
        System.Text.Json.Nodes.JsonNode journal = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(journalPath))!;
        string transaction = journal["transaction_id"]!.GetValue<string>();
        System.Text.Json.Nodes.JsonObject legacy = LegacyJournal(journal);
        string assetStage = Path.Combine(Root, legacy["asset_stage_path"]!.GetValue<string>());
        File.Delete(Path.Combine(Path.GetDirectoryName(assetStage)!, "authority.json"));
        File.WriteAllText(journalPath, legacy.ToJsonString(JsonOptions.Stable));
        byte[] legacyBytes = File.ReadAllBytes(journalPath);
        string[] retainedPaths =
        [
            Path.Combine(Root, legacy["asset_backup_path"]!.GetValue<string>()),
            assetStage,
            Path.Combine(Root, legacy["manifest_stage_path"]!.GetValue<string>()),
            Path.Combine(Root, old.ManifestPath),
        ];
        Dictionary<string, byte[]> retainedBytes = retainedPaths.ToDictionary(
            path => path,
            File.ReadAllBytes,
            StringComparer.Ordinal
        );
        Assert.Equal(oldBytes, retainedBytes[retainedPaths[0]]);
        Assert.Equal(oldManifestBytes, retainedBytes[retainedPaths[3]]);
        Assert.False(File.Exists(Path.Combine(Root, old.Request.Output.Path)));
        string manifestBackup = Path.Combine(Root, legacy["manifest_backup_path"]!.GetValue<string>());
        Assert.False(File.Exists(manifestBackup));

        AssetCtlException failure = Assert.Throws<AssetCtlException>(() =>
            AtomicPublisher.RecoverPending(configuration)
        );

        Assert.Equal(7, failure.ExitCode);
        Assert.Equal("Publication journal is invalid and was quarantined.", failure.Message);
        Assert.False(File.Exists(journalPath));
        string quarantined = Assert.Single(Directory.GetFiles(Path.Combine(journalRoot, "quarantine")));
        Assert.Matches($"^{transaction}\\.[0-9a-f]{{32}}\\.invalid$", Path.GetFileName(quarantined));
        Assert.Equal(legacyBytes, File.ReadAllBytes(quarantined));
        foreach (string path in retainedPaths)
        {
            Assert.Equal(retainedBytes[path], File.ReadAllBytes(path));
        }
        Assert.False(File.Exists(Path.Combine(Root, old.Request.Output.Path)));
        Assert.False(File.Exists(manifestBackup));
    }

    /// <summary>A journal edited after interruption cannot replace transaction-owned authority.</summary>
    [Fact]
    public void ChangedRecoveryLocatorPreservesPredecessorAndUnrelatedFiles()
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] oldBytes = "old"u8.ToArray();
        AssetManifest old = Manifest(oldBytes, 1);
        WritePair(oldBytes, old);
        Interrupt(configuration, old);
        string journalPath = Directory
            .GetFiles(Path.Combine(Root, ".assetctl/state/publish-transactions"), "*.json")
            .Single();
        System.Text.Json.Nodes.JsonNode journal = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(journalPath))!;
        string transaction = journal["transaction_id"]!.GetValue<string>();
        string backup = Path.Combine(Root, old.Request.Output.Path + ".assetctl-backup-" + transaction);
        string stage = Path.Combine(Root, journal["asset_stage_path"]!.GetValue<string>());
        byte[] staged = File.ReadAllBytes(stage);
        journal["asset_existed"] = false;
        journal["asset_predecessor"] = null;
        File.WriteAllText(journalPath, journal.ToJsonString(JsonOptions.Stable));
        Assert.Throws<AssetCtlException>(() => AtomicPublisher.RecoverPending(configuration));
        Assert.Equal(oldBytes, File.ReadAllBytes(backup));
        Assert.Equal(staged, File.ReadAllBytes(stage));
        Assert.False(File.Exists(Path.Combine(Root, old.Request.Output.Path)));
        Assert.Equal(1, ManifestStore.Load(configuration, old.ManifestPath).Revision);
    }

    /// <summary>Recovery retains an unowned stage or backup replacement even when its pathname matches.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RecoveryCannotDeleteUnownedArtifact(bool replaceStage)
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] oldBytes = "old"u8.ToArray();
        AssetManifest old = Manifest(oldBytes, 1);
        WritePair(oldBytes, old);
        Interrupt(configuration, old);
        string journalPath = Directory
            .GetFiles(Path.Combine(Root, ".assetctl/state/publish-transactions"), "*.json")
            .Single();
        System.Text.Json.Nodes.JsonNode journal = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(journalPath))!;
        string artifact = Path.Combine(
            Root,
            journal[replaceStage ? "asset_stage_path" : "asset_backup_path"]!.GetValue<string>()
        );
        byte[] preserved = File.ReadAllBytes(artifact);
        string replacement = artifact + ".unowned";
        File.WriteAllBytes(replacement, preserved);
        File.Move(replacement, artifact, overwrite: true);
        Assert.Throws<AssetCtlException>(() => AtomicPublisher.RecoverPending(configuration));
        Assert.Equal(preserved, File.ReadAllBytes(artifact));
        Assert.False(File.Exists(Path.Combine(Root, old.Request.Output.Path)));
        Assert.Equal(1, ManifestStore.Load(configuration, old.ManifestPath).Revision);
    }

    /// <summary>Independent lifecycle admission protects approved and deprecated predecessors.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProtectedPredecessorCannotBePublished(bool deprecated)
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] oldBytes = "protected"u8.ToArray();
        AssetManifest old = Manifest(oldBytes, 1);
        old = old with
        {
            Request = old.Request with { Lifecycle = deprecated ? AssetLifecycle.Deprecated : AssetLifecycle.Approved },
            Approval = new ApprovalRecord("owner", DateTimeOffset.UtcNow, "approved"),
            Deprecation = deprecated
                ? new AlterCourse.AssetCtl.Domain.DomainModels.DeprecationRecord(
                    "owner",
                    DateTimeOffset.UtcNow,
                    "retired"
                )
                : null,
        };
        WritePair(oldBytes, old);
        string oldText = File.ReadAllText(Path.Combine(Root, old.ManifestPath));
        byte[] next = "next"u8.ToArray();
        Assert.Throws<AssetCtlException>(() =>
            AtomicPublisher.Publish(
                configuration,
                old.Request.Output.Path,
                next,
                old.ManifestPath,
                ManifestStore.Serialize(Manifest(next, 2))
            )
        );
        Assert.Equal(oldBytes, File.ReadAllBytes(Path.Combine(Root, old.Request.Output.Path)));
        Assert.Equal(oldText, File.ReadAllText(Path.Combine(Root, old.ManifestPath)));
    }

    /// <summary>A required reporting failure after manifest installation cannot imply rollback.</summary>
    [Fact]
    public void PostCommitFailureStillReportsPublication()
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] oldBytes = "old"u8.ToArray();
        AssetManifest old = Manifest(oldBytes, 1);
        WritePair(oldBytes, old);
        byte[] next = "next"u8.ToArray();
        AtomicPublisher.PublicationResult result = AtomicPublisher.Publish(
            configuration,
            old.Request.Output.Path,
            next,
            old.ManifestPath,
            ManifestStore.Serialize(Manifest(next, 2)),
            new AtomicPublisher.PublicationTestHooks(AfterMove: move =>
            {
                if (move == AtomicPublisher.PublicationMove.InstallManifest)
                    throw new IOException("postcommit-test-failure");
            })
        );
        Assert.True(result.Published);
        Assert.Equal(next, File.ReadAllBytes(Path.Combine(Root, old.Request.Output.Path)));
        Assert.Equal(2, ManifestStore.Load(configuration, old.ManifestPath).Revision);
        Assert.Equal(1, AtomicPublisher.RecoverPending(configuration).RecoveredTransactions);
    }

    /// <summary>Unknown rollback artifacts preserve the primary failure and all retained predecessor evidence.</summary>
    [Fact]
    public void UnownedRollbackArtifactCannotReplacePrimaryFailure()
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] oldBytes = "predecessor"u8.ToArray();
        AssetManifest old = Manifest(oldBytes, 1);
        WritePair(oldBytes, old);
        string manifestText = File.ReadAllText(Path.Combine(Root, old.ManifestPath));
        var primary = new IOException("primary-publication-failure");
        string? backup = null;
        byte[] unknown = "unowned-backup"u8.ToArray();
        byte[] next = "next"u8.ToArray();
        Exception? failure = Record.Exception(() =>
            AtomicPublisher.Publish(
                configuration,
                old.Request.Output.Path,
                next,
                old.ManifestPath,
                ManifestStore.Serialize(Manifest(next, 2)),
                new AtomicPublisher.PublicationTestHooks(AfterMove: move =>
                {
                    if (move != AtomicPublisher.PublicationMove.BackupAsset)
                        return;
                    backup = Directory.GetFiles(Path.Combine(Root, "assets"), "*.assetctl-backup-*").Single();
                    File.Move(backup, backup + ".retained");
                    File.WriteAllBytes(backup, unknown);
                    throw primary;
                })
            )
        );
        Assert.Same(primary, failure);
        Assert.NotNull(backup);
        Assert.Equal(oldBytes, File.ReadAllBytes(backup + ".retained"));
        Assert.Equal(unknown, File.ReadAllBytes(backup));
        Assert.Equal(manifestText, File.ReadAllText(Path.Combine(Root, old.ManifestPath)));
    }

    /// <summary>A byte-identical stage replacement does not inherit the publisher's ownership.</summary>
    [Fact]
    public void ReplacedStageRetainsUnownedBytesBeforePublication()
    {
        EffectiveConfiguration configuration = Configuration();
        byte[] oldBytes = "old"u8.ToArray();
        AssetManifest old = Manifest(oldBytes, 1);
        WritePair(oldBytes, old);
        byte[] next = "next"u8.ToArray();
        string? stage = null;
        Assert.Throws<AssetCtlException>(() =>
            AtomicPublisher.Publish(
                configuration,
                old.Request.Output.Path,
                next,
                old.ManifestPath,
                ManifestStore.Serialize(Manifest(next, 2)),
                new AtomicPublisher.PublicationTestHooks(
                    AfterWorkFilesStaged: (assetStage, _) =>
                    {
                        stage = assetStage;
                        File.Move(assetStage, assetStage + ".retained");
                        File.WriteAllBytes(assetStage, next);
                    }
                )
            )
        );
        Assert.NotNull(stage);
        Assert.Equal(next, File.ReadAllBytes(stage));
        Assert.Equal(next, File.ReadAllBytes(stage + ".retained"));
        Assert.Equal(oldBytes, File.ReadAllBytes(Path.Combine(Root, old.Request.Output.Path)));
        Assert.Equal(1, ManifestStore.Load(configuration, old.ManifestPath).Revision);
    }

    private static System.Text.Json.Nodes.JsonObject LegacyJournal(System.Text.Json.Nodes.JsonNode journal)
    {
        var legacy = new System.Text.Json.Nodes.JsonObject();
        // Pre-authority journals had only these locator fields; absent authority_version deserializes as version zero.
        foreach (
            string field in new[]
            {
                "transaction_id",
                "asset_path",
                "manifest_path",
                "asset_stage_path",
                "manifest_stage_path",
                "asset_backup_path",
                "manifest_backup_path",
                "asset_existed",
                "manifest_existed",
                "asset_hash",
                "asset_length",
                "manifest_hash",
            }
        )
        {
            legacy[field] = journal[field]!.DeepClone();
        }
        return legacy;
    }

    private static void Interrupt(EffectiveConfiguration configuration, AssetManifest old)
    {
        byte[] next = "next"u8.ToArray();
        Assert.Throws<AtomicPublisher.SimulatedPublicationInterruptionException>(() =>
            AtomicPublisher.Publish(
                configuration,
                old.Request.Output.Path,
                next,
                old.ManifestPath,
                ManifestStore.Serialize(Manifest(next, 2)),
                new AtomicPublisher.PublicationTestHooks(AfterMove: move =>
                {
                    if (move == AtomicPublisher.PublicationMove.BackupAsset)
                        throw new AtomicPublisher.SimulatedPublicationInterruptionException();
                })
            )
        );
    }

    /// <summary>Removes only this test sandbox, including displaced admitted directories.</summary>
    public void Dispose()
    {
        if (Directory.Exists(_sandbox))
            Directory.Delete(_sandbox, recursive: true);
    }

    private EffectiveConfiguration Configuration()
    {
        Directory.CreateDirectory(Path.Combine(Root, "assets"));
        Directory.CreateDirectory(Path.Combine(Root, "catalog"));
        return new EffectiveConfiguration(
            Root,
            new AssetCtlPaths("assets", "catalog", "styles", ".assetctl/work", "runs", ".assetctl/state", "logs"),
            new AssetCtlPolicy(false, true, true, true, false, "reject"),
            new AssetCtlLimits(1_000_000, 1_000_000, 10, 10, 10, 30, 1_000_000),
            new SpendingLimits(0, 0, 0),
            new Dictionary<string, ProviderInstance>(StringComparer.Ordinal),
            [],
            [],
            new Dictionary<string, QualityTier>(StringComparer.Ordinal)
            {
                ["development"] = new("development", 1, 1, "disabled", true, 0),
            },
            new Dictionary<string, StyleProfile>(StringComparer.Ordinal)
            {
                ["engineering-icons"] = new("engineering-icons", "test", [], []),
            },
            new Dictionary<string, string>(StringComparer.Ordinal),
            "hash"
        );
    }

    private void WritePair(byte[] bytes, AssetManifest manifest)
    {
        File.WriteAllBytes(Path.Combine(Root, manifest.Request.Output.Path), bytes);
        File.WriteAllText(Path.Combine(Root, manifest.ManifestPath), ManifestStore.Serialize(manifest));
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
}
