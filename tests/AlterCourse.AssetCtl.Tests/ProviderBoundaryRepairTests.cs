using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Exercises provider response, redirect, and timeout boundaries with deterministic HTTP fixtures.</summary>
public sealed class ProviderBoundaryRepairTests
{
    /// <summary>Rejects excess response candidates before fetching any provider-supplied URL.</summary>
    [Fact]
    public async Task ExcessResponseCandidatesAreRejectedBeforeDownloads()
    {
        int downloadRequests = 0;
        var handler = new FixtureHandler(request =>
        {
            if (string.Equals(request.RequestUri!.Host, "external.api.recraft.ai", StringComparison.Ordinal))
            {
                return Json(
                    HttpStatusCode.OK,
                    JsonSerializer.Serialize(
                        new
                        {
                            data = new[]
                            {
                                new { url = "https://cdn.example/one.png" },
                                new { url = "https://cdn.example/two.png" },
                            },
                        }
                    )
                );
            }

            downloadRequests++;
            return Image([1]);
        });
        var adapter = new RecraftImageAdapter(new HttpClient(handler));
        ProviderExecutionContext context = AllowDownloads(TestData.Context(adapter.AdapterId), "cdn.example");

        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                context,
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );

        Assert.Equal(ProviderErrorCategory.MalformedResponse, exception.Category);
        Assert.Equal(0, downloadRequests);
    }

    /// <summary>Applies one retained-byte ceiling across inline and downloaded candidates.</summary>
    [Fact]
    public async Task CandidateBytesShareOneAggregateBudget()
    {
        const int maximumBytes = 8;
        string inline = Convert.ToBase64String(new byte[5]);
        var handler = new FixtureHandler(request =>
            string.Equals(request.RequestUri!.Host, "external.api.recraft.ai", StringComparison.Ordinal)
                ? Json(
                    HttpStatusCode.OK,
                    JsonSerializer.Serialize(
                        new
                        {
                            data = new object[]
                            {
                                new { b64_json = inline },
                                new { url = "https://cdn.example/two.png" },
                            },
                        }
                    )
                )
                : Image(new byte[4])
        );
        var adapter = new RecraftImageAdapter(new HttpClient(handler));
        ProviderExecutionContext context = AllowDownloads(TestData.Context(adapter.AdapterId), "cdn.example") with
        {
            MaximumDownloadBytes = maximumBytes,
        };

        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                context,
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 2, []),
                CancellationToken.None
            )
        );

        Assert.Equal(ProviderErrorCategory.UnsafeDownload, exception.Category);
    }

    /// <summary>Revalidates every redirect target before issuing the redirected request.</summary>
    [Fact]
    public async Task RedirectTargetMustRemainAllowlisted()
    {
        var requestedHosts = new List<string>();
        var handler = new FixtureHandler(request =>
        {
            requestedHosts.Add(request.RequestUri!.Host);
            if (string.Equals(request.RequestUri.Host, "external.api.recraft.ai", StringComparison.Ordinal))
            {
                return Json(HttpStatusCode.OK, "{\"data\":[{\"url\":\"https://cdn.example/first.png\"}]}");
            }

            return new HttpResponseMessage(HttpStatusCode.Found)
            {
                Headers = { Location = new Uri("https://untrusted.example/escaped.png") },
            };
        });
        var adapter = new RecraftImageAdapter(new HttpClient(handler));

        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                AllowDownloads(TestData.Context(adapter.AdapterId), "cdn.example"),
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );

        Assert.Equal(ProviderErrorCategory.UnsafeDownload, exception.Category);
        Assert.DoesNotContain("untrusted.example", requestedHosts, StringComparer.Ordinal);
    }

    /// <summary>Normalizes a total download timeout as a retryable provider timeout.</summary>
    [Fact]
    public async Task DownloadTimeoutIsNormalized()
    {
        var handler = new FixtureHandler(
            async (request, cancellationToken) =>
            {
                if (string.Equals(request.RequestUri!.Host, "external.api.recraft.ai", StringComparison.Ordinal))
                {
                    return Json(HttpStatusCode.OK, "{\"data\":[{\"url\":\"https://cdn.example/slow.png\"}]}");
                }

                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
                throw new InvalidOperationException("unreachable");
            }
        );
        var adapter = new RecraftImageAdapter(new HttpClient(handler));
        ProviderExecutionContext context = AllowDownloads(TestData.Context(adapter.AdapterId), "cdn.example") with
        {
            TimeoutSeconds = 1,
        };

        ProviderException exception = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                context,
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );

        Assert.Equal(ProviderErrorCategory.Timeout, exception.Category);
        Assert.True(exception.Retryable);
    }

    private static ProviderExecutionContext AllowDownloads(ProviderExecutionContext context, string host) =>
        context with
        {
            Provider = context.Provider with
            {
                AllowedDownloadHosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { host },
            },
        };

    private static HttpResponseMessage Json(HttpStatusCode status, string json) =>
        new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage Image(byte[] bytes)
    {
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _response;

        public FixtureHandler(Func<HttpRequestMessage, HttpResponseMessage> response) =>
            _response = (request, _) => Task.FromResult(response(request));

        public FixtureHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> response) =>
            _response = response;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => _response(request, cancellationToken);
    }
}
