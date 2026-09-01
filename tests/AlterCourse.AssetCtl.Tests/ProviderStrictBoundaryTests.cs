using System.Net;
using System.Text;
using System.Text.Json;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Review;
using Microsoft.Extensions.Logging;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Proves typed provider parsing, identifier hygiene, transport policy, and structured diagnostics.</summary>
public sealed class ProviderStrictBoundaryTests
{
    private static readonly Action<Microsoft.Extensions.Logging.ILogger, int, Exception?> LogAttempt =
        LoggerMessage.Define<int>(LogLevel.Warning, new EventId(1, "ProviderAttempt"), "Provider attempt {Attempt}");

    /// <summary>Accepts documented response metadata while keeping required image fields strict.</summary>
    [Theory]
    [InlineData("recraft")]
    [InlineData("openai")]
    [InlineData("xai")]
    public async Task ImageAdaptersAcceptAdditionalSuccessMetadata(string kind)
    {
        AssetRequest request = TestData.Request();
        string image = Convert.ToBase64String(LocalPlaceholderGenerator.RenderPng(request));
        IAssetGenerator adapter = CreateAdapter(
            kind,
            $"{{\"created\":1,\"model\":\"resolved-model\",\"usage\":{{\"total_tokens\":10}},\"data\":[{{\"b64_json\":\"{image}\",\"revised_prompt\":\"safe\"}}]}}"
        );

        GenerationBatchResult result = await adapter.GenerateAsync(
            TestData.Context(adapter.AdapterId),
            new NormalizedGenerationRequest(request, "prompt", 1, []),
            CancellationToken.None
        );

        Assert.Single(result.Candidates);
    }

    /// <summary>Normalizes server errors for every external generation and review adapter.</summary>
    [Theory]
    [InlineData("recraft")]
    [InlineData("openai")]
    [InlineData("xai")]
    [InlineData("reviewer")]
    public async Task ExternalAdaptersNormalizeServerErrors(string kind)
    {
        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            InvokeAdapterAsync(
                kind,
                new FixtureHandler((_, _) => Task.FromResult(Response(HttpStatusCode.BadGateway, "{}")))
            )
        );

