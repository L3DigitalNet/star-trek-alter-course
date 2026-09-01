using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AlterCourse.AssetCtl.Configuration;
using YamlDotNet.RepresentationModel;

namespace AlterCourse.AssetCtl.Catalog;

/// <summary>Owns the tracked manifest contract; runtime receipts and provider DTOs never become catalog authority.</summary>
internal static class ManifestStore
{
    private static readonly System.Text.RegularExpressions.Regex AssetIdPattern = new("^[a-z0-9]+(?:[.-][a-z0-9]+)+$", System.Text.RegularExpressions.RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    public static IReadOnlyList<AssetManifest> LoadAll(EffectiveConfiguration configuration)
    {
        string catalogRoot = PathPolicy.ResolveUnder(configuration.RepositoryRoot, configuration.Paths.CatalogRoot, "catalog_root", allowMissing: true);
        if (!Directory.Exists(catalogRoot))
        {
            return [];
        }

        global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest[] manifests = Directory.EnumerateFiles(catalogRoot, "*.asset.yaml", SearchOption.AllDirectories).Order(StringComparer.Ordinal).Select(path => Load(configuration, path)).ToArray();
        global::System.Linq.IGrouping<string, global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest>? duplicateId = manifests.GroupBy(manifest => manifest.Request.Id, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicateId is not null)
        {
            throw new AssetCtlException($"Duplicate asset id '{duplicateId.Key}'.", 2);
        }

        global::System.Linq.IGrouping<string, global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest>? duplicateOutput = manifests.GroupBy(manifest => manifest.Request.Output.Path, StringComparer.Ordinal).FirstOrDefault(group => group.Count() > 1);
        if (duplicateOutput is not null)
        {
            throw new AssetCtlException($"Duplicate asset output '{duplicateOutput.Key}'.", 2);
        }

        return manifests;
    }

    public static AssetManifest Load(EffectiveConfiguration configuration, string path)
    {
        string catalogRoot = PathPolicy.ResolveUnder(configuration.RepositoryRoot, configuration.Paths.CatalogRoot, "catalog_root", allowMissing: false);
        string absolute = Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(path, configuration.RepositoryRoot);
        if (!absolute.StartsWith(catalogRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new AssetCtlException("Manifest path is outside catalog root.", 2);
        }

        global::YamlDotNet.RepresentationModel.YamlMappingNode root = StrictYaml.LoadMapping(absolute);
        string id = ValidateHeader(root);

        global::AlterCourse.AssetCtl.Domain.DomainModels.AssetLifecycle lifecycle = ParseLifecycle(root.Scalar("lifecycle", "manifest"));
        global::YamlDotNet.RepresentationModel.YamlMappingNode outputNode = root.Mapping("output", "manifest");
        global::AlterCourse.AssetCtl.Domain.DomainModels.OutputContract output = ReadOutput(configuration, outputNode);

        global::YamlDotNet.RepresentationModel.YamlMappingNode visual = root.Mapping("visual", "manifest");
        visual.RequireOnly("manifest.visual", "style_profile", "importance", "tags");
        string style = visual.Scalar("style_profile", "manifest.visual");
        if (!configuration.Styles.ContainsKey(style))
        {
            throw new AssetCtlException($"manifest.visual.style_profile: unknown style '{style}'.", 2);
        }

        global::YamlDotNet.RepresentationModel.YamlMappingNode constraints = root.Mapping("constraints", "manifest");
        constraints.RequireOnly("manifest.constraints", "required", "prohibited");
        global::System.Collections.Generic.IReadOnlyList<global::AlterCourse.AssetCtl.Domain.DomainModels.AssetReference> references = ReadReferences(root.OptionalSequence("references", "manifest"));
        global::AlterCourse.AssetCtl.Domain.DomainModels.RightsRecord rights = ReadRights(root.Mapping("rights", "manifest"), lifecycle, references);
        string qualityTier = lifecycle == AssetLifecycle.Placeholder ? "development" : "production-candidate";
        global::AlterCourse.AssetCtl.Domain.DomainModels.GenerationProvenance? generation = ReadGeneration(root.OptionalMapping("generation", "manifest"), qualityTier);
        if (generation is not null)
        {
            qualityTier = generation.QualityTier;
        }

        var request = new AssetRequest(
            id,
            lifecycle,
            root.Scalar("kind", "manifest"),
            root.Scalar("purpose", "manifest"),
            output,
            style,
            YamlValues.Strings(constraints.OptionalSequence("required", "manifest.constraints"), "manifest.constraints.required"),
            YamlValues.Strings(constraints.OptionalSequence("prohibited", "manifest.constraints"), "manifest.constraints.prohibited"),
            YamlValues.Strings(visual.OptionalSequence("tags", "manifest.visual"), "manifest.visual.tags"),
            references,
            qualityTier
        );
        (MechanicalValidationResult? mechanical, SemanticReviewResult? semantic) = ReadValidation(root.OptionalMapping("validation", "manifest"));
        global::AlterCourse.AssetCtl.Domain.DomainModels.IntegrityRecord? integrity = ReadIntegrity(root.OptionalMapping("integrity", "manifest"));
        global::AlterCourse.AssetCtl.Domain.DomainModels.ApprovalRecord approval = ReadApproval(root.OptionalMapping("approval", "manifest"));
        ValidateLifecycle(lifecycle, rights, approval, integrity);
        return new AssetManifest("1", request, root.Integer("revision", "manifest"), rights, generation, mechanical, semantic, integrity, approval, root.OptionalScalar("supersedes", "manifest"), Path.GetRelativePath(configuration.RepositoryRoot, absolute));
    }

    private static string ValidateHeader(YamlMappingNode root)
    {
        root.RequireOnly("manifest", "schema_version", "id", "lifecycle", "kind", "revision", "purpose", "output", "visual", "constraints", "references", "rights", "generation", "validation", "integrity", "approval", "supersedes");
        if (!string.Equals(root.Scalar("schema_version", "manifest"), "1", StringComparison.Ordinal))
        {
            throw new AssetCtlException("manifest.schema_version: unsupported version.", 2);
        }

        string id = root.Scalar("id", "manifest");
        return AssetIdPattern.IsMatch(id) && id.Length <= 160 ? id : throw new AssetCtlException("manifest.id: expected a lowercase semantic dot-namespaced ID.", 2);
    }

    private static OutputContract ReadOutput(EffectiveConfiguration configuration, YamlMappingNode node)
    {
        node.RequireOnly("manifest.output", "path", "format", "width", "height", "transparency", "target_display_sizes");
        string outputPath = node.Scalar("path", "manifest.output");
        string assetRoot = PathPolicy.ResolveUnder(configuration.RepositoryRoot, configuration.Paths.GodotAssetRoot, "godot_asset_root", allowMissing: true);
        string resolvedOutput = PathPolicy.ResolveUnder(configuration.RepositoryRoot, outputPath, "manifest.output.path", allowMissing: true);
        if (!resolvedOutput.StartsWith(assetRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new AssetCtlException("manifest.output.path: output is outside Godot asset root.", 2);
        }

        AssetFormat format = node.Scalar("format", "manifest.output") switch
        {
            "svg" => AssetFormat.Svg,
            "png" => AssetFormat.Png,
            string value => throw new AssetCtlException($"manifest.output.format: unsupported '{value}'.", 2),
        };
        if (!outputPath.EndsWith('.' + format.ToString().ToLowerInvariant(), StringComparison.Ordinal))
        {
            throw new AssetCtlException("manifest.output: extension does not match format.", 2);
        }

        return new OutputContract(outputPath, format, node.Integer("width", "manifest.output"), node.Integer("height", "manifest.output"), string.Equals(node.Scalar("transparency", "manifest.output"), "required", StringComparison.Ordinal), YamlValues.Strings(node.Sequence("target_display_sizes", "manifest.output"), "manifest.output.target_display_sizes").Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray());
    }

    public static string Serialize(AssetManifest manifest)
    {
        var builder = new StringBuilder();
        Line(builder, 0, "schema_version", "1");
        Line(builder, 0, "id", manifest.Request.Id);
        Line(builder, 0, "lifecycle", Lifecycle(manifest.Request.Lifecycle));
        Line(builder, 0, "kind", manifest.Request.Kind);
        builder.AppendLine(CultureInfo.InvariantCulture, $"revision: {manifest.Revision}");
        Line(builder, 0, "purpose", manifest.Request.Purpose);
        builder.AppendLine("output:");
        Line(builder, 2, "path", manifest.Request.Output.Path);
        Line(builder, 2, "format", manifest.Request.Output.Format.ToString().ToLowerInvariant());
        builder.AppendLine(CultureInfo.InvariantCulture, $"  width: {manifest.Request.Output.Width}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  height: {manifest.Request.Output.Height}");
        Line(builder, 2, "transparency", manifest.Request.Output.TransparencyRequired ? "required" : "optional");
        Sequence(builder, 2, "target_display_sizes", manifest.Request.Output.TargetDisplaySizes.Select(value => value.ToString(CultureInfo.InvariantCulture)));
        builder.AppendLine("visual:");
        Line(builder, 2, "style_profile", manifest.Request.StyleProfile);
        Line(builder, 2, "importance", "secondary");
        Sequence(builder, 2, "tags", manifest.Request.Tags);
        builder.AppendLine("constraints:");
        Sequence(builder, 2, "required", manifest.Request.Required);
        Sequence(builder, 2, "prohibited", manifest.Request.Prohibited);
        builder.AppendLine("references: []");
        builder.AppendLine("rights:");
        Line(builder, 2, "classification", manifest.Rights.Classification);
        NullableLine(builder, 2, "license", manifest.Rights.License);
        NullableLine(builder, 2, "attribution", manifest.Rights.Attribution);
        NullableLine(builder, 2, "source", manifest.Rights.Source);
        NullableLine(builder, 2, "notes", manifest.Rights.Notes);
        WriteGeneration(builder, manifest.Generation);
        WriteValidation(builder, manifest.MechanicalValidation, manifest.SemanticReview);
        WriteIntegrity(builder, manifest.Integrity);

        builder.AppendLine("approval:");
        NullableLine(builder, 2, "approved_by", manifest.Approval.ApprovedBy);
        NullableLine(builder, 2, "approved_at", manifest.Approval.ApprovedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        NullableLine(builder, 2, "approval_note", manifest.Approval.ApprovalNote);
        NullableLine(builder, 0, "supersedes", manifest.Supersedes);
        return builder.ToString();
    }

    private static void WriteGeneration(StringBuilder builder, GenerationProvenance? generation)
    {
        if (generation is null)
        {
            builder.AppendLine("generation: null");
            return;
        }

        builder.AppendLine("generation:");
        Line(builder, 2, "source_type", "generated");
        Line(builder, 2, "generated_at", generation.GeneratedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Line(builder, 2, "run_id", generation.RunId);
        Line(builder, 2, "route", generation.Route);
        Line(builder, 2, "provider", generation.Provider);
        Line(builder, 2, "adapter", generation.Adapter);
        Line(builder, 2, "model_profile", generation.ModelProfile);
        Line(builder, 2, "model", generation.Model);
        Line(builder, 2, "quality_tier", generation.QualityTier);
        Line(builder, 2, "final_prompt", generation.FinalPrompt);
        Line(builder, 2, "prompt_sha256", generation.PromptSha256);
        Line(builder, 2, "request_sha256", generation.RequestSha256);
        Line(builder, 2, "effective_config_sha256", generation.EffectiveConfigSha256);
        NullableLine(builder, 2, "provider_request_id", generation.ProviderRequestId);
        builder.AppendLine(CultureInfo.InvariantCulture, $"  estimated_cost_usd: {generation.EstimatedCostUsd}");
        string actualCost = generation.ActualCostUsd?.ToString(CultureInfo.InvariantCulture) ?? "null";
        builder.AppendLine(CultureInfo.InvariantCulture, $"  actual_cost_usd: {actualCost}");
    }

    private static void WriteValidation(StringBuilder builder, MechanicalValidationResult? mechanical, SemanticReviewResult? semantic)
    {
        if (mechanical is null)
        {
            builder.AppendLine("validation: null");
            return;
        }

        builder.AppendLine("validation:");
        Line(builder, 2, "mechanical_status", mechanical.Passed ? "pass" : "fail");
        Line(builder, 2, "mechanical_validator_version", "1");
        Sequence(builder, 2, "mechanical_findings", mechanical.Findings);
        Line(builder, 2, "semantic_status", semantic is null ? "not-run" : semantic.HasHardFailure ? "fail" : "pass");
        if (semantic is not null)
        {
            builder.AppendLine(CultureInfo.InvariantCulture, $"  semantic_score: {semantic.OverallScore}");
            Line(builder, 2, "semantic_independence", semantic.Independence);
        }
    }

    private static void WriteIntegrity(StringBuilder builder, IntegrityRecord? integrity)
    {
        if (integrity is null)
        {
            builder.AppendLine("integrity: null");
            return;
        }

        builder.AppendLine("integrity:");
        Line(builder, 2, "sha256", integrity.Sha256);
        builder.AppendLine(CultureInfo.InvariantCulture, $"  byte_length: {integrity.ByteLength}");
        Line(builder, 2, "media_type", integrity.MediaType);
    }

    public static void VerifyIntegrity(EffectiveConfiguration configuration, AssetManifest manifest)
    {
        if (manifest.Integrity is null)
        {
            throw new AssetCtlException($"{manifest.Request.Id}: manifest has no integrity record.", 1);
        }

        string path = PathPolicy.ResolveUnder(configuration.RepositoryRoot, manifest.Request.Output.Path, "asset output", allowMissing: false);
        byte[] bytes = File.ReadAllBytes(path);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (!string.Equals(hash, manifest.Integrity.Sha256, StringComparison.Ordinal) || bytes.LongLength != manifest.Integrity.ByteLength)
        {
            throw new AssetCtlException($"{manifest.Request.Id}: asset integrity does not match manifest.", 7);
        }
    }

    private static List<AssetReference> ReadReferences(YamlSequenceNode? node)
    {
        if (node is null)
        {
            return [];
        }

        var result = new List<AssetReference>();
        foreach (global::YamlDotNet.RepresentationModel.YamlMappingNode item in node.Children.Cast<YamlMappingNode>())
        {
            item.RequireOnly("manifest.references", "path", "sha256", "rights_basis");
            result.Add(new AssetReference(item.Scalar("path", "manifest.references"), item.Scalar("sha256", "manifest.references"), item.Scalar("rights_basis", "manifest.references")));
        }

        return result;
    }

    private static RightsRecord ReadRights(YamlMappingNode node, AssetLifecycle lifecycle, IReadOnlyList<AssetReference> references)
    {
        node.RequireOnly("manifest.rights", "classification", "license", "attribution", "source", "notes");
        string classification = node.Scalar("classification", "manifest.rights");
        string[] allowed = new[] { "original-project-created", "original-provider-generated", "third-party-licensed", "third-party-fan-project-reference", "unreviewed-generated-placeholder", "unknown" };
        if (!allowed.Contains(classification, StringComparer.Ordinal))
        {
            throw new AssetCtlException("manifest.rights.classification: unknown classification.", 2);
        }

        if (lifecycle == AssetLifecycle.Candidate && classification is "unknown" or "unreviewed-generated-placeholder")
        {
            throw new AssetCtlException("candidate rights must identify an expected non-placeholder basis.", 1);
        }

        if (references.Any(reference => string.IsNullOrWhiteSpace(reference.RightsBasis)))
        {
            throw new AssetCtlException("every reference requires a rights basis.", 1);
        }

        return new RightsRecord(classification, node.OptionalScalar("license", "manifest.rights"), node.OptionalScalar("attribution", "manifest.rights"), node.OptionalScalar("source", "manifest.rights"), node.OptionalScalar("notes", "manifest.rights"));
    }

    private static GenerationProvenance? ReadGeneration(YamlMappingNode? node, string defaultTier)
    {
        if (node is null)
        {
            return null;
        }

        node.RequireOnly("manifest.generation", "source_type", "generated_at", "run_id", "route", "provider", "adapter", "model_profile", "model", "quality_tier", "final_prompt", "prompt_sha256", "request_sha256", "effective_config_sha256", "provider_request_id", "estimated_cost_usd", "actual_cost_usd");
        return new GenerationProvenance(DateTimeOffset.Parse(node.Scalar("generated_at", "generation"), CultureInfo.InvariantCulture), node.Scalar("run_id", "generation"), node.Scalar("route", "generation"), node.Scalar("provider", "generation"), node.Scalar("adapter", "generation"), node.Scalar("model_profile", "generation"), node.Scalar("model", "generation"), node.OptionalScalar("quality_tier", "generation") ?? defaultTier, node.Scalar("final_prompt", "generation"), node.Scalar("prompt_sha256", "generation"), node.Scalar("request_sha256", "generation"), node.Scalar("effective_config_sha256", "generation"), node.OptionalScalar("provider_request_id", "generation"), decimal.Parse(node.Scalar("estimated_cost_usd", "generation"), CultureInfo.InvariantCulture), ParseNullableDecimal(node.OptionalScalar("actual_cost_usd", "generation")));
    }

    private static IntegrityRecord? ReadIntegrity(YamlMappingNode? node)
    {
        if (node is null)
        {
            return null;
        }

        node.RequireOnly("manifest.integrity", "sha256", "byte_length", "media_type");
        string sha = node.Scalar("sha256", "integrity");
        if (sha.Length != 64 || sha.Any(character => !Uri.IsHexDigit(character)))
        {
            throw new AssetCtlException("manifest.integrity.sha256: expected lowercase SHA-256.", 2);
        }

        return new IntegrityRecord(sha.ToLowerInvariant(), node.Long("byte_length", "integrity"), node.Scalar("media_type", "integrity"));
    }

    private static (MechanicalValidationResult?, SemanticReviewResult?) ReadValidation(YamlMappingNode? node)
    {
        if (node is null)
        {
            return (null, null);
        }

        node.RequireOnly("manifest.validation", "mechanical_status", "mechanical_validator_version", "mechanical_findings", "semantic_status", "semantic_score", "semantic_independence");
        bool passed = string.Equals(node.Scalar("mechanical_status", "manifest.validation"), "pass", StringComparison.Ordinal);
        var mechanical = new MechanicalValidationResult(passed, string.Empty, 0, 0, false, YamlValues.Strings(node.OptionalSequence("mechanical_findings", "manifest.validation"), "manifest.validation.mechanical_findings"), [], new Dictionary<int, byte[]>());
        string semanticStatus = node.Scalar("semantic_status", "manifest.validation");
        if (string.Equals(semanticStatus, "not-run", StringComparison.Ordinal))
        {
            return (mechanical, null);
        }

        double score = double.Parse(node.Scalar("semantic_score", "manifest.validation"), CultureInfo.InvariantCulture);
        bool semanticPassed = string.Equals(semanticStatus, "pass", StringComparison.Ordinal);
        SemanticReviewResult semantic = new(semanticPassed, semanticPassed, semanticPassed, semanticPassed, score, score, [], false, false, score, semanticStatus, node.Scalar("semantic_independence", "manifest.validation"));
        return (mechanical, semantic);
    }

    private static ApprovalRecord ReadApproval(YamlMappingNode? node)
    {
        if (node is null)
        {
            return new ApprovalRecord(null, null, null);
        }

        node.RequireOnly("manifest.approval", "approved_by", "approved_at", "approval_note");
        string? date = node.OptionalScalar("approved_at", "approval");
        return new ApprovalRecord(node.OptionalScalar("approved_by", "approval"), date is null ? null : DateTimeOffset.Parse(date, CultureInfo.InvariantCulture), node.OptionalScalar("approval_note", "approval"));
    }

    private static void ValidateLifecycle(AssetLifecycle lifecycle, RightsRecord rights, ApprovalRecord approval, IntegrityRecord? integrity)
    {
        if (lifecycle == AssetLifecycle.Approved)
        {
            if (rights.Classification is "unknown" or "unreviewed-generated-placeholder" || string.IsNullOrWhiteSpace(rights.Notes) && string.IsNullOrWhiteSpace(rights.License) || approval.ApprovedBy is null || approval.ApprovedAt is null || approval.ApprovalNote is null || integrity is null)
            {
                throw new AssetCtlException("approved asset lacks rights, approval, or integrity evidence.", 1);
            }
        }
    }

    private static AssetLifecycle ParseLifecycle(string value) => value switch { "placeholder" => AssetLifecycle.Placeholder, "candidate" => AssetLifecycle.Candidate, "approved" => AssetLifecycle.Approved, "deprecated" => AssetLifecycle.Deprecated, _ => throw new AssetCtlException($"manifest.lifecycle: unknown '{value}'.", 2) };
    private static string Lifecycle(AssetLifecycle value) => value.ToString().ToLowerInvariant();
    private static decimal? ParseNullableDecimal(string? value) => value is null ? null : decimal.Parse(value, CultureInfo.InvariantCulture);
    private static void Line(StringBuilder builder, int indentation, string key, string value) => builder.Append(' ', indentation).Append(key).Append(": '").Append(value.Replace("'", "''", StringComparison.Ordinal)).AppendLine("'");
    private static void NullableLine(StringBuilder builder, int indentation, string key, string? value) { if (value is null) builder.Append(' ', indentation).Append(key).AppendLine(": null"); else Line(builder, indentation, key, value); }
    private static void Sequence(StringBuilder builder, int indentation, string key, IEnumerable<string> values) { string[] array = values.ToArray(); if (array.Length == 0) { builder.Append(' ', indentation).Append(key).AppendLine(": []"); return; } builder.Append(' ', indentation).Append(key).AppendLine(":"); foreach (string value in array) builder.Append(' ', indentation + 2).Append("- '").Append(value.Replace("'", "''", StringComparison.Ordinal)).AppendLine("'"); }
}
