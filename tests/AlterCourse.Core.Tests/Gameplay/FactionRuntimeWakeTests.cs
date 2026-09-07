using AlterCourse.Core.AI;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;
using FactionTestWorld = AlterCourse.Core.Tests.Gameplay.FactionBootstrapTests.FactionTestWorld;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies deterministic faction wake scheduling, assignment, satisfaction, and dormancy.</summary>
public sealed class FactionRuntimeWakeTests
{
    /// <summary>Confirms initial evaluation chooses deterministically and preserves same-time causal order.</summary>
    [Fact]
    public void InitialWakeSelectsLowestShipAndSchedulesArrivalBeforeFactionReevaluation()
    {
        GameSimulation game = CreateFactionGame();

        SimulationAdvanceTraceResult result = GameSimulation.AdvanceTo(
            game.CaptureState(),
            new SimulationTime(0),
            FactionTestWorld.ShipCatalog,
            FactionTestWorld.FactionCatalog
        );

        Assert.Equal(
            FactionAssignmentDecisionOutcome.AssignmentProposed,
            Assert.Single(result.Traces).FactionDecision!.Outcome
        );
        FactionState faction = result.State.Factions[0];
        Assert.Equal(new ShipInstanceId(2), faction.PresenceObjective!.AssignedShipId);
        ScheduledWork[] correlated = result
            .State.Scheduler.OutstandingWork.Where(work =>
                work.Target.ShipId == new ShipInstanceId(2) || work.Target.FactionId == faction.Id
            )
            .ToArray();
        Assert.Equal(2, correlated.Length);
        Assert.Equal(ScheduledWorkKind.TravelArrival, correlated[0].Kind);
        Assert.Equal(ScheduledWorkKind.FactionDecisionWake, correlated[1].Kind);
        Assert.Equal(correlated[0].DueTime, correlated[1].DueTime);
        Assert.True(correlated[0].Sequence < correlated[1].Sequence);
    }

    /// <summary>Confirms arrival and observation precede permanent objective satisfaction without churn.</summary>
    [Fact]
    public void ArrivalProducesPresenceThenSatisfiedObjectiveWithoutAssignmentChurn()
    {
        GameSimulation game = CreateFactionGame();
        SimulationState assigned = GameSimulation
            .AdvanceTo(
                game.CaptureState(),
                new SimulationTime(0),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;

        SimulationAdvanceTraceResult arrived = GameSimulation.AdvanceTo(
            assigned,
            new SimulationTime(1000),
            FactionTestWorld.ShipCatalog,
            FactionTestWorld.FactionCatalog
        );

        FactionState faction = arrived.State.Factions[0];
        Assert.Equal(FactionObjectiveStatus.Satisfied, faction.PresenceObjective!.Status);
        Assert.Null(faction.PresenceObjective.AssignedShipId);
        Assert.Null(faction.PresenceObjective.AssignedOrderId);
        Assert.Null(faction.PendingDecisionWake);
        Assert.Null(arrived.State.GetRequiredShip(new ShipInstanceId(2)).ActiveOrder);
        Assert.DoesNotContain(
            arrived.State.Scheduler.OutstandingWork,
            work => work.Target.Kind == ScheduledWorkTargetKind.Faction
        );
        Assert.Contains(
            arrived.State.GetRequiredShip(new ShipInstanceId(2)).SensorKnowledge.Contacts,
            contact => contact.TargetShipId == new ShipInstanceId(4)
        );
    }

    /// <summary>Confirms a future hold release creates exactly one meaningful reevaluation boundary.</summary>
    [Fact]
    public void HoldCompletionSchedulesOneFutureRetryThatCanAssign()
    {
        GameSimulation game = CreateFactionGame(
            new HoldUntilOrderStart(new SimulationTime(500)),
            includeAlternate: false
        );

        SimulationState waiting = GameSimulation
            .AdvanceTo(
                game.CaptureState(),
                new SimulationTime(0),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        ScheduledWork retry = Assert.Single(
            waiting.Scheduler.OutstandingWork,
            work => work.Target.Kind == ScheduledWorkTargetKind.Faction
        );
        Assert.Equal(new SimulationTime(500), retry.DueTime);

        SimulationState assigned = GameSimulation
            .AdvanceTo(waiting, new SimulationTime(500), FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .State;
        Assert.Equal(FactionObjectiveStatus.Assigned, assigned.Factions[0].PresenceObjective!.Status);
    }

    /// <summary>Confirms perpetual patrol arrivals do not cause unchanged faction retry loops.</summary>
    [Fact]
    public void PerpetualPatrolDoesNotScheduleUnchangedFactionRetries()
    {
        var patrol = new PatrolRouteOrderStart(
            [FactionTestWorld.Alpha, FactionTestWorld.Beta, FactionTestWorld.Gamma],
            1
        );
        ShipStart patrolShip = FactionTestWorld.CreateShip(
            2,
            FactionTestWorld.Alpha,
            FactionTestWorld.FactionA,
            patrol,
            new TravelingStart(FactionTestWorld.Alpha, FactionTestWorld.Beta, new SimulationTime(0))
        );
        ShipStart player = FactionTestWorld.CreateShip(1, FactionTestWorld.Alpha, null);
        var faction = new FactionStart(FactionTestWorld.FactionA, FactionTestWorld.DefinitionA, FactionTestWorld.Gamma);
        GameSimulation game = new GameBootstrap(
            new SimulationTime(0),
            FactionTestWorld.CreateMap(cyclic: true),
            player.InstanceId,
            [player, patrolShip],
            [faction]
        ).CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);

        SimulationState state = GameSimulation
            .AdvanceTo(
                game.CaptureState(),
                new SimulationTime(0),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;

        Assert.Null(state.Factions[0].PendingDecisionWake);
        Assert.DoesNotContain(
            state.Scheduler.OutstandingWork,
            work => work.Target.Kind == ScheduledWorkTargetKind.Faction
        );
        Assert.Single(state.Scheduler.OutstandingWork, work => work.Kind == ScheduledWorkKind.TravelArrival);
    }

    /// <summary>Confirms target domains prevent equal numeric faction and ship identities from leaking relevance.</summary>
    [Fact]
    public void SameNumericFactionAndPlayerIdentitiesDoNotCreatePlayerRelevance()
    {
        GameSimulation game = CreateFactionGame();

        AdvanceUntilResult result = game.AdvanceUntilNextPlayerRelevantEvent();

        Assert.Equal(AdvanceUntilOutcome.NoPlayerEvent, result.Outcome);
        Assert.Equal(new SimulationTime(0), result.StoppedAt);
        Assert.Empty(result.ResolvedEvents);
    }

    private static GameSimulation CreateFactionGame(
        ShipOrderStart? firstNpcOrder = null,
        bool includeAlternate = true
    ) =>
        FactionTestWorld
            .CreateBootstrap(
                [
                    new FactionStart(FactionTestWorld.FactionA, FactionTestWorld.DefinitionA, FactionTestWorld.Beta),
                    new FactionStart(FactionTestWorld.FactionB, FactionTestWorld.DefinitionB),
                ],
                controlledShips: true,
                alternateControlled: includeAlternate,
                firstNpcOrder: firstNpcOrder
            )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
}
