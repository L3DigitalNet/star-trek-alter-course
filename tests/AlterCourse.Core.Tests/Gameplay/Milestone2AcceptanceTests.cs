using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Locks the integrated deterministic Core signature required for Milestone 2 acceptance.</summary>
public sealed class Milestone2AcceptanceTests
{
    private const long HourMilliseconds = 60 * 60 * 1000;
    private static readonly ShipInstanceId PlayerId = new(1);
    private static readonly ShipInstanceId PatrolId = new(2);
    private static readonly ShipInstanceId HoldId = new(3);
    private static readonly ShipDefinitionId DefinitionId = new("pathfinder");
    private static readonly LocationId Alpha = new("alpha-watch");
    private static readonly LocationId Beta = new("beta-watch");
    private static readonly LocationId Refuge = new("quiet-refuge");
    private static readonly GameSaveMetadata Metadata = new(
        "milestone-2-acceptance",
        "Milestone 2 Acceptance",
        new DateTimeOffset(2026, 9, 2, 12, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 2, 12, 30, 0, TimeSpan.Zero)
    );

    /// <summary>Confirms the proof world retains its complete typed signature through 72 strategic hours.</summary>
    [Fact]
    public void ProofWorldContinuesDeterministicallyThroughSeventyTwoHours()
    {
        ShipDefinitionCatalog catalog = CreateCatalog();
        GameSimulation game = Milestone2ProofSetup.Create(catalog);
        ShipState initialPlayer = game.CaptureState().GetRequiredShip(PlayerId);
        List<ScheduledConsequenceTrace> traces = [];

        for (long hour = 6; hour <= 72; hour += 6)
        {
            SimulationAdvanceTraceResult leg = GameSimulation.AdvanceTo(game.CaptureState(), Time(hour), catalog);
            traces.AddRange(leg.Traces);
            game = GameSimulation.RestoreState(leg.State, catalog);

            PatrolRouteOrder patrol = Assert.IsType<PatrolRouteOrder>(leg.State.GetRequiredShip(PatrolId).ActiveOrder);
            Assert.Equal(new ShipOrderId(1), patrol.Id);
            bool outbound = hour % 12 == 0;
            AssertPatrolLeg(
                leg.State,
                outbound ? Alpha : Beta,
                outbound ? Beta : Alpha,
                outbound ? 1 : 0,
                Time(hour),
                Time(hour + 6)
            );
        }

        AssertProofTrace(traces);

        SimulationState final = game.CaptureState();
        Assert.Equal(Time(72), final.Time);
        Assert.Equal(initialPlayer, final.GetRequiredShip(PlayerId));
        Assert.Null(final.GetRequiredShip(PlayerId).ActiveOrder);
        Assert.Equal(Refuge, Assert.IsType<AtLocationState>(final.GetRequiredShip(PlayerId).StrategicState).LocationId);
        Assert.Null(final.GetRequiredShip(HoldId).ActiveOrder);
        Assert.Equal(Alpha, Assert.IsType<AtLocationState>(final.GetRequiredShip(HoldId).StrategicState).LocationId);
        Assert.DoesNotContain(final.Scheduler.OutstandingWork, work => work.TargetShipId == HoldId);
        AssertPatrolLeg(final, Alpha, Beta, 1, Time(72), Time(78));
        ScheduledWork nextArrival = Assert.Single(final.Scheduler.OutstandingWork);
        Assert.Equal(
            (14L, PatrolId, ScheduledWorkKind.TravelArrival, Time(78)),
            (nextArrival.Id.Value, nextArrival.TargetShipId, nextArrival.Kind, nextArrival.DueTime)
        );
    }

