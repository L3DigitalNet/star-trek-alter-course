using System.Text.Json;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Routing;
using Microsoft.Extensions.Logging;
using AssetReference = AlterCourse.AssetCtl.Domain.DomainModels.AssetReference;
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
        AssetManifest published = ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath);
        Assert.Equal($"{published.Generation!.RunId}.json", Path.GetFileName(receiptPath));
        string receipt = await File.ReadAllTextAsync(Path.Combine(fixture.Root, receiptPath));
        AssertSuccessfulReceipt(receipt);
        Assert.DoesNotContain("normalized_bytes", receipt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("target_previews", receipt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(Convert.ToBase64String(reviewer.LastRequest.Original), receipt, StringComparison.Ordinal);

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

    /// <summary>Regenerates instead of reusing a manifest whose semantic digest was fabricated.</summary>
    [Fact]
    public async Task IdempotentReuseRejectsFabricatedSemanticEvidenceDigest()
    {
        var generator = new ScriptedGenerator("fake-generator", request => Success(request.Request));
        var reviewer = new CapturingReviewer();
        using var fixture = new GenerationFixture([generator, reviewer], semanticReview: "required");

        await fixture.GenerateAsync();
        AssetManifest current = ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath);
        AssetManifest fabricated = current with
        {
            SemanticReview = current.SemanticReview! with { EvidenceSha256 = new string('a', 64) },
        };
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root, fabricated.ManifestPath),
            ManifestStore.Serialize(fabricated)
        );

        await fixture.Orchestrator.GenerateAsync(
            fixture.Configuration,
            fabricated,
            force: false,
            dryRun: false,
            offline: false,
            CancellationToken.None
        );

        Assert.Equal(2, generator.Calls);
        Assert.Equal(2, reviewer.Calls);
    }

    /// <summary>Rejects a same-family generator and required reviewer before either adapter can spend.</summary>
    [Fact]
    public async Task RequiredReviewerIndependenceIsEnforcedBeforeGeneration()
    {
        var generator = new ScriptedGenerator("openai-images", request => Success(request.Request));
        var reviewer = new CapturingReviewer("openai-vision-review");
        using var fixture = new GenerationFixture([generator, reviewer], semanticReview: "required");

        AssetCtlException exception = await Assert.ThrowsAsync<AssetCtlException>(() => fixture.GenerateAsync());

        Assert.Equal(5, exception.ExitCode);
        Assert.Equal(0, generator.Calls);
        Assert.Equal(0, reviewer.Calls);
    }

    /// <summary>Retains generated evidence when a normalized malformed review response fails the run.</summary>
    [Fact]
    public async Task MalformedReviewFailureReceiptPreservesCandidateEvidenceAndInvocation()
    {
        var generator = new ScriptedGenerator("evidence-generator", request => Success(request.Request));
        var reviewer = new FailingReviewer();
        using var fixture = new GenerationFixture(
            [generator, reviewer],
            semanticReview: "required",
            retainUnselectedCandidates: true
        );

        await Assert.ThrowsAsync<ProviderException>(() => fixture.GenerateAsync());

        string receiptPath = Assert.Single(
            Directory.EnumerateFiles(Path.Combine(fixture.Root, fixture.Configuration.Paths.ReceiptRoot), "*.json")
        );
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath));
        JsonElement root = document.RootElement;
        Assert.False(root.GetProperty("invocation").GetProperty("force").GetBoolean());
        Assert.False(root.GetProperty("invocation").GetProperty("offline").GetBoolean());
        JsonElement candidate = Assert.Single(root.GetProperty("candidates").EnumerateArray());
        string retainedPath = candidate.GetProperty("retained_path").GetString()!;
        Assert.True(File.Exists(Path.Combine(fixture.Root, retainedPath)));
        Assert.Equal(64, candidate.GetProperty("sha256").GetString()!.Length);
        Assert.True(candidate.GetProperty("mechanical").GetProperty("passed").GetBoolean());
        Assert.Equal(JsonValueKind.Null, candidate.GetProperty("semantic").ValueKind);
        Assert.Contains(
            root.GetProperty("attempts").EnumerateArray(),
            item =>
                string.Equals(item.GetProperty("event_type").GetString(), "review-attempt", StringComparison.Ordinal)
        );
        Assert.DoesNotContain("sig=secret", root.GetRawText(), StringComparison.Ordinal);
    }

    private static void AssertSuccessfulReceipt(string receipt)
    {
        Assert.Contains("physical-attempt", receipt, StringComparison.Ordinal);
        Assert.Contains("review-attempt", receipt, StringComparison.Ordinal);
        Assert.Contains("selected_sha256", receipt, StringComparison.Ordinal);
        Assert.Contains("\"rollback\": \"not-required\"", receipt, StringComparison.Ordinal);
        Assert.Contains("\"repository_state_sha256\"", receipt, StringComparison.Ordinal);
        Assert.Contains("\"arguments\"", receipt, StringComparison.Ordinal);
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

    /// <summary>Passes the active route's retry ceiling into the provider HTTP boundary.</summary>
    [Fact]
    public async Task ProviderContextUsesConfiguredRetryAfterCeiling()
    {
        var generator = new ScriptedGenerator("retry-context", request => Success(request.Request));
        using var fixture = new GenerationFixture(
            [generator],
            semanticReview: "disabled",
            retry: new RouteRetryPolicy(1, 0, 123, 0, new HashSet<ProviderErrorCategory>())
        );

        await fixture.GenerateAsync();

        Assert.Equal(123, generator.LastContext!.MaximumRetryAfterDelayMilliseconds);
    }

    /// <summary>Prices only endpoint-less targets when an offline invocation builds its executable plan.</summary>
    [Fact]
    public async Task OfflineGenerationExcludesUnreachableExternalSpendBeforeBudgetChecks()
    {
        var external = new ScriptedGenerator("external", request => Success(request.Request));
        var local = new LocalPlaceholderGenerator();
        using var fixture = new GenerationFixture([external, local], semanticReview: "disabled");
        var providers = fixture.Configuration.Providers.ToDictionary(
            pair => pair.Key,
            pair => pair.Value,
            StringComparer.Ordinal
        );
        ProviderInstance externalProvider = providers[external.AdapterId];
        ModelProfile externalModel = externalProvider.Models["profile"] with { EstimatedCostPerOutput = 100m };
        providers[external.AdapterId] = externalProvider with
        {
            Endpoint = new Uri("https://provider.example/v1"),
            Models = new Dictionary<string, ModelProfile>(StringComparer.Ordinal)
            {
                [externalModel.Id] = externalModel,
            },
        };
        EffectiveConfiguration configuration = fixture.Configuration with
        {
            Providers = EffectiveConfiguration.ReadOnly(providers),
        };

        object result = await fixture.Orchestrator.GenerateAsync(
            configuration,
            fixture.Manifest,
            force: false,
            dryRun: true,
            offline: true,
            CancellationToken.None
        );

        JsonElement plan = JsonDocument
            .Parse(JsonSerializer.Serialize(result, JsonOptions.Stable))
            .RootElement.GetProperty("plan");
        Assert.Equal(0m, plan.GetProperty("estimated_maximum_cost").GetDecimal());
        Assert.Equal(local.AdapterId, plan.GetProperty("selected_target").GetProperty("adapter_id").GetString());
    }

    /// <summary>Allows explicitly accepted unknown prices while retaining attempt and daily controls.</summary>
    [Fact]
    public async Task UnknownPriceAllowReachesGenerationInsteadOfFailingPlanConstruction()
    {
        var generator = new ScriptedGenerator("unknown-price", request => Success(request.Request));
        using var fixture = new GenerationFixture([generator], semanticReview: "disabled");
        ProviderInstance provider = fixture.Configuration.Providers[generator.AdapterId];
        ModelProfile model = provider.Models["profile"] with { EstimatedCostPerOutput = null };
        var providers = new Dictionary<string, ProviderInstance>(StringComparer.Ordinal)
        {
            [provider.Id] = provider with
            {
                Models = new Dictionary<string, ModelProfile>(StringComparer.Ordinal) { [model.Id] = model },
            },
        };
        EffectiveConfiguration configuration = fixture.Configuration with
        {
            Policy = fixture.Configuration.Policy with { UnknownPricePolicy = "allow" },
            Providers = EffectiveConfiguration.ReadOnly(providers),
        };

        object result = await fixture.GenerateAsync(configuration);

        Assert.Equal(1, generator.Calls);
        AssetManifest published = ManifestStore.Load(configuration, fixture.Manifest.ManifestPath);
        Assert.Null(published.Generation!.EstimatedCostUsd);
        JsonElement output = JsonDocument.Parse(JsonSerializer.Serialize(result, JsonOptions.Stable)).RootElement;
        Assert.Equal(JsonValueKind.Null, output.GetProperty("estimated_cost_usd").ValueKind);
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

    /// <summary>Retains only the selected candidate unless diagnostics retention is explicitly enabled.</summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task CandidateRetentionFollowsConfiguredPolicy(bool retainUnselected, int expectedFiles)
    {
        var generator = new ScriptedGenerator(
            "retention-generator",
            request => new GenerationBatchResult(
                [
                    new GeneratedCandidate(
                        0,
                        LocalPlaceholderGenerator.RenderPng(request.Request),
                        "image/png",
                        "one",
                        0m
                    ),
                    new GeneratedCandidate(
                        1,
                        LocalPlaceholderGenerator.RenderPng(request.Request),
                        "image/png",
                        "two",
                        0m
                    ),
                ],
                "batch",
                0m
            )
        );
        using var fixture = new GenerationFixture(
            [generator],
            "disabled",
            retainUnselectedCandidates: retainUnselected,
            candidates: 2
        );

        await fixture.GenerateAsync();

        AssetManifest current = ManifestStore.Load(fixture.Configuration, fixture.Manifest.ManifestPath);
        string candidateRoot = Path.Combine(
            fixture.Root,
            fixture.Configuration.Paths.WorkRoot,
            current.Generation!.RunId
        );
        Assert.Equal(expectedFiles, Directory.EnumerateFiles(candidateRoot).Count());
    }

    /// <summary>Retains failed candidates only when diagnostic retention is enabled while preserving receipt hashes.</summary>
    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task FailureCandidateRetentionFollowsConfiguredPolicy(bool retainUnselected, int expectedFiles)
    {
        var generator = new ScriptedGenerator("failure-retention", request => Success(request.Request));
        var reviewer = new FailingReviewer();
        using var fixture = new GenerationFixture(
            [generator, reviewer],
            semanticReview: "required",
            retainUnselectedCandidates: retainUnselected
        );

        await Assert.ThrowsAsync<ProviderException>(() => fixture.GenerateAsync());

        string receiptPath = Assert.Single(
            Directory.EnumerateFiles(Path.Combine(fixture.Root, fixture.Configuration.Paths.ReceiptRoot), "*.json")
        );
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(receiptPath));
        JsonElement candidate = Assert.Single(document.RootElement.GetProperty("candidates").EnumerateArray());
        Assert.Equal(64, candidate.GetProperty("sha256").GetString()!.Length);
        Assert.Equal(
            retainUnselected ? JsonValueKind.String : JsonValueKind.Null,
            candidate.GetProperty("retained_path").ValueKind
        );
        string candidateRoot = Path.Combine(fixture.Root, fixture.Configuration.Paths.WorkRoot);
        int retained = Directory.Exists(candidateRoot)
            ? Directory.EnumerateFiles(candidateRoot, "failure-candidate-*", SearchOption.AllDirectories).Count()
            : 0;
        Assert.Equal(expectedFiles, retained);
    }

    /// <summary>Returns Godot paths relative to an alternate configured asset root.</summary>
    [Fact]
    public async Task GenerationResultUsesConfiguredGodotAssetRoot()
    {
        var generator = new ScriptedGenerator("alternate-root", request => Success(request.Request));
        using var fixture = new GenerationFixture([generator], "disabled", godotAssetRoot: "game/content");

        object result = await fixture.GenerateAsync();
        JsonElement json = JsonDocument.Parse(JsonSerializer.Serialize(result, JsonOptions.Stable)).RootElement;

        Assert.Equal("res://test/placeholder.png", json.GetProperty("godot_path").GetString());
    }

    /// <summary>Preserves billed-attempt evidence when a provider fails before producing candidates.</summary>
    [Fact]
    public async Task BillableProviderFailureWritesNonAuthoritativeFailureReceipt()
    {
        var generator = new ScriptedGenerator(
            "billable-failure",
            _ => throw new ProviderException(ProviderErrorCategory.Authentication, "denied?token=secret")
        );
        using var fixture = new GenerationFixture([generator], "disabled", modelCost: 0.25m);

        await Assert.ThrowsAsync<AssetCtlException>(() => fixture.GenerateAsync());

        string receiptPath = Assert.Single(
            Directory.EnumerateFiles(Path.Combine(fixture.Root, fixture.Configuration.Paths.ReceiptRoot), "*.json")
        );
        string receipt = await File.ReadAllTextAsync(receiptPath);
        Assert.Contains("\"status\": \"failure\"", receipt, StringComparison.Ordinal);
        Assert.Contains("\"estimated_maximum_cost_usd\": 0.25", receipt, StringComparison.Ordinal);
        Assert.Contains("\"repository_path\": null", receipt, StringComparison.Ordinal);
        Assert.DoesNotContain("token=secret", receipt, StringComparison.Ordinal);
    }

    /// <summary>Keeps installed publication truth when the primary receipt sink fails afterward.</summary>
    [Fact]
    public async Task ReceiptSinkFailureAfterPublishPreservesPublicationTruth()
    {
        var generator = new ScriptedGenerator("sink-failure", request => Success(request.Request));
        using var fixture = new GenerationFixture([generator], "disabled");
        await File.WriteAllTextAsync(Path.Combine(fixture.Root, "blocked-receipts"), "not a directory");
        EffectiveConfiguration configuration = fixture.Configuration with
        {
            Paths = fixture.Configuration.Paths with { ReceiptRoot = "blocked-receipts" },
        };

        object result = await fixture.GenerateAsync(configuration);

        JsonElement json = JsonDocument.Parse(JsonSerializer.Serialize(result, JsonOptions.Stable)).RootElement;
        string receiptPath = json.GetProperty("receipt_path").GetString()!;
        Assert.StartsWith(".assetctl/work/receipt-fallback", receiptPath, StringComparison.Ordinal);
        string receipt = await File.ReadAllTextAsync(Path.Combine(fixture.Root, receiptPath));
        Assert.Contains("\"published\": true", receipt, StringComparison.Ordinal);
        Assert.Contains("\"recovered_pending_transactions\": 0", receipt, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(fixture.Root, fixture.Manifest.Request.Output.Path)));
    }

    /// <summary>Rejects arbitrary bytes bearing an image extension before adapter invocation.</summary>
    [Fact]
    public async Task InvalidReferenceIsRejectedBeforeProviderInvocation()
    {
        var generator = new ScriptedGenerator("reference-generator", request => Success(request.Request));
        using var fixture = new GenerationFixture([generator], "disabled", referenceBytes: "not an image"u8.ToArray());

        await Assert.ThrowsAsync<AssetCtlException>(() => fixture.GenerateAsync());

        Assert.Equal(0, generator.Calls);
    }

    /// <summary>Enforces the configured reference byte ceiling before adapter invocation.</summary>
    [Fact]
    public async Task OversizedReferenceIsRejectedBeforeProviderInvocation()
    {
        var generator = new ScriptedGenerator("oversized-reference", request => Success(request.Request));
        using var fixture = new GenerationFixture(
            [generator],
            "disabled",
            referenceBytes: new byte[32],
            maximumReferenceBytes: 16
        );

        await Assert.ThrowsAsync<AssetCtlException>(() => fixture.GenerateAsync());

        Assert.Equal(0, generator.Calls);
    }

    /// <summary>Rejects a physical escape from the configured asset root even when it remains in the repository.</summary>
    [Fact]
    public async Task OutputSymlinkEscapeWithinRepositoryIsRejectedBeforeProviderInvocation()
    {
        var generator = new ScriptedGenerator("symlink-generator", request => Success(request.Request));
        using var fixture = new GenerationFixture([generator], "disabled");
        string outputDirectory = Path.GetDirectoryName(
            Path.Combine(fixture.Root, fixture.Manifest.Request.Output.Path)
        )!;
        Directory.Delete(outputDirectory, recursive: true);
        string escaped = Path.Combine(fixture.Root, "escaped-output");
        Directory.CreateDirectory(escaped);
        Directory.CreateSymbolicLink(outputDirectory, escaped);

        await Assert.ThrowsAsync<AssetCtlException>(() => fixture.GenerateAsync());

        Assert.Equal(0, generator.Calls);
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

        public ProviderExecutionContext? LastContext { get; private set; }

        public IReadOnlySet<AssetCapability> SupportedCapabilities { get; } =
            new HashSet<AssetCapability>
            {
                AssetCapability.RasterGenerate,
                AssetCapability.ImageTransparentOutput,
                AssetCapability.ImageReferenceInput,
            };

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
            LastContext = context;
            return Task.FromResult(invoke(request));
        }
    }

    private sealed class CapturingReviewer(string adapterId = "different-reviewer")
        : AlterCourse.AssetCtl.Providers.ProviderContracts.IAssetReviewer
    {
        public string AdapterId => adapterId;

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

    private sealed class FailingReviewer : AlterCourse.AssetCtl.Providers.ProviderContracts.IAssetReviewer
    {
        public string AdapterId => "malformed-reviewer";

        public IReadOnlySet<AssetCapability> SupportedCapabilities { get; } =
            new HashSet<AssetCapability> { AssetCapability.ReviewSemantic };

        public IReadOnlySet<string> AllowedEndpointHosts { get; } =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public void ValidateOptions(IReadOnlyDictionary<string, string> options) { }

        public Task<SemanticReviewResult> ReviewAsync(
            ProviderExecutionContext context,
            SemanticReviewRequest request,
            CancellationToken cancellationToken
        ) =>
            throw new ProviderException(
                ProviderErrorCategory.MalformedResponse,
                "null response from https://provider.example/result?sig=secret"
            );
    }

    private sealed class GenerationFixture : IDisposable
    {
        public GenerationFixture(
            IReadOnlyList<IAdapterDescriptor> adapters,
            string semanticReview,
            RouteRetryPolicy? retry = null,
            int maximumTotalAttempts = 12,
            bool retainUnselectedCandidates = false,
            int candidates = 1,
            decimal modelCost = 0m,
            byte[]? referenceBytes = null,
            long maximumReferenceBytes = 1_000_000,
            string godotAssetRoot = "src/AlterCourse.Godot/assets"
        )
        {
            Root = Path.Combine(Path.GetTempPath(), "assetctl-e2e-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
            AssetRequest request = CreateRequest(referenceBytes, godotAssetRoot);
            var providers = adapters.ToDictionary(
                adapter => adapter.AdapterId,
                adapter => Provider(adapter, modelCost),
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
                maximumTotalAttempts,
                retainUnselectedCandidates,
                candidates,
                maximumReferenceBytes,
                godotAssetRoot
            );
            Manifest = CreateManifest(request);
            string manifestPath = Path.Combine(Root, Manifest.ManifestPath);
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)!);
            File.WriteAllText(manifestPath, ManifestStore.Serialize(Manifest));
            string outputPath = Path.Combine(Root, Manifest.Request.Output.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            File.WriteAllBytes(outputPath, LocalPlaceholderGenerator.RenderPng(request));
            if (referenceBytes is not null)
            {
                string referencePath = Path.Combine(Root, request.References.Single().Path);
                Directory.CreateDirectory(Path.GetDirectoryName(referencePath)!);
                File.WriteAllBytes(referencePath, referenceBytes);
            }
            var registry = new AdapterRegistry(adapters);
            Orchestrator = new GenerationOrchestrator(registry, new AssetRouter(registry));
        }

        private static AssetRequest CreateRequest(byte[]? referenceBytes, string godotAssetRoot)
        {
            AssetRequest request = TestData.Request() with
            {
                Output = TestData.Request().Output with
                {
                    Path = Path.Combine(godotAssetRoot, "test", "placeholder.png"),
                },
            };
            return referenceBytes is null
                ? request
                : request with
                {
                    References =
                    [
                        new AssetReference(
                            "references/input.png",
                            Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(referenceBytes)),
                            "project-original"
                        ),
                    ],
                };
        }

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
            string? reviewerId = adapters
                .Where(adapter => adapter is AlterCourse.AssetCtl.Providers.ProviderContracts.IAssetReviewer)
                .Select(adapter => adapter.AdapterId)
                .SingleOrDefault();
            RouteDefinition[] review = reviewerId is null ? [] : [ReviewRoute(reviewerId)];
            return (generation, review);
        }

        private static RouteDefinition ReviewRoute(string reviewerId) =>
            new(
                "review",
                100,
                null,
                null,
                AssetCapability.ReviewSemantic,
                [new RouteTarget(reviewerId, "profile")],
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
            int maximumTotalAttempts,
            bool retainUnselectedCandidates,
            int candidates,
            long maximumReferenceBytes,
            string godotAssetRoot
        ) =>
            new(
                root,
                new AssetCtlPaths(
                    godotAssetRoot,
                    "config/assets/catalog",
                    "config/assets/styles",
                    ".assetctl/work",
                    ".assetctl/runs",
                    ".assetctl/state",
                    ".assetctl/logs"
                ),
                new AssetCtlPolicy(true, true, true, true, false, "reject", retainUnselectedCandidates),
                new AssetCtlLimits(1_000_000, maximumReferenceBytes, 4, maximumTotalAttempts, 10, 30, 1_000_000),
                new SpendingLimits(10m, 10m, 10m),
                providers,
                [generationRoute],
                reviewRoutes,
                new Dictionary<string, QualityTier>(StringComparer.Ordinal)
                {
                    [request.QualityTier] = new(request.QualityTier, candidates, 1, semanticReview, true, 0.80),
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

        public Task<object> GenerateAsync(EffectiveConfiguration? configuration = null) =>
            Orchestrator.GenerateAsync(
                configuration ?? Configuration,
                Manifest,
                force: false,
                dryRun: false,
                offline: false,
                CancellationToken.None
            );

        private static ProviderInstance Provider(IAdapterDescriptor adapter, decimal modelCost)
        {
            IReadOnlySet<AssetCapability> capabilities =
                adapter is IAssetGenerator
                    ? new HashSet<AssetCapability>
                    {
                        AssetCapability.RasterGenerate,
                        AssetCapability.ImageTransparentOutput,
                        AssetCapability.ImageReferenceInput,
                    }
                    : new HashSet<AssetCapability> { AssetCapability.ReviewSemantic };
            var model = new ModelProfile(
                "profile",
                "vendor-model",
                capabilities,
                modelCost,
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
