namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies atomic publication, process locking, and game-layer independence.</summary>
public sealed class PublishingAndBoundaryTests
{
    /// <summary>Restores the prior asset when its paired manifest cannot be published.</summary>
    [Fact]
    public void AtomicPublisherRestoresPriorAssetWhenManifestPublicationFails()
    {
        string root = Path.Combine(Path.GetTempPath(), "assetctl-publish-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        Directory.CreateDirectory(Path.Combine(root, "catalog", "blocked.asset.yaml"));
        File.WriteAllText(Path.Combine(root, "assets", "asset.png"), "old-asset");
        global::AlterCourse.AssetCtl.Domain.DomainModels.EffectiveConfiguration configuration = Configuration(root);
        try
        {
            Assert.ThrowsAny<IOException>(() => AtomicPublisher.Publish(configuration, "assets/asset.png", "new-asset"u8.ToArray(), "catalog/blocked.asset.yaml", "new-manifest"));
            Assert.Equal("old-asset", File.ReadAllText(Path.Combine(root, "assets", "asset.png")));
            Assert.True(Directory.Exists(Path.Combine(root, "catalog", "blocked.asset.yaml")));
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
        string?[] references = typeof(AtomicPublisher).Assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();
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

    private static EffectiveConfiguration Configuration(string root) => new(root, new AssetCtlPaths("assets", "catalog", "styles", "work", "runs", "state", "logs"), new AssetCtlPolicy(false, true, true, true, false, "reject"), new AssetCtlLimits(1_000_000, 1_000_000, 10, 10, 10, 30, 1_000_000), new SpendingLimits(0, 0, 0), new Dictionary<string, ProviderInstance>(StringComparer.Ordinal), [], [], new Dictionary<string, QualityTier>(StringComparer.Ordinal), new Dictionary<string, StyleProfile>(StringComparer.Ordinal), new Dictionary<string, string>(StringComparer.Ordinal), "hash");
}
