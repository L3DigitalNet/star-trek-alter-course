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
    private static readonly System.Buffers.SearchValues<char> InvalidLocalFragmentCharacters =
        System.Buffers.SearchValues.Create(" \t\r\n'\"()");
    private static readonly HashSet<string> AllowedSvgElements = new(StringComparer.Ordinal)
    {
        "svg",
        "g",
        "defs",
        "path",
        "rect",
        "circle",
        "ellipse",
        "line",
        "polyline",
        "polygon",
        "title",
        "desc",
        "metadata",
        "use",
        "clipPath",
        "mask",
        "linearGradient",
        "radialGradient",
        "stop",
        "filter",
        "feBlend",
        "feColorMatrix",
        "feComposite",
        "feFlood",
        "feGaussianBlur",
        "feMerge",
        "feMergeNode",
        "feOffset",
    };

    public static MechanicalValidationResult Validate(
        AssetRequest request,
        byte[] bytes,
        long maximumBytes,
        long maximumPixels
    )
    {
        try
        {
            OutputContractPolicy.Validate(request.Output, maximumPixels);
        }
        catch (AssetCtlException exception)
        {
            return Failure(exception.Message);
        }

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
            string? codecFinding = ValidatePngCodec(codec, bytes);
            if (codecFinding is not null)
            {
                return Failure(codecFinding);
            }

            SKImageInfo info = codec.Info;
            if (!DimensionsMatch(request.Output, info.Width, info.Height, maximumPixels))
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

    private static string? ValidatePngCodec(SKCodec? codec, byte[] bytes)
    {
        if (codec is null || codec.EncodedFormat != SKEncodedImageFormat.Png)
        {
            return "file is not a decodable PNG";
        }

        return codec.FrameCount > 1 || ContainsPngAnimationControl(bytes) ? "animated PNG output is prohibited" : null;
    }

    private static bool ContainsPngAnimationControl(byte[] bytes)
    {
        int offset = 8;
        while (offset <= bytes.Length - 12)
        {
            int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(offset, 4));
            if (length < 0 || length > bytes.Length - offset - 12)
            {
                return false;
            }

            if (bytes.AsSpan(offset + 4, 4).SequenceEqual("acTL"u8))
            {
                return true;
            }

            offset += length + 12;
        }

        return false;
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
            if (document.Nodes().OfType<XProcessingInstruction>().Any())
                return Failure("SVG processing instructions are prohibited");
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
            if (!DimensionsMatch(request.Output, width, height, maximumPixels))
            {
                return Failure("SVG dimensions exceed policy");
            }

            if (!TryGetViewBox(root, out double viewWidth, out double viewHeight))
            {
                return Failure("SVG requires a finite four-number viewBox with positive dimensions");
            }

            byte[] normalized = NormalizeSvg(document);
            using var svg = new SKSvg();
            using var renderStream = new MemoryStream(normalized, writable: false);
            if (svg.Load(renderStream) is null)
            {
                return Failure("sanitized SVG did not render");
            }

            Dictionary<int, byte[]> previews = RenderSvgPreviews(
                svg,
                request.Output.TargetDisplaySizes,
                viewWidth,
                viewHeight
            );

            return new MechanicalValidationResult(true, "image/svg+xml", width, height, true, [], normalized, previews);
        }
        catch (Exception exception) when (exception is XmlException or InvalidOperationException or FormatException)
        {
            return Failure($"SVG parse or render failed: {exception.Message}");
        }
    }

    private static bool TryGetViewBox(XElement root, out double width, out double height)
    {
        string[]? parts = root.Attribute("viewBox")?.Value?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is { Length: 4 })
        {
            return TryParseValidViewBox(parts, out width, out height);
        }

        width = 0;
        height = 0;
        return false;
    }

    private static bool TryParseValidViewBox(string[] parts, out double width, out double height)
    {
        width = 0;
        height = 0;
        double[] values = new double[4];
        for (int index = 0; index < parts.Length; index++)
        {
            if (
                !double.TryParse(parts[index], NumberStyles.Float, CultureInfo.InvariantCulture, out values[index])
                || !double.IsFinite(values[index])
            )
            {
                return false;
            }
        }

        width = values[2];
        height = values[3];
        return width > 0 && height > 0;
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

    private static bool DimensionsMatch(OutputContract expected, int width, int height, long maximumPixels) =>
        OutputContractPolicy.AllowsDimensions(width, height, maximumPixels)
        && width == expected.Width
        && height == expected.Height;

    private static string? ValidateSvgElements(XElement root, AssetRequest request)
    {
        foreach (XElement element in root.DescendantsAndSelf().ToArray())
        {
            string local = element.Name.LocalName;
            if (
                element.Name.Namespace != SvgNamespace
                || !AllowedSvgElements.Contains(local)
                || local is "image" or "feImage"
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
                    || string.Equals(name, "base", StringComparison.OrdinalIgnoreCase)
                    || IsExternalResource(name, value)
                )
                {
                    return $"prohibited SVG attribute '{name}'";
                }
            }
        }

        return null;
    }

    private static bool IsExternalResource(string name, string value)
    {
        if (name is "href" or "src")
        {
            return !IsLocalFragment(value);
        }

        int searchFrom = 0;
        while (value.IndexOf("url(", searchFrom, StringComparison.OrdinalIgnoreCase) is int urlStart && urlStart >= 0)
        {
            int targetStart = urlStart + 4;
            int targetEnd = value.IndexOf(')', targetStart);
            if (targetEnd < 0)
            {
                return true;
            }

            string target = value[targetStart..targetEnd].Trim();
            if (target.Length >= 2 && target[0] is '\'' or '"' && target[^1] == target[0])
            {
                target = target[1..^1].Trim();
            }

            if (!IsLocalFragment(target))
            {
                return true;
            }

            searchFrom = targetEnd + 1;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out _);
    }

    private static bool IsLocalFragment(string value) =>
        value.Length > 1 && value[0] == '#' && value.AsSpan(1).IndexOfAny(InvalidLocalFragmentCharacters) < 0;

    private static Dictionary<int, byte[]> RenderSvgPreviews(
        SKSvg svg,
        IReadOnlyList<int> sizes,
        double width,
        double height
    )
    {
        Dictionary<int, byte[]> previews = [];
        foreach (int size in sizes)
        {
            using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Rgba8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            if (surface is null || svg.Picture is null)
            {
                throw new InvalidOperationException("target-size SVG render surface was unavailable");
            }

            surface.Canvas.Scale(Math.Min(size / (float)width, size / (float)height));
            surface.Canvas.DrawPicture(svg.Picture);
            using SKImage image = surface.Snapshot();
            using SKData data =
                image.Encode(SKEncodedImageFormat.Png, 100)
                ?? throw new InvalidOperationException("target-size SVG preview encoding failed");
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
