using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Review;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies provider REST contracts exclusively through deterministic HTTP fixtures.</summary>
public sealed class ProviderContractTests
{
    /// <summary>Maps HTTP payment failure to the stable balance category without retaining response content.</summary>
    [Theory]
    [InlineData("recraft")]
    [InlineData("openai")]
    [InlineData("xai")]
    public async Task PaymentRequiredNormalizesToInsufficientBalanceWithoutResponseBody(string kind)
    {
        const string sensitiveBody = "insufficient balance for account secret-account-id";
        var handler = new RecordingHandler(_ => Json(HttpStatusCode.PaymentRequired, sensitiveBody));
        IAssetGenerator adapter = kind switch
        {
            "recraft" => new RecraftImageAdapter(new HttpClient(handler)),
            "openai" => new OpenAiImageAdapter(new HttpClient(handler)),
            "xai" => new XaiImageAdapter(new HttpClient(handler)),
            _ => throw new InvalidOperationException(),
        };

        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                TestData.Context(adapter.AdapterId),
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );

        Assert.Equal(ProviderErrorCategory.InsufficientBalance, exception.Category);
        Assert.DoesNotContain("secret-account-id", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Preserves SVG media semantics for Recraft vector generation.</summary>
    [Fact]
    public async Task RecraftVectorResponseProducesSvgCandidate()
    {
        byte[] svg = AlterCourse.AssetCtl.Generation.LocalPlaceholderGenerator.RenderSvg(
            TestData.Request(AssetFormat.Svg)
        );
        var handler = new RecordingHandler(_ =>
            Json(
                HttpStatusCode.OK,
                JsonSerializer.Serialize(new { data = new[] { new { b64_json = Convert.ToBase64String(svg) } } })
            )
        );
        var adapter = new RecraftImageAdapter(new HttpClient(handler));
        GenerationBatchResult result = await adapter.GenerateAsync(
            TestData.Context(adapter.AdapterId),
            new NormalizedGenerationRequest(TestData.Request(AssetFormat.Svg), "vector", 1, []),
            CancellationToken.None
        );
        Assert.Equal("image/svg+xml", Assert.Single(result.Candidates).MediaType);
    }

    /// <summary>Uses configured endpoints, models, candidate counts, and bearer authentication.</summary>
    [Theory]
    [InlineData("recraft", "recraft-images", "/v1/images/generations")]
    [InlineData("openai", "openai-images", "/v1/images/generations")]
    [InlineData("xai", "xai-images", "/v1/images/generations")]
    public async Task GenerationAdaptersUseConfiguredEndpointModelCountAndBearerAuth(
        string kind,
        string adapterId,
        string expectedPath
    )
    {
        byte[] png = LocalPlaceholderGenerator.RenderPng(TestData.Request());
        var handler = new RecordingHandler(_ =>
            Json(
                HttpStatusCode.OK,
                $"{{\"id\":\"request-1\",\"data\":[{{\"b64_json\":\"{Convert.ToBase64String(png)}\"}}]}}"
            )
        );
        using var client = new HttpClient(handler);
        IAssetGenerator adapter = kind switch
        {
            "recraft" => new RecraftImageAdapter(client),
            "openai" => new OpenAiImageAdapter(client),
            "xai" => new XaiImageAdapter(client),
            _ => throw new InvalidOperationException(),
        };
        ProviderExecutionContext context = TestData.Context(adapterId);
        GenerationBatchResult result = await adapter.GenerateAsync(
            context,
            new NormalizedGenerationRequest(TestData.Request(), "safe prompt", 1, []),
            CancellationToken.None
        );
        Assert.Single(result.Candidates);
        Assert.Equal(expectedPath, handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal(HttpMethod.Post, handler.Request.Method);
        Assert.Equal("Bearer", handler.Request.Headers.Authorization!.Scheme);
        Assert.Equal(context.Credential, handler.Request.Headers.Authorization.Parameter);
        Assert.Contains("\"model\":\"model\"", handler.Body, StringComparison.Ordinal);
    }

    /// <summary>Transmits an exact supported output size instead of relying on a provider default.</summary>
    [Theory]
    [InlineData("recraft")]
    [InlineData("openai")]
    public async Task ExactSupportedDimensionsAreTransmitted(string kind)
    {
        AssetRequest request = TestData.Request() with
        {
            Output = TestData.Request().Output with { Width = 1024, Height = 1024 },
        };
        byte[] png = LocalPlaceholderGenerator.RenderPng(request);
        var handler = new RecordingHandler(_ =>
            Json(HttpStatusCode.OK, $"{{\"data\":[{{\"b64_json\":\"{Convert.ToBase64String(png)}\"}}]}}")
        );
        IAssetGenerator adapter = string.Equals(kind, "recraft", StringComparison.Ordinal)
            ? new RecraftImageAdapter(new HttpClient(handler))
            : new OpenAiImageAdapter(new HttpClient(handler));

        _ = await adapter.GenerateAsync(
            TestData.Context(adapter.AdapterId) with
            {
                Model = TestData.Context(adapter.AdapterId).Model with
                {
                    Options = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["supported_sizes"] = "1024x1024",
                    },
                },
            },
            new NormalizedGenerationRequest(request, "prompt", 1, []),
            CancellationToken.None
        );

        Assert.Contains("\"size\":\"1024x1024\"", handler.Body, StringComparison.Ordinal);
    }

    /// <summary>Refuses an exact output contract that the provider protocol cannot guarantee before sending.</summary>
    [Theory]
    [InlineData("recraft")]
    [InlineData("openai")]
    [InlineData("xai")]
    public async Task UnsupportedExactDimensionsAreRefusedBeforeSending(string kind)
    {
        var handler = new RecordingHandler(_ => throw new Xunit.Sdk.XunitException("HTTP request was sent"));
        IAssetGenerator adapter = kind switch
        {
            "recraft" => new RecraftImageAdapter(new HttpClient(handler)),
            "openai" => new OpenAiImageAdapter(new HttpClient(handler)),
            "xai" => new XaiImageAdapter(new HttpClient(handler)),
            _ => throw new InvalidOperationException(),
        };

        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                TestData.Context(adapter.AdapterId) with
                {
                    Model = TestData.Context(adapter.AdapterId).Model with
                    {
                        Options = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["supported_sizes"] = "1024x1024",
                        },
                    },
                },
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );

