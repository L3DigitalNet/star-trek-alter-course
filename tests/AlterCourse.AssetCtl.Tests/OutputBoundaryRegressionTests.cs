using System.Text;
using System.Text.Json;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Review;
using AlterCourse.AssetCtl.Routing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Exercises direct command diagnostics and required stdout across real offline commit boundaries.</summary>
[Collection("Diagnostic process boundary")]
public sealed class OutputBoundaryRegressionTests
{
    private const string Sentinel = "/home/synthetic-owner/private/sentinel-output-secret";

    /// <summary>Directly injected diagnostics must not become command admission or mutation authority.</summary>
    [Theory]
    [InlineData("status", "none")]
    [InlineData("status", "enabled")]
    [InlineData("status", "log")]
    [InlineData("deprecate", "none")]
    [InlineData("deprecate", "enabled")]
    [InlineData("deprecate", "log")]
    public async Task DirectLoggerFaultPreservesCommandOutcome(string command, string fault)
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        using var client = new HttpClient();
        var logger = new DirectLogger(fault);
        AlterCourse.AssetCtl.Cli.CliTypes.CommandApp app = CreateApp(client, logger);

        int exit = await app.RunAsync(Arguments(command, fixture), CancellationToken.None);

        Assert.Equal(0, exit);
        Assert.Equal(
            command is "deprecate" ? AssetLifecycle.Deprecated : AssetLifecycle.Placeholder,
            fixture.Load().Request.Lifecycle
        );
        Assert.NotEmpty(fixture.Output.ToString());
    }

    /// <summary>Rejects unknown command content before it can enter a structured command event.</summary>
    [Fact]
    public async Task UnknownCommandDoesNotLogOrRenderUntrustedIdentity()
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        using var client = new HttpClient();
        var logger = new DirectLogger("none");
        AlterCourse.AssetCtl.Cli.CliTypes.CommandApp app = CreateApp(client, logger);

        AssetCtlException failure = await Assert.ThrowsAsync<AssetCtlException>(() =>
            app.RunAsync([Sentinel], CancellationToken.None)
        );

        Assert.Equal(2, failure.ExitCode);
        Assert.Empty(logger.Rendered);
        Assert.DoesNotContain(Sentinel, failure.Message, StringComparison.Ordinal);
    }

    /// <summary>Reports applied lifecycle/publication state truthfully when the independent required result stream fails.</summary>
    [Theory]
    [InlineData("deprecate")]
    [InlineData("generate")]
    public async Task RequiredStdoutFailureAfterCommitPreservesAppliedOutcome(string command)
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        var writer = new BrokenOutput();
        Console.SetOut(writer);

        int exit = await Program.RunProcessAsync(Arguments(command, fixture), (_, _) => NullLoggerFactory.Instance);

        AssetManifest current = fixture.Load();
        Assert.Equal(0, exit);
        Assert.True(writer.Attempts > 0);
        Assert.Contains("committed", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.Contains("reporting-degraded", fixture.Error.ToString(), StringComparison.Ordinal);
        if (command is "deprecate")
        {
            Assert.Equal(AssetLifecycle.Deprecated, current.Request.Lifecycle);
        }
        else
        {
            Assert.NotNull(current.Integrity);
            Assert.True(File.Exists(Path.Combine(fixture.Root, current.Request.Output.Path)));
        }
    }

    /// <summary>A failed result stream for a read-only command remains a reporting failure without an applied-mutation claim.</summary>
    [Fact]
    public async Task ReadOnlyStdoutFailureHasUnappliedReportingClassification()
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        var writer = new BrokenOutput();
        Console.SetOut(writer);
        byte[] before = await File.ReadAllBytesAsync(fixture.ManifestPath);

        int exit = await Program.RunProcessAsync(["status", "--output", "json"], (_, _) => NullLoggerFactory.Instance);

        Assert.Equal(1, exit);
        Assert.True(writer.Attempts > 0);
        Assert.Equal(before, await File.ReadAllBytesAsync(fixture.ManifestPath));
        Assert.Contains("result-output-unavailable", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("committed", fixture.Error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Refusal occurs before any result write and retains the original classification despite a broken stdout.</summary>
    [Fact]
    public async Task OperationRefusalPrecedesStdoutAndPreservesState()
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        var writer = new BrokenOutput();
        Console.SetOut(writer);
        byte[] before = await File.ReadAllBytesAsync(fixture.ManifestPath);

        int exit = await Program.RunProcessAsync(
            ["deprecate", "--asset-id", fixture.AssetId, "--actor", "test"],
            (_, _) => NullLoggerFactory.Instance
        );

        Assert.Equal(2, exit);
        Assert.Equal(0, writer.Attempts);
        Assert.Equal(before, await File.ReadAllBytesAsync(fixture.ManifestPath));
    }

    /// <summary>Help remains valid when optional log configuration discovery encounters a missing file.</summary>
    [Fact]
    public async Task HelpSurvivesOptionalLogConfigurationIoFailure()
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        File.Delete(Path.Combine(fixture.Root, "config", "assets", "providers.yaml"));

        int exit = await Program.RunProcessAsync(["help"], (_, _) => NullLoggerFactory.Instance);

        Assert.Equal(0, exit);
        Assert.Contains("Commands:", fixture.Output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Inspects real error-channel output for unknown arguments, keys, and parser-source text.</summary>
    [Theory]
    [InlineData("argument")]
    [InlineData("option")]
    [InlineData("yaml-key")]
    [InlineData("yaml-parser")]
    public async Task UntrustedInputIsNotCopiedToStderr(string kind)
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        string[] arguments = ["status", "--output", "json"];
        if (kind is "argument")
        {
            arguments = ["status", Sentinel];
        }
        else if (kind is "option")
        {
            arguments = ["status", "--" + Sentinel];
        }
        else
        {
            string path = Path.Combine(fixture.Root, "config", "assets", "assetctl.yaml");
            string addition = kind is "yaml-key"
                ? JsonSerializer.Serialize(Sentinel) + ": true\n"
                : "broken: [\"" + Sentinel + "\"\n";
            await File.AppendAllTextAsync(path, addition);
        }

        int exit = await Program.RunProcessAsync(arguments, (_, _) => NullLoggerFactory.Instance);

        Assert.Equal(2, exit);
        Assert.DoesNotContain(Sentinel, fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Root, fixture.Error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Exposes the real schema failure before the outer process catch can replace its classification.</summary>
    [Fact]
    public async Task InvalidSchemaUsesExpectedApplicationRefusalThroughDirectDispatch()
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        using var client = new HttpClient();
        string schema = JsonSerializer.Serialize(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
                ["type"] = Sentinel,
            }
        );
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Root, "config", "assets", "schemas", "diagnostic.json"),
            schema
        );
        AlterCourse.AssetCtl.Cli.CliTypes.CommandApp app = CreateApp(
            client,
            NullLogger<AlterCourse.AssetCtl.Cli.CliTypes.CommandApp>.Instance
        );

        AssetCtlException failure = await Assert.ThrowsAsync<AssetCtlException>(() =>
            app.RunAsync(["validate-config", "--output", "json"], CancellationToken.None)
        );

        Assert.Equal(2, failure.ExitCode);
    }

    private static string[] Arguments(string command, DiagnosticBoundaryRegressionTests.ProcessFixture fixture) =>
        command switch
        {
            "status" => ["status", "--output", "json"],
            "generate" => ["generate", "--output", "json", "--asset-id", fixture.AssetId, "--offline"],
            _ =>
            [
                "deprecate",
                "--output",
                "json",
                "--asset-id",
                fixture.AssetId,
                "--actor",
                "test",
                "--reason",
                "retired",
            ],
        };

    private static AlterCourse.AssetCtl.Cli.CliTypes.CommandApp CreateApp(
        HttpClient client,
        ILogger<AlterCourse.AssetCtl.Cli.CliTypes.CommandApp> logger
    )
    {
        var registry = new AdapterRegistry([
            new LocalPlaceholderGenerator(),
            new RecraftImageAdapter(client),
            new OpenAiImageAdapter(client),
            new XaiImageAdapter(client),
            new OpenAiVisionReviewer(client),
        ]);
        var router = new AssetRouter(registry);
        return new(
            new ConfigurationLoader(registry.Descriptors),
            router,
            new GenerationOrchestrator(registry, router),
            logger
        );
    }

    private sealed class DirectLogger(string fault) : ILogger<AlterCourse.AssetCtl.Cli.CliTypes.CommandApp>
    {
        public List<string> Rendered { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            fault is "enabled" ? throw new IOException("synthetic logger fault") : true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (fault is "log")
            {
                throw new IOException("synthetic logger fault");
            }
            Rendered.Add(formatter(state, exception));
        }
    }

    private sealed class BrokenOutput : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;
        public int Attempts { get; private set; }

        public override void WriteLine(string? value)
        {
            Attempts++;
            throw new IOException(Sentinel);
        }
    }
}
