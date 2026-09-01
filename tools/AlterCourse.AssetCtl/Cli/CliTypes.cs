using System.Security.Cryptography;
using System.Text.Json;
using AlterCourse.AssetCtl.Catalog;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Routing;
using AlterCourse.AssetCtl.Validation;
using Microsoft.Extensions.Logging;

namespace AlterCourse.AssetCtl.Cli;

internal static class CliTypes
{
private static readonly Action<ILogger, string, Exception?> LogCommand = LoggerMessage.Define<string>(
    LogLevel.Information,
    new EventId(1, "AssetCtlCommand"),
    "Running AssetCtl command {Command}"
);

public sealed class CommandApp(
    ConfigurationLoader configurationLoader,
    AssetRouter router,
    GenerationOrchestrator generation,
    ILogger<CommandApp> logger
)
{
    public async Task<int> RunAsync(string[] arguments, CancellationToken cancellationToken)
    {
        if (arguments.Length == 0 || arguments[0] is "help" or "--help" or "-h")
        {
            Console.Out.WriteLine(Usage);
            return arguments.Length == 0 ? 2 : 0;
        }

        var options = CliOptions.Parse(arguments[1..]);
        string output = options.Value("output") ?? "human";
        if (output is not ("human" or "json"))
        {
            throw new AssetCtlException("--output must be human or json.", 2);
        }

        string repository = RepositoryLocator.Find(Environment.CurrentDirectory);
        global::AlterCourse.AssetCtl.Domain.DomainModels.EffectiveConfiguration configuration = configurationLoader.Load(repository);
        LogCommand(logger, arguments[0], null);
        object result = arguments[0] switch
        {
            "validate-config" => ValidateConfig(configuration),
            "doctor" => Doctor(configuration, options),
            "find" => Find(configuration, options),
            "status" => Status(configuration),
            "plan" => Plan(configuration, ResolveManifest(configuration, options)),
            "generate" => await generation.GenerateAsync(configuration, ResolveManifest(configuration, options), options.Flag("force"), options.Flag("dry-run"), options.Flag("offline"), cancellationToken).ConfigureAwait(false),
            "verify" => Verify(configuration, options),
            "approve" => Approve(configuration, options),
            "deprecate" => Deprecate(configuration, options),
            _ => throw new AssetCtlException($"Unknown command '{arguments[0]}'.", 2),
        };
        if (string.Equals(output, "json", StringComparison.Ordinal))
        {
            Console.Out.WriteLine(JsonSerializer.Serialize(result, JsonOptions.Stable));
        }
        else
        {
            Console.Out.WriteLine(Human(arguments[0], result));
        }

        return 0;
    }

    private static object ValidateConfig(EffectiveConfiguration configuration)
    {
        global::System.Collections.Generic.IReadOnlyList<global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest> catalog = ManifestStore.LoadAll(configuration);
        foreach (global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest manifest in catalog)
        {
            if (manifest.Integrity is null)
            {
                throw new AssetCtlException($"{manifest.Request.Id}: selected asset requires integrity.", 1);
            }

            ManifestStore.VerifyIntegrity(configuration, manifest);
            byte[] bytes = File.ReadAllBytes(Path.Combine(configuration.RepositoryRoot, manifest.Request.Output.Path));
            global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult validation = MechanicalValidator.Validate(manifest.Request, bytes, configuration.Limits.MaximumDownloadBytes, configuration.Limits.MaximumDecodedPixels);
            if (!validation.Passed)
            {
                throw new AssetCtlException($"{manifest.Request.Id}: mechanical validation failed: {string.Join("; ", validation.Findings)}", 1);
            }
        }

        return new { valid = true, configuration_hash = configuration.EffectiveHash, manifests = catalog.Count, offline = true, read_only = true };
    }

    private static object Doctor(EffectiveConfiguration configuration, CliOptions options)
    {
        if (options.Flag("probe"))
        {
            throw new AssetCtlException("Live provider probes are not implemented; this command never spends silently.", 2);
        }

        return new
        {
            healthy = true,
            configuration_files = configuration.FileHashes,
            configuration_hash = configuration.EffectiveHash,
            providers = configuration.Providers.Values.OrderBy(provider => provider.Id, StringComparer.Ordinal).Select(provider => new
            {
                id = provider.Id,
                adapter = provider.AdapterId,
                provider.Enabled,
                credential = provider.CredentialEnvironmentVariable is null ? null : new { variable = provider.CredentialEnvironmentVariable, present = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(provider.CredentialEnvironmentVariable)) },
                models = provider.Models.Keys.Order(StringComparer.Ordinal),
            }),
            decoder = "SkiaSharp",
            renderer = "Svg.Skia",
            probe = "not-run",
            may_spend = false,
        };
    }

