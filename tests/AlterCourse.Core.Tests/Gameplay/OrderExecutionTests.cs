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

/// <summary>Verifies autonomous order invariants, execution, cancellation, and visibility boundaries.</summary>
public sealed class OrderExecutionTests
{
    private static readonly ShipInstanceId PlayerId = new(1);
    private static readonly ShipInstanceId NpcId = new(2);
    private static readonly ShipInstanceId SecondNpcId = new(3);
    private static readonly ShipDefinitionId DefinitionId = new("test-ship");
    private static readonly LocationId Alpha = new("alpha");
    private static readonly LocationId Beta = new("beta");
    private static readonly LocationId Gamma = new("gamma");

    /// <summary>Confirms aggregate validation binds active orders to unique identities and physical state.</summary>
    [Fact]
    public void ValidatesOrderIdentityIsolationAndPhysicalCorrelation()
    {
        (SimulationState valid, ShipDefinitionCatalog catalog) = CreateTravelOrderState(id => new TravelToOrder(
            id,
            Beta
        ));
        valid.Validate(catalog);

        ShipState npc = valid.GetRequiredShip(NpcId);
        Assert.Throws<InvalidOperationException>(() =>
            (valid with { OrderIdAllocator = ShipOrderIdAllocator.Restore(1) }).Validate(catalog)
        );
        Assert.Throws<InvalidOperationException>(() =>
            valid
                .ReplaceShip(NpcId, npc with { ActiveOrder = new TravelToOrder(new ShipOrderId(1), Gamma) })
                .Validate(catalog)
        );
        Assert.Throws<InvalidOperationException>(() => (valid with { PlayerShipId = NpcId }).Validate(catalog));
    }

    /// <summary>Confirms one active order identity cannot be attached to multiple ships.</summary>
    [Fact]
    public void RejectsDuplicateActiveOrderIdentities()
    {
        (SimulationState valid, ShipDefinitionCatalog catalog) = CreateTravelOrderState(id => new TravelToOrder(
            id,
            Beta
        ));
        SimulationScheduler scheduler = valid.Scheduler;
        (scheduler, ScheduledWork secondArrival) = scheduler.Schedule(
            new SimulationTime(1000),
            SecondNpcId,
            ScheduledWorkKind.TravelArrival
        );
        var secondTravel = new TravelState(Alpha, Beta, new SimulationTime(0), secondArrival.DueTime, secondArrival.Id);
        ShipState secondNpc = CreateShip(SecondNpcId, Alpha) with
        {
            StrategicState = new TravelingState(secondTravel),
            ActiveOrder = new TravelToOrder(new ShipOrderId(1), Beta),
        };
        var duplicateOrders = new SimulationState(
            valid.Time,
            scheduler,
            ShipInstanceIdAllocator.Restore(4),
            valid.StrategicMap,
            valid.PlayerShipId,
            [.. valid.Ships, secondNpc],
            valid.OrderIdAllocator
        );

        Assert.Throws<InvalidOperationException>(() => duplicateOrders.Validate(catalog));
    }

    /// <summary>Confirms patrol routes must close and every order wake must have one owning hold.</summary>
    [Fact]
    public void RejectsInvalidPatrolRoutesAndOrphanOrderWakes()
    {
        (SimulationState patrol, ShipDefinitionCatalog catalog) = CreateTravelOrderState(
            id => new PatrolRouteOrder(id, [Alpha, Beta, Gamma], 1),
            includeWrapRoute: false
        );
        Assert.Throws<InvalidOperationException>(() => patrol.Validate(catalog));

        var scheduler = SimulationScheduler.Create();
        (scheduler, _) = scheduler.Schedule(new SimulationTime(1000), NpcId, ScheduledWorkKind.OrderWake);
        SimulationState orphan = CreateState(scheduler, [CreateShip(PlayerId, Alpha), CreateShip(NpcId, Alpha)]);
        Assert.Throws<InvalidOperationException>(() => orphan.Validate(CreateCatalog()));
    }

