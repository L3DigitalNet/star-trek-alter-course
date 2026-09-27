using System.Globalization;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Tests.Support;
using AlterCourse.Godot.Gameplay.Logging;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using Serilog.Formatting.Json;

namespace AlterCourse.Core.Tests;

/// <summary>Verifies rendered and structured diagnostic privacy and optional-provider isolation.</summary>
public sealed class GameplayLoggingTests
{
    private const string Sensitive = "SYNTHETIC_SECRET /home/private/save-payload.json";

    /// <summary>Checks the actual backend representation excludes exception messages, inner exceptions, and payloads.</summary>
    [Fact]
    public void RenderedAndStructuredFailuresExcludeUntrustedExceptions()
    {
        var sink = new CapturingSink();
        using Logger backend = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        using var factory = new Serilog.Extensions.Logging.SerilogLoggerFactory(backend, dispose: false);
        var diagnostics = new GameDiagnostics(factory.CreateLogger<GameDiagnostics>(), () => { });
        diagnostics.Failure(
            GameDiagnostics.FailureOperation.Load,
            new GamePersistenceException(
                GamePersistenceFailure.InvalidData,
                Sensitive,
                Sensitive,
                new IOException(Sensitive)
            ),
            42,
            1
        );
        LogEvent value = Assert.Single(sink.Events);
        Assert.Null(value.Exception);
        Assert.Equal(
            "InvalidExternalInput",
            Assert.IsType<ScalarValue>(value.Properties["FailureClassification"]).Value
        );
        Assert.DoesNotContain(Sensitive, value.RenderMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        new JsonFormatter(renderMessage: true).Format(value, writer);
        Assert.DoesNotContain("SYNTHETIC_SECRET", writer.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("/home/private", writer.ToString(), StringComparison.Ordinal);
        Assert.Contains("SimulationTimeMilliseconds", writer.ToString(), StringComparison.Ordinal);
    }

    /// <summary>Retains each persistence failure category while excluding the original exception.</summary>
    [Theory]
    [InlineData(GamePersistenceFailure.InvalidData, "InvalidExternalInput")]
    [InlineData(GamePersistenceFailure.UnsupportedVersion, "UnsupportedVersion")]
    [InlineData(GamePersistenceFailure.InputOutput, "RecoverableIO")]
    [InlineData(GamePersistenceFailure.IncompatibleContent, "IncompatibleContent")]
    public void PersistenceFailureCategoriesRemainDistinct(GamePersistenceFailure failure, string classification)
    {
        var sink = new CapturingSink();
        using Logger backend = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        using var factory = new Serilog.Extensions.Logging.SerilogLoggerFactory(backend, dispose: false);
        var diagnostics = new GameDiagnostics(factory.CreateLogger<GameDiagnostics>(), () => { });
        diagnostics.Failure(
            GameDiagnostics.FailureOperation.Load,
            new GamePersistenceException(failure, Sensitive, Sensitive),
            42,
            1
        );
        LogEvent value = Assert.Single(sink.Events);
        Assert.Equal(classification, Assert.IsType<ScalarValue>(value.Properties["FailureClassification"]).Value);
        Assert.Null(value.Exception);
    }

    /// <summary>Proves a failing logger and fallback cannot escape the diagnostic operation.</summary>
    [Fact]
    public void ThrowingLoggerAndFallbackRemainOptional()
    {
        int fallbackCount = 0;
        var provider = new ThrowingLogger<GameDiagnostics>();
        var diagnostics = new GameDiagnostics(
            provider,
            () =>
            {
                fallbackCount++;
                throw new IOException(Sensitive);
            }
        );
        diagnostics.Failure(
            GameDiagnostics.FailureOperation.Content,
            new InvalidOperationException(Sensitive),
            null,
            null
        );
        diagnostics.Failure(
            GameDiagnostics.FailureOperation.Simulation,
            new InvalidOperationException(Sensitive),
            42,
            1
        );
        Assert.Equal(1, fallbackCount);
        Assert.Equal(2, provider.Rendered.Count);
        Assert.All(
            provider.Rendered,
            rendered => Assert.DoesNotContain("SYNTHETIC_SECRET", rendered, StringComparison.Ordinal)
        );
        Assert.All(provider.Exceptions, Assert.Null);
    }

    /// <summary>Checks path, factory, provider creation, and cleanup failure boundaries.</summary>
    [Fact]
    public void StartupAndShutdownFailuresRemainOptional()
    {
        int failures = 0;
        using (GameplayLogging.Create(() => throw new IOException(Sensitive), () => failures++)) { }
        using (GameplayLogging.Create(() => "unused", () => failures++, _ => throw new IOException(Sensitive))) { }
        var logging = GameplayLogging.Create(() => "unused", () => failures++, _ => new ThrowingFactory());
        logging.Diagnostics.Lifecycle(true, null, null);
        logging.Dispose();
        logging.Dispose();
        Assert.Equal(5, failures);
    }

    /// <summary>Confirms Serilog suppresses ordinary sink failures and does not change simulation results.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SimulationStateAndPersistenceIgnoreProviderFailure(bool failingSink)
    {
        var sink = new CapturingSink { Fail = failingSink };
        using Logger backend = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        using var factory = new Serilog.Extensions.Logging.SerilogLoggerFactory(backend, dispose: false);
        AlterCourse.Core.Content.ShipDefinitionCatalog catalog = TestShipContent.Pathfinder();
        GameSimulation plain = FirstGameSetup.Create(catalog);
        GameSimulation logged = FirstGameSetup.Create(catalog, factory.CreateLogger<GameSimulation>());
        GameSimulation failing = FirstGameSetup.Create(catalog, new ThrowingLogger<GameSimulation>());
        GameSimulation noop = FirstGameSetup.Create(catalog, NullLogger<GameSimulation>.Instance);
        foreach (GameSimulation simulation in new[] { plain, logged, failing, noop })
        {
            simulation.AdvanceFixedSteps(20);
        }
        var metadata = new GameSaveMetadata("safe", "safe", DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch);
        byte[] expected = GamePersistence.Serialize(plain, metadata);
        Assert.Equal(expected, GamePersistence.Serialize(logged, metadata));
        Assert.Equal(expected, GamePersistence.Serialize(failing, metadata));
        Assert.Equal(expected, GamePersistence.Serialize(noop, metadata));
        Assert.NotEmpty(sink.Events);
        Assert.Contains(sink.Events, value => value.Properties.ContainsKey("SelectedAction"));
        Assert.Contains(sink.Events, value => value.Properties.ContainsKey("RankingMetric"));
        Assert.Contains(sink.Events, value => value.Properties.ContainsKey("Constraint"));
    }

    /// <summary>Checks the real rolling file emits safe JSON and releases its file handle on shutdown.</summary>
    [Fact]
    public void DefaultFileBackendProducesSafeStructuredOutputAndCloses()
    {
        string directory = Path.Combine(Path.GetTempPath(), "altercourse-logging-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (var logging = GameplayLogging.Create(() => directory, () => { }))
            {
                logging.Diagnostics.Failure(
                    GameDiagnostics.FailureOperation.Content,
                    new IOException(Sensitive),
                    42,
                    1
                );
            }
            string path = Assert.Single(Directory.GetFiles(directory));
            string json = File.ReadAllText(path);
            Assert.Contains("SessionCorrelation", json, StringComparison.Ordinal);
            Assert.Contains("RecoverableIO", json, StringComparison.Ordinal);
            Assert.DoesNotContain("SYNTHETIC_SECRET", json, StringComparison.Ordinal);
            Assert.DoesNotContain("/home/private", json, StringComparison.Ordinal);
            File.Delete(path);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private sealed class CapturingSink : ILogEventSink
    {
        internal List<LogEvent> Events { get; } = [];
        internal bool Fail { get; init; }

        public void Emit(LogEvent logEvent)
        {
            Events.Add(logEvent);
            if (Fail)
            {
                throw new IOException(Sensitive);
            }
        }
    }

    private sealed class ThrowingFactory : ILoggerFactory
    {
        public void AddProvider(ILoggerProvider provider) => throw new IOException(Sensitive);

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) =>
            throw new IOException(Sensitive);

        public void Dispose() => throw new IOException(Sensitive);
    }

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        internal List<string> Rendered { get; } = [];
        internal List<Exception?> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        )
        {
            Rendered.Add(formatter(state, exception));
            Exceptions.Add(exception);
            throw new IOException(Sensitive);
        }
    }
}
