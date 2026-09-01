using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AlterCourse.AssetCtl.Review;

internal static class ReviewEvidence
{
    public static string Compute(
        AssetRequest request,
        byte[] normalizedBytes,
        string effectiveConfigSha256,
        string reviewerProvider,
        string reviewerModelProfile,
        SemanticReviewResult review
    )
    {
        string assetHash = Convert.ToHexStringLower(SHA256.HashData(normalizedBytes));
        string requestJson = JsonSerializer.Serialize(request, JsonOptions.Stable);
        string reviewSummary = JsonSerializer.Serialize(
            new
            {
                decision = review.Decision,
                hard_failure = review.HasHardFailure,
                overall_score = review.OverallScore,
                independence = review.Independence,
            },
            JsonOptions.Stable
        );
        string evidence = string.Join(
            '\n',
            assetHash,
            requestJson,
            effectiveConfigSha256,
            reviewerProvider,
            reviewerModelProfile,
            reviewSummary
        );
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(evidence)));
    }
}