    /// <summary>Confirms TravelTo is a deterministic one-shot with a fully typed internal trace.</summary>
    [Fact]
    public void TravelToCompletesOnceWithTypedDeterministicTrace()
    {
        (SimulationState state, ShipDefinitionCatalog catalog) = CreateTravelOrderState(id => new TravelToOrder(
            id,
            Beta
        ));

        SimulationAdvanceTraceResult result = GameSimulation.AdvanceTo(state, new SimulationTime(1000), catalog);
        ShipState npc = result.State.GetRequiredShip(NpcId);
        ScheduledConsequenceTrace trace = Assert.Single(result.Traces);

        Assert.Null(npc.ActiveOrder);
        Assert.Equal(Beta, Assert.IsType<AtLocationState>(npc.StrategicState).LocationId);
        Assert.Equal(ScheduledConsequenceRule.TravelToArrival, trace.Rule);
        Assert.Equal(ScheduledConsequenceAction.CompleteTravelTo, trace.Action);
        Assert.Equal(new ShipOrderId(1), trace.OrderId);
        Assert.Equal(ShipOrderKind.TravelTo, trace.OrderKind);
        Assert.True(trace.Completed);
        Assert.False(trace.RandomnessUsed);
    }

    /// <summary>Confirms patrol progress cycles through the shared nonplayer travel command path.</summary>
    [Fact]
    public void PatrolPersistsOneIdentityAndCyclesThroughSharedTravelApplication()
    {
        (SimulationState state, ShipDefinitionCatalog catalog) = CreateTravelOrderState(id => new PatrolRouteOrder(
            id,
            [Alpha, Beta, Gamma],
            1
        ));

        SimulationAdvanceTraceResult result = GameSimulation.AdvanceTo(state, new SimulationTime(3000), catalog);
        ShipState npc = result.State.GetRequiredShip(NpcId);
        PatrolRouteOrder patrol = Assert.IsType<PatrolRouteOrder>(npc.ActiveOrder);
        TravelingState traveling = Assert.IsType<TravelingState>(npc.StrategicState);

        Assert.Equal(new ShipOrderId(1), patrol.Id);
        Assert.Equal(1, patrol.NextWaypointIndex);
        Assert.Equal(Alpha, traveling.Travel.Origin);
        Assert.Equal(Beta, traveling.Travel.Destination);
        Assert.All(
            result.Traces,
            trace =>
            {
                Assert.Equal(new ShipOrderId(1), trace.OrderId);
                Assert.Equal(ScheduledConsequenceAction.ContinuePatrol, trace.Action);
                Assert.False(trace.Completed);
                Assert.False(trace.RandomnessUsed);
            }
        );
        Assert.Equal(3, result.Traces.Count);

        SimulationState reordered = new(
            state.Time,
            state.Scheduler,
            state.ShipIdAllocator,
            state.StrategicMap,
            state.PlayerShipId,
            state.Ships.Reverse(),
            state.OrderIdAllocator
        );
        SimulationAdvanceTraceResult reorderedResult = GameSimulation.AdvanceTo(
            reordered,
            new SimulationTime(3000),
            catalog
        );
        Assert.Equal(result.Traces, reorderedResult.Traces);
        ShipState reorderedNpc = reorderedResult.State.GetRequiredShip(NpcId);
        PatrolRouteOrder reorderedPatrol = Assert.IsType<PatrolRouteOrder>(reorderedNpc.ActiveOrder);
        Assert.Equal(npc.StrategicState, reorderedNpc.StrategicState);
        Assert.Equal(patrol.Id, reorderedPatrol.Id);
        Assert.Equal(patrol.NextWaypointIndex, reorderedPatrol.NextWaypointIndex);
        Assert.Equal(patrol.Waypoints.ToArray(), reorderedPatrol.Waypoints.ToArray());
    }

    /// <summary>Confirms the shared travel command accepts a declared nonplayer target.</summary>
    [Fact]
    public void SharedTravelApplicationConsumesNonplayerCommand()
    {
        SimulationState atLocation = CreateState(
            SimulationScheduler.Create(),
            [CreateShip(PlayerId, Alpha), CreateShip(NpcId, Alpha)]
        );

        ShipTravelApplicationResult application = GameSimulation.ApplyShipTravel(
            atLocation,
            new ShipTravelCommand(NpcId, Beta)
        );

        Assert.Equal(TravelOutcome.Accepted, application.Outcome);
        Assert.IsType<TravelingState>(application.CandidateState.GetRequiredShip(NpcId).StrategicState);
    }