    /// <summary>Confirms a V3 save taken mid-patrol resumes to the uninterrupted 72-hour signature.</summary>
    [Fact]
    public void V3ReloadAtThirtySixHoursContinuesToIdenticalFinalState()
    {
        ShipDefinitionCatalog catalog = CreateCatalog();
        SimulationState initial = Milestone2ProofSetup.Create(catalog).CaptureState();
        SimulationAdvanceTraceResult uninterrupted = GameSimulation.AdvanceTo(initial, Time(72), catalog);
        SimulationAdvanceTraceResult midpoint = GameSimulation.AdvanceTo(initial, Time(36), catalog);
        var midpointGame = GameSimulation.RestoreState(midpoint.State, catalog);

        AssertPatrolLeg(midpoint.State, Alpha, Beta, 1, Time(36), Time(42));
        Assert.Equal(3, midpoint.State.OrderIdAllocator.NextId);
        byte[] midpointBytes = GamePersistence.Serialize(midpointGame, Metadata);
        LoadedGameSave reloaded = GamePersistence.Deserialize(midpointBytes, catalog, "milestone-2-midpoint.json");
        Assert.Equal(midpointBytes, GamePersistence.Serialize(reloaded.Simulation, Metadata));

        SimulationAdvanceTraceResult continued = GameSimulation.AdvanceTo(midpoint.State, Time(72), catalog);
        SimulationAdvanceTraceResult reloadedContinuation = GameSimulation.AdvanceTo(
            reloaded.Simulation.CaptureState(),
            Time(72),
            catalog
        );

        Assert.Equal(continued.Traces, reloadedContinuation.Traces);
        AssertEquivalentState(uninterrupted.State, reloadedContinuation.State);
        Assert.Equal(Serialize(uninterrupted.State, catalog), Serialize(reloadedContinuation.State, catalog));
    }

    /// <summary>Confirms non-boundary and consequence-boundary chunks produce one stable V3 result.</summary>
    [Fact]
    public void StrategicChunkingProducesIdenticalSeventyTwoHourState()
    {
        ShipDefinitionCatalog catalog = CreateCatalog();
        SimulationState initial = Milestone2ProofSetup.Create(catalog).CaptureState();
        SimulationState oneJump = GameSimulation.AdvanceTo(initial, Time(72), catalog).State;
        SimulationState chunked = initial;
        SimulationTime[] boundaries =
        [
            Time(3).AdvanceBy(new SimulationDuration(100)),
            Time(5).AdvanceBy(new SimulationDuration(500)),
            Time(6),
            Time(8).AdvanceBy(new SimulationDuration(900)),
            Time(9),
            Time(23).AdvanceBy(new SimulationDuration(100)),
            Time(36),
            Time(53).AdvanceBy(new SimulationDuration(700)),
            Time(72),
        ];

        foreach (SimulationTime boundary in boundaries)
        {
            chunked = GameSimulation.AdvanceTo(chunked, boundary, catalog).State;
        }

        AssertEquivalentState(oneJump, chunked);
        Assert.Equal(Serialize(oneJump, catalog), Serialize(chunked, catalog));
    }