    private static object Find(EffectiveConfiguration configuration, CliOptions options)
    {
        string? query = options.Value("query");
        string? id = options.Value("id");
        string? kind = options.Value("kind");
        string? lifecycle = options.Value("lifecycle");
        string? tag = options.Value("tag");
        string? style = options.Value("style-profile");
        object[] values = ManifestStore.LoadAll(configuration).Where(manifest => id is null || string.Equals(manifest.Request.Id, id, StringComparison.Ordinal)).Where(manifest => kind is null || string.Equals(manifest.Request.Kind, kind, StringComparison.Ordinal)).Where(manifest => lifecycle is null || string.Equals(manifest.Request.Lifecycle.ToString(), lifecycle, StringComparison.OrdinalIgnoreCase)).Where(manifest => tag is null || manifest.Request.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)).Where(manifest => style is null || string.Equals(manifest.Request.StyleProfile, style, StringComparison.Ordinal)).Where(manifest => query is null || manifest.Request.Id.Contains(query, StringComparison.OrdinalIgnoreCase) || manifest.Request.Purpose.Contains(query, StringComparison.OrdinalIgnoreCase)).OrderBy(manifest => manifest.Request.Id, StringComparer.Ordinal).Select(manifest => (object)new { asset_id = manifest.Request.Id, kind = manifest.Request.Kind, lifecycle = manifest.Request.Lifecycle.ToString().ToLowerInvariant(), repository_path = manifest.Request.Output.Path, godot_path = ToGodotPath(manifest.Request.Output.Path), purpose = manifest.Request.Purpose }).ToArray();
        return new { count = values.Length, assets = values };
    }

    private static object Status(EffectiveConfiguration configuration)
    {
        global::System.Collections.Generic.IReadOnlyList<global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest> catalog = ManifestStore.LoadAll(configuration);
        int missing = catalog.Count(manifest => !File.Exists(Path.Combine(configuration.RepositoryRoot, manifest.Request.Output.Path)));
        int mismatched = 0;
        foreach (global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest manifest in catalog.Where(manifest => manifest.Integrity is not null && File.Exists(Path.Combine(configuration.RepositoryRoot, manifest.Request.Output.Path))))
        {
            byte[] bytes = File.ReadAllBytes(Path.Combine(configuration.RepositoryRoot, manifest.Request.Output.Path));
            if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), manifest.Integrity!.Sha256, StringComparison.Ordinal))
            {
                mismatched++;
            }
        }

        return new { total = catalog.Count, placeholders = catalog.Count(manifest => manifest.Request.Lifecycle == AssetLifecycle.Placeholder), candidates = catalog.Count(manifest => manifest.Request.Lifecycle == AssetLifecycle.Candidate), approved = catalog.Count(manifest => manifest.Request.Lifecycle == AssetLifecycle.Approved), deprecated = catalog.Count(manifest => manifest.Request.Lifecycle == AssetLifecycle.Deprecated), missing_files = missing, integrity_mismatches = mismatched };
    }

    private object Plan(EffectiveConfiguration configuration, AssetManifest manifest)
    {
        global::AlterCourse.AssetCtl.Domain.DomainModels.GenerationPlan plan = router.Plan(configuration, manifest.Request);
        return new { asset_id = manifest.Request.Id, required_capabilities = plan.RequiredCapabilities.Select(Capability), matching_targets = plan.Targets, selected_target = plan.SelectedTarget, reviewer = plan.Reviewer, candidate_count = plan.CandidateCount, attempts_per_route = plan.AttemptsPerRoute, estimated_maximum_cost_usd = plan.EstimatedMaximumCost, local_fallback = plan.UsesLocalFallback };
    }

    private static object Verify(EffectiveConfiguration configuration, CliOptions options)
    {
        string? target = options.Value("asset-id");
        global::System.Collections.Generic.IReadOnlyList<global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest> selected = target is null ? ManifestStore.LoadAll(configuration) : [ResolveManifest(configuration, options)];
        List<object> results = [];
        foreach (global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest manifest in selected)
        {
            ManifestStore.VerifyIntegrity(configuration, manifest);
            byte[] bytes = File.ReadAllBytes(Path.Combine(configuration.RepositoryRoot, manifest.Request.Output.Path));
            global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult mechanical = MechanicalValidator.Validate(manifest.Request, bytes, configuration.Limits.MaximumDownloadBytes, configuration.Limits.MaximumDecodedPixels);
            if (!mechanical.Passed)
            {
                throw new AssetCtlException($"{manifest.Request.Id}: {string.Join("; ", mechanical.Findings)}", 1);
            }

            results.Add(new { asset_id = manifest.Request.Id, mechanical = "pass", sha256 = manifest.Integrity!.Sha256 });
        }

        return new { valid = true, assets = results };
    }

    private static object Approve(EffectiveConfiguration configuration, CliOptions options)
    {
        global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest manifest = ResolveManifest(configuration, options);
        if (manifest.Request.Lifecycle == AssetLifecycle.Approved)
        {
            return new { asset_id = manifest.Request.Id, lifecycle = "approved", unchanged = true };
        }

        if (manifest.Request.Lifecycle != AssetLifecycle.Candidate)
        {
            throw new AssetCtlException("Only a candidate can be approved.", 8);
        }

        string actor = options.Required("approved-by");
        string note = options.Required("approval-note");
        if (!string.Equals(options.Required("confirm-approved-asset"), manifest.Request.Id, StringComparison.Ordinal))
        {
            throw new AssetCtlException("--confirm-approved-asset must exactly equal the asset ID.", 8);
        }

        ManifestStore.VerifyIntegrity(configuration, manifest);
        if (manifest.MechanicalValidation?.Passed != true || string.Equals(configuration.QualityTiers[manifest.Request.QualityTier].SemanticReview, "required", StringComparison.Ordinal) && (manifest.SemanticReview is null || manifest.SemanticReview.HasHardFailure) || manifest.Rights.Classification is "unknown" or "unreviewed-generated-placeholder" || string.IsNullOrWhiteSpace(manifest.Rights.License) && string.IsNullOrWhiteSpace(manifest.Rights.Notes))
        {
            throw new AssetCtlException("Approval requires passing validation and complete non-placeholder rights data.", 8);
        }

        string assetPath = Path.Combine(configuration.RepositoryRoot, manifest.Request.Output.Path);
        string beforeHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assetPath)));
        global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest approved = manifest with { Request = manifest.Request with { Lifecycle = AssetLifecycle.Approved }, Approval = new ApprovalRecord(actor, DateTimeOffset.UtcNow, note) };
        if (options.Flag("dry-run"))
        {
            return new { dry_run = true, asset_id = approved.Request.Id, lifecycle = "approved", sha256 = beforeHash };
        }

        WriteManifestAtomic(configuration, approved);
        string afterHash = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(assetPath)));
        if (!string.Equals(beforeHash, afterHash, StringComparison.Ordinal))
        {
            throw new AssetCtlException("Approval changed asset bytes; manifest update was refused.", 7);
        }

        return new { asset_id = approved.Request.Id, lifecycle = "approved", sha256 = afterHash };
    }

    private static object Deprecate(EffectiveConfiguration configuration, CliOptions options)
    {
        global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest manifest = ResolveManifest(configuration, options);
        string actor = options.Required("actor");
        string reason = options.Required("reason");
        if (manifest.Request.Lifecycle == AssetLifecycle.Approved && !string.Equals(options.Required("confirm-approved-asset"), manifest.Request.Id, StringComparison.Ordinal))
        {
            throw new AssetCtlException("Deprecating an approved asset requires exact --confirm-approved-asset.", 8);
        }

        global::AlterCourse.AssetCtl.Domain.DomainModels.AssetManifest deprecated = manifest with { Request = manifest.Request with { Lifecycle = AssetLifecycle.Deprecated }, Approval = manifest.Approval with { ApprovalNote = $"{manifest.Approval.ApprovalNote} Deprecated by {actor}: {reason}".Trim() } };
        if (options.Flag("dry-run"))
        {
            return new { dry_run = true, asset_id = deprecated.Request.Id, lifecycle = "deprecated", actor, reason };
        }

        WriteManifestAtomic(configuration, deprecated);
        return new { asset_id = deprecated.Request.Id, lifecycle = "deprecated", actor, reason };
    }

    private static void WriteManifestAtomic(EffectiveConfiguration configuration, AssetManifest manifest)
    {
        string path = PathPolicy.ResolveUnder(configuration.RepositoryRoot, manifest.ManifestPath, "manifest", allowMissing: false);
        string stage = path + ".assetctl-stage-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(stage, ManifestStore.Serialize(manifest), new System.Text.UTF8Encoding(false));
        File.Move(stage, path, overwrite: true);
    }

    private static AssetManifest ResolveManifest(EffectiveConfiguration configuration, CliOptions options)
    {
        string? manifestPath = options.Value("manifest");
        if (manifestPath is not null)
        {
            return ManifestStore.Load(configuration, manifestPath);
        }

        string id = options.Required("asset-id");
        return ManifestStore.LoadAll(configuration).SingleOrDefault(manifest => string.Equals(manifest.Request.Id, id, StringComparison.Ordinal)) ?? throw new AssetCtlException($"Asset '{id}' was not found.", 1);
    }

    private static string Human(string command, object result) => $"{command}: {JsonSerializer.Serialize(result, JsonOptions.Stable)}";
    private static string ToGodotPath(string repositoryPath) => "res://" + repositoryPath["src/AlterCourse.Godot/".Length..];
    private static string Capability(AssetCapability capability) => capability switch { AssetCapability.RasterGenerate => "raster.generate", AssetCapability.VectorGenerate => "vector.generate", AssetCapability.ImageEdit => "image.edit", AssetCapability.ImageReferenceInput => "image.reference-input", AssetCapability.ImageTransparentOutput => "image.transparent-output", AssetCapability.ImageBackgroundRemove => "image.background-remove", AssetCapability.ImageVectorize => "image.vectorize", AssetCapability.ReviewSemantic => "review.semantic", AssetCapability.ReviewReferenceComparison => "review.reference-comparison", _ => throw new ArgumentOutOfRangeException(nameof(capability)) };

    public const string Usage = """
        Usage: assetctl <command> [options]
        Commands: validate-config, doctor, find, status, plan, generate, verify, approve, deprecate
        Common: --output human|json; --offline; --dry-run
        Asset selection: --asset-id ID or --manifest PATH
        """;
}

