using System.Net;
using System.Security.Cryptography;
using System.Text;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Review;
using AlterCourse.AssetCtl.Validation;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Exercises hostile provider, manifest, spending, and lifecycle inputs at their trust boundaries.</summary>
public sealed class SecurityRegressionTests
{
    /// <summary>Prevents a configured credential from being sent to a host outside the adapter protocol boundary.</summary>
    [Theory]
    [InlineData("https://attacker.example/v1")]
    [InlineData("https://user@external.api.recraft.ai/v1")]
    [InlineData("https://external.api.recraft.ai/v1?redirect=attacker")]
    [InlineData("https://external.api.recraft.ai/v1#fragment")]
    public async Task ProviderCredentialsAreNeverSentToUntrustedEndpointShapes(string endpoint)
    {
        var handler = new CountingHandler();
        var adapter = new RecraftImageAdapter(new HttpClient(handler));
        ProviderExecutionContext context = TestData.Context(adapter.AdapterId) with
        {
            Provider = TestData.Context(adapter.AdapterId).Provider with { Endpoint = new Uri(endpoint) },
        };

        await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                context,
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );
        Assert.Equal(0, handler.RequestCount);
    }

    /// <summary>Rejects oversized JSON before parsing and oversized inline images before decoding allocation.</summary>
    [Fact]
    public async Task ProviderResponsesAreBoundedBeforeLargeAllocations()
    {
        string oversizedJson = "{\"padding\":\"" + new string('x', 256) + "\"}";
        var jsonAdapter = new RecraftImageAdapter(
            new HttpClient(
                new StaticHandler(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(oversizedJson, Encoding.UTF8, "application/json"),
                    }
                )
            )
        );
        ProviderExecutionContext jsonContext = TestData.Context(jsonAdapter.AdapterId) with
        {
            MaximumJsonResponseBytes = 64,
        };
        ProviderException jsonFailure = await Assert.ThrowsAsync<ProviderException>(() =>
            jsonAdapter.GenerateAsync(
                jsonContext,
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );
        Assert.Equal(ProviderErrorCategory.MalformedResponse, jsonFailure.Category);

        string encoded = Convert.ToBase64String(new byte[129]);
        var imageAdapter = new RecraftImageAdapter(
            new HttpClient(
                new StaticHandler(
                    new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(
                            $"{{\"data\":[{{\"b64_json\":\"{encoded}\"}}]}}",
                            Encoding.UTF8,
                            "application/json"
                        ),
                    }
                )
            )
        );
        ProviderExecutionContext imageContext = TestData.Context(imageAdapter.AdapterId) with
        {
            MaximumDownloadBytes = 128,
            MaximumJsonResponseBytes = 1_024,
        };
        ProviderException imageFailure = await Assert.ThrowsAsync<ProviderException>(() =>
            imageAdapter.GenerateAsync(
                imageContext,
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );
        Assert.Equal(ProviderErrorCategory.UnsafeDownload, imageFailure.Category);
    }

    /// <summary>Rejects unsafe source and preview dimensions before any renderer allocation.</summary>
    [Theory]
    [InlineData(0, 64, 1)]
    [InlineData(50_000, 50_000, 1)]
    [InlineData(64, 64, 0)]
    [InlineData(64, 64, 4097)]
    [InlineData(64, 64, 50_000)]
    public void OutputContractBoundsDimensionsAndPreviewSizes(int width, int height, int preview)
    {
        OutputContract output = TestData.Request().Output with
        {
            Width = width,
            Height = height,
            TargetDisplaySizes = [preview],
        };
        Assert.Throws<AssetCtlException>(() => OutputContractPolicy.Validate(output, 1_000_000));
    }

    /// <summary>Rejects external SVG resource references, including non-network URI schemes and external use targets.</summary>
    [Theory]
    [InlineData("<use href='file:///etc/passwd'/>")]
    [InlineData("<use href='other.svg#shape'/>")]
    [InlineData("<filter><feImage href='https://attacker.example/pixel'/></filter>")]
    [InlineData("<a href='mailto:attacker@example.test'><path d='M0 0'/></a>")]
    [InlineData("<g xml:base='../outside.svg'><use href='#shape'/></g>")]
    [InlineData("<evil:path xmlns:evil='https://attacker.example/ns' d='M0 0'/>")]
    public void SvgRejectsEveryNonFragmentResourceUri(string hostileElement)
    {
        string svg =
            $"<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64' viewBox='0 0 64 64'>{hostileElement}</svg>";
        MechanicalValidationResult result = MechanicalValidator.Validate(
            TestData.Request(AssetFormat.Svg),
            Encoding.UTF8.GetBytes(svg),
            1_000_000,
            1_000_000
        );
        Assert.False(result.Passed);
        Assert.Contains(result.Findings, finding => finding.Contains("prohibited SVG", StringComparison.Ordinal));
    }

    /// <summary>Accumulates every billable reservation and distinguishes unknown estimates from zero-cost work.</summary>
    [Fact]
    public void SpendGuardAccumulatesAttemptsReviewsAndDailyReservations()
    {
        var ledger = new FakeSpendLedger();
        var guard = new SpendGuard(
            new SpendingLimits(1.00m, 1.00m, 2.00m),
            "reject",
            ledger,
            () => new DateOnly(2026, 9, 1)
        );
        guard.Reserve(0.10m, 2, "generation");
        guard.Reserve(0.05m, 1, "review");
        guard.Reserve(0m, 1, "local");
        Assert.Equal(0.25m, guard.TotalReservedUsd);
        Assert.Equal([0.20m, 0.05m, 0m], ledger.Reservations);
        Assert.Throws<AssetCtlException>(() => guard.Reserve(null, 1, "unknown"));
        Assert.Throws<AssetCtlException>(() => guard.Reserve(0.80m, 1, "retry"));
    }

    /// <summary>Ignores editable manifest pass fields and validates the current asset bytes during approval.</summary>
    [Fact]
    public void ApprovalRerunsMechanicalValidationAgainstCurrentBytes()
    {
        using var repository = new TemporaryRepository([1, 2, 3]);
        AssetManifest manifest = repository.Manifest(mechanicalPassed: true);
        repository.WriteManifest(manifest);

        Assert.Throws<AssetCtlException>(() => ApprovalPolicy.Validate(repository.Configuration, manifest));
    }

    /// <summary>Requires semantic evidence bound to current bytes, request, configuration, and an independent reviewer.</summary>
    [Fact]
    public void ApprovalRejectsEditableSemanticPassWithoutCurrentEvidence()
    {
        AssetRequest seed = TestData.Request() with
        {
            Lifecycle = AssetLifecycle.Candidate,
            Output = TestData.Request().Output with { Path = "assets/asset.png" },
            QualityTier = "production",
        };
        byte[] png = LocalPlaceholderGenerator.RenderPng(seed);
        using var repository = new TemporaryRepository(png, semanticRequired: true);
        AssetManifest manifest = repository.Manifest(mechanicalPassed: true) with
        {
            Generation = repository.Generation(),
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
                "different-provider-family"
            ),
        };
        repository.WriteManifest(manifest);

        Assert.Throws<AssetCtlException>(() => ApprovalPolicy.Validate(repository.Configuration, manifest));
    }

    /// <summary>Rejects a well-shaped but fabricated digest instead of trusting its length.</summary>
    [Fact]
    public void ApprovalRejectsFabricatedSemanticEvidenceDigest()
    {
        AssetRequest seed = TestData.Request() with
        {
            Lifecycle = AssetLifecycle.Candidate,
            Output = TestData.Request().Output with { Path = "assets/asset.png" },
            QualityTier = "production",
        };
        byte[] png = LocalPlaceholderGenerator.RenderPng(seed);
        using var repository = new TemporaryRepository(png, semanticRequired: true);
        SemanticReviewResult review = new(
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
            "different-provider-family",
            new string('a', 64),
            "reviewer",
            "review-model"
        );
        AssetManifest manifest = repository.Manifest(mechanicalPassed: true) with
        {
            Generation = repository.Generation(),
            SemanticReview = review,
        };
        repository.WriteManifest(manifest);

        Assert.Throws<AssetCtlException>(() => ApprovalPolicy.Validate(repository.Configuration, manifest));
    }

    /// <summary>Rejects validly hashed review evidence from a different adapter in the generator's provider family.</summary>
    [Fact]
    public void ApprovalRejectsSameProviderFamilyDespiteIndependentLabel()
    {
        AssetRequest seed = TestData.Request() with
        {
            Lifecycle = AssetLifecycle.Candidate,
            Output = TestData.Request().Output with { Path = "assets/asset.png" },
            QualityTier = "production",
        };
        byte[] png = LocalPlaceholderGenerator.RenderPng(seed);
        using var repository = new TemporaryRepository(png, semanticRequired: true);
        AssetManifest baseline = repository.Manifest(mechanicalPassed: true);
        SemanticReviewResult review = new(
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
            "different-provider-family",
            null,
            "reviewer",
            "review-model"
        );
        review = review with
        {
            EvidenceSha256 = ReviewEvidence.Compute(
                baseline.Request,
                png,
                repository.Configuration.EffectiveHash,
                "reviewer",
                "review-model",
                review
            ),
        };
        AssetManifest manifest = baseline with
        {
            Generation = repository.Generation("openai-images"),
            SemanticReview = review,
        };
        repository.WriteManifest(manifest);

        Assert.Throws<AssetCtlException>(() => ApprovalPolicy.Validate(repository.Configuration, manifest));
    }

    /// <summary>Rejects a lifecycle write when the manifest changed after it was observed.</summary>
    [Fact]
    public void ManifestMutationUsesRevisionAndContentCompareAndSwap()
    {
        AssetRequest seed = TestData.Request() with
        {
            Lifecycle = AssetLifecycle.Candidate,
            Output = TestData.Request().Output with { Path = "assets/asset.png" },
            QualityTier = "production",
        };
        byte[] png = LocalPlaceholderGenerator.RenderPng(seed);
        using var repository = new TemporaryRepository(png);
        AssetManifest observed = repository.Manifest(mechanicalPassed: true);
        repository.WriteManifest(observed with { Revision = observed.Revision + 1 });

        Assert.Throws<AssetCtlException>(() => ManifestMutation.ReloadForMutation(repository.Configuration, observed));
    }

    private sealed class FakeSpendLedger : ISpendLedger
    {
        public List<decimal> Reservations { get; } = [];

        public void Reserve(DateOnly date, decimal amount, decimal dailyLimit)
        {
            decimal total = Reservations.Sum() + amount;
            if (total > dailyLimit)
            {
                throw new AssetCtlException("daily budget exceeded", 6);
            }

            Reservations.Add(amount);
        }
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        }
    }

    private sealed class StaticHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(response);
    }

    private sealed class TemporaryRepository : IDisposable
    {
        private readonly byte[] bytes;

        public TemporaryRepository(byte[] bytes, bool semanticRequired = false)
        {
            this.bytes = bytes;
            Root = Path.Combine(Path.GetTempPath(), "assetctl-security-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "assets"));
            Directory.CreateDirectory(Path.Combine(Root, "catalog"));
            File.WriteAllBytes(Path.Combine(Root, "assets", "asset.png"), bytes);
            var reviewerModel = new ModelProfile(
                "review-model",
                "model",
                new HashSet<AssetCapability> { AssetCapability.ReviewSemantic },
                0.01m,
                "fixed-output",
                new Dictionary<string, string>(StringComparer.Ordinal)
            );
            var reviewer = new ProviderInstance(
                "reviewer",
                "openai-vision-review",
                true,
                new Uri("https://api.openai.com/v1"),
                "OPENAI_API_KEY",
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new Dictionary<string, ModelProfile>(StringComparer.Ordinal) { [reviewerModel.Id] = reviewerModel }
            );
            Configuration = new EffectiveConfiguration(
                Root,
                new AssetCtlPaths(
                    "assets",
                    "catalog",
                    "styles",
                    ".assetctl/work",
                    ".assetctl/runs",
                    ".assetctl/state",
                    ".assetctl/logs"
                ),
                new AssetCtlPolicy(false, true, true, true, false, "reject"),
                new AssetCtlLimits(1_000_000, 1_000_000, 10, 10, 10, 30, 1_000_000),
                new SpendingLimits(1, 1, 1),
                new Dictionary<string, ProviderInstance>(StringComparer.Ordinal) { [reviewer.Id] = reviewer },
                [],
                [],
                new Dictionary<string, QualityTier>(StringComparer.Ordinal)
                {
                    ["production"] = new QualityTier(
                        "production",
                        1,
                        1,
                        semanticRequired ? "required" : "disabled",
                        false,
                        0.8
                    ),
                },
                new Dictionary<string, StyleProfile>(StringComparer.Ordinal)
                {
                    ["engineering-icons"] = new StyleProfile("engineering-icons", "test", [], []),
                },
                new Dictionary<string, string>(StringComparer.Ordinal),
                "config-hash"
            );
        }

        public string Root { get; }

        public EffectiveConfiguration Configuration { get; }

        public AssetManifest Manifest(bool mechanicalPassed)
        {
            AssetRequest request = TestData.Request() with
            {
                Lifecycle = AssetLifecycle.Candidate,
                Output = TestData.Request().Output with { Path = "assets/asset.png" },
                QualityTier = "production",
            };
            return new AssetManifest(
                "1",
                request,
                1,
                new RightsRecord("original-project-created", "project", null, null, "test"),
                null,
                new MechanicalValidationResult(
                    mechanicalPassed,
                    "image/png",
                    64,
                    64,
                    true,
                    [],
                    bytes,
                    new Dictionary<int, byte[]>()
                ),
                null,
                new IntegrityRecord(Convert.ToHexStringLower(SHA256.HashData(bytes)), bytes.LongLength, "image/png"),
                new ApprovalRecord(null, null, null),
                null,
                "catalog/test.asset.yaml"
            );
        }

        public GenerationProvenance Generation(string adapter = "recraft-images")
        {
            AssetRequest request = Manifest(mechanicalPassed: true).Request;
            return new GenerationProvenance(
                DateTimeOffset.UtcNow,
                "run",
                "route",
                "generator",
                adapter,
                "model",
                "vendor-model",
                "production",
                "prompt",
                "prompt-hash",
                ConfigurationLoader.Hash(System.Text.Json.JsonSerializer.Serialize(request, JsonOptions.Stable)),
                Configuration.EffectiveHash,
                null,
                0.1m,
                null
            );
        }

        public void WriteManifest(AssetManifest manifest) =>
            File.WriteAllText(Path.Combine(Root, manifest.ManifestPath), ManifestStore.Serialize(manifest));

        public void Dispose() => Directory.Delete(Root, recursive: true);
    }
}
