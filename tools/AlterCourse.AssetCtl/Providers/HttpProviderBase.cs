using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;

namespace AlterCourse.AssetCtl.Providers;

internal abstract class HttpProviderBase(HttpClient httpClient, IReadOnlySet<string> allowedEndpointHosts)
{
    public static class Redactor
    {
        public static string Sanitize(string value)
        {
            if (
                Uri.TryCreate(value, UriKind.Absolute, out Uri? uri)
                && (
                    string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.Ordinal)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
                )
            )
            {
                return uri.GetLeftPart(UriPartial.Path);
            }

            return System.Text.RegularExpressions.Regex.Replace(
                value,
                "(?i)(?<key>authorization|api[_-]?key|token|secret)\\s*[:=]\\s*(?:Bearer\\s+)?[^\\s,;]+",
                "${key}=[REDACTED]",
                System.Text.RegularExpressions.RegexOptions.CultureInvariant
                    | System.Text.RegularExpressions.RegexOptions.ExplicitCapture,
                TimeSpan.FromMilliseconds(100)
            );
        }
    }

    protected HttpClient Client { get; } = httpClient;

    protected async Task<JsonDocument> SendJsonAsync(
        HttpRequestMessage message,
        ProviderExecutionContext context,
        CancellationToken cancellationToken
    )
    {
        ValidateRequestEndpoint(message);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.Credential);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(context.TimeoutSeconds));
        HttpResponseMessage response = await SendAsync(message, cancellationToken, timeout.Token).ConfigureAwait(false);

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw Normalize(response.StatusCode, response.Headers.RetryAfter is not null);
            }

            if (response.Content.Headers.ContentLength > context.MaximumJsonResponseBytes)
            {
                throw new ProviderException(
                    ProviderErrorCategory.MalformedResponse,
                    "Provider JSON response exceeds byte limit."
                );
            }

            byte[] json = await ReadBoundedAsync(
                    response.Content,
                    context.MaximumJsonResponseBytes,
                    timeout.Token,
                    ProviderErrorCategory.MalformedResponse,
                    "Provider JSON response exceeded byte limit while streaming."
                )
                .ConfigureAwait(false);
            try
            {
                return JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 32 });
            }
            catch (JsonException exception)
            {
                throw new ProviderException(
                    ProviderErrorCategory.MalformedResponse,
                    $"Malformed provider JSON: {exception.Message}"
                );
            }
        }
    }

    private void ValidateRequestEndpoint(HttpRequestMessage message)
    {
        Uri endpoint =
            message.RequestUri
            ?? throw new ProviderException(
                ProviderErrorCategory.InvalidRequest,
                "Provider request omitted an endpoint."
            );
        try
        {
            ProviderEndpointPolicy.Validate(endpoint, allowedEndpointHosts, "provider request");
        }
        catch (AssetCtlException exception)
        {
            throw new ProviderException(ProviderErrorCategory.InvalidRequest, exception.Message);
        }
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage message,
        CancellationToken callerToken,
        CancellationToken timeoutToken
    )
    {
        try
        {
            return await Client
                .SendAsync(message, HttpCompletionOption.ResponseHeadersRead, timeoutToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            throw new ProviderException(ProviderErrorCategory.Timeout, "Provider request timed out.", retryable: true);
        }
        catch (HttpRequestException exception)
        {
            throw new ProviderException(
                ProviderErrorCategory.TransientNetwork,
                Redactor.Sanitize(exception.Message),
                retryable: true
            );
        }
    }

    protected async Task<IReadOnlyList<GeneratedCandidate>> ParseImageDataAsync(
        JsonElement root,
        ProviderExecutionContext context,
        CancellationToken cancellationToken
    )
    {
        if (!root.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new ProviderException(
                ProviderErrorCategory.MalformedResponse,
                "Provider response is missing data array."
            );
        }

        var candidates = new List<GeneratedCandidate>();
        int index = 0;
        foreach (global::System.Text.Json.JsonElement item in data.EnumerateArray())
        {
            if (item.TryGetProperty("b64_json", out JsonElement encoded) && encoded.ValueKind == JsonValueKind.String)
            {
                candidates.Add(DecodeInlineCandidate(encoded, index++, context.MaximumDownloadBytes));
            }
            else if (item.TryGetProperty("url", out JsonElement urlNode) && urlNode.ValueKind == JsonValueKind.String)
            {
                candidates.Add(
                    new GeneratedCandidate(
                        index++,
                        await DownloadAsync(urlNode.GetString()!, context, cancellationToken).ConfigureAwait(false),
                        "image/png",
                        null,
                        null
                    )
                );
            }
            else
            {
                throw new ProviderException(
                    ProviderErrorCategory.MalformedResponse,
                    "Provider did not return inline image bytes."
                );
            }
        }

        return candidates.Count != 0
            ? candidates
            : throw new ProviderException(ProviderErrorCategory.MalformedResponse, "Provider returned no candidates.");
    }

    private static GeneratedCandidate DecodeInlineCandidate(JsonElement encoded, int index, long maximumBytes)
    {
        try
        {
            string value = encoded.GetString()!;
            if (Base64.GetMaxDecodedFromUtf8Length(value.Length) > maximumBytes)
            {
                throw new ProviderException(
                    ProviderErrorCategory.UnsafeDownload,
                    "Provider inline image exceeds byte limit."
                );
            }

            return new GeneratedCandidate(index, Convert.FromBase64String(value), "image/png", null, null);
        }
        catch (FormatException)
        {
            throw new ProviderException(
                ProviderErrorCategory.MalformedResponse,
                "Provider returned invalid base64 image data."
            );
        }
    }

    private async Task<byte[]> DownloadAsync(
        string value,
        ProviderExecutionContext context,
        CancellationToken cancellationToken
    )
    {
        if (
            !Uri.TryCreate(value, UriKind.Absolute, out Uri? current)
            || !string.Equals(current.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
        )
        {
            throw new ProviderException(ProviderErrorCategory.UnsafeDownload, "Provider download URL must use HTTPS.");
        }

        for (int redirect = 0; redirect <= 5; redirect++)
        {
            if (!context.Provider.AllowedDownloadHosts.Contains(current.Host))
            {
                throw new ProviderException(
                    ProviderErrorCategory.UnsafeDownload,
                    $"Provider download host '{current.Host}' is not allowlisted."
                );
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, current);
            using global::System.Net.Http.HttpResponseMessage response = await Client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                global::System.Uri location =
                    response.Headers.Location
                    ?? throw new ProviderException(
                        ProviderErrorCategory.UnsafeDownload,
                        "Provider redirect omitted Location."
                    );
                current = location.IsAbsoluteUri ? location : new Uri(current, location);
                if (!string.Equals(current.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal))
                {
                    throw new ProviderException(ProviderErrorCategory.UnsafeDownload, "Provider redirect left HTTPS.");
                }

                continue;
            }

            ValidateDownloadResponse(response, context.MaximumDownloadBytes);
            return await ReadBoundedAsync(response.Content, context.MaximumDownloadBytes, cancellationToken)
                .ConfigureAwait(false);
        }

        throw new ProviderException(ProviderErrorCategory.UnsafeDownload, "Provider download exceeded redirect limit.");
    }

    private static void ValidateDownloadResponse(HttpResponseMessage response, long maximumBytes)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw Normalize(response.StatusCode, response.Headers.RetryAfter is not null);
        }

        string? mediaType = response.Content.Headers.ContentType?.MediaType;
        if (mediaType is not ("image/png" or "image/svg+xml"))
        {
            throw new ProviderException(
                ProviderErrorCategory.UnsafeDownload,
                "Provider download media type is not an allowed image type."
            );
        }

        if (response.Content.Headers.ContentLength > maximumBytes)
        {
            throw new ProviderException(ProviderErrorCategory.UnsafeDownload, "Provider download exceeds byte limit.");
        }
    }

    private static async Task<byte[]> ReadBoundedAsync(
        HttpContent content,
        long maximumBytes,
        CancellationToken cancellationToken,
        ProviderErrorCategory category = ProviderErrorCategory.UnsafeDownload,
        string message = "Provider download exceeded byte limit while streaming."
    )
    {
        Stream input = await content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        await using System.Runtime.CompilerServices.ConfiguredAsyncDisposable inputLifetime = input.ConfigureAwait(
            false
        );
        using var output = new MemoryStream();
        byte[] buffer = new byte[16_384];
        int read;
        while ((read = await input.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) != 0)
        {
            if (output.Length + read > maximumBytes)
            {
                throw new ProviderException(category, message);
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        return output.ToArray();
    }

    protected static ProviderException Normalize(HttpStatusCode status, bool hasRetryAfter) =>
        status switch
        {
            HttpStatusCode.BadRequest => new ProviderException(
                ProviderErrorCategory.InvalidRequest,
                "Provider rejected the request."
            ),
            HttpStatusCode.Unauthorized => new ProviderException(
                ProviderErrorCategory.Authentication,
                "Provider authentication failed."
            ),
            HttpStatusCode.Forbidden => new ProviderException(
                ProviderErrorCategory.Authorization,
                "Provider authorization failed."
            ),
            HttpStatusCode.RequestTimeout => new ProviderException(
                ProviderErrorCategory.Timeout,
                "Provider timed out.",
                retryable: true
            ),
            HttpStatusCode.TooManyRequests => new ProviderException(
                ProviderErrorCategory.RateLimit,
                hasRetryAfter ? "Provider rate limit included Retry-After." : "Provider rate limited the request.",
                retryable: true
            ),
            >= HttpStatusCode.InternalServerError => new ProviderException(
                ProviderErrorCategory.ProviderServer,
                "Provider server failed.",
                retryable: true
            ),
            _ => new ProviderException(ProviderErrorCategory.InvalidRequest, $"Provider returned HTTP {(int)status}."),
        };
}
