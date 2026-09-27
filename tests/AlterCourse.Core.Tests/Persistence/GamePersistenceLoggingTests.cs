using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Tests.Gameplay;
using Microsoft.Extensions.Logging;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies that restoration preserves the injected diagnostic boundary without persisting logger state.</summary>
public sealed class GamePersistenceLoggingTests
{
    /// <summary>Current and historical restoration retain the supplied logger and identical continuation.</summary>
    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(9)]
    [InlineData(10)]
    public void DeserializeRetainsLoggerWithoutChangingContinuation(int version)
    {
        var fixture = new Milestone3ProofFixture();
        byte[] current = GamePersistence.Serialize(fixture.CreateDefault(), Milestone3ProofFixture.Metadata);
        JsonObject root = version is 5 or 6
            ? GamePersistenceAdmissionTests.HistoricalDocument(current, version)
            : JsonNode.Parse(current)!.AsObject();
        if (version == 9)
            SaveJsonV10.ToV9(root);
        byte[] json = Encoding.UTF8.GetBytes(root.ToJsonString());
        var logger = new RecordingLogger();
        GameSimulation logged = GamePersistence.Deserialize(json, fixture.Catalog, "logged.json", logger).Simulation;
        GameSimulation baseline = GamePersistence.Deserialize(json, fixture.Catalog, "baseline.json").Simulation;

        logged.AdvanceFixedSteps(100);
        baseline.AdvanceFixedSteps(100);

        Assert.True(logger.EventCount > 0);
        Assert.Equal(
            GamePersistence.Serialize(baseline, Milestone3ProofFixture.Metadata),
            GamePersistence.Serialize(logged, Milestone3ProofFixture.Metadata)
        );
    }

    /// <summary>The file-load entry point passes the same logger through reconstruction.</summary>
    [Fact]
    public void FileLoadRetainsLogger()
    {
        string directory = Path.Combine(Path.GetTempPath(), "logged-save-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var fixture = new Milestone3ProofFixture();
            string path = Path.Combine(directory, "slot.json");
            GamePersistence.Save(path, fixture.CreateDefault(), Milestone3ProofFixture.Metadata);
            var logger = new RecordingLogger();
            GameSimulation loaded = GamePersistence
                .Load(path, fixture.Catalog, FactionDefinitionCatalog.Empty, logger)
                .Simulation;
            loaded.AdvanceFixedSteps(100);
            Assert.True(logger.EventCount > 0);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class RecordingLogger : ILogger<GameSimulation>
    {
        internal int EventCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => EventCount++;
    }
}
