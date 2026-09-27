using System.Globalization;
using System.Text;
using System.Text.Json;
using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using AlterCourse.Core.Tests.Gameplay;
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
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ThrowingLoggerAndFallbackRemainOptional(bool throwOnIsEnabled)
    {
        int fallbackCount = 0;
        var provider = new ThrowingLogger<GameDiagnostics>
        {
            Mode = throwOnIsEnabled ? FailureMode.Enable : FailureMode.Log,
        };
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
        Assert.Equal(throwOnIsEnabled ? 0 : 2, provider.Rendered.Count);
        Assert.True(provider.EnableChecks > 0);
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
        var fixture = new ObservationResponseProofFixture();
        ILogger<GameSimulation>[] loggers =
        [
            factory.CreateLogger<GameSimulation>(),
            NullLogger<GameSimulation>.Instance,
            .. new[] { FailureMode.Log, FailureMode.Enable, FailureMode.DebugEnabled, FailureMode.DebugLog }.Select(
                mode => new ThrowingLogger<GameSimulation> { Mode = mode }
            ),
        ];
        GameSimulation plain = FirstGameSetup.Create(fixture.ShipCatalog, fixture.FactionCatalog);
        string beforeLoad = ExerciseFactionScenario(plain);
        byte[] saved = GamePersistence.Serialize(plain, ObservationResponseProofFixture.Metadata);
        GameSimulation resumedPlain = GamePersistence
            .Deserialize(saved, fixture.ShipCatalog, fixture.FactionCatalog, "baseline")
            .Simulation;
        string afterLoad = ExerciseFactionScenario(resumedPlain);
        foreach (ILogger<GameSimulation> logger in loggers)
        {
            GameSimulation game = FirstGameSetup.Create(fixture.ShipCatalog, fixture.FactionCatalog, logger);
            Assert.Equal(beforeLoad, ExerciseFactionScenario(game));
            Assert.Equal(saved, GamePersistence.Serialize(game, ObservationResponseProofFixture.Metadata));
            GameSimulation resumed = GamePersistence
                .Deserialize(saved, fixture.ShipCatalog, fixture.FactionCatalog, Sensitive, logger)
                .Simulation;
            Assert.Equal(afterLoad, ExerciseFactionScenario(resumed));
            Assert.Equal(
                GamePersistence.Serialize(resumedPlain, ObservationResponseProofFixture.Metadata),
                GamePersistence.Serialize(resumed, ObservationResponseProofFixture.Metadata)
            );
            if (logger is ThrowingLogger<GameSimulation> failing)
            {
                Assert.True(failing.FailedCalls > 0, failing.Mode.ToString());
            }
        }
        Assert.Contains(sink.Events, value => HasEventId(value, 1401));
        Assert.Contains(sink.Events, value => HasEventId(value, 1402));
        Assert.Contains(sink.Events, value => HasDecisionKind(value, "FactionAssignment"));
        Assert.Contains(sink.Events, value => HasDecisionKind(value, "FactionInvestigation"));
        Assert.DoesNotContain(sink.Events, value => value.Level >= LogEventLevel.Error);
    }

    private static string ExerciseFactionScenario(GameSimulation simulation)
    {
        AssertRejectedMutationPreservesState(simulation);
        SimulationAdvanceResult advance = simulation.AdvanceFixedSteps(160);
        object advanceDecisions = DecisionEvidence(simulation);
        AdvanceUntilResult until = simulation.AdvanceUntilNextPlayerRelevantEvent();
        HailResult negativeHail = simulation.RequestHail(new SensorContactId(long.MaxValue));
        Assert.Equal(HailOutcome.ContactNotFound, negativeHail.Outcome);
        return JsonSerializer.Serialize(
            new
            {
                advance,
                advanceDecisions,
                until,
                negativeHail,
                finalDecisions = DecisionEvidence(simulation),
            }
        );
    }

    private static object DecisionEvidence(GameSimulation simulation) =>
        new
        {
            Contact = simulation.LastContactDecisionExplanation,
            Faction = simulation.LastFactionDecisionExplanation,
            Investigation = simulation.LastFactionInvestigationDecisionExplanation,
            Combat = simulation.LastDefensiveCombatDecisionExplanation,
        };

    private static void AssertRejectedMutationPreservesState(GameSimulation simulation)
    {
        SimulationState before = simulation.CaptureState();
        SpeedKilometersPerSecond maximum = GameSimulation.EffectiveMaximumTacticalSpeed(
            before.GetRequiredShip(before.PlayerShipId).Engineering
        );
        SetTacticalCourseResult result = simulation.SetTacticalCourse(
            new SetTacticalCourseIntent(new HeadingDegrees(180), new SpeedKilometersPerSecond(maximum.Value + 1))
        );
        Assert.Equal(SetTacticalCourseOutcome.SpeedExceedsCurrentCapability, result.Outcome);
        Assert.Same(before, simulation.CaptureState());
    }

    private static bool HasEventId(LogEvent value, int expected) =>
        value.Properties.TryGetValue("EventId", out LogEventPropertyValue? property)
        && property is StructureValue structure
        && structure.Properties.Any(field =>
            string.Equals(field.Name, "Id", StringComparison.Ordinal)
            && field.Value is ScalarValue { Value: int id }
            && id == expected
        );

    private static bool HasDecisionKind(LogEvent value, string expected) =>
        value.Properties.TryGetValue("DecisionKind", out LogEventPropertyValue? property)
        && property is ScalarValue { Value: string kind }
        && string.Equals(kind, expected, StringComparison.Ordinal);

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

    /// <summary>Proves the production file configuration rolls whole events and retains the newest bounded set.</summary>
    [Fact]
    public void ProductionFileRolloverUsesActualSizeAndRetentionLimits()
    {
        string directory = Path.Combine(Path.GetTempPath(), "altercourse-retention-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        int lastSequence;
        try
        {
            using (
                Logger backend = GameplayLogging.ConfigureFileSink(new LoggerConfiguration(), directory).CreateLogger()
            )
            {
                // Controlled padding forces the actual production limits without flooding the console sink.
                string padding = new('x', checked((int)(GameplayLogging.FileSizeLimitBytes / 16)));
                backend.Information("Retention {EventSequence} {Padding}", 0, padding);
                long eventBytes = new FileInfo(Assert.Single(Directory.GetFiles(directory))).Length;
                int eventsPerRoll = checked((int)((GameplayLogging.FileSizeLimitBytes + eventBytes - 1) / eventBytes));
                lastSequence = (GameplayLogging.RetainedFileCount + 2) * eventsPerRoll;
                for (int sequence = 1; sequence <= lastSequence; sequence++)
                {
                    backend.Information("Retention {EventSequence} {Padding}", sequence, padding);
                }
            }
            string[] paths = Directory.GetFiles(directory);
            Assert.Equal(GameplayLogging.RetainedFileCount, paths.Length);
            var retainedSequences = new HashSet<int>();
            foreach (string path in paths)
            {
                long maximumEventBytes = ReadRetentionRecords(path, retainedSequences);
                Assert.True(new FileInfo(path).Length <= GameplayLogging.FileSizeLimitBytes + maximumEventBytes);
                using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                Assert.True(exclusive.Length > 0);
            }
            Assert.DoesNotContain(0, retainedSequences);
            Assert.Contains(lastSequence, retainedSequences);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static long ReadRetentionRecords(string path, HashSet<int> sequences)
    {
        long maximumBytes = 0;
        foreach (string record in File.ReadLines(path))
        {
            using var json = JsonDocument.Parse(record);
            sequences.Add(json.RootElement.GetProperty("Properties").GetProperty("EventSequence").GetInt32());
            maximumBytes = Math.Max(maximumBytes, Encoding.UTF8.GetByteCount(record + Environment.NewLine));
        }
        return maximumBytes;
    }

    /// <summary>Exercises the real factory's default and explicit Debug filtering without global console mutation.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RealFactoryFiltersDecisionDetails(bool debug)
    {
        string directory = Path.Combine(Path.GetTempPath(), "altercourse-level-" + Guid.NewGuid().ToString("N"));
        try
        {
            using (
                var logging = GameplayLogging.Create(
                    () => directory,
                    () => { },
                    minimumLevel: debug ? LogEventLevel.Debug : LogEventLevel.Information
                )
            )
            {
                FirstGameSetup.Create(TestShipContent.Pathfinder(), logging.SimulationLogger).AdvanceFixedSteps(20);
            }
            string json = string.Concat(Directory.GetFiles(directory).Select(File.ReadAllText));
            Assert.Contains("ShipContactDecision", json, StringComparison.Ordinal);
            Assert.Equal(debug, json.Contains("\"RankingMetric\"", StringComparison.Ordinal));
            Assert.Equal(debug, json.Contains("\"Constraint\"", StringComparison.Ordinal));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    /// <summary>Classifies untyped BCL failures conservatively rather than claiming a domain invariant.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UntypedInvalidOperationFailuresAreProgrammingDefects(bool disposed)
    {
        var sink = new CapturingSink();
        using Logger backend = new LoggerConfiguration().WriteTo.Sink(sink).CreateLogger();
        using var factory = new Serilog.Extensions.Logging.SerilogLoggerFactory(backend, dispose: false);
        var diagnostics = new GameDiagnostics(factory.CreateLogger<GameDiagnostics>(), () => { });
        Exception exception = disposed
            ? new ObjectDisposedException(Sensitive)
            : new InvalidOperationException(Sensitive);
        diagnostics.Failure(GameDiagnostics.FailureOperation.Presentation, exception, 42, null);
        LogEvent value = Assert.Single(sink.Events);
        Assert.Equal("ProgrammingDefect", Assert.IsType<ScalarValue>(value.Properties["FailureClassification"]).Value);
        Assert.Null(Assert.IsType<ScalarValue>(value.Properties["ActorId"]).Value);
        Assert.Null(value.Exception);
    }

    /// <summary>Exercises accepted scan, event-boundary advance, and hail diagnostics under each provider failure.</summary>
    [Theory]
    [InlineData("None")]
    [InlineData("Collecting")]
    [InlineData("Log")]
    [InlineData("Enable")]
    [InlineData("DebugEnabled")]
    [InlineData("DebugLog")]
    [InlineData("DebugConstraintLog")]
    public void HailAndAdvanceUntilPreserveResultsAndExplanations(string mode)
    {
        var sink = new CapturingSink();
        using Logger backend = new LoggerConfiguration().MinimumLevel.Debug().WriteTo.Sink(sink).CreateLogger();
        using var factory = new Serilog.Extensions.Logging.SerilogLoggerFactory(backend, dispose: false);
        ILogger<GameSimulation> logger = mode switch
        {
            "None" => NullLogger<GameSimulation>.Instance,
            "Collecting" => factory.CreateLogger<GameSimulation>(),
            _ => new ThrowingLogger<GameSimulation> { Mode = Enum.Parse<FailureMode>(mode) },
        };
        GameSimulation baseline = CreateHailWorld(null);
        GameSimulation game = CreateHailWorld(logger);
        Assert.Equal(ExerciseHailScenario(baseline), ExerciseHailScenario(game));
        Assert.Equal(
            GamePersistence.Serialize(baseline, Milestone3ProofFixture.Metadata),
            GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata)
        );
        if (logger is ThrowingLogger<GameSimulation> failing)
        {
            Assert.True(failing.FailedCalls > 0, failing.Mode.ToString());
        }
        if (string.Equals(mode, "Collecting", StringComparison.Ordinal))
        {
            Assert.Contains(sink.Events, value => HasEventId(value, 1400));
        }
    }

    private static GameSimulation CreateHailWorld(ILogger<GameSimulation>? logger)
    {
        ShipDefinitionCatalog catalog = TestShipContent.Pathfinder();
        var local = new LocationId("logging-hail-local");
        ShipStart[] starts =
        [
            new(
                new ShipInstanceId(1),
                new ShipDefinitionId("pathfinder"),
                "Player",
                default,
                default,
                new AtLocationStart(local),
                TestShipStarts.Pathfinder()
            ),
            new(
                new ShipInstanceId(2),
                new ShipDefinitionId("pathfinder"),
                "Observer",
                new TacticalPosition(1, 0),
                default,
                new AtLocationStart(local),
                TestShipStarts.Pathfinder()
            ),
        ];
        GameSimulation initial = new GameBootstrap(
            new SimulationTime(0),
            new StrategicMap([new StrategicLocation(local, "Local", default)], []),
            starts[0].InstanceId,
            starts
        ).CreateSimulation(catalog);
        SimulationState state = initial.CaptureState();
        ShipState npc = state.GetRequiredShip(starts[1].InstanceId);
        return GameSimulation.RestoreState(
            state.ReplaceShip(
                npc.InstanceId,
                npc with
                {
                    AutonomousState = new ShipAutonomousState(ShipContactPosture.CautiousContact),
                }
            ),
            catalog,
            FactionDefinitionCatalog.Empty,
            logger
        );
    }

    private static string ExerciseHailScenario(GameSimulation simulation)
    {
        simulation.AdvanceFixedSteps(1);
        SensorContactId contact = Assert.Single(simulation.GetPlayerProjection().Ship.Sensors.Contacts).Id;
        ActiveSensorScanResult scan = simulation.RequestActiveSensorScan(contact);
        Assert.Equal(ActiveSensorScanOutcome.Accepted, scan.Outcome);
        AdvanceUntilResult until = simulation.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Contains(until.ResolvedEvents, value => value.Kind == PlayerAdvanceEventKind.ActiveSensorScanCompleted);
        HailResult hail = simulation.RequestHail(contact);
        Assert.Equal(HailOutcome.Acknowledged, hail.Outcome);
        ShipContactDecisionExplanation decision = Assert.IsType<ShipContactDecisionExplanation>(
            simulation.LastContactDecisionExplanation
        );
        Assert.Equal(ShipContactDecisionPolicyReason.IdentifiedHailHold, decision.Candidates[0].PolicyReason);
        return JsonSerializer.Serialize(
            new
            {
                scan,
                until,
                hail,
                decision,
                projection = simulation.GetPlayerProjection(),
            }
        );
    }

    /// <summary>Compares world, rejection, restore, and continuation semantics despite construction or cleanup failures.</summary>
    [Theory]
    [InlineData("Path")]
    [InlineData("Factory")]
    [InlineData("Provider")]
    [InlineData("Dispose")]
    public void CompositionFailuresDoNotChangeWorldContinuation(string phase)
    {
        int fallbacks = 0;
        var provider = new ThrowingFactory
        {
            FailCreation = string.Equals(phase, "Provider", StringComparison.Ordinal),
            FailDispose = string.Equals(phase, "Dispose", StringComparison.Ordinal),
        };
        using var runtime = GameplayLogging.Create(
            () => string.Equals(phase, "Path", StringComparison.Ordinal) ? throw new IOException(Sensitive) : "unused",
            () => fallbacks++,
            _ => string.Equals(phase, "Factory", StringComparison.Ordinal) ? throw new IOException(Sensitive) : provider
        );
        var fixture = new ObservationResponseProofFixture();
        GameSimulation baseline = FirstGameSetup.Create(fixture.ShipCatalog, fixture.FactionCatalog);
        GameSimulation game = FirstGameSetup.Create(
            fixture.ShipCatalog,
            fixture.FactionCatalog,
            runtime.SimulationLogger
        );
        Assert.Equal(ExerciseFactionScenario(baseline), ExerciseFactionScenario(game));
        byte[] saved = GamePersistence.Serialize(baseline, ObservationResponseProofFixture.Metadata);
        Assert.Equal(saved, GamePersistence.Serialize(game, ObservationResponseProofFixture.Metadata));
        runtime.Dispose();
        runtime.Dispose();
        Assert.True(fallbacks > 0);
        if (phase is "Provider" or "Dispose")
        {
            Assert.Equal(1, provider.DisposeCalls);
        }
        GameSimulation resumedBaseline = GamePersistence
            .Deserialize(saved, fixture.ShipCatalog, fixture.FactionCatalog, "baseline")
            .Simulation;
        GameSimulation resumedGame = GamePersistence
            .Deserialize(saved, fixture.ShipCatalog, fixture.FactionCatalog, Sensitive, runtime.SimulationLogger)
            .Simulation;
        Assert.Equal(ExerciseFactionScenario(resumedBaseline), ExerciseFactionScenario(resumedGame));
        Assert.Equal(
            GamePersistence.Serialize(resumedBaseline, ObservationResponseProofFixture.Metadata),
            GamePersistence.Serialize(resumedGame, ObservationResponseProofFixture.Metadata)
        );
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
        internal bool FailCreation { get; init; } = true;
        internal bool FailDispose { get; init; } = true;
        internal int DisposeCalls { get; private set; }

        public void AddProvider(ILoggerProvider provider) => throw new IOException(Sensitive);

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName) =>
            FailCreation ? throw new IOException(Sensitive) : NullLogger.Instance;

        public void Dispose()
        {
            DisposeCalls++;
            if (FailDispose)
            {
                throw new IOException(Sensitive);
            }
        }
    }

    private sealed class ThrowingLogger<T> : ILogger<T>
    {
        internal FailureMode Mode { get; init; } = FailureMode.Log;
        internal int EnableChecks { get; private set; }
        internal int FailedCalls { get; private set; }
        internal List<string> Rendered { get; } = [];
        internal List<Exception?> Exceptions { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel)
        {
            EnableChecks++;
            if (Mode == FailureMode.Enable || (Mode == FailureMode.DebugEnabled && logLevel == LogLevel.Debug))
            {
                FailedCalls++;
                throw new IOException(Sensitive);
            }
            return true;
        }

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
            if (
                Mode == FailureMode.Log
                || (Mode == FailureMode.DebugLog && logLevel == LogLevel.Debug)
                || (Mode == FailureMode.DebugConstraintLog && eventId.Id == 1411)
            )
            {
                FailedCalls++;
                throw new IOException(Sensitive);
            }
        }
    }

    private enum FailureMode
    {
        Log,
        Enable,
        DebugEnabled,
        DebugLog,
        DebugConstraintLog,
    }
}
