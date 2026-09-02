using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies the deterministic headless gameplay aggregate and its player contract.</summary>
public sealed class GameSimulationTests
{
    /// <summary>Confirms setup is deterministic, damaged, repairing, and projected read-only.</summary>
    [Fact]
    public void FirstSetupIsDeterministicDamagedAndReadOnly()
    {
        GameSimulation first = CreateGame();
        GameSimulation second = CreateGame();

        PlayerProjection projection = first.GetPlayerProjection();

        Assert.Equal(1, projection.Ship.InstanceId.Value);
        Assert.Equal("USS Pathfinder", projection.Ship.DisplayName);
        Assert.Equal(projection, second.GetPlayerProjection());
        Assert.Equal(3, projection.Strategic.Locations.Count);
        Assert.Equal(2, projection.Strategic.Routes.Count);
        Assert.NotNull(projection.Strategic.CurrentLocation);
        Assert.Null(projection.Strategic.Travel);
        Assert.Equal(0.4, projection.Ship.Sensors.Integrity, 10);
        Assert.True(projection.Ship.Sensors.IsRepairing);
        Assert.Equal(0, projection.Ship.Sensors.RepairProgress);
        Assert.Contains(PlayerAction.Travel, projection.AvailableActions);
        Assert.Contains(PlayerAction.SetTacticalCourse, projection.AvailableActions);

        Assert.IsAssignableFrom<IReadOnlyList<StrategicLocationProjection>>(projection.Strategic.Locations);
        Assert.NotSame(projection.Strategic.Locations, first.GetPlayerProjection().Strategic.Locations);
    }

    /// <summary>Confirms invalid travel is rejected atomically and active travel cannot be replaced.</summary>
    [Fact]
    public void TravelRejectsSelfUnconnectedAndAlreadyTravelingWithoutChangingState()
    {
        GameSimulation game = CreateGame();
        PlayerProjection initial = game.GetPlayerProjection();
        var metadata = new GameSaveMetadata(
            "travel-rejection",
            "Travel Rejection",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch
        );
        byte[] initialSnapshot = GamePersistence.Serialize(game, metadata);
        LocationId origin = initial.Strategic.CurrentLocation!.Id;
        LocationId connected = initial.Strategic.Routes.Single(route => route.Origin == origin).Destination;
        LocationId unconnected = initial
            .Strategic.Locations.Select(location => location.Id)
            .Single(id => id != origin && id != connected);

        Assert.Equal(TravelOutcome.SameLocation, game.RequestTravel(new TravelIntent(origin)).Outcome);
        Assert.Equal(TravelOutcome.RouteUnavailable, game.RequestTravel(new TravelIntent(unconnected)).Outcome);
        Assert.Equal(initial, game.GetPlayerProjection());
        Assert.Equal(initialSnapshot, GamePersistence.Serialize(game, metadata));

        Assert.Equal(TravelOutcome.Accepted, game.RequestTravel(new TravelIntent(connected)).Outcome);
        PlayerProjection traveling = game.GetPlayerProjection();
        byte[] travelingSnapshot = GamePersistence.Serialize(game, metadata);
        Assert.Equal(TravelOutcome.AlreadyTraveling, game.RequestTravel(new TravelIntent(unconnected)).Outcome);
        Assert.DoesNotContain(PlayerAction.Travel, traveling.AvailableActions);
        Assert.DoesNotContain(PlayerAction.SetTacticalCourse, traveling.AvailableActions);
        Assert.Contains(PlayerAction.AdvanceTime, traveling.AvailableActions);
        Assert.Equal(traveling, game.GetPlayerProjection());
        Assert.Equal(travelingSnapshot, GamePersistence.Serialize(game, metadata));
    }

    /// <summary>Confirms travel remains explicit until its scheduled arrival boundary.</summary>
    [Fact]
    public void TravelPersistsUntilArrivalAndDoesNotTeleport()
    {
        GameSimulation game = CreateGame();
        LocationId origin = game.GetPlayerProjection().Strategic.CurrentLocation!.Id;
        LocationId destination = ConnectedDestination(game);

        TravelRequestResult accepted = game.RequestTravel(new TravelIntent(destination));
        game.AdvanceFixedSteps(119);
        PlayerProjection beforeArrival = game.GetPlayerProjection();

        Assert.Equal(TravelOutcome.Accepted, accepted.Outcome);
        Assert.Null(beforeArrival.Strategic.CurrentLocation);
        Assert.Equal(origin, beforeArrival.Strategic.Travel!.Origin);
        Assert.Equal(destination, beforeArrival.Strategic.Travel.Destination);
        Assert.Equal(12000, beforeArrival.Strategic.Travel.ExpectedArrival.Milliseconds);
        Assert.True(beforeArrival.Strategic.Travel.IsActive);

        game.AdvanceFixedSteps(1);
        PlayerProjection arrived = game.GetPlayerProjection();
        Assert.Equal(destination, arrived.Strategic.CurrentLocation!.Id);
        Assert.Null(arrived.Strategic.Travel);
        Assert.Equal(0.25, arrived.Ship.Tactical.Position.XKilometers, 10);
        Assert.Equal(-0.75, arrived.Ship.Tactical.Position.YKilometers, 10);
    }

