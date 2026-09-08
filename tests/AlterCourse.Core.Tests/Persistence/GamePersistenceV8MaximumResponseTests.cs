using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using AlterCourse.Core.Tests.Gameplay;
using FactionTestWorld = AlterCourse.Core.Tests.Gameplay.FactionBootstrapTests.FactionTestWorld;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies the maximum response-capable V8 faction shape.</summary>
public sealed class GamePersistenceV8MaximumResponseTests
{
    /// <summary>All 2,040 simultaneous deliveries drain with one response evaluation per recipient.</summary>
    [Fact]
    public void MaximumSameTimeDeliveryBatchCoalescesPerFaction()
    {
        (SimulationState state, FactionDefinitionCatalog factionCatalog) = CreateMaximumDeliveryState();

        SimulationAdvanceTraceResult delivered = GameSimulation.AdvanceTo(
            state,
            new SimulationTime(2_000),
            FactionTestWorld.ShipCatalog,
            factionCatalog
        );
        ScheduledConsequenceTrace[] deliveryTraces =
        [
            .. delivered.Traces.Where(trace => trace.WorkKind == ScheduledWorkKind.ObservationReportDelivery),
        ];

        Assert.Equal(2_040, deliveryTraces.Length);
        Assert.Equal(255, deliveryTraces.Select(trace => trace.Target.FactionId).Distinct().Count());
        Assert.Equal(255, deliveryTraces.Count(trace => trace.FactionInvestigationDecision is not null));
        Assert.Empty(delivered.State.Scheduler.OutstandingWork);
    }

