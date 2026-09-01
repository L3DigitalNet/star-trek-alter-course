namespace AlterCourse.AssetCtl.Providers;

internal static class ProviderContracts
{
    public sealed record ProviderExecutionContext(
        ProviderInstance Provider,
        ModelProfile Model,
        string Credential,
        int TimeoutSeconds,
        long MaximumDownloadBytes,
        long MaximumJsonResponseBytes,
        string RunId
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
        string RubricJsonSchema
    );

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
