using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using AlterCourse.AssetCtl.Diagnostics;
using AlterCourse.AssetCtl.Generation;
using AlterCourse.AssetCtl.Review;
using AlterCourse.AssetCtl.Validation;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;
using ILogger = Microsoft.Extensions.Logging.ILogger;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Inspects producer classifications and rendered Serilog output using synthetic untrusted payloads only.</summary>
[Collection("Diagnostic process boundary")]
public sealed class DiagnosticPrivacyRegressionTests
{
    private const string Sentinel = "/home/synthetic-owner/private/sentinel-secret";
    private static readonly Action<ILogger, string, string, Exception?> LogFailure = LoggerMessage.Define<
        string,
        string
    >(LogLevel.Warning, new EventId(20, "ProviderFailure"), "Provider failure {Category}: {Diagnostic}");

    /// <summary>Normalizes network prose before a classified failure can enter command errors, ordinary logs, or receipts.</summary>
    [Fact]
    public async Task NetworkFailureUsesStableCategoryAndRenderedSafeContext()
    {
        using var client = new HttpClient(new NetworkFaultHandler());
        var adapter = new RecraftImageAdapter(client);
        ProviderException failure = await Assert.ThrowsAsync<ProviderException>(() =>
            adapter.GenerateAsync(
                TestData.Context(adapter.AdapterId),
                new NormalizedGenerationRequest(TestData.Request(), "prompt", 1, []),
                CancellationToken.None
            )
        );

        Assert.Equal(ProviderErrorCategory.TransientNetwork, failure.Category);
        Assert.True(failure.Retryable);
        Assert.Equal("Provider request failed at the network boundary.", failure.Message);
        var sink = new CapturingSink();
        using ILoggerFactory factory = BestEffortLoggerFactory.Create(() => CreateSerilogFactory(sink));
        LogFailure(factory.CreateLogger("AssetCtl"), failure.Category.ToString(), failure.Message, failure);

        Assert.Single(sink.Events);
        Assert.Null(sink.Events[0].Exception);
        Assert.Equal("TransientNetwork", Assert.IsType<ScalarValue>(sink.Events[0].Properties["Category"]).Value);
        Assert.DoesNotContain(Sentinel, sink.Render(), StringComparison.Ordinal);
    }

    /// <summary>Does not forward nested exceptions, stack paths, Data, or large messages to structured sinks.</summary>
    [Fact]
    public void StructuredEventsExcludeUntrustedExceptionObjects()
    {
        var sink = new CapturingSink();
        using ILoggerFactory factory = BestEffortLoggerFactory.Create(() => CreateSerilogFactory(sink));
        Action throwFailure = () =>
            throw new IOException(Sentinel + new string('x', 8192), new InvalidOperationException("sentinel-inner"));
        IOException failure = Assert.Throws<IOException>(throwFailure);
        failure.Data["private"] = "sentinel-data";
        LogFailure(factory.CreateLogger("AssetCtl"), "MalformedResponse", "invalid-provider-payload", failure);

        Assert.Null(Assert.Single(sink.Events).Exception);
        string rendered = sink.Render();
        Assert.Contains("MalformedResponse", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain(Sentinel, rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-inner", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-data", rendered, StringComparison.Ordinal);
        Assert.DoesNotContain("DiagnosticPrivacyRegressionTests.cs", rendered, StringComparison.Ordinal);
        Assert.True(rendered.Length < 1024);
    }

    /// <summary>Shares the real command route with collecting diagnostics and renders its safe command identity as JSON.</summary>
    [Theory]
    [InlineData("status")]
    [InlineData("deprecate")]
    public async Task CollectingDiagnosticsPreserveCommandResultAndMutation(string command)
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        var sink = new CapturingSink();
        string[] arguments = command is "status"
            ? ["status", "--output", "json"]
            :
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
            ];

        int exit = await Program.RunProcessAsync(arguments, (_, _) => CreateSerilogFactory(sink));

        Assert.Equal(0, exit);
        Assert.NotEmpty(fixture.Output.ToString());
        Assert.Equal(
            command is "deprecate" ? AssetLifecycle.Deprecated : AssetLifecycle.Placeholder,
            fixture.Load().Request.Lifecycle
        );
        LogEvent emitted = Assert.Single(sink.Events);
        Assert.Equal(command, Assert.IsType<ScalarValue>(emitted.Properties["Command"]).Value);
        Assert.Null(emitted.Exception);
        Assert.DoesNotContain(Sentinel, sink.Render(), StringComparison.Ordinal);
    }

    /// <summary>Bounds parser and validator findings without embedding source bytes or parser exception prose.</summary>
    [Fact]
    public void SemanticAndSvgParserFailuresExcludeSourceText()
    {
        string payload = JsonSerializer.Serialize(new { matches_subject = Sentinel });
        ProviderException semantic = Assert.Throws<ProviderException>(() => OpenAiVisionReviewer.Parse(payload));
        Assert.Equal(ProviderErrorCategory.MalformedResponse, semantic.Category);
        Assert.Equal("Invalid semantic review payload.", semantic.Message);

        string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><sentinel-secret></wrong>";
        MechanicalValidationResult mechanical = MechanicalValidator.Validate(
            TestData.Request(AssetFormat.Svg),
            Encoding.UTF8.GetBytes(svg),
            1_000_000,
            1_000_000
        );
        Assert.False(mechanical.Passed);
        Assert.Equal(["SVG parse or render failed"], mechanical.Findings);
    }

    /// <summary>Keeps real CLI schema-validation errors useful while excluding untrusted schema-evaluation details.</summary>
    [Fact]
    public async Task SchemaEvaluationFailureUsesFixedSourceContextOnStderr()
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
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

        int exit = await Program.RunProcessAsync(
            ["validate-config", "--output", "json"],
            (_, _) => Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
        );

        Assert.Equal(2, exit);
        Assert.Contains(
            "config/assets/schemas: invalid draft 2020-12 schema",
            fixture.Error.ToString(),
            StringComparison.Ordinal
        );
        Assert.DoesNotContain(Sentinel, fixture.Error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Does not forward malformed ledger contents into the operation error used by publication callers.</summary>
    [Fact]
    public void MalformedSpendLedgerPreservesClassificationWithoutParserProse()
    {
        using var fixture = new DiagnosticBoundaryRegressionTests.ProcessFixture();
        EffectiveConfiguration configuration = fixture.Configuration();
        var ledger = new FileSpendLedger(configuration);
        File.WriteAllText(
            Path.Combine(fixture.Root, configuration.Paths.StateRoot, "daily-spend.json"),
            JsonSerializer.Serialize(new { secret = Sentinel })
        );

        AssetCtlException failure = Assert.Throws<AssetCtlException>(() =>
            ledger.Reserve(new DateOnly(2026, 9, 30), 0, 0)
        );

        Assert.Equal(7, failure.ExitCode);
        Assert.Equal("daily spending ledger is invalid JSON.", failure.Message);
    }

    private static Serilog.Extensions.Logging.SerilogLoggerFactory CreateSerilogFactory(CapturingSink sink) =>
        new(new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger(), dispose: true);

    private sealed class NetworkFaultHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => throw new HttpRequestException(Sentinel, new IOException("sentinel-inner"), HttpStatusCode.BadGateway);
    }

    private sealed class CapturingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);

        public string Render()
        {
            using var output = new StringWriter(CultureInfo.InvariantCulture);
            var formatter = new JsonFormatter(renderMessage: true, formatProvider: CultureInfo.InvariantCulture);
            foreach (LogEvent logEvent in Events)
            {
                formatter.Format(logEvent, output);
            }
            return output.ToString();
        }
    }
}
