using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Simulation;
using FactionTestWorld = AlterCourse.Core.Tests.Gameplay.FactionBootstrapTests.FactionTestWorld;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Proves bounded faction scheduling and persistence across multi-day deterministic continuation.</summary>
public sealed class FactionLongHorizonTests
{
    private const int HorizonDays = 30;
    private const long DayMilliseconds = 86_400_000;
    private readonly FactionAssignmentProofFixture _fixture = new();

    /// <summary>Confirms a satisfied objective remains dormant with stable scheduler counters for thirty days.</summary>
    [Fact]
    public void SatisfiedObjectiveHasNoReassignmentOrWorkGrowthForThirtyDays()
    {
        GameSimulation completed = _fixture.ContinueTo(
            _fixture.Assign(_fixture.Create()),
            FactionAssignmentProofFixture.ArrivalTime
        );
        SimulationState starting = completed.CaptureState();
        long workCounter = starting.Scheduler.NextWorkId;
        long sequenceCounter = starting.Scheduler.NextSequence;

        GameSimulation continued = ContinueByDays(completed, HorizonDays);
        SimulationState final = continued.CaptureState();

        Assert.Equal(
            FactionObjectiveStatus.Satisfied,
            final.GetRequiredFaction(FactionAssignmentProofFixture.FactionA).PresenceObjective!.Status
        );
        Assert.Null(final.GetRequiredFaction(FactionAssignmentProofFixture.FactionA).PendingDecisionWake);
        Assert.Equal(workCounter, final.Scheduler.NextWorkId);
        Assert.Equal(sequenceCounter, final.Scheduler.NextSequence);
        Assert.Empty(final.Scheduler.OutstandingWork);
        Assert.All(
            final.Ships.Where(ship => ship.DirectControllerFactionId is not null),
            ship => Assert.Contains(final.Factions, faction => faction.Id == ship.DirectControllerFactionId)
        );
        AssertRoundTripContinuation(continued);
    }

