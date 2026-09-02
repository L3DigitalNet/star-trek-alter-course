using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies typed active-order bootstrap validation, allocation, and scheduler derivation.</summary>
public sealed class GameBootstrapOrderTests
{
    private const long HourMilliseconds = 60 * 60 * 1000;
    private static readonly ShipDefinitionId DefinitionId = new("test-ship");
    private static readonly ShipInstanceId PlayerId = new(10);
    private static readonly LocationId Alpha = new("alpha");
    private static readonly LocationId Beta = new("beta");
    private static readonly LocationId Gamma = new("gamma");

    /// <summary>Confirms declaration enumeration cannot change order allocation or scheduler sequence.</summary>
    [Fact]
    public void CanonicalizesOrderAndWorkAllocationByShipIdentity()
    {
        ShipStart player = CreateStart(PlayerId, new AtLocationStart(Alpha));
        ShipStart holder = CreateStart(
            new ShipInstanceId(20),
            new AtLocationStart(Gamma),
            new HoldUntilOrderStart(Time(9))
        );
        ShipStart traveler = CreateStart(
            new ShipInstanceId(30),
            new TravelingStart(Alpha, Beta, Time(0)),
            new TravelToOrderStart(Beta)
        );

        SimulationState forward = CreateBootstrap([player, holder, traveler])
            .CreateSimulation(CreateCatalog())
            .CaptureState();
        SimulationState reversed = CreateBootstrap([traveler, holder, player])
            .CreateSimulation(CreateCatalog())
            .CaptureState();

        Assert.Equal(forward.Scheduler.OutstandingWork.ToArray(), reversed.Scheduler.OutstandingWork.ToArray());
        Assert.Equal(3, forward.OrderIdAllocator.NextId);
        foreach (ShipStart start in new[] { player, holder, traveler })
        {
            ShipState forwardShip = forward.GetRequiredShip(start.InstanceId);
            ShipState reversedShip = reversed.GetRequiredShip(start.InstanceId);
            Assert.Equal(forwardShip.StrategicState, reversedShip.StrategicState);
            Assert.Equal(forwardShip.ActiveOrder, reversedShip.ActiveOrder);
        }

        Assert.Equal(new ShipOrderId(1), forward.GetRequiredShip(holder.InstanceId).ActiveOrder!.Id);
        Assert.Equal(new ShipOrderId(2), forward.GetRequiredShip(traveler.InstanceId).ActiveOrder!.Id);

        ScheduledWork holdWake = forward.Scheduler.OutstandingWork.Single(work =>
            work.TargetShipId == holder.InstanceId
        );
        ScheduledWork arrival = forward.Scheduler.OutstandingWork.Single(work =>
            work.TargetShipId == traveler.InstanceId
        );
        Assert.Equal(0, holdWake.Sequence);
        Assert.Equal(ScheduledWorkKind.OrderWake, holdWake.Kind);
        Assert.Equal(1, arrival.Sequence);
        Assert.Equal(ScheduledWorkKind.TravelArrival, arrival.Kind);
    }

