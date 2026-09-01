using System.Security.Cryptography;

namespace AlterCourse.AssetCtl.Tests;

internal static class TestData
{
    public static AssetRequest Request(AssetFormat format = AssetFormat.Png) =>
        new(
            "ui.test.placeholder",
            AssetLifecycle.Placeholder,
            "icon",
            "Mark an unavailable test control.",
            new OutputContract(
                format == AssetFormat.Png
                    ? "src/AlterCourse.Godot/assets/test/placeholder.png"
                    : "src/AlterCourse.Godot/assets/test/placeholder.svg",
                format,
                64,
                64,
                true,
                [16, 24, 64]
            ),
            "engineering-icons",
            ["simple silhouette"],
            ["text", "watermark"],
            ["test"],
            [],
            "development"
        );

    public static ProviderExecutionContext Context(string adapter, string model = "model", string? credential = null)
    {
        var capabilities = new HashSet<AssetCapability>
        {
            AssetCapability.RasterGenerate,
            AssetCapability.VectorGenerate,
            AssetCapability.ImageEdit,
            AssetCapability.ImageReferenceInput,
            AssetCapability.ImageTransparentOutput,
            AssetCapability.ReviewSemantic,
            AssetCapability.ReviewReferenceComparison,
        };
        var profile = new ModelProfile(
            "profile",
            model,
            capabilities,
            0.01m,
            "fixed-output",
            new Dictionary<string, string>(StringComparer.Ordinal)
        );
        var provider = new ProviderInstance(
            "arbitrary-instance",
            adapter,
            true,
            new Uri(
                adapter switch
                {
                    "recraft-images" => "https://external.api.recraft.ai/v1",
                    "openai-images" or "openai-vision-review" => "https://api.openai.com/v1",
                    "xai-images" => "https://api.x.ai/v1",
                    _ => "https://provider.example/v1",
                }
            ),
            "TEST_API_KEY",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new Dictionary<string, ModelProfile>(StringComparer.Ordinal) { ["profile"] = profile }
        );
        return new ProviderExecutionContext(
            provider,
            profile,
            credential ?? Convert.ToHexString(RandomNumberGenerator.GetBytes(24)),
            10,
            1_000_000,
            1_000_000,
            "run"
        );
    }
}
