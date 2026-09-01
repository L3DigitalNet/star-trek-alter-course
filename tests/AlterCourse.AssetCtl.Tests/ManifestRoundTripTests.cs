using System.Globalization;
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

    /// <summary>Preserves every committed generation and semantic-review field through a manifest rewrite.</summary>
    [Fact]
    public void LifecycleMutationRoundTripsCompleteProvenanceAndReview()
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest observed = ManifestWithProvenance();
        WriteManifest(observed);

        AssetManifest loaded = ManifestStore.Load(configuration, observed.ManifestPath);
        AssetManifest replacement = loaded with { Revision = loaded.Revision + 1 };
        ManifestMutation.WriteCas(configuration, loaded, replacement);
        AssetManifest actual = ManifestStore.Load(configuration, observed.ManifestPath);

        Assert.Equal(observed.Generation, actual.Generation);
        Assert.Equal(observed.SemanticReview!.VisualDefects, actual.SemanticReview!.VisualDefects);
        Assert.Equal(
            observed.SemanticReview with
            {
                VisualDefects = actual.SemanticReview.VisualDefects,
            },
            actual.SemanticReview
        );
    }

    /// <summary>Preserves authored request policy independently from lifecycle and generated provenance.</summary>
    [Fact]
    public void SerializationRoundTripsImportanceAndAuthoredQualityTier()
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest expected = Manifest() with
        {
            Request = Manifest().Request with { Importance = "critical", QualityTier = "bespoke-preview" },
        };
        WriteManifest(expected);

        AssetManifest actual = ManifestStore.Load(configuration, expected.ManifestPath);

        Assert.Equal("critical", actual.Request.Importance);
        Assert.Equal("bespoke-preview", actual.Request.QualityTier);
        Assert.Contains(
            "quality_tier: 'bespoke-preview'",
            File.ReadAllText(Path.Combine(root, expected.ManifestPath)),
            StringComparison.Ordinal
        );
    }

    /// <summary>Preserves the exact canonical prompt bytes and their hash through YAML serialization.</summary>
    [Fact]
    public void SerializationRoundTripsMultilinePromptAndHashWithHostilePunctuation()
    {
        EffectiveConfiguration configuration = Configuration();
        string prompt = "Role: tactical ! alert #1\nKeep A & B distinct; render * literally.\nFinal line";
        string promptHash = ConfigurationLoader.Hash(prompt);
        AssetManifest expected = ManifestWithProvenance() with
        {
            Generation = ManifestWithProvenance().Generation! with { FinalPrompt = prompt, PromptSha256 = promptHash },
        };
        WriteManifest(expected);

        AssetManifest actual = ManifestStore.Load(configuration, expected.ManifestPath);

        Assert.Equal(prompt, actual.Generation!.FinalPrompt);
        Assert.Equal(promptHash, actual.Generation.PromptSha256);
        Assert.Equal(ConfigurationLoader.Hash(actual.Generation.FinalPrompt), actual.Generation.PromptSha256);
    }

    /// <summary>Rejects noncanonical prompt line endings instead of changing hash-bearing provenance bytes.</summary>
    [Fact]
    public void SerializationRejectsCarriageReturnsInFinalPrompt()
    {
        AssetManifest manifest = ManifestWithProvenance() with
        {
            Generation = ManifestWithProvenance().Generation! with { FinalPrompt = "first\r\nsecond" },
        };

        AssetCtlException exception = Assert.Throws<AssetCtlException>(() => ManifestStore.Serialize(manifest));

        Assert.Contains("canonical LF", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Fails closed when committed provenance names an unsupported adapter or schema contract.</summary>
    [Theory]
    [InlineData("adapter_version: '1'", "adapter_version: '999'")]
    [InlineData("provenance_schema_version: '1'", "provenance_schema_version: '999'")]
    [InlineData("semantic_rubric_version: '1'", "semantic_rubric_version: '999'")]
    public void ManifestRejectsUnsupportedProvenanceVersions(string current, string unsupported)
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest manifest = Manifest() with
        {
            Generation = new GenerationProvenance(
                DateTimeOffset.UtcNow,
                "run",
                "route",
                "provider",
                "adapter",
                "profile",
                "model",
                "development",
                "prompt",
                new string('1', 64),
                new string('2', 64),
                new string('3', 64),
                null,
                0,
                null
            ),
            MechanicalValidation = new MechanicalValidationResult(
                true,
                "image/png",
                1,
                1,
                false,
                [],
                [],
                new Dictionary<int, byte[]>()
            ),
            SemanticReview = new SemanticReviewResult(
                true,
                true,
                true,
                true,
                1,
                1,
                [],
                false,
                false,
                1,
                "pass",
                "local-only"
            ),
        };
        string serialized = ManifestStore.Serialize(manifest).Replace(current, unsupported, StringComparison.Ordinal);
        Assert.DoesNotContain(current, serialized, StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(root, manifest.ManifestPath), serialized);

        Assert.Throws<AssetCtlException>(() => ManifestStore.Load(configuration, manifest.ManifestPath));
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

    private static AssetManifest ManifestWithProvenance() =>
        Manifest() with
        {
            Generation = new GenerationProvenance(
                DateTimeOffset.Parse("2026-09-01T12:34:56Z", CultureInfo.InvariantCulture),
                "run-17",
                "vector-primary",
                "provider-a",
                "adapter-a",
                "profile-a",
                "model-a",
                "production-candidate",
                "final prompt",
                new string('1', 64),
                new string('2', 64),
                new string('3', 64),
                "provider-request-9",
                0.42m,
                0.39m,
                "1",
                "1"
            ),
            MechanicalValidation = new MechanicalValidationResult(
                true,
                "image/png",
                64,
                64,
                true,
                ["normalized"],
                [],
                new Dictionary<int, byte[]>()
            ),
            SemanticReview = new SemanticReviewResult(
                true,
                false,
                true,
                false,
                0.73,
                0.61,
                ["aliased edge", "ambiguous silhouette"],
                true,
                false,
                0.66,
                "fail",
                "different-provider-family",
                new string('4', 64),
                "reviewer-a",
                "review-profile-a",
                "1"
            ),
        };

    private void WriteManifest(AssetManifest manifest) =>
        File.WriteAllText(Path.Combine(root, manifest.ManifestPath), ManifestStore.Serialize(manifest));
}