    /// <summary>Confirms tactical headings are clockwise from north with the required cardinal motion.</summary>
    [Theory]
    [InlineData(0, 0, 1)]
    [InlineData(90, 1, 0)]
    [InlineData(180, 0, -1)]
    [InlineData(270, -1, 0)]
    public void TacticalHeadingUsesClockwiseDegreesFromNorth(double heading, double expectedX, double expectedY)
    {
        GameSimulation game = CreateGame();
        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(
                new SetTacticalCourseIntent(new HeadingDegrees(heading), new SpeedKilometersPerSecond(10))
            ).Outcome
        );

        game.AdvanceFixedSteps(1);
        TacticalProjection tactical = game.GetPlayerProjection().Ship.Tactical;

        Assert.Equal(3.25 + expectedX, tactical.Position.XKilometers, 10);
        Assert.Equal(-7.5 + expectedY, tactical.Position.YKilometers, 10);
    }

    /// <summary>Confirms non-grid diagonal motion is invariant to fixed-step batching.</summary>
    [Fact]
    public void TacticalMovementIsContinuousAndBatchEquivalent()
    {
        GameSimulation tenSingles = CreateGame();
        GameSimulation twoBatches = CreateGame();
        GameSimulation oneBatch = CreateGame();
        var intent = new SetTacticalCourseIntent(new HeadingDegrees(45), new SpeedKilometersPerSecond(3.5));
        tenSingles.SetTacticalCourse(intent);
        twoBatches.SetTacticalCourse(intent);
        oneBatch.SetTacticalCourse(intent);

        for (int index = 0; index < 10; index++)
        {
            tenSingles.AdvanceFixedSteps(1);
        }

        twoBatches.AdvanceFixedSteps(5);
        twoBatches.AdvanceFixedSteps(5);
        oneBatch.AdvanceFixedSteps(10);

        Assert.Equal(tenSingles.GetPlayerProjection(), twoBatches.GetPlayerProjection());
        Assert.Equal(tenSingles.GetPlayerProjection(), oneBatch.GetPlayerProjection());
        Assert.NotEqual(3.25, tenSingles.GetPlayerProjection().Ship.Tactical.Position.XKilometers);
    }

    /// <summary>Confirms zero speed holds position and invalid course requests are atomic.</summary>
    [Fact]
    public void ZeroSpeedHoldsPositionAndInvalidCoursesAreAtomic()
    {
        GameSimulation game = CreateGame();
        PlayerProjection initial = game.GetPlayerProjection();

        Assert.Equal(
            SetTacticalCourseOutcome.SpeedExceedsMaximum,
            game.SetTacticalCourse(
                new SetTacticalCourseIntent(new HeadingDegrees(10), new SpeedKilometersPerSecond(10.01))
            ).Outcome
        );
        Assert.Equal(initial, game.GetPlayerProjection());

        game.SetTacticalCourse(new SetTacticalCourseIntent(new HeadingDegrees(90), new SpeedKilometersPerSecond(0)));
        game.AdvanceFixedSteps(5);
        Assert.Equal(initial.Ship.Tactical.Position, game.GetPlayerProjection().Ship.Tactical.Position);

        game.RequestTravel(new TravelIntent(ConnectedDestination(game)));
        PlayerProjection traveling = game.GetPlayerProjection();
        Assert.Equal(
            SetTacticalCourseOutcome.UnavailableWhileTraveling,
            game.SetTacticalCourse(
                new SetTacticalCourseIntent(new HeadingDegrees(20), new SpeedKilometersPerSecond(1))
            ).Outcome
        );
        Assert.Equal(traveling, game.GetPlayerProjection());
    }

    /// <summary>Confirms the authored maximum tactical speed is an inclusive valid command boundary.</summary>
    [Fact]
    public void MaximumTacticalSpeedIsAccepted()
    {
        GameSimulation game = CreateGame();

        SetTacticalCourseResult result = game.SetTacticalCourse(
            new SetTacticalCourseIntent(new HeadingDegrees(0), new SpeedKilometersPerSecond(10))
        );
        game.AdvanceFixedSteps(1);

        Assert.Equal(SetTacticalCourseOutcome.Accepted, result.Outcome);
        Assert.Equal(-6.5, game.GetPlayerProjection().Ship.Tactical.Position.YKilometers, 10);
    }

    /// <summary>Confirms one clock drives meaningful repair progress during strategic travel.</summary>
    [Fact]
    public void RepairProgressesDuringTravelAndCompletesBeforeArrival()
    {
        GameSimulation game = CreateGame();
        game.RequestTravel(new TravelIntent(ConnectedDestination(game)));
        game.AdvanceFixedSteps(40);

        PlayerProjection halfway = game.GetPlayerProjection();
        Assert.Equal(0.5, halfway.Ship.Sensors.RepairProgress, 10);
        Assert.Equal(0.7, halfway.Ship.Sensors.Integrity, 10);
        Assert.True(halfway.Ship.Sensors.IsRepairing);
        Assert.NotNull(halfway.Strategic.Travel);

        game.AdvanceFixedSteps(40);
        PlayerProjection complete = game.GetPlayerProjection();
        Assert.Equal(1, complete.Ship.Sensors.Integrity);
        Assert.Equal(1, complete.Ship.Sensors.RepairProgress);
        Assert.False(complete.Ship.Sensors.IsRepairing);
        Assert.NotNull(complete.Strategic.Travel);
    }

    /// <summary>Confirms scheduler-boundary advancement matches ordinary fixed-step consequences.</summary>
    [Fact]
    public void AdvanceUntilUsesSchedulerBoundariesAndMatchesOrdinaryAdvance()
    {
        GameSimulation until = CreateGame();
        GameSimulation ordinary = CreateGame();
        LocationId destination = ConnectedDestination(until);
        until.RequestTravel(new TravelIntent(destination));
        ordinary.RequestTravel(new TravelIntent(destination));

        AdvanceUntilResult repair = until.AdvanceUntilNextScheduledEvent();
        AdvanceUntilResult arrival = until.AdvanceUntilNextScheduledEvent();
        SimulationAdvanceResult ordinaryAdvance = ordinary.AdvanceFixedSteps(120);

        Assert.Equal(AdvanceUntilOutcome.ScheduledEventResolved, repair.Outcome);
        Assert.Equal(8000, repair.StoppedAt.Milliseconds);
        Assert.Equal([ScheduledWorkKind.SensorRepairCompletion], repair.ResolvedKinds);
        Assert.NotNull(repair.Projection.Strategic.Travel);
        Assert.Equal(12000, arrival.StoppedAt.Milliseconds);
        Assert.Equal([ScheduledWorkKind.TravelArrival], arrival.ResolvedKinds);
        Assert.Equal(12000, ordinaryAdvance.FinalTime.Milliseconds);
        Assert.Equal(
            [ScheduledWorkKind.SensorRepairCompletion, ScheduledWorkKind.TravelArrival],
            ordinaryAdvance.ResolvedKinds
        );
        Assert.Equal(ordinary.GetPlayerProjection(), ordinaryAdvance.Projection);
        Assert.Equal(until.GetPlayerProjection(), ordinary.GetPlayerProjection());
    }

    /// <summary>Confirms advance-until is a no-op once no scheduled boundary remains.</summary>
    [Fact]
    public void AdvanceUntilWithoutScheduledWorkDoesNotAdvance()
    {
        GameSimulation game = CreateGame();
        game.RequestTravel(new TravelIntent(ConnectedDestination(game)));
        game.AdvanceFixedSteps(140);
        PlayerProjection before = game.GetPlayerProjection();

        AdvanceUntilResult result = game.AdvanceUntilNextScheduledEvent();

        Assert.Equal(AdvanceUntilOutcome.NoScheduledEvent, result.Outcome);
        Assert.Equal(before.SimulationTime, result.StoppedAt);
        Assert.Empty(result.ResolvedKinds);
        Assert.Equal(before, result.Projection);
        Assert.Equal(before, game.GetPlayerProjection());
    }

    /// <summary>Confirms strategic departure freezes and clears prior local tactical motion.</summary>
    [Fact]
    public void TravelClearsLocalTacticalMotionWhilePositionRemainsStable()
    {
        GameSimulation game = CreateGame();
        game.SetTacticalCourse(new SetTacticalCourseIntent(new HeadingDegrees(45), new SpeedKilometersPerSecond(3.5)));
        TacticalPositionProjection before = game.GetPlayerProjection().Ship.Tactical.Position;

        game.RequestTravel(new TravelIntent(ConnectedDestination(game)));
        game.AdvanceFixedSteps(1);
        TacticalProjection traveling = game.GetPlayerProjection().Ship.Tactical;

        Assert.Equal(before, traveling.Position);
        Assert.Equal(0, traveling.SpeedKilometersPerSecond);
    }

    /// <summary>Confirms input at an arrival boundary observes the resolved local state.</summary>
    [Fact]
    public void InputsAtIdenticalBoundariesApplyAfterDueWorkAndThenMove()
    {
        GameSimulation game = CreateGame();
        LocationId destination = ConnectedDestination(game);
        game.RequestTravel(new TravelIntent(destination));
        game.AdvanceFixedSteps(120);

        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(
                new SetTacticalCourseIntent(new HeadingDegrees(90), new SpeedKilometersPerSecond(2))
            ).Outcome
        );
        game.AdvanceFixedSteps(1);

        Assert.Equal(0.45, game.GetPlayerProjection().Ship.Tactical.Position.XKilometers, 10);
    }

    /// <summary>Confirms invalid or overflowing step counts leave aggregate state unchanged.</summary>
    [Fact]
    public void AdvanceRejectsInvalidCountsWithoutMutation()
    {
        GameSimulation game = CreateGame();
        PlayerProjection initial = game.GetPlayerProjection();

        game.AdvanceFixedSteps(0);
        Assert.Equal(initial, game.GetPlayerProjection());
        Assert.Throws<ArgumentOutOfRangeException>(() => game.AdvanceFixedSteps(-1));
        Assert.Throws<OverflowException>(() => game.AdvanceFixedSteps(int.MaxValue));
        Assert.Equal(initial, game.GetPlayerProjection());
    }

    /// <summary>Confirms oversized catch-up work is rejected before any ship or clock mutation.</summary>
    [Fact]
    public void AdvanceRejectsShipStepWorkOverBudgetWithoutMutation()
    {
        GameSimulation game = CreateGame();
        var metadata = new GameSaveMetadata(
            "work-budget",
            "Work Budget",
            DateTimeOffset.UnixEpoch,
            DateTimeOffset.UnixEpoch
        );
        byte[] initial = GamePersistence.Serialize(game, metadata);
        game.SetTacticalCourse(new SetTacticalCourseIntent(new HeadingDegrees(90), new SpeedKilometersPerSecond(1)));
        byte[] moving = GamePersistence.Serialize(game, metadata);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            game.AdvanceFixedSteps(1_000_001)
        );

        Assert.Contains("actual ship-step work budget", exception.Message, StringComparison.Ordinal);
        Assert.NotEqual(initial, moving);
        Assert.Equal(moving, GamePersistence.Serialize(game, metadata));
    }

    /// <summary>Confirms bootstrap cannot admit scheduled work that persistence would reject for time exhaustion.</summary>
    [Fact]
    public void BootstrapRejectsScheduledDueTimeWithoutContinuationHeadroom()
    {
        var origin = new StrategicLocation(new LocationId("origin"), "Origin", default);
        var destination = new StrategicLocation(new LocationId("destination"), "Destination", default);
        var map = new StrategicMap(
            [origin, destination],
            [new StrategicRoute(origin.Id, destination.Id, new SimulationDuration(9223372036854775800))]
        );
        var start = new ShipStart(
            new ShipInstanceId(1),
            new ShipDefinitionId("pathfinder"),
            "USS Boundary",
            default,
            default,
            new SensorIntegrity(1),
            new TravelingStart(origin.Id, destination.Id, new SimulationTime(0))
        );

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            new GameBootstrap(new SimulationTime(0), map, start.InstanceId, [start]).CreateSimulation(CreateCatalog())
        );

        Assert.Contains("continuation headroom", exception.Message, StringComparison.Ordinal);
    }

    private static GameSimulation CreateGame()
    {
        return FirstGameSetup.Create(CreateCatalog());
    }

    private static ShipDefinitionCatalog CreateCatalog()
    {
        const string definition = """
            {
              "schemaVersion": 2,
              "id": "pathfinder",
              "designDisplayName": "Pathfinder class",
              "maximumTacticalSpeedKilometersPerSecond": 10,
              "sensorRepairDurationMilliseconds": 8000
            }
            """;
        string schema = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "src/AlterCourse.Godot/content/schemas/ship-definition-v2.schema.json")
        );
        ShipDefinitionCatalog catalog = new ShipDefinitionCatalogLoader(schema).LoadCatalog([
            ShipDefinitionContent.FromText("pathfinder.json", definition),
        ]);
        return catalog;
    }

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

    private static LocationId ConnectedDestination(GameSimulation game)
    {
        PlayerProjection projection = game.GetPlayerProjection();
        LocationId origin = projection.Strategic.CurrentLocation!.Id;
        return projection.Strategic.Routes.Single(route => route.Origin == origin).Destination;
    }
}
