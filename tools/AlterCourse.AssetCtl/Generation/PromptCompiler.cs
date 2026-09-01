using System.Globalization;
using System.Text;

namespace AlterCourse.AssetCtl.Generation;

internal static class PromptCompiler
{
    public static class CandidateSelector
    {
        public static (GeneratedCandidate Candidate, MechanicalValidationResult Mechanical, SemanticReviewResult? Review) Select(
            IEnumerable<(GeneratedCandidate Candidate, MechanicalValidationResult Mechanical, SemanticReviewResult? Review)> candidates,
            QualityTier tier
        )
        {
            (global::AlterCourse.AssetCtl.Domain.DomainModels.GeneratedCandidate Candidate, global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult Mechanical, global::AlterCourse.AssetCtl.Domain.DomainModels.SemanticReviewResult? Review) selected = candidates
                .Where(value => value.Mechanical.Passed)
                .Where(value => value.Review is null ? !string.Equals(tier.SemanticReview, "required", StringComparison.Ordinal) : !value.Review.HasHardFailure && value.Review.OverallScore >= tier.MinimumSemanticScore)
                .OrderByDescending(value => value.Review?.OverallScore ?? 0)
                .ThenByDescending(value => value.Review?.ReadableAtTargetSizes ?? false)
                .ThenBy(value => value.Candidate.CreationOrder)
                .FirstOrDefault();
            return selected.Candidate is not null ? selected : throw new AssetCtlException("No generated candidate passed required validation and review.", 1);
        }
    }

    public const string Version = "1";

    public static (string Prompt, string Hash) Compile(AssetRequest request, StyleProfile style)
    {
        var builder = new StringBuilder();
        builder.AppendLine(CultureInfo.InvariantCulture, $"Purpose: {request.Purpose}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Asset kind: {request.Kind}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Output: {request.Output.Format.ToString().ToLowerInvariant()} {request.Output.Width}x{request.Output.Height}; transparency {(request.Output.TransparencyRequired ? "required" : "optional")}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Style: {style.Summary}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Required: {string.Join("; ", style.Required.Concat(request.Required))}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Prohibited: {string.Join("; ", style.Prohibited.Concat(request.Prohibited))}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Target display sizes: {string.Join(", ", request.Output.TargetDisplaySizes)} px");
        builder.Append($"Prompt contract version: {Version}");
        string prompt = builder.ToString();
        return (prompt, ConfigurationLoader.Hash(prompt));
    }
}
