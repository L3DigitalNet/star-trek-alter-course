using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;
using Svg.Skia;

namespace AlterCourse.AssetCtl.Validation;

/// <summary>Normalizes untrusted raster or SVG bytes and returns only fully decoded, policy-conforming output.</summary>
internal static class MechanicalValidator
{
    private static readonly XNamespace SvgNamespace = "http://www.w3.org/2000/svg";

    public static MechanicalValidationResult Validate(
        AssetRequest request,
        byte[] bytes,
        long maximumBytes,
        long maximumPixels
    )
    {
        if (bytes.Length == 0 || bytes.LongLength > maximumBytes)
        {
            return Failure("output byte length is outside policy");
        }

        return request.Output.Format switch
        {
            AssetFormat.Png => ValidatePng(request, bytes, maximumPixels),
            AssetFormat.Svg => ValidateSvg(request, bytes, maximumPixels),
            _ => Failure("unsupported output format"),
        };
    }

    private static MechanicalValidationResult ValidatePng(AssetRequest request, byte[] bytes, long maximumPixels)
    {
        try
        {
            using var codec = SKCodec.Create(new SKMemoryStream(bytes));
            if (codec is null || codec.EncodedFormat != SKEncodedImageFormat.Png)
            {
                return Failure("file is not a decodable PNG");
            }

            SKImageInfo info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || (long)info.Width * info.Height > maximumPixels)
            {
                return Failure("decoded dimensions exceed policy");
            }

            var decodeInfo = new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            using var bitmap = new SKBitmap(decodeInfo);
            if (codec.GetPixels(decodeInfo, bitmap.GetPixels()) is not SKCodecResult.Success)
            {
                return Failure("full PNG decode failed");
            }

            bool hasTransparentPixel = false;
            for (int y = 0; y < bitmap.Height && !hasTransparentPixel; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    if (bitmap.GetPixel(x, y).Alpha < byte.MaxValue)
                    {
                        hasTransparentPixel = true;
                        break;
                    }
                }
            }

            if (request.Output.TransparencyRequired && !hasTransparentPixel)
            {
                return Failure("transparent output was required but no transparent pixel exists");
            }

            using var normalizedImage = SKImage.FromBitmap(bitmap);
            using SKData normalizedData = normalizedImage.Encode(SKEncodedImageFormat.Png, 100);
            byte[] normalized = normalizedData.ToArray();
            return new MechanicalValidationResult(
                true,
                "image/png",
                info.Width,
                info.Height,
                hasTransparentPixel,
                [],
                normalized,
                BuildPreviews(bitmap, request.Output.TargetDisplaySizes)
            );
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            return Failure($"PNG decode failed: {exception.Message}");
        }
    }

    private static MechanicalValidationResult ValidateSvg(AssetRequest request, byte[] bytes, long maximumPixels)
    {
        try
        {
            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                MaxCharactersInDocument = 1_048_576,
                MaxCharactersFromEntities = 0,
            };
            using var source = new MemoryStream(bytes, writable: false);
            using var reader = XmlReader.Create(source, settings);
            var document = XDocument.Load(reader, LoadOptions.None);
            global::System.Xml.Linq.XElement? root = document.Root;
            if (root?.Name != SvgNamespace + "svg")
            {
                return Failure("document root is not SVG");
            }

            string? unsafeFinding = ValidateSvgElements(root, request);
            if (unsafeFinding is not null)
            {
                return Failure(unsafeFinding);
            }

            int width = ParseDimension(root.Attribute("width")?.Value, "width");
            int height = ParseDimension(root.Attribute("height")?.Value, "height");
            if (width <= 0 || height <= 0 || (long)width * height > maximumPixels)
            {
                return Failure("SVG dimensions exceed policy");
            }

            string[]? viewBox = root.Attribute("viewBox")?.Value?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (
                viewBox is not { Length: 4 }
                || !viewBox.All(part => double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
            )
            {
                return Failure("SVG requires a finite four-number viewBox");
            }

            byte[] normalized = NormalizeSvg(document);
            using var svg = new SKSvg();
            using var renderStream = new MemoryStream(normalized, writable: false);
            if (svg.Load(renderStream) is null)
            {
                return Failure("sanitized SVG did not render");
            }

            Dictionary<int, byte[]> previews = RenderSvgPreviews(svg, request.Output.TargetDisplaySizes, width, height);

            return new MechanicalValidationResult(true, "image/svg+xml", width, height, true, [], normalized, previews);
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException or FormatException)
        {
            return Failure($"SVG parse or render failed: {exception.Message}");
        }
    }

    private static byte[] NormalizeSvg(XDocument document)
    {
        using var normalizedStream = new MemoryStream();
        var writerSettings = new XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(false),
            Indent = false,
            OmitXmlDeclaration = true,
        };
        using (var writer = XmlWriter.Create(normalizedStream, writerSettings))
        {
            document.Save(writer);
        }

        return normalizedStream.ToArray();
    }

    private static string? ValidateSvgElements(XElement root, AssetRequest request)
    {
        foreach (XElement element in root.DescendantsAndSelf().ToArray())
        {
            string local = element.Name.LocalName;
            if (
                local is "script" or "foreignObject" or "image" or "style"
                || string.Equals(local, "text", StringComparison.Ordinal)
                    && request.Prohibited.Contains("text", StringComparer.OrdinalIgnoreCase)
            )
            {
                return $"prohibited SVG element '{local}'";
            }

            if (string.Equals(local, "metadata", StringComparison.Ordinal))
            {
                element.Remove();
                continue;
            }

            foreach (XAttribute attribute in element.Attributes().ToArray())
            {
                if (attribute.IsNamespaceDeclaration)
                {
                    continue;
                }

                string name = attribute.Name.LocalName;
                string value = attribute.Value.Trim();
                if (
                    name.StartsWith("on", StringComparison.OrdinalIgnoreCase)
                    || value.Contains("url(", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("http:", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("https:", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                    || value.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
                )
                {
                    return $"prohibited SVG attribute '{name}'";
                }
            }
        }

        return null;
    }

    private static Dictionary<int, byte[]> RenderSvgPreviews(SKSvg svg, IReadOnlyList<int> sizes, int width, int height)
    {
        Dictionary<int, byte[]> previews = [];
        foreach (int size in sizes)
        {
            using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.Scale(Math.Min(size / (float)width, size / (float)height));
            surface.Canvas.DrawPicture(svg.Picture);
            using SKImage image = surface.Snapshot();
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            previews.Add(size, data.ToArray());
        }

        return previews;
    }

    private static int ParseDimension(string? value, string name)
    {
        if (
            value is null
            || value.EndsWith('%')
            || !int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int result)
        )
        {
            throw new FormatException($"SVG {name} must be an integer pixel dimension.");
        }

        return result;
    }

    private static Dictionary<int, byte[]> BuildPreviews(SKBitmap bitmap, IReadOnlyList<int> sizes)
    {
        var result = new Dictionary<int, byte[]>();
        foreach (int size in sizes.Distinct().Order())
        {
            if (size <= 0 || size > 4096)
            {
                throw new ArgumentException("Target display size is outside safety bounds.", nameof(sizes));
            }

            using SKBitmap resized = bitmap.Resize(
                new SKImageInfo(size, size),
                new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None)
            );
            using var image = SKImage.FromBitmap(resized);
            using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
            result.Add(size, data.ToArray());
        }

        return result;
    }

    private static MechanicalValidationResult Failure(string finding) =>
        new(false, "application/octet-stream", 0, 0, false, [finding], [], new Dictionary<int, byte[]>());
}
