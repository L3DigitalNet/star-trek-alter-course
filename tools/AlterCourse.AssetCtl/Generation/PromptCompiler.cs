using System.Globalization;
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
        builder.AppendLine(CultureInfo.InvariantCulture, $"Identity: {request.Id}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Purpose: {request.Purpose}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"Visual kind: {request.Kind}");
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"Output contract: {request.Output.Format.ToString().ToLowerInvariant()}"
        );
        builder.AppendLine(CultureInfo.InvariantCulture, $"Resolved style summary: {style.Summary}");
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"Required constraints: {string.Join("; ", style.Required.Concat(request.Required))}"
        );
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"Prohibited content: {string.Join("; ", style.Prohibited.Concat(request.Prohibited))}"
        );
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"Dimensions: {request.Output.Width}x{request.Output.Height} px"
        );
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"Target display sizes: {string.Join(", ", request.Output.TargetDisplaySizes)} px"
        );
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"Reference instructions: {ReferenceInstructions(request.References)}"
        );
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"Hard technical constraints: exact {request.Output.Width}x{request.Output.Height} {request.Output.Format.ToString().ToLowerInvariant()}; transparency {(request.Output.TransparencyRequired ? "required" : "optional")}; no external resources"
        );
        builder.AppendLine(
            CultureInfo.InvariantCulture,
            $"Lifecycle reminder: {request.Lifecycle.ToString().ToLowerInvariant()} assets are not approval-ready."
        );
        builder.Append($"Prompt contract version: {Version}");
        string prompt = builder.ToString();
        return (prompt, ConfigurationLoader.Hash(prompt));
    }

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
