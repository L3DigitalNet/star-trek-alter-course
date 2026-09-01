namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies atomic publication, process locking, and game-layer independence.</summary>
public sealed class PublishingAndBoundaryTests
{
    /// <summary>Restores the prior pair when the manifest's final rename cannot complete.</summary>
    [Fact]
    public void AtomicPublisherRestoresPriorAssetWhenManifestPublicationFails()
    {
        string root = Path.Combine(Path.GetTempPath(), "assetctl-publish-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        Directory.CreateDirectory(Path.Combine(root, "catalog"));
        byte[] oldBytes = "old-asset"u8.ToArray();
        byte[] newBytes = "new-asset"u8.ToArray();
        File.WriteAllBytes(Path.Combine(root, "assets", "asset.png"), oldBytes);
        global::AlterCourse.AssetCtl.Domain.DomainModels.EffectiveConfiguration configuration = Configuration(root);
        AssetManifest oldManifest = Manifest(oldBytes);
        AssetManifest newManifest = Manifest(newBytes) with { Revision = 2 };
        File.WriteAllText(Path.Combine(root, oldManifest.ManifestPath), ManifestStore.Serialize(oldManifest));
        try
        {
            Assert.Throws<IOException>(() =>
                AtomicPublisher.Publish(
                    configuration,
                    "assets/asset.png",
                    newBytes,
                    newManifest.ManifestPath,
                    ManifestStore.Serialize(newManifest),
                    new AtomicPublisher.PublicationTestHooks(BeforeMove: move =>
                    {
                        if (move == AtomicPublisher.PublicationMove.InstallManifest)
                        {
                            throw new IOException("simulated manifest rename failure");
                        }
                    })
                )
            );
            Assert.Equal("old-asset", File.ReadAllText(Path.Combine(root, "assets", "asset.png")));
            Assert.Equal(
                oldManifest.Integrity!.Sha256,
                ManifestStore.Load(configuration, oldManifest.ManifestPath).Integrity!.Sha256
            );
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Keeps the AssetCtl executable independent from Core and Godot assemblies.</summary>
    [Fact]
    public void ApplicationAssemblyDoesNotReferenceGameOrGodotAssemblies()
    {
        string?[] references = typeof(AtomicPublisher)
            .Assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToArray();
        Assert.DoesNotContain("AlterCourse.Core", references, StringComparer.Ordinal);
        Assert.DoesNotContain("AlterCourse.Godot", references, StringComparer.Ordinal);
        Assert.DoesNotContain(references, name => name?.StartsWith("Godot", StringComparison.Ordinal) == true);
    }

    /// <summary>Prevents two processes from publishing the same asset concurrently.</summary>
    [Fact]
    public void AssetLockRefusesConcurrentPublicationForSameId()
    {
        string root = Path.Combine(Path.GetTempPath(), "assetctl-lock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        global::AlterCourse.AssetCtl.Domain.DomainModels.EffectiveConfiguration configuration = Configuration(root);
        try
        {
            using var first = AssetLock.Acquire(configuration, "ui.test.asset");
            Assert.Throws<AssetCtlException>(() => AssetLock.Acquire(configuration, "ui.test.asset"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static EffectiveConfiguration Configuration(string root) =>
        new(
            root,
            new AssetCtlPaths("assets", "catalog", "styles", "work", "runs", "state", "logs"),
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

    private static AssetManifest Manifest(byte[] bytes) =>
        new(
            "1",
            TestData.Request() with
            {
                Output = TestData.Request().Output with { Path = "assets/asset.png" },
            },
            1,
            new RightsRecord("original-project-created", "project", null, null, "test"),
            null,
            null,
            null,
            new IntegrityRecord(
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)),
                bytes.LongLength,
                "image/png"
            ),
            new ApprovalRecord(null, null, null),
            null,
            "catalog/test.asset.yaml"
        );
}