    /// <summary>Confirms stale identities do nothing and cancellation does not cancel a physical leg.</summary>
    [Fact]
    public void ExactCancellationPreservesUnrelatedWorkAndLetsPhysicalLegFinish()
    {
        (SimulationState state, ShipDefinitionCatalog catalog) = CreateTravelOrderState(id => new TravelToOrder(
            id,
            Beta
        ));
        var game = GameSimulation.RestoreState(state, catalog);
        ScheduledWork arrival = Assert.Single(state.Scheduler.OutstandingWork);

        Assert.Equal(OrderCancellationOutcome.NotFound, game.CancelOrder(new ShipOrderId(99)).Outcome);
        Assert.Equal(state, game.CaptureState());
        Assert.Equal(OrderCancellationOutcome.Cancelled, game.CancelOrder(new ShipOrderId(1)).Outcome);
        Assert.Null(game.CaptureState().GetRequiredShip(NpcId).ActiveOrder);
        Assert.Equal(arrival, Assert.Single(game.CaptureState().Scheduler.OutstandingWork));

        ShipState orderlessNpc = state.GetRequiredShip(NpcId) with { ActiveOrder = null };
        SimulationState orderlessState = state.ReplaceShip(NpcId, orderlessNpc) with
        {
            OrderIdAllocator = ShipOrderIdAllocator.Create(),
        };
        var orderless = GameSimulation.RestoreState(orderlessState, catalog);
        var metadata = new GameSaveMetadata(
            "cancelled-order",
            "Cancelled Order",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch
        );
        Assert.Equal(GamePersistence.Serialize(orderless, metadata), GamePersistence.Serialize(game, metadata));

        SimulationAdvanceTraceResult completed = GameSimulation.AdvanceTo(
            game.CaptureState(),
            new SimulationTime(1000),
            catalog
        );
        Assert.IsType<AtLocationState>(completed.State.GetRequiredShip(NpcId).StrategicState);
        Assert.Empty(completed.State.Scheduler.OutstandingWork);
        Assert.Null(Assert.Single(completed.Traces).OrderId);
    }

    /// <summary>Confirms hold cancellation removes its exact wake while preserving unrelated work.</summary>
    [Fact]
    public void HoldCancellationRemovesOnlyItsExactWake()
    {
        var scheduler = SimulationScheduler.Create();
        (scheduler, ScheduledWork wake) = scheduler.Schedule(
            new SimulationTime(600),
            NpcId,
            ScheduledWorkKind.OrderWake
        );
        (scheduler, ScheduledWork repairWork) = scheduler.Schedule(
            new SimulationTime(800),
            PlayerId,
            ScheduledWorkKind.SensorRepairCompletion
        );
        var repair = new SensorRepairState(
            new SensorIntegrity(0.5),
            new SensorIntegrity(1),
            new SimulationTime(0),
            new SimulationTime(800),
            repairWork.Id
        );
        ShipState player = CreateShip(PlayerId, Alpha) with
        {
            SensorIntegrity = new SensorIntegrity(0.5),
            SensorRepair = repair,
        };
        ShipState npc = CreateShip(NpcId, Beta) with
        {
            ActiveOrder = new HoldUntilOrder(new ShipOrderId(1), new SimulationTime(600), wake.Id),
        };
        SimulationState state = CreateState(
            scheduler,
            [player, npc],
            orderIdAllocator: ShipOrderIdAllocator.Restore(2)
        );
        ShipDefinitionCatalog catalog = CreateCatalog(new SimulationDuration(800));
        var game = GameSimulation.RestoreState(state, catalog);

        Assert.Equal(OrderCancellationOutcome.Cancelled, game.CancelOrder(new ShipOrderId(1)).Outcome);
        Assert.Equal(repairWork, Assert.Single(game.CaptureState().Scheduler.OutstandingWork));
        Assert.Equal(player.StrategicState, game.CaptureState().GetRequiredShip(PlayerId).StrategicState);
        Assert.Equal(npc.StrategicState, game.CaptureState().GetRequiredShip(NpcId).StrategicState);
    }

