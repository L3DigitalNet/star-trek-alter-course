using AlterCourse.AssetCtl.Generation;
using RepositoryLocator = AlterCourse.AssetCtl.Cli.CliTypes.RepositoryLocator;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Locks deterministic local placeholder bytes to reviewed repository fixtures.</summary>
public sealed class GoldenPlaceholderTests
{
    /// <summary>Matches PNG output against fixed bytes rather than another renderer invocation.</summary>
    [Fact]
    public void PngMatchesReviewedGoldenBytes()
    {
        string repository = RepositoryLocator.Find(Environment.CurrentDirectory);
        string expected = File.ReadAllText(
            Path.Combine(repository, "tests/AlterCourse.AssetCtl.Tests/Golden/local-placeholder.png.base64")
        );
        Assert.Equal(Convert.FromBase64String(expected), LocalPlaceholderGenerator.RenderPng(TestData.Request()));
    }

    /// <summary>Matches SVG output against a fixed, human-reviewable byte fixture.</summary>
    [Fact]
    public void SvgMatchesReviewedGoldenBytes()
    {
        string repository = RepositoryLocator.Find(Environment.CurrentDirectory);
        string expected = File.ReadAllText(
            Path.Combine(repository, "tests/AlterCourse.AssetCtl.Tests/Golden/local-placeholder.svg.base64")
        );
        Assert.Equal(
            Convert.FromBase64String(expected),
            LocalPlaceholderGenerator.RenderSvg(TestData.Request(AssetFormat.Svg))
        );
    }
}
