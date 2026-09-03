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
        Assert.Equal(5, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("engineering-backbone-v1", root["simulationRulesVersion"]!.GetValue<string>());
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

        Assert.Equal(5, current["schemaVersion"]!.GetValue<int>());
        Assert.Equal("holdUntil", npc["activeOrder"]!["kind"]!.GetValue<string>());
        Assert.Equal("orderWake", simulation["scheduler"]!["outstandingWork"]![0]!["kind"]!.GetValue<string>());
        Assert.Equal(1, npc["sensorKnowledge"]!["nextContactId"]!.GetValue<long>());
        Assert.Empty(npc["sensorKnowledge"]!["contacts"]!.AsArray());
        Assert.Null(npc["sensorKnowledge"]!["activeScan"]);
        Assert.Null(npc["autonomousState"]!["contactPosture"]);
        Assert.Null(npc["autonomousState"]!["pendingContactDecisionWake"]);
        Assert.Single(simulation["scheduler"]!["outstandingWork"]!.AsArray());
    }

    /// <summary>Confirms a fully populated native V4 graph migrates without losing identity or work order.</summary>
    [Fact]
    public void MigratesPopulatedNativeV4GraphAndContinuesDeterministically()
    {
        byte[] fixture = CreatePopulatedNativeV4Save();
        JsonObject source = Parse(fixture);
        Assert.Equal(4, source["schemaVersion"]!.GetValue<int>());
        Assert.Equal("sensor-knowledge-first-contact-v1", source["simulationRulesVersion"]!.GetValue<string>());

        LoadedGameSave migrated = GamePersistence.Deserialize(fixture, Catalog(), "native-populated-v4.json");
        AssertPopulatedNativeV4Migration(migrated.Simulation);

        byte[] migratedV5 = GamePersistence.Serialize(migrated.Simulation, migrated.Metadata);
        AssertCurrentOrdering(migratedV5);
        LoadedGameSave resumed = GamePersistence.Deserialize(migratedV5, Catalog(), "native-populated-v5.json");

        AssertNativeV4Continuation(migrated, resumed);
    }

    private static void AssertPopulatedNativeV4Migration(GameSimulation simulation)
    {
        SimulationState state = simulation.CaptureState();
        ShipState player = state.GetRequiredShip(new ShipInstanceId(1));
        ShipState cautiousNpc = state.GetRequiredShip(new ShipInstanceId(2));
        SensorContactTrack learned = Assert.Single(player.SensorKnowledge.Contacts, contact => contact.Id.Value == 7);
        ActiveSensorScanState scan = Assert.IsType<ActiveSensorScanState>(player.SensorKnowledge.ActiveScan);
        SystemRepairState repair = Assert.IsType<SystemRepairState>(player.Engineering.ActiveRepair);
        ShipContactDecisionWake wake = Assert.IsType<ShipContactDecisionWake>(
            cautiousNpc.AutonomousState.PendingContactDecisionWake
        );

        Assert.Equal(new SimulationTime(100), state.Time);
        Assert.Equal([1L, 2L, 3L], state.Ships.Select(ship => ship.InstanceId.Value));
        Assert.Equal(9, player.SensorKnowledge.NextContactId);
        Assert.Equal(new ShipInstanceId(2), learned.TargetShipId);
        Assert.Equal(SensorContactIdentification.Identified, learned.Identification);
        Assert.Equal("NPC One", learned.KnownVesselDisplayName);
        Assert.Equal("Pathfinder", learned.KnownDesignDisplayName);
        Assert.Equal(new SensorContactId(8), scan.TargetContactId);
        Assert.Equal(new SimulationTime(100), scan.StartedAt);
        Assert.Equal(new SimulationTime(2_100), scan.ExpectedCompletion);
        Assert.Equal(new ScheduledWorkId(2), scan.ScheduledCompletionId);
        Assert.Equal(ShipSystemId.Sensors, repair.TargetSystem);
        Assert.Equal(new SystemCondition(0.5), repair.StartingCondition);
        Assert.Equal(new SystemCondition(1), repair.TargetCondition);
        Assert.Equal(new SimulationTime(100), repair.StartedAt);
        Assert.Equal(new SimulationTime(8_100), repair.ExpectedCompletion);
        Assert.Equal(new ScheduledWorkId(3), repair.ScheduledCompletionId);
        Assert.Equal(ShipContactPosture.CautiousContact, cautiousNpc.AutonomousState.ContactPosture);
        Assert.Equal(new ScheduledWorkId(1), wake.ScheduledWorkId);
        Assert.Equal(new SimulationTime(100), wake.DueTime);
        Assert.Equal(
            [
                (1L, 100L, 0L, 2L, ScheduledWorkKind.ShipContactDecisionWake),
                (2L, 2_100L, 1L, 1L, ScheduledWorkKind.ActiveSensorScanCompletion),
                (3L, 8_100L, 2L, 1L, ScheduledWorkKind.SystemRepairCompletion),
            ],
            state.Scheduler.OutstandingWork.Select(work =>
                (work.Id.Value, work.DueTime.Milliseconds, work.Sequence, work.TargetShipId.Value, work.Kind)
            )
        );
    }

    private static void AssertCurrentOrdering(byte[] migratedV5)
    {
        JsonObject current = Parse(migratedV5);
        Assert.Equal(5, current["schemaVersion"]!.GetValue<int>());
        Assert.Equal(
            [1L, 2L, 3L],
            current["simulation"]!["ships"]!.AsArray().Select(ship => ship!["instanceId"]!.GetValue<long>())
        );
        Assert.Equal(
            [7L, 8L],
            current["simulation"]!["ships"]![0]!["sensorKnowledge"]!["contacts"]!
                .AsArray()
                .Select(contact => contact!["id"]!.GetValue<long>())
        );
    }

    private static void AssertNativeV4Continuation(LoadedGameSave migrated, LoadedGameSave resumed)
    {
        Assert.Equal(migrated.Simulation.AdvanceFixedSteps(1), resumed.Simulation.AdvanceFixedSteps(1));

        SimulationAdvanceResult scanCompletion = migrated.Simulation.AdvanceFixedSteps(19);
        Assert.Equal(scanCompletion, resumed.Simulation.AdvanceFixedSteps(19));
        Assert.Contains(
            scanCompletion.ResolvedEvents,
            item =>
                item.Kind == PlayerAdvanceEventKind.ActiveSensorScanCompleted
                && item.SensorContactId == new SensorContactId(8)
                && item.OccurredAt == new SimulationTime(2_100)
        );

        SimulationAdvanceResult repairCompletion = migrated.Simulation.AdvanceFixedSteps(60);
        Assert.Equal(repairCompletion, resumed.Simulation.AdvanceFixedSteps(60));
        Assert.Contains(
            repairCompletion.ResolvedEvents,
            item =>
                item.Kind == PlayerAdvanceEventKind.SystemRepairCompleted
                && item.ShipSystemId == ShipSystemId.Sensors
                && item.OccurredAt == new SimulationTime(8_100)
        );
        Assert.Equal(
            GamePersistence.Serialize(migrated.Simulation, migrated.Metadata),
            GamePersistence.Serialize(resumed.Simulation, resumed.Metadata)
        );
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
        const int independentlyCorrelatedWorkKindsPerShip = 5;
        int contactLossMaximum = SensorKnowledge.MaximumContactsPerObserver * maximumShips;
        int conservativeMaximum = contactLossMaximum + (maximumShips * independentlyCorrelatedWorkKindsPerShip);

        Assert.Equal(255, SensorKnowledge.MaximumContactsPerObserver);
        Assert.Equal(65_280, contactLossMaximum);
        Assert.Equal(66_560, conservativeMaximum);
        Assert.Equal(SimulationScheduler.MaximumOutstandingWork, conservativeMaximum);
    }

    /// <summary>Confirms the maximum retained-contact graph has a bounded, stable V4 representation.</summary>
    [Fact]
    public void MaximumContactWorldRoundTripsWithinSaveEnvelope()
    {
        (GameSimulation simulation, ShipDefinitionCatalog catalog) = CreateMaximumContactSimulation();
        var metadata = new GameSaveMetadata(new string('\u0080', 128), new string('\u0080', 128), Timestamp, Timestamp);

        byte[] saved = GamePersistence.Serialize(simulation, metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(saved, catalog, "maximum-contacts-v4.json");

        Assert.Equal(95_677_740, saved.Length);
        Assert.InRange(saved.Length, 1, 128 * 1024 * 1024);
        Assert.Equal(256, loaded.Simulation.CaptureState().Ships.Length);
        Assert.All(
            loaded.Simulation.CaptureState().Ships,
            ship => Assert.Equal(255, ship.SensorKnowledge.Contacts.Length)
        );
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
    }

    private static (GameSimulation Simulation, ShipDefinitionCatalog Catalog) CreateMaximumContactSimulation()
    {
        string maximumIdentity = new string('d', ShipDefinitionId.MaximumLength);
        string maximumName = new('\u0080', ShipState.MaximumVesselDisplayNameLength);
        string maximumDesignName = new('\u0080', ShipDefinition.MaximumDesignDisplayNameLength);
        var definitionId = new ShipDefinitionId(maximumIdentity);
        var locationId = new LocationId(new string('l', 128));
        var catalog = new ShipDefinitionCatalog(
            new Dictionary<ShipDefinitionId, ShipDefinition>
            {
                [definitionId] = new ShipDefinition(
                    definitionId,
                    maximumDesignName,
                    new SpeedKilometersPerSecond(10),
                    new DistanceKilometers(30),
                    new SimulationDuration(2_000),
                    new SimulationDuration(8_000)
                ),
            }
        );
        var dueTime = new SimulationTime(5_000);
        var work = new ScheduledWork[SimulationState.MaximumShips * SensorKnowledge.MaximumContactsPerObserver];
        var ships = new ShipState[SimulationState.MaximumShips];
        long nextWorkId = 1;
        for (int observerIndex = 0; observerIndex < ships.Length; observerIndex++)
        {
            ships[observerIndex] = CreateMaximumContactShip(
                observerIndex,
                definitionId,
                locationId,
                maximumName,
                maximumDesignName,
                dueTime,
                work,
                ref nextWorkId
            );
        }

        var scheduler = SimulationScheduler.Restore(nextWorkId, nextWorkId - 1, work);
        var map = new StrategicMap([new StrategicLocation(locationId, new string('\u0080', 64), default)], []);
        var state = new SimulationState(
            new SimulationTime(0),
            scheduler,
            ShipInstanceIdAllocator.Restore(SimulationState.MaximumShips + 1L),
            map,
            new ShipInstanceId(1),
            ships
        );
        return (GameSimulation.RestoreState(state, catalog), catalog);
    }

    private static ShipState CreateMaximumContactShip(
        int observerIndex,
        ShipDefinitionId definitionId,
        LocationId locationId,
        string maximumName,
        string maximumDesignName,
        SimulationTime dueTime,
        ScheduledWork[] work,
        ref long nextWorkId
    )
    {
        var observerId = new ShipInstanceId(observerIndex + 1L);
        var contacts = new SensorContactTrack[SensorKnowledge.MaximumContactsPerObserver];
        int contactIndex = 0;
        for (int targetIndex = 0; targetIndex < SimulationState.MaximumShips; targetIndex++)
        {
            if (targetIndex == observerIndex)
            {
                continue;
            }

            var workId = new ScheduledWorkId(nextWorkId);
            contacts[contactIndex] = new SensorContactTrack(
                new SensorContactId(contactIndex + 1L),
                new ShipInstanceId(targetIndex + 1L),
                new TacticalPosition(targetIndex, -targetIndex),
                new SimulationTime(0),
                SensorContactStatus.Stale,
                SensorContactIdentification.Identified,
                maximumName,
                maximumDesignName,
                workId,
                dueTime
            );
            work[checked((int)(nextWorkId - 1))] = new ScheduledWork(
                workId,
                dueTime,
                nextWorkId - 1,
                observerId,
                ScheduledWorkKind.SensorContactLoss
            );
            nextWorkId++;
            contactIndex++;
        }

        return new ShipState(
            observerId,
            definitionId,
            maximumName,
            new TacticalPosition(observerIndex, -observerIndex),
            default,
            new ShipEngineeringState(
                new SystemCondition(1),
                new SystemCondition(1),
                new SystemCondition(1),
                new PowerAllocation(new(70), new(50))
            ),
            new AtLocationState(locationId),
            sensorKnowledge: new SensorKnowledge(SensorKnowledge.MaximumContactsPerObserver + 1L, contacts)
        );
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
        new(
            id,
            DefinitionId,
            name,
            default,
            default,
            new ShipEngineeringState(
                new SystemCondition(1),
                new SystemCondition(1),
                new SystemCondition(1),
                new PowerAllocation(new(70), new(50))
            ),
            new AtLocationState(Location)
        );

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

    private static byte[] CreatePopulatedNativeV4Save() =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion": 4,
              "simulationRulesVersion": "sensor-knowledge-first-contact-v1",
              "metadata": { "saveId": "native-v4", "displayName": "Native V4", "createdAtUtc": "2026-09-03T00:00:00+00:00", "savedAtUtc": "2026-09-03T00:00:00+00:00" },
              "simulation": {
                "timeMilliseconds": 100,
                "shipAllocatorNextId": 4,
                "orderAllocatorNextId": 1,
                "playerShipId": 1,
                "scheduler": {
                  "nextWorkId": 4,
                  "nextSequence": 3,
                  "outstandingWork": [
                    { "id": 1, "dueTimeMilliseconds": 100, "sequence": 0, "kind": "shipContactDecisionWake", "targetShipId": 2 },
                    { "id": 2, "dueTimeMilliseconds": 2100, "sequence": 1, "kind": "activeSensorScanCompletion", "targetShipId": 1 },
                    { "id": 3, "dueTimeMilliseconds": 8100, "sequence": 2, "kind": "sensorRepairCompletion", "targetShipId": 1 }
                  ]
                },
                "strategicMap": {
                  "locations": [{ "id": "alpha", "displayName": "Alpha", "position": { "xUnitless": 0, "yUnitless": 0 } }],
                  "routes": []
                },
                "ships": [
                  {{NativeV4PlayerJson()}},
                  {{NativeV4CautiousNpcJson()}},
                  {{NativeV4SecondNpcJson()}}
                ]
              }
            }
            """
        );

    private static string NativeV4PlayerJson() =>
        """
            {
              "instanceId": 1, "definitionId": "pathfinder", "displayName": "Player",
              "tacticalPosition": { "xKilometers": 0, "yKilometers": 0 },
              "tacticalMotion": { "headingDegrees": 0, "speedKilometersPerSecond": 0 },
              "sensorIntegrity": 0.5,
              "sensorRepair": { "startingIntegrity": 0.5, "targetIntegrity": 1, "startedAtMilliseconds": 100, "expectedCompletionMilliseconds": 8100, "scheduledCompletionId": 3 },
              "strategicState": { "kind": "atLocation", "locationId": "alpha", "travel": null }, "activeOrder": null,
              "sensorKnowledge": {
                "nextContactId": 9,
                "contacts": [
                  { "id": 7, "targetShipId": 2, "lastObservedPosition": { "xKilometers": 3, "yKilometers": -4 }, "lastObservedAtMilliseconds": 100, "status": "current", "identification": "identified", "knownVesselDisplayName": "NPC One", "knownDesignDisplayName": "Pathfinder", "lossWorkId": null, "lossDueTimeMilliseconds": null },
                  { "id": 8, "targetShipId": 3, "lastObservedPosition": { "xKilometers": 6, "yKilometers": -8 }, "lastObservedAtMilliseconds": 100, "status": "current", "identification": "detected", "knownVesselDisplayName": null, "knownDesignDisplayName": null, "lossWorkId": null, "lossDueTimeMilliseconds": null }
                ],
                "activeScan": { "targetContactId": 8, "startedAtMilliseconds": 100, "expectedCompletionMilliseconds": 2100, "scheduledCompletionId": 2 }
              },
              "autonomousState": { "contactPosture": null, "pendingContactDecisionWake": null }
            }
            """;

    private static string NativeV4CautiousNpcJson() =>
        """
            {
              "instanceId": 2, "definitionId": "pathfinder", "displayName": "NPC One",
              "tacticalPosition": { "xKilometers": 3, "yKilometers": -4 },
              "tacticalMotion": { "headingDegrees": 0, "speedKilometersPerSecond": 0 },
              "sensorIntegrity": 1, "sensorRepair": null,
              "strategicState": { "kind": "atLocation", "locationId": "alpha", "travel": null }, "activeOrder": null,
              "sensorKnowledge": {
                "nextContactId": 4,
                "contacts": [{ "id": 3, "targetShipId": 1, "lastObservedPosition": { "xKilometers": 0, "yKilometers": 0 }, "lastObservedAtMilliseconds": 100, "status": "current", "identification": "detected", "knownVesselDisplayName": null, "knownDesignDisplayName": null, "lossWorkId": null, "lossDueTimeMilliseconds": null }],
                "activeScan": null
              },
              "autonomousState": { "contactPosture": "cautiousContact", "pendingContactDecisionWake": { "scheduledWorkId": 1, "dueTimeMilliseconds": 100 } }
            }
            """;

    private static string NativeV4SecondNpcJson() =>
        """
            {
              "instanceId": 3, "definitionId": "pathfinder", "displayName": "NPC Two",
              "tacticalPosition": { "xKilometers": 6, "yKilometers": -8 },
              "tacticalMotion": { "headingDegrees": 0, "speedKilometersPerSecond": 0 },
              "sensorIntegrity": 1, "sensorRepair": null,
              "strategicState": { "kind": "atLocation", "locationId": "alpha", "travel": null }, "activeOrder": null,
              "sensorKnowledge": { "nextContactId": 1, "contacts": [], "activeScan": null },
              "autonomousState": { "contactPosture": null, "pendingContactDecisionWake": null }
            }
            """;

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
