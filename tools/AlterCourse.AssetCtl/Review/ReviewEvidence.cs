using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AlterCourse.AssetCtl.Review;

internal static class ReviewEvidence
{
    public static bool Verify(
        AssetRequest request,
        byte[] normalizedBytes,
        string effectiveConfigSha256,
        string reviewerProvider,
        string reviewerModelProfile,
        SemanticReviewResult review
    )
    {
        if (review.EvidenceSha256 is null || review.EvidenceSha256.Length != 64)
        {
            return false;
        }

        byte[] supplied;
        try
        {
            supplied = Convert.FromHexString(review.EvidenceSha256);
        }
        catch (FormatException)
        {
            return false;
        }

        string expectedHex = Compute(
            request,
            normalizedBytes,
            effectiveConfigSha256,
            reviewerProvider,
            reviewerModelProfile,
            review
        );
        byte[] expected = Convert.FromHexString(expectedHex);
        return CryptographicOperations.FixedTimeEquals(supplied, expected);
    }

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
        // Evidence deliberately excludes EvidenceSha256 itself while binding every reviewer-controlled field.
        // Git history plus explicit owner approval is the trust boundary; this digest detects editable drift only.
        string reviewSummary = JsonSerializer.Serialize(
            new
            {
                matches_subject = review.MatchesSubject,
                required_constraints_satisfied = review.RequiredConstraintsSatisfied,
                prohibited_content_absent = review.ProhibitedContentAbsent,
                readable_at_target_sizes = review.ReadableAtTargetSizes,
                style_adherence = review.StyleAdherence,
                semantic_clarity = review.SemanticClarity,
                visual_defects = review.VisualDefects,
                unrequested_text_detected = review.UnrequestedTextDetected,
                logo_or_watermark_detected = review.LogoOrWatermarkDetected,
                decision = review.Decision,
                overall_score = review.OverallScore,
                independence = review.Independence,
                reviewer_provider = review.ReviewerProvider,
                reviewer_model_profile = review.ReviewerModelProfile,
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
