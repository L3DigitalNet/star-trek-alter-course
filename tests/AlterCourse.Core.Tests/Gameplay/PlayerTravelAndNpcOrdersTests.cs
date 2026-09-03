using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies ordinary player travel remains independent from offscreen NPC order execution.</summary>
public sealed class PlayerTravelAndNpcOrdersTests
{
    private const long HourMilliseconds = 60 * 60 * 1000;
    private static readonly ShipDefinitionId DefinitionId = new("test-ship");
    private static readonly ShipInstanceId PlayerId = new(1);
    private static readonly ShipInstanceId PatrolId = new(2);
    private static readonly ShipInstanceId HoldId = new(3);
    private static readonly LocationId PlayerOrigin = new("player-origin");
    private static readonly LocationId PlayerDestination = new("player-destination");
    private static readonly LocationId PatrolAlpha = new("patrol-alpha");
    private static readonly LocationId PatrolBeta = new("patrol-beta");
    private static readonly LocationId HoldLocation = new("hold-location");

    /// <summary>Confirms player travel and independent NPC orders cross their boundaries in one headless run.</summary>
    [Fact]
    public void TravelingPlayerAndNpcOrdersProgressTogetherWithoutCoupling()
    {
        ShipDefinitionCatalog catalog = CreateCatalog();
        GameSimulation game = CreateSimulation(catalog);
        Assert.Equal(TravelOutcome.Accepted, game.RequestTravel(new TravelIntent(PlayerDestination)).Outcome);
        SimulationState initial = game.CaptureState();
        PatrolRouteOrder initialPatrol = Assert.IsType<PatrolRouteOrder>(initial.GetRequiredShip(PatrolId).ActiveOrder);

        SimulationAdvanceTraceResult advanced = GameSimulation.AdvanceTo(initial, Time(13), catalog);

        ShipState player = advanced.State.GetRequiredShip(PlayerId);
        Assert.Null(player.ActiveOrder);
        Assert.Equal(PlayerDestination, Assert.IsType<AtLocationState>(player.StrategicState).LocationId);

        ShipState patrolShip = advanced.State.GetRequiredShip(PatrolId);
        PatrolRouteOrder continuedPatrol = Assert.IsType<PatrolRouteOrder>(patrolShip.ActiveOrder);
        TravelingState patrolTravel = Assert.IsType<TravelingState>(patrolShip.StrategicState);
        Assert.Equal(initialPatrol.Id, continuedPatrol.Id);
        Assert.Equal(1, continuedPatrol.NextWaypointIndex);
        Assert.Equal(PatrolAlpha, patrolTravel.Travel.Origin);
        Assert.Equal(PatrolBeta, patrolTravel.Travel.Destination);
        Assert.Equal(Time(12), patrolTravel.Travel.Departure);
        Assert.Equal(Time(18), patrolTravel.Travel.ExpectedArrival);

        ShipState holder = advanced.State.GetRequiredShip(HoldId);
        Assert.Null(holder.ActiveOrder);
        Assert.Equal(HoldLocation, Assert.IsType<AtLocationState>(holder.StrategicState).LocationId);
        Assert.DoesNotContain(advanced.State.Scheduler.OutstandingWork, work => work.TargetShipId == HoldId);

        Assert.Equal(
            [
                (PatrolId, ScheduledWorkKind.TravelArrival, Time(6)),
                (HoldId, ScheduledWorkKind.OrderWake, Time(9)),
                (PatrolId, ScheduledWorkKind.TravelArrival, Time(12)),
                (PlayerId, ScheduledWorkKind.TravelArrival, Time(13)),
            ],
            advanced.Traces.Select(trace => (trace.TargetShipId, trace.WorkKind, trace.ResolutionTime))
        );
    }

    private static GameSimulation CreateSimulation(ShipDefinitionCatalog catalog)
    {
        StrategicMap map = CreateMap();
        ShipStart[] starts =
        [
            CreateStart(PlayerId, new AtLocationStart(PlayerOrigin)),
            CreateStart(
                PatrolId,
                new TravelingStart(PatrolAlpha, PatrolBeta, Time(0)),
                new PatrolRouteOrderStart([PatrolAlpha, PatrolBeta], 1)
            ),
            CreateStart(HoldId, new AtLocationStart(HoldLocation), new HoldUntilOrderStart(Time(9))),
        ];

        return new GameBootstrap(Time(3), map, PlayerId, starts).CreateSimulation(catalog);
    }

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
            new SystemCondition(1),
            strategic,
            ActiveOrder: activeOrder
        );

    private static StrategicMap CreateMap()
    {
        StrategicLocation playerOrigin = new(PlayerOrigin, "Player Origin", default);
        StrategicLocation playerDestination = new(PlayerDestination, "Player Destination", default);
        StrategicLocation patrolAlpha = new(PatrolAlpha, "Patrol Alpha", default);
        StrategicLocation patrolBeta = new(PatrolBeta, "Patrol Beta", default);
        StrategicLocation holdLocation = new(HoldLocation, "Hold Location", default);
        return new StrategicMap(
            [playerOrigin, playerDestination, patrolAlpha, patrolBeta, holdLocation],
            [
                new StrategicRoute(playerOrigin.Id, playerDestination.Id, Duration(10)),
                new StrategicRoute(patrolAlpha.Id, patrolBeta.Id, Duration(6)),
            ]
        );
    }

    private static ShipDefinitionCatalog CreateCatalog()
    {
        var definition = new ShipDefinition(
            DefinitionId,
            "Test ship",
            new SpeedKilometersPerSecond(10),
            new DistanceKilometers(30),
            new SimulationDuration(2000),
            Duration(6)
        );
        return new ShipDefinitionCatalog(
            new Dictionary<ShipDefinitionId, ShipDefinition> { [definition.Id] = definition }
        );
    }

    private static SimulationTime Time(long hours) => new(hours * HourMilliseconds);

    private static SimulationDuration Duration(long hours) => new(hours * HourMilliseconds);
}