public sealed class CliOptions
{
    private readonly Dictionary<string, string?> values;
    private CliOptions(Dictionary<string, string?> values) => this.values = values;

    public static CliOptions Parse(string[] arguments)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        for (int index = 0; index < arguments.Length; index++)
        {
            string value = arguments[index];
            if (!value.StartsWith("--", StringComparison.Ordinal) || value.Length == 2)
            {
                throw new AssetCtlException($"Unexpected argument '{value}'.", 2);
            }

            string key = value[2..];
            string? optionValue = null;
            if (index + 1 < arguments.Length && !arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                optionValue = arguments[++index];
            }

            if (!result.TryAdd(key, optionValue))
            {
                throw new AssetCtlException($"Duplicate option '--{key}'.", 2);
            }
        }

        return new CliOptions(result);
    }

    public string? Value(string key) => values.TryGetValue(key, out string? value) ? value ?? throw new AssetCtlException($"--{key} requires a value.", 2) : null;
    public bool Flag(string key) => values.TryGetValue(key, out string? value) ? value is null ? true : throw new AssetCtlException($"--{key} does not take a value.", 2) : false;
    public string Required(string key) => Value(key) ?? throw new AssetCtlException($"--{key} is required.", 2);
}

public static class RepositoryLocator
{
    public static string Find(string start)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(start));
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AlterCourse.sln")) && Directory.Exists(Path.Combine(directory.FullName, "config", "assets")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new AssetCtlException("No AlterCourse repository root found from current directory.", 2);
    }
}
}
