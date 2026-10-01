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
    [InlineData("status", "cancel-enabled")]
    [InlineData("status", "cancel-log")]
    [InlineData("deprecate", "none")]
    [InlineData("deprecate", "enabled")]
    [InlineData("deprecate", "log")]
    [InlineData("deprecate", "cancel-enabled")]
    [InlineData("deprecate", "cancel-log")]
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
    [InlineData("approve")]
    public async Task RequiredStdoutFailureAfterCommitPreservesAppliedOutcome(string command)
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        if (command is "approve")
        {
            using var candidate = new LifecycleBoundaryFixture();
            AssetRequest request = TestData.Request(AssetFormat.Svg) with { Lifecycle = AssetLifecycle.Candidate };
            AssetManifest manifest = fixture.Load() with
            {
                Request = request,
                Integrity = candidate.Manifest.Integrity,
            };
            string selected = Path.Combine(fixture.Root, request.Output.Path);
            Directory.CreateDirectory(Path.GetDirectoryName(selected)!);
            await File.WriteAllBytesAsync(selected, candidate.Bytes);
            await File.WriteAllTextAsync(fixture.ManifestPath, ManifestStore.Serialize(manifest));
        }
        var writer = new BrokenOutput();
        Console.SetOut(writer);

        int exit = await Program.RunProcessAsync(Arguments(command, fixture), (_, _) => NullLoggerFactory.Instance);

        AssetManifest current = fixture.Load();
        Assert.Equal(9, exit);
        Assert.True(writer.Attempts > 0);
        Assert.Contains("committed", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.Contains("reporting-degraded", fixture.Error.ToString(), StringComparison.Ordinal);
        if (command is "deprecate")
        {
            Assert.Equal(AssetLifecycle.Deprecated, current.Request.Lifecycle);
        }
        else if (command is "approve")
        {
            Assert.Equal(AssetLifecycle.Approved, current.Request.Lifecycle);
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

        Assert.Equal(9, exit);
        Assert.True(writer.Attempts > 0);
        Assert.Equal(before, await File.ReadAllBytesAsync(fixture.ManifestPath));
        Assert.Contains("result-output-unavailable", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("committed", fixture.Error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Two unavailable receipt sinks cannot make installed publication look rolled back or fully reported.</summary>
    [Fact]
    public async Task ReceiptFailureAfterPublicationReportsCommitWithoutCommandCompletionClaim()
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        using var client = new HttpClient();
        AdapterRegistry registry = CreateRegistry(client);
        EffectiveConfiguration configuration = new ConfigurationLoader(registry.Descriptors).Load(fixture.Root);
        string receipts = Path.Combine(fixture.Root, configuration.Paths.ReceiptRoot);
        string work = Path.Combine(fixture.Root, configuration.Paths.WorkRoot);
        Directory.CreateDirectory(Path.GetDirectoryName(receipts)!);
        Directory.CreateDirectory(work);
        await File.WriteAllTextAsync(receipts, "blocked");
        await File.WriteAllTextAsync(Path.Combine(work, "receipt-fallback"), "blocked");

        int exit = await Program.RunProcessAsync(Arguments("generate", fixture), (_, _) => NullLoggerFactory.Instance);

        Assert.Equal(9, exit);
        Assert.NotNull(fixture.Load().Integrity);
        Assert.Contains(
            "mutation committed; receipt-output-unavailable",
            fixture.Error.ToString(),
            StringComparison.Ordinal
        );
        Assert.DoesNotContain("command completed", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.Empty(fixture.Output.ToString());
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

    /// <summary>Help remains valid when optional log configuration discovery encounters an unreadable file entry.</summary>
    [Fact]
    public async Task HelpSurvivesOptionalLogConfigurationIoFailure()
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        string providerPath = Path.Combine(fixture.Root, "config", "assets", "providers.yaml");
        File.Delete(providerPath);
        Directory.CreateDirectory(providerPath);

        int exit = await Program.RunProcessAsync(["help"], (_, _) => NullLoggerFactory.Instance);

        Assert.Equal(0, exit);
        Assert.Contains("Commands:", fixture.Output.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Inspects real error-channel output for unknown arguments, keys, and parser-source text.</summary>
    [Theory]
    [MemberData(nameof(UntrustedInputs))]
    public async Task UntrustedInputIsNotCopiedToStderr(string kind, string untrusted)
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        string[] arguments = ["status", "--output", "json"];
        if (kind is "argument")
        {
            arguments = ["status", untrusted];
        }
        else if (kind is "option")
        {
            arguments = ["status", "--" + untrusted];
        }
        else
        {
            string path = Path.Combine(fixture.Root, "config", "assets", "assetctl.yaml");
            string addition = kind is "yaml-key"
                ? JsonSerializer.Serialize(untrusted) + ": true\n"
                : "broken: [" + JsonSerializer.Serialize(untrusted) + "\n";
            await File.AppendAllTextAsync(path, addition);
        }

        int exit = await Program.RunProcessAsync(arguments, (_, _) => NullLoggerFactory.Instance);

        Assert.Equal(2, exit);
        Assert.DoesNotContain("sentinel", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(fixture.Root, fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.True(fixture.Error.ToString().Length < 512);
    }

    /// <summary>Supplies synthetic path, credential, URL, and large-message payloads at each raw-input producer.</summary>
    public static TheoryData<string, string> UntrustedInputs
    {
        get
        {
            var values = new TheoryData<string, string>();
            foreach (string kind in new[] { "argument", "option", "yaml-key", "yaml-parser" })
            {
                foreach (
                    string payload in new[]
                    {
                        Sentinel,
                        "sentinel-plain-credential",
                        "api_key='sentinel-quoted-credential'",
                        "api_key=sentinel-raw-credential",
                        "nested https://synthetic.invalid/image?token=sentinel-signed-secret text",
                        "inner stack Data " + new string('x', 10_000) + "sentinel-large-message",
                    }
                )
                {
                    values.Add(kind, payload);
                }
            }
            return values;
        }
    }

    /// <summary>Repeated real process validation cannot mutate shared schema registration authority.</summary>
    [Fact]
    public async Task RepeatedReadOnlyProcessValidationUsesIndependentSchemaRegistries()
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        for (int invocation = 0; invocation < 2; invocation++)
        {
            int exit = await Program.RunProcessAsync(
                ["validate-config", "--output", "json"],
                (_, _) => NullLoggerFactory.Instance
            );
            Assert.Equal(0, exit);
        }
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
            "approve" =>
            [
                "approve",
                "--output",
                "json",
                "--asset-id",
                fixture.AssetId,
                "--approved-by",
                "test",
                "--approval-note",
                "reviewed",
                "--confirm-approved-asset",
                fixture.AssetId,
            ],
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
        AdapterRegistry registry = CreateRegistry(client);
        var router = new AssetRouter(registry);
        return new(
            new ConfigurationLoader(registry.Descriptors),
            router,
            new GenerationOrchestrator(registry, router),
            logger
        );
    }

    private static AdapterRegistry CreateRegistry(HttpClient client) =>
        new([
            new LocalPlaceholderGenerator(),
            new RecraftImageAdapter(client),
            new OpenAiImageAdapter(client),
            new XaiImageAdapter(client),
            new OpenAiVisionReviewer(client),
        ]);

    private sealed class DirectLogger(string fault) : ILogger<AlterCourse.AssetCtl.Cli.CliTypes.CommandApp>
    {
        public List<string> Rendered { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            fault switch
            {
                "enabled" => throw new IOException("synthetic logger fault"),
                "cancel-enabled" => throw new OperationCanceledException("synthetic diagnostic cancellation"),
                _ => true,
            };

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
            if (fault is "cancel-log")
            {
                throw new OperationCanceledException("synthetic diagnostic cancellation");
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
