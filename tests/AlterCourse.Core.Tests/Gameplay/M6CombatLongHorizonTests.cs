using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Exercises sustained real defense with bounded work and V9 continuation.</summary>
public sealed class M6CombatLongHorizonTests
{
    private const int ShotCount = 512;
    private readonly M6CombatProofFixture _fixture = new(lowDamage: true);

    /// <summary>Proves recurring defense stays active without cooldown work, damage resets or save divergence.</summary>
    [Fact]
    public void RepeatedLegitimateDefenseRemainsBoundedAndConvergesAcrossMidStimulusSave()
    {
        GameSimulation game = _fixture.Pair();
        GameSimulation? resumed = null;
        SensorContactId contact = M6CombatProofFixture.Contact(game, M6CombatProofFixture.Defender);
        long startedAt = game.CaptureState().Time.Milliseconds;
        long startingWorkId = game.CaptureState().Scheduler.NextWorkId;
        long startingSequence = game.CaptureState().Scheduler.NextSequence;
        int decisions = 0;
        int maximumOutstanding = 0;
        Assert.Empty(game.CaptureState().Scheduler.OutstandingWork);
        for (int shot = 0; shot < ShotCount; shot++)
        {
            if (shot > 0)
            {
                SimulationTime ready = M6CombatProofFixture.Player(game).Combat.NextDirectedEnergyReadyAt;
                (game, resumed) = AdvanceTogether(game, resumed, ready, responding: false);
            }
            FireDirectedEnergyResult fired = game.FireDirectedEnergy(
                new(contact, ShipSystemKind.DirectedEnergyWeapons)
            );
            Assert.Equal(FireDirectedEnergyOutcome.Accepted, fired.Outcome);
            if (resumed is not null)
                Assert.Equal(fired, resumed.FireDirectedEnergy(new(contact, ShipSystemKind.DirectedEnergyWeapons)));
            SimulationState pending = game.CaptureState();
            CombatStimulus stimulus = pending.GetRequiredShip(M6CombatProofFixture.Defender).Combat.PendingStimulus!;
            AssertPendingCorrelation(pending, stimulus);
            maximumOutstanding = Math.Max(maximumOutstanding, pending.Scheduler.OutstandingWork.Length);
            if (shot == ShotCount / 2 - 1)
                resumed = _fixture.RoundTrip(game);
            if (resumed is not null)
                _fixture.AssertEquivalent(game, resumed);
            (game, resumed) = AdvanceTogether(game, resumed, stimulus.DueTime, responding: true);
            decisions++;
        }
        Assert.Equal(ShotCount, decisions);
        Assert.Equal(1, maximumOutstanding);
        Assert.Equal(startingWorkId + ShotCount, game.CaptureState().Scheduler.NextWorkId);
        Assert.Equal(startingSequence + ShotCount, game.CaptureState().Scheduler.NextSequence);
        Assert.InRange(M6CombatProofFixture.Player(game).Engineering.DirectedEnergyCondition.Value, 0.9, 0.999);
        Assert.True(
            game.CaptureState().GetRequiredShip(M6CombatProofFixture.Defender).Engineering.ShieldCondition.Value < 1
        );
        long elapsed = game.CaptureState().Time.Milliseconds - startedAt;
        Assert.True(elapsed > 1_000_000);
        _fixture.AssertEquivalent(game, Assert.IsType<GameSimulation>(resumed));
        AssertDormantContinuation(game, resumed!);
        Console.WriteLine(
            $"shots={ShotCount}; decisions={decisions}; elapsedMs={elapsed}; maxOutstanding={maximumOutstanding}; workAllocated={game.CaptureState().Scheduler.NextWorkId - startingWorkId}"
        );
    }

    private void AssertDormantContinuation(GameSimulation game, GameSimulation resumed)
    {
        SimulationTime dormantUntil = game.CaptureState().Time.AdvanceBy(new SimulationDuration(60_000));
        SimulationAdvanceTraceResult dormant = _fixture.AdvanceTrace(game, dormantUntil);
        Assert.DoesNotContain(dormant.Traces, trace => trace.CombatDecision is not null);
        Assert.Empty(dormant.State.Scheduler.OutstandingWork);
        _fixture.AssertEquivalent(_fixture.Restore(dormant.State), _fixture.AdvanceTo(resumed, dormantUntil));
    }

    private void AssertPendingCorrelation(SimulationState pending, CombatStimulus stimulus)
    {
        ScheduledWork work = Assert.Single(pending.Scheduler.OutstandingWork);
        Assert.Equal(ScheduledWorkKind.ShipCombatDecisionWake, work.Kind);
        Assert.Equal(stimulus.ScheduledWorkId, work.Id);
        Assert.Equal(stimulus.DueTime, work.DueTime);
        Assert.Equal(M6CombatProofFixture.Defender, work.TargetShipId);
        Assert.Equal(pending.Time, stimulus.ObservedAt);
        Assert.Equal(pending.Time.AdvanceBy(SimulationFixedStep.Duration), stimulus.DueTime);
        Assert.Equal(
            pending
                .GetRequiredShip(M6CombatProofFixture.Defender)
                .SensorKnowledge.Contacts.Single(item => item.TargetShipId == pending.PlayerShipId)
                .Id,
            stimulus.ContactId
        );
        Assert.All(
            pending.Ships.Where(ship => ship.InstanceId != M6CombatProofFixture.Defender),
            ship => Assert.Null(ship.Combat.PendingStimulus)
        );
        pending.Validate(_fixture.Catalog);
    }

    private (GameSimulation Game, GameSimulation? Resumed) AdvanceTogether(
        GameSimulation game,
        GameSimulation? resumed,
        SimulationTime time,
        bool responding
    )
    {
        SimulationAdvanceTraceResult advanced = _fixture.AdvanceTrace(game, time);
        if (responding)
        {
            ScheduledConsequenceTrace trace = Assert.Single(advanced.Traces, item => item.CombatDecision is not null);
            Assert.Equal(DefensiveCombatDecisionAction.ReturnFire, trace.CombatDecision!.SelectedAction);
            Assert.Equal(FireDirectedEnergyOutcome.Accepted, trace.CombatDecision.ApplicationOutcome);
        }
        else
            Assert.DoesNotContain(advanced.Traces, trace => trace.CombatDecision is not null);
        GameSimulation next = _fixture.Restore(advanced.State);
        Assert.Empty(next.CaptureState().Scheduler.OutstandingWork);
        Assert.All(next.CaptureState().Ships, ship => Assert.Null(ship.Combat.PendingStimulus));
        GameSimulation? loadedNext = null;
        if (resumed is not null)
        {
            SimulationAdvanceTraceResult loaded = _fixture.AdvanceTrace(resumed, time);
            Assert.Equal(advanced.Traces.ToArray(), loaded.Traces.ToArray());
            Assert.Equal(advanced.PlayerEvents.ToArray(), loaded.PlayerEvents.ToArray());
            loadedNext = _fixture.Restore(loaded.State);
            _fixture.AssertEquivalent(next, loadedNext);
        }
        return (next, loadedNext);
    }
}
