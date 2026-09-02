using System.Collections.ObjectModel;

namespace AlterCourse.AssetCtl.Domain;

internal static class DomainModels
{
    /// <summary>Version identifiers for durable AssetCtl provenance and semantic-review contracts.</summary>
    public static class AssetContractVersions
    {
        /// <summary>Current provider-adapter provenance contract.</summary>
        public const string Adapter = "1";

        /// <summary>Current generation provenance schema.</summary>
        public const string Provenance = "1";

        /// <summary>Current structured semantic-review rubric.</summary>
        public const string SemanticRubric = "1";
    }

    public enum AssetLifecycle
    {
        Placeholder,
        Candidate,
        Approved,
        Deprecated,
    }

    public enum AssetFormat
    {
        Svg,
        Png,
    }

    public enum OutputTransparency
    {
        Required,
        Optional,
    }

    public enum SemanticReviewPolicy
    {
        Disabled,
        WhenAvailable,
        Required,
    }

    public enum AssetCapability
    {
        RasterGenerate,
        VectorGenerate,
        ImageEdit,
        ImageReferenceInput,
        ImageTransparentOutput,
        ImageBackgroundRemove,
        ImageVectorize,
        ReviewSemantic,
        ReviewReferenceComparison,
    }

    public enum ProviderErrorCategory
    {
        InvalidRequest,
        Authentication,
        Authorization,
        InsufficientBalance,
        RateLimit,
        TransientNetwork,
        ProviderServer,
        Timeout,
        MalformedResponse,
        UnsafeDownload,
        UnsupportedOutput,
        Validation,
    }

    public sealed record OutputContract(
        string Path,
        AssetFormat Format,
        int Width,
        int Height,
        bool TransparencyRequired,
        IReadOnlyList<int> TargetDisplaySizes
    )
    {
        public OutputTransparency Transparency =>
            TransparencyRequired ? OutputTransparency.Required : OutputTransparency.Optional;
    }

    public sealed record AssetReference(string Path, string Sha256, string RightsBasis);

    public sealed record RightsRecord(
        string Classification,
        string? License,
        string? Attribution,
        string? Source,
        string? Notes
    );

    public sealed record ApprovalRecord(string? ApprovedBy, DateTimeOffset? ApprovedAt, string? ApprovalNote);

    public sealed record DeprecationRecord(string Actor, DateTimeOffset DeprecatedAt, string Reason);

    public sealed record AssetRequest(
        string Id,
        AssetLifecycle Lifecycle,
        string Kind,
        string Purpose,
        OutputContract Output,
        string StyleProfile,
        string Importance,
        IReadOnlyList<string> Required,
        IReadOnlyList<string> Prohibited,
        IReadOnlyList<string> Tags,
        IReadOnlyList<AssetReference> References,
        string QualityTier
    );

    public sealed record GenerationProvenance(
        DateTimeOffset GeneratedAt,
        string RunId,
        string Route,
        string Provider,
        string Adapter,
        string ModelProfile,
        string Model,
        string QualityTier,
        string FinalPrompt,
        string PromptSha256,
        string RequestSha256,
        string EffectiveConfigSha256,
        string? ProviderRequestId,
        decimal? EstimatedCostUsd,
        decimal? ActualCostUsd,
        string AdapterVersion = AssetContractVersions.Adapter,
        string ProvenanceSchemaVersion = AssetContractVersions.Provenance
    );

    public sealed record IntegrityRecord(string Sha256, long ByteLength, string MediaType);

    public sealed record MechanicalValidationResult(
        bool Passed,
        string MediaType,
        int Width,
        int Height,
        bool HasAlpha,
        IReadOnlyList<string> Findings,
        byte[] NormalizedBytes,
        IReadOnlyDictionary<int, byte[]> TargetPreviews
    );

    public sealed record SemanticReviewResult(
        bool MatchesSubject,
        bool RequiredConstraintsSatisfied,
        bool ProhibitedContentAbsent,
        bool ReadableAtTargetSizes,
        double StyleAdherence,
        double SemanticClarity,
        IReadOnlyList<string> VisualDefects,
        bool UnrequestedTextDetected,
        bool LogoOrWatermarkDetected,
        double OverallScore,
        string Decision,
        string Independence,
        string? EvidenceSha256 = null,
        string? ReviewerProvider = null,
        string? ReviewerModelProfile = null,
        string SemanticRubricVersion = AssetContractVersions.SemanticRubric
    )
    {
        public bool HasHardFailure =>
            !MatchesSubject
            || !RequiredConstraintsSatisfied
            || !ProhibitedContentAbsent
            || !ReadableAtTargetSizes
            || UnrequestedTextDetected
            || LogoOrWatermarkDetected
            || !string.Equals(Decision, "pass", StringComparison.Ordinal);
    }

    public sealed record AssetManifest(
        string SchemaVersion,
        AssetRequest Request,
        int Revision,
        RightsRecord Rights,
        GenerationProvenance? Generation,
        MechanicalValidationResult? MechanicalValidation,
        SemanticReviewResult? SemanticReview,
        IntegrityRecord? Integrity,
        ApprovalRecord Approval,
        string? Supersedes,
        string ManifestPath,
        DeprecationRecord? Deprecation = null
    );

    public sealed record GeneratedCandidate(
        int CreationOrder,
        byte[] Bytes,
        string MediaType,
        string? ProviderRequestId,
        decimal? ActualCostUsd
    );

    public sealed record GenerationBatchResult(
        IReadOnlyList<GeneratedCandidate> Candidates,
        string? ProviderRequestId,
        decimal? ActualCostUsd
    );