    /// <summary>Confirms declared patrol progress remains correlated and is ready to continue at arrival.</summary>
    [Fact]
    public void RestoresInProgressPatrolAndContinuesItsExactNextLeg()
    {
        ShipStart patrol = CreateStart(
            new ShipInstanceId(20),
            new TravelingStart(Alpha, Beta, Time(0)),
            new PatrolRouteOrderStart([Alpha, Beta, Gamma], 1)
        );
        GameSimulation game = CreateBootstrap([CreateStart(PlayerId, new AtLocationStart(Gamma)), patrol])
            .CreateSimulation(CreateCatalog());
        SimulationState initial = game.CaptureState();
        ShipState initialPatrolShip = initial.GetRequiredShip(patrol.InstanceId);
        PatrolRouteOrder initialOrder = Assert.IsType<PatrolRouteOrder>(initialPatrolShip.ActiveOrder);
        TravelingState initialTravel = Assert.IsType<TravelingState>(initialPatrolShip.StrategicState);

        Assert.Equal(1, initialOrder.NextWaypointIndex);
        Assert.Equal(Time(0), initialTravel.Travel.Departure);
        Assert.Equal(Time(6), initialTravel.Travel.ExpectedArrival);

        SimulationAdvanceTraceResult advanced = GameSimulation.AdvanceTo(initial, Time(6), CreateCatalog());
        ShipState continuedShip = advanced.State.GetRequiredShip(patrol.InstanceId);
        PatrolRouteOrder continuedOrder = Assert.IsType<PatrolRouteOrder>(continuedShip.ActiveOrder);
        TravelingState continuedTravel = Assert.IsType<TravelingState>(continuedShip.StrategicState);

        Assert.Equal(initialOrder.Id, continuedOrder.Id);
        Assert.Equal(2, continuedOrder.NextWaypointIndex);
        Assert.Equal(Beta, continuedTravel.Travel.Origin);
        Assert.Equal(Gamma, continuedTravel.Travel.Destination);
        Assert.Equal(Time(6), continuedTravel.Travel.Departure);
        Assert.Equal(Time(8), continuedTravel.Travel.ExpectedArrival);
        Assert.Equal(
            continuedTravel.Travel.ScheduledArrivalId,
            Assert.Single(advanced.State.Scheduler.OutstandingWork).Id
        );
    }

    /// <summary>Confirms each hold owns exactly its independently derived wake identity.</summary>
    [Fact]
    public void DerivesIndependentExactHoldWakes()
    {
        ShipStart first = CreateStart(
            new ShipInstanceId(20),
            new AtLocationStart(Alpha),
            new HoldUntilOrderStart(Time(9))
        );
        ShipStart second = CreateStart(
            new ShipInstanceId(30),
            new AtLocationStart(Beta),
            new HoldUntilOrderStart(Time(10))
        );
        SimulationState state = CreateBootstrap([second, CreateStart(PlayerId, new AtLocationStart(Gamma)), first])
            .CreateSimulation(CreateCatalog())
            .CaptureState();

        HoldUntilOrder firstOrder = Assert.IsType<HoldUntilOrder>(state.GetRequiredShip(first.InstanceId).ActiveOrder);
        HoldUntilOrder secondOrder = Assert.IsType<HoldUntilOrder>(
            state.GetRequiredShip(second.InstanceId).ActiveOrder
        );
        ScheduledWork firstWake = Assert.Single(
            state.Scheduler.OutstandingWork,
            work => work.TargetShipId == first.InstanceId
        );
        ScheduledWork secondWake = Assert.Single(
            state.Scheduler.OutstandingWork,
            work => work.TargetShipId == second.InstanceId
        );

        Assert.Equal(firstWake.Id, firstOrder.ScheduledWakeId);
        Assert.Equal(firstWake.DueTime, firstOrder.Until);
        Assert.Equal(secondWake.Id, secondOrder.ScheduledWakeId);
        Assert.Equal(secondWake.DueTime, secondOrder.Until);
        Assert.NotEqual(firstOrder.ScheduledWakeId, secondOrder.ScheduledWakeId);
        Assert.All(state.Scheduler.OutstandingWork, work => Assert.Equal(ScheduledWorkKind.OrderWake, work.Kind));
    }

    /// <summary>Confirms ordinary active travel does not imply autonomous intent.</summary>
    [Fact]
    public void LeavesOrderlessTravelOrderless()
    {
        ShipStart traveler = CreateStart(new ShipInstanceId(20), new TravelingStart(Alpha, Beta, Time(0)));
        SimulationState state = CreateBootstrap([CreateStart(PlayerId, new AtLocationStart(Gamma)), traveler])
            .CreateSimulation(CreateCatalog())
            .CaptureState();

        Assert.Null(state.GetRequiredShip(traveler.InstanceId).ActiveOrder);
        Assert.Equal(1, state.OrderIdAllocator.NextId);
        Assert.Equal(ScheduledWorkKind.TravelArrival, Assert.Single(state.Scheduler.OutstandingWork).Kind);
    }

    /// <summary>Confirms the sole player-controlled ship cannot receive autonomous bootstrap intent.</summary>
    [Fact]
    public void RejectsPlayerOrderStart()
    {
        ShipStart player = CreateStart(PlayerId, new AtLocationStart(Alpha), new HoldUntilOrderStart(Time(9)));

        Assert.Throws<ArgumentException>(() => CreateBootstrap([player]));
    }

