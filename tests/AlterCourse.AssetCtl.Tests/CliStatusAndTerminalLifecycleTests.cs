using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies stable status output and terminal deprecation behavior.</summary>
public sealed class CliStatusAndTerminalLifecycleTests : IDisposable
{
    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "assetctl-cli-status-" + Guid.NewGuid().ToString("N")
    );

    /// <summary>Reports only candidates that do not satisfy their current semantic-review policy.</summary>
    [Fact]
    public void StatusReportsCandidatesAwaitingReviewInStableJsonAndHumanForms()
    {
        EffectiveConfiguration configuration = Configuration();
        WriteManifest(Manifest("awaiting", AssetLifecycle.Candidate, "required", null));
        WriteManifest(Manifest("reviewed", AssetLifecycle.Candidate, "required", PassingReview()));
        WriteManifest(Manifest("deprecated", AssetLifecycle.Deprecated, "production-candidate", null));
        WriteManifest(Manifest("placeholder", AssetLifecycle.Placeholder, "required", null));

        object status = Invoke("Status", configuration);
        string json = JsonSerializer.Serialize(status, JsonOptions.Stable);
        string human = (string)Invoke("Human", "status", status);

        Assert.Equal(
            "{\"total\":4,\"placeholders\":1,\"candidates\":1,\"approved\":0,\"deprecated\":1,\"missing_files\":4,\"integrity_mismatches\":0}",
            json
        );
        Assert.Equal("status: " + json, human);
    }

    /// <summary>Rejects deprecated-to-deprecated transition without changing revision or manifest bytes.</summary>
    [Fact]
    public void DeprecateRejectsAlreadyDeprecatedWithoutRewritingManifest()
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest deprecated = Manifest("terminal", AssetLifecycle.Deprecated, "disabled", null);
        WriteManifest(deprecated);
        string path = Path.Combine(root, deprecated.ManifestPath);
        string before = File.ReadAllText(path);

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() =>
            Invoke(
                "Deprecate",
                configuration,
                CliOptions.Parse(["--asset-id", deprecated.Request.Id, "--actor", "operator", "--reason", "retired"])
            )
        );

        AssetCtlException exception = Assert.IsType<AssetCtlException>(invocation.InnerException);
        Assert.Equal(8, exception.ExitCode);
        Assert.Equal("Deprecated assets cannot be deprecated again.", exception.Message);
        Assert.Equal(before, File.ReadAllText(path));
        Assert.Equal(deprecated.Revision, ManifestStore.Load(configuration, deprecated.ManifestPath).Revision);
    }

    /// <summary>Records deprecation provenance without rewriting pre-existing approval evidence.</summary>
    [Fact]
    public void DeprecatePreservesApprovalEvidenceAndWritesSeparateProvenance()
    {
        EffectiveConfiguration configuration = Configuration();
        var approval = new ApprovalRecord(null, null, "review note retained");
        AssetManifest candidate = Manifest("candidate", AssetLifecycle.Candidate, "development", null) with
        {
            Approval = approval,
        };
        WriteManifest(candidate);

        _ = Invoke(
            "Deprecate",
            configuration,
            CliOptions.Parse(["--asset-id", candidate.Request.Id, "--actor", "operator", "--reason", "superseded"])
        );

        AssetManifest deprecated = ManifestStore.Load(configuration, candidate.ManifestPath);
        Assert.Equal(approval, deprecated.Approval);
        Assert.Equal("operator", deprecated.Deprecation!.Actor);
        Assert.Equal("superseded", deprecated.Deprecation.Reason);
    }

    /// <summary>Rejects blank mutation provenance before changing the manifest bytes.</summary>
    [Theory]
    [InlineData("actor")]
    [InlineData("reason")]
    public void DeprecateRejectsBlankProvenanceWithoutWriting(string blankOption)
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest candidate = Manifest("blank", AssetLifecycle.Candidate, "development", null);
        WriteManifest(candidate);
        string path = Path.Combine(root, candidate.ManifestPath);
        string before = File.ReadAllText(path);
        string[] arguments =
        [
            "--asset-id",
            candidate.Request.Id,
            "--actor",
            string.Equals(blankOption, "actor", StringComparison.Ordinal) ? "   " : "operator",
            "--reason",
            string.Equals(blankOption, "reason", StringComparison.Ordinal) ? "   " : "retired",
        ];

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() =>
            Invoke("Deprecate", configuration, CliOptions.Parse(arguments))
        );

        AssetCtlException exception = Assert.IsType<AssetCtlException>(invocation.InnerException);
        Assert.Equal(2, exception.ExitCode);
        Assert.Equal(before, File.ReadAllText(path));
    }

    /// <summary>Rejects blank approval provenance before policy evaluation or manifest mutation.</summary>
    [Theory]
    [InlineData("approved-by")]
    [InlineData("approval-note")]
    public void ApproveRejectsBlankProvenanceWithoutWriting(string blankOption)
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest candidate = Manifest("blank-approval", AssetLifecycle.Candidate, "development", null);
        WriteManifest(candidate);
        string path = Path.Combine(root, candidate.ManifestPath);
        string before = File.ReadAllText(path);
        string[] arguments =
        [
            "--asset-id",
            candidate.Request.Id,
            "--approved-by",
            string.Equals(blankOption, "approved-by", StringComparison.Ordinal) ? "   " : "operator",
            "--approval-note",
            string.Equals(blankOption, "approval-note", StringComparison.Ordinal) ? "   " : "reviewed",
            "--confirm-approved-asset",
            candidate.Request.Id,
        ];

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() =>
            Invoke("Approve", configuration, CliOptions.Parse(arguments))
        );

        AssetCtlException exception = Assert.IsType<AssetCtlException>(invocation.InnerException);
        Assert.Equal(2, exception.ExitCode);
        Assert.Equal(before, File.ReadAllText(path));
    }

    /// <summary>Rejects ambiguous verification selectors instead of silently preferring one.</summary>
    [Fact]
    public void VerifyRejectsAssetIdAndManifestTogether()
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest manifest = CompleteManifest("ambiguous");
        WriteManifest(manifest);

        TargetInvocationException invocation = Assert.Throws<TargetInvocationException>(() =>
            Invoke(
                "Verify",
                configuration,
                CliOptions.Parse(["--asset-id", manifest.Request.Id, "--manifest", manifest.ManifestPath])
            )
        );

        AssetCtlException exception = Assert.IsType<AssetCtlException>(invocation.InnerException);
        Assert.Equal(2, exception.ExitCode);
        Assert.Contains("mutually exclusive", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Keeps the complete nine-command CLI contract in deterministic help order.</summary>
    [Fact]
    public void UsageListsAllNineCommandsInStableOrder()
    {
        Assert.Contains(
            "Commands: validate-config, doctor, find, status, plan, generate, verify, approve, deprecate",
            global::AlterCourse.AssetCtl.Cli.CliTypes.CommandApp.Usage,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "generate: --force --dry-run --offline",
            global::AlterCourse.AssetCtl.Cli.CliTypes.CommandApp.Usage,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "approve: --approved-by --approval-note --confirm-approved-asset --dry-run",
            global::AlterCourse.AssetCtl.Cli.CliTypes.CommandApp.Usage,
            StringComparison.Ordinal
        );
        Assert.Contains(
            "deprecate: --actor --reason --confirm-approved-asset --dry-run",
            global::AlterCourse.AssetCtl.Cli.CliTypes.CommandApp.Usage,
            StringComparison.Ordinal
        );
    }

    /// <summary>Verifies only the manifest selected by path and leaves unrelated catalog entries out of the result.</summary>
    [Fact]
    public void VerifyManifestSelectsExactlyOneCatalogEntry()
    {
        EffectiveConfiguration configuration = Configuration();
        AssetManifest selected = CompleteManifest("selected");
        AssetManifest unrelated = CompleteManifest("unrelated") with
        {
            Integrity = new IntegrityRecord(new string('0', 64), 1, "image/png"),
        };
        WriteManifest(selected);
        WriteManifest(unrelated);
        File.WriteAllBytes(
            Path.Combine(root, selected.Request.Output.Path),
            AlterCourse.AssetCtl.Generation.LocalPlaceholderGenerator.RenderPng(selected.Request)
        );
        File.WriteAllBytes(Path.Combine(root, unrelated.Request.Output.Path), [0]);

        object result = Invoke("Verify", configuration, CliOptions.Parse(["--manifest", selected.ManifestPath]));
        string json = JsonSerializer.Serialize(result, JsonOptions.Stable);

        Assert.Contains(selected.Request.Id, json, StringComparison.Ordinal);
        Assert.DoesNotContain(unrelated.Request.Id, json, StringComparison.Ordinal);
    }

    /// <summary>Removes the isolated catalog used by each CLI contract test.</summary>
    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private EffectiveConfiguration Configuration()
    {
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        Directory.CreateDirectory(Path.Combine(root, "catalog"));
        return new EffectiveConfiguration(
            root,
            new AssetCtlPaths("assets", "catalog", "styles", ".assetctl/work", "runs", ".assetctl/state", "logs"),
            new AssetCtlPolicy(false, true, true, true, false, "reject"),
            new AssetCtlLimits(1_000_000, 1_000_000, 10, 10, 10, 30, 1_000_000),
            new SpendingLimits(0, 0, 0),
            new Dictionary<string, ProviderInstance>(StringComparer.Ordinal),
            [],
            [],
            new Dictionary<string, QualityTier>(StringComparer.Ordinal)
            {
                ["development"] = new QualityTier("development", 1, 1, "disabled", true, 0),
                ["disabled"] = new QualityTier("disabled", 1, 1, "disabled", true, 0),
                ["production-candidate"] = new QualityTier("production-candidate", 1, 1, "required", false, 0.8),
                ["required"] = new QualityTier("required", 1, 1, "required", false, 0.8),
            },
            new Dictionary<string, StyleProfile>(StringComparer.Ordinal)
            {
                ["engineering-icons"] = new StyleProfile("engineering-icons", "test", [], []),
            },
            new Dictionary<string, string>(StringComparer.Ordinal),
            "hash"
        );
    }

    private static AssetManifest Manifest(
        string name,
        AssetLifecycle lifecycle,
        string tier,
        SemanticReviewResult? review
    ) =>
        new(
            "1",
            TestData.Request() with
            {
                Id = $"ui.test.{name}",
                Lifecycle = lifecycle,
                QualityTier = tier,
                Output = TestData.Request().Output with { Path = $"assets/{name}.png" },
            },
            3,
            new RightsRecord("original-project-created", "project", null, null, "test"),
            null,
            review is null
                ? null
                : new MechanicalValidationResult(
                    true,
                    "image/png",
                    64,
                    64,
                    true,
                    [],
                    [],
                    new Dictionary<int, byte[]>()
                ),
            review,
            null,
            new ApprovalRecord(null, null, null),
            null,
            $"catalog/ui.test.{name}.asset.yaml",
            lifecycle == AssetLifecycle.Deprecated
                ? new DeprecationRecord(
                    "operator",
                    DateTimeOffset.Parse("2026-09-01T00:00:00Z", CultureInfo.InvariantCulture),
                    "retired"
                )
                : null
        );

    private static SemanticReviewResult PassingReview() =>
        new(true, true, true, true, 0.9, 0.9, [], false, false, 0.9, "pass", "different-provider-family");

    private static AssetManifest CompleteManifest(string name)
    {
        AssetManifest manifest = Manifest(name, AssetLifecycle.Placeholder, "development", null);
        byte[] bytes = AlterCourse.AssetCtl.Generation.LocalPlaceholderGenerator.RenderPng(manifest.Request);
        return manifest with
        {
            Integrity = new IntegrityRecord(
                Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes)),
                bytes.LongLength,
                "image/png"
            ),
        };
    }

    private void WriteManifest(AssetManifest manifest) =>
        File.WriteAllText(Path.Combine(root, manifest.ManifestPath), ManifestStore.Serialize(manifest));

    private static object Invoke(string name, params object[] arguments)
    {
        MethodInfo method =
            typeof(global::AlterCourse.AssetCtl.Cli.CliTypes.CommandApp).GetMethod(
                name,
                BindingFlags.NonPublic | BindingFlags.Static
            ) ?? throw new InvalidOperationException($"Missing CLI method {name}.");
        return method.Invoke(null, arguments)
            ?? throw new InvalidOperationException($"CLI method {name} returned null.");
    }
}
