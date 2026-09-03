using System.Text.Json.Nodes;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Locks the public typed bootstrap to its deterministic three-ship V3 world signature.</summary>
public sealed class WorldBootstrapSignatureTests
{
    private static readonly GameSaveMetadata Metadata = new(
        "bootstrap-signature",
        "Bootstrap Signature",
        new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 2, 12, 30, 0, TimeSpan.Zero)
    );

    /// <summary>Confirms declaration order cannot alter projection, V3 bytes, ship order, or work order.</summary>
    [Fact]
    public void NormalizesEquivalentInputsToOneStableWorldSignature()
    {
        GameBootstrap forward = CreateBootstrap();
        GameBootstrap reversed = CreateBootstrap(CreateStarts().Reverse());
        GameSimulation first = forward.CreateSimulation(CreateCatalog());
        GameSimulation second = reversed.CreateSimulation(CreateCatalog());
        JsonObject root = Parse(first);
        JsonArray simulationShips = root["simulation"]!["ships"]!.AsArray();
        JsonArray work = root["simulation"]!["scheduler"]!["outstandingWork"]!.AsArray();

        Assert.Equal([1L, 2L, 3L], forward.ShipStarts.Select(start => start.InstanceId.Value));
        Assert.IsAssignableFrom<IReadOnlyList<ShipStart>>(forward.ShipStarts);
        Assert.Throws<NotSupportedException>(() => ((IList<ShipStart>)forward.ShipStarts).Add(forward.ShipStarts[0]));
        Assert.Equal(first.GetPlayerProjection(), second.GetPlayerProjection());
        Assert.Equal(GamePersistence.Serialize(first, Metadata), GamePersistence.Serialize(second, Metadata));
        Assert.Equal([1L, 2L, 3L], simulationShips.Select(ship => ship!["instanceId"]!.GetValue<long>()));
        Assert.Equal(
            [
                (1L, 1L, 0L, 8000L, "sensorRepairCompletion"),
                (2L, 2L, 1L, 8000L, "sensorRepairCompletion"),
                (3L, 3L, 2L, 14000L, "travelArrival"),
            ],
            work.Select(item =>
                (
                    item!["id"]!.GetValue<long>(),
                    item["targetShipId"]!.GetValue<long>(),
                    item["sequence"]!.GetValue<long>(),
                    item["dueTimeMilliseconds"]!.GetValue<long>(),
                    item["kind"]!.GetValue<string>()
                )
            )
        );

        Assert.DoesNotContain(
            typeof(PlayerProjection).GetProperties(),
            property =>
                property.Name.Contains("Ship", StringComparison.Ordinal)
                && !string.Equals(property.Name, nameof(PlayerProjection.Ship), StringComparison.Ordinal)
        );
    }

    /// <summary>Confirms the largest accepted authored world is immediately compatible with its V3 loader.</summary>
    [Fact]
    public void MaximumBootstrapWorldSerializesAndDeserializes()
    {
        string definitionId = CreateMaximumIdentity("definition");
        string expandingDisplayName = new('\u0080', 64);
        StrategicMap map = CreateMaximumMap();
        ShipStart[] starts =
        [
            .. Enumerable
                .Range(1, 256)
                .Select(id => new ShipStart(
                    new ShipInstanceId(id),
                    new ShipDefinitionId(definitionId),
                    expandingDisplayName,
                    default,
                    default,
                    new SensorIntegrity(1),
                    new AtLocationStart(map.Locations[0].Id)
                )),
        ];
        GameSimulation game = new GameBootstrap(
            new SimulationTime(0),
            map,
            starts[0].InstanceId,
            starts
        ).CreateSimulation(CreateCatalog(definitionId));
        var maximumMetadata = new GameSaveMetadata(
            new string('\u0080', 128),
            new string('\u0080', 128),
            Metadata.CreatedAtUtc,
            Metadata.SavedAtUtc
        );

        byte[] saved = GamePersistence.Serialize(game, maximumMetadata);
        LoadedGameSave restored = GamePersistence.Deserialize(
            saved,
            CreateCatalog(definitionId),
            "maximum-bootstrap.json"
        );
        JsonNode simulation = JsonNode.Parse(saved)!["simulation"]!;

        Assert.InRange(saved.Length, 1, 1024 * 1024);
        Assert.Equal(256, simulation["ships"]!.AsArray().Count);
        Assert.Equal(256, simulation["strategicMap"]!["locations"]!.AsArray().Count);
        Assert.Equal(1024, simulation["strategicMap"]!["routes"]!.AsArray().Count);
        Assert.Equal(saved, GamePersistence.Serialize(restored.Simulation, restored.Metadata));
    }

    /// <summary>Confirms player input and scheduled consequences remain isolated to their declared ship targets.</summary>
    [Fact]
    public void AdvancesIndependentLocalRepairAndTravelStateByTarget()
    {
        GameSimulation game = CreateBootstrap().CreateSimulation(CreateCatalog());
        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(
                new SetTacticalCourseIntent(new HeadingDegrees(90), new SpeedKilometersPerSecond(2))
            ).Outcome
        );
        game.AdvanceFixedSteps(40);
        JsonArray atFourSeconds = Ships(Parse(game));

        Assert.Equal(11.25, game.GetPlayerProjection().Ship.Tactical.Position.XKilometers, 10);
        Assert.Equal(0.7, atFourSeconds[0]!["sensorIntegrity"]!.GetValue<double>(), 10);
        Assert.Equal(0.7, atFourSeconds[1]!["sensorIntegrity"]!.GetValue<double>(), 10);
        Assert.NotNull(atFourSeconds[0]!["sensorRepair"]);
        Assert.NotNull(atFourSeconds[1]!["sensorRepair"]);
        Assert.Equal("atLocation", atFourSeconds[1]!["strategicState"]!["kind"]!.GetValue<string>());
        Assert.Equal("traveling", atFourSeconds[2]!["strategicState"]!["kind"]!.GetValue<string>());
        Assert.Equal(6, atFourSeconds[2]!["tacticalPosition"]!["xKilometers"]!.GetValue<double>());

        AdvanceUntilResult repairs = game.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Equal(8000, repairs.StoppedAt.Milliseconds);
        Assert.Equal([new PlayerAdvanceEvent(PlayerAdvanceEventKind.SensorRepairCompleted)], repairs.ResolvedEvents);
        JsonArray afterRepairs = Ships(Parse(game));
        Assert.Null(afterRepairs[0]!["sensorRepair"]);
        Assert.Null(afterRepairs[1]!["sensorRepair"]);
        Assert.Equal("traveling", afterRepairs[2]!["strategicState"]!["kind"]!.GetValue<string>());

        AdvanceUntilResult noPlayerEvent = game.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Equal(AdvanceUntilOutcome.NoPlayerEvent, noPlayerEvent.Outcome);
        Assert.Equal(8000, noPlayerEvent.StoppedAt.Milliseconds);
        Assert.Empty(noPlayerEvent.ResolvedEvents);
        SimulationAdvanceResult hiddenArrival = game.AdvanceFixedSteps(60);
        Assert.Empty(hiddenArrival.ResolvedEvents);
        JsonArray complete = Ships(Parse(game));
        Assert.Equal("dawn-anchor", complete[0]!["strategicState"]!["locationId"]!.GetValue<string>());
        Assert.Equal("vesper-reach", complete[1]!["strategicState"]!["locationId"]!.GetValue<string>());
        Assert.Equal("meridian-drift", complete[2]!["strategicState"]!["locationId"]!.GetValue<string>());
        Assert.Equal(0.25, complete[2]!["tacticalPosition"]!["xKilometers"]!.GetValue<double>());
        Assert.Equal(-0.75, complete[2]!["tacticalPosition"]!["yKilometers"]!.GetValue<double>());
    }

    /// <summary>Confirms player strategic commands neither replace nor retarget a preexisting NPC journey.</summary>
    [Fact]
    public void PlayerTravelChangesOnlyPlayerStrategicState()
    {
        GameSimulation game = CreateBootstrap().CreateSimulation(CreateCatalog());
        JsonObject before = Parse(game);
        JsonNode npcTravel = Ships(before)[2]!["strategicState"]!.DeepClone();
        JsonNode npcWork = WorkForShip(before, new ShipInstanceId(3)).DeepClone();

        Assert.Equal(
            TravelOutcome.Accepted,
            game.RequestTravel(new TravelIntent(new LocationId("vesper-reach"))).Outcome
        );
        JsonObject after = Parse(game);

        Assert.Equal("traveling", Ships(after)[0]!["strategicState"]!["kind"]!.GetValue<string>());
        Assert.Equal("atLocation", Ships(after)[1]!["strategicState"]!["kind"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(npcTravel, Ships(after)[2]!["strategicState"]));
        Assert.True(JsonNode.DeepEquals(npcWork, WorkForShip(after, new ShipInstanceId(3))));
    }

    /// <summary>Confirms a player course command leaves an independently moving local NPC course intact.</summary>
    [Fact]
    public void PlayerTacticalCourseChangesOnlyPlayerShip()
    {
        ShipStart[] starts = CreateStarts();
        starts[1] = starts[1] with
        {
            TacticalMotion = new TacticalMotion(new HeadingDegrees(0), new SpeedKilometersPerSecond(1)),
        };
        GameSimulation game = CreateBootstrap(starts).CreateSimulation(CreateCatalog());
        JsonNode npcCourse = Ships(Parse(game))[1]!["tacticalMotion"]!.DeepClone();

        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(
                new SetTacticalCourseIntent(new HeadingDegrees(90), new SpeedKilometersPerSecond(2))
            ).Outcome
        );
        JsonArray afterCommand = Ships(Parse(game));
        Assert.True(JsonNode.DeepEquals(npcCourse, afterCommand[1]!["tacticalMotion"]));

        game.AdvanceFixedSteps(1);
        JsonArray afterStep = Ships(Parse(game));
        Assert.Equal(-2, afterStep[1]!["tacticalPosition"]!["xKilometers"]!.GetValue<double>(), 10);
        Assert.Equal(4.1, afterStep[1]!["tacticalPosition"]!["yKilometers"]!.GetValue<double>(), 10);
    }

    /// <summary>Confirms a V3 save at four seconds resumes byte-identically through both event boundaries.</summary>
    [Fact]
    public void SaveReloadContinuationMatchesUninterruptedWorld()
    {
        ShipDefinitionCatalog catalog = CreateCatalog();
        GameSimulation uninterrupted = CreateBootstrap().CreateSimulation(catalog);
        uninterrupted.SetTacticalCourse(
            new SetTacticalCourseIntent(new HeadingDegrees(90), new SpeedKilometersPerSecond(2))
        );
        uninterrupted.AdvanceFixedSteps(40);
        byte[] partway = GamePersistence.Serialize(uninterrupted, Metadata);
        GameSimulation restored = GamePersistence.Deserialize(partway, catalog, "bootstrap-signature.json").Simulation;

        AdvanceUntilResult first = uninterrupted.AdvanceUntilNextPlayerRelevantEvent();
        AdvanceUntilResult restoredFirst = restored.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Equal(first, restoredFirst);
        Assert.Equal(uninterrupted.GetPlayerProjection(), restored.GetPlayerProjection());
        Assert.Equal(GamePersistence.Serialize(uninterrupted, Metadata), GamePersistence.Serialize(restored, Metadata));

        AdvanceUntilResult second = uninterrupted.AdvanceUntilNextPlayerRelevantEvent();
        AdvanceUntilResult restoredSecond = restored.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Equal(second, restoredSecond);
        Assert.Equal(uninterrupted.GetPlayerProjection(), restored.GetPlayerProjection());
        Assert.Equal(GamePersistence.Serialize(uninterrupted, Metadata), GamePersistence.Serialize(restored, Metadata));
    }

    /// <summary>Confirms bootstrap rejects invalid identity, reference, capability, time, and repair declarations.</summary>
    [Fact]
    public void RejectsInvalidDeclarationsAtTheBootstrapBoundary()
    {
        ShipStart valid = CreateStarts()[0];
        ShipStart second = CreateStarts()[1];
        Assert.Throws<ArgumentException>(() => CreateBootstrap([valid, valid]));
        Assert.Throws<ArgumentException>(() => CreateBootstrap([valid with { InstanceId = default }]));
        Assert.Throws<ArgumentException>(() => new GameBootstrap(new SimulationTime(0), CreateMap(), default, [valid]));
        Assert.Throws<ArgumentException>(() =>
            new GameBootstrap(new SimulationTime(0), CreateMap(), new ShipInstanceId(99), [valid])
        );
        Assert.Throws<ArgumentException>(() => CreateBootstrap(Array.Empty<ShipStart>()));
        Assert.Throws<ArgumentException>(() => CreateBootstrap([valid, null!]));
        Assert.Throws<KeyNotFoundException>(() =>
            CreateBootstrap([valid with { DefinitionId = new ShipDefinitionId("missing") }])
                .CreateSimulation(CreateCatalog())
        );
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([valid with { VesselDisplayName = " " }]).CreateSimulation(CreateCatalog())
        );
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([valid with { Strategic = null! }]).CreateSimulation(CreateCatalog())
        );
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([valid with { VesselDisplayName = new string('V', 65) }])
        );
        Assert.Throws<ArgumentException>(() => CreateBootstrap(OverflowAfter(valid, 257)));
        Assert.Throws<ArgumentException>(() =>
            new GameBootstrap(new SimulationTime(9223372036854775800), CreateMap(), valid.InstanceId, [valid])
        );
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([valid with { InstanceId = new ShipInstanceId(long.MaxValue - 1) }])
                .CreateSimulation(CreateCatalog())
        );
    }

    /// <summary>Confirms invalid content references, capabilities, and repair declarations fail closed.</summary>
    [Fact]
    public void RejectsInvalidContentDependentDeclarations()
    {
        ShipStart valid = CreateStarts()[0];
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([valid with { Strategic = new AtLocationStart(default) }]).CreateSimulation(CreateCatalog())
        );
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([valid with { Strategic = new AtLocationStart(new LocationId("missing")) }])
                .CreateSimulation(CreateCatalog())
        );
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([
                    valid with
                    {
                        TacticalMotion = new TacticalMotion(new HeadingDegrees(0), new SpeedKilometersPerSecond(10.01)),
                    },
                ])
                .CreateSimulation(CreateCatalog())
        );
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([valid with { SensorIntegrity = new SensorIntegrity(0.5) }])
                .CreateSimulation(CreateCatalog())
        );
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([
                    valid with
                    {
                        SensorRepair = new SensorRepairStart(
                            new SensorIntegrity(0.4),
                            new SensorIntegrity(0.4),
                            new SimulationTime(0)
                        ),
                    },
                ])
                .CreateSimulation(CreateCatalog())
        );
        Assert.Throws<ArgumentException>(() =>
            new GameBootstrap(new SimulationTime(50), CreateMap(), valid.InstanceId, [valid])
        );
    }

    /// <summary>Confirms active travel requires a route, a containing interval, and dormant tactical motion.</summary>
    [Fact]
    public void RejectsInvalidActiveTravelDeclarations()
    {
        ShipStart second = CreateStarts()[1];
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([
                    second with
                    {
                        Strategic = new TravelingStart(
                            new LocationId("dawn-anchor"),
                            new LocationId("meridian-drift"),
                            new SimulationTime(0)
                        ),
                    },
                ])
                .CreateSimulation(CreateCatalog())
        );
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([
                    second with
                    {
                        Strategic = new TravelingStart(
                            new LocationId("vesper-reach"),
                            new LocationId("meridian-drift"),
                            new SimulationTime(100)
                        ),
                    },
                ])
                .CreateSimulation(CreateCatalog())
        );
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([
                    second with
                    {
                        Strategic = new TravelingStart(
                            new LocationId("vesper-reach"),
                            new LocationId("meridian-drift"),
                            new SimulationTime(0)
                        ),
                        TacticalMotion = new TacticalMotion(new HeadingDegrees(0), new SpeedKilometersPerSecond(1)),
                    },
                ])
                .CreateSimulation(CreateCatalog())
        );
    }

    /// <summary>Confirms explicit sparse identities restore the allocator immediately after the maximum identity.</summary>
    [Fact]
    public void DerivesAllocatorFromMaximumExplicitShipIdentity()
    {
        ShipStart[] starts = CreateStarts();
        ShipStart[] sparse =
        [
            starts[0] with
            {
                InstanceId = new ShipInstanceId(2),
            },
            starts[1] with
            {
                InstanceId = new ShipInstanceId(5),
            },
            starts[2] with
            {
                InstanceId = new ShipInstanceId(9),
            },
        ];
        GameSimulation game = new GameBootstrap(
            new SimulationTime(0),
            CreateMap(),
            sparse[0].InstanceId,
            sparse
        ).CreateSimulation(CreateCatalog());
        JsonObject simulation = Parse(game)["simulation"]!.AsObject();

        Assert.Equal(10, simulation["shipAllocatorNextId"]!.GetValue<long>());
        Assert.Equal(
            [2L, 5L, 9L],
            simulation["ships"]!.AsArray().Select(ship => ship!["instanceId"]!.GetValue<long>())
        );
        Assert.Equal(
            [2L, 5L, 9L],
            simulation["scheduler"]!["outstandingWork"]!
                .AsArray()
                .Select(work => work!["targetShipId"]!.GetValue<long>())
        );
    }

    private static GameBootstrap CreateBootstrap(IEnumerable<ShipStart>? starts = null) =>
        new(new SimulationTime(0), CreateMap(), new ShipInstanceId(1), starts ?? CreateStarts());

    private static ShipStart[] CreateStarts()
    {
        var initial = new SimulationTime(0);
        var definition = new ShipDefinitionId("pathfinder");
        var damaged = new SensorIntegrity(0.4);
        var repaired = new SensorIntegrity(1);
        TacticalMotion stopped = default;
        return
        [
            new(
                new ShipInstanceId(1),
                definition,
                "USS Pathfinder",
                new TacticalPosition(3.25, -7.5),
                stopped,
                damaged,
                new AtLocationStart(new LocationId("dawn-anchor")),
                new SensorRepairStart(damaged, repaired, initial)
            ),
            new(
                new ShipInstanceId(2),
                definition,
                "USS Wayfarer",
                new TacticalPosition(-2, 4),
                stopped,
                damaged,
                new AtLocationStart(new LocationId("vesper-reach")),
                new SensorRepairStart(damaged, repaired, initial)
            ),
            new(
                new ShipInstanceId(3),
                definition,
                "USS Horizon",
                new TacticalPosition(6, 1.5),
                stopped,
                repaired,
                new TravelingStart(new LocationId("vesper-reach"), new LocationId("meridian-drift"), initial)
            ),
        ];
    }

    private static StrategicMap CreateMap()
    {
        var dawn = new StrategicLocation(
            new LocationId("dawn-anchor"),
            "Dawn Anchor",
            new StrategicMapPosition(-5.5, 2.25)
        );
        var vesper = new StrategicLocation(
            new LocationId("vesper-reach"),
            "Vesper Reach",
            new StrategicMapPosition(8.125, 11.75)
        );
        var meridian = new StrategicLocation(
            new LocationId("meridian-drift"),
            "Meridian Drift",
            new StrategicMapPosition(17.4, -3.6)
        );
        return new StrategicMap(
            [dawn, vesper, meridian],
            [
                new StrategicRoute(dawn.Id, vesper.Id, new SimulationDuration(12000)),
                new StrategicRoute(vesper.Id, meridian.Id, new SimulationDuration(14000)),
            ]
        );
    }

    private static StrategicMap CreateMaximumMap()
    {
        StrategicLocation[] locations =
        [
            .. Enumerable
                .Range(0, 256)
                .Select(index => new StrategicLocation(
                    new LocationId(CreateMaximumIdentity($"location-{index:D3}")),
                    new string('\u0080', 64),
                    new StrategicMapPosition(index, -index)
                )),
        ];
        var routes = new List<StrategicRoute>(1024);
        for (int origin = 0; origin < locations.Length && routes.Count < routes.Capacity; origin++)
        {
            for (
                int destination = origin + 1;
                destination < locations.Length && routes.Count < routes.Capacity;
                destination++
            )
            {
                routes.Add(
                    new StrategicRoute(locations[origin].Id, locations[destination].Id, new SimulationDuration(100))
                );
            }
        }

        return new StrategicMap(locations, routes);
    }

    private static string CreateMaximumIdentity(string prefix) => prefix.PadRight(128, 'x');

    private static IEnumerable<T> OverflowAfter<T>(T value, int yieldedCount)
    {
        for (int index = 0; index < yieldedCount; index++)
        {
            yield return value;
        }

        throw new InvalidOperationException("The bounded consumer enumerated past its rejection threshold.");
    }

    private static ShipDefinitionCatalog CreateCatalog(string definitionId = "pathfinder")
    {
        string definition = $$"""
            {
              "schemaVersion": 3,
              "id": "{{definitionId}}",
              "designDisplayName": "Pathfinder class",
              "maximumTacticalSpeedKilometersPerSecond": 10,
              "passiveSensorRangeKilometers": 30.0,
              "activeScanDurationMilliseconds": 2000,
              "sensorRepairDurationMilliseconds": 8000
            }
            """;
        string schema = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "src/AlterCourse.Godot/content/schemas/ship-definition-v3.schema.json")
        );
        return new ShipDefinitionCatalogLoader(schema).LoadCatalog([
            ShipDefinitionContent.FromText("pathfinder.json", definition),
        ]);
    }

    private static JsonObject Parse(GameSimulation simulation) =>
        JsonNode.Parse(GamePersistence.Serialize(simulation, Metadata))!.AsObject();

    private static JsonArray Ships(JsonObject root) => root["simulation"]!["ships"]!.AsArray();

    private static JsonNode WorkForShip(JsonObject root, ShipInstanceId shipId) =>
        root["simulation"]!["scheduler"]!["outstandingWork"]!
            .AsArray()
            .Single(work => work!["targetShipId"]!.GetValue<long>() == shipId.Value)!;

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
