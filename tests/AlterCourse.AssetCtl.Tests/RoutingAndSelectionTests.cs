using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Routing;
using GenerationPlan = AlterCourse.AssetCtl.Domain.DomainModels.GenerationPlan;
using RouteFallbackPolicy = AlterCourse.AssetCtl.Domain.DomainModels.RouteFallbackPolicy;
using RouteRetryPolicy = AlterCourse.AssetCtl.Domain.DomainModels.RouteRetryPolicy;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies configuration-only routing, deterministic selection, and prompt stability.</summary>
public sealed class RoutingAndSelectionTests
{
    /// <summary>Routes a new adapter through configured capabilities without provider-name branches.</summary>
    [Fact]
    public void FakeAdapterRoutesWithoutCoreProviderChanges()
    {
        var fake = new FakeGenerator("new-protocol");
        var registry = new AdapterRegistry([fake]);
        var profile = new ModelProfile(
            "configured-model",
            "vendor-model",
            new HashSet<AssetCapability> { AssetCapability.RasterGenerate, AssetCapability.ImageTransparentOutput },
            0m,
            "fixed",
            new Dictionary<string, string>(StringComparer.Ordinal)
        );
        var provider = new ProviderInstance(
            "configuration-only-name",
            fake.AdapterId,
            true,
            null,
            null,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, ModelProfile>(StringComparer.Ordinal) { [profile.Id] = profile }
        );
        global::AlterCourse.AssetCtl.Domain.DomainModels.AssetRequest request = TestData.Request();
        global::AlterCourse.AssetCtl.Domain.DomainModels.EffectiveConfiguration configuration = Configuration(
            provider,
            request
        );
        global::AlterCourse.AssetCtl.Domain.DomainModels.GenerationPlan plan = new AssetRouter(registry).Plan(
            configuration,
            request
        );
        Assert.Equal("configuration-only-name", plan.SelectedTarget!.ProviderId);
        Assert.Equal("new-protocol", plan.SelectedTarget.AdapterId);
    }

    /// <summary>Explains every independent reason that makes a configured target ineligible.</summary>
    [Fact]
    public void RouterExplainsDisabledCredentialsCapabilityAndUnknownPrice()
    {
        var fake = new FakeGenerator("adapter");
        var registry = new AdapterRegistry([fake]);
        var profile = new ModelProfile(
            "model",
            "vendor",
            new HashSet<AssetCapability> { AssetCapability.RasterGenerate },
            null,
            "unknown",
            new Dictionary<string, string>(StringComparer.Ordinal)
        );
        var provider = new ProviderInstance(
            "opaque",
            fake.AdapterId,
            false,
            new Uri("https://example.test/v1"),
            "MISSING_ASSETCTL_TEST_KEY",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, ModelProfile>(StringComparer.Ordinal) { [profile.Id] = profile }
        );
        global::AlterCourse.AssetCtl.Domain.DomainModels.GenerationPlan plan = new AssetRouter(registry).Plan(
            Configuration(provider, TestData.Request()),
            TestData.Request()
        );
        IReadOnlyList<string> reasons = Assert.Single(plan.Targets).RejectionReasons;
        Assert.Contains("provider-disabled", reasons, StringComparer.Ordinal);
        Assert.Contains("external-generation-disabled", reasons, StringComparer.Ordinal);
        Assert.Contains("credential-missing:MISSING_ASSETCTL_TEST_KEY", reasons, StringComparer.Ordinal);
        Assert.Contains("model-capability-mismatch", reasons, StringComparer.Ordinal);
        Assert.Contains("unknown-price", reasons, StringComparer.Ordinal);
    }