    /// <summary>Every response-capable faction retains a full active/history alternative within the envelope.</summary>
    [Fact]
    public void MaximumActiveResponseWorldRoundTripsWithinSaveEnvelope()
    {
        (SimulationState state, FactionDefinitionCatalog factionCatalog) = CreateMaximumActiveResponseState();
        var simulation = GameSimulation.RestoreState(state, FactionTestWorld.ShipCatalog, factionCatalog);

        byte[] saved = GamePersistence.Serialize(simulation, Milestone3ProofFixture.Metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(
            saved,
            FactionTestWorld.ShipCatalog,
            factionCatalog,
            "maximum-active-response-v8.json"
        );
        SimulationState restored = loaded.Simulation.CaptureState();
        int maximumActiveFactions = (SimulationState.MaximumShips - 1) / 2;

        Assert.InRange(saved.Length, 1, 128 * 1024 * 1024);
        Assert.Equal(127, maximumActiveFactions);
        Assert.Equal(
            maximumActiveFactions,
            restored.Factions.Count(faction => faction.Observation!.ActiveInvestigation is not null)
        );
        Assert.Equal(
            maximumActiveFactions * 24,
            restored.Factions.Sum(faction => faction.Observation!.CompletionWatermarks.Length)
        );
        Assert.All(restored.Factions, faction => Assert.Equal(16, faction.Observation!.ReceivedReports.Length));
        Assert.All(restored.Factions, faction => Assert.Equal(8, faction.Observation!.InFlightReports.Length));
        Assert.Equal(maximumActiveFactions * 10, restored.Scheduler.OutstandingWork.Length);
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
    }

    private static (SimulationState State, FactionDefinitionCatalog FactionCatalog) CreateMaximumActiveResponseState()
    {
        int factionCount = (SimulationState.MaximumShips - 1) / 2;
        (FactionStart[] factions, FactionDefinitionCatalog factionCatalog) = CreateFactions(factionCount);
        ShipStart[] ships = CreateShips(factionCount);
        SimulationState state = new GameBootstrap(
            new SimulationTime(6_000),
            CreateMap(),
            ships[0].InstanceId,
            ships,
            factions
        )
            .CreateSimulation(FactionTestWorld.ShipCatalog, factionCatalog)
            .CaptureState();
        long nextReportId = 1;
        for (int index = 0; index < factionCount; index++)
            state = AddActiveResponse(state, index, ref nextReportId);
        return (state, factionCatalog);
    }

    private static (SimulationState State, FactionDefinitionCatalog FactionCatalog) CreateMaximumDeliveryState()
    {
        int factionCount = SimulationState.MaximumShips - 1;
        (FactionStart[] factions, FactionDefinitionCatalog factionCatalog) = CreateFactions(factionCount);
        ShipStart[] ships =
        [
            FactionTestWorld.CreateShip(1, FactionTestWorld.Gamma, null),
            .. Enumerable
                .Range(0, factionCount)
                .Select(index =>
                    FactionTestWorld.CreateShip(index + 2L, FactionTestWorld.Alpha, new FactionId(index + 1L)) with
                    {
                        TacticalPosition = new TacticalPosition((index + 1L) * 1_000, 0),
                    }
                ),
        ];
        SimulationState state = new GameBootstrap(
            new SimulationTime(0),
            FactionTestWorld.CreateMap(),
            ships[0].InstanceId,
            ships,
            factions
        )
            .CreateSimulation(FactionTestWorld.ShipCatalog, factionCatalog)
            .CaptureState();
        long nextReportId = 1;
        for (int index = 0; index < factionCount; index++)
            state = AddDeliveries(state, index, ref nextReportId);
        return (state, factionCatalog);
    }

    private static SimulationState AddDeliveries(SimulationState state, int factionIndex, ref long nextReportId)
    {
        FactionState faction = state.Factions[factionIndex];
        ShipState source = state.GetRequiredShip(new ShipInstanceId(factionIndex + 2L));
        state = state.ReplaceShip(
            source.InstanceId,
            source with
            {
                SensorKnowledge = new SensorKnowledge(9, source.SensorKnowledge.Contacts),
            }
        );
        var inFlight = new List<ObservationReportInFlight>(8);
        SimulationScheduler scheduler = state.Scheduler;
        for (int index = 1; index <= 8; index++)
        {
            ObservationReportSnapshot report = CreateReport(
                nextReportId++,
                source.InstanceId,
                index,
                FactionTestWorld.Alpha
            );
            (scheduler, ScheduledWork delivery) = scheduler.Schedule(
                new SimulationTime(2_000),
                ScheduledWorkTarget.ForFaction(faction.Id),
                ScheduledWorkKind.ObservationReportDelivery
            );
            inFlight.Add(new ObservationReportInFlight(report, delivery.Id, delivery.DueTime));
        }
        return state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(ObservationResponsePosture.Enabled, inFlight),
            }
        ) with
        {
            Scheduler = scheduler,
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(nextReportId),
        };
    }

    private static SimulationState AddActiveResponse(SimulationState state, int index, ref long nextReportId)
    {
        FactionState faction = state.Factions[index];
        var sourceId = new ShipInstanceId(2 + (2L * index));
        ShipState source = state.GetRequiredShip(sourceId);
        state = state.ReplaceShip(
            sourceId,
            source with
            {
                SensorKnowledge = new SensorKnowledge(26, source.SensorKnowledge.Contacts),
            }
        );
        ObservationReportSnapshot activeSource = CreateReport(nextReportId++, sourceId, 1, FactionTestWorld.Beta);
        state = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    receivedReports: [new ReceivedObservationReport(activeSource, new SimulationTime(2_000))]
                ),
            }
        ) with
        {
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(nextReportId),
        };
        FactionInvestigationProposal proposal = GameSimulation
            .DecideFactionInvestigation(state, state.Factions[index])
            .Proposal!;
        state = GameSimulation.ApplyFactionInvestigation(state, proposal, FactionTestWorld.ShipCatalog).CandidateState;
        return AddMaximumHistory(state, index, sourceId, ref nextReportId);
    }

    private static SimulationState AddMaximumHistory(
        SimulationState state,
        int factionIndex,
        ShipInstanceId sourceId,
        ref long nextReportId
    )
    {
        FactionState faction = state.Factions[factionIndex];
        FactionObservationState observation = faction.Observation!;
        var received = new List<ReceivedObservationReport>(16);
        var inFlight = new List<ObservationReportInFlight>(8);
        var watermarks = new List<ObservationLocationCompletionWatermark>(24);
        SimulationScheduler scheduler = state.Scheduler;
        for (int index = 1; index <= 24; index++)
        {
            LocationId location = new($"history-{index}");
            SimulationTime observedAt = HistoryObservedAt(index);
            ObservationReportSnapshot report = CreateReport(nextReportId++, sourceId, index + 1L, location, observedAt);
            watermarks.Add(new ObservationLocationCompletionWatermark(location, state.Time));
            if (index <= 16)
            {
                SimulationTime receivedAt = HistoryReceivedAt(index);
                received.Add(new ReceivedObservationReport(report, receivedAt, ObservationReportHandling.Handled));
            }
            else
            {
                SimulationTime dueTime = observedAt.AdvanceBy(
                    new SimulationDuration(FactionObservationState.ReportDeliveryDelayMilliseconds)
                );
                (scheduler, inFlight) = AddDelivery(scheduler, faction.Id, report, inFlight, dueTime);
            }
        }
        state = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    inFlight,
                    received,
                    observation.ActiveInvestigation,
                    watermarks
                ),
            }
        );
        return state with
        {
            Scheduler = scheduler,
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(nextReportId),
        };
    }

    private static SimulationTime HistoryObservedAt(int index) =>
        new(
            index <= 7 ? 0
            : index <= 15 ? 2_000
            : index <= 23 ? 4_000
            : 6_000
        );

    private static SimulationTime HistoryReceivedAt(int index) =>
        new(
            index <= 7 ? 2_000
            : index <= 15 ? 4_000
            : 6_000
        );

    private static (SimulationScheduler Scheduler, List<ObservationReportInFlight> InFlight) AddDelivery(
        SimulationScheduler scheduler,
        FactionId factionId,
        ObservationReportSnapshot report,
        List<ObservationReportInFlight> inFlight,
        SimulationTime dueTime
    )
    {
        (scheduler, ScheduledWork delivery) = scheduler.Schedule(
            dueTime,
            ScheduledWorkTarget.ForFaction(factionId),
            ScheduledWorkKind.ObservationReportDelivery
        );
        inFlight.Add(new ObservationReportInFlight(report, delivery.Id, delivery.DueTime));
        return (scheduler, inFlight);
    }

    private static ObservationReportSnapshot CreateReport(
        long reportId,
        ShipInstanceId sourceId,
        long contactId,
        LocationId location,
        SimulationTime? observedAt = null
    ) =>
        new(
            new ObservationReportId(reportId),
            sourceId,
            new SensorContactId(contactId),
            location,
            new TacticalPosition(double.MaxValue, -double.MaxValue),
            observedAt ?? new SimulationTime(0),
            SensorContactIdentification.Identified,
            new string('\u0080', ShipState.MaximumVesselDisplayNameLength),
            new string('\u0080', ShipDefinition.MaximumDesignDisplayNameLength)
        );

    private static ShipStart[] CreateShips(int factionCount) =>
        [
            FactionTestWorld.CreateShip(1, FactionTestWorld.Gamma, null),
            .. Enumerable
                .Range(0, factionCount)
                .SelectMany(index =>
                {
                    var factionId = new FactionId(index + 1L);
                    long sourceId = 2 + (2L * index);
                    return new[]
                    {
                        FactionTestWorld.CreateShip(sourceId, FactionTestWorld.Alpha, factionId) with
                        {
                            TacticalPosition = new TacticalPosition(sourceId * 1_000, 0),
                        },
                        FactionTestWorld.CreateShip(sourceId + 1, FactionTestWorld.Alpha, factionId) with
                        {
                            TacticalPosition = new TacticalPosition((sourceId + 1) * 1_000, 0),
                        },
                    };
                }),
        ];

    private static (FactionStart[] Starts, FactionDefinitionCatalog Catalog) CreateFactions(int count)
    {
        var definitions = new Dictionary<FactionDefinitionId, FactionDefinition>();
        var starts = new FactionStart[count];
        for (int index = 0; index < count; index++)
        {
            FactionId factionId = new(index + 1L);
            FactionDefinitionId definitionId = new($"response-{index + 1}");
            definitions.Add(definitionId, new FactionDefinition(definitionId, $"Response {index + 1}"));
            starts[index] = new FactionStart(
                factionId,
                definitionId,
                ObservationResponsePosture: ObservationResponsePosture.Enabled
            );
        }
        return (starts, new FactionDefinitionCatalog(definitions));
    }

    private static StrategicMap CreateMap() =>
        new(
            [
                new StrategicLocation(FactionTestWorld.Alpha, "Alpha", default),
                new StrategicLocation(FactionTestWorld.Beta, "Beta", default),
                new StrategicLocation(FactionTestWorld.Gamma, "Gamma", default),
                .. Enumerable
                    .Range(1, 24)
                    .Select(index => new StrategicLocation(
                        new LocationId($"history-{index}"),
                        $"History {index}",
                        default
                    )),
            ],
            [new StrategicRoute(FactionTestWorld.Alpha, FactionTestWorld.Beta, new SimulationDuration(1_000))]
        );
}