        Assert.Equal(ProviderErrorCategory.UnsupportedOutput, exception.Category);
        Assert.Null(handler.Request);
    }

    /// <summary>Refuses reference inputs on the generation-only Recraft endpoint instead of dropping them.</summary>
    [Fact]
    public async Task RecraftRejectsReferencesBeforeSending()
    {
        var handler = new RecordingHandler(_ => throw new Xunit.Sdk.XunitException("HTTP request was sent"));
        var adapter = new RecraftImageAdapter(new HttpClient(handler));
        byte[] reference = LocalPlaceholderGenerator.RenderPng(TestData.Request());

        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                TestData.Context(adapter.AdapterId) with
                {
                    Model = TestData.Context(adapter.AdapterId).Model with
                    {
                        Options = new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["supported_sizes"] = "1024x1024",
                        },
                    },
                },
                new NormalizedGenerationRequest(
                    TestData.Request() with
                    {
                        Output = TestData.Request().Output with { Width = 1024, Height = 1024 },
                    },
                    "prompt",
                    1,
                    [("reference.png", "image/png", reference)]
                ),
                CancellationToken.None
            )
        );

        Assert.Equal(ProviderErrorCategory.UnsupportedOutput, exception.Category);
        Assert.Null(handler.Request);
    }

    /// <summary>Maps reference bytes into each provider's configured edit request shape.</summary>
    [Theory]
    [InlineData("openai")]
    [InlineData("xai")]
    public async Task EditAdaptersMapReferenceBytesWithoutNetwork(string kind)
    {
        byte[] png = LocalPlaceholderGenerator.RenderPng(TestData.Request());
        var handler = new RecordingHandler(_ =>
            Json(HttpStatusCode.OK, $"{{\"data\":[{{\"b64_json\":\"{Convert.ToBase64String(png)}\"}}]}}")
        );
        using var client = new HttpClient(handler);
        IAssetGenerator adapter = string.Equals(kind, "openai", StringComparison.Ordinal)
            ? new OpenAiImageAdapter(client)
            : new XaiImageAdapter(client);
        await adapter.GenerateAsync(
            TestData.Context(adapter.AdapterId),
            new NormalizedGenerationRequest(TestData.Request(), "edit", 1, [("reference.png", "image/png", png)]),
            CancellationToken.None
        );
        if (string.Equals(kind, "openai", StringComparison.Ordinal))
        {
            Assert.Contains("reference.png", handler.Body, StringComparison.Ordinal);
        }
        else
        {
            using var body = JsonDocument.Parse(handler.Body);
            Assert.Equal(Convert.ToBase64String(png), body.RootElement.GetProperty("image")[0].GetString());
        }
    }

    /// <summary>Normalizes HTTP failures without retaining provider response bodies.</summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Authentication", false)]
    [InlineData(HttpStatusCode.Forbidden, "Authorization", false)]
    [InlineData(HttpStatusCode.RequestTimeout, "Timeout", true)]
    [InlineData(HttpStatusCode.TooManyRequests, "RateLimit", true)]
    [InlineData(HttpStatusCode.InternalServerError, "ProviderServer", true)]
    public async Task HttpFailuresNormalizeWithoutResponseBodies(
        HttpStatusCode status,
        string expectedName,
        bool retryable
    )
    {
        ProviderErrorCategory expected = Enum.Parse<ProviderErrorCategory>(expectedName);
        string credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var handler = new RecordingHandler(_ => Json(status, JsonSerializer.Serialize(new { credential })));
        var adapter = new RecraftImageAdapter(new HttpClient(handler));
        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                TestData.Context(adapter.AdapterId),
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );
        Assert.Equal(expected, exception.Category);
        Assert.Equal(retryable, exception.Retryable);
        Assert.DoesNotContain(credential, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Preserves a bounded Retry-After delay for the routing layer without retaining response data.</summary>
    [Theory]
    [InlineData("12", 5_000)]
    [InlineData("1", 1_000)]
    public async Task RetryAfterDelayIsParsedAndCapped(string retryAfter, int expectedMilliseconds)
    {
        HttpResponseMessage response = Json(HttpStatusCode.TooManyRequests, "{}");
        response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
        var adapter = new RecraftImageAdapter(new HttpClient(new RecordingHandler(_ => response)));
        ProviderExecutionContext context = TestData.Context(adapter.AdapterId) with
        {
            MaximumRetryAfterDelayMilliseconds = 5_000,
        };

        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                context,
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );

        Assert.Equal(
            TimeSpan.FromMilliseconds(expectedMilliseconds),
            AlterCourse.AssetCtl.Providers.ProviderContracts.RetryAfterDelay(exception)
        );
    }

    /// <summary>Parses an HTTP-date Retry-After value and caps it to the configured retry ceiling.</summary>
    [Fact]
    public async Task RetryAfterDateIsParsedAndCapped()
    {
        HttpResponseMessage response = Json(HttpStatusCode.ServiceUnavailable, "{}");
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
            DateTimeOffset.UtcNow.AddMinutes(1)
        );
        var adapter = new RecraftImageAdapter(new HttpClient(new RecordingHandler(_ => response)));
        ProviderExecutionContext context = TestData.Context(adapter.AdapterId) with
        {
            MaximumRetryAfterDelayMilliseconds = 5_000,
        };

        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                context,
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );

        Assert.Equal(
            TimeSpan.FromMilliseconds(5_000),
            AlterCourse.AssetCtl.Providers.ProviderContracts.RetryAfterDelay(exception)
        );
    }

    /// <summary>Accepts only schema-valid structured semantic review output.</summary>
    [Fact]
    public async Task SemanticReviewerRequiresSchemaValidStructuredResult()
    {
        const string review =
            "{\"matches_subject\":true,\"required_constraints_satisfied\":true,\"prohibited_content_absent\":true,\"readable_at_target_sizes\":true,\"style_adherence\":0.9,\"semantic_clarity\":0.8,\"visual_defects\":[],\"unrequested_text_detected\":false,\"logo_or_watermark_detected\":false,\"overall_score\":0.85,\"decision\":\"pass\"}";
        var handler = new RecordingHandler(_ =>
            Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { output_text = review }))
        );
        var reviewer = new OpenAiVisionReviewer(new HttpClient(handler));
        SemanticReviewResult result = await reviewer.ReviewAsync(
            TestData.Context(reviewer.AdapterId),
            new SemanticReviewRequest(
                TestData.Request(),
                [1],
                "image/png",
                new Dictionary<int, byte[]>(0),
                SemanticReviewSchema.Json
            ),
            CancellationToken.None
        );
        Assert.Equal(0.85, result.OverallScore);
        Assert.False(result.HasHardFailure);
        Assert.Equal("/v1/responses", handler.Request!.RequestUri!.AbsolutePath);
    }

    /// <summary>Supplies resolved style constraints and every target-size preview to structured vision review.</summary>
    [Fact]
    public async Task SemanticReviewerSendsResolvedStyleAndTargetPreviews()
    {
        const string review =
            "{\"matches_subject\":true,\"required_constraints_satisfied\":true,\"prohibited_content_absent\":true,\"readable_at_target_sizes\":true,\"style_adherence\":0.9,\"semantic_clarity\":0.8,\"visual_defects\":[],\"unrequested_text_detected\":false,\"logo_or_watermark_detected\":false,\"overall_score\":0.85,\"decision\":\"pass\"}";
        var handler = new RecordingHandler(_ =>
            Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { output_text = review }))
        );
        using var client = new HttpClient(handler);
        var reviewer = new OpenAiVisionReviewer(client);
        ProviderExecutionContext context = TestData.Context(reviewer.AdapterId);
        await reviewer.ReviewAsync(
            context,
            new SemanticReviewRequest(
                TestData.Request(),
                [1, 2, 3],
                "image/png",
                new Dictionary<int, byte[]> { [16] = [4, 5], [24] = [6, 7] },
                SemanticReviewSchema.Json,
                "resolved LCARS line language",
                ["style-required"],
                ["style-prohibited"]
            ),
            CancellationToken.None
        );

        Assert.Contains("resolved LCARS line language", handler.Body, StringComparison.Ordinal);
        Assert.Contains("style-required", handler.Body, StringComparison.Ordinal);
        Assert.Contains("style-prohibited", handler.Body, StringComparison.Ordinal);
        Assert.Contains("Target-size preview: 16x16 pixels", handler.Body, StringComparison.Ordinal);
        Assert.Contains("Target-size preview: 24x24 pixels", handler.Body, StringComparison.Ordinal);
        Assert.Equal(3, Occurrences(handler.Body, "\"type\":\"input_image\""));
        Assert.DoesNotContain(context.Credential, handler.Body, StringComparison.Ordinal);
    }

    /// <summary>Removes credentials and signed query values from diagnostic text.</summary>
    [Fact]
    public void RedactorRemovesTokensAndSignedQueries()
    {
        string credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        Assert.DoesNotContain(credential, Redactor.Sanitize($"authorization={credential}"), StringComparison.Ordinal);
        Assert.Equal(
            "https://example.test/image",
            Redactor.Sanitize($"https://example.test/image?signature={credential}")
        );
    }

    /// <summary>Bounds provider downloads by scheme, host, media type, redirects, and byte count.</summary>
    [Fact]
    public async Task UrlOutputsRequireHttpsAllowlistMediaTypeAndByteBound()
    {
        byte[] png = LocalPlaceholderGenerator.RenderPng(TestData.Request());
        string signedQuery = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        var handler = new RecordingHandler(request =>
            string.Equals(request.RequestUri!.Host, "external.api.recraft.ai", StringComparison.Ordinal)
                ? Json(
                    HttpStatusCode.OK,
                    JsonSerializer.Serialize(
                        new { data = new[] { new { url = $"https://cdn.example/output.png?signature={signedQuery}" } } }
                    )
                )
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(png)
                    {
                        Headers = { ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png") },
                    },
                }
        );
        global::AlterCourse.AssetCtl.Providers.ProviderContracts.ProviderExecutionContext baseContext =
            TestData.Context("recraft-images");
        global::AlterCourse.AssetCtl.Domain.DomainModels.ProviderInstance provider = baseContext.Provider with
        {
            AllowedDownloadHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cdn.example" },
        };
        var adapter = new RecraftImageAdapter(new HttpClient(handler));
        GenerationBatchResult result = await adapter.GenerateAsync(
            baseContext with
            {
                Provider = provider,
            },
            new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
            CancellationToken.None
        );
        Assert.Equal(png, Assert.Single(result.Candidates).Bytes);

        global::AlterCourse.AssetCtl.Domain.DomainModels.ProviderInstance denied = provider with
        {
            AllowedDownloadHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        };
        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                baseContext with
                {
                    Provider = denied,
                },
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );
        Assert.Equal(ProviderErrorCategory.UnsafeDownload, exception.Category);
        Assert.DoesNotContain(signedQuery, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Rejects malformed provider responses and unsupported adapter options before use.</summary>
    [Fact]
    public async Task MalformedResponsesAndUnknownOptionsFailBeforeUse()
    {
        var adapter = new RecraftImageAdapter(new HttpClient(new RecordingHandler(_ => Json(HttpStatusCode.OK, "{}"))));
        await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                TestData.Context(adapter.AdapterId),
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );
        Assert.Throws<ProviderException>(() =>
            adapter.ValidateOptions(new Dictionary<string, string>(StringComparer.Ordinal) { ["unknown"] = "value" })
        );
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static int Occurrences(string value, string pattern)
    {
        int count = 0;
        int offset = 0;
        while ((offset = value.IndexOf(pattern, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += pattern.Length;
        }

        return count;
    }

    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Request = request;
            Body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return response(request);
        }
    }
}