    public sealed record ModelProfile(
        string Id,
        string VendorModel,
        IReadOnlySet<AssetCapability> Capabilities,
        decimal? EstimatedCostPerOutput,
        string PricingBasis,
        IReadOnlyDictionary<string, string> Options
    );

    public sealed record ProviderInstance(
        string Id,
        string AdapterId,
        bool Enabled,
        Uri? Endpoint,
        string? CredentialEnvironmentVariable,
        IReadOnlySet<string> AllowedDownloadHosts,
        IReadOnlyDictionary<string, ModelProfile> Models,
        IReadOnlySet<AssetLifecycle>? AllowedLifecycles = null,
        IReadOnlySet<string>? AllowedEndpointHosts = null
    );

    public sealed record RouteTarget(string ProviderId, string ModelProfileId);

    public sealed record RouteFallbackPolicy(
        bool CapabilityMatch,
        IReadOnlySet<ProviderErrorCategory> AllowedErrorCategories
    );

    public sealed record RouteRetryPolicy(
        int MaximumAttemptsPerTarget,
        int InitialDelayMilliseconds,
        int MaximumDelayMilliseconds,
        double JitterRatio,
        IReadOnlySet<ProviderErrorCategory> ErrorCategories
    );

    public sealed record RouteDefinition(
        string Id,
        int Priority,
        AssetLifecycle? Lifecycle,
        AssetFormat? Format,
        AssetCapability Capability,
        IReadOnlyList<RouteTarget> Targets,
        int ConfigurationOrder,
        RouteFallbackPolicy? FallbackPolicy = null,
        RouteRetryPolicy? RetryPolicy = null
    );

    public sealed record QualityTier(
        string Id,
        int Candidates,
        int AttemptsPerRoute,
        string SemanticReview,
        bool AllowUnreviewedPlaceholder,
        double MinimumSemanticScore
    )
    {
        public SemanticReviewPolicy ReviewPolicy =>
            SemanticReview switch
            {
                "disabled" => SemanticReviewPolicy.Disabled,
                "when-available" => SemanticReviewPolicy.WhenAvailable,
                "required" => SemanticReviewPolicy.Required,
                _ => throw new InvalidOperationException($"Unsupported semantic-review policy '{SemanticReview}'."),
            };
    }

    public sealed record SchemaDocumentStatus(string Path, string Draft, bool Valid);

    public sealed record WritableRootStatus(string Name, string Path, bool Writable, string Basis);

    public sealed record RouteIntegrityStatus(
        bool Valid,
        int GenerationRoutes,
        int ReviewRoutes,
        int Targets,
        int FallbackPolicies,
        int RetryPolicies
    );

    public sealed record PlannedTarget(
        string RouteId,
        string ProviderId,
        string ModelProfileId,
        string AdapterId,
        bool Eligible,
        IReadOnlyList<string> RejectionReasons,
        decimal? EstimatedMaximumCost,
        string EstimateBasis = "fixed-output"
    );

    public sealed record GenerationPlan(
        AssetRequest Request,
        IReadOnlyList<AssetCapability> RequiredCapabilities,
        IReadOnlyList<PlannedTarget> Targets,
        PlannedTarget? SelectedTarget,
        PlannedTarget? Reviewer,
        int CandidateCount,
        int AttemptsPerRoute,
        decimal? EstimatedMaximumCost,
        bool UsesLocalFallback
    );

    public sealed record StyleProfile(
        string Id,
        string Summary,
        IReadOnlyList<string> Required,
        IReadOnlyList<string> Prohibited
    );

    public sealed record AssetCtlPaths(
        string GodotAssetRoot,
        string CatalogRoot,
        string StyleRoot,
        string WorkRoot,
        string ReceiptRoot,
        string StateRoot,
        string LogRoot
    );

    public sealed record AssetCtlPolicy(
        bool ExternalGenerationEnabled,
        bool ProtectApprovedAssets,
        bool LocalPlaceholderFallback,
        bool RequireHttpsEndpoints,
        bool AllowRemoteReferenceUrls,
        string UnknownPricePolicy,
        bool RetainUnselectedCandidates = false
    );

    public sealed record AssetCtlLimits(
        long MaximumDownloadBytes,
        long MaximumReferenceBytes,
        int MaximumCandidatesPerRequest,
        int MaximumTotalAttempts,
        int DefaultHttpTimeoutSeconds,
        int MaximumHttpTimeoutSeconds,
        long MaximumDecodedPixels
    );

    public sealed record SpendingLimits(decimal PerAssetUsd, decimal PerRunUsd, decimal PerDayUsd);

    public sealed record EffectiveConfiguration(
        string RepositoryRoot,
        AssetCtlPaths Paths,
        AssetCtlPolicy Policy,
        AssetCtlLimits Limits,
        SpendingLimits Spending,
        IReadOnlyDictionary<string, ProviderInstance> Providers,
        IReadOnlyList<RouteDefinition> Routes,
        IReadOnlyList<RouteDefinition> ReviewRoutes,
        IReadOnlyDictionary<string, QualityTier> QualityTiers,
        IReadOnlyDictionary<string, StyleProfile> Styles,
        IReadOnlyDictionary<string, string> FileHashes,
        string EffectiveHash
    )
    {
        public static IReadOnlyDictionary<TKey, TValue> ReadOnly<TKey, TValue>(IDictionary<TKey, TValue> values)
            where TKey : notnull => new ReadOnlyDictionary<TKey, TValue>(values);
    }

    public sealed class AssetCtlException(string message, int exitCode = 1) : Exception(message)
    {
        public int ExitCode { get; } = exitCode;
    }

    public sealed class ProviderException(ProviderErrorCategory category, string message, bool retryable = false)
        : Exception(message)
    {
        public ProviderErrorCategory Category { get; } = category;

        public bool Retryable { get; } = retryable;
    }
}