    /// <summary>Confirms exact hold cancellation survives reload and stale identities cannot affect replacement intent.</summary>
    [Fact]
    public void HoldCancellationRemovesOnlyItsWakeAndCannotCancelReplacementOrder()
    {
        ShipDefinitionCatalog catalog = CreateCatalog();
        GameSimulation game = Milestone2ProofSetup.Create(catalog);
        ScheduledWork originalWake = game.CaptureState()
            .Scheduler.OutstandingWork.Single(work => work.TargetShipId == HoldId);

        Assert.Equal(OrderCancellationOutcome.Cancelled, game.CancelOrder(new ShipOrderId(2)).Outcome);
        SimulationState cancelled = game.CaptureState();
        Assert.DoesNotContain(cancelled.Scheduler.OutstandingWork, work => work.Id == originalWake.Id);
        Assert.Contains(cancelled.Scheduler.OutstandingWork, work => work.TargetShipId == PatrolId);

        (SimulationScheduler scheduler, ScheduledWork replacementWake) = cancelled.Scheduler.Schedule(
            Time(15),
            HoldId,
            ScheduledWorkKind.OrderWake
        );
        (ShipOrderIdAllocator allocator, ShipOrderId replacementId) = cancelled.OrderIdAllocator.Allocate();
        ShipState holdShip = cancelled.GetRequiredShip(HoldId);
        SimulationState withReplacement = cancelled.ReplaceShip(
            HoldId,
            holdShip with
            {
                ActiveOrder = new HoldUntilOrder(replacementId, Time(15), replacementWake.Id),
            }
        ) with
        {
            Scheduler = scheduler,
            OrderIdAllocator = allocator,
        };
        LoadedGameSave reloaded = GamePersistence.Deserialize(
            Serialize(withReplacement, catalog),
            catalog,
            "hold-cancelled.json"
        );
        byte[] beforeStaleRequests = GamePersistence.Serialize(reloaded.Simulation, Metadata);

        Assert.Equal(OrderCancellationOutcome.NotFound, reloaded.Simulation.CancelOrder(new ShipOrderId(2)).Outcome);
        Assert.Equal(OrderCancellationOutcome.NotFound, reloaded.Simulation.CancelOrder(new ShipOrderId(99)).Outcome);
        Assert.Equal(beforeStaleRequests, GamePersistence.Serialize(reloaded.Simulation, Metadata));

        SimulationState afterOriginalBoundary = GameSimulation
            .AdvanceTo(reloaded.Simulation.CaptureState(), Time(12), catalog)
            .State;
        HoldUntilOrder replacement = Assert.IsType<HoldUntilOrder>(
            afterOriginalBoundary.GetRequiredShip(HoldId).ActiveOrder
        );
        Assert.Equal(replacementId, replacement.Id);
        Assert.Equal(replacementWake.Id, replacement.ScheduledWakeId);
        Assert.Contains(afterOriginalBoundary.Scheduler.OutstandingWork, work => work.Id == replacementWake.Id);
    }

    /// <summary>Confirms canceled transit orders finish their physical leg after reload without following on.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TransitCancellationRetainsArrivalButNeverFollowsOn(bool patrol)
    {
        ShipDefinitionCatalog catalog = CreateCatalog();
        GameSimulation game = CreateTransitCancellationGame(catalog, patrol);

        if (patrol)
        {
            Assert.IsType<PatrolRouteOrder>(game.CaptureState().GetRequiredShip(PatrolId).ActiveOrder);
        }
        else
        {
            Assert.IsType<TravelToOrder>(game.CaptureState().GetRequiredShip(PatrolId).ActiveOrder);
        }

        Assert.Equal(OrderCancellationOutcome.Cancelled, game.CancelOrder(new ShipOrderId(1)).Outcome);
        SimulationState cancelled = game.CaptureState();
        Assert.Equal(
            [(PatrolId, ScheduledWorkKind.TravelArrival), (HoldId, ScheduledWorkKind.OrderWake)],
            cancelled.Scheduler.OutstandingWork.Select(work => (work.TargetShipId, work.Kind))
        );

        LoadedGameSave reloaded = GamePersistence.Deserialize(
            Serialize(cancelled, catalog),
            catalog,
            patrol ? "cancelled-patrol.json" : "cancelled-travel.json"
        );
        SimulationAdvanceTraceResult advanced = GameSimulation.AdvanceTo(
            reloaded.Simulation.CaptureState(),
            Time(12),
            catalog
        );
        ScheduledConsequenceTrace arrival = advanced.Traces.Single(trace => trace.TargetShipId == PatrolId);
        ShipState traveler = advanced.State.GetRequiredShip(PatrolId);

        Assert.Null(arrival.OrderId);
        Assert.Equal(ScheduledConsequenceRule.OrderlessTravelArrival, arrival.Rule);
        Assert.Equal(ScheduledConsequenceAction.FinishTravel, arrival.Action);
        Assert.Null(traveler.ActiveOrder);
        Assert.Equal(Beta, Assert.IsType<AtLocationState>(traveler.StrategicState).LocationId);
        Assert.DoesNotContain(advanced.State.Scheduler.OutstandingWork, work => work.TargetShipId == PatrolId);
        Assert.Empty(advanced.State.Scheduler.OutstandingWork);
    }

