using Microsoft.Extensions.Logging.Abstractions;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Inspects actual process stderr at configuration and manifest producer boundaries.</summary>
[Collection("Diagnostic process boundary")]
public sealed class SourceDiagnosticPrivacyRegressionTests
{
    private const string Sentinel = "/home/synthetic-owner/private/sentinel-source-secret";

    /// <summary>Unadmitted identities and scalar values never become error-channel context.</summary>
    [Theory]
    [InlineData("providers.yaml", "  local-placeholder:\n", "  '" + Sentinel + "': []\n  local-placeholder:\n")]
    [InlineData("providers.yaml", "      default:\n", "      '" + Sentinel + "': []\n      default:\n")]
    [InlineData("quality-tiers.yaml", "  development:\n", "  '" + Sentinel + "': []\n  development:\n")]
    [InlineData("providers.yaml", "adapter: \"local-placeholder\"", "adapter: '" + Sentinel + "'")]
    [InlineData("manifest", "format: 'png'", "format: '" + Sentinel + "'")]
    [InlineData("manifest", "lifecycle: 'placeholder'", "lifecycle: '" + Sentinel + "'")]
    [InlineData("manifest", "style_profile: 'engineering-icons'", "style_profile: '" + Sentinel + "'")]
    [InlineData("manifest", "quality_tier: 'development'", "quality_tier: '" + Sentinel + "'")]
    [InlineData(
        "assetctl.yaml",
        "godot_asset_root: \"src/AlterCourse.Godot/assets\"",
        "godot_asset_root: \"src/sentinel-source-root\""
    )]
    public async Task UnknownSourceValuesRemainBoundedInRenderedStderr(
        string source,
        string original,
        string replacement
    )
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        string path = source is "manifest"
            ? fixture.ManifestPath
            : Path.Combine(fixture.Root, "config", "assets", source);
        string before = await File.ReadAllTextAsync(path);
        Assert.Contains(original, before, StringComparison.Ordinal);
        await File.WriteAllTextAsync(path, before.Replace(original, replacement, StringComparison.Ordinal));

        int exit = await Program.RunProcessAsync(["status", "--output", "json"], (_, _) => NullLoggerFactory.Instance);

        Assert.Equal(2, exit);
        string rendered = fixture.Error.ToString();
        Assert.DoesNotContain("sentinel", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Root, rendered, StringComparison.Ordinal);
        Assert.NotEmpty(rendered);
        Assert.True(rendered.Length < 512);
        Assert.Empty(fixture.Output.ToString());
    }
}