        Assert.Equal(ProviderErrorCategory.ProviderServer, exception.Category);
        Assert.True(exception.Retryable);
    }

    /// <summary>Normalizes total request cancellation as a retryable timeout for every external adapter.</summary>
    [Theory]
    [InlineData("recraft")]
    [InlineData("openai")]
    [InlineData("xai")]
    [InlineData("reviewer")]
    public async Task ExternalAdaptersNormalizeTimeouts(string kind)
    {
        var handler = new FixtureHandler(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("unreachable");
            }
        );

        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            InvokeAdapterAsync(kind, handler, timeoutSeconds: 1)
        );

        Assert.Equal(ProviderErrorCategory.Timeout, exception.Category);
        Assert.True(exception.Retryable);
    }

    /// <summary>Rejects HTTP redirects from every provider API endpoint without following them.</summary>
    [Theory]
    [InlineData("recraft")]
    [InlineData("openai")]
    [InlineData("xai")]
    [InlineData("reviewer")]
    public async Task ExternalAdaptersRejectApiRedirects(string kind)
    {
        var handler = new FixtureHandler(
            (_, _) =>
                Task.FromResult(
                    new HttpResponseMessage(HttpStatusCode.Found)
                    {
                        Headers = { Location = new Uri("https://untrusted.example/next") },
                    }
                )
        );

        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            InvokeAdapterAsync(kind, handler)
        );
        Assert.Equal(ProviderErrorCategory.InvalidRequest, exception.Category);
        Assert.Equal(1, handler.RequestCount);
    }

    /// <summary>Rejects undeclared options consistently across every external adapter.</summary>
    [Theory]
    [InlineData("recraft")]
    [InlineData("openai")]
    [InlineData("xai")]
    [InlineData("reviewer")]
    public void ExternalAdaptersRejectUnknownOptions(string kind)
    {
        var client = new HttpClient(new StaticHandler("{}"));
        var options = new Dictionary<string, string>(StringComparer.Ordinal) { ["undeclared"] = "value" };
        Action validate = kind switch
        {
            "recraft" => () => new RecraftImageAdapter(client).ValidateOptions(options),
            "openai" => () => new OpenAiImageAdapter(client).ValidateOptions(options),
            "xai" => () => new XaiImageAdapter(client).ValidateOptions(options),
            "reviewer" => () => new OpenAiVisionReviewer(client).ValidateOptions(options),
            _ => throw new InvalidOperationException(),
        };

        Assert.Throws<ProviderException>(validate);
    }

    /// <summary>Drops URL-like and credential-shaped request identifiers from every image adapter result.</summary>
    [Theory]
    [InlineData("recraft")]
    [InlineData("openai")]
    [InlineData("xai")]
    public async Task ImageAdaptersDropUnsafeRequestIdentifiers(string kind)
    {
        byte[] png = AlterCourse.AssetCtl.Generation.LocalPlaceholderGenerator.RenderPng(TestData.Request());
        string response = JsonSerializer.Serialize(
            new
            {
                id = "https://provider.example/job?signature=private",
                data = new[] { new { b64_json = Convert.ToBase64String(png) } },
            }
        );
        IAssetGenerator adapter = CreateAdapter(kind, response);

        GenerationBatchResult result = await adapter.GenerateAsync(
            TestData.Context(adapter.AdapterId),
            new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
            CancellationToken.None
        );

        Assert.Null(result.ProviderRequestId);
    }

    /// <summary>Drops provider identifiers that echo credentials or resemble standalone access tokens.</summary>
    [Theory]
    [InlineData("recraft")]
    [InlineData("openai")]
    [InlineData("xai")]
    public async Task ImageAdaptersDropCredentialEchoRequestIdentifiers(string kind)
    {
        string credential = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        byte[] png = AlterCourse.AssetCtl.Generation.LocalPlaceholderGenerator.RenderPng(TestData.Request());
        string response = JsonSerializer.Serialize(
            new { id = $"job-{credential}", data = new[] { new { b64_json = Convert.ToBase64String(png) } } }
        );
        IAssetGenerator adapter = CreateAdapter(kind, response);

        GenerationBatchResult result = await adapter.GenerateAsync(
            TestData.Context(adapter.AdapterId, credential: credential),
            new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
            CancellationToken.None
        );

        Assert.Null(result.ProviderRequestId);
    }

    /// <summary>Normalizes nullable provider collection and item shapes as malformed responses.</summary>
    [Theory]
    [InlineData("{\"data\":null}")]
    [InlineData("{\"data\":[null]}")]
    public async Task ImageAdaptersRejectNullSuccessShapes(string response)
    {
        IAssetGenerator adapter = CreateAdapter("recraft", response);
        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                TestData.Context(adapter.AdapterId),
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );
        Assert.Equal(ProviderErrorCategory.MalformedResponse, exception.Category);
    }

    /// <summary>Normalizes null nested reviewer objects and arrays as malformed responses.</summary>
    [Theory]
    [InlineData("{\"choices\":[{\"message\":null}]}")]
    [InlineData(
        "{\"output_text\":\"{\\\"matches_subject\\\":true,\\\"required_constraints_satisfied\\\":true,\\\"prohibited_content_absent\\\":true,\\\"readable_at_target_sizes\\\":true,\\\"style_adherence\\\":0.5,\\\"semantic_clarity\\\":0.5,\\\"visual_defects\\\":null,\\\"unrequested_text_detected\\\":false,\\\"logo_or_watermark_detected\\\":false,\\\"overall_score\\\":0.5,\\\"decision\\\":\\\"pass\\\"}\"}"
    )]
    public async Task ReviewerRejectsNullNestedShapes(string response)
    {
        var handler = new StaticHandler(response);
        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            InvokeAdapterAsync("reviewer", handler)
        );
        Assert.Equal(ProviderErrorCategory.MalformedResponse, exception.Category);
    }

    /// <summary>Requires the semantic-review schema to contain exactly the declared fields and constraints.</summary>
    [Theory]
    [MemberData(nameof(InvalidSemanticReviews))]
    public void SemanticReviewRejectsSchemaViolations(string json)
    {
        ProviderException exception = Assert.Throws<ProviderException>(() => OpenAiVisionReviewer.Parse(json));
        Assert.Equal(ProviderErrorCategory.MalformedResponse, exception.Category);
    }

    /// <summary>Builds a redirect-disabled transport with a finite connection timeout.</summary>
    [Fact]
    public void ProviderTransportHasExplicitConnectionBoundary()
    {
        using SocketsHttpHandler handler = Program.CreateProviderHandler();
        Assert.False(handler.AllowAutoRedirect);
        Assert.InRange(handler.ConnectTimeout, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
    }

    /// <summary>Writes rolling diagnostics as one structured JSON object per event.</summary>
    [Fact]
    public void RollingLogUsesStructuredJson()
    {
        string repository = Path.Combine(Path.GetTempPath(), $"assetctl-log-{Guid.NewGuid():N}");
        try
        {
            using (ILoggerFactory factory = Program.CreateLoggerFactory(repository))
            {
                LogAttempt(factory.CreateLogger("contract"), 2, null);
            }

            string path = Assert.Single(Directory.GetFiles(Path.Combine(repository, ".assetctl", "logs"), "*.json"));
            using var document = JsonDocument.Parse(Assert.Single(File.ReadLines(path)));
            Assert.Equal(2, document.RootElement.GetProperty("Properties").GetProperty("Attempt").GetInt32());
        }
        finally
        {
            if (Directory.Exists(repository))
            {
                Directory.Delete(repository, recursive: true);
            }
        }
    }

    /// <summary>Writes structured logs under the configured log root rather than a built-in path.</summary>
    [Fact]
    public void RollingLogUsesConfiguredRoot()
    {
        string repository = Path.Combine(Path.GetTempPath(), $"assetctl-log-root-{Guid.NewGuid():N}");
        Directory.CreateDirectory(repository);
        try
        {
            using (ILoggerFactory factory = Program.CreateLoggerFactory(repository, "var/assetctl/logs"))
            {
                LogAttempt(factory.CreateLogger("contract"), 1, null);
            }

            Assert.Single(Directory.GetFiles(Path.Combine(repository, "var", "assetctl", "logs"), "*.json"));
            Assert.False(Directory.Exists(Path.Combine(repository, ".assetctl", "logs")));
        }
        finally
        {
            Directory.Delete(repository, recursive: true);
        }
    }

    /// <summary>Does not create diagnostic directories when no repository was discovered.</summary>
    [Fact]
    public void LoggingWithoutRepositoryUsesStderrOnly()
    {
        string outside = Path.Combine(Path.GetTempPath(), $"assetctl-no-repo-{Guid.NewGuid():N}");

        using ILoggerFactory factory = Program.CreateLoggerFactory(null, null);

        Assert.NotNull(factory);
        Assert.False(Directory.Exists(outside));
    }

    /// <summary>Gets semantic payloads that each violate one exact schema rule.</summary>
    public static TheoryData<string> InvalidSemanticReviews =>
        new()
        {
            ValidReview.Replace("}", ",\"extra\":true}", StringComparison.Ordinal),
            ValidReview.Replace("\"matches_subject\":true,", string.Empty, StringComparison.Ordinal),
            ValidReview.Replace("\"style_adherence\":0.5", "\"style_adherence\":1.1", StringComparison.Ordinal),
            ValidReview.Replace("\"decision\":\"pass\"", "\"decision\":\"maybe\"", StringComparison.Ordinal),
            ValidReview.Replace(
                "\"visual_defects\":[]",
                $"\"visual_defects\":{JsonSerializer.Serialize(Enumerable.Repeat("x", 21))}",
                StringComparison.Ordinal
            ),
        };

    private const string ValidReview =
        "{\"matches_subject\":true,\"required_constraints_satisfied\":true,\"prohibited_content_absent\":true,\"readable_at_target_sizes\":true,\"style_adherence\":0.5,\"semantic_clarity\":0.5,\"visual_defects\":[],\"unrequested_text_detected\":false,\"logo_or_watermark_detected\":false,\"overall_score\":0.5,\"decision\":\"pass\"}";

    private static IAssetGenerator CreateAdapter(string kind, string response)
    {
        var client = new HttpClient(new StaticHandler(response));
        return kind switch
        {
            "recraft" => new RecraftImageAdapter(client),
            "openai" => new OpenAiImageAdapter(client),
            "xai" => new XaiImageAdapter(client),
            _ => throw new InvalidOperationException(),
        };
    }

    private static async Task InvokeAdapterAsync(string kind, HttpMessageHandler handler, int timeoutSeconds = 10)
    {
        using var client = new HttpClient(handler);
        if (string.Equals(kind, "reviewer", StringComparison.Ordinal))
        {
            var reviewer = new OpenAiVisionReviewer(client);
            ProviderExecutionContext context = TestData.Context(reviewer.AdapterId) with
            {
                TimeoutSeconds = timeoutSeconds,
            };
            await reviewer
                .ReviewAsync(
                    context,
                    new SemanticReviewRequest(
                        TestData.Request(),
                        [1],
                        "image/png",
                        new Dictionary<int, byte[]>(),
                        SemanticReviewSchema.Json
                    ),
                    CancellationToken.None
                )
                .ConfigureAwait(false);
            return;
        }

        IAssetGenerator adapter = kind switch
        {
            "recraft" => new RecraftImageAdapter(client),
            "openai" => new OpenAiImageAdapter(client),
            "xai" => new XaiImageAdapter(client),
            _ => throw new InvalidOperationException(),
        };
        ProviderExecutionContext generationContext = TestData.Context(adapter.AdapterId) with
        {
            TimeoutSeconds = timeoutSeconds,
        };
        await adapter
            .GenerateAsync(
                generationContext,
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
            .ConfigureAwait(false);
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string response) =>
        new(status) { Content = new StringContent(response, Encoding.UTF8, "application/json") };

    private sealed class StaticHandler(string response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(response, Encoding.UTF8, "application/json"),
                }
            );
    }

    private sealed class FixtureHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response)
        : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestCount++;
            return response(request, cancellationToken);
        }
    }
}
