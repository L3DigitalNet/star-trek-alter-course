using System.Text.Json;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Routing;
using Microsoft.Extensions.Logging;
using IAdapterDescriptor = AlterCourse.AssetCtl.Configuration.ConfigurationTypes.IAdapterDescriptor;
using RouteFallbackPolicy = AlterCourse.AssetCtl.Domain.DomainModels.RouteFallbackPolicy;
using RouteRetryPolicy = AlterCourse.AssetCtl.Domain.DomainModels.RouteRetryPolicy;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Exercises the offline orchestration boundary with deterministic in-memory providers.</summary>
public sealed class GenerationEndToEndTests
{
    /// <summary>Generates, reviews, publishes, receipts, then reuses the current result without provider calls.</summary>
    [Fact]
    public async Task GenerateReviewPublishAndIdempotentReuseAreEndToEnd()
    {
        var generator = new ScriptedGenerator("fake-generator", request => Success(request.Request));
        var reviewer = new CapturingReviewer();
        using var fixture = new GenerationFixture([generator, reviewer], semanticReview: "required");

        object first = await fixture.Orchestrator.GenerateAsync(
            fixture.Configuration,
            fixture.Manifest,
            force: false,
            dryRun: false,
            offline: false,
            CancellationToken.None
        );

        Assert.True(File.Exists(Path.Combine(fixture.Root, fixture.Manifest.Request.Output.Path)));
        Assert.NotNull(reviewer.LastRequest);
        Assert.Equal("Engineering icon language.", reviewer.LastRequest.StyleSummary);
        Assert.Contains("simple silhouette", reviewer.LastRequest.StyleRequired!, StringComparer.Ordinal);
        Assert.Contains("watermark", reviewer.LastRequest.StyleProhibited!, StringComparer.Ordinal);
        Assert.Equal([16, 24, 64], reviewer.LastRequest.TargetPreviews.Keys.Order());

        string receiptPath = JsonDocument
            .Parse(JsonSerializer.Serialize(first, JsonOptions.Stable))
            .RootElement.GetProperty("receipt_path")
            .GetString()!;
        string receipt = await File.ReadAllTextAsync(Path.Combine(fixture.Root, receiptPath));
        AssertSuccessfulReceipt(receipt);

        AssetManifest current = ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath);
        AssertCurrentProvenance(fixture, current);
        object second = await fixture.Orchestrator.GenerateAsync(
            fixture.Configuration,
            current,
            force: false,
            dryRun: false,
            offline: false,
            CancellationToken.None
        );
        JsonElement result = JsonDocument.Parse(JsonSerializer.Serialize(second, JsonOptions.Stable)).RootElement;
        Assert.True(result.GetProperty("existing").GetBoolean());
        Assert.Equal(1, generator.Calls);
        Assert.Equal(1, reviewer.Calls);
    }

    private static void AssertSuccessfulReceipt(string receipt)
    {
        Assert.Contains("physical-attempt", receipt, StringComparison.Ordinal);
        Assert.Contains("review-attempt", receipt, StringComparison.Ordinal);
        Assert.Contains("selected_sha256", receipt, StringComparison.Ordinal);
        Assert.Contains("\"rollback\": \"not-required\"", receipt, StringComparison.Ordinal);
    }

    private static void AssertCurrentProvenance(GenerationFixture fixture, AssetManifest current)
    {
        string currentRequestHash = ConfigurationLoader.Hash(
            JsonSerializer.Serialize(current.Request, JsonOptions.Stable)
        );
        Assert.Equal(current.Generation!.RequestSha256, currentRequestHash);
        (string _, string currentPromptHash) = PromptCompiler.Compile(
            current.Request,
            fixture.Configuration.Styles[current.Request.StyleProfile]
        );
        Assert.Equal(current.Generation.PromptSha256, currentPromptHash);
        Assert.Equal(fixture.Configuration.EffectiveHash, current.Generation.EffectiveConfigSha256);
        Assert.Equal(64, current.SemanticReview!.EvidenceSha256!.Length);
    }

    /// <summary>Honors Retry-After inside configured bounds before a successful physical retry.</summary>
    [Fact]
    public async Task RetryAfterIsBoundedAndRecorded()
    {
        int invocation = 0;
        var generator = new ScriptedGenerator(
            "retry-generator",
            request =>
            {
                invocation++;
                if (invocation == 1)
                {
                    var exception = new ProviderException(ProviderErrorCategory.RateLimit, "retry", retryable: true);
                    exception.Data["AlterCourse.AssetCtl.RetryAfterDelay"] = TimeSpan.FromHours(1);
                    throw exception;
                }

                return Success(request.Request);
            }
        );
        using var fixture = new GenerationFixture(
            [generator],
            semanticReview: "disabled",
            retry: new RouteRetryPolicy(
                2,
                0,
                0,
                0,
                new HashSet<ProviderErrorCategory> { ProviderErrorCategory.RateLimit }
            )
        );

        object result = await fixture.Orchestrator.GenerateAsync(
            fixture.Configuration,
            fixture.Manifest,
            force: false,
            dryRun: false,
            offline: false,
            CancellationToken.None
        );

        Assert.Equal(2, generator.Calls);
        string receiptPath = JsonDocument
            .Parse(JsonSerializer.Serialize(result, JsonOptions.Stable))
            .RootElement.GetProperty("receipt_path")
            .GetString()!;
        string receipt = await File.ReadAllTextAsync(Path.Combine(fixture.Root, receiptPath));
        Assert.Contains("\"event_type\": \"retry\"", receipt, StringComparison.Ordinal);
        Assert.Contains("\"delay_milliseconds\": 0", receipt, StringComparison.Ordinal);
    }

    /// <summary>Falls through only when the active route permits the observed provider category.</summary>
    [Fact]
    public async Task AllowedCategoryFallsBackToNextTarget()
    {
        var failed = new ScriptedGenerator(
            "failed-generator",
            _ => throw new ProviderException(ProviderErrorCategory.Authentication, "denied")
        );
        var fallback = new ScriptedGenerator("fallback-generator", request => Success(request.Request));
        using var fixture = new GenerationFixture([failed, fallback], semanticReview: "disabled");

        await fixture.Orchestrator.GenerateAsync(
            fixture.Configuration,
            fixture.Manifest,
            force: false,
            dryRun: false,
            offline: false,
            CancellationToken.None
        );

        Assert.Equal(1, failed.Calls);
        Assert.Equal(1, fallback.Calls);
    }

    /// <summary>Stops at a hard category that the route did not admit for fallback.</summary>
    [Fact]
    public async Task DisallowedCategoryFailsClosedBeforeNextTarget()
    {
        var failed = new ScriptedGenerator(
            "failed-generator",
            _ => throw new ProviderException(ProviderErrorCategory.InvalidRequest, "invalid")
        );
        var fallback = new ScriptedGenerator("fallback-generator", request => Success(request.Request));
        using var fixture = new GenerationFixture([failed, fallback], semanticReview: "disabled");

        await Assert.ThrowsAsync<ProviderException>(() =>
            fixture.Orchestrator.GenerateAsync(
                fixture.Configuration,
                fixture.Manifest,
                force: false,
                dryRun: false,
                offline: false,
                CancellationToken.None
            )
        );

        Assert.Equal(1, failed.Calls);
        Assert.Equal(0, fallback.Calls);
    }

    /// <summary>Shares one physical-attempt ceiling across retries and fallback targets.</summary>
    [Fact]
    public async Task GlobalAttemptLimitCapsTheFallbackTree()
    {
        var failed = new ScriptedGenerator(
            "failed-generator",
            _ => throw new ProviderException(ProviderErrorCategory.TransientNetwork, "network", retryable: true)
        );
        var fallback = new ScriptedGenerator("fallback-generator", request => Success(request.Request));
        var retry = new RouteRetryPolicy(
            5,
            0,
            0,
            0,
            new HashSet<ProviderErrorCategory> { ProviderErrorCategory.TransientNetwork }
        );
        using var fixture = new GenerationFixture(
            [failed, fallback],
            semanticReview: "disabled",
            retry,
            maximumTotalAttempts: 2
        );

        await Assert.ThrowsAsync<AssetCtlException>(() =>
            fixture.Orchestrator.GenerateAsync(
                fixture.Configuration,
                fixture.Manifest,
                force: false,
                dryRun: false,
                offline: false,
                CancellationToken.None
            )
        );

        Assert.Equal(2, failed.Calls);
        Assert.Equal(0, fallback.Calls);
    }

    /// <summary>Degrades to the safe logger when the structured sink path cannot be created.</summary>
    [Fact]
    public void LoggingSinkFailureDoesNotEscapeComposition()
    {
        using ILoggerFactory factory = Program.CreateLoggerFactory("\0");
        Microsoft.Extensions.Logging.ILogger logger = factory.CreateLogger("test");
        Assert.NotNull(logger);
    }

    private static GenerationBatchResult Success(AssetRequest request) =>
        new(
            [new GeneratedCandidate(0, LocalPlaceholderGenerator.RenderPng(request), "image/png", "request-1", 0m)],
            "request-1",
            0m
        );

    private sealed class ScriptedGenerator(
        string adapterId,
        Func<NormalizedGenerationRequest, GenerationBatchResult> invoke
    ) : IAssetGenerator
    {
        public string AdapterId => adapterId;

        public int Calls { get; private set; }

        public IReadOnlySet<AssetCapability> SupportedCapabilities { get; } =
            new HashSet<AssetCapability> { AssetCapability.RasterGenerate, AssetCapability.ImageTransparentOutput };

        public IReadOnlySet<string> AllowedEndpointHosts { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) { }

        public Task<GenerationBatchResult> GenerateAsync(
            ProviderExecutionContext context,
            NormalizedGenerationRequest request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            return Task.FromResult(invoke(request));
        }
    }

    private sealed class CapturingReviewer : AlterCourse.AssetCtl.Providers.ProviderContracts.IAssetReviewer
    {
        public string AdapterId => "fake-reviewer";

        public int Calls { get; private set; }

        public SemanticReviewRequest? LastRequest { get; private set; }

        public IReadOnlySet<AssetCapability> SupportedCapabilities { get; } =
            new HashSet<AssetCapability> { AssetCapability.ReviewSemantic };

        public IReadOnlySet<string> AllowedEndpointHosts { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) { }

        public Task<SemanticReviewResult> ReviewAsync(
            ProviderExecutionContext context,
            SemanticReviewRequest request,
            CancellationToken cancellationToken
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            LastRequest = request;
            return Task.FromResult(
                new SemanticReviewResult(
                    true,
                    true,
                    true,
                    true,
                    0.95,
                    0.96,
                    [],
                    false,
                    false,
                    0.95,
                    "pass",
                    "different-provider-family"
                )
            );
        }
    }

    private sealed class GenerationFixture : IDisposable
    {
        public GenerationFixture(
            IReadOnlyList<IAdapterDescriptor> adapters,
            string semanticReview,
            RouteRetryPolicy? retry = null,
            int maximumTotalAttempts = 12
        )
        {
            Root = Path.Combine(Path.GetTempPath(), "assetctl-e2e-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            AssetRequest request = CreateRequest();
            var providers = adapters.ToDictionary(
                adapter => adapter.AdapterId,
                adapter => Provider(adapter),
                StringComparer.Ordinal
            );
            (RouteDefinition generationRoute, RouteDefinition[] reviewRoutes) = Routes(adapters, retry);
            Configuration = CreateConfiguration(
                Root,
                request,
                semanticReview,
                providers,
                generationRoute,
                reviewRoutes,
                maximumTotalAttempts
            );
            Manifest = CreateManifest(request);
            string manifestPath = Path.Combine(Root, Manifest.ManifestPath);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            File.WriteAllText(manifestPath, ManifestStore.Serialize(Manifest));
            string outputPath = Path.Combine(Root, Manifest.Request.Output.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, LocalPlaceholderGenerator.RenderPng(request));
            var registry = new AdapterRegistry(adapters);
            Orchestrator = new GenerationOrchestrator(registry, new AssetRouter(registry));
        }

        private static AssetRequest CreateRequest() =>
            TestData.Request() with
            {
                Output = TestData.Request().Output with { Path = "src/AlterCourse.Godot/assets/test/placeholder.png" },
            };

        private static (RouteDefinition Generation, RouteDefinition[] Review) Routes(
            IReadOnlyList<IAdapterDescriptor> adapters,
            RouteRetryPolicy? retry
        )
        {
            string[] generatorIds = adapters
                .Where(adapter => adapter is IAssetGenerator)
                .Select(adapter => adapter.AdapterId)
                .ToArray();
            RouteDefinition generation = new(
                "generation",
                100,
                AssetLifecycle.Placeholder,
                AssetFormat.Png,
                AssetCapability.RasterGenerate,
                generatorIds.Select(id => new RouteTarget(id, "profile")).ToArray(),
                0,
                new RouteFallbackPolicy(
                    true,
                    new HashSet<ProviderErrorCategory>
                    {
                        ProviderErrorCategory.Authentication,
                        ProviderErrorCategory.RateLimit,
                        ProviderErrorCategory.TransientNetwork,
                        ProviderErrorCategory.Validation,
                    }
                ),
                retry ?? new RouteRetryPolicy(1, 0, 0, 0, new HashSet<ProviderErrorCategory>())
            );
            RouteDefinition[] review = adapters.Any(adapter => adapter is CapturingReviewer) ? [ReviewRoute()] : [];
            return (generation, review);
        }

        private static RouteDefinition ReviewRoute() =>
            new(
                "review",
                100,
                null,
                null,
                AssetCapability.ReviewSemantic,
                [new RouteTarget("fake-reviewer", "profile")],
                0,
                new RouteFallbackPolicy(true, new HashSet<ProviderErrorCategory>()),
                new RouteRetryPolicy(1, 0, 0, 0, new HashSet<ProviderErrorCategory>())
            );

        private static EffectiveConfiguration CreateConfiguration(
            string root,
            AssetRequest request,
            string semanticReview,
            IReadOnlyDictionary<string, ProviderInstance> providers,
            RouteDefinition generationRoute,
            IReadOnlyList<RouteDefinition> reviewRoutes,
            int maximumTotalAttempts
        ) =>
            new(
                root,
                new AssetCtlPaths(
                    "src/AlterCourse.Godot/assets",
                    "config/assets/catalog",
                    "config/assets/styles",
                    ".assetctl/work",
                    ".assetctl/runs",
                    ".assetctl/state",
                    ".assetctl/logs"
                ),
                new AssetCtlPolicy(true, true, true, true, false, "reject"),
                new AssetCtlLimits(1_000_000, 1_000_000, 4, maximumTotalAttempts, 10, 30, 1_000_000),
                new SpendingLimits(10m, 10m, 10m),
                providers,
                [generationRoute],
                reviewRoutes,
                new Dictionary<string, QualityTier>(StringComparer.Ordinal)
                {
                    [request.QualityTier] = new(request.QualityTier, 1, 1, semanticReview, true, 0.80),
                },
                new Dictionary<string, StyleProfile>(StringComparer.Ordinal)
                {
                    [request.StyleProfile] = new(
                        request.StyleProfile,
                        "Engineering icon language.",
                        ["strong outline"],
                        ["photorealism"]
                    ),
                },
                new Dictionary<string, string>(StringComparer.Ordinal) { ["config"] = "hash" },
                "effective-hash"
            );

        private static AssetManifest CreateManifest(AssetRequest request) =>
            new(
                "1",
                request,
                0,
                new RightsRecord("unreviewed-generated-placeholder", null, null, null, null),
                null,
                null,
                null,
                null,
                new ApprovalRecord(null, null, null),
                null,
                "config/assets/catalog/test.asset.yaml"
            );

        public string Root { get; }

        public EffectiveConfiguration Configuration { get; }

        public AssetManifest Manifest { get; }

        public GenerationOrchestrator Orchestrator { get; }

        public void Dispose() => Directory.Delete(Root, recursive: true);

        private static ProviderInstance Provider(IAdapterDescriptor adapter)
        {
            IReadOnlySet<AssetCapability> capabilities =
                adapter is IAssetGenerator
                    ? new HashSet<AssetCapability>
                    {
                        AssetCapability.RasterGenerate,
                        AssetCapability.ImageTransparentOutput,
                    }
                    : new HashSet<AssetCapability> { AssetCapability.ReviewSemantic };
            var model = new ModelProfile(
                "profile",
                "vendor-model",
                capabilities,
                0m,
                "fixed-output",
                new Dictionary<string, string>(StringComparer.Ordinal)
            );
            return new ProviderInstance(
                adapter.AdapterId,
                adapter.AdapterId,
                true,
                null,
                null,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, ModelProfile>(StringComparer.Ordinal) { [model.Id] = model }
            );
        }
    }
}
