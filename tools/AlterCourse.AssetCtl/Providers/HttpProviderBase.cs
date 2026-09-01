using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AlterCourse.AssetCtl.Providers;

internal abstract class HttpProviderBase(HttpClient httpClient, IReadOnlySet<string> allowedEndpointHosts)
{
    private static readonly JsonSerializerOptions StrictJson = new()
    {
        MaxDepth = 32,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

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

    protected async Task<T> SendJsonAsync<T>(
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
                throw Normalize(
                    response.StatusCode,
                    response.Headers.RetryAfter,
                    context.MaximumRetryAfterDelayMilliseconds
                );
            }

            if (response.Content.Headers.ContentLength > context.MaximumJsonResponseBytes)
            {
                throw new ProviderException(
                    ProviderErrorCategory.MalformedResponse,
                    "Provider JSON response exceeds byte limit."
                );
            }

            byte[] json;
            try
            {
                json = await ReadBoundedAsync(
                        response.Content,
                        context.MaximumJsonResponseBytes,
                        ProviderErrorCategory.MalformedResponse,
                        "Provider JSON response exceeded byte limit while streaming.",
                        timeout.Token
                    )
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ProviderException(
                    ProviderErrorCategory.Timeout,
                    "Provider response timed out.",
                    retryable: true
                );
            }
            try
            {
                return Deserialize<T>(json);
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

    private static T Deserialize<T>(byte[] json) =>
        JsonSerializer.Deserialize<T>(json, StrictJson)
        ?? throw new ProviderException(ProviderErrorCategory.MalformedResponse, "Provider JSON response was null.");

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
        ProviderContracts.ImageResponse response,
        ProviderExecutionContext context,
        int requestedCandidateCount,
        CancellationToken cancellationToken
    )
    {
        ValidateCandidateCount(response.Data, requestedCandidateCount);

        var candidates = new List<GeneratedCandidate>(response.Data.Length);
        long retainedBytes = 0;
        int index = 0;
        foreach (ProviderContracts.ImageResponseItem item in response.Data)
        {
            long remainingBytes = context.MaximumDownloadBytes - retainedBytes;
            if (remainingBytes <= 0)
            {
                throw new ProviderException(
                    ProviderErrorCategory.UnsafeDownload,
                    "Provider candidates exceed the aggregate byte limit."
                );
            }

            GeneratedCandidate candidate;
            if (item.Base64Json is not null && item.Url is null)
            {
                candidate = DecodeInlineCandidate(item.Base64Json, index++, remainingBytes);
            }
            else if (item.Url is not null && item.Base64Json is null)
            {
                candidate = new GeneratedCandidate(
                    index++,
                    await DownloadAsync(item.Url, context, remainingBytes, cancellationToken).ConfigureAwait(false),
                    "image/png",
                    null,
                    null
                );
            }
            else
            {
                throw new ProviderException(
                    ProviderErrorCategory.MalformedResponse,
                    "Provider did not return inline image bytes."
                );
            }

            retainedBytes = checked(retainedBytes + candidate.Bytes.LongLength);
            candidates.Add(candidate);
        }

        return candidates.Count != 0
            ? candidates
            : throw new ProviderException(ProviderErrorCategory.MalformedResponse, "Provider returned no candidates.");
    }

    private static void ValidateCandidateCount(ProviderContracts.ImageResponseItem[] data, int requestedCandidateCount)
    {
        if (requestedCandidateCount <= 0 || data.Length > requestedCandidateCount)
        {
            throw new ProviderException(
                ProviderErrorCategory.MalformedResponse,
                "Provider returned more candidates than requested."
            );
        }
    }

    private static GeneratedCandidate DecodeInlineCandidate(string value, int index, long maximumBytes)
    {
        try
        {
            int maximumDecodedLength = Base64.GetMaxDecodedFromUtf8Length(value.Length);
            if (
                maximumDecodedLength > maximumBytes
                && maximumDecodedLength - Math.Min(2, maximumDecodedLength) > maximumBytes
            )
            {
                throw new ProviderException(
                    ProviderErrorCategory.UnsafeDownload,
                    "Provider inline image exceeds byte limit."
                );
            }

            byte[] bytes = Convert.FromBase64String(value);
            if (bytes.LongLength > maximumBytes)
            {
                throw new ProviderException(
                    ProviderErrorCategory.UnsafeDownload,
                    "Provider candidates exceed the aggregate byte limit."
                );
            }

            return new GeneratedCandidate(index, bytes, "image/png", null, null);
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
        long maximumBytes,
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

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(context.TimeoutSeconds));
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
            using global::System.Net.Http.HttpResponseMessage response = await SendDownloadAsync(
                    request,
                    cancellationToken,
                    timeout.Token
                )
                .ConfigureAwait(false);
            if ((int)response.StatusCode is >= 300 and < 400)
            {
                current = ResolveRedirect(current, response);
                continue;
            }

            ValidateDownloadResponse(response, maximumBytes, context.MaximumRetryAfterDelayMilliseconds);
            try
            {
                return await ReadBoundedAsync(
                        response.Content,
                        maximumBytes,
                        ProviderErrorCategory.UnsafeDownload,
                        "Provider download exceeded byte limit while streaming.",
                        timeout.Token
                    )
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new ProviderException(
                    ProviderErrorCategory.Timeout,
                    "Provider download timed out.",
                    retryable: true
                );
            }
        }

        throw new ProviderException(ProviderErrorCategory.UnsafeDownload, "Provider download exceeded redirect limit.");
    }

