using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
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
    private const long HighTime = 8_000_000_000_000_000_000;
    private const long HighWorkId = 8_100_000_000_000_000_000;
    private const long HighContactId = 8_200_000_000_000_000_000;
    private const long HighOrderId = 8_300_000_000_000_000_000;
    private const long HighReportId = 8_400_000_000_000_000_000;
    private const long HighShipId = 8_500_000_000_000_000_000;
    private const long HighFactionId = 8_600_000_000_000_000_000;
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
        Assert.Equal(8, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("observation-driven-faction-response-v1", root["simulationRulesVersion"]!.GetValue<string>());
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

        Assert.Equal(8, current["schemaVersion"]!.GetValue<int>());
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

        byte[] migratedCurrent = GamePersistence.Serialize(migrated.Simulation, migrated.Metadata);
        AssertCurrentOrdering(migratedCurrent);
        LoadedGameSave resumed = GamePersistence.Deserialize(migratedCurrent, Catalog(), "native-populated-v6.json");

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
        // The V4 shape records no location, so the chain to V6 must leave the frame unqualified
        // rather than derive one from either ship's present strategic state.
        Assert.Null(learned.ObservedAtLocationId);
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

    private static void AssertCurrentOrdering(byte[] migratedCurrent)
    {
        JsonObject current = Parse(migratedCurrent);
        Assert.Equal(8, current["schemaVersion"]!.GetValue<int>());
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
        string invalid = valid.Replace("\"xKilometers\":1", "\"xKilometers\":1e999", StringComparison.Ordinal);

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
        int factionWorkMaximum = SimulationState.MaximumFactions * (1 + FactionObservationState.MaximumInFlightReports);
        Assert.Equal(SimulationScheduler.MaximumOutstandingWork, conservativeMaximum + factionWorkMaximum);
    }

    /// <summary>Confirms the V8 empty-observation maximum faction baseline remains within the envelope.</summary>
    /// <remarks>
    /// Every ship carries the full directed contact graph, an active scan and repair; every eligible
    /// non-player ship also carries an order, autonomous wake, and controller. This retains the
    /// pre-observation 66,302-work baseline independently of the populated response alternative below.
    /// </remarks>
    [Fact]
    public void MaximumFactionWorldRoundTripsWithinSaveEnvelope()
    {
        (GameSimulation simulation, ShipDefinitionCatalog catalog, FactionDefinitionCatalog factionCatalog) =
            CreateMaximumFactionSimulation();
        var metadata = new GameSaveMetadata(new string('\u0080', 128), new string('\u0080', 128), Timestamp, Timestamp);

        byte[] saved = GamePersistence.Serialize(simulation, metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(
            saved,
            catalog,
            factionCatalog,
            "maximum-faction-world-v8.json"
        );

        Assert.InRange(saved.Length, 1, 128 * 1024 * 1024);
        Assert.Equal(88_092_638, saved.Length);
        Assert.Equal(256, loaded.Simulation.CaptureState().Ships.Length);
        Assert.Equal(256, loaded.Simulation.CaptureState().Factions.Length);
        Assert.Equal(66_302, loaded.Simulation.CaptureState().Scheduler.OutstandingWork.Length);
        Assert.All(
            loaded.Simulation.CaptureState().Ships,
            ship => Assert.Equal(255, ship.SensorKnowledge.Contacts.Length)
        );
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
    }

    /// <summary>Confirms every NPC faction can retain its full report history beside the strongest ship graph.</summary>
    [Fact]
    public void MaximumReportWorldRoundTripsWithinSaveEnvelope()
    {
        (GameSimulation baseline, ShipDefinitionCatalog catalog, FactionDefinitionCatalog factionCatalog) =
            CreateMaximumFactionSimulation(new SimulationTime(6_000));
        SimulationState populated = PopulateMaximumReports(baseline.CaptureState());
        var simulation = GameSimulation.RestoreState(populated, catalog, factionCatalog);
        var metadata = new GameSaveMetadata(new string('\u0080', 128), new string('\u0080', 128), Timestamp, Timestamp);

        byte[] saved = GamePersistence.Serialize(simulation, metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(
            saved,
            catalog,
            factionCatalog,
            "maximum-report-world-v8.json"
        );
        SimulationState restored = loaded.Simulation.CaptureState();

        Assert.InRange(saved.Length, 1, 128 * 1024 * 1024);
        Assert.Equal(
            255 * FactionObservationState.MaximumInFlightReports,
            restored.Factions.Sum(faction => faction.Observation!.InFlightReports.Length)
        );
        Assert.Equal(
            255 * FactionObservationState.MaximumReceivedReports,
            restored.Factions.Sum(faction => faction.Observation!.ReceivedReports.Length)
        );
        Assert.Equal(68_343, restored.Scheduler.OutstandingWork.Length);
        Assert.Equal(6_121, restored.ObservationReportIdAllocator.NextId);
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
    }

    /// <summary>Confirms compact V8 output admits a validated maximum-width report world.</summary>
    /// <remarks>
    /// The measured vertex saturates contacts, report history, work correlations, identity widths, times, positions,
    /// and escaped names. The derived ceiling then adds complete maximum encodings for every omitted bounded shape;
    /// adding whole alternatives instead of their deltas deliberately overcounts mutually exclusive states.
    /// </remarks>
    [Fact]
    public void MaximumWidthReportWorldFitsCompactSaveEnvelope()
    {
        (GameSimulation baseline, ShipDefinitionCatalog catalog, FactionDefinitionCatalog factionCatalog) =
            CreateMaximumFactionSimulation(new SimulationTime(HighTime), highWidth: true);
        SimulationState populated = PopulateMaximumReports(baseline.CaptureState(), highWidth: true);
        var simulation = GameSimulation.RestoreState(populated, catalog, factionCatalog);
        var metadata = new GameSaveMetadata(new string('\u0080', 128), new string('\u0080', 128), Timestamp, Timestamp);

        byte[] saved = GamePersistence.Serialize(simulation, metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(saved, catalog, factionCatalog, "maximum-width-v8.json");
        long conservativeSupportedShapeCeiling = CompactV8SizeBound.FromMeasuredReportVertex(saved.Length);

        Assert.Equal(108_890_984, saved.Length);
        Assert.Equal(113_024_376, conservativeSupportedShapeCeiling);
        Assert.InRange(conservativeSupportedShapeCeiling, 1, 128L * 1024 * 1024);
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
    }

    private static SimulationState PopulateMaximumReports(SimulationState state, bool highWidth = false)
    {
        var work = state.Scheduler.OutstandingWork.ToList();
        long nextWorkId = state.Scheduler.NextWorkId;
        long nextReportId = highWidth ? HighReportId : 1;
        FactionState[] factions = [.. state.Factions];
        for (int index = 1; index < factions.Length; index++)
        {
            factions[index] = PopulateFactionReports(
                factions[index],
                state.Ships[index].InstanceId,
                state,
                work,
                ref nextWorkId,
                ref nextReportId,
                highWidth
            );
        }

        ShipState[] scanCompatibleShips = ReplaceActiveScansWithContactLoss(state, work, ref nextWorkId, highWidth);
        (StrategicMap map, ShipState[] ships) = AddOrderlessPlayerTravel(
            state,
            scanCompatibleShips,
            work,
            ref nextWorkId
        );
        return new SimulationState(
            state.Time,
            SimulationScheduler.Restore(nextWorkId, nextWorkId - 1, work),
            state.ShipIdAllocator,
            map,
            state.PlayerShipId,
            ships,
            state.OrderIdAllocator,
            factions,
            ObservationReportIdAllocator.Restore(nextReportId)
        );
    }

    private static FactionState PopulateFactionReports(
        FactionState faction,
        ShipInstanceId sourceId,
        SimulationState state,
        List<ScheduledWork> work,
        ref long nextWorkId,
        ref long nextReportId,
        bool highWidth
    )
    {
        List<ObservationReportInFlight> inFlight = new(FactionObservationState.MaximumInFlightReports);
        List<ReceivedObservationReport> received = new(FactionObservationState.MaximumReceivedReports);
        long epochBase = highWidth ? state.Time.Milliseconds - 6_000 : 0;
        for (int index = 0; index < FactionObservationState.MaximumReceivedReports; index++)
        {
            received.Add(
                new ReceivedObservationReport(
                    CreateMaximumReport(
                        nextReportId++,
                        sourceId,
                        (highWidth ? HighContactId : 0) + index + 1L,
                        state.StrategicMap.Locations[0].Id,
                        new SimulationTime(epochBase + (index < 8 ? 0 : 2_000))
                    ),
                    new SimulationTime(epochBase + (index < 8 ? 2_000 : 4_000))
                )
            );
        }
        for (int index = 0; index < FactionObservationState.MaximumInFlightReports; index++)
        {
            ObservationReportSnapshot report = CreateMaximumReport(
                nextReportId++,
                sourceId,
                (highWidth ? HighContactId : 0) + FactionObservationState.MaximumReceivedReports + index + 1L,
                state.StrategicMap.Locations[0].Id,
                new SimulationTime(epochBase + 4_000)
            );
            ScheduledWorkId deliveryId = AddWork(
                work,
                ref nextWorkId,
                state.Time,
                ScheduledWorkTarget.ForFaction(faction.Id),
                ScheduledWorkKind.ObservationReportDelivery
            );
            inFlight.Add(new ObservationReportInFlight(report, deliveryId, state.Time));
        }
        return faction with
        {
            Observation = new FactionObservationState(ObservationResponsePosture.Enabled, inFlight, received),
        };
    }

    private static ShipState[] ReplaceActiveScansWithContactLoss(
        SimulationState state,
        List<ScheduledWork> work,
        ref long nextWorkId,
        bool highWidth = false
    )
    {
        ShipState[] ships = state.Ships.ToArray();
        SimulationTime lossDueTime = state.Time.AdvanceBy(new SimulationDuration(5_000));
        for (int index = 0; index < ships.Length; index++)
        {
            ShipState ship = ships[index];
            ScheduledWorkId scanWorkId = ship.SensorKnowledge.ActiveScan!.ScheduledCompletionId;
            Assert.Equal(1, work.RemoveAll(item => item.Id == scanWorkId));
            ScheduledWorkId lossWorkId = AddWork(
                work,
                ref nextWorkId,
                lossDueTime,
                ScheduledWorkTarget.ForShip(ship.InstanceId),
                ScheduledWorkKind.SensorContactLoss
            );
            SensorContactTrack[] contacts = ship.SensorKnowledge.Contacts.ToArray();
            contacts[0] = contacts[0] with
            {
                Status = SensorContactStatus.Stale,
                Identification = highWidth ? SensorContactIdentification.Identified : contacts[0].Identification,
                KnownVesselDisplayName = highWidth
                    ? new string('\u0080', ShipState.MaximumVesselDisplayNameLength)
                    : contacts[0].KnownVesselDisplayName,
                KnownDesignDisplayName = highWidth
                    ? new string('\u0080', ShipDefinition.MaximumDesignDisplayNameLength)
                    : contacts[0].KnownDesignDisplayName,
                LossWorkId = lossWorkId,
                LossDueTime = lossDueTime,
            };
            ships[index] = ship with
            {
                SensorKnowledge = new SensorKnowledge(ship.SensorKnowledge.NextContactId, contacts),
            };
        }
        return ships;
    }

    private static (StrategicMap Map, ShipState[] Ships) AddOrderlessPlayerTravel(
        SimulationState state,
        ShipState[] ships,
        List<ScheduledWork> work,
        ref long nextWorkId
    )
    {
        ShipState player = ships[0];
        LocationId destination = new("travel-destination");
        SimulationTime arrivalAt = state.Time.AdvanceBy(new SimulationDuration(2_000));
        ScheduledWorkId arrivalId = AddWork(
            work,
            ref nextWorkId,
            arrivalAt,
            ScheduledWorkTarget.ForShip(player.InstanceId),
            ScheduledWorkKind.TravelArrival
        );
        StrategicMap map = new(
            [.. state.StrategicMap.Locations, new StrategicLocation(destination, "Travel destination", default)],
            [new StrategicRoute(state.StrategicMap.Locations[0].Id, destination, new SimulationDuration(2_000))]
        );
        ships[0] = player with
        {
            StrategicState = new TravelingState(
                new TravelState(state.StrategicMap.Locations[0].Id, destination, state.Time, arrivalAt, arrivalId)
            ),
        };
        return (map, ships);
    }

    private static ObservationReportSnapshot CreateMaximumReport(
        long reportId,
        ShipInstanceId sourceId,
        long contactId,
        LocationId locationId,
        SimulationTime observedAt
    ) =>
        new(
            new ObservationReportId(reportId),
            sourceId,
            new SensorContactId(contactId),
            locationId,
            new TacticalPosition(-double.MaxValue, -double.MaxValue),
            observedAt,
            SensorContactIdentification.Identified,
            new string('\u0080', ShipState.MaximumVesselDisplayNameLength),
            new string('\u0080', ShipDefinition.MaximumDesignDisplayNameLength)
        );

    private static (
        GameSimulation Simulation,
        ShipDefinitionCatalog Catalog,
        FactionDefinitionCatalog FactionCatalog
    ) CreateMaximumFactionSimulation(SimulationTime? currentTime = null, bool highWidth = false)
    {
        SimulationTime stateTime = currentTime ?? new SimulationTime(0);
        string maximumIdentity = new string('d', ShipDefinitionId.MaximumLength);
        string maximumName = new('\u0080', ShipState.MaximumVesselDisplayNameLength);
        string maximumDesignName = new('\u0080', ShipDefinition.MaximumDesignDisplayNameLength);
        var definitionId = new ShipDefinitionId(maximumIdentity);
        var locationId = new LocationId(new string('l', 128));
        ShipDefinitionCatalog catalog = CreateMaximumShipCatalog(definitionId, maximumDesignName);
        SimulationTime contactLossDueTime = stateTime.AdvanceBy(new SimulationDuration(5_000));
        var work = new List<ScheduledWork>(SimulationScheduler.MaximumOutstandingWork);
        var ships = new ShipState[SimulationState.MaximumShips];
        long nextWorkId = highWidth ? HighWorkId : 1;
        for (int observerIndex = 0; observerIndex < ships.Length; observerIndex++)
        {
            ships[observerIndex] = CreateMaximumContactShip(
                observerIndex,
                definitionId,
                locationId,
                maximumName,
                maximumDesignName,
                stateTime,
                contactLossDueTime,
                work,
                ref nextWorkId,
                highWidth
            );
        }

        (FactionState[] factions, FactionDefinitionCatalog factionCatalog) = CreateMaximumFactions(
            locationId,
            stateTime,
            work,
            ref nextWorkId,
            highWidth
        );
        var scheduler = SimulationScheduler.Restore(nextWorkId, nextWorkId - 1, work);
        var map = new StrategicMap([new StrategicLocation(locationId, new string('\u0080', 64), default)], []);
        var state = new SimulationState(
            stateTime,
            scheduler,
            ShipInstanceIdAllocator.Restore(
                highWidth ? HighShipId + SimulationState.MaximumShips : SimulationState.MaximumShips + 1L
            ),
            map,
            new ShipInstanceId(highWidth ? HighShipId : 1),
            ships,
            ShipOrderIdAllocator.Restore(
                highWidth ? HighOrderId + SimulationState.MaximumShips : SimulationState.MaximumShips
            ),
            factions
        );
        return (GameSimulation.RestoreState(state, catalog, factionCatalog), catalog, factionCatalog);
    }

    private static ShipDefinitionCatalog CreateMaximumShipCatalog(
        ShipDefinitionId definitionId,
        string maximumDesignName
    ) =>
        new(
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

    private static ShipState CreateMaximumContactShip(
        int observerIndex,
        ShipDefinitionId definitionId,
        LocationId locationId,
        string maximumName,
        string maximumDesignName,
        SimulationTime currentTime,
        SimulationTime dueTime,
        List<ScheduledWork> work,
        ref long nextWorkId,
        bool highWidth
    )
    {
        var observerId = new ShipInstanceId((highWidth ? HighShipId : 1) + observerIndex);
        SensorKnowledge knowledge = CreateMaximumKnowledge(
            observerIndex,
            observerId,
            locationId,
            maximumName,
            maximumDesignName,
            currentTime,
            dueTime,
            work,
            ref nextWorkId,
            highWidth
        );
        (SystemRepairState repair, ShipOrder? order, ShipAutonomousState autonomous) = CreateMaximumCommitments(
            observerIndex,
            observerId,
            currentTime,
            work,
            ref nextWorkId,
            highWidth
        );
        return new ShipState(
            observerId,
            definitionId,
            maximumName,
            highWidth
                ? new TacticalPosition(-double.MaxValue, -double.MaxValue)
                : new TacticalPosition(observerIndex, -observerIndex),
            default,
            new ShipEngineeringState(
                new SystemCondition(1),
                repair.ConditionAt(currentTime),
                new SystemCondition(1),
                new PowerAllocation(new(70), new(50)),
                repair
            ),
            new AtLocationState(locationId),
            order,
            knowledge,
            autonomous,
            observerIndex == 0 ? null : new FactionId((highWidth ? HighFactionId : 1) + observerIndex)
        );
    }

    private static SensorKnowledge CreateMaximumKnowledge(
        int observerIndex,
        ShipInstanceId observerId,
        LocationId locationId,
        string maximumName,
        string maximumDesignName,
        SimulationTime currentTime,
        SimulationTime lossDueTime,
        List<ScheduledWork> work,
        ref long nextWorkId,
        bool highWidth
    )
    {
        var contacts = new SensorContactTrack[SensorKnowledge.MaximumContactsPerObserver];
        int contactIndex = 0;
        for (int targetIndex = 0; targetIndex < SimulationState.MaximumShips; targetIndex++)
        {
            if (targetIndex == observerIndex)
            {
                continue;
            }

            bool scannedContact = contactIndex == 0;
            ScheduledWorkId? workId = scannedContact ? null : new ScheduledWorkId(nextWorkId);
            contacts[contactIndex] = new SensorContactTrack(
                new SensorContactId((highWidth ? HighContactId : 0) + contactIndex + 1L),
                new ShipInstanceId((highWidth ? HighShipId : 1) + targetIndex),
                highWidth
                    ? new TacticalPosition(-double.MaxValue, -double.MaxValue)
                    : new TacticalPosition(targetIndex, -targetIndex),
                currentTime,
                locationId,
                scannedContact ? SensorContactStatus.Current : SensorContactStatus.Stale,
                scannedContact ? SensorContactIdentification.Detected : SensorContactIdentification.Identified,
                scannedContact ? null : maximumName,
                scannedContact ? null : maximumDesignName,
                workId,
                scannedContact ? null : lossDueTime
            );
            if (workId is not null)
            {
                AddWork(
                    work,
                    ref nextWorkId,
                    lossDueTime,
                    ScheduledWorkTarget.ForShip(observerId),
                    ScheduledWorkKind.SensorContactLoss
                );
            }
            contactIndex++;
        }

        SimulationTime scanDueTime = currentTime.AdvanceBy(new SimulationDuration(2_000));
        ScheduledWorkId scanWorkId = AddWork(
            work,
            ref nextWorkId,
            scanDueTime,
            ScheduledWorkTarget.ForShip(observerId),
            ScheduledWorkKind.ActiveSensorScanCompletion
        );
        return new SensorKnowledge(
            (highWidth ? HighContactId : 0) + SensorKnowledge.MaximumContactsPerObserver + 1L,
            contacts,
            new ActiveSensorScanState(
                new SensorContactId((highWidth ? HighContactId : 0) + 1L),
                currentTime,
                scanDueTime,
                scanWorkId
            )
        );
    }

    private static (
        SystemRepairState Repair,
        ShipOrder? Order,
        ShipAutonomousState Autonomous
    ) CreateMaximumCommitments(
        int observerIndex,
        ShipInstanceId observerId,
        SimulationTime currentTime,
        List<ScheduledWork> work,
        ref long nextWorkId,
        bool highWidth
    )
    {
        SimulationTime repairDueTime = currentTime.AdvanceBy(new SimulationDuration(8_000));
        ScheduledWorkId repairWorkId = AddWork(
            work,
            ref nextWorkId,
            repairDueTime,
            ScheduledWorkTarget.ForShip(observerId),
            ScheduledWorkKind.SystemRepairCompletion
        );

        ShipOrder? order = null;
        ShipAutonomousState autonomous = ShipAutonomousState.Empty;
        if (observerIndex > 0)
        {
            SimulationTime holdDueTime = currentTime.AdvanceBy(new SimulationDuration(6_000));
            ScheduledWorkId holdWorkId = AddWork(
                work,
                ref nextWorkId,
                holdDueTime,
                ScheduledWorkTarget.ForShip(observerId),
                ScheduledWorkKind.OrderWake
            );
            order = new HoldUntilOrder(
                new ShipOrderId((highWidth ? HighOrderId : 0) + observerIndex),
                holdDueTime,
                holdWorkId
            );
            SimulationTime decisionDueTime = currentTime.AdvanceBy(new SimulationDuration(7_000));
            ScheduledWorkId decisionWorkId = AddWork(
                work,
                ref nextWorkId,
                decisionDueTime,
                ScheduledWorkTarget.ForShip(observerId),
                ScheduledWorkKind.ShipContactDecisionWake
            );
            autonomous = new ShipAutonomousState(
                ShipContactPosture.CautiousContact,
                new ShipContactDecisionWake(decisionWorkId, decisionDueTime)
            );
        }

        return (
            new SystemRepairState(
                ShipSystemId.Sensors,
                new SystemCondition(0.5),
                new SystemCondition(1),
                currentTime,
                repairDueTime,
                repairWorkId
            ),
            order,
            autonomous
        );
    }

    private static (FactionState[] Factions, FactionDefinitionCatalog Catalog) CreateMaximumFactions(
        LocationId locationId,
        SimulationTime dueTime,
        List<ScheduledWork> work,
        ref long nextWorkId,
        bool highWidth = false
    )
    {
        var definitions = new Dictionary<FactionDefinitionId, FactionDefinition>();
        var factions = new FactionState[SimulationState.MaximumFactions];
        for (int index = 0; index < factions.Length; index++)
        {
            var factionId = new FactionId((highWidth ? HighFactionId : 1) + index);
            string prefix = $"{index + 1:D3}-";
            var definitionId = new FactionDefinitionId(
                prefix + new string('f', FactionDefinitionId.MaximumLength - prefix.Length)
            );
            definitions.Add(
                definitionId,
                new FactionDefinition(definitionId, new string('\u0080', FactionDefinition.MaximumDisplayNameLength))
            );
            ScheduledWorkId wakeId = AddWork(
                work,
                ref nextWorkId,
                dueTime,
                ScheduledWorkTarget.ForFaction(factionId),
                ScheduledWorkKind.FactionDecisionWake
            );
            factions[index] = new FactionState(
                factionId,
                definitionId,
                new EstablishPresenceObjectiveState(locationId, FactionObjectiveStatus.Pending),
                new PendingFactionDecisionWake(wakeId, dueTime)
            );
        }

        return (factions, new FactionDefinitionCatalog(definitions));
    }

    private static ScheduledWorkId AddWork(
        List<ScheduledWork> work,
        ref long nextWorkId,
        SimulationTime dueTime,
        ScheduledWorkTarget target,
        ScheduledWorkKind kind
    )
    {
        var id = new ScheduledWorkId(nextWorkId);
        work.Add(new ScheduledWork(id, dueTime, nextWorkId - 1, target, kind));
        nextWorkId++;
        return id;
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
            null,
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

    private static class CompactV8SizeBound
    {
        private const int MaximumLongBytes = 19;
        private const int MaximumFiniteDoubleBytes = 24;
        private const int MaximumAsciiIdentityTokenBytes = LocationId.MaximumLength + 2;

        internal static long FromMeasuredReportVertex(int measuredBytes)
        {
            int unoccupiedWorkSlots = SimulationScheduler.MaximumOutstandingWork - 68_343;
            return measuredBytes
                + MaximumStrategicMapBytes()
                + (SimulationState.MaximumShips * MaximumPerShipAlternativeBytes())
                + (SimulationState.MaximumFactions * MaximumPerFactionAlternativeBytes())
                + (unoccupiedWorkSlots * MaximumScheduledWorkBytes());
        }

        private static int MaximumStrategicMapBytes()
        {
            int position = Object(("xUnitless", MaximumFiniteDoubleBytes), ("yUnitless", MaximumFiniteDoubleBytes));
            int location = Object(
                ("id", MaximumAsciiIdentityTokenBytes),
                ("displayName", 2 + (6 * StrategicLocation.MaximumDisplayNameLength)),
                ("position", position)
            );
            int route = Object(
                ("origin", MaximumAsciiIdentityTokenBytes),
                ("destination", MaximumAsciiIdentityTokenBytes),
                ("durationMilliseconds", MaximumLongBytes)
            );
            return Object(
                ("locations", Array(StrategicMap.MaximumLocations, location)),
                ("routes", Array(StrategicMap.MaximumRoutes, route))
            );
        }

        private static int MaximumPerShipAlternativeBytes()
        {
            int repair = Object(
                ("targetSystem", MaximumAsciiIdentityTokenBytes),
                ("startingCondition", MaximumFiniteDoubleBytes),
                ("targetCondition", MaximumFiniteDoubleBytes),
                ("startedAtMilliseconds", MaximumLongBytes),
                ("expectedCompletionMilliseconds", MaximumLongBytes),
                ("scheduledCompletionId", MaximumLongBytes)
            );
            int engineering = Object(
                ("generationCondition", MaximumFiniteDoubleBytes),
                ("sensorCondition", MaximumFiniteDoubleBytes),
                ("impulseCondition", MaximumFiniteDoubleBytes),
                ("sensorAllocation", MaximumLongBytes),
                ("impulseAllocation", MaximumLongBytes),
                ("activeRepair", repair)
            );
            return MaximumMotionBytes()
                + engineering
                + MaximumTravelStateBytes()
                + MaximumPatrolOrderBytes()
                + MaximumActiveScanBytes()
                + MaximumAutonomousStateBytes()
                + MaximumLongBytes;
        }

        private static int MaximumPerFactionAlternativeBytes()
        {
            int watermark = Object(
                ("locationId", MaximumAsciiIdentityTokenBytes),
                ("observedThroughMilliseconds", MaximumLongBytes)
            );
            int objective = Object(
                ("targetLocationId", MaximumAsciiIdentityTokenBytes),
                ("status", MaximumAsciiIdentityTokenBytes),
                ("assignedShipId", MaximumLongBytes),
                ("assignedOrderId", MaximumLongBytes)
            );
            int pendingWake = Object(("workId", MaximumLongBytes), ("dueTimeMilliseconds", MaximumLongBytes));
            int handlingAlternatives = FactionObservationState.MaximumReceivedReports * MaximumAsciiIdentityTokenBytes;
            return objective
                + pendingWake
                + Array(FactionObservationState.MaximumCompletionWatermarks, watermark)
                + MaximumActiveInvestigationBytes()
                + handlingAlternatives;
        }

        private static int MaximumObservationReportBytes()
        {
            int position = Object(("xKilometers", MaximumFiniteDoubleBytes), ("yKilometers", MaximumFiniteDoubleBytes));
            int maximumDisplayNameTokenBytes = 2 + (6 * ShipState.MaximumVesselDisplayNameLength);
            return Object(
                ("reportId", MaximumLongBytes),
                ("observerShipId", MaximumLongBytes),
                ("observerContactId", MaximumLongBytes),
                ("observedAtLocationId", MaximumAsciiIdentityTokenBytes),
                ("observedPosition", position),
                ("observedAtMilliseconds", MaximumLongBytes),
                ("identification", MaximumAsciiIdentityTokenBytes),
                ("knownVesselDisplayName", maximumDisplayNameTokenBytes),
                ("knownDesignDisplayName", maximumDisplayNameTokenBytes)
            );
        }

        private static int MaximumActiveInvestigationBytes() =>
            Object(
                ("sourceReport", MaximumObservationReportBytes()),
                ("responderShipId", MaximumLongBytes),
                ("originLocationId", MaximumAsciiIdentityTokenBytes),
                ("destinationLocationId", MaximumAsciiIdentityTokenBytes),
                ("sourceReceivedAtMilliseconds", MaximumLongBytes),
                ("assignedAtMilliseconds", MaximumLongBytes),
                ("orderId", MaximumLongBytes)
            );

        private static int MaximumMotionBytes() =>
            Object(
                ("headingDegrees", MaximumFiniteDoubleBytes),
                ("speedKilometersPerSecond", MaximumFiniteDoubleBytes)
            );

        private static int MaximumTravelStateBytes()
        {
            int travel = Object(
                ("origin", MaximumAsciiIdentityTokenBytes),
                ("destination", MaximumAsciiIdentityTokenBytes),
                ("departureMilliseconds", MaximumLongBytes),
                ("expectedArrivalMilliseconds", MaximumLongBytes),
                ("scheduledArrivalId", MaximumLongBytes)
            );
            return Object(
                ("kind", MaximumAsciiIdentityTokenBytes),
                ("locationId", MaximumAsciiIdentityTokenBytes),
                ("travel", travel)
            );
        }

        private static int MaximumPatrolOrderBytes() =>
            Object(
                ("kind", MaximumAsciiIdentityTokenBytes),
                ("id", MaximumLongBytes),
                ("waypoints", Array(PatrolRouteOrder.MaximumWaypointCount, MaximumAsciiIdentityTokenBytes)),
                ("nextWaypointIndex", MaximumLongBytes)
            );

        private static int MaximumActiveScanBytes() =>
            Object(
                ("targetContactId", MaximumLongBytes),
                ("startedAtMilliseconds", MaximumLongBytes),
                ("expectedCompletionMilliseconds", MaximumLongBytes),
                ("scheduledCompletionId", MaximumLongBytes)
            );

        private static int MaximumAutonomousStateBytes()
        {
            int wake = Object(("scheduledWorkId", MaximumLongBytes), ("dueTimeMilliseconds", MaximumLongBytes));
            return Object(("contactPosture", MaximumAsciiIdentityTokenBytes), ("pendingContactDecisionWake", wake));
        }

        private static int MaximumScheduledWorkBytes() =>
            Object(
                ("id", MaximumLongBytes),
                ("dueTimeMilliseconds", MaximumLongBytes),
                ("sequence", MaximumLongBytes),
                ("kind", MaximumAsciiIdentityTokenBytes),
                ("targetKind", MaximumAsciiIdentityTokenBytes),
                ("targetShipId", MaximumLongBytes),
                ("targetFactionId", MaximumLongBytes)
            );

        private static int Object(params (string Name, int ValueBytes)[] properties) =>
            2
            + Math.Max(0, properties.Length - 1)
            + properties.Sum(property => property.Name.Length + 3 + property.ValueBytes);

        private static int Array(int count, int itemBytes) => 2 + (count * itemBytes) + Math.Max(0, count - 1);
    }

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
