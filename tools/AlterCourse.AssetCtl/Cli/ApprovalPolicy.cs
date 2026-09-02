using System.Text.Json;
using AlterCourse.AssetCtl.Validation;

namespace AlterCourse.AssetCtl.Cli;

internal static class ApprovalPolicy
{
    public static void Validate(EffectiveConfiguration configuration, AssetManifest manifest)
    {
        ManifestStore.VerifyIntegrity(configuration, manifest);
        string assetPath = PathPolicy.ResolveOutputPath(
            configuration,
            manifest.Request.Output.Path,
            allowMissing: false
        );
        byte[] bytes = File.ReadAllBytes(assetPath);
        MechanicalValidationResult mechanical = MechanicalValidator.Validate(
            manifest.Request,
            bytes,
            configuration.Limits.MaximumDownloadBytes,
            configuration.Limits.MaximumDecodedPixels
        );
        if (!mechanical.Passed)
        {
            throw new AssetCtlException("Approval requires current bytes to pass mechanical validation.", 8);
        }

        QualityTier tier = configuration.QualityTiers[manifest.Request.QualityTier];
        if (string.Equals(tier.SemanticReview, "required", StringComparison.Ordinal))
        {
            ValidateSemanticEvidence(configuration, manifest, tier, mechanical.NormalizedBytes);
        }
    }

    private static void ValidateSemanticEvidence(
        EffectiveConfiguration configuration,
        AssetManifest manifest,
        QualityTier tier,
        byte[] normalizedBytes
    )
    {
        GenerationProvenance generation =
            manifest.Generation
            ?? throw new AssetCtlException("Approval requires generation provenance for semantic evidence.", 8);
        SemanticReviewResult review =
            manifest.SemanticReview ?? throw new AssetCtlException("Approval requires semantic review evidence.", 8);
        string requestHash = ConfigurationLoader.Hash(JsonSerializer.Serialize(manifest.Request, JsonOptions.Stable));
        if (
            review.HasHardFailure
            || review.OverallScore < tier.MinimumSemanticScore
            || !string.Equals(generation.RequestSha256, requestHash, StringComparison.Ordinal)
            || !string.Equals(generation.EffectiveConfigSha256, configuration.EffectiveHash, StringComparison.Ordinal)
            || review.ReviewerProvider is null
            || review.ReviewerModelProfile is null
            || review.EvidenceSha256 is null
            || !configuration.Providers.TryGetValue(review.ReviewerProvider, out ProviderInstance? reviewer)
            || !reviewer.Models.TryGetValue(review.ReviewerModelProfile, out ModelProfile? reviewerModel)
            || !reviewerModel.Capabilities.Contains(AssetCapability.ReviewSemantic)
            || string.Equals(
                ProviderFamily(reviewer.AdapterId),
                ProviderFamily(generation.Adapter),
                StringComparison.Ordinal
            )
            || !string.Equals(review.Independence, "different-provider-family", StringComparison.Ordinal)
            || !ReviewEvidence.Verify(
                manifest.Request,
                normalizedBytes,
                configuration.EffectiveHash,
                review.ReviewerProvider,
                review.ReviewerModelProfile,
                review
            )
        )
        {
            throw new AssetCtlException("Approval requires current independently verifiable semantic evidence.", 8);
        }
    }

    private static string ProviderFamily(string adapterId)
    {
        int separator = adapterId.IndexOf('-', StringComparison.Ordinal);
        return separator < 0 ? adapterId : adapterId[..separator];
    }
}