    /// <summary>Confirms hidden work resolves before the player boundary without becoming public output.</summary>
    [Fact]
    public void PlayerRelevantAdvanceResolvesHiddenEarlierWorkWithoutLeakingIt()
    {
        (SimulationState state, ShipDefinitionCatalog catalog) = CreateHiddenHoldAndPlayerRepairState();
        var game = GameSimulation.RestoreState(state, catalog);

        SimulationAdvanceTraceResult traced = GameSimulation.AdvanceTo(state, new SimulationTime(800), catalog);
        ScheduledConsequenceTrace hiddenTrace = traced.Traces[0];
        Assert.Equal(NpcId, hiddenTrace.TargetShipId);
        Assert.Equal(ScheduledConsequenceRule.HoldUntilWake, hiddenTrace.Rule);
        Assert.Equal(ScheduledConsequenceAction.CompleteHold, hiddenTrace.Action);
        Assert.True(hiddenTrace.Completed);
        Assert.False(hiddenTrace.RandomnessUsed);

        AdvanceUntilResult result = game.AdvanceUntilNextPlayerRelevantEvent();

        Assert.Equal(800, result.StoppedAt.Milliseconds);
        Assert.Equal([ScheduledWorkKind.SensorRepairCompletion], result.ResolvedKinds);
        Assert.Null(game.CaptureState().GetRequiredShip(NpcId).ActiveOrder);
        Assert.Null(game.CaptureState().GetRequiredShip(PlayerId).SensorRepair);

        SimulationState hiddenOnly = state.ReplaceShip(PlayerId, CreateShip(PlayerId, Alpha)) with
        {
            Scheduler = SimulationScheduler.Restore(2, 1, [state.Scheduler.OutstandingWork[0]]),
        };
        var noPlayerBoundary = GameSimulation.RestoreState(hiddenOnly, catalog);
        AdvanceUntilResult noEvent = noPlayerBoundary.AdvanceUntilNextScheduledEvent();
        Assert.Equal(AdvanceUntilOutcome.NoScheduledEvent, noEvent.Outcome);
        Assert.Equal(new SimulationTime(0), noEvent.StoppedAt);
        Assert.Equal(hiddenOnly, noPlayerBoundary.CaptureState());
    }

    /// <summary>Confirms an earlier same-time patrol consequence cannot block the player boundary.</summary>
    [Fact]
    public void SameBoundaryPatrolBeforePlayerRepairProgressesAndReportsOnlyPlayerWork()
    {
        (SimulationState state, ShipDefinitionCatalog catalog) = CreateSameBoundaryPatrolAndRepairState();
        var game = GameSimulation.RestoreState(state, catalog);

        AdvanceUntilResult result = game.AdvanceUntilNextPlayerRelevantEvent();

        Assert.Equal(new SimulationTime(1000), result.StoppedAt);
        Assert.Equal([ScheduledWorkKind.SensorRepairCompletion], result.ResolvedKinds);
        Assert.Null(game.CaptureState().GetRequiredShip(PlayerId).SensorRepair);
        AssertPatrolLeg(game.CaptureState(), NpcId, new ShipOrderId(1), 2, Beta, Gamma);
    }

    /// <summary>Confirms either same-time patrol sequence advances both owners deterministically.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameBoundaryPatrolArrivalsProgressForEitherSequenceOrder(bool reverseSequenceAndInsertion)
    {
        (SimulationState state, ShipDefinitionCatalog catalog) = CreateSameBoundaryPatrolState(
            reverseSequenceAndInsertion
        );

        SimulationAdvanceTraceResult result = GameSimulation.AdvanceTo(state, new SimulationTime(1000), catalog);

        ShipInstanceId[] expectedOrder = reverseSequenceAndInsertion ? [SecondNpcId, NpcId] : [NpcId, SecondNpcId];
        Assert.Equal(expectedOrder, result.Traces.Select(trace => trace.TargetShipId));
        Assert.Equal(expectedOrder, result.State.Scheduler.OutstandingWork.Select(work => work.TargetShipId));
        Assert.All(
            result.State.Scheduler.OutstandingWork,
            work => Assert.Equal(new SimulationTime(2000), work.DueTime)
        );
        AssertPatrolLeg(result.State, NpcId, new ShipOrderId(1), 2, Beta, Gamma);
        AssertPatrolLeg(result.State, SecondNpcId, new ShipOrderId(2), 0, Gamma, Alpha);
    }

