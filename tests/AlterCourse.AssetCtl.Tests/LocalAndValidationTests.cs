using System.Text;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Validation;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Verifies deterministic local generation and mechanical validation of untrusted images.</summary>
public sealed class LocalAndValidationTests
{
    /// <summary>Produces byte-identical PNG placeholders that pass full decoding checks.</summary>
    [Fact]
    public void LocalPngIsDeterministicAndFullyValid()
    {
        global::AlterCourse.AssetCtl.Domain.DomainModels.AssetRequest request = TestData.Request();
        byte[] first = LocalPlaceholderGenerator.RenderPng(request);
        byte[] second = LocalPlaceholderGenerator.RenderPng(request);
        Assert.Equal(first, second);
        global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult result =
            MechanicalValidator.Validate(request, first, 1_000_000, 1_000_000);
        Assert.True(result.Passed, string.Join("; ", result.Findings));
        Assert.Equal(3, result.TargetPreviews.Count);
        Assert.True(result.HasAlpha);
    }

    /// <summary>Produces byte-identical SVG placeholders that sanitize and render successfully.</summary>
    [Fact]
    public void LocalSvgIsDeterministicSanitizedAndRenderable()
    {
        global::AlterCourse.AssetCtl.Domain.DomainModels.AssetRequest request = TestData.Request(AssetFormat.Svg);
        byte[] first = LocalPlaceholderGenerator.RenderSvg(request);
        Assert.Equal(first, LocalPlaceholderGenerator.RenderSvg(request));
        global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult result =
            MechanicalValidator.Validate(request, first, 1_000_000, 1_000_000);
        Assert.True(result.Passed, string.Join("; ", result.Findings));
        Assert.DoesNotContain(
            "metadata",
            Encoding.UTF8.GetString(result.NormalizedBytes),
            StringComparison.OrdinalIgnoreCase
        );
        Assert.Equal(3, result.TargetPreviews.Count);
    }

    /// <summary>Rejects active, external, embedded, and manifest-prohibited SVG content.</summary>
    [Theory]
    [InlineData("<script>alert(1)</script>", "script")]
    [InlineData("<foreignObject/>", "foreignObject")]
    [InlineData("<image href='data:image/png;base64,AA=='/>", "image")]
    [InlineData("<path onclick='x()'/>", "onclick")]
    [InlineData("<path fill='url(https://evil.example/a)'/>", "fill")]
    [InlineData("<text>x</text>", "text")]
    public void SvgRejectsActiveExternalEmbeddedAndProhibitedContent(string body, string expected)
    {
        string svg = $"<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64' viewBox='0 0 64 64'>{body}</svg>";
        global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult result =
            MechanicalValidator.Validate(
                TestData.Request(AssetFormat.Svg),
                Encoding.UTF8.GetBytes(svg),
                1_000_000,
                1_000_000
            );
        Assert.False(result.Passed);
        Assert.Contains(expected, string.Join("; ", result.Findings), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Rejects SVG document types before entity expansion can occur.</summary>
    [Fact]
    public void SvgRejectsDtdBeforeEntityExpansion()
    {
        const string svg =
            "<!DOCTYPE svg [<!ENTITY x SYSTEM 'file:///etc/passwd'>]><svg xmlns='http://www.w3.org/2000/svg' width='64' height='64' viewBox='0 0 64 64'><title>&x;</title></svg>";
        global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult result =
            MechanicalValidator.Validate(
                TestData.Request(AssetFormat.Svg),
                Encoding.UTF8.GetBytes(svg),
                1_000_000,
                1_000_000
            );
        Assert.False(result.Passed);
    }

    /// <summary>Fails closed for corrupt PNG data and encoded data beyond policy limits.</summary>
    [Fact]
    public void CorruptAndOversizedPngsFailClosed()
    {
        Assert.False(MechanicalValidator.Validate(TestData.Request(), [1, 2, 3], 1_000_000, 1_000_000).Passed);
        Assert.False(
            MechanicalValidator
                .Validate(TestData.Request(), LocalPlaceholderGenerator.RenderPng(TestData.Request()), 10, 1_000_000)
                .Passed
        );
    }
}
