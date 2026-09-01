using AlterCourse.AssetCtl.Cli;
using AlterCourse.AssetCtl.Configuration;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies versioned configuration values, schema documents, routes, and operational diagnostics.</summary>
public sealed class ConfigurationContractTests
{
    /// <summary>Accepts only the three version-one semantic-review policy values.</summary>
    [Theory]
    [InlineData("disabled")]
    [InlineData("when-available")]
    [InlineData("required")]
    public void SemanticReviewPolicyAcceptsClosedVersionedValues(string value)
    {
        _ = ConfigurationLoader.ParseSemanticReviewPolicy(value, "tier");
    }

    /// <summary>Rejects an unknown semantic-review policy rather than coercing it to an enabled mode.</summary>
    [Fact]
    public void SemanticReviewPolicyRejectsUnknownValue()
    {
        AssetCtlException exception = Assert.Throws<AssetCtlException>(() =>
            ConfigurationLoader.ParseSemanticReviewPolicy("best-effort", "tier")
        );

        Assert.Contains("tier.semantic_review", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Rejects an unknown output-transparency value through the loader-facing contract.</summary>
    [Fact]
    public void OutputTransparencyRejectsUnknownValue()
    {
        AssetCtlException exception = Assert.Throws<AssetCtlException>(() =>
            ConfigurationLoader.ParseOutputTransparency("transparent-ish", "output")
        );

        Assert.Contains("output.transparency", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Parses every tracked schema as a draft 2020-12 JSON Schema document.</summary>
    [Fact]
    public void TrackedSchemasAreValidJsonSchemaDocuments()
    {
        string repository = CliTypes.RepositoryLocator.Find(Environment.CurrentDirectory);

        IReadOnlyList<global::AlterCourse.AssetCtl.Domain.DomainModels.SchemaDocumentStatus> results =
            ConfigurationTypes.JsonSchemaDocumentValidator.ValidateTrackedSchemas(repository);

        Assert.NotEmpty(results);
        Assert.All(results, result => Assert.True(result.Valid));
        Assert.Contains(results, result => result.Path.EndsWith("routing.schema.json", StringComparison.Ordinal));
    }

    /// <summary>Rejects a parsed JSON document whose schema keywords violate the supported meta-contract.</summary>
    [Fact]
    public void SchemaValidationRejectsInvalidTypeKeyword()
    {
        string root = TemporaryRepository();
        try
        {
            string schemas = Path.Combine(root, "config", "assets", "schemas");
            Directory.CreateDirectory(schemas);
            File.WriteAllText(
                Path.Combine(schemas, "invalid.json"),
                """
                { "$schema": "https://json-schema.org/draft/2020-12/schema", "type": "imaginary" }
                """
            );

            Assert.Throws<AssetCtlException>(() =>
                ConfigurationTypes.JsonSchemaDocumentValidator.ValidateTrackedSchemas(root)
            );
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Rejects a validation keyword whose JSON type violates the draft 2020-12 meta-schema.</summary>
    [Fact]
    public void SchemaValidationRejectsInvalidMinimumKeywordType()
    {
        AssertInvalidSchema("""{ "$schema": "https://json-schema.org/draft/2020-12/schema", "minimum": "invalid" }""");
    }

    /// <summary>Rejects a malformed reference rather than treating it as an inert annotation.</summary>
    [Fact]
    public void SchemaValidationRejectsMalformedReference()
    {
        AssertInvalidSchema("""{ "$schema": "https://json-schema.org/draft/2020-12/schema", "$ref": 7 }""");
    }

    /// <summary>Rejects an unsupported dialect before schema evaluation can select ambiguous semantics.</summary>
    [Fact]
    public void SchemaValidationRejectsUnsupportedDialect()
    {
        AssertInvalidSchema("""{ "$schema": "https://example.invalid/unknown-dialect", "type": "object" }""");
    }

    /// <summary>Requires route identifiers to remain unique across generation and review routes.</summary>
    [Fact]
    public void ConfigurationLoadRejectsRouteIdDuplicatedAcrossRouteKinds()
    {
        string root = CopyTrackedConfiguration();
        try
        {
            string routing = Path.Combine(root, "config", "assets", "routing.yaml");
            File.WriteAllText(
                routing,
                File.ReadAllText(routing)
                    .Replace("default-semantic-review", "vector-placeholder", StringComparison.Ordinal)
            );

            Assert.Throws<AssetCtlException>(() => Loader().Load(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Preserves the strict tracked candidate-retention policy in the effective configuration.</summary>
    [Fact]
    public void EffectivePolicyPreservesRetainUnselectedCandidates()
    {
        string root = CopyTrackedConfiguration();
        try
        {
            string tracked = Path.Combine(root, "config", "assets", "assetctl.yaml");
            File.WriteAllText(
                tracked,
                File.ReadAllText(tracked)
                    .Replace(
                        "retain_unselected_candidates: false",
                        "retain_unselected_candidates: true",
                        StringComparison.Ordinal
                    )
            );

            Assert.True(Loader().Load(root).Policy.RetainUnselectedCandidates);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Applies the untracked local override after tracked policy without accepting secret fields.</summary>
    [Fact]
    public void LocalOverrideHasDeterministicPrecedenceOverTrackedPolicy()
    {
        string root = CopyTrackedConfiguration();
        try
        {
            string localRoot = Path.Combine(root, ".assetctl");
            Directory.CreateDirectory(localRoot);
            File.WriteAllText(
                Path.Combine(localRoot, "config.local.yaml"),
                """
                schema_version: "1"
                policy:
                  external_generation_enabled: true
                  local_placeholder_fallback: false
                spending:
                  maximum_estimated_cost_per_asset_usd: 0.25
                  maximum_estimated_cost_per_run_usd: 0.50
                  maximum_estimated_cost_per_day_usd: 0.75
                """
            );

            EffectiveConfiguration configuration = Loader().Load(root);

            Assert.True(configuration.Policy.ExternalGenerationEnabled);
            Assert.False(configuration.Policy.LocalPlaceholderFallback);
            Assert.True(configuration.Policy.ProtectApprovedAssets);
            Assert.False(configuration.Policy.RetainUnselectedCandidates);
            Assert.Equal(0.25m, configuration.Spending.PerAssetUsd);
            Assert.Equal(0.50m, configuration.Spending.PerRunUsd);
            Assert.Equal(0.75m, configuration.Spending.PerDayUsd);
            Assert.Contains(".assetctl/config.local.yaml", configuration.FileHashes.Keys, StringComparer.Ordinal);
            Assert.Equal(64, configuration.FileHashes[".assetctl/config.local.yaml"].Length);
            Assert.Equal(64, configuration.EffectiveHash.Length);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Keeps invocation-only CLI flags outside the merged configuration and its provenance hash.</summary>
    [Fact]
    public void InvocationFlagsDoNotBecomeHiddenConfigurationOverrides()
    {
        string root = CopyTrackedConfiguration();
        try
        {
            EffectiveConfiguration before = Loader().Load(root);
            var options = CliTypes.CliOptions.Parse(["--offline", "--dry-run"]);
            EffectiveConfiguration after = Loader().Load(root);

            Assert.True(options.Flag("offline"));
            Assert.True(options.Flag("dry-run"));
            Assert.Equal(before.FileHashes, after.FileHashes);
            Assert.Equal(before.EffectiveHash, after.EffectiveHash);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Rejects a non-boolean retained-candidate policy instead of coercing configuration text.</summary>
    [Fact]
    public void RetainUnselectedCandidatesRequiresStrictBoolean()
    {
        string root = CopyTrackedConfiguration();
        try
        {
            string tracked = Path.Combine(root, "config", "assets", "assetctl.yaml");
            File.WriteAllText(
                tracked,
                File.ReadAllText(tracked)
                    .Replace(
                        "retain_unselected_candidates: false",
                        "retain_unselected_candidates: keep",
                        StringComparison.Ordinal
                    )
            );

            Assert.Throws<AssetCtlException>(() => Loader().Load(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Rejects an unknown semantic-review value when loading the tracked tier document.</summary>
    [Fact]
    public void ConfigurationLoadRejectsUnknownSemanticReviewValue()
    {
        string root = CopyTrackedConfiguration();
        try
        {
            string tiers = Path.Combine(root, "config", "assets", "quality-tiers.yaml");
            File.WriteAllText(
                tiers,
                File.ReadAllText(tiers).Replace("when-available", "best-effort", StringComparison.Ordinal)
            );

            Assert.Throws<AssetCtlException>(() => Loader().Load(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Rejects route retry counts that exceed the globally named maximum-total-attempts limit.</summary>
    [Fact]
    public void RouteRetryMetadataCannotExceedGlobalAttemptLimit()
    {
        string root = CopyTrackedConfiguration();
        try
        {
            string routing = Path.Combine(root, "config", "assets", "routing.yaml");
            File.WriteAllText(
                routing,
                File.ReadAllText(routing)
                    .Replace(
                        "maximum_attempts_per_target: 2",
                        "maximum_attempts_per_target: 13",
                        StringComparison.Ordinal
                    )
            );

            AssetCtlException exception = Assert.Throws<AssetCtlException>(() => Loader().Load(root));
            Assert.Contains("maximum_total_attempts", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Rejects unknown route fallback error categories instead of silently treating them as retryable.</summary>
    [Fact]
    public void RouteFallbackRejectsUnknownErrorCategory()
    {
        string root = CopyTrackedConfiguration();
        try
        {
            string routing = Path.Combine(root, "config", "assets", "routing.yaml");
            File.WriteAllText(
                routing,
                File.ReadAllText(routing).Replace("rate-limit", "unknown-error", StringComparison.Ordinal)
            );

            Assert.Throws<AssetCtlException>(() => Loader().Load(root));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Reports writable operational roots and route cross-references without leaving probe files.</summary>
    [Fact]
    public void DoctorDiagnosticsAreExplicitAndLeaveNoPersistentMutation()
    {
        string root = CopyTrackedConfiguration();
        try
        {
            EffectiveConfiguration configuration = Loader().Load(root);

            IReadOnlyList<global::AlterCourse.AssetCtl.Domain.DomainModels.WritableRootStatus> roots =
                CliTypes.CommandApp.DoctorDiagnostics.CheckWritableRoots(configuration);
            global::AlterCourse.AssetCtl.Domain.DomainModels.RouteIntegrityStatus routes =
                CliTypes.CommandApp.DoctorDiagnostics.CheckRouteIntegrity(configuration);

            Assert.Equal(4, roots.Count);
            Assert.All(roots, status => Assert.True(status.Writable));
            Assert.True(routes.Valid);
            Assert.Equal(routes.GenerationRoutes + routes.ReviewRoutes, routes.FallbackPolicies);
            Assert.Equal(routes.GenerationRoutes + routes.ReviewRoutes, routes.RetryPolicies);
            Assert.Empty(Directory.EnumerateFiles(root, ".assetctl-doctor-*.tmp", SearchOption.AllDirectories));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static ConfigurationLoader Loader()
    {
        ConfigurationTypes.IAdapterDescriptor[] descriptors =
        {
            new FakeDescriptor("local-placeholder"),
            new FakeDescriptor("recraft-images"),
            new FakeDescriptor("openai-images"),
            new FakeDescriptor("xai-images"),
            new FakeDescriptor("openai-vision-review"),
        };
        return new ConfigurationLoader(descriptors.ToDictionary(value => value.AdapterId, StringComparer.Ordinal));
    }

    private static string CopyTrackedConfiguration()
    {
        string sourceRoot = CliTypes.RepositoryLocator.Find(Environment.CurrentDirectory);
        string targetRoot = TemporaryRepository();
        CopyDirectory(Path.Combine(sourceRoot, "config", "assets"), Path.Combine(targetRoot, "config", "assets"));
        Directory.CreateDirectory(Path.Combine(targetRoot, "src", "AlterCourse.Godot", "assets"));
        return targetRoot;
    }

    private static string TemporaryRepository()
    {
        string root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static void AssertInvalidSchema(string schema)
    {
        string root = TemporaryRepository();
        try
        {
            string schemas = Path.Combine(root, "config", "assets", "schemas");
            Directory.CreateDirectory(schemas);
            File.WriteAllText(Path.Combine(schemas, "invalid.json"), schema);
            Assert.Throws<AssetCtlException>(() =>
                ConfigurationTypes.JsonSchemaDocumentValidator.ValidateTrackedSchemas(root)
            );
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        foreach (string directory in Directory.EnumerateDirectories(source))
        {
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
        }
    }

    private sealed class FakeDescriptor(string adapterId) : ConfigurationTypes.IAdapterDescriptor
    {
        public string AdapterId { get; } = adapterId;

        public IReadOnlySet<AssetCapability> SupportedCapabilities { get; } =
            Enum.GetValues<AssetCapability>().ToHashSet();

        public IReadOnlySet<string> AllowedEndpointHosts { get; } =
            new HashSet<string>(
                ["external.api.recraft.ai", "api.openai.com", "api.x.ai"],
                StringComparer.OrdinalIgnoreCase
            );

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) { }
    }
}