    /// <summary>Filters failed candidates and resolves score ties by creation order.</summary>
    [Fact]
    public void CandidateSelectionFiltersHardFailuresAndBreaksTiesByCreationOrder()
    {
        byte[] bytes = new byte[] { 1 };
        var mechanical = new MechanicalValidationResult(
            true,
            "image/png",
            1,
            1,
            true,
            [],
            bytes,
            new Dictionary<int, byte[]>(0)
        );
        global::AlterCourse.AssetCtl.Domain.DomainModels.SemanticReviewResult review = Review(0.9);
        (
            global::AlterCourse.AssetCtl.Domain.DomainModels.GeneratedCandidate Candidate,
            global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult Mechanical,
            global::AlterCourse.AssetCtl.Domain.DomainModels.SemanticReviewResult? Review
        ) selected = CandidateSelector.Select(
            [
                (new GeneratedCandidate(2, bytes, "image/png", null, 0), mechanical, review),
                (new GeneratedCandidate(1, bytes, "image/png", null, 0), mechanical, review),
                (
                    new GeneratedCandidate(0, bytes, "image/png", null, 0),
                    mechanical with
                    {
                        Passed = false,
                    },
                    Review(1.0)
                ),
            ],
            new QualityTier("test", 3, 1, "required", false, 0.8)
        );
        Assert.Equal(1, selected.Candidate.CreationOrder);
    }

    /// <summary>Rejects a semantically valid candidate whose score is below the tier threshold.</summary>
    [Fact]
    public void CandidateSelectionEnforcesMinimumSemanticScore()
    {
        byte[] bytes = [1];
        var mechanical = new MechanicalValidationResult(
            true,
            "image/png",
            1,
            1,
            true,
            [],
            bytes,
            new Dictionary<int, byte[]>(0)
        );
        Assert.Throws<AssetCtlException>(() =>
            CandidateSelector.Select(
                [(new GeneratedCandidate(0, bytes, "image/png", null, 0), mechanical, Review(0.79))],
                new QualityTier("test", 1, 1, "required", false, 0.80)
            )
        );
    }

    /// <summary>Includes every reachable retry, fallback target, candidate, and reviewer attempt in plan cost.</summary>
    [Fact]
    public void PlanCostConservativelyIncludesFallbacksRetriesAndReviews()
    {
        var first = new FakeGenerator("first");
        var second = new FakeGenerator("second");
        var reviewer = new FakeReviewer();
        var registry = new AdapterRegistry([first, second, reviewer]);
        AssetRequest request = TestData.Request();
        ProviderInstance firstProvider = Provider(
            "first-provider",
            first.AdapterId,
            0.10m,
            AssetCapability.RasterGenerate
        );
        ProviderInstance secondProvider = Provider(
            "second-provider",
            second.AdapterId,
            0.10m,
            AssetCapability.RasterGenerate
        );
        ProviderInstance reviewProvider = Provider(
            "review-provider",
            reviewer.AdapterId,
            0.05m,
            AssetCapability.ReviewSemantic
        );
        var retry = new RouteRetryPolicy(2, 0, 0, 0, new HashSet<ProviderErrorCategory>());
        var fallback = new RouteFallbackPolicy(true, new HashSet<ProviderErrorCategory>());
        var route = new RouteDefinition(
            "generation",
            100,
            request.Lifecycle,
            request.Output.Format,
            AssetCapability.RasterGenerate,
            [new RouteTarget(firstProvider.Id, "profile"), new RouteTarget(secondProvider.Id, "profile")],
            0,
            fallback,
            retry
        );
        var reviewRoute = new RouteDefinition(
            "review",
            100,
            null,
            null,
            AssetCapability.ReviewSemantic,
            [new RouteTarget(reviewProvider.Id, "profile")],
            0,
            fallback,
            retry
        );
        var providers = new Dictionary<string, ProviderInstance>(StringComparer.Ordinal)
        {
            [firstProvider.Id] = firstProvider,
            [secondProvider.Id] = secondProvider,
            [reviewProvider.Id] = reviewProvider,
        };
        EffectiveConfiguration configuration = Configuration(providers, request, route, reviewRoute);

        GenerationPlan plan = new AssetRouter(registry).Plan(configuration, request);

        Assert.Equal(1.50m, plan.EstimatedMaximumCost);
    }

    /// <summary>Keeps prompt ordering and its content hash deterministic.</summary>
    [Fact]
    public void PromptOrderAndHashAreStable()
    {
        var style = new StyleProfile("style", "summary", ["style required"], ["style prohibited"]);
        (string Prompt, string Hash) first = PromptCompiler.Compile(TestData.Request(), style);
        (string Prompt, string Hash) second = PromptCompiler.Compile(TestData.Request(), style);
        Assert.Equal(first, second);
        Assert.True(
            first.Prompt.IndexOf("Purpose:", StringComparison.Ordinal)
                < first.Prompt.IndexOf("Output:", StringComparison.Ordinal)
        );
        Assert.True(
            first.Prompt.IndexOf("Output:", StringComparison.Ordinal)
                < first.Prompt.IndexOf("Style:", StringComparison.Ordinal)
        );
    }

