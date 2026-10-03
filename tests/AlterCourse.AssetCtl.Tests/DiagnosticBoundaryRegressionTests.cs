using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AlterCourse.AssetCtl.Tests;

/// <summary>Exercises optional diagnostic failures through real offline command dispatch and temporary catalog mutation.</summary>
[Collection("Diagnostic process boundary")]
public sealed class DiagnosticBoundaryRegressionTests
{
    /// <summary>Preserves the result and applied state when only the diagnostic boundary varies.</summary>
    [Theory]
    [InlineData("status", "none")]
    [InlineData("status", "null")]
    [InlineData("status", "constructor")]
    [InlineData("status", "create")]
    [InlineData("status", "enabled")]
    [InlineData("status", "log")]
    [InlineData("status", "dispose")]
    [InlineData("status", "constructor-cancel")]
    [InlineData("status", "create-cancel")]
    [InlineData("status", "enabled-cancel")]
    [InlineData("status", "log-cancel")]
    [InlineData("status", "dispose-cancel")]
    [InlineData("deprecate", "none")]
    [InlineData("deprecate", "null")]
    [InlineData("deprecate", "constructor")]
    [InlineData("deprecate", "create")]
    [InlineData("deprecate", "enabled")]
    [InlineData("deprecate", "log")]
    [InlineData("deprecate", "dispose")]
    [InlineData("deprecate", "constructor-cancel")]
    [InlineData("deprecate", "create-cancel")]
    [InlineData("deprecate", "enabled-cancel")]
    [InlineData("deprecate", "log-cancel")]
    [InlineData("deprecate", "dispose-cancel")]
    public async Task DiagnosticFaultPreservesCommandOutcomeAndState(string command, string fault)
    {
        using var fixture = new ProcessFixture();
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

        int exit = await Program.RunProcessAsync(arguments, (_, _) => CreateFactory(fault));

        Assert.Equal(
            command is "deprecate" ? AssetLifecycle.Deprecated : AssetLifecycle.Placeholder,
            fixture.Load().Request.Lifecycle
        );
        Assert.NotEmpty(fixture.Output.ToString());
        Assert.Equal(0, exit);
        Assert.False(Directory.Exists(Path.Combine(fixture.Root, ".assetctl", "runs")));
        Assert.False(File.Exists(Path.Combine(fixture.Root, ".assetctl", "state", "daily-spend.json")));
    }