    private static Uri ResolveRedirect(Uri current, HttpResponseMessage response)
    {
        global::System.Uri location =
            response.Headers.Location
            ?? throw new ProviderException(ProviderErrorCategory.UnsafeDownload, "Provider redirect omitted Location.");
        Uri redirect = location.IsAbsoluteUri ? location : new Uri(current, location);
        return string.Equals(redirect.Scheme, Uri.UriSchemeHttps, StringComparison.Ordinal)
            ? redirect
            : throw new ProviderException(ProviderErrorCategory.UnsafeDownload, "Provider redirect left HTTPS.");
    }

    private async Task<HttpResponseMessage> SendDownloadAsync(
        HttpRequestMessage request,
        CancellationToken callerToken,
        CancellationToken timeoutToken
    )
    {
        try
        {
            return await Client
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            throw new ProviderException(ProviderErrorCategory.Timeout, "Provider download timed out.", retryable: true);
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

    private static void ValidateDownloadResponse(
        HttpResponseMessage response,
        long maximumBytes,
        int maximumRetryAfterDelayMilliseconds
    )
    {
        if (!response.IsSuccessStatusCode)
        {
            throw Normalize(response.StatusCode, response.Headers.RetryAfter, maximumRetryAfterDelayMilliseconds);
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
        ProviderErrorCategory category,
        string message,
        CancellationToken cancellationToken
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

    protected static ProviderException Normalize(
        HttpStatusCode status,
        RetryConditionHeaderValue? retryAfter,
        int maximumRetryAfterDelayMilliseconds
    )
    {
        ProviderException exception = status switch
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
                retryAfter is not null
                    ? "Provider rate limit included Retry-After."
                    : "Provider rate limited the request.",
                retryable: true
            ),
            >= HttpStatusCode.InternalServerError => new ProviderException(
                ProviderErrorCategory.ProviderServer,
                "Provider server failed.",
                retryable: true
            ),
            _ => new ProviderException(ProviderErrorCategory.InvalidRequest, $"Provider returned HTTP {(int)status}."),
        };

        TimeSpan? delay = NormalizeRetryAfter(retryAfter, maximumRetryAfterDelayMilliseconds);
        if (exception.Retryable && delay is not null)
        {
            exception.Data[ProviderContracts.RetryAfterDelayDataKey] = delay.Value;
        }

        return exception;
    }

    private static TimeSpan? NormalizeRetryAfter(
        RetryConditionHeaderValue? retryAfter,
        int maximumRetryAfterDelayMilliseconds
    )
    {
        if (retryAfter is null)
        {
            return null;
        }

        TimeSpan requested = retryAfter.Delta ?? retryAfter.Date - DateTimeOffset.UtcNow ?? TimeSpan.Zero;
        var maximum = TimeSpan.FromMilliseconds(Math.Max(0, maximumRetryAfterDelayMilliseconds));
        return requested <= TimeSpan.Zero ? TimeSpan.Zero
            : requested > maximum ? maximum
            : requested;
    }
}
