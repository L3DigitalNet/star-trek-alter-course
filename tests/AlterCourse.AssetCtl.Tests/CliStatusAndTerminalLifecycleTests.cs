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

    /// <summary>Keeps the complete nine-command CLI contract in deterministic help order.</summary>
    [Fact]
    public void UsageListsAllNineCommandsInStableOrder()
    {
        Assert.Contains(
            "Commands: validate-config, doctor, find, status, plan, generate, verify, approve, deprecate",
            global::AlterCourse.AssetCtl.Cli.CliTypes.CommandApp.Usage,
            StringComparison.Ordinal
        );
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
