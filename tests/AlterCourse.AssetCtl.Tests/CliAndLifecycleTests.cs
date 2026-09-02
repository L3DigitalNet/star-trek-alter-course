using System.Security.Cryptography;
using AlterCourse.AssetCtl.Cli;
using AlterCourse.AssetCtl.Review;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies CLI parsing, lifecycle review gates, and diagnostic redaction.</summary>
public sealed class CliAndLifecycleTests
{
    /// <summary>Keeps validate-config on stderr-only logging so an absent runtime-state root remains absent.</summary>
    [Fact]
    public void ValidateConfigLoggingCreatesNoRuntimeState()
    {
        string root = Path.Combine(Path.GetTempPath(), $"assetctl-validate-no-write-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            string? logRoot = AlterCourse.AssetCtl.Program.ResolveLogRoot(["validate-config"], root);
            using Microsoft.Extensions.Logging.ILoggerFactory logger = AlterCourse.AssetCtl.Program.CreateLoggerFactory(
                root,
                logRoot
            );

            Assert.Null(logRoot);
            Assert.False(Directory.Exists(Path.Combine(root, ".assetctl")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    /// <summary>Provides one complete accepted option set for each public command.</summary>
    public static TheoryData<string, string[]> CommandOptions =>
        new()
        {
            { "validate-config", ["--output", "json", "--offline"] },
            { "doctor", ["--output", "json", "--probe"] },
            {
                "find",
                [
                    "--output",
                    "json",
                    "--query",
                    "icon",
                    "--id",
                    "asset",
                    "--kind",
                    "icon",
                    "--lifecycle",
                    "candidate",
                    "--tag",
                    "ui",
                    "--style-profile",
                    "style",
                ]
            },
            { "status", ["--output", "json"] },
            { "plan", ["--output", "json", "--asset-id", "asset"] },
            { "generate", ["--output", "json", "--asset-id", "asset", "--force", "--dry-run", "--offline"] },
            { "verify", ["--output", "json", "--asset-id", "asset"] },
            {
                "approve",
                [
                    "--output",
                    "json",
                    "--asset-id",
                    "asset",
                    "--approved-by",
                    "owner",
                    "--approval-note",
                    "ok",
                    "--confirm-approved-asset",
                    "asset",
                    "--dry-run",
                ]
            },
            {
                "deprecate",
                [
                    "--output",
                    "json",
                    "--asset-id",
                    "asset",
                    "--actor",
                    "owner",
                    "--reason",
                    "obsolete",
                    "--confirm-approved-asset",
                    "asset",
                    "--dry-run",
                ]
            },
        };

    /// <summary>Rejects ambiguous duplicate options and options missing required values.</summary>
    [Fact]
    public void CliRejectsDuplicateAndMissingOptionValues()
    {
        Assert.Throws<AssetCtlException>(() => CliOptions.Parse(["--output", "json", "--output", "human"]));
        var options = CliOptions.Parse(["--output"]);
        Assert.Throws<AssetCtlException>(() => options.Value("output"));
    }

    /// <summary>Defines and enforces a closed option allowlist for every command.</summary>
    [Theory]
    [MemberData(nameof(CommandOptions))]
    public void EveryCommandHasAClosedOptionAllowlist(string command, string[] arguments)
    {
        CliOptions.Parse(arguments).RequireOnlyFor(command);
        AssetCtlException exception = Assert.Throws<AssetCtlException>(() =>
            CliOptions.Parse([.. arguments, "--offine"]).RequireOnlyFor(command)
        );

        Assert.Equal(2, exception.ExitCode);
        Assert.Contains("--offine", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Derives Godot resource paths from the configured asset root.</summary>
    [Fact]
    public void GodotPathUsesConfiguredAssetRootAndRejectsEscapes()
    {
        string root = Path.Combine(Path.GetTempPath(), $"assetctl-godot-path-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "game", "content"));
        try
        {
            EffectiveConfiguration configuration = PathConfiguration(root, "game/content");

            Assert.Equal("res://ui/icon.png", CliTypes.ToGodotPath(configuration, "game/content/ui/icon.png"));
            Assert.Throws<AssetCtlException>(() => CliTypes.ToGodotPath(configuration, "game/other/icon.png"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static EffectiveConfiguration PathConfiguration(string repositoryRoot, string godotRoot) =>
        new(
            repositoryRoot,
            new AssetCtlPaths(godotRoot, "catalog", "styles", "work", "runs", "state", "logs"),
            new AssetCtlPolicy(false, true, true, true, false, "reject"),
            new AssetCtlLimits(1, 1, 1, 1, 1, 1, 1),
            new SpendingLimits(0, 0, 0),
            new Dictionary<string, ProviderInstance>(StringComparer.Ordinal),
            [],
            [],
            new Dictionary<string, QualityTier>(StringComparer.Ordinal),
            new Dictionary<string, StyleProfile>(StringComparer.Ordinal),
            new Dictionary<string, string>(StringComparer.Ordinal),
            "hash"
        );

    /// <summary>Preserves semantic hard failures regardless of aggregate score.</summary>
    [Fact]
    public void SemanticHardFailuresCannotBeHiddenByHighScore()
    {
        var result = new SemanticReviewResult(
            false,
            true,
            true,
            true,
            1,
            1,
            [],
            false,
            false,
            1,
            "pass",
            "different-provider-family"
        );
        Assert.True(result.HasHardFailure);
    }

    /// <summary>Rejects reviewer output that is incomplete or outside schema bounds.</summary>
    [Fact]
    public void ReviewerRejectsOutOfRangeMetricsAndMissingFields()
    {
        ProviderException exception = Assert.Throws<ProviderException>(() =>
            OpenAiVisionReviewer.Parse("{\"overall_score\":2}")
        );
        Assert.Equal(ProviderErrorCategory.MalformedResponse, exception.Category);
    }

    /// <summary>Routes provider diagnostics through the shared credential redactor.</summary>
    [Theory]
    [InlineData("authorization: Bearer ")]
    [InlineData("api_key=")]
    [InlineData("token:")]
    public void EveryProviderDiagnosticUsesSharedRedaction(string prefix)
    {
        string credential = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
        Assert.DoesNotContain(credential, Redactor.Sanitize(prefix + credential), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Binds structured review fields so an editable score detail cannot retain valid evidence.</summary>
    [Fact]
    public void ReviewEvidenceBindsEveryStructuredField()
    {
        AssetRequest request = TestData.Request();
        byte[] bytes = [1, 2, 3];
        var review = new SemanticReviewResult(
            true,
            true,
            true,
            true,
            0.9,
            0.8,
            ["minor aliasing"],
            false,
            false,
            0.85,
            "pass",
            "different-provider-family",
            null,
            "reviewer",
            "profile"
        );
        string baseline = ReviewEvidence.Compute(request, bytes, "config", "reviewer", "profile", review);

        string changed = ReviewEvidence.Compute(
            request,
            bytes,
            "config",
            "reviewer",
            "profile",
            review with
            {
                VisualDefects = ["watermark"],
            }
        );

        Assert.False(string.Equals(baseline, changed, StringComparison.Ordinal));
    }
}
