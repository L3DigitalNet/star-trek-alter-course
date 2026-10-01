using System.Globalization;
using AlterCourse.AssetCtl.Cli;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Review;
using AlterCourse.AssetCtl.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using YamlDotNet.RepresentationModel;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Proves offline configuration loading reaches selected-byte admission through CommandApp.RunAsync.</summary>
[Collection("Selected asset dispatch")]
public sealed class SelectedAssetDispatchTests
{
    /// <summary>One-byte excess is refused by full offline dispatch without creating runtime state or mutating a manifest.</summary>
    [Fact]
    public async Task OfflineValidateConfigDispatchRejectsOversizedSelectionWithoutWrites()
    {
        string originalDirectory = Environment.CurrentDirectory;
        string sourceRoot = CliTypes.RepositoryLocator.Find(originalDirectory);
        using var fixture = new LifecycleBoundaryFixture(new byte[9], maximumBytes: 8);
        PrepareConfiguration(sourceRoot, fixture);
        string predecessor = await File.ReadAllTextAsync(fixture.ManifestPath);
        using var client = new HttpClient(new OfflineHandler());
        var registry = new AdapterRegistry([
            new LocalPlaceholderGenerator(),
            new RecraftImageAdapter(client),
            new OpenAiImageAdapter(client),
            new XaiImageAdapter(client),
            new OpenAiVisionReviewer(client),
        ]);
        var router = new AssetRouter(registry);
        var app = new CliTypes.CommandApp(
            new ConfigurationLoader(registry.Descriptors),
            router,
            new GenerationOrchestrator(registry, router),
            NullLogger<CliTypes.CommandApp>.Instance
        );
        try
        {
            Environment.CurrentDirectory = fixture.Root;
            AssetCtlException failure = await Assert.ThrowsAsync<AssetCtlException>(() =>
                app.RunAsync(["validate-config", "--offline", "--output", "json"], CancellationToken.None)
            );

            Assert.Equal(1, failure.ExitCode);
            Assert.Contains("byte limit", failure.Message, StringComparison.Ordinal);
            Assert.Equal(predecessor, await File.ReadAllTextAsync(fixture.ManifestPath));
            Assert.False(Directory.Exists(Path.Combine(fixture.Root, fixture.Configuration.Paths.StateRoot)));
            Assert.False(Directory.Exists(Path.Combine(fixture.Root, ".assetctl")));
        }
        finally
        {
            Environment.CurrentDirectory = originalDirectory;
        }
    }

    private static void PrepareConfiguration(string sourceRoot, LifecycleBoundaryFixture fixture)
    {
        string source = Path.Combine(sourceRoot, "config", "assets");
        string target = Path.Combine(fixture.Root, "config", "assets");
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(fixture.Root, "AlterCourse.sln"), "fixture repository marker");
        foreach (string path in Directory.EnumerateFiles(source, "*.yaml"))
        {
            File.Copy(path, Path.Combine(target, Path.GetFileName(path)));
        }

        CopyFiles(Path.Combine(source, "styles"), Path.Combine(fixture.Root, fixture.Configuration.Paths.StyleRoot));
        CopyFiles(Path.Combine(source, "schemas"), Path.Combine(target, "schemas"));
        string configurationPath = Path.Combine(target, "assetctl.yaml");
        YamlMappingNode root = StrictYaml.LoadMapping(configurationPath);
        var paths = (YamlMappingNode)root.Children[new YamlScalarNode("paths")];
        Set(paths, "godot_asset_root", fixture.Configuration.Paths.GodotAssetRoot);
        Set(paths, "catalog_root", fixture.Configuration.Paths.CatalogRoot);
        Set(paths, "style_root", fixture.Configuration.Paths.StyleRoot);
        Set(paths, "work_root", fixture.Configuration.Paths.WorkRoot);
        Set(paths, "receipt_root", fixture.Configuration.Paths.ReceiptRoot);
        Set(paths, "state_root", fixture.Configuration.Paths.StateRoot);
        Set(paths, "log_root", fixture.Configuration.Paths.LogRoot);
        var limits = (YamlMappingNode)root.Children[new YamlScalarNode("limits")];
        Set(
            limits,
            "maximum_download_bytes",
            fixture.Configuration.Limits.MaximumDownloadBytes.ToString(CultureInfo.InvariantCulture)
        );
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new YamlStream(new YamlDocument(root)).Save(writer, assignAnchors: false);
        File.WriteAllText(configurationPath, writer.ToString());
    }

    private static void CopyFiles(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (string path in Directory.EnumerateFiles(source))
        {
            File.Copy(path, Path.Combine(target, Path.GetFileName(path)));
        }
    }

    private static void Set(YamlMappingNode node, string name, string value) =>
        node.Children[new YamlScalarNode(name)] = new YamlScalarNode(value);

    private sealed class OfflineHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw new InvalidOperationException("Offline command attempted HTTP.");
    }
}
