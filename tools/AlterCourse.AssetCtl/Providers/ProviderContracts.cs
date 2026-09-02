namespace AlterCourse.AssetCtl.Providers;

internal static class ProviderContracts
{
    internal sealed class ImageResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("id")]
        public string? Id { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("created")]
        public long? Created { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("data")]
        [System.Text.Json.Serialization.JsonRequired]
        public ImageResponseItem?[]? Data { get; init; }
    }

    internal sealed class ImageResponseItem
    {
        [System.Text.Json.Serialization.JsonPropertyName("b64_json")]
        public string? Base64Json { get; init; }

        [System.Text.Json.Serialization.JsonPropertyName("url")]
        public string? Url { get; init; }
    }

    public sealed record ProviderExecutionContext(
        ProviderInstance Provider,
        ModelProfile Model,
        string Credential,
        int TimeoutSeconds,
        long MaximumDownloadBytes,
        long MaximumJsonResponseBytes,
        string RunId,
        int MaximumRetryAfterDelayMilliseconds = 30_000
    );

    public sealed record NormalizedGenerationRequest(
        AssetRequest Request,
        string Prompt,
        int CandidateCount,
        IReadOnlyList<(string FileName, string MediaType, byte[] Bytes)> References
    );

    public sealed record SemanticReviewRequest(
        AssetRequest Request,
        byte[] Original,
        string MediaType,
        IReadOnlyDictionary<int, byte[]> TargetPreviews,
        string RubricJsonSchema,
        string StyleSummary = "",
        IReadOnlyList<string>? StyleRequired = null,
        IReadOnlyList<string>? StyleProhibited = null
    );

    internal const string RetryAfterDelayDataKey = "AlterCourse.AssetCtl.RetryAfterDelay";

    public static TimeSpan? RetryAfterDelay(ProviderException exception) =>
        exception.Data[RetryAfterDelayDataKey] is TimeSpan delay ? delay : null;

    public interface IAssetGenerator : IAdapterDescriptor
    {
        public Task<GenerationBatchResult> GenerateAsync(
            ProviderExecutionContext context,
            NormalizedGenerationRequest request,
            CancellationToken cancellationToken
        );
    }

    public interface IAssetReviewer : IAdapterDescriptor
    {
        public Task<SemanticReviewResult> ReviewAsync(
            ProviderExecutionContext context,
            SemanticReviewRequest request,
            CancellationToken cancellationToken
        );
    }

    public sealed class AdapterRegistry
    {
        private readonly Dictionary<string, IAdapterDescriptor> adapters;

        public AdapterRegistry(IEnumerable<IAdapterDescriptor> values)
        {
            adapters = values.ToDictionary(value => value.AdapterId, StringComparer.Ordinal);
        }

        public IReadOnlyDictionary<string, IAdapterDescriptor> Descriptors => adapters;

        public IAssetGenerator Generator(string adapterId) =>
            adapters.TryGetValue(adapterId, out IAdapterDescriptor? adapter) && adapter is IAssetGenerator generator
                ? generator
                : throw new AssetCtlException($"Adapter '{adapterId}' does not generate assets.", 5);

        public IAssetReviewer Reviewer(string adapterId) =>
            adapters.TryGetValue(adapterId, out IAdapterDescriptor? adapter) && adapter is IAssetReviewer reviewer
                ? reviewer
                : throw new AssetCtlException($"Adapter '{adapterId}' does not review assets.", 5);
    }
}
