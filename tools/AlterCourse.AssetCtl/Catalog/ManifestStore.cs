using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using AlterCourse.AssetCtl.Configuration;
using YamlDotNet.RepresentationModel;
using AssetContractVersions = AlterCourse.AssetCtl.Domain.DomainModels.AssetContractVersions;

namespace AlterCourse.AssetCtl.Catalog;

/// <summary>Owns the tracked manifest contract; runtime receipts and provider DTOs never become catalog authority.</summary>
internal static class ManifestStore
{
    private static readonly HashSet<string> AssetKinds = new(StringComparer.Ordinal)
    {
        "icon",
        "map-marker",
        "ship-sprite",
        "emblem",
        "illustration",
        "background",
        "texture",
        "other",
    };

    private static readonly System.Text.RegularExpressions.Regex AssetIdPattern = new(
        "^[a-z0-9]+(?:[.-][a-z0-9]+)+$",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100)
    );

    public static IReadOnlyList<AssetManifest> LoadAll(EffectiveConfiguration configuration)
    {
        string catalogRoot = PathPolicy.ResolveUnder(
            configuration.RepositoryRoot,
            configuration.Paths.CatalogRoot,
            "catalog_root",
            allowMissing: true
        );
        if (!Directory.Exists(catalogRoot))
        {
            return [];
        }

        global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest[] manifests = Directory
            .EnumerateFiles(catalogRoot, "*.asset.yaml", SearchOption.AllDirectories)
            .Order(StringComparer.Ordinal)
            .Select(path => Load(configuration, path))
            .ToArray();
        global::System.Linq.IGrouping<
            string,
            global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest
        >? duplicateId = manifests
            .GroupBy(manifest => manifest.Request.Id, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateId is not null)
        {
            throw new AssetCtlException($"Duplicate asset id '{duplicateId.Key}'.", 2);
        }

        global::System.Linq.IGrouping<
            string,
            global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest
        >? duplicateOutput = manifests
            .GroupBy(manifest => manifest.Request.Output.Path, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateOutput is not null)
        {
            throw new AssetCtlException($"Duplicate asset output '{duplicateOutput.Key}'.", 2);
        }

        return manifests;
    }

    public static AssetManifest Load(EffectiveConfiguration configuration, string path)
    {
        string repositoryRelativePath = Path.IsPathRooted(path)
            ? Path.GetRelativePath(configuration.RepositoryRoot, path)
            : path;
        // The configured-root resolver walks every existing path component before StrictYaml opens the file.
        // A textual prefix check cannot detect a catalog symlink that resolves outside the repository.
        string absolute = PathPolicy.ResolveManifestPath(configuration, repositoryRelativePath, allowMissing: false);
        EnsurePhysicalCatalogContainment(configuration, absolute);

        global::YamlDotNet.RepresentationModel.YamlMappingNode root = StrictYaml.LoadMapping(absolute);
        string id = ValidateHeader(root);

        global::AlterCourse.AssetCtl.Domain.DomainModels.AssetLifecycle lifecycle = ParseLifecycle(
            root.Scalar("lifecycle", "manifest")
        );
        global::YamlDotNet.RepresentationModel.YamlMappingNode outputNode = root.Mapping("output", "manifest");
        global::AlterCourse.AssetCtl.Domain.DomainModels.OutputContract output = ReadOutput(configuration, outputNode);
        (AssetRequest request, RightsRecord rights, GenerationProvenance? generation) = ReadManifestBody(
            configuration,
            root,
            id,
            lifecycle,
            output
        );
        (MechanicalValidationResult? mechanical, SemanticReviewResult? semantic) = ReadValidation(
            root.OptionalMapping("validation", "manifest")
        );
        global::AlterCourse.AssetCtl.Domain.DomainModels.IntegrityRecord? integrity = ReadIntegrity(
            root.OptionalMapping("integrity", "manifest")
        );
        global::AlterCourse.AssetCtl.Domain.DomainModels.ApprovalRecord approval = ReadApproval(
            root.OptionalMapping("approval", "manifest")
        );
        DeprecationRecord? deprecation = ReadDeprecation(root.OptionalMapping("deprecation", "manifest"));
        ValidateLifecycle(lifecycle, rights, approval, integrity, deprecation);
        return new AssetManifest(
            "1",
            request,
            root.Integer("revision", "manifest"),
            rights,
            generation,
            mechanical,
            semantic,
            integrity,
            approval,
            root.OptionalScalar("supersedes", "manifest"),
            Path.GetRelativePath(configuration.RepositoryRoot, absolute),
            deprecation
        );
    }

    public static AssetManifest LoadCatalogEntry(EffectiveConfiguration configuration, string path)
    {
        string absolute = PathPolicy.ResolveManifestPath(configuration, path, allowMissing: false);
        string relative = Path.GetRelativePath(configuration.RepositoryRoot, absolute);
        return LoadAll(configuration)
                .SingleOrDefault(manifest => string.Equals(manifest.ManifestPath, relative, StringComparison.Ordinal))
            ?? throw new AssetCtlException("Manifest is not an authoritative entry in the configured catalog.", 2);
    }

    public static void ValidatePublicationOwnership(
        EffectiveConfiguration configuration,
        string manifestPath,
        string outputPath
    )
    {
        AssetManifest owner = LoadCatalogEntry(configuration, manifestPath);
        if (!string.Equals(owner.Request.Output.Path, outputPath, StringComparison.Ordinal))
        {
            throw new AssetCtlException("Publication output is not owned by the selected catalog manifest.", 2);
        }
    }

    private static void EnsurePhysicalCatalogContainment(EffectiveConfiguration configuration, string manifestPath)
    {
        string catalogRoot = PathPolicy.ResolveUnder(
            configuration.RepositoryRoot,
            configuration.Paths.CatalogRoot,
            "catalog_root",
            allowMissing: false
        );
        string current = catalogRoot;
        foreach (string segment in Path.GetRelativePath(catalogRoot, manifestPath).Split(Path.DirectorySeparatorChar))
        {
            current = Path.Combine(current, segment);
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != FileAttributes.ReparsePoint)
            {
                continue;
            }

            string? resolved = File.ResolveLinkTarget(current, returnFinalTarget: true)?.FullName;
            if (
                resolved is null
                || !Path.GetFullPath(resolved)
                    .StartsWith(catalogRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            )
            {
                throw new AssetCtlException("Manifest path symlink escapes the configured catalog root.", 2);
            }
        }
    }

    private static (AssetRequest Request, RightsRecord Rights, GenerationProvenance? Generation) ReadManifestBody(
        EffectiveConfiguration configuration,
        YamlMappingNode root,
        string id,
        AssetLifecycle lifecycle,
        OutputContract output
    )
    {
        YamlMappingNode visual = root.Mapping("visual", "manifest");
        visual.RequireOnly("manifest.visual", "style_profile", "importance", "tags");
        string style = visual.Scalar("style_profile", "manifest.visual");
        if (!configuration.Styles.ContainsKey(style))
        {
            throw new AssetCtlException($"manifest.visual.style_profile: unknown style '{style}'.", 2);
        }

        YamlMappingNode constraints = root.Mapping("constraints", "manifest");
        constraints.RequireOnly("manifest.constraints", "required", "prohibited");
        IReadOnlyList<AssetReference> references = ReadReferences(root.OptionalSequence("references", "manifest"));
        RightsRecord rights = ReadRights(root.Mapping("rights", "manifest"), lifecycle, references);
        string qualityTier = root.Scalar("quality_tier", "manifest");
        if (!configuration.QualityTiers.ContainsKey(qualityTier))
        {
            throw new AssetCtlException($"manifest.quality_tier: unknown quality tier '{qualityTier}'.", 2);
        }
        string kind = root.Scalar("kind", "manifest");
        if (!AssetKinds.Contains(kind))
        {
            throw new AssetCtlException($"manifest.kind: unsupported '{kind}'.", 2);
        }
        GenerationProvenance? generation = ReadGeneration(root.OptionalMapping("generation", "manifest"), qualityTier);

        var request = new AssetRequest(
            id,
            lifecycle,
            kind,
            root.Scalar("purpose", "manifest"),
            output,
            style,
            visual.Scalar("importance", "manifest.visual"),
            YamlValues.Strings(
                constraints.OptionalSequence("required", "manifest.constraints"),
                "manifest.constraints.required"
            ),
            YamlValues.Strings(
                constraints.OptionalSequence("prohibited", "manifest.constraints"),
                "manifest.constraints.prohibited"
            ),
            YamlValues.Strings(visual.OptionalSequence("tags", "manifest.visual"), "manifest.visual.tags"),
            references,
            qualityTier
        );
        return (request, rights, generation);
    }

    private static string ValidateHeader(YamlMappingNode root)
    {
        root.RequireOnly(
            "manifest",
            "schema_version",
            "id",
            "lifecycle",
            "quality_tier",
            "kind",
            "revision",
            "purpose",
            "output",
            "visual",
            "constraints",
            "references",
            "rights",
            "generation",
            "validation",
            "integrity",
            "approval",
            "deprecation",
            "supersedes"
        );
        if (!string.Equals(root.Scalar("schema_version", "manifest"), "1", StringComparison.Ordinal))
        {
            throw new AssetCtlException("manifest.schema_version: unsupported version.", 2);
        }

        string id = root.Scalar("id", "manifest");
        return AssetIdPattern.IsMatch(id) && id.Length <= 160
            ? id
            : throw new AssetCtlException("manifest.id: expected a lowercase semantic dot-namespaced ID.", 2);
    }

    private static OutputContract ReadOutput(EffectiveConfiguration configuration, YamlMappingNode node)
    {
        node.RequireOnly(
            "manifest.output",
            "path",
            "format",
            "width",
            "height",
            "transparency",
            "target_display_sizes"
        );
        string outputPath = node.Scalar("path", "manifest.output");
        string assetRoot = PathPolicy.ResolveUnder(
            configuration.RepositoryRoot,
            configuration.Paths.GodotAssetRoot,
            "godot_asset_root",
            allowMissing: true
        );
        string resolvedOutput = PathPolicy.ResolveUnder(
            configuration.RepositoryRoot,
            outputPath,
            "manifest.output.path",
            allowMissing: true
        );
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

        global::AlterCourse.AssetCtl.Domain.DomainModels.OutputTransparency transparency =
            ConfigurationTypes.ConfigurationLoader.ParseOutputTransparency(
                node.Scalar("transparency", "manifest.output"),
                "manifest.output"
            );
        var output = new OutputContract(
            outputPath,
            format,
            node.Integer("width", "manifest.output"),
            node.Integer("height", "manifest.output"),
            transparency == global::AlterCourse.AssetCtl.Domain.DomainModels.OutputTransparency.Required,
            YamlValues
                .Strings(
                    node.Sequence("target_display_sizes", "manifest.output"),
                    "manifest.output.target_display_sizes"
                )
                .Select(value => ParseInteger(value, "manifest.output.target_display_sizes"))
                .ToArray()
        );
        OutputContractPolicy.Validate(output, configuration.Limits.MaximumDecodedPixels);
        return output;
    }

    public static string Serialize(AssetManifest manifest)
    {
        var builder = new StringBuilder();
        Line(builder, 0, "schema_version", "1");
        Line(builder, 0, "id", manifest.Request.Id);
        Line(builder, 0, "lifecycle", Lifecycle(manifest.Request.Lifecycle));
        Line(builder, 0, "quality_tier", manifest.Request.QualityTier);
        Line(builder, 0, "kind", manifest.Request.Kind);
        builder.AppendLine(CultureInfo.InvariantCulture, $"revision: {manifest.Revision}");
        Line(builder, 0, "purpose", manifest.Request.Purpose);
        builder.AppendLine("output:");
        Line(builder, 2, "path", manifest.Request.Output.Path);
        Line(builder, 2, "format", manifest.Request.Output.Format.ToString().ToLowerInvariant());
        builder.AppendLine(CultureInfo.InvariantCulture, $"  width: {manifest.Request.Output.Width}");
        builder.AppendLine(CultureInfo.InvariantCulture, $"  height: {manifest.Request.Output.Height}");
        Line(builder, 2, "transparency", manifest.Request.Output.TransparencyRequired ? "required" : "optional");
        Sequence(
            builder,
            2,
            "target_display_sizes",
            manifest.Request.Output.TargetDisplaySizes.Select(value => value.ToString(CultureInfo.InvariantCulture))
        );
        builder.AppendLine("visual:");
        Line(builder, 2, "style_profile", manifest.Request.StyleProfile);
        Line(builder, 2, "importance", manifest.Request.Importance);
        Sequence(builder, 2, "tags", manifest.Request.Tags);
        builder.AppendLine("constraints:");
        Sequence(builder, 2, "required", manifest.Request.Required);
        Sequence(builder, 2, "prohibited", manifest.Request.Prohibited);
        WriteReferences(builder, manifest.Request.References);
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
        NullableLine(
            builder,
            2,
            "approved_at",
            manifest.Approval.ApprovedAt?.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
        );
        NullableLine(builder, 2, "approval_note", manifest.Approval.ApprovalNote);
        WriteDeprecation(builder, manifest.Deprecation);
        NullableLine(builder, 0, "supersedes", manifest.Supersedes);
        return builder.ToString();
    }

    private static void WriteDeprecation(StringBuilder builder, DeprecationRecord? deprecation)
    {
        if (deprecation is null)
        {
            builder.AppendLine("deprecation: null");
            return;
        }

        builder.AppendLine("deprecation:");
        Line(builder, 2, "actor", deprecation.Actor);
        Line(
            builder,
            2,
            "deprecated_at",
            deprecation.DeprecatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
        );
        Line(builder, 2, "reason", deprecation.Reason);
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
        Line(
            builder,
            2,
            "generated_at",
            generation.GeneratedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
        );
        Line(builder, 2, "run_id", generation.RunId);
        Line(builder, 2, "route", generation.Route);
        Line(builder, 2, "provider", generation.Provider);
        Line(builder, 2, "adapter", generation.Adapter);
        Line(builder, 2, "adapter_version", generation.AdapterVersion);
        Line(builder, 2, "provenance_schema_version", generation.ProvenanceSchemaVersion);
        Line(builder, 2, "model_profile", generation.ModelProfile);
        Line(builder, 2, "model", generation.Model);
        Line(builder, 2, "quality_tier", generation.QualityTier);
        BlockScalar(builder, 2, "final_prompt", generation.FinalPrompt);
        Line(builder, 2, "prompt_sha256", generation.PromptSha256);
        Line(builder, 2, "request_sha256", generation.RequestSha256);
        Line(builder, 2, "effective_config_sha256", generation.EffectiveConfigSha256);
        NullableLine(builder, 2, "provider_request_id", generation.ProviderRequestId);
        string estimatedCost = generation.EstimatedCostUsd?.ToString(CultureInfo.InvariantCulture) ?? "null";
        builder.AppendLine(CultureInfo.InvariantCulture, $"  estimated_cost_usd: {estimatedCost}");
        string actualCost = generation.ActualCostUsd?.ToString(CultureInfo.InvariantCulture) ?? "null";
        builder.AppendLine(CultureInfo.InvariantCulture, $"  actual_cost_usd: {actualCost}");
    }

    private static void WriteReferences(StringBuilder builder, IReadOnlyList<AssetReference> references)
    {
        if (references.Count == 0)
        {
            builder.AppendLine("references: []");
            return;
        }

        builder.AppendLine("references:");
        foreach (AssetReference reference in references)
        {
            builder.AppendLine("  -");
            Line(builder, 4, "path", reference.Path);
            Line(builder, 4, "sha256", reference.Sha256);
            Line(builder, 4, "rights_basis", reference.RightsBasis);
        }
    }

    private static void BlockScalar(StringBuilder builder, int indentation, string key, string value)
    {
        if (value.Contains('\r', StringComparison.Ordinal))
        {
            throw new AssetCtlException($"{key}: expected canonical LF line endings.", 2);
        }

        int trailingLineFeeds = value.Length - value.TrimEnd('\n').Length;
        string chomping = trailingLineFeeds switch
        {
            0 => "|-",
            1 => "|",
            _ => "|+",
        };
        builder.Append(' ', indentation).Append(key).Append(": ").AppendLine(chomping);
        string content = trailingLineFeeds == 0 ? value : value[..^trailingLineFeeds];
        foreach (string line in content.Split('\n'))
        {
            builder.Append(' ', indentation + 2).AppendLine(line);
        }

        for (int index = 1; index < trailingLineFeeds; index++)
        {
            builder.AppendLine();
        }
    }

    private static void WriteValidation(
        StringBuilder builder,
        MechanicalValidationResult? mechanical,
        SemanticReviewResult? semantic
    )
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
        Line(
            builder,
            2,
            "semantic_status",
            semantic is null ? "not-run"
                : semantic.HasHardFailure ? "fail"
                : "pass"
        );
        if (semantic is not null)
        {
            Line(builder, 2, "semantic_rubric_version", semantic.SemanticRubricVersion);
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"  semantic_matches_subject: {BooleanValue(semantic.MatchesSubject)}"
            );
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"  semantic_required_constraints_satisfied: {BooleanValue(semantic.RequiredConstraintsSatisfied)}"
            );
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"  semantic_prohibited_content_absent: {BooleanValue(semantic.ProhibitedContentAbsent)}"
            );
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"  semantic_readable_at_target_sizes: {BooleanValue(semantic.ReadableAtTargetSizes)}"
            );
            builder.AppendLine(CultureInfo.InvariantCulture, $"  semantic_style_adherence: {semantic.StyleAdherence}");
            builder.AppendLine(CultureInfo.InvariantCulture, $"  semantic_clarity: {semantic.SemanticClarity}");
            Sequence(builder, 2, "semantic_visual_defects", semantic.VisualDefects);
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"  semantic_unrequested_text_detected: {BooleanValue(semantic.UnrequestedTextDetected)}"
            );
            builder.AppendLine(
                CultureInfo.InvariantCulture,
                $"  semantic_logo_or_watermark_detected: {BooleanValue(semantic.LogoOrWatermarkDetected)}"
            );
            builder.AppendLine(CultureInfo.InvariantCulture, $"  semantic_score: {semantic.OverallScore}");
            Line(builder, 2, "semantic_decision", semantic.Decision);
            Line(builder, 2, "semantic_independence", semantic.Independence);
            NullableLine(builder, 2, "semantic_evidence_sha256", semantic.EvidenceSha256);
            NullableLine(builder, 2, "semantic_reviewer_provider", semantic.ReviewerProvider);
            NullableLine(builder, 2, "semantic_reviewer_model_profile", semantic.ReviewerModelProfile);
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

        string path = PathPolicy.ResolveUnder(
            configuration.RepositoryRoot,
            manifest.Request.Output.Path,
            "asset output",
            allowMissing: false
        );
        byte[] bytes = File.ReadAllBytes(path);
        string hash = Convert.ToHexStringLower(SHA256.HashData(bytes));
        if (
            !string.Equals(hash, manifest.Integrity.Sha256, StringComparison.Ordinal)
            || bytes.LongLength != manifest.Integrity.ByteLength
        )
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
            result.Add(
                new AssetReference(
                    item.Scalar("path", "manifest.references"),
                    item.Scalar("sha256", "manifest.references"),
                    item.Scalar("rights_basis", "manifest.references")
                )
            );
        }

        return result;
    }

    private static RightsRecord ReadRights(
        YamlMappingNode node,
        AssetLifecycle lifecycle,
        IReadOnlyList<AssetReference> references
    )
    {
        node.RequireOnly("manifest.rights", "classification", "license", "attribution", "source", "notes");
        string classification = node.Scalar("classification", "manifest.rights");
        string[] allowed = new[]
        {
            "original-project-created",
            "original-provider-generated",
            "third-party-licensed",
            "third-party-fan-project-reference",
            "unreviewed-generated-placeholder",
            "unknown",
        };
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

        return new RightsRecord(
            classification,
            node.OptionalScalar("license", "manifest.rights"),
            node.OptionalScalar("attribution", "manifest.rights"),
            node.OptionalScalar("source", "manifest.rights"),
            node.OptionalScalar("notes", "manifest.rights")
        );
    }

    private static GenerationProvenance? ReadGeneration(YamlMappingNode? node, string defaultTier)
    {
        if (node is null)
        {
            return null;
        }

        ValidateGenerationKeys(node);
        string adapterVersion = node.Scalar("adapter_version", "generation");
        string provenanceVersion = node.Scalar("provenance_schema_version", "generation");
        RequireContractVersion(adapterVersion, AssetContractVersions.Adapter, "generation.adapter_version");
        RequireContractVersion(
            provenanceVersion,
            AssetContractVersions.Provenance,
            "generation.provenance_schema_version"
        );
        if (!string.Equals(node.Scalar("source_type", "generation"), "generated", StringComparison.Ordinal))
        {
            throw new AssetCtlException("generation.source_type: unsupported source type.", 2);
        }

        return new GenerationProvenance(
            ParseTimestamp(node.Scalar("generated_at", "generation"), "manifest.generation.generated_at"),
            node.Scalar("run_id", "generation"),
            node.Scalar("route", "generation"),
            node.Scalar("provider", "generation"),
            node.Scalar("adapter", "generation"),
            node.Scalar("model_profile", "generation"),
            node.Scalar("model", "generation"),
            node.OptionalScalar("quality_tier", "generation") ?? defaultTier,
            node.Scalar("final_prompt", "generation"),
            node.Scalar("prompt_sha256", "generation"),
            node.Scalar("request_sha256", "generation"),
            node.Scalar("effective_config_sha256", "generation"),
            node.OptionalScalar("provider_request_id", "generation"),
            ParseNullableDecimal(
                node.OptionalScalar("estimated_cost_usd", "generation"),
                "manifest.generation.estimated_cost_usd"
            ),
            ParseNullableDecimal(
                node.OptionalScalar("actual_cost_usd", "generation"),
                "manifest.generation.actual_cost_usd"
            ),
            adapterVersion,
            provenanceVersion
        );
    }

    private static void ValidateGenerationKeys(YamlMappingNode node) =>
        node.RequireOnly(
            "manifest.generation",
            "source_type",
            "generated_at",
            "run_id",
            "route",
            "provider",
            "adapter",
            "adapter_version",
            "provenance_schema_version",
            "model_profile",
            "model",
            "quality_tier",
            "final_prompt",
            "prompt_sha256",
            "request_sha256",
            "effective_config_sha256",
            "provider_request_id",
            "estimated_cost_usd",
            "actual_cost_usd"
        );

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

        return new IntegrityRecord(
            sha.ToLowerInvariant(),
            node.Long("byte_length", "integrity"),
            node.Scalar("media_type", "integrity")
        );
    }

    private static (MechanicalValidationResult?, SemanticReviewResult?) ReadValidation(YamlMappingNode? node)
    {
        if (node is null)
        {
            return (null, null);
        }

        node.RequireOnly(
            "manifest.validation",
            "mechanical_status",
            "mechanical_validator_version",
            "mechanical_findings",
            "semantic_status",
            "semantic_rubric_version",
            "semantic_matches_subject",
            "semantic_required_constraints_satisfied",
            "semantic_prohibited_content_absent",
            "semantic_readable_at_target_sizes",
            "semantic_style_adherence",
            "semantic_clarity",
            "semantic_visual_defects",
            "semantic_unrequested_text_detected",
            "semantic_logo_or_watermark_detected",
            "semantic_score",
            "semantic_decision",
            "semantic_independence",
            "semantic_evidence_sha256",
            "semantic_reviewer_provider",
            "semantic_reviewer_model_profile"
        );
        bool passed = string.Equals(
            node.Scalar("mechanical_status", "manifest.validation"),
            "pass",
            StringComparison.Ordinal
        );
        var mechanical = new MechanicalValidationResult(
            passed,
            string.Empty,
            0,
            0,
            false,
            YamlValues.Strings(
                node.OptionalSequence("mechanical_findings", "manifest.validation"),
                "manifest.validation.mechanical_findings"
            ),
            [],
            new Dictionary<int, byte[]>()
        );
        string semanticStatus = node.Scalar("semantic_status", "manifest.validation");
        if (string.Equals(semanticStatus, "not-run", StringComparison.Ordinal))
        {
            return (mechanical, null);
        }

        return (mechanical, ReadSemantic(node, semanticStatus));
    }

    private static SemanticReviewResult ReadSemantic(YamlMappingNode node, string semanticStatus)
    {
        string rubricVersion = node.Scalar("semantic_rubric_version", "manifest.validation");
        RequireContractVersion(
            rubricVersion,
            AssetContractVersions.SemanticRubric,
            "manifest.validation.semantic_rubric_version"
        );
        SemanticReviewResult semantic = new(
            ParseBoolean(node, "semantic_matches_subject"),
            ParseBoolean(node, "semantic_required_constraints_satisfied"),
            ParseBoolean(node, "semantic_prohibited_content_absent"),
            ParseBoolean(node, "semantic_readable_at_target_sizes"),
            ParseScore(node, "semantic_style_adherence"),
            ParseScore(node, "semantic_clarity"),
            YamlValues.Strings(
                node.Sequence("semantic_visual_defects", "manifest.validation"),
                "manifest.validation.semantic_visual_defects"
            ),
            ParseBoolean(node, "semantic_unrequested_text_detected"),
            ParseBoolean(node, "semantic_logo_or_watermark_detected"),
            ParseScore(node, "semantic_score"),
            ParseDecision(node.Scalar("semantic_decision", "manifest.validation")),
            node.Scalar("semantic_independence", "manifest.validation"),
            node.OptionalScalar("semantic_evidence_sha256", "manifest.validation"),
            node.OptionalScalar("semantic_reviewer_provider", "manifest.validation"),
            node.OptionalScalar("semantic_reviewer_model_profile", "manifest.validation"),
            rubricVersion
        );
        bool statusPassed = semanticStatus switch
        {
            "pass" => true,
            "fail" => false,
            _ => throw new AssetCtlException("manifest.validation.semantic_status: unsupported status.", 2),
        };
        if (statusPassed == semantic.HasHardFailure)
        {
            throw new AssetCtlException(
                "manifest.validation.semantic_status does not match the structured semantic result.",
                2
            );
        }

        return semantic;
    }

    private static bool ParseBoolean(YamlMappingNode node, string key) =>
        node.Scalar(key, "manifest.validation") switch
        {
            "true" => true,
            "false" => false,
            _ => throw new AssetCtlException($"manifest.validation.{key}: expected true or false.", 2),
        };

    private static double ParseScore(YamlMappingNode node, string key)
    {
        if (
            !double.TryParse(
                node.Scalar(key, "manifest.validation"),
                NumberStyles.Float,
                CultureInfo.InvariantCulture,
                out double value
            )
            || !double.IsFinite(value)
            || value is < 0 or > 1
        )
        {
            throw new AssetCtlException($"manifest.validation.{key}: expected a finite value from 0 to 1.", 2);
        }

        return value;
    }

    private static string ParseDecision(string value) =>
        value is "pass" or "fail"
            ? value
            : throw new AssetCtlException("manifest.validation.semantic_decision: unsupported decision.", 2);

    private static void RequireContractVersion(string actual, string expected, string path)
    {
        if (!string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new AssetCtlException($"{path}: unsupported version '{actual}'.", 2);
        }
    }

    private static ApprovalRecord ReadApproval(YamlMappingNode? node)
    {
        if (node is null)
        {
            return new ApprovalRecord(null, null, null);
        }

        node.RequireOnly("manifest.approval", "approved_by", "approved_at", "approval_note");
        string? date = node.OptionalScalar("approved_at", "approval");
        return new ApprovalRecord(
            node.OptionalScalar("approved_by", "approval"),
            date is null ? null : ParseTimestamp(date, "manifest.approval.approved_at"),
            node.OptionalScalar("approval_note", "approval")
        );
    }

    private static DeprecationRecord? ReadDeprecation(YamlMappingNode? node)
    {
        if (node is null)
        {
            return null;
        }

        node.RequireOnly("manifest.deprecation", "actor", "deprecated_at", "reason");
        string actor = node.Scalar("actor", "manifest.deprecation");
        string reason = node.Scalar("reason", "manifest.deprecation");
        if (string.IsNullOrWhiteSpace(actor) || string.IsNullOrWhiteSpace(reason))
        {
            throw new AssetCtlException("manifest.deprecation: actor and reason cannot be blank.", 2);
        }

        return new DeprecationRecord(
            actor,
            ParseTimestamp(node.Scalar("deprecated_at", "manifest.deprecation"), "manifest.deprecation.deprecated_at"),
            reason
        );
    }

    private static void ValidateLifecycle(
        AssetLifecycle lifecycle,
        RightsRecord rights,
        ApprovalRecord approval,
        IntegrityRecord? integrity,
        DeprecationRecord? deprecation
    )
    {
        if (lifecycle == AssetLifecycle.Approved)
        {
            if (
                rights.Classification is "unknown" or "unreviewed-generated-placeholder"
                || string.IsNullOrWhiteSpace(rights.Notes) && string.IsNullOrWhiteSpace(rights.License)
                || approval.ApprovedBy is null
                || approval.ApprovedAt is null
                || approval.ApprovalNote is null
                || integrity is null
            )
            {
                throw new AssetCtlException("approved asset lacks rights, approval, or integrity evidence.", 1);
            }
        }

        if (lifecycle == AssetLifecycle.Deprecated && deprecation is null)
        {
            throw new AssetCtlException("deprecated asset lacks structured deprecation evidence.", 1);
        }
        if (lifecycle != AssetLifecycle.Deprecated && deprecation is not null)
        {
            throw new AssetCtlException("manifest.deprecation is only valid for deprecated assets.", 2);
        }
    }

    private static AssetLifecycle ParseLifecycle(string value) =>
        value switch
        {
            "placeholder" => AssetLifecycle.Placeholder,
            "candidate" => AssetLifecycle.Candidate,
            "approved" => AssetLifecycle.Approved,
            "deprecated" => AssetLifecycle.Deprecated,
            _ => throw new AssetCtlException($"manifest.lifecycle: unknown '{value}'.", 2),
        };

    private static string Lifecycle(AssetLifecycle value) => value.ToString().ToLowerInvariant();

    private static int ParseInteger(string value, string path) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            ? parsed
            : throw new AssetCtlException($"{path}: expected an integer.", 2);

    private static decimal ParseDecimal(string value, string path) =>
        decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)
            ? parsed
            : throw new AssetCtlException($"{path}: expected a decimal number.", 2);

    private static decimal? ParseNullableDecimal(string? value, string path) =>
        value is null ? null : ParseDecimal(value, path);

    private static DateTimeOffset ParseTimestamp(string value, string path) =>
        DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AssumeUniversal,
            out DateTimeOffset parsed
        )
            ? parsed
            : throw new AssetCtlException($"{path}: expected an ISO 8601 timestamp.", 2);

    private static string BooleanValue(bool value) => value ? "true" : "false";

    private static void Line(StringBuilder builder, int indentation, string key, string value)
    {
        if (value.Contains('\r', StringComparison.Ordinal))
        {
            throw new AssetCtlException($"{key}: expected canonical LF line endings.", 2);
        }
        if (value.Contains('\n', StringComparison.Ordinal))
        {
            BlockScalar(builder, indentation, key, value);
            return;
        }

        builder
            .Append(' ', indentation)
            .Append(key)
            .Append(": '")
            .Append(value.Replace("'", "''", StringComparison.Ordinal))
            .AppendLine("'");
    }

    private static void NullableLine(StringBuilder builder, int indentation, string key, string? value)
    {
        if (value is null)
            builder.Append(' ', indentation).Append(key).AppendLine(": null");
        else
            Line(builder, indentation, key, value);
    }

    private static void Sequence(StringBuilder builder, int indentation, string key, IEnumerable<string> values)
    {
        string[] array = values.ToArray();
        if (array.Length == 0)
        {
            builder.Append(' ', indentation).Append(key).AppendLine(": []");
            return;
        }
        builder.Append(' ', indentation).Append(key).AppendLine(":");
        foreach (string value in array)
        {
            if (value.Contains('\n', StringComparison.Ordinal))
            {
                BlockSequenceScalar(builder, indentation + 2, value);
            }
            else
            {
                builder
                    .Append(' ', indentation + 2)
                    .Append("- '")
                    .Append(value.Replace("'", "''", StringComparison.Ordinal))
                    .AppendLine("'");
            }
        }
    }

    private static void BlockSequenceScalar(StringBuilder builder, int indentation, string value)
    {
        if (value.Contains('\r', StringComparison.Ordinal))
        {
            throw new AssetCtlException("sequence item: expected canonical LF line endings.", 2);
        }

        int trailingLineFeeds = value.Length - value.TrimEnd('\n').Length;
        string chomping = trailingLineFeeds switch
        {
            0 => "|-",
            1 => "|",
            _ => "|+",
        };
        builder.Append(' ', indentation).Append("- ").AppendLine(chomping);
        string content = trailingLineFeeds == 0 ? value : value[..^trailingLineFeeds];
        foreach (string line in content.Split('\n'))
        {
            builder.Append(' ', indentation + 2).AppendLine(line);
        }
        for (int index = 1; index < trailingLineFeeds; index++)
        {
            builder.AppendLine();
        }
    }
}