    /// <summary>Confirms the public consequence budget failure leaves the live aggregate byte-for-byte unchanged.</summary>
    [Fact]
    public void PublicAdvanceRejectsRapidPatrolAtomicallyAtTotalBudget()
    {
        ShipDefinitionCatalog catalog = CreateCatalog();
        GameSimulation game = CreateRapidPatrol(catalog);
        byte[] before = GamePersistence.Serialize(game, Metadata);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            game.AdvanceFixedSteps(10_001)
        );

        Assert.Contains("10000 total consequence execution budget", exception.Message, StringComparison.Ordinal);
        Assert.Contains("attempt 10001", exception.Message, StringComparison.Ordinal);
        Assert.Equal(before, GamePersistence.Serialize(game, Metadata));
        Assert.Equal(Time(0), game.CaptureState().Time);
    }

    /// <summary>Confirms tactical motion uses 100 ms steps before zero speed restores strategic advancement.</summary>
    [Fact]
    public void ZeroSpeedReentersStrategicAdvancementAcrossRepairAndOrderBoundaries()
    {
        ShipDefinitionCatalog catalog = CreateCatalog(new SimulationDuration(3 * HourMilliseconds));
        SimulationState proof = Milestone2ProofSetup.Create(catalog).CaptureState();
        (SimulationScheduler scheduler, ScheduledWork repairWork) = proof.Scheduler.Schedule(
            Time(6),
            PlayerId,
            ScheduledWorkKind.SystemRepairCompletion
        );
        var repair = new SystemRepairState(
            new SensorIntegrity(0.25),
            new SensorIntegrity(1),
            Time(3),
            Time(6),
            repairWork.Id
        );
        ShipState player = WithSensorRepair(proof.GetRequiredShip(PlayerId), repair, 0.25);
        var game = GameSimulation.RestoreState(
            proof.ReplaceShip(PlayerId, player) with
            {
                Scheduler = scheduler,
            },
            catalog
        );
        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(
                new SetTacticalCourseIntent(new HeadingDegrees(90), new SpeedKilometersPerSecond(10))
            ).Outcome
        );
        game.AdvanceFixedSteps(1);
        TacticalPosition afterMotion = game.CaptureState().GetRequiredShip(PlayerId).TacticalPosition;
        Assert.Equal(1, afterMotion.XKilometers, 10);
        Assert.Equal(0, afterMotion.YKilometers, 10);

        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(
                new SetTacticalCourseIntent(new HeadingDegrees(237), new SpeedKilometersPerSecond(0))
            ).Outcome
        );
        int remainingSteps = checked((int)((Time(9).Milliseconds - game.CaptureState().Time.Milliseconds) / 100));
        SimulationAdvanceResult result = game.AdvanceFixedSteps(remainingSteps);
        SimulationState final = game.CaptureState();

        Assert.Equal(Time(9), result.FinalTime);
        Assert.Equal(
            [
                new PlayerAdvanceEvent(
                    PlayerAdvanceEventKind.SystemRepairCompleted,
                    Time(6),
                    ShipSystemId: ShipSystemId.Sensors
                ),
            ],
            result.ResolvedEvents
        );
        Assert.Equal(afterMotion, final.GetRequiredShip(PlayerId).TacticalPosition);
        Assert.Equal(1, final.GetRequiredShip(PlayerId).SensorIntegrity.Value);
        Assert.Null(final.GetRequiredShip(PlayerId).SensorRepair);
        AssertPatrolLeg(final, Beta, Alpha, 0, Time(6), Time(12));
        Assert.Null(final.GetRequiredShip(HoldId).ActiveOrder);
    }

    private static GameSimulation CreateTransitCancellationGame(ShipDefinitionCatalog catalog, bool patrol)
    {
        ShipOrderStart order = patrol ? new PatrolRouteOrderStart([Alpha, Beta], 1) : new TravelToOrderStart(Beta);
        ShipStart[] starts =
        [
            CreateStart(PlayerId, new AtLocationStart(Refuge)),
            CreateStart(PatrolId, new TravelingStart(Alpha, Beta, Time(0)), order),
            CreateStart(HoldId, new AtLocationStart(Alpha), new HoldUntilOrderStart(Time(9))),
        ];
        return new GameBootstrap(
            Time(3),
            CreateMap(new SimulationDuration(6 * HourMilliseconds)),
            PlayerId,
            starts
        ).CreateSimulation(catalog);
    }

    private static GameSimulation CreateRapidPatrol(ShipDefinitionCatalog catalog)
    {
        ShipStart[] starts =
        [
            CreateStart(PlayerId, new AtLocationStart(Refuge)),
            CreateStart(
                PatrolId,
                new TravelingStart(Alpha, Beta, Time(0)),
                new PatrolRouteOrderStart([Alpha, Beta], 1)
            ),
        ];
        return new GameBootstrap(Time(0), CreateMap(new SimulationDuration(100)), PlayerId, starts).CreateSimulation(
            catalog
        );
    }

    private static ShipStart CreateStart(
        ShipInstanceId id,
        ShipStrategicStart strategic,
        ShipOrderStart? order = null
    ) =>
        new(
            id,
            DefinitionId,
            $"Ship {id.Value}",
            default,
            default,
            new SensorIntegrity(1),
            strategic,
            ActiveOrder: order
        );

    private static StrategicMap CreateMap(SimulationDuration duration)
    {
        StrategicLocation alpha = new(Alpha, "Alpha Watch", default);
        StrategicLocation beta = new(Beta, "Beta Watch", default);
        StrategicLocation refuge = new(Refuge, "Quiet Refuge", default);
        return new StrategicMap([alpha, beta, refuge], [new StrategicRoute(Alpha, Beta, duration)]);
    }

    private static ShipDefinitionCatalog CreateCatalog(SimulationDuration? repairDuration = null)
    {
        var definition = new ShipDefinition(
            DefinitionId,
            "Pathfinder",
            new SpeedKilometersPerSecond(10),
            new DistanceKilometers(30),
            new SimulationDuration(2000),
            repairDuration ?? new SimulationDuration(6 * HourMilliseconds)
        );
        return new ShipDefinitionCatalog(
            new Dictionary<ShipDefinitionId, ShipDefinition> { [DefinitionId] = definition }
        );
    }

    private static byte[] Serialize(SimulationState state, ShipDefinitionCatalog catalog) =>
        GamePersistence.Serialize(GameSimulation.RestoreState(state, catalog), Metadata);

    private static void AssertProofTrace(List<ScheduledConsequenceTrace> traces)
    {
        long[] expectedHours = [6, 9, 12, 18, 24, 30, 36, 42, 48, 54, 60, 66, 72];
        Assert.Equal(13, traces.Count);
        Assert.Equal(expectedHours.Select(Time), traces.Select(trace => trace.ResolutionTime));
        Assert.Equal(Enumerable.Range(1, 13).Select(value => (long)value), traces.Select(trace => trace.WorkId.Value));

        for (int index = 0; index < traces.Count; index++)
        {
            ScheduledConsequenceTrace trace = traces[index];
            bool isHold = expectedHours[index] == 9;
            Assert.Equal(isHold ? HoldId : PatrolId, trace.TargetShipId);
            Assert.Equal(isHold ? ScheduledWorkKind.OrderWake : ScheduledWorkKind.TravelArrival, trace.WorkKind);
            Assert.Equal(new ShipOrderId(isHold ? 2 : 1), trace.OrderId);
            Assert.Equal(isHold ? ShipOrderKind.HoldUntil : ShipOrderKind.PatrolRoute, trace.OrderKind);
            Assert.Equal(
                isHold ? ScheduledConsequenceRule.HoldUntilWake : ScheduledConsequenceRule.PatrolWaypointArrival,
                trace.Rule
            );
            Assert.Equal(
                isHold ? ScheduledConsequenceAction.CompleteHold : ScheduledConsequenceAction.ContinuePatrol,
                trace.Action
            );
            Assert.Equal(isHold, trace.Completed);
            Assert.False(trace.RandomnessUsed);
        }
    }

    private static void AssertEquivalentState(SimulationState expected, SimulationState actual)
    {
        Assert.Equal(expected.Time, actual.Time);
        Assert.Equal(expected.OrderIdAllocator, actual.OrderIdAllocator);
        Assert.Equal(expected.Scheduler.NextWorkId, actual.Scheduler.NextWorkId);
        Assert.Equal(expected.Scheduler.NextSequence, actual.Scheduler.NextSequence);
        Assert.Equal(expected.Scheduler.OutstandingWork.ToArray(), actual.Scheduler.OutstandingWork.ToArray());
        foreach (ShipState expectedShip in expected.Ships)
        {
            ShipState actualShip = actual.GetRequiredShip(expectedShip.InstanceId);
            Assert.Equal(expectedShip.DefinitionId, actualShip.DefinitionId);
            Assert.Equal(expectedShip.VesselDisplayName, actualShip.VesselDisplayName);
            Assert.Equal(expectedShip.TacticalPosition, actualShip.TacticalPosition);
            Assert.Equal(expectedShip.TacticalMotion, actualShip.TacticalMotion);
            Assert.Equal(expectedShip.SensorIntegrity, actualShip.SensorIntegrity);
            Assert.Equal(expectedShip.SensorRepair, actualShip.SensorRepair);
            Assert.Equal(expectedShip.StrategicState, actualShip.StrategicState);
            if (expectedShip.ActiveOrder is PatrolRouteOrder expectedPatrol)
            {
                PatrolRouteOrder actualPatrol = Assert.IsType<PatrolRouteOrder>(actualShip.ActiveOrder);
                Assert.Equal(expectedPatrol.Id, actualPatrol.Id);
                Assert.Equal(expectedPatrol.NextWaypointIndex, actualPatrol.NextWaypointIndex);
                Assert.Equal(expectedPatrol.Waypoints.ToArray(), actualPatrol.Waypoints.ToArray());
            }
            else
            {
                Assert.Equal(expectedShip.ActiveOrder, actualShip.ActiveOrder);
            }
        }
    }

    private static void AssertPatrolLeg(
        SimulationState state,
        LocationId origin,
        LocationId destination,
        int nextWaypointIndex,
        SimulationTime departure,
        SimulationTime arrival
    )
    {
        ShipState ship = state.GetRequiredShip(PatrolId);
        PatrolRouteOrder patrol = Assert.IsType<PatrolRouteOrder>(ship.ActiveOrder);
        TravelingState traveling = Assert.IsType<TravelingState>(ship.StrategicState);
        Assert.Equal(new ShipOrderId(1), patrol.Id);
        Assert.Equal(nextWaypointIndex, patrol.NextWaypointIndex);
        Assert.Equal(origin, traveling.Travel.Origin);
        Assert.Equal(destination, traveling.Travel.Destination);
        Assert.Equal(departure, traveling.Travel.Departure);
        Assert.Equal(arrival, traveling.Travel.ExpectedArrival);
    }

    private static ShipState WithSensorRepair(ShipState ship, SystemRepairState repair, double condition) =>
        ship with
        {
            Engineering = ship.Engineering with
            {
                SensorCondition = new SystemCondition(condition),
                ActiveRepair = repair,
            },
        };

    private static SimulationTime Time(long hours) => new(hours * HourMilliseconds);
}