    /// <summary>Confirms zero speed permits a long strategic jump regardless of heading.</summary>
    [Fact]
    public void StrategicJumpHandlesSeventyTwoHoursAndRepairsAnalytically()
    {
        var duration = new SimulationDuration(72L * 60 * 60 * 1000);
        var scheduler = SimulationScheduler.Create();
        (scheduler, ScheduledWork work) = scheduler.Schedule(
            new SimulationTime(duration.Milliseconds),
            PlayerId,
            ScheduledWorkKind.SensorRepairCompletion
        );
        var repair = new SensorRepairState(
            new SensorIntegrity(0.25),
            new SensorIntegrity(1),
            new SimulationTime(0),
            work.DueTime,
            work.Id
        );
        ShipState player = CreateShip(PlayerId, Alpha) with
        {
            SensorIntegrity = new SensorIntegrity(0.25),
            SensorRepair = repair,
        };
        ShipState stationaryNpc = CreateShip(NpcId, Beta) with
        {
            TacticalPosition = new TacticalPosition(4, -3),
            TacticalMotion = new TacticalMotion(new HeadingDegrees(237), new SpeedKilometersPerSecond(0)),
        };
        SimulationState state = CreateState(scheduler, [player, stationaryNpc]);
        var game = GameSimulation.RestoreState(state, CreateCatalog(duration));

        AdvanceUntilResult result = game.AdvanceUntilNextPlayerRelevantEvent();

        Assert.Equal(duration.Milliseconds, result.StoppedAt.Milliseconds);
        Assert.Equal(1, result.Projection.Ship.Sensors.Integrity);
        Assert.False(result.Projection.Ship.Sensors.IsRepairing);
        ShipState advancedNpc = game.CaptureState().GetRequiredShip(NpcId);
        Assert.Equal(stationaryNpc.TacticalPosition, advancedNpc.TacticalPosition);
        Assert.Equal(stationaryNpc.TacticalMotion, advancedNpc.TacticalMotion);
    }

    /// <summary>Confirms the total consequence budget rejects the candidate at the documented attempt.</summary>
    [Fact]
    public void TotalConsequenceBudgetRejectsAtomicallyAtDeterministicAttempt()
    {
        (SimulationState state, ShipDefinitionCatalog catalog) = CreateTravelOrderState(
            id => new PatrolRouteOrder(id, [Alpha, Beta], 1),
            routeDuration: new SimulationDuration(100)
        );
        var game = GameSimulation.RestoreState(state, catalog);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            GameSimulation.AdvanceTo(state, new SimulationTime(1_000_100), catalog)
        );