    /// <summary>Confirms perpetual patrol can cross two days without faction polling or scheduler growth beyond its own arrival.</summary>
    [Fact]
    public void PerpetuallyCommittedCandidateKeepsFactionDormantForTwoDays()
    {
        GameSimulation game = CreatePatrolWorld();
        SimulationState initial = GameSimulation
            .AdvanceTo(
                game.CaptureState(),
                new SimulationTime(0),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        Assert.Null(initial.Factions[0].PendingDecisionWake);
        Assert.Equal(2, initial.Scheduler.OutstandingWork.Count(work => work.Kind == ScheduledWorkKind.TravelArrival));

        const int patrolDays = 2;
        const long arrivals = patrolDays * DayMilliseconds / 1_000;
        SimulationState advanced = AdvanceInHourChunks(initial, patrolDays);

        Assert.Equal(FactionObjectiveStatus.Pending, advanced.Factions[0].PresenceObjective!.Status);
        Assert.Null(advanced.Factions[0].PendingDecisionWake);
        Assert.DoesNotContain(
            advanced.Scheduler.OutstandingWork,
            work => work.Target.Kind == ScheduledWorkTargetKind.Faction
        );
        Assert.Equal(2, advanced.Scheduler.OutstandingWork.Count(work => work.Kind == ScheduledWorkKind.TravelArrival));
        Assert.Equal(initial.Scheduler.NextWorkId + (2 * arrivals), advanced.Scheduler.NextWorkId);
        Assert.Equal(initial.Scheduler.NextSequence + (2 * arrivals), advanced.Scheduler.NextSequence);
    }

    /// <summary>Confirms a finite commitment creates one future faction wake and assigns at that release boundary.</summary>
    [Fact]
    public void FiniteHoldCreatesOneFutureWakeAndThenAssigns()
    {
        var release = new SimulationTime(DayMilliseconds);
        SimulationState initial = FactionTestWorld
            .CreateBootstrap(
                [new FactionStart(FactionTestWorld.FactionA, FactionTestWorld.DefinitionA, FactionTestWorld.Beta)],
                controlledShips: true,
                alternateControlled: false,
                firstNpcOrder: new HoldUntilOrderStart(release)
            )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .CaptureState();
        SimulationState waiting = GameSimulation
            .AdvanceTo(initial, new SimulationTime(0), FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .State;

        ScheduledWork wake = Assert.Single(
            waiting.Scheduler.OutstandingWork,
            work => work.Target.Kind == ScheduledWorkTargetKind.Faction
        );
        Assert.Equal(release, wake.DueTime);
        SimulationState assigned = GameSimulation
            .AdvanceTo(waiting, release, FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .State;
        Assert.Equal(FactionObjectiveStatus.Assigned, assigned.Factions[0].PresenceObjective!.Status);
        Assert.Equal(new ShipInstanceId(2), assigned.Factions[0].PresenceObjective!.AssignedShipId);
        Assert.Single(assigned.Scheduler.OutstandingWork, work => work.Target.Kind == ScheduledWorkTargetKind.Faction);
    }

    private GameSimulation ContinueByDays(GameSimulation game, int days)
    {
        GameSimulation current = game;
        for (int day = 0; day < days; day++)
        {
            SimulationTime target = current.CaptureState().Time.AdvanceBy(new SimulationDuration(DayMilliseconds));
            current = _fixture.ContinueTo(current, target);
        }
        return current;
    }

    private void AssertRoundTripContinuation(GameSimulation game)
    {
        GameSimulation loaded = _fixture.RoundTrip(game, "faction-thirty-day-v7.json");
        Assert.Equal(
            GamePersistence.Serialize(game, FactionAssignmentProofFixture.Metadata),
            GamePersistence.Serialize(loaded, FactionAssignmentProofFixture.Metadata)
        );
        GameSimulation expected = ContinueByDays(game, 1);
        GameSimulation actual = ContinueByDays(loaded, 1);
        Assert.Equal(
            GamePersistence.Serialize(expected, FactionAssignmentProofFixture.Metadata),
            GamePersistence.Serialize(actual, FactionAssignmentProofFixture.Metadata)
        );
    }

    private static GameSimulation CreatePatrolWorld()
    {
        var patrol = new PatrolRouteOrderStart(
            [FactionTestWorld.Alpha, FactionTestWorld.Beta, FactionTestWorld.Gamma],
            1
        );
        ShipStart player = FactionTestWorld.CreateShip(1, FactionTestWorld.Alpha, null);
        ShipStart npc = FactionTestWorld.CreateShip(
            2,
            FactionTestWorld.Alpha,
            FactionTestWorld.FactionA,
            patrol,
            new TravelingStart(FactionTestWorld.Alpha, FactionTestWorld.Beta, new SimulationTime(0))
        );
        ShipStart alternate = FactionTestWorld.CreateShip(
            3,
            FactionTestWorld.Alpha,
            FactionTestWorld.FactionA,
            patrol,
            new TravelingStart(FactionTestWorld.Alpha, FactionTestWorld.Beta, new SimulationTime(0))
        );
        return new GameBootstrap(
            new SimulationTime(0),
            FactionTestWorld.CreateMap(cyclic: true),
            player.InstanceId,
            [player, npc, alternate],
            [new FactionStart(FactionTestWorld.FactionA, FactionTestWorld.DefinitionA, FactionTestWorld.Gamma)]
        ).CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
    }

    private static SimulationState AdvanceInHourChunks(SimulationState initial, int days)
    {
        SimulationState current = initial;
        const long hour = 3_600_000;
        for (int index = 0; index < days * 24; index++)
        {
            SimulationAdvanceTraceResult chunk = GameSimulation.AdvanceTo(
                current,
                current.Time.AdvanceBy(new SimulationDuration(hour)),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            );
            current = chunk.State;
        }
        return current;
    }
}