    /// <summary>Confirms TravelTo requires active travel to its exact declared destination.</summary>
    [Fact]
    public void RejectsUncorrelatedTravelToStarts()
    {
        ShipStart player = CreateStart(PlayerId, new AtLocationStart(Gamma));
        ShipStart mismatch = CreateStart(
            new ShipInstanceId(20),
            new TravelingStart(Alpha, Beta, Time(0)),
            new TravelToOrderStart(Gamma)
        );
        ShipStart stationary = CreateStart(
            new ShipInstanceId(20),
            new AtLocationStart(Alpha),
            new TravelToOrderStart(Beta)
        );

        Assert.Throws<ArgumentException>(() => CreateBootstrap([player, mismatch]).CreateSimulation(CreateCatalog()));
        Assert.Throws<ArgumentException>(() => CreateBootstrap([player, stationary]).CreateSimulation(CreateCatalog()));
    }

    /// <summary>Confirms patrol declarations stay bounded and reference a complete cyclic map route.</summary>
    [Fact]
    public void RejectsInvalidPatrolShapeAndMapReferences()
    {
        Assert.Throws<ArgumentException>(() => new PatrolRouteOrderStart([Alpha], 0));
        Assert.Throws<ArgumentException>(() =>
            new PatrolRouteOrderStart(Enumerable.Range(0, 17).Select(index => new LocationId($"point-{index}")), 0)
        );

        ShipStart player = CreateStart(PlayerId, new AtLocationStart(Gamma));
        ShipStrategicStart travel = new TravelingStart(Alpha, Beta, Time(0));
        ShipStart unknown = CreateStart(
            new ShipInstanceId(20),
            travel,
            new PatrolRouteOrderStart([Alpha, Beta, new LocationId("unknown")], 1)
        );
        ShipStart missingAdjacent = CreateStart(
            new ShipInstanceId(20),
            travel,
            new PatrolRouteOrderStart([Alpha, Beta, Gamma], 1)
        );

        Assert.Throws<ArgumentException>(() => CreateBootstrap([player, unknown]).CreateSimulation(CreateCatalog()));
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([player, missingAdjacent], CreateMap(includeBetaGamma: false))
                .CreateSimulation(CreateCatalog())
        );
        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([player, missingAdjacent], CreateMap(includeGammaAlpha: false))
                .CreateSimulation(CreateCatalog())
        );
    }

    /// <summary>Confirms patrol progress identifies the exact active physical leg.</summary>
    [Fact]
    public void RejectsPatrolProgressDestinationMismatch()
    {
        ShipStart patrol = CreateStart(
            new ShipInstanceId(20),
            new TravelingStart(Alpha, Beta, Time(0)),
            new PatrolRouteOrderStart([Alpha, Beta, Gamma], 2)
        );

        Assert.Throws<ArgumentException>(() =>
            CreateBootstrap([CreateStart(PlayerId, new AtLocationStart(Gamma)), patrol])
                .CreateSimulation(CreateCatalog())
        );
    }

    /// <summary>Confirms holds require a strictly future wake and stationary strategic state.</summary>
    [Fact]
    public void RejectsPastEqualAndTravelingHoldStarts()
    {
        ShipStart player = CreateStart(PlayerId, new AtLocationStart(Gamma));

        foreach (SimulationTime invalidUntil in new[] { Time(2), Time(3) })
        {
            ShipStart invalid = CreateStart(
                new ShipInstanceId(20),
                new AtLocationStart(Alpha),
                new HoldUntilOrderStart(invalidUntil)
            );
            Assert.Throws<ArgumentException>(() =>
                CreateBootstrap([player, invalid]).CreateSimulation(CreateCatalog())
            );
        }

        ShipStart traveling = CreateStart(
            new ShipInstanceId(20),
            new TravelingStart(Alpha, Beta, Time(0)),
            new HoldUntilOrderStart(Time(9))
        );
        Assert.Throws<ArgumentException>(() => CreateBootstrap([player, traveling]).CreateSimulation(CreateCatalog()));
    }

    /// <summary>Confirms the Milestone 2 proof begins mid-patrol with distinct future patrol and hold boundaries.</summary>
    [Fact]
    public void Milestone2ProofStartsAtThreeHoursWithGenuineInProgressIntent()
    {
        GameSimulation game = Milestone2ProofSetup.Create(CreateCatalog("pathfinder"));
        SimulationState state = game.CaptureState();
        ShipState player = state.GetRequiredShip(new ShipInstanceId(1));
        ShipState patrolShip = state.GetRequiredShip(new ShipInstanceId(2));
        ShipState holdShip = state.GetRequiredShip(new ShipInstanceId(3));
        TravelingState patrolTravel = Assert.IsType<TravelingState>(patrolShip.StrategicState);
        PatrolRouteOrder patrol = Assert.IsType<PatrolRouteOrder>(patrolShip.ActiveOrder);
        HoldUntilOrder hold = Assert.IsType<HoldUntilOrder>(holdShip.ActiveOrder);

        Assert.Equal(Time(3), state.Time);
        Assert.Null(player.ActiveOrder);
        Assert.IsType<AtLocationState>(player.StrategicState);
        Assert.Equal(Time(0), patrolTravel.Travel.Departure);
        Assert.Equal(Time(6), patrolTravel.Travel.ExpectedArrival);
        Assert.Equal(1, patrol.NextWaypointIndex);
        Assert.Equal(Time(9), hold.Until);
        Assert.Equal([Time(6), Time(9)], state.Scheduler.OutstandingWork.Select(work => work.DueTime));

        SimulationAdvanceTraceResult atPatrolBoundary = GameSimulation.AdvanceTo(
            state,
            Time(6),
            CreateCatalog("pathfinder")
        );
        ShipState continued = atPatrolBoundary.State.GetRequiredShip(new ShipInstanceId(2));
        TravelingState returnTravel = Assert.IsType<TravelingState>(continued.StrategicState);
        Assert.Equal(Time(6), returnTravel.Travel.Departure);
        Assert.Equal(Time(12), returnTravel.Travel.ExpectedArrival);
        Assert.IsType<HoldUntilOrder>(atPatrolBoundary.State.GetRequiredShip(new ShipInstanceId(3)).ActiveOrder);
    }

    private static GameBootstrap CreateBootstrap(IEnumerable<ShipStart> starts, StrategicMap? map = null) =>
        new(Time(3), map ?? CreateMap(), PlayerId, starts);

    private static ShipStart CreateStart(
        ShipInstanceId id,
        ShipStrategicStart strategic,
        ShipOrderStart? activeOrder = null
    ) =>
        new(
            id,
            DefinitionId,
            $"Ship {id.Value}",
            default,
            default,
            new SensorIntegrity(1),
            strategic,
            ActiveOrder: activeOrder
        );

    private static StrategicMap CreateMap(bool includeBetaGamma = true, bool includeGammaAlpha = true)
    {
        StrategicLocation alpha = new(Alpha, "Alpha", default);
        StrategicLocation beta = new(Beta, "Beta", default);
        StrategicLocation gamma = new(Gamma, "Gamma", default);
        List<StrategicRoute> routes = [new(alpha.Id, beta.Id, Duration(6))];
        if (includeBetaGamma)
        {
            routes.Add(new StrategicRoute(beta.Id, gamma.Id, Duration(2)));
        }

        if (includeGammaAlpha)
        {
            routes.Add(new StrategicRoute(gamma.Id, alpha.Id, Duration(4)));
        }

        return new StrategicMap([alpha, beta, gamma], routes);
    }

    private static ShipDefinitionCatalog CreateCatalog(string definitionId = "test-ship")
    {
        var definition = new ShipDefinition(
            new ShipDefinitionId(definitionId),
            "Test ship",
            new SpeedKilometersPerSecond(10),
            Duration(6)
        );
        return new ShipDefinitionCatalog(
            new Dictionary<ShipDefinitionId, ShipDefinition> { [definition.Id] = definition }
        );
    }

    private static SimulationTime Time(long hours) => new(hours * HourMilliseconds);

    private static SimulationDuration Duration(long hours) => new(hours * HourMilliseconds);
}
