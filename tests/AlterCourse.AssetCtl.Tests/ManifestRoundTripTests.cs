using System.Security.Cryptography;
using AssetReference = AlterCourse.AssetCtl.Domain.DomainModels.AssetReference;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies manifest parsing and mutation preserve the complete tracked contract.</summary>
public sealed class ManifestRoundTripTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "assetctl-manifest-roundtrip-" + Guid.NewGuid().ToString("N")
    );

    /// <summary>Preserves reference hashes, rights bases, and rights evidence through serialization.</summary>
    [Fact]
    public void SerializationRoundTripsReferencesAndRights()
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest expected = Manifest() with
        {
            Request = Manifest().Request with
            {
                References =
                [
                    new AssetReference("references/bridge.png", new string('a', 64), "CC-BY-4.0"),
                    new AssetReference("references/panel.svg", new string('b', 64), "project-original"),
                ],
            },
            Rights = new RightsRecord(
                "third-party-licensed",
                "CC-BY-4.0",
                "Example Artist",
                "https://example.invalid/source",
                "Reference-only use."
            ),
        };
        WriteManifest(expected);

        AssetManifest actual = ManifestStore.Load(configuration, expected.ManifestPath);

        Assert.Equal(expected.Request.References, actual.Request.References);
        Assert.Equal(expected.Rights, actual.Rights);
    }

    /// <summary>Preserves references and rights while lifecycle compare-and-swap rewrites the manifest.</summary>
    [Fact]
    public void LifecycleMutationRoundTripsReferencesAndRights()
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest observed = Manifest() with
        {
            Request = Manifest().Request with
            {
                References = [new AssetReference("references/source.png", new string('c', 64), "project-original")],
            },
            Rights = new RightsRecord("original-project-created", "project", "crew", "local", "retained"),
        };
        WriteManifest(observed);
        AssetManifest replacement = observed with { Revision = observed.Revision + 1 };

        ManifestMutation.WriteCas(configuration, observed, replacement);
        AssetManifest actual = ManifestStore.Load(configuration, observed.ManifestPath);

        Assert.Equal(observed.Request.References, actual.Request.References);
        Assert.Equal(observed.Rights, actual.Rights);
    }

    /// <summary>Rejects unknown transparency instead of silently weakening it to optional.</summary>
    [Fact]
    public void ManifestRejectsUnknownTransparency()
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest manifest = Manifest();
        string serialized = ManifestStore
            .Serialize(manifest)
            .Replace("transparency: 'required'", "transparency: 'sometimes'", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(root, manifest.ManifestPath), serialized);

        AssetCtlException exception = Assert.Throws<AssetCtlException>(() =>
            ManifestStore.Load(configuration, manifest.ManifestPath)
        );

        Assert.Contains("transparency", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Rejects a catalog symlink that resolves outside the configured catalog before reading it.</summary>
    [Fact]
    public void ManifestLoadRejectsCatalogSymlinkEscape()
    {
        EffectiveConfiguration configuration = Configuration();
        string outside = Path.Combine(root, "catalog-sibling");
        Directory.CreateDirectory(outside);
        File.WriteAllText(Path.Combine(outside, "escaped.asset.yaml"), ManifestStore.Serialize(Manifest()));
        Directory.CreateSymbolicLink(Path.Combine(root, "catalog", "escaped"), outside);
        Assert.Throws<AssetCtlException>(() => ManifestStore.Load(configuration, "catalog/escaped/escaped.asset.yaml"));
    }

    /// <summary>Removes the isolated repository used by each manifest contract test.</summary>
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

    private static AssetManifest Manifest()
    {
        byte[] bytes = "asset"u8.ToArray();
        return new AssetManifest(
            "1",
            TestData.Request() with
            {
                Output = TestData.Request().Output with { Path = "assets/test.png" },
            },
            1,
            new RightsRecord("original-project-created", "project", null, null, "test"),
            null,
            null,
            null,
            new IntegrityRecord(Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.LongLength, "image/png"),
            new ApprovalRecord(null, null, null),
            null,
            "catalog/test.asset.yaml"
        );
    }

    private void WriteManifest(AssetManifest manifest) =>
        File.WriteAllText(Path.Combine(root, manifest.ManifestPath), ManifestStore.Serialize(manifest));
}
