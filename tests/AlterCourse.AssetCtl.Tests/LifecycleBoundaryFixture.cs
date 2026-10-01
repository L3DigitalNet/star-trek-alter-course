using System.Security.Cryptography;
using AlterCourse.AssetCtl.Generation;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Provides isolated Linux filesystem objects and a credential-free candidate for boundary tests.</summary>
internal sealed class LifecycleBoundaryFixture : IDisposable
{
    public LifecycleBoundaryFixture(byte[]? bytes = null, long maximumBytes = 1_000_000)
    {
        Root = Path.Combine(Path.GetTempPath(), "assetctl-lifecycle-boundary-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(Root, "assets"));
        Directory.CreateDirectory(Path.Combine(Root, "catalog"));
        Configuration = new EffectiveConfiguration(
            Root,
            new AssetCtlPaths("assets", "catalog", "styles", "work", "runs", "state", "logs"),
            new AssetCtlPolicy(false, true, true, true, false, "reject"),
            new AssetCtlLimits(maximumBytes, maximumBytes, 10, 10, 10, 30, 1_000_000),
            new SpendingLimits(0, 0, 0),
            new Dictionary<string, ProviderInstance>(StringComparer.Ordinal),
            [],
            [],
            new Dictionary<string, QualityTier>(StringComparer.Ordinal)
            {
                ["development"] = new QualityTier("development", 1, 1, "disabled", true, 0),
            },
            new Dictionary<string, StyleProfile>(StringComparer.Ordinal)
            {
                ["engineering-icons"] = new StyleProfile("engineering-icons", "test", [], []),
            },
            new Dictionary<string, string>(StringComparer.Ordinal),
            "config-hash"
        );
        AssetRequest request = TestData.Request(AssetFormat.Svg) with
        {
            Lifecycle = AssetLifecycle.Candidate,
            Output = TestData.Request(AssetFormat.Svg).Output with { Path = "assets/asset.svg" },
        };
        Bytes = bytes ?? LocalPlaceholderGenerator.RenderSvg(request);
        Manifest = new AssetManifest(
            "1",
            request,
            1,
            new RightsRecord("original-project-created", "project", null, null, "fixture"),
            null,
            null,
            null,
            new IntegrityRecord(Convert.ToHexStringLower(SHA256.HashData(Bytes)), Bytes.LongLength, "image/svg+xml"),
            new ApprovalRecord(null, null, null),
            null,
            "catalog/test.asset.yaml"
        );
        File.WriteAllBytes(AssetPath, Bytes);
        File.WriteAllText(ManifestPath, ManifestStore.Serialize(Manifest));
    }

    public string Root { get; }
    public EffectiveConfiguration Configuration { get; }
    public AssetManifest Manifest { get; }
    public byte[] Bytes { get; }
    public string AssetPath => Path.Combine(Root, Manifest.Request.Output.Path);
    public string ManifestPath => Path.Combine(Root, Manifest.ManifestPath);

    public CliOptions ApprovalOptions(bool dryRun = false) =>
        CliOptions.Parse(
            [
                "--asset-id", Manifest.Request.Id,
                "--approved-by", "fixture-owner",
                "--approval-note", "fixture-review",
                "--confirm-approved-asset", Manifest.Request.Id,
                .. dryRun ? new[] { "--dry-run" } : [],
            ]
        );

    public void Dispose()
    {
        string catalog = Path.Combine(Root, "catalog");
        if (new DirectoryInfo(catalog).LinkTarget is not null)
        {
            Directory.Delete(catalog);
        }

        Directory.Delete(Root, recursive: true);
    }
}
