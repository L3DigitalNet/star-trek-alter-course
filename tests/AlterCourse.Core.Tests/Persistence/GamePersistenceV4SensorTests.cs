using System.Text;
using System.Text.Json.Nodes;
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

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies V4 sensor knowledge, scan, posture, and exact scheduler persistence.</summary>
public sealed class GamePersistenceV4SensorTests
{
    private static readonly ShipDefinitionId DefinitionId = new("pathfinder");
    private static readonly LocationId Location = new("alpha");
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 3, 0, 0, 0, TimeSpan.Zero);

    /// <summary>Confirms populated inert V4 state round trips with stable canonical bytes.</summary>
    [Fact]
    public void RoundTripsPopulatedSensorStateWithStableOrdering()
    {
        GameSimulation simulation = CreatePopulatedSimulation();
        byte[] first = GamePersistence.Serialize(simulation, Metadata());
        LoadedGameSave loaded = GamePersistence.Deserialize(first, Catalog(), "populated-v4.json");
        byte[] second = GamePersistence.Serialize(loaded.Simulation, loaded.Metadata);
        JsonObject root = Parse(second);
        JsonArray ships = root["simulation"]!["ships"]!.AsArray();

        Assert.Equal(first, second);
        Assert.Equal(4, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("sensor-knowledge-first-contact-v1", root["simulationRulesVersion"]!.GetValue<string>());
        Assert.Equal(
            "identified",
            ships[0]!["sensorKnowledge"]!["contacts"]![0]!["identification"]!.GetValue<string>()
        );
        Assert.Equal("stale", ships[1]!["sensorKnowledge"]!["contacts"]![0]!["status"]!.GetValue<string>());
        Assert.Equal(1, ships[1]!["sensorKnowledge"]!["contacts"]![0]!["lossWorkId"]!.GetValue<long>());
        Assert.Equal(2, ships[2]!["sensorKnowledge"]!["activeScan"]!["scheduledCompletionId"]!.GetValue<long>());
        Assert.Equal("cautiousContact", ships[1]!["autonomousState"]!["contactPosture"]!.GetValue<string>());
        Assert.Equal(
            3,
            ships[1]!["autonomousState"]!["pendingContactDecisionWake"]!["scheduledWorkId"]!.GetValue<long>()
        );
    }

    /// <summary>Confirms V3 migration preserves orders and work while supplying inert V4 defaults.</summary>
    [Fact]
    public void MigratesV3ToEmptySensorStateWithoutInventingWork()
    {
        LoadedGameSave loaded = GamePersistence.Deserialize(CreateV3HoldSave(), Catalog(), "v3-hold.json");
        JsonObject current = Parse(GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
        JsonNode simulation = current["simulation"]!;
        JsonNode npc = simulation["ships"]![1]!;

        Assert.Equal(4, current["schemaVersion"]!.GetValue<int>());
        Assert.Equal("holdUntil", npc["activeOrder"]!["kind"]!.GetValue<string>());
        Assert.Equal("orderWake", simulation["scheduler"]!["outstandingWork"]![0]!["kind"]!.GetValue<string>());
        Assert.Equal(1, npc["sensorKnowledge"]!["nextContactId"]!.GetValue<long>());
        Assert.Empty(npc["sensorKnowledge"]!["contacts"]!.AsArray());
        Assert.Null(npc["sensorKnowledge"]!["activeScan"]);
        Assert.Null(npc["autonomousState"]!["contactPosture"]);
        Assert.Null(npc["autonomousState"]!["pendingContactDecisionWake"]);
        Assert.Single(simulation["scheduler"]!["outstandingWork"]!.AsArray());
    }

    /// <summary>Confirms malformed V4 knowledge and work graphs fail before another simulation can change.</summary>
    [Theory]
    [InlineData("self-target")]
    [InlineData("unknown-target")]
    [InlineData("zero-id")]
    [InlineData("duplicate-target")]
    [InlineData("allocator")]
    [InlineData("future-observation")]
    [InlineData("status")]
    [InlineData("identification")]
    [InlineData("overlength-name")]
    [InlineData("loss-due")]
    [InlineData("current-loss")]
    [InlineData("scan-work")]
    [InlineData("scan-target")]
    [InlineData("posture")]
    [InlineData("decision-work")]
    [InlineData("orphan-work")]
    public void RejectsMalformedV4Correlations(string mutation)
    {
        byte[] valid = GamePersistence.Serialize(CreatePopulatedSimulation(), Metadata());
        byte[] invalid = Mutate(valid, root => ApplyMutation(root, mutation));

        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(invalid, Catalog(), $"{mutation}.json")
        );
        Assert.Equal(valid, GamePersistence.Serialize(CreatePopulatedSimulation(), Metadata()));
    }

    /// <summary>Confirms work introduced by V4 is rejected when relabeled as a V3 payload.</summary>
    [Fact]
    public void RejectsV4WorkKindUnderV3Schema()
    {
        byte[] invalid = Mutate(
            CreateV3HoldSave(),
            root => root["simulation"]!["scheduler"]!["outstandingWork"]![0]!["kind"] = "sensorContactLoss"
        );

        Assert.Throws<GamePersistenceException>(() => GamePersistence.Deserialize(invalid, Catalog(), "v3-kind.json"));
    }

    /// <summary>Confirms nonfinite observed coordinates are rejected by the bounded JSON contract.</summary>
    [Fact]
    public void RejectsNonfiniteObservedPosition()
    {
        string valid = Encoding.UTF8.GetString(GamePersistence.Serialize(CreatePopulatedSimulation(), Metadata()));
        string invalid = valid.Replace("\"xKilometers\": 1", "\"xKilometers\": 1e999", StringComparison.Ordinal);

        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(Encoding.UTF8.GetBytes(invalid), Catalog(), "nonfinite-contact.json")
        );
    }

    /// <summary>Confirms the admitted cap leaves scheduler room for every compatible per-ship work kind.</summary>
    [Fact]
    public void ContactCapRespectsSchedulerMaximumAndScanCardinality()
    {
        const int maximumShips = 256;
        int contactLossMaximum = SensorKnowledge.MaximumContactsPerObserver * maximumShips;
        int activeScanMaximum = maximumShips;
        int totalCompatibleWork = contactLossMaximum + 766;
        int totalWithOneScannedContactPerShip = (contactLossMaximum - activeScanMaximum) + activeScanMaximum + 766;

        Assert.Equal(3072, contactLossMaximum);
        Assert.Equal(256, activeScanMaximum);
        Assert.Equal(3838, totalCompatibleWork);
        Assert.Equal(totalCompatibleWork, totalWithOneScannedContactPerShip);
        Assert.InRange(totalCompatibleWork, 1, SimulationScheduler.MaximumOutstandingWork);
    }

    private static GameSimulation CreatePopulatedSimulation()
    {
        var playerId = new ShipInstanceId(1);
        var firstNpcId = new ShipInstanceId(2);
        var secondNpcId = new ShipInstanceId(3);
        (SimulationScheduler scheduler, ScheduledWork loss, ScheduledWork scan, ScheduledWork decision) =
            CreateContactScheduler(firstNpcId, secondNpcId);

        ShipState player = Ship(playerId, "Player") with
        {
            SensorKnowledge = new SensorKnowledge(
                2,
                [Contact(1, firstNpcId, "NPC One", SensorContactIdentification.Identified)]
            ),
        };
        ShipState firstNpc = Ship(firstNpcId, "NPC One") with
        {
            SensorKnowledge = new SensorKnowledge(
                2,
                [
                    Contact(1, playerId, "Player") with
                    {
                        Status = SensorContactStatus.Stale,
                        LossWorkId = loss.Id,
                        LossDueTime = loss.DueTime,
                    },
                ]
            ),
            AutonomousState = new ShipAutonomousState(
                ShipContactPosture.CautiousContact,
                new ShipContactDecisionWake(decision.Id, decision.DueTime)
            ),
        };
        ShipState secondNpc = Ship(secondNpcId, "NPC Two") with
        {
            SensorKnowledge = new SensorKnowledge(
                2,
                [Contact(1, playerId, "Player")],
                new ActiveSensorScanState(new SensorContactId(1), new SimulationTime(100), scan.DueTime, scan.Id)
            ),
        };
        var state = new SimulationState(
            new SimulationTime(100),
            scheduler,
            ShipInstanceIdAllocator.Restore(4),
            new StrategicMap([new StrategicLocation(Location, "Alpha", default)], []),
            playerId,
            [secondNpc, player, firstNpc]
        );
        return GameSimulation.RestoreState(state, Catalog());
    }

    private static (
        SimulationScheduler Scheduler,
        ScheduledWork Loss,
        ScheduledWork Scan,
        ScheduledWork Decision
    ) CreateContactScheduler(ShipInstanceId firstNpcId, ShipInstanceId secondNpcId)
    {
        var scheduler = SimulationScheduler.Create();
        (scheduler, ScheduledWork loss) = scheduler.Schedule(
            new SimulationTime(500),
            firstNpcId,
            ScheduledWorkKind.SensorContactLoss
        );
        (scheduler, ScheduledWork scan) = scheduler.Schedule(
            new SimulationTime(2100),
            secondNpcId,
            ScheduledWorkKind.ActiveSensorScanCompletion
        );
        (scheduler, ScheduledWork decision) = scheduler.Schedule(
            new SimulationTime(100),
            firstNpcId,
            ScheduledWorkKind.ShipContactDecisionWake
        );
        return (scheduler, loss, scan, decision);
    }

    private static SensorContactTrack Contact(
        long id,
        ShipInstanceId target,
        string targetName,
        SensorContactIdentification identification = SensorContactIdentification.Detected
    ) =>
        new(
            new SensorContactId(id),
            target,
            new TacticalPosition(id, -id),
            new SimulationTime(100),
            SensorContactStatus.Current,
            identification,
            identification == SensorContactIdentification.Identified ? targetName : null,
            identification == SensorContactIdentification.Identified ? "Pathfinder" : null
        );

    private static ShipState Ship(ShipInstanceId id, string name) =>
        new(id, DefinitionId, name, default, default, new SensorIntegrity(1), null, new AtLocationState(Location));

    private static void ApplyMutation(JsonObject root, string mutation)
    {
        JsonNode simulation = root["simulation"]!;
        JsonNode playerKnowledge = simulation["ships"]![0]!["sensorKnowledge"]!;
        JsonNode firstNpcKnowledge = simulation["ships"]![1]!["sensorKnowledge"]!;
        JsonNode firstNpcContact = firstNpcKnowledge["contacts"]![0]!;
        switch (mutation)
        {
            case "self-target":
                firstNpcContact["targetShipId"] = 2;
                break;
            case "unknown-target":
                firstNpcContact["targetShipId"] = 99;
                break;
            case "zero-id":
                firstNpcContact["id"] = 0;
                break;
            case "duplicate-target":
                AddDuplicateTarget(playerKnowledge);
                break;
            case "allocator":
                playerKnowledge["nextContactId"] = 1;
                break;
            case "future-observation":
                firstNpcContact["lastObservedAtMilliseconds"] = 200;
                break;
            case "status":
                firstNpcContact["status"] = "unknown";
                break;
            case "identification":
                firstNpcContact["identification"] = "identified";
                break;
            case "overlength-name":
                AddOverlengthIdentity(firstNpcContact);
                break;
            case "loss-due":
                firstNpcContact["lossDueTimeMilliseconds"] = 600;
                break;
            case "current-loss":
                firstNpcContact["status"] = "current";
                break;
            case "scan-work":
                simulation["ships"]![2]!["sensorKnowledge"]!["activeScan"]!["scheduledCompletionId"] = 99;
                break;
            case "scan-target":
                simulation["ships"]![2]!["sensorKnowledge"]!["activeScan"]!["targetContactId"] = 99;
                break;
            case "posture":
                simulation["ships"]![1]!["autonomousState"]!["contactPosture"] = null;
                break;
            case "decision-work":
                simulation["ships"]![1]!["autonomousState"]!["pendingContactDecisionWake"]!["scheduledWorkId"] = 99;
                break;
            case "orphan-work":
                RemoveLossState(firstNpcContact);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }
    }

    private static void AddDuplicateTarget(JsonNode knowledge)
    {
        knowledge["contacts"]!.AsArray().Add(knowledge["contacts"]![0]!.DeepClone());
        knowledge["contacts"]![1]!["id"] = 2;
        knowledge["nextContactId"] = 3;
    }

    private static void AddOverlengthIdentity(JsonNode contact)
    {
        contact["identification"] = "identified";
        contact["knownVesselDisplayName"] = new string('v', 65);
        contact["knownDesignDisplayName"] = "Pathfinder";
    }

    private static void RemoveLossState(JsonNode contact)
    {
        contact["lossWorkId"] = null;
        contact["lossDueTimeMilliseconds"] = null;
        contact["status"] = "lost";
    }

    private static byte[] CreateV3HoldSave() =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion": 3, "simulationRulesVersion": "active-world-orders-v1",
              "metadata": { "saveId": "v3", "displayName": "V3", "createdAtUtc": "2026-09-03T00:00:00+00:00", "savedAtUtc": "2026-09-03T00:00:00+00:00" },
              "simulation": {
                "timeMilliseconds": 100, "shipAllocatorNextId": 3, "orderAllocatorNextId": 2, "playerShipId": 1,
                "scheduler": { "nextWorkId": 2, "nextSequence": 1, "outstandingWork": [
                  { "id": 1, "dueTimeMilliseconds": 500, "sequence": 0, "kind": "orderWake", "targetShipId": 2 }
                ] },
                "strategicMap": { "locations": [{ "id": "alpha", "displayName": "Alpha", "position": { "xUnitless": 0, "yUnitless": 0 } }], "routes": [] },
                "ships": [
                  {{ShipJson(1, "Player", "null")}},
                  {{ShipJson(
                2,
                "NPC",
                "{ \"kind\": \"holdUntil\", \"id\": 1, \"untilMilliseconds\": 500, \"scheduledWakeId\": 1 }"
            )}}
                ]
              }
            }
            """
        );

    private static string ShipJson(long id, string name, string order) =>
        $$"""{ "instanceId": {{id}}, "definitionId": "pathfinder", "displayName": "{{name}}", "tacticalPosition": { "xKilometers": 0, "yKilometers": 0 }, "tacticalMotion": { "headingDegrees": 0, "speedKilometersPerSecond": 0 }, "sensorIntegrity": 1, "sensorRepair": null, "strategicState": { "kind": "atLocation", "locationId": "alpha", "travel": null }, "activeOrder": {{order}} }""";

    private static byte[] Mutate(byte[] source, Action<JsonObject> mutation)
    {
        JsonObject root = Parse(source);
        mutation(root);
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static JsonObject Parse(byte[] json) => JsonNode.Parse(json)!.AsObject();

    private static GameSaveMetadata Metadata() => new("slot", "Sensors", Timestamp, Timestamp);

    private static ShipDefinitionCatalog Catalog() =>
        new(
            new Dictionary<ShipDefinitionId, ShipDefinition>
            {
                [DefinitionId] = new ShipDefinition(
                    DefinitionId,
                    "Pathfinder",
                    new SpeedKilometersPerSecond(10),
                    new DistanceKilometers(30),
                    new SimulationDuration(2000),
                    new SimulationDuration(8000)
                ),
            }
        );
}
