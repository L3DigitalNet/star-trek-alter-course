using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies the explicit V1 save contract and isolated simulation reconstruction.</summary>
public sealed class GamePersistenceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SavedAt = new(2026, 9, 2, 14, 45, 0, TimeSpan.Zero);

    /// <summary>Confirms active travel and repair survive a semantic snapshot round trip.</summary>
    [Fact]
    public void RoundTripsWhileTravelAndSensorRepairAreActive()
    {
        GameSimulation original = CreateTravelingGame();
        GameSaveMetadata metadata = CreateMetadata();

        byte[] json = GamePersistence.Serialize(original, metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(json, CreateCatalog(), "memory-save.json");

        Assert.Equal(metadata, loaded.Metadata);
        Assert.Equal(original.GetPlayerProjection(), loaded.Simulation.GetPlayerProjection());
        Assert.Equal(json, GamePersistence.Serialize(loaded.Simulation, metadata));
    }

    /// <summary>Confirms future-order state and correlated active operations are explicit wire data.</summary>
    [Fact]
    public void OutputContainsActiveOperationsAndCompleteDeterministicState()
    {
        byte[] json = GamePersistence.Serialize(CreateTravelingGame(), CreateMetadata());
        using var document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement simulation = root.GetProperty("simulation");
        JsonElement scheduler = simulation.GetProperty("scheduler");
        JsonElement work = scheduler.GetProperty("outstandingWork");
        JsonElement strategic = simulation.GetProperty("strategicState");
        JsonElement repair = simulation.GetProperty("playerShip").GetProperty("sensorRepair");

        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal(4000, simulation.GetProperty("timeMilliseconds").GetInt64());
        Assert.Equal(2, simulation.GetProperty("shipAllocatorNextId").GetInt64());
        Assert.Equal(3, scheduler.GetProperty("nextWorkId").GetInt64());
        Assert.Equal(2, scheduler.GetProperty("nextSequence").GetInt64());
        Assert.Equal(2, work.GetArrayLength());
        Assert.Equal("sensorRepairCompletion", work[0].GetProperty("kind").GetString());
        Assert.Equal("travelArrival", work[1].GetProperty("kind").GetString());
        Assert.Equal("traveling", strategic.GetProperty("kind").GetString());
        Assert.Equal(2, strategic.GetProperty("travel").GetProperty("scheduledArrivalId").GetInt64());
        Assert.Equal(1, repair.GetProperty("scheduledCompletionId").GetInt64());
        JsonElement locations = simulation.GetProperty("strategicMap").GetProperty("locations");
        Assert.Equal(3, locations.GetArrayLength());
        Assert.True(locations[0].GetProperty("position").TryGetProperty("xUnitless", out _));
        Assert.True(locations[0].GetProperty("position").TryGetProperty("yUnitless", out _));
        Assert.Equal(2, simulation.GetProperty("strategicMap").GetProperty("routes").GetArrayLength());
    }

    /// <summary>Confirms load continuation matches uninterrupted authoritative simulation behavior.</summary>
    [Fact]
    public void LoadedContinuationMatchesContinuousPath()
    {
        GameSimulation continuous = CreateTravelingGame();
        GameSimulation toSave = CreateTravelingGame();
        byte[] saved = GamePersistence.Serialize(toSave, CreateMetadata());
        toSave = null!;

        GameSimulation resumed = GamePersistence.Deserialize(saved, CreateCatalog(), "continuation.json").Simulation;

        ContinueScenario(continuous);
        ContinueScenario(resumed);

        Assert.Equal(continuous.GetPlayerProjection(), resumed.GetPlayerProjection());
        Assert.Equal(
            GamePersistence.Serialize(continuous, CreateMetadata()),
            GamePersistence.Serialize(resumed, CreateMetadata())
        );
    }

    /// <summary>Confirms V1 is selected explicitly and all unsupported versions fail closed.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(2)]
    [InlineData(99)]
    public void RejectsInvalidOrUnsupportedVersion(int version)
    {
        byte[] json = Mutate(root => root["schemaVersion"] = version);

        GamePersistenceException exception = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(json, CreateCatalog(), "version.json")
        );

        Assert.Equal(GamePersistenceFailure.UnsupportedVersion, exception.Failure);
        Assert.Equal("version.json", exception.SourceIdentity);
    }

    /// <summary>Confirms malformed, truncated, and duplicate-member documents fail before mapping.</summary>
    [Theory]
    [InlineData("{\"schemaVersion\":1")]
    [InlineData("{\"schemaVersion\":1} trailing")]
    public void RejectsMalformedOrTruncatedJson(string json)
    {
        GamePersistenceException exception = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(Encoding.UTF8.GetBytes(json), CreateCatalog(), "broken.json")
        );

        Assert.Equal(GamePersistenceFailure.InvalidData, exception.Failure);
        Assert.Contains("broken.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Confirms repeated JSON names cannot exploit last-value-wins deserialization.</summary>
    [Fact]
    public void RejectsDuplicateJsonMembers()
    {
        string json = Encoding.UTF8.GetString(GamePersistence.Serialize(CreateTravelingGame(), CreateMetadata()));
        json = json.Replace(
            "\"schemaVersion\": 1,",
            "\"schemaVersion\": 1,\n  \"schemaVersion\": 1,",
            StringComparison.Ordinal
        );

        GamePersistenceException exception = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(Encoding.UTF8.GetBytes(json), CreateCatalog(), "duplicate.json")
        );

        Assert.Equal(GamePersistenceFailure.InvalidData, exception.Failure);
        Assert.Contains("duplicate JSON member 'schemaVersion'", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Confirms unknown and absent required members cannot silently change contract meaning.</summary>
    [Fact]
    public void RejectsUnknownAndMissingRequiredMembers()
    {
        byte[] unknown = Mutate(root => root["unexpected"] = true);
        byte[] missing = Mutate(root => root.Remove("simulation"));

        AssertFailure(unknown, "unknown.json", GamePersistenceFailure.InvalidData, "unexpected");
        AssertFailure(missing, "missing.json", GamePersistenceFailure.InvalidData, "simulation");
    }

    /// <summary>Confirms content references and scheduler ordering are semantic load boundaries.</summary>
    [Fact]
    public void RejectsInvalidContentAndSchedulerState()
    {
        byte[] invalidContent = Mutate(root =>
            root["simulation"]!["playerShip"]!["definitionId"] = "missing-definition"
        );
        byte[] duplicateId = Mutate(root =>
        {
            var work = (JsonArray)root["simulation"]!["scheduler"]!["outstandingWork"]!;
            work[1]!["id"] = work[0]!["id"]!.GetValue<long>();
        });
        byte[] outOfOrder = Mutate(root =>
        {
            var work = (JsonArray)root["simulation"]!["scheduler"]!["outstandingWork"]!;
            JsonNode? first = work[0];
            work.RemoveAt(0);
            work.Add(first);
        });
        byte[] duplicateSequence = Mutate(root =>
        {
            var work = (JsonArray)root["simulation"]!["scheduler"]!["outstandingWork"]!;
            work[1]!["sequence"] = work[0]!["sequence"]!.GetValue<long>();
        });

        AssertFailure(invalidContent, "content.json", GamePersistenceFailure.InvalidData, "missing-definition");
        AssertFailure(duplicateId, "duplicate-work.json", GamePersistenceFailure.InvalidData, "duplicate");
        AssertFailure(outOfOrder, "order.json", GamePersistenceFailure.InvalidData, "order");
        AssertFailure(duplicateSequence, "duplicate-sequence.json", GamePersistenceFailure.InvalidData, "duplicate");
    }

    /// <summary>Confirms aggregate fixed-step alignment is enforced before state becomes live.</summary>
    [Fact]
    public void RejectsOffGridCurrentAndDueTimes()
    {
        byte[] current = Mutate(root => root["simulation"]!["timeMilliseconds"] = 4050);
        byte[] due = Mutate(root =>
            root["simulation"]!["scheduler"]!["outstandingWork"]![0]!["dueTimeMilliseconds"] = 8050
        );

        AssertFailure(current, "current-grid.json", GamePersistenceFailure.InvalidData, "fixed-step");
        AssertFailure(due, "due-grid.json", GamePersistenceFailure.InvalidData, "fixed-step");
    }

    /// <summary>Confirms restored clocks and allocators retain room for deterministic continuation.</summary>
    [Fact]
    public void RejectsTerminalTimeAndSchedulerCounters()
    {
        long terminalAlignedTime = long.MaxValue - (long.MaxValue % 100);
        byte[] time = Mutate(root => root["simulation"]!["timeMilliseconds"] = terminalAlignedTime);
        byte[] workId = Mutate(root => root["simulation"]!["scheduler"]!["nextWorkId"] = long.MaxValue);
        byte[] sequence = Mutate(root => root["simulation"]!["scheduler"]!["nextSequence"] = long.MaxValue);

        AssertFailure(time, "terminal-time.json", GamePersistenceFailure.InvalidData, "headroom");
        AssertFailure(workId, "terminal-work-id.json", GamePersistenceFailure.InvalidData, "headroom");
        AssertFailure(sequence, "terminal-sequence.json", GamePersistenceFailure.InvalidData, "headroom");
    }

    /// <summary>Confirms travel and repair must each correlate to exactly matching scheduled work.</summary>
    [Fact]
    public void RejectsBrokenTravelAndRepairCorrelations()
    {
        byte[] travel = Mutate(root => root["simulation"]!["strategicState"]!["travel"]!["scheduledArrivalId"] = 1);
        byte[] repair = Mutate(root =>
            root["simulation"]!["playerShip"]!["sensorRepair"]!["scheduledCompletionId"] = 2
        );
        byte[] surplusArrival = Mutate(root =>
        {
            JsonObject scheduler = root["simulation"]!["scheduler"]!.AsObject();
            scheduler["nextWorkId"] = 4;
            scheduler["nextSequence"] = 3;
            scheduler["outstandingWork"]!
                .AsArray()
                .Add(
                    new JsonObject
                    {
                        ["id"] = 3,
                        ["dueTimeMilliseconds"] = 14000,
                        ["sequence"] = 2,
                        ["kind"] = "travelArrival",
                    }
                );
        });

        AssertFailure(travel, "travel-correlation.json", GamePersistenceFailure.InvalidData, "correlat");
        AssertFailure(repair, "repair-correlation.json", GamePersistenceFailure.InvalidData, "correlat");
        AssertFailure(surplusArrival, "surplus-arrival.json", GamePersistenceFailure.InvalidData, "exactly one");
    }

    /// <summary>Confirms nonfinite physical values and unknown event kinds never reach domain records.</summary>
    [Fact]
    public void RejectsNonfiniteTacticalValuesAndInvalidEnum()
    {
        string json = Encoding.UTF8.GetString(GamePersistence.Serialize(CreateTravelingGame(), CreateMetadata()));
        json = ReplaceNumber(json, "\"xKilometers\": ", "1e400");
        byte[] invalidKind = Mutate(root =>
            root["simulation"]!["scheduler"]!["outstandingWork"]![0]!["kind"] = "unknownWork"
        );

        AssertFailure(Encoding.UTF8.GetBytes(json), "nonfinite.json", GamePersistenceFailure.InvalidData, "finite");
        AssertFailure(invalidKind, "kind.json", GamePersistenceFailure.InvalidData, "kind");
    }

    /// <summary>Confirms a rejected load cannot alter a separate valid live aggregate.</summary>
    [Fact]
    public void FailedLoadLeavesExistingSimulationUnchanged()
    {
        GameSimulation existing = CreateTravelingGame();
        byte[] before = GamePersistence.Serialize(existing, CreateMetadata());
        byte[] invalid = Mutate(root => root["simulation"]!["shipAllocatorNextId"] = 1);

        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(invalid, CreateCatalog(), "isolated.json")
        );

        Assert.Equal(before, GamePersistence.Serialize(existing, CreateMetadata()));
    }

    /// <summary>Confirms replacement is complete, metadata is preserved, and temporary files are cleaned.</summary>
    [Fact]
    public void AtomicPathSaveReplacesPriorSaveAndCleansTemporaryFiles()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "slot-one.json");
        try
        {
            GameSimulation prior = CreateTravelingGame();
            GamePersistence.Save(path, prior, CreateMetadata("prior", "Prior"));
            byte[] priorBytes = File.ReadAllBytes(path);

            GameSimulation replacement = CreateTravelingGame();
            replacement.AdvanceFixedSteps(10);
            GameSaveMetadata replacementMetadata = CreateMetadata("replacement", "Replacement");
            GamePersistence.Save(path, replacement, replacementMetadata);

            byte[] replacementBytes = File.ReadAllBytes(path);
            LoadedGameSave loaded = GamePersistence.Load(path, CreateCatalog());
            Assert.NotEqual(priorBytes, replacementBytes);
            Assert.Equal(replacementMetadata, loaded.Metadata);
            Assert.Equal(replacement.GetPlayerProjection(), loaded.Simulation.GetPlayerProjection());
            Assert.Equal(
                ["slot-one.json"],
                Directory.GetFiles(directory).Select(Path.GetFileName),
                StringComparer.Ordinal
            );
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Confirms validation happens before replacement and leaves no staging artifact.</summary>
    [Fact]
    public void InvalidCandidateNeverReplacesPriorSaveOrLeavesTemporaryFile()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "slot-one.json");
        try
        {
            GamePersistence.Save(path, CreateTravelingGame(), CreateMetadata());
            byte[] priorBytes = File.ReadAllBytes(path);
            var invalid = new GameSaveMetadata("", "Invalid", CreatedAt, SavedAt);

            Assert.Throws<ArgumentException>(() => GamePersistence.Save(path, CreateTravelingGame(), invalid));

            Assert.Equal(priorBytes, File.ReadAllBytes(path));
            Assert.Equal(
                ["slot-one.json"],
                Directory.GetFiles(directory).Select(Path.GetFileName),
                StringComparer.Ordinal
            );
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Confirms the V1 DTO contract excludes presentation, engine, service, and cache state.</summary>
    [Fact]
    public void OutputContainsOnlyTheExplicitCorePersistenceContract()
    {
        string json = Encoding.UTF8.GetString(GamePersistence.Serialize(CreateTravelingGame(), CreateMetadata()));
        string[] excludedTerms =
        [
            "accumulator",
            "node",
            "resource",
            "service",
            "logger",
            "delegate",
            "cache",
            "render",
            "pixel",
            "pan",
            "zoom",
            "scene",
            "transform",
            "random",
        ];

        Assert.All(excludedTerms, term => Assert.DoesNotContain(term, json, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Confirms load input is bounded by bytes and nesting depth.</summary>
    [Fact]
    public void RejectsOversizedAndExcessivelyDeepInput()
    {
        byte[] oversized = new byte[(1024 * 1024) + 1];
        byte[] deep = Encoding.UTF8.GetBytes(new string('[', 40) + new string(']', 40));

        AssertFailure(oversized, "oversized.json", GamePersistenceFailure.InvalidData, "byte");
        AssertFailure(deep, "deep.json", GamePersistenceFailure.InvalidData, "depth");
    }

    private static void ContinueScenario(GameSimulation simulation)
    {
        simulation.AdvanceUntilNextScheduledEvent();
        simulation.AdvanceUntilNextScheduledEvent();
        simulation.SetTacticalCourse(
            new SetTacticalCourseIntent(new HeadingDegrees(90), new SpeedKilometersPerSecond(2))
        );
        simulation.AdvanceFixedSteps(7);
    }

    private static GameSimulation CreateTravelingGame()
    {
        GameSimulation game = FirstGameSetup.Create(CreateDefinition());
        game.SetTacticalCourse(new SetTacticalCourseIntent(new HeadingDegrees(45), new SpeedKilometersPerSecond(3.5)));
        game.AdvanceFixedSteps(10);
        LocationId origin = game.GetPlayerProjection().Strategic.CurrentLocation!.Id;
        LocationId destination = game.GetPlayerProjection()
            .Strategic.Routes.Single(route => route.Origin == origin)
            .Destination;
        game.RequestTravel(new TravelIntent(destination));
        game.AdvanceFixedSteps(30);
        return game;
    }

    private static ShipDefinition CreateDefinition() =>
        new(
            new ShipDefinitionId("pathfinder"),
            "USS Pathfinder",
            new SpeedKilometersPerSecond(10),
            new SensorIntegrity(0.4),
            new SimulationDuration(8000)
        );

    private static ShipDefinitionCatalog CreateCatalog()
    {
        const string definition = """
            {
              "schemaVersion": 1,
              "id": "pathfinder",
              "displayName": "USS Pathfinder",
              "maximumTacticalSpeedKilometersPerSecond": 10,
              "initialSensorIntegrity": 0.4,
              "sensorRepairDurationMilliseconds": 8000
            }
            """;
        string schema = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "src/AlterCourse.Godot/content/schemas/ship-definition-v1.schema.json")
        );
        return new ShipDefinitionCatalogLoader(schema).LoadCatalog([
            ShipDefinitionContent.FromText("pathfinder.json", definition),
        ]);
    }

    private static GameSaveMetadata CreateMetadata(string saveId = "slot-one", string displayName = "Voyage One") =>
        new(saveId, displayName, CreatedAt, SavedAt);

    private static byte[] Mutate(Action<JsonObject> mutation)
    {
        JsonObject root = JsonNode
            .Parse(GamePersistence.Serialize(CreateTravelingGame(), CreateMetadata()))!
            .AsObject();
        mutation(root);
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static string ReplaceNumber(string json, string propertyPrefix, string replacement)
    {
        int valueStart = json.IndexOf(propertyPrefix, StringComparison.Ordinal) + propertyPrefix.Length;
        int valueEnd = json.IndexOf(',', valueStart);
        Assert.True(valueStart >= propertyPrefix.Length && valueEnd > valueStart);
        return string.Concat(json.AsSpan(0, valueStart), replacement, json.AsSpan(valueEnd));
    }

    private static void AssertFailure(
        byte[] json,
        string source,
        GamePersistenceFailure failure,
        string messageFragment
    )
    {
        GamePersistenceException exception = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(json, CreateCatalog(), source)
        );
        Assert.Equal(failure, exception.Failure);
        Assert.Equal(source, exception.SourceIdentity);
        Assert.Contains(messageFragment, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"alter-course-persistence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private static string FindRepositoryRoot()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (File.Exists(Path.Combine(directory.FullName, "AlterCourse.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