        Assert.Contains("10000 total consequence execution budget", exception.Message, StringComparison.Ordinal);
        Assert.Contains("attempt 10001", exception.Message, StringComparison.Ordinal);
        Assert.Equal(state, game.CaptureState());
    }

    private static (SimulationState State, ShipDefinitionCatalog Catalog) CreateTravelOrderState(
        Func<ShipOrderId, ShipOrder> createOrder,
        bool includeWrapRoute = true,
        SimulationDuration? routeDuration = null
    )
    {
        SimulationDuration duration = routeDuration ?? new SimulationDuration(1000);
        StrategicMap map = CreateMap(duration, includeWrapRoute);
        var scheduler = SimulationScheduler.Create();
        (scheduler, ScheduledWork arrival) = scheduler.Schedule(
            new SimulationTime(duration.Milliseconds),
            NpcId,
            ScheduledWorkKind.TravelArrival
        );
        var travel = new TravelState(Alpha, Beta, new SimulationTime(0), arrival.DueTime, arrival.Id);
        ShipState npc = CreateShip(NpcId, Alpha) with
        {
            StrategicState = new TravelingState(travel),
            ActiveOrder = createOrder(new ShipOrderId(1)),
        };
        SimulationState state = CreateState(
            scheduler,
            [CreateShip(PlayerId, Alpha), npc],
            map,
            ShipOrderIdAllocator.Restore(2)
        );
        return (state, CreateCatalog());
    }

    private static (SimulationState State, ShipDefinitionCatalog Catalog) CreateHiddenHoldAndPlayerRepairState()
    {
        var scheduler = SimulationScheduler.Create();
        (scheduler, ScheduledWork wake) = scheduler.Schedule(
            new SimulationTime(600),
            NpcId,
            ScheduledWorkKind.OrderWake
        );
        (scheduler, ScheduledWork repairWork) = scheduler.Schedule(
            new SimulationTime(800),
            PlayerId,
            ScheduledWorkKind.SensorRepairCompletion
        );
        var repair = new SensorRepairState(
            new SensorIntegrity(0.5),
            new SensorIntegrity(1),
            new SimulationTime(0),
            repairWork.DueTime,
            repairWork.Id
        );
        ShipState player = CreateShip(PlayerId, Alpha) with
        {
            SensorIntegrity = new SensorIntegrity(0.5),
            SensorRepair = repair,
        };
        ShipState npc = CreateShip(NpcId, Beta) with
        {
            ActiveOrder = new HoldUntilOrder(new ShipOrderId(1), wake.DueTime, wake.Id),
        };
        return (
            CreateState(scheduler, [player, npc], orderIdAllocator: ShipOrderIdAllocator.Restore(2)),
            CreateCatalog(new SimulationDuration(800))
        );
    }

    private static (SimulationState State, ShipDefinitionCatalog Catalog) CreateSameBoundaryPatrolAndRepairState()
    {
        var scheduler = SimulationScheduler.Create();
        (scheduler, ScheduledWork arrival) = scheduler.Schedule(
            new SimulationTime(1000),
            NpcId,
            ScheduledWorkKind.TravelArrival
        );
        (scheduler, ScheduledWork repairWork) = scheduler.Schedule(
            new SimulationTime(1000),
            PlayerId,
            ScheduledWorkKind.SensorRepairCompletion
        );
        var repair = new SensorRepairState(
            new SensorIntegrity(0.5),
            new SensorIntegrity(1),
            new SimulationTime(0),
            repairWork.DueTime,
            repairWork.Id
        );
        ShipState player = CreateShip(PlayerId, Alpha) with
        {
            SensorIntegrity = new SensorIntegrity(0.5),
            SensorRepair = repair,
        };
        ShipState npc = CreatePatrollingShip(NpcId, new ShipOrderId(1), Alpha, Beta, 1, arrival);
        return (
            CreateState(scheduler, [player, npc], orderIdAllocator: ShipOrderIdAllocator.Restore(2)),
            CreateCatalog()
        );
    }

    private static (SimulationState State, ShipDefinitionCatalog Catalog) CreateSameBoundaryPatrolState(
        bool reverseSequenceAndInsertion
    )
    {
        (SimulationScheduler scheduler, ScheduledWork firstArrival, ScheduledWork secondArrival) =
            ScheduleSameBoundaryArrivals(reverseSequenceAndInsertion);
        ShipState firstNpc = CreatePatrollingShip(NpcId, new ShipOrderId(1), Alpha, Beta, 1, firstArrival);
        ShipState secondNpc = CreatePatrollingShip(SecondNpcId, new ShipOrderId(2), Beta, Gamma, 2, secondArrival);
        ShipState player = CreateShip(PlayerId, Alpha);
        ShipState[] ships = reverseSequenceAndInsertion ? [secondNpc, player, firstNpc] : [player, firstNpc, secondNpc];
        var state = new SimulationState(
            new SimulationTime(0),
            scheduler,
            ShipInstanceIdAllocator.Restore(4),
            CreateMap(new SimulationDuration(1000)),
            PlayerId,
            ships,
            ShipOrderIdAllocator.Restore(3)
        );
        return (state, CreateCatalog());
    }

    private static (
        SimulationScheduler Scheduler,
        ScheduledWork First,
        ScheduledWork Second
    ) ScheduleSameBoundaryArrivals(bool reverseSequence)
    {
        var scheduler = SimulationScheduler.Create();
        ScheduledWork first;
        ScheduledWork second;
        if (reverseSequence)
        {
            (scheduler, second) = scheduler.Schedule(
                new SimulationTime(1000),
                SecondNpcId,
                ScheduledWorkKind.TravelArrival
            );
            (scheduler, first) = scheduler.Schedule(new SimulationTime(1000), NpcId, ScheduledWorkKind.TravelArrival);
        }
        else
        {
            (scheduler, first) = scheduler.Schedule(new SimulationTime(1000), NpcId, ScheduledWorkKind.TravelArrival);
            (scheduler, second) = scheduler.Schedule(
                new SimulationTime(1000),
                SecondNpcId,
                ScheduledWorkKind.TravelArrival
            );
        }

        return (scheduler, first, second);
    }

    private static ShipState CreatePatrollingShip(
        ShipInstanceId shipId,
        ShipOrderId orderId,
        LocationId origin,
        LocationId destination,
        int nextWaypointIndex,
        ScheduledWork arrival
    )
    {
        var travel = new TravelState(origin, destination, new SimulationTime(0), arrival.DueTime, arrival.Id);
        return CreateShip(shipId, origin) with
        {
            StrategicState = new TravelingState(travel),
            ActiveOrder = new PatrolRouteOrder(orderId, [Alpha, Beta, Gamma], nextWaypointIndex),
        };
    }

    private static void AssertPatrolLeg(
        SimulationState state,
        ShipInstanceId shipId,
        ShipOrderId orderId,
        int nextWaypointIndex,
        LocationId origin,
        LocationId destination
    )
    {
        ShipState ship = state.GetRequiredShip(shipId);
        PatrolRouteOrder patrol = Assert.IsType<PatrolRouteOrder>(ship.ActiveOrder);
        TravelingState traveling = Assert.IsType<TravelingState>(ship.StrategicState);
        Assert.Equal(orderId, patrol.Id);
        Assert.Equal(nextWaypointIndex, patrol.NextWaypointIndex);
        Assert.Equal(origin, traveling.Travel.Origin);
        Assert.Equal(destination, traveling.Travel.Destination);
    }

    private static SimulationState CreateState(
        SimulationScheduler scheduler,
        IEnumerable<ShipState> ships,
        StrategicMap? map = null,
        ShipOrderIdAllocator? orderIdAllocator = null
    ) =>
        new(
            new SimulationTime(0),
            scheduler,
            ShipInstanceIdAllocator.Restore(3),
            map ?? CreateMap(new SimulationDuration(1000)),
            PlayerId,
            ships,
            orderIdAllocator
        );

    private static ShipState CreateShip(ShipInstanceId id, LocationId location) =>
        new(
            id,
            DefinitionId,
            $"Ship {id.Value}",
            default,
            default,
            new SensorIntegrity(1),
            null,
            new AtLocationState(location)
        );

    private static StrategicMap CreateMap(SimulationDuration duration, bool includeWrapRoute = true)
    {
        StrategicLocation alpha = new(Alpha, "Alpha", default);
        StrategicLocation beta = new(Beta, "Beta", default);
        StrategicLocation gamma = new(Gamma, "Gamma", default);
        List<StrategicRoute> routes = [new(alpha.Id, beta.Id, duration), new(beta.Id, gamma.Id, duration)];
        if (includeWrapRoute)
        {
            routes.Add(new StrategicRoute(gamma.Id, alpha.Id, duration));
        }

        return new StrategicMap([alpha, beta, gamma], routes);
    }

    private static ShipDefinitionCatalog CreateCatalog(SimulationDuration? repairDuration = null)
    {
        var definition = new ShipDefinition(
            DefinitionId,
            "Test ship",
            new SpeedKilometersPerSecond(10),
            repairDuration ?? new SimulationDuration(1000)
        );
        return new ShipDefinitionCatalog(
            new Dictionary<ShipDefinitionId, ShipDefinition> { [DefinitionId] = definition }
        );
    }
}
