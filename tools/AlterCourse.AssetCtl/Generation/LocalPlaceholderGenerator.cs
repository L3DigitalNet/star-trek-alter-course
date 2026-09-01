using System.Security.Cryptography;
using System.Text;
using SkiaSharp;

namespace AlterCourse.AssetCtl.Generation;

internal sealed class LocalPlaceholderGenerator : IAssetGenerator
{
    public const string Version = "1";

    private static readonly IReadOnlySet<AssetCapability> Capabilities = new HashSet<AssetCapability>
    {
        AssetCapability.RasterGenerate,
        AssetCapability.VectorGenerate,
        AssetCapability.ImageTransparentOutput,
    };

    public string AdapterId => "local-placeholder";

    public IReadOnlySet<AssetCapability> SupportedCapabilities => Capabilities;

    public void ValidateOptions(IReadOnlyDictionary<string, string> options)
    {
        if (options.Count != 0)
        {
            throw new ProviderException(ProviderErrorCategory.InvalidRequest, "Local placeholder accepts no options.");
        }
    }

    public Task<GenerationBatchResult> GenerateAsync(
        ProviderExecutionContext context,
        NormalizedGenerationRequest request,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateOptions(context.Model.Options);
        byte[] bytes = request.Request.Output.Format switch
        {
            AssetFormat.Svg => RenderSvg(request.Request),
            AssetFormat.Png => RenderPng(request.Request),
            _ => throw new ProviderException(
                ProviderErrorCategory.UnsupportedOutput,
                "Unsupported local output format."
            ),
        };
        string mediaType = request.Request.Output.Format == AssetFormat.Svg ? "image/svg+xml" : "image/png";
        GeneratedCandidate[] candidates = [new(0, bytes, mediaType, null, 0m)];
        return Task.FromResult(new GenerationBatchResult(candidates, null, 0m));
    }

    public static byte[] RenderSvg(AssetRequest request)
    {
        string color = Color(request.Id);
        string label = Initials(request.Id);
        int width = request.Output.Width;
        int height = request.Output.Height;
        string svg =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{width}\" height=\"{height}\" viewBox=\"0 0 {width} {height}\"><rect x=\"1\" y=\"1\" width=\"{width - 2}\" height=\"{height - 2}\" rx=\"{Math.Max(2, Math.Min(width, height) / 10)}\" fill=\"none\" stroke=\"#{color}\" stroke-width=\"2\"/><path d=\"M {width / 4} {height / 4} L {width * 3 / 4} {height * 3 / 4} M {width * 3 / 4} {height / 4} L {width / 4} {height * 3 / 4}\" stroke=\"#{color}\" stroke-width=\"{Math.Max(2, Math.Min(width, height) / 12)}\"/><title>Placeholder {System.Security.SecurityElement.Escape(request.Id)} {label}</title></svg>";
        return Encoding.UTF8.GetBytes(svg);
    }

    public static byte[] RenderPng(AssetRequest request)
    {
        var info = new SKImageInfo(
            request.Output.Width,
            request.Output.Height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul
        );
        using var surface = SKSurface.Create(info);
        SKCanvas canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        string hex = Color(request.Id);
        var color = SKColor.Parse('#' + hex);
        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = false,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(2, Math.Min(info.Width, info.Height) / 12f),
        };
        canvas.DrawRect(1, 1, info.Width - 2, info.Height - 2, paint);
        canvas.DrawLine(info.Width / 4f, info.Height / 4f, info.Width * 3f / 4f, info.Height * 3f / 4f, paint);
        canvas.DrawLine(info.Width * 3f / 4f, info.Height / 4f, info.Width / 4f, info.Height * 3f / 4f, paint);
        using SKImage image = surface.Snapshot();
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static string Color(string id)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes($"assetctl-local-v{Version}:{id}"));
        // Saturated midrange channels remain conspicuous on both light and dark game backgrounds.
        return $"{64 + hash[0] % 160:X2}{64 + hash[1] % 160:X2}{64 + hash[2] % 160:X2}";
    }

    private static string Initials(string id) =>
        string.Concat(id.Split('.').TakeLast(2).Select(part => char.ToUpperInvariant(part[0])));
}
