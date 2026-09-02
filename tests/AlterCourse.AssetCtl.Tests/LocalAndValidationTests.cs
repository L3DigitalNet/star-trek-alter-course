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

    /// <summary>Removes an entire provider metadata subtree before validating retained SVG namespaces.</summary>
    [Fact]
    public void SvgRemovesProviderMetadataSubtreeBeforeValidation()
    {
        const string svg =
            "<svg xmlns='http://www.w3.org/2000/svg' xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#' xmlns:dc='http://purl.org/dc/elements/1.1/' width='64' height='64' viewBox='0 0 64 64'><metadata><rdf:RDF><rdf:Description><dc:creator>Provider Name</dc:creator></rdf:Description></rdf:RDF></metadata><path d='M8 8h48v48H8z'/></svg>";

        MechanicalValidationResult result = MechanicalValidator.Validate(
            TestData.Request(AssetFormat.Svg),
            Encoding.UTF8.GetBytes(svg),
            1_000_000,
            1_000_000
        );

        Assert.True(result.Passed, string.Join("; ", result.Findings));
        string normalized = Encoding.UTF8.GetString(result.NormalizedBytes);
        Assert.DoesNotContain("metadata", normalized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Provider Name", normalized, StringComparison.Ordinal);
    }

    /// <summary>Allows only a short sanitized identifier when the manifest permits SVG text.</summary>
    [Theory]
    [InlineData("ui.test", true)]
    [InlineData("Unsafe text!", false)]
    [InlineData("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789.extra", false)]
    public void SvgTextMustBeAPermittedShortSanitizedIdentifier(string identifier, bool expectedPass)
    {
        string svg =
            $"<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64' viewBox='0 0 64 64'><path d='M1 1h62v62H1z'/><text x='2' y='20'>{identifier}</text></svg>";
        AssetRequest request = TestData.Request(AssetFormat.Svg) with { Prohibited = ["watermark"] };

        MechanicalValidationResult result = MechanicalValidator.Validate(
            request,
            Encoding.UTF8.GetBytes(svg),
            1_000_000,
            1_000_000
        );

        Assert.Equal(expectedPass, result.Passed);
    }

    /// <summary>Rejects an SVG whose target-size render contains no drawable output.</summary>
    [Fact]
    public void SvgRejectsTargetSizeRenderFailure()
    {
        const string svg =
            "<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64' viewBox='0 0 64 64'><defs><path id='unused' d='M0 0h64v64z'/></defs></svg>";

        MechanicalValidationResult result = MechanicalValidator.Validate(
            TestData.Request(AssetFormat.Svg),
            Encoding.UTF8.GetBytes(svg),
            1_000_000,
            1_000_000
        );

        Assert.False(result.Passed);
        Assert.Contains("target-size", string.Join("; ", result.Findings), StringComparison.OrdinalIgnoreCase);
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

    /// <summary>Rejects an external URL hidden after a safe local fragment in the same SVG value.</summary>
    [Fact]
    public void SvgRejectsExternalResourceAfterLocalFragment()
    {
        const string svg =
            "<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64' viewBox='0 0 64 64'><path fill=\"url(#safe) url('https://untrusted.example/paint')\" d='M0 0h64v64z'/></svg>";

        global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult result =
            MechanicalValidator.Validate(
                TestData.Request(AssetFormat.Svg),
                Encoding.UTF8.GetBytes(svg),
                1_000_000,
                1_000_000
            );

        Assert.False(result.Passed);
        Assert.Contains("fill", string.Join("; ", result.Findings), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Preserves local fragment URL references used by safe SVG gradients.</summary>
    [Fact]
    public void SvgAllowsLocalFragmentResources()
    {
        const string svg =
            "<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64' viewBox='0 0 64 64'><defs><linearGradient id='paint'><stop offset='0' stop-color='#fff'/><stop offset='1' stop-color='#000'/></linearGradient></defs><rect width='64' height='64' fill='url(#paint)'/></svg>";

        global::AlterCourse.AssetCtl.Domain.DomainModels.MechanicalValidationResult result =
            MechanicalValidator.Validate(
                TestData.Request(AssetFormat.Svg),
                Encoding.UTF8.GetBytes(svg),
                1_000_000,
                1_000_000
            );

        Assert.True(result.Passed, string.Join("; ", result.Findings));
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

    /// <summary>Rejects document-level processing instructions before SVG rendering or normalization.</summary>
    [Fact]
    public void SvgRejectsExternalStylesheetProcessingInstruction()
    {
        const string svg =
            "<?xml-stylesheet type='text/css' href='https://untrusted.example/style.css'?><svg xmlns='http://www.w3.org/2000/svg' width='64' height='64' viewBox='0 0 64 64'><path d='M0 0h64v64z'/></svg>";
        MechanicalValidationResult result = MechanicalValidator.Validate(
            TestData.Request(AssetFormat.Svg),
            Encoding.UTF8.GetBytes(svg),
            1_000_000,
            1_000_000
        );
        Assert.False(result.Passed);
        Assert.Contains("processing", string.Join("; ", result.Findings), StringComparison.OrdinalIgnoreCase);
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

    /// <summary>Rejects a fully opaque PNG when the output contract requires transparency.</summary>
    [Fact]
    public void OpaquePngFailsTransparencyContract()
    {
        using var bitmap = new SkiaSharp.SKBitmap(64, 64, isOpaque: true);
        bitmap.Erase(SkiaSharp.SKColors.Black);
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using SkiaSharp.SKData encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);

        MechanicalValidationResult result = MechanicalValidator.Validate(
            TestData.Request(),
            encoded.ToArray(),
            1_000_000,
            1_000_000
        );

        Assert.False(result.Passed);
        Assert.Contains("transparent", string.Join("; ", result.Findings), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Rejects bytes whose decoded media type disagrees with the requested output format.</summary>
    [Fact]
    public void OutputFormatMismatchFailsClosed()
    {
        byte[] png = LocalPlaceholderGenerator.RenderPng(TestData.Request());
        MechanicalValidationResult result = MechanicalValidator.Validate(
            TestData.Request(AssetFormat.Svg),
            png,
            1_000_000,
            1_000_000
        );
        Assert.False(result.Passed);
    }

    /// <summary>Normalizes a supported provider WebP raster into the catalog's canonical PNG output.</summary>
    [Fact]
    public void WebpRasterNormalizesToPng()
    {
        using var bitmap = new SkiaSharp.SKBitmap(64, 64);
        bitmap.Erase(new SkiaSharp.SKColor(40, 80, 160, 128));
        using var image = SkiaSharp.SKImage.FromBitmap(bitmap);
        using SkiaSharp.SKData encoded = image.Encode(SkiaSharp.SKEncodedImageFormat.Webp, 100);

        MechanicalValidationResult result = MechanicalValidator.Validate(
            TestData.Request(),
            encoded.ToArray(),
            1_000_000,
            1_000_000
        );

        Assert.True(result.Passed, string.Join("; ", result.Findings));
        Assert.Equal("image/png", result.MediaType);
        Assert.True(result.NormalizedBytes.AsSpan().StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }));
    }

    /// <summary>Rejects non-finite, zero-area, and malformed SVG view boxes.</summary>
    [Theory]
    [InlineData("0 0 0 64")]
    [InlineData("0 0 NaN 64")]
    [InlineData("0 0 64")]
    public void SvgRejectsMalformedViewBox(string viewBox)
    {
        string svg =
            $"<svg xmlns='http://www.w3.org/2000/svg' width='64' height='64' viewBox='{viewBox}'><path d='M0 0h64v64z'/></svg>";
        MechanicalValidationResult result = MechanicalValidator.Validate(
            TestData.Request(AssetFormat.Svg),
            Encoding.UTF8.GetBytes(svg),
            1_000_000,
            1_000_000
        );
        Assert.False(result.Passed);
    }

    /// <summary>Accepts standard pixel dimensions and comma-separated view boxes.</summary>
    [Theory]
    [InlineData("64px", "64px", "0,0,64,64")]
    [InlineData("64", "64", "0\t0 64\n64")]
    public void SvgAcceptsSafeStandardDimensionForms(string width, string height, string viewBox)
    {
        string svg =
            $"<svg xmlns='http://www.w3.org/2000/svg' width='{width}' height='{height}' viewBox='{viewBox}'><path d='M0 0h64v64z'/></svg>";
        MechanicalValidationResult result = MechanicalValidator.Validate(
            TestData.Request(AssetFormat.Svg),
            Encoding.UTF8.GetBytes(svg),
            1_000_000,
            1_000_000
        );

        Assert.True(result.Passed, string.Join("; ", result.Findings));
    }

    /// <summary>Rejects an APNG after the decoder confirms that it contains multiple frames.</summary>
    [Fact]
    public void AnimatedPngFailsClosed()
    {
        byte[] png = LocalPlaceholderGenerator.RenderPng(TestData.Request());
        byte[] animated = AnimatedPngFixture.Create(png);

        MechanicalValidationResult result = MechanicalValidator.Validate(
            TestData.Request(),
            animated,
            1_000_000,
            1_000_000
        );

        Assert.False(result.Passed);
        Assert.Contains("animated", string.Join("; ", result.Findings), StringComparison.OrdinalIgnoreCase);
    }
}