    /// <summary>Keeps the original pre-commit refusal classification even when diagnostic teardown also fails.</summary>
    [Theory]
    [InlineData("none")]
    [InlineData("constructor")]
    [InlineData("create")]
    [InlineData("enabled")]
    [InlineData("log")]
    [InlineData("dispose")]
    [InlineData("dispose-cancel")]
    public async Task DiagnosticFaultPreservesOriginalOperationRefusal(string fault)
    {
        using var fixture = new ProcessFixture();
        byte[] before = await File.ReadAllBytesAsync(fixture.ManifestPath);

        int exit = await Program.RunProcessAsync(
            ["deprecate", "--asset-id", fixture.AssetId, "--actor", "test"],
            (_, _) => CreateFactory(fault)
        );

        Assert.Equal(before, await File.ReadAllBytesAsync(fixture.ManifestPath));
        Assert.Equal(2, exit);
        Assert.Contains("--reason", fixture.Error.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Inspects actual rendered fallback output rather than treating regex sanitization as a safety proof.</summary>
    [Theory]
    [InlineData("/home/synthetic-owner/private/sentinel-home")]
    [InlineData("unlabelled-sentinel-credential")]
    [InlineData("{\"api_key\":\"sentinel-quoted-secret\"}")]
    [InlineData("download failed at https://example.invalid/a?signature=sentinel-signed-secret then retry")]
    [InlineData("sentinel-large-message")]
    public async Task ConstructionFallbackDoesNotRenderUntrustedExceptionText(string sentinel)
    {
        using var fixture = new ProcessFixture();
        var exception = new IOException(
            sentinel + new string('x', 8192),
            new InvalidOperationException("sentinel-inner-secret")
        );
        exception.Data["private"] = "sentinel-data-secret";

        int exit = await Program.RunProcessAsync(
            ["status", "--output", "json"],
            (repository, logRoot) => Program.CreateLoggerFactory(repository, logRoot, _ => throw exception)
        );

        Assert.Equal(0, exit);
        Assert.DoesNotContain(sentinel, fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-inner-secret", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("sentinel-data-secret", fixture.Error.ToString(), StringComparison.Ordinal);
        Assert.True(fixture.Error.ToString().Length < 1024);
    }

    /// <summary>A failing diagnostic fallback stream must not prevent the independent required stdout result.</summary>
    [Theory]
    [InlineData("status")]
    [InlineData("deprecate")]
    public async Task ConstructionFallbackStderrFaultPreservesCommand(string command)
    {
        using var fixture = new ProcessFixture();
        var writer = new FallbackFaultWriter();
        Console.SetError(writer);

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
        int exit = await Program.RunProcessAsync(
            arguments,
            (repository, logRoot) =>
                Program.CreateLoggerFactory(repository, logRoot, _ => throw new IOException("sink unavailable"))
        );

        Assert.Equal(0, exit);
        Assert.NotEmpty(fixture.Output.ToString());
        Assert.Equal(
            command is "deprecate" ? AssetLifecycle.Deprecated : AssetLifecycle.Placeholder,
            fixture.Load().Request.Lifecycle
        );
        Assert.True(writer.WriteAttempts > 0);
    }

    /// <summary>Preserves the original operation refusal when both diagnostic teardown and error reporting fail.</summary>
    [Fact]
    public async Task StderrAndDisposalFaultsPreserveOriginalOperationRefusal()
    {
        using var fixture = new ProcessFixture();
        var writer = new FallbackFaultWriter();
        Console.SetError(writer);
        byte[] before = await File.ReadAllBytesAsync(fixture.ManifestPath);

        int exit = await Program.RunProcessAsync(
            ["deprecate", "--asset-id", fixture.AssetId, "--actor", "test"],
            (_, _) => CreateFactory("dispose")
        );

        Assert.Equal(2, exit);
        Assert.Equal(before, await File.ReadAllBytesAsync(fixture.ManifestPath));
        Assert.True(writer.WriteAttempts > 0);
    }

    private static ILoggerFactory CreateFactory(string fault) =>
        fault is "null" ? Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance
        : fault.StartsWith("constructor", StringComparison.Ordinal) ? throw DiagnosticFault(fault)
        : new FaultFactory(fault);

    private static Exception DiagnosticFault(string fault) =>
        fault.EndsWith("-cancel", StringComparison.Ordinal)
            ? new OperationCanceledException("diagnostic cancellation")
            : new IOException("diagnostic fault");

    private sealed class FaultFactory(string fault) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) =>
            fault.StartsWith("create", StringComparison.Ordinal)
                ? throw DiagnosticFault(fault)
                : new FaultLogger(fault);

        public void AddProvider(ILoggerProvider provider) => throw new NotSupportedException();

        public void Dispose()
        {
            if (fault.StartsWith("dispose", StringComparison.Ordinal))
            {
                throw DiagnosticFault(fault);
            }
        }
    }

    private sealed class FaultLogger(string fault) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) =>
            fault.StartsWith("enabled", StringComparison.Ordinal) ? throw DiagnosticFault(fault) : true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            if (fault.StartsWith("log", StringComparison.Ordinal))
            {
                throw DiagnosticFault(fault);
            }
        }
    }

    private sealed class FallbackFaultWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public int WriteAttempts { get; private set; }

        public override void WriteLine(string? value)
        {
            WriteAttempts++;
            throw new IOException("fallback stderr unavailable");
        }
    }

    internal sealed class ProcessFixture : IDisposable
    {
        private readonly string _previousDirectory = Environment.CurrentDirectory;
        private readonly TextWriter _previousOutput = Console.Out;
        private readonly TextWriter _previousError = Console.Error;

        public ProcessFixture()
        {
            string source = AlterCourse.AssetCtl.Cli.CliTypes.RepositoryLocator.Find(_previousDirectory);
            CopyTree(Path.Combine(source, "config", "assets"), Path.Combine(Root, "config", "assets"));
            File.WriteAllText(Path.Combine(Root, "AlterCourse.sln"), "");
            Directory.Delete(Path.Combine(Root, "config", "assets", "catalog"), recursive: true);
            Directory.CreateDirectory(Path.GetDirectoryName(ManifestPath)!);
            Directory.CreateDirectory(Path.Combine(Root, "src", "AlterCourse.Godot", "assets"));
            var manifest = new AssetManifest(
                "1",
                TestData.Request(),
                1,
                new RightsRecord("original-project-created", "project", null, null, "test"),
                null,
                null,
                null,
                null,
                new ApprovalRecord(null, null, null),
                null,
                "config/assets/catalog/diagnostic.asset.yaml"
            );
            File.WriteAllText(ManifestPath, ManifestStore.Serialize(manifest));
            Environment.CurrentDirectory = Root;
            Console.SetOut(Output);
            Console.SetError(Error);
        }

        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), "assetctl-diagnostics-" + Guid.NewGuid().ToString("N"));
        public string AssetId { get; } = TestData.Request().Id;
        public string ManifestPath => Path.Combine(Root, "config", "assets", "catalog", "diagnostic.asset.yaml");
        public StringWriter Output { get; } = new(CultureInfo.InvariantCulture);
        public StringWriter Error { get; } = new(CultureInfo.InvariantCulture);

        public AssetManifest Load() =>
            ManifestStore.Load(Configuration(), "config/assets/catalog/diagnostic.asset.yaml");

        public EffectiveConfiguration Configuration()
        {
            using var client = new HttpClient();
            var registry = new AdapterRegistry([
                new AlterCourse.AssetCtl.Generation.LocalPlaceholderGenerator(),
                new AlterCourse.AssetCtl.Providers.ProviderAdapters.RecraftImageAdapter(client),
                new AlterCourse.AssetCtl.Providers.ProviderAdapters.OpenAiImageAdapter(client),
                new AlterCourse.AssetCtl.Providers.ProviderAdapters.XaiImageAdapter(client),
                new AlterCourse.AssetCtl.Review.OpenAiVisionReviewer(client),
            ]);
            return new ConfigurationLoader(registry.Descriptors).Load(Root);
        }

        public void Dispose()
        {
            Console.SetOut(_previousOutput);
            Console.SetError(_previousError);
            Environment.CurrentDirectory = _previousDirectory;
            Output.Dispose();
            Error.Dispose();
            Directory.Delete(Root, recursive: true);
        }

        private static void CopyTree(string source, string target)
        {
            Directory.CreateDirectory(target);
            foreach (string file in Directory.EnumerateFiles(source))
            {
                File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
            }
            foreach (string directory in Directory.EnumerateDirectories(source))
            {
                CopyTree(directory, Path.Combine(target, Path.GetFileName(directory)));
            }
        }
    }
}
