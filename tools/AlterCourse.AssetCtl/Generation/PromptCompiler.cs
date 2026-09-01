using System.Text;

namespace AlterCourse.AssetCtl.Generation;

internal static class PromptCompiler
{
    public static class CandidateSelector
    {
        public static (
            GeneratedCandidate Candidate,
            MechanicalValidationResult Mechanical,
            SemanticReviewResult? Review
        ) Select(
            IEnumerable<(
                GeneratedCandidate Candidate,
                MechanicalValidationResult Mechanical,
                SemanticReviewResult? Review
            )> candidates,
            QualityTier tier
        )
        {
            (
                global::AlterCourse.AssetCtl.Domain.DomainModels.GeneratedCandidate Candidate,
                global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult Mechanical,
                global::AlterCourse.AssetCtl.Domain.DomainModels.SemanticReviewResult? Review
            ) selected = candidates
                .Where(value => value.Mechanical.Passed)
                .Where(value =>
                    value.Review is null
                        ? !string.Equals(tier.SemanticReview, "required", StringComparison.Ordinal)
                        : !value.Review.HasHardFailure && value.Review.OverallScore >= tier.MinimumSemanticScore
                )
                .OrderByDescending(value => value.Review?.OverallScore ?? 0)
                .ThenByDescending(value => value.Review?.ReadableAtTargetSizes ?? false)
                .ThenBy(value => value.Candidate.CreationOrder)
                .FirstOrDefault();
            return selected.Candidate is not null
                ? selected
                : throw new AssetCtlException("No generated candidate passed required validation and review.", 1);
        }
    }

    public const string Version = "2";

    public static (string Prompt, string Hash) Compile(AssetRequest request, StyleProfile style)
    {
        var builder = new StringBuilder();
        AppendLine(builder, $"Identity: {request.Id}");
        AppendLine(builder, $"Purpose: {request.Purpose}");
        AppendLine(builder, $"Visual kind: {request.Kind}");
        AppendLine(builder, $"Output contract: {request.Output.Format.ToString().ToLowerInvariant()}");
        AppendLine(builder, $"Resolved style summary: {style.Summary}");
        AppendLine(builder, $"Required constraints: {string.Join("; ", style.Required.Concat(request.Required))}");
        AppendLine(builder, $"Prohibited content: {string.Join("; ", style.Prohibited.Concat(request.Prohibited))}");
        AppendLine(builder, $"Dimensions: {request.Output.Width}x{request.Output.Height} px");
        AppendLine(builder, $"Target display sizes: {string.Join(", ", request.Output.TargetDisplaySizes)} px");
        AppendLine(builder, $"Reference instructions: {ReferenceInstructions(request.References)}");
        AppendLine(
            builder,
            $"Hard technical constraints: exact {request.Output.Width}x{request.Output.Height} {request.Output.Format.ToString().ToLowerInvariant()}; transparency {(request.Output.TransparencyRequired ? "required" : "optional")}; no external resources"
        );
        AppendLine(
            builder,
            $"Lifecycle reminder: {request.Lifecycle.ToString().ToLowerInvariant()} assets must remain functionally clear rather than polished."
        );
        builder.Append($"Prompt contract version: {Version}");
        string prompt = builder.ToString();
        return (prompt, ConfigurationLoader.Hash(prompt));
    }

    private static void AppendLine(StringBuilder builder, string value) => builder.Append(value).Append('\n');

    private static string ReferenceInstructions(IReadOnlyList<AssetReference> references) =>
        references.Count == 0
            ? "none"
            : string.Join(
                "; ",
                references
                    .OrderBy(reference => reference.Path, StringComparer.Ordinal)
                    .ThenBy(reference => reference.Sha256, StringComparer.Ordinal)
                    .Select(reference => $"use {reference.Path} only under rights basis {reference.RightsBasis}")
            );
}
