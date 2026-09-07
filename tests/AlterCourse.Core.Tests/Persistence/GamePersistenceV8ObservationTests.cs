using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;
using AlterCourse.Core.Tests.Gameplay;
using FactionTestWorld = AlterCourse.Core.Tests.Gameplay.FactionBootstrapTests.FactionTestWorld;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies V8 observation snapshots and the strict adjacent V7 migration.</summary>
public sealed class GamePersistenceV8ObservationTests
{
    /// <summary>V7 migration preserves prior authority while inventing no observation-response history.</summary>
    [Fact]
    public void MigratesV7AdjacentlyWithDisabledEmptyObservationState()
    {
        GameSimulation current = FirstGameSetup.Create(
            new Milestone3ProofFixture().Catalog,
            FactionTestWorld.FactionCatalog
        );
        SimulationState assigned = GameSimulation
            .AdvanceTo(
                current.CaptureState(),
                new SimulationTime(0),
                new Milestone3ProofFixture().Catalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        current = GameSimulation.RestoreState(
            assigned,
            new Milestone3ProofFixture().Catalog,
            FactionTestWorld.FactionCatalog
        );
        JsonObject v7 = ToHistoricalV7(GamePersistence.Serialize(current, Milestone3ProofFixture.Metadata));
        JsonNode originalSimulation = v7["simulation"]!;

        LoadedGameSave loaded = GamePersistence.Deserialize(
            Encoding.UTF8.GetBytes(v7.ToJsonString()),
            new Milestone3ProofFixture().Catalog,
            FactionTestWorld.FactionCatalog,
            "adjacent-v7.json"
        );
        JsonObject migrated = Parse(GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
        JsonNode migratedSimulation = migrated["simulation"]!;

        Assert.Equal(8, migrated["schemaVersion"]!.GetValue<int>());
        Assert.Equal("observation-driven-faction-response-v1", migrated["simulationRulesVersion"]!.GetValue<string>());
        Assert.Equal(1, migratedSimulation["observationReportAllocatorNextId"]!.GetValue<long>());
        Assert.Equal(
            originalSimulation["timeMilliseconds"]!.ToJsonString(),
            migratedSimulation["timeMilliseconds"]!.ToJsonString()
        );
        Assert.Equal(
            originalSimulation["shipAllocatorNextId"]!.ToJsonString(),
            migratedSimulation["shipAllocatorNextId"]!.ToJsonString()
        );
        Assert.Equal(
            originalSimulation["orderAllocatorNextId"]!.ToJsonString(),
            migratedSimulation["orderAllocatorNextId"]!.ToJsonString()
        );
        Assert.Equal(originalSimulation["scheduler"]!.ToJsonString(), migratedSimulation["scheduler"]!.ToJsonString());
        Assert.Equal(originalSimulation["ships"]!.ToJsonString(), migratedSimulation["ships"]!.ToJsonString());
        Assert.All(
            migratedSimulation["factions"]!.AsArray(),
            faction => AssertEmptyDisabledObservation(faction!["observation"]!.AsObject())
        );
        Assert.NotNull(migratedSimulation["ships"]![3]!["sensorKnowledge"]!["contacts"]![0]);
        Assert.Contains(
            migratedSimulation["ships"]!.AsArray(),
            ship => ship!["activeOrder"] is not null && ship["directControllerFactionId"] is not null
        );
    }

    /// <summary>V7 source validation remains frozen even after V8 admits a new rules identity and work kind.</summary>
    [Theory]
    [InlineData("rules")]
    [InlineData("report-delivery")]
    public void RejectsV8TokensInjectedIntoHistoricalV7(string mutation)
    {
        JsonObject v7 = ToHistoricalV7(
            GamePersistence.Serialize(
                FirstGameSetup.Create(new Milestone3ProofFixture().Catalog, FactionTestWorld.FactionCatalog),
                Milestone3ProofFixture.Metadata
            )
        );
        if (string.Equals(mutation, "rules", StringComparison.Ordinal))
        {
            v7["simulationRulesVersion"] = "observation-driven-faction-response-v1";
        }
        else
        {
            v7["simulation"]!["scheduler"]!["outstandingWork"]![0]!["kind"] = "reportDelivery";
        }

        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(
                Encoding.UTF8.GetBytes(v7.ToJsonString()),
                new Milestone3ProofFixture().Catalog,
                FactionTestWorld.FactionCatalog,
                $"historical-v7-{mutation}.json"
            )
        );
    }

    /// <summary>V8 remains compatible with a world that has no political state.</summary>
    [Fact]
    public void RoundTripsZeroFactionWorld()
    {
        var fixture = new Milestone3ProofFixture();
        byte[] saved = GamePersistence.Serialize(fixture.CreateDefault(), Milestone3ProofFixture.Metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(saved, fixture.Catalog, "zero-factions-v8.json");
        JsonObject root = Parse(saved);

        Assert.Equal(8, root["schemaVersion"]!.GetValue<int>());
        Assert.Empty(root["simulation"]!["factions"]!.AsArray());
        Assert.Equal(1, root["simulation"]!["observationReportAllocatorNextId"]!.GetValue<long>());
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
    }

    /// <summary>All actor-safe report fields survive a bounded received-state round trip.</summary>
    [Fact]
    public void RoundTripsBoundedReceivedReportWithHistoricalIdentification()
    {
        GameSimulation game = CreateReceivedReportGame();
        byte[] saved = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(
            saved,
            FactionTestWorld.ShipCatalog,
            FactionTestWorld.FactionCatalog,
            "received-report-v8.json"
        );
        JsonObject simulation = Parse(saved)["simulation"]!.AsObject();
        JsonObject observation = simulation["factions"]![1]!["observation"]!.AsObject();
        JsonObject received = observation["receivedReports"]![0]!.AsObject();
        JsonObject report = received["report"]!.AsObject();

        Assert.Equal(2, simulation["observationReportAllocatorNextId"]!.GetValue<long>());
        Assert.Equal("enabled", observation["posture"]!.GetValue<string>());
        Assert.Equal(1, report["reportId"]!.GetValue<long>());
        Assert.Equal(4, report["observerShipId"]!.GetValue<long>());
        Assert.Equal(37, report["observerContactId"]!.GetValue<long>());
        Assert.Equal("beta", report["observedAtLocationId"]!.GetValue<string>());
        Assert.Equal(12.5, report["observedPosition"]!["xKilometers"]!.GetValue<double>());
        Assert.Equal(-4.25, report["observedPosition"]!["yKilometers"]!.GetValue<double>());
        Assert.Equal(0, report["observedAtMilliseconds"]!.GetValue<long>());
        Assert.Equal("identified", report["identification"]!.GetValue<string>());
        Assert.Equal("Observed Vessel", report["knownVesselDisplayName"]!.GetValue<string>());
        Assert.Equal("Observed Design", report["knownDesignDisplayName"]!.GetValue<string>());
        Assert.Equal(2_000, received["receivedAtMilliseconds"]!.GetValue<long>());
        Assert.Equal("handled", received["handling"]!.GetValue<string>());
        Assert.Equal("beta", observation["completionWatermarks"]![0]!["locationId"]!.GetValue<string>());
        Assert.Equal(2_000, observation["completionWatermarks"]![0]!["observedThroughMilliseconds"]!.GetValue<long>());
        Assert.Null(report["targetShipId"]);
        Assert.Null(report["targetControllerFactionId"]);
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
    }

    /// <summary>Required members, finite bounds, enums, chronology, and actor-safe shape fail before construction.</summary>
    [Theory]
    [InlineData("missing-allocator")]
    [InlineData("missing-observation")]
    [InlineData("null-received")]
    [InlineData("unknown-posture")]
    [InlineData("unknown-handling")]
    [InlineData("future-receipt")]
    [InlineData("hidden-target")]
    [InlineData("too-many-received")]
    public void RejectsMalformedV8ObservationShape(string mutation)
    {
        GameSimulation game = CreateReceivedReportGame();
        byte[] before = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        JsonObject root = Parse(before);
        JsonObject simulation = root["simulation"]!.AsObject();
        JsonObject faction = simulation["factions"]![1]!.AsObject();
        JsonObject observation = faction["observation"]!.AsObject();
        JsonObject received = observation["receivedReports"]![0]!.AsObject();
        switch (mutation)
        {
            case "missing-allocator":
                simulation.Remove("observationReportAllocatorNextId");
                break;
            case "missing-observation":
                faction.Remove("observation");
                break;
            case "null-received":
                observation["receivedReports"] = null;
                break;
            case "unknown-posture":
                observation["posture"] = "unknown";
                break;
            case "unknown-handling":
                received["handling"] = "unknown";
                break;
            case "future-receipt":
                received["receivedAtMilliseconds"] = 2_100;
                break;
            case "hidden-target":
                received["report"]!["targetShipId"] = 1;
                break;
            case "too-many-received":
                JsonArray reports = observation["receivedReports"]!.AsArray();
                for (int index = 2; index <= 17; index++)
                {
                    JsonObject extra = received.DeepClone().AsObject();
                    extra["report"]!["reportId"] = index;
                    reports.Add(extra);
                }
                simulation["observationReportAllocatorNextId"] = 18;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
        }

        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(
                Encoding.UTF8.GetBytes(root.ToJsonString()),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog,
                $"malformed-v8-{mutation}.json"
            )
        );
        Assert.Equal(before, GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata));
    }