    private static EffectiveConfiguration Configuration(ProviderInstance provider, AssetRequest request)
    {
        var route = new RouteDefinition(
            "route",
            100,
            request.Lifecycle,
            request.Output.Format,
            AssetCapability.RasterGenerate,
            [new RouteTarget(provider.Id, provider.Models.Keys.Single())],
            0
        );
        return new EffectiveConfiguration(
            "/repo",
            new AssetCtlPaths("assets", "catalog", "styles", "work", "runs", "state", "logs"),
            new AssetCtlPolicy(false, true, true, true, false, "reject"),
            new AssetCtlLimits(1_000_000, 1_000_000, 10, 10, 10, 30, 1_000_000),
            new SpendingLimits(1, 1, 1),
            new Dictionary<string, ProviderInstance>(StringComparer.Ordinal) { [provider.Id] = provider },
            [route],
            [],
            new Dictionary<string, QualityTier>(StringComparer.Ordinal)
            {
                [request.QualityTier] = new(request.QualityTier, 1, 1, "disabled", true, 0),
            },
            new Dictionary<string, StyleProfile>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal),
            "hash"
        );
    }

    private static EffectiveConfiguration Configuration(
        IReadOnlyDictionary<string, ProviderInstance> providers,
        AssetRequest request,
        RouteDefinition route,
        RouteDefinition reviewRoute
    ) =>
        new(
            "/repo",
            new AssetCtlPaths("assets", "catalog", "styles", "work", "runs", "state", "logs"),
            new AssetCtlPolicy(false, true, true, true, false, "reject"),
            new AssetCtlLimits(1_000_000, 1_000_000, 10, 10, 10, 30, 1_000_000),
            new SpendingLimits(10, 10, 10),
            providers,
            [route],
            [reviewRoute],
            new Dictionary<string, QualityTier>(StringComparer.Ordinal)
            {
                [request.QualityTier] = new(request.QualityTier, 3, 2, "required", false, 0.8),
            },
            new Dictionary<string, StyleProfile>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal),
            "hash"
        );

    private static ProviderInstance Provider(string id, string adapterId, decimal cost, AssetCapability capability)
    {
        var capabilities = new HashSet<AssetCapability> { capability };
        if (capability is AssetCapability.RasterGenerate)
        {
            capabilities.Add(AssetCapability.ImageTransparentOutput);
        }

        var profile = new ModelProfile(
            "profile",
            "vendor",
            capabilities,
            cost,
            "fixed-output",
            new Dictionary<string, string>(StringComparer.Ordinal)
        );
        return new ProviderInstance(
            id,
            adapterId,
            true,
            null,
            null,
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, ModelProfile>(StringComparer.Ordinal) { [profile.Id] = profile }
        );
    }

    private static SemanticReviewResult Review(double score) =>
        new(true, true, true, true, score, score, [], false, false, score, "pass", "different-provider-family");

    private sealed class FakeGenerator(string id) : IAssetGenerator
    {
        public string AdapterId => id;
        public IReadOnlySet<AssetCapability> SupportedCapabilities { get; } =
            new HashSet<AssetCapability> { AssetCapability.RasterGenerate, AssetCapability.ImageTransparentOutput };

        public IReadOnlySet<string> AllowedEndpointHosts { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) { }

        public Task<GenerationBatchResult> GenerateAsync(
            ProviderExecutionContext context,
            NormalizedGenerationRequest request,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();
    }

    private sealed class FakeReviewer : AlterCourse.AssetCtl.Providers.ProviderContracts.IAssetReviewer
    {
        public string AdapterId => "reviewer";

        public IReadOnlySet<AssetCapability> SupportedCapabilities { get; } =
            new HashSet<AssetCapability> { AssetCapability.ReviewSemantic };

        public IReadOnlySet<string> AllowedEndpointHosts { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) { }

        public Task<SemanticReviewResult> ReviewAsync(
            ProviderExecutionContext context,
            SemanticReviewRequest request,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();
    }
}