    private static GameSimulation CreateReceivedReportGame()
    {
        GameSimulation initial = FactionTestWorld
            .CreateBootstrap(
                [
                    new FactionStart(FactionTestWorld.FactionA, FactionTestWorld.DefinitionA),
                    new FactionStart(FactionTestWorld.FactionB, FactionTestWorld.DefinitionB),
                ],
                controlledShips: true
            )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
        initial.AdvanceFixedSteps(20);
        SimulationState state = initial.CaptureState();
        ShipState reportingShip = state.GetRequiredShip(new ShipInstanceId(4));
        state = state.ReplaceShip(
            reportingShip.InstanceId,
            reportingShip with
            {
                SensorKnowledge = new SensorKnowledge(38, []),
            }
        );
        var report = new ObservationReportSnapshot(
            new ObservationReportId(1),
            new ShipInstanceId(4),
            new SensorContactId(37),
            FactionTestWorld.Beta,
            new TacticalPosition(12.5, -4.25),
            new SimulationTime(0),
            SensorContactIdentification.Identified,
            "Observed Vessel",
            "Observed Design"
        );
        FactionState faction = state.GetRequiredFaction(FactionTestWorld.FactionB);
        var observation = new FactionObservationState(
            ObservationResponsePosture.Enabled,
            receivedReports:
            [
                new ReceivedObservationReport(report, new SimulationTime(2_000), ObservationReportHandling.Handled),
            ],
            completionWatermarks:
            [
                new ObservationLocationCompletionWatermark(FactionTestWorld.Beta, new SimulationTime(2_000)),
            ]
        );
        state = state.ReplaceFaction(FactionTestWorld.FactionB, faction with { Observation = observation }) with
        {
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(2),
        };
        return GameSimulation.RestoreState(state, FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
    }

    private static JsonObject ToHistoricalV7(byte[] current)
    {
        JsonObject root = Parse(current);
        root["schemaVersion"] = 7;
        root["simulationRulesVersion"] = "faction-intent-autonomous-assignment-v1";
        JsonObject simulation = root["simulation"]!.AsObject();
        simulation.Remove("observationReportAllocatorNextId");
        foreach (JsonNode? faction in simulation["factions"]!.AsArray())
        {
            faction!.AsObject().Remove("observation");
        }
        return root;
    }

    private static void AssertEmptyDisabledObservation(JsonObject observation)
    {
        Assert.Equal("disabled", observation["posture"]!.GetValue<string>());
        Assert.Empty(observation["inFlightReports"]!.AsArray());
        Assert.Empty(observation["receivedReports"]!.AsArray());
        Assert.Null(observation["activeInvestigation"]);
        Assert.Empty(observation["completionWatermarks"]!.AsArray());
    }

    private static JsonObject Parse(byte[] json) => JsonNode.Parse(json)!.AsObject();
}
