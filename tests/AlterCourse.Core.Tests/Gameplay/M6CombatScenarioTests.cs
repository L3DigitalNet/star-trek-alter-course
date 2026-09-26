using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Proves the first engagement through actual commands and atomic Core transitions.</summary>
public sealed class M6CombatScenarioTests
{
    private readonly M6CombatProofFixture _fixture = new();

    /// <summary>Proves the four-ship first-game contact chain and its V9 combat checkpoint.</summary>
    [Fact]
    public void FourShipFirstGameAcquiresIdentifiesManeuversPenetratesAndRoundTrips()
    {
        GameSimulation game = _fixture.FourShipFirstGame();
        SensorContactId contact = _fixture.EnterCombatRange(game);
        ShipInstanceId kestrel = Milestone3ProofFixture.Kestrel(game).InstanceId;
        Assert.Equal(
            SensorContactStatus.Current,
            M6CombatProofFixture.Player(game).SensorKnowledge.Contacts.Single(item => item.Id == contact).Status
        );
        FireDirectedEnergyResult first = game.FireDirectedEnergy(new(contact, ShipSystemId.ImpulsePropulsion));
        Assert.Equal(FireDirectedEnergyOutcome.Accepted, first.Outcome);
        Assert.Contains(first.ResolvedEvents, item => item.Kind == PlayerAdvanceEventKind.ShieldImpact);
        Assert.DoesNotContain(first.ResolvedEvents, item => item.Kind == PlayerAdvanceEventKind.SubsystemPenetration);
        bool penetrated = false;
        for (int shot = 0; shot < 16 && !penetrated; shot++)
        {
            game = _fixture.AdvanceTo(game, M6CombatProofFixture.Player(game).Combat.NextDirectedEnergyReadyAt);
            FireDirectedEnergyResult result = game.FireDirectedEnergy(new(contact, ShipSystemId.ImpulsePropulsion));
            Assert.Equal(FireDirectedEnergyOutcome.Accepted, result.Outcome);
            penetrated = result.ResolvedEvents.Any(item => item.Kind == PlayerAdvanceEventKind.SubsystemPenetration);
        }
        Assert.True(penetrated, "The bounded production engagement must reach concrete penetration.");
        ShipState victim = game.CaptureState().GetRequiredShip(kestrel);
        Assert.True(victim.Engineering.ImpulseCondition.Value < 1);
        Assert.True(
            victim.Engineering.ImpulseCapability(_fixture.Catalog.GetRequired(victim.DefinitionId).Engineering) < 0.1
        );
        game.CaptureState().Validate(_fixture.Catalog);
        _fixture.AssertEquivalent(game, _fixture.RoundTrip(game));
    }

    /// <summary>Proves populated V9 combat continues with the current six-ship faction composition.</summary>
    [Fact]
    public void ProductionCombatCheckpointPreservesExactFutureBoundariesAndFactionWork()
    {
        (GameSimulation game, SensorContactId contact, ShipInstanceId kestrel) = ProductionCheckpoint();
        SimulationState checkpoint = game.CaptureState();
        Assert.Equal(6, checkpoint.Ships.Length);
        Assert.Equal(2, checkpoint.Factions.Length);
        Assert.True(checkpoint.ObservationReportIdAllocator.NextId > 1);
        Assert.True(checkpoint.Factions.Any(item => item.Observation!.ReceivedReports.Length > 0));
        ShipState victim = checkpoint.GetRequiredShip(kestrel);
        Assert.InRange(victim.Engineering.ShieldCondition.Value, double.Epsilon, 0.999);
        Assert.True(victim.Engineering.ImpulseCondition.Value < 1);
        Assert.Equal(
            M6CombatProofFixture.Allocation(20, 5, 20, 30),
            M6CombatProofFixture.Player(game).Engineering.Allocation
        );
        CombatStimulus stimulus = Assert.IsType<CombatStimulus>(victim.Combat.PendingStimulus);
        SimulationTime ready = M6CombatProofFixture.Player(game).Combat.NextDirectedEnergyReadyAt;
        Assert.True(ready.Milliseconds > checkpoint.Time.Milliseconds);
        GameSimulation resumed = _fixture.RoundTrip(game);
        _fixture.AssertEquivalent(game, resumed);
        FireDirectedEnergyIntent intent = new(contact, ShipSystemId.Sensors);
        Assert.Equal(FireDirectedEnergyOutcome.CooldownActive, game.FireDirectedEnergy(intent).Outcome);
        Assert.Equal(FireDirectedEnergyOutcome.CooldownActive, resumed.FireDirectedEnergy(intent).Outcome);
        Assert.Same(checkpoint, game.CaptureState());
        foreach (
            SimulationTime boundary in new[]
            {
                stimulus.DueTime,
                new SimulationTime(ready.Milliseconds - SimulationFixedStep.Duration.Milliseconds),
                ready,
            }
        )
        {
            SimulationAdvanceTraceResult first = _fixture.AdvanceTrace(game, boundary);
            SimulationAdvanceTraceResult second = _fixture.AdvanceTrace(resumed, boundary);
            Assert.Equal(first.PlayerEvents.ToArray(), second.PlayerEvents.ToArray());
            Assert.Equal(first.Traces.ToArray(), second.Traces.ToArray());
            game = _fixture.Restore(first.State);
            resumed = _fixture.Restore(second.State);
            _fixture.AssertEquivalent(game, resumed);
            if (boundary.Milliseconds < ready.Milliseconds)
            {
                Assert.Equal(FireDirectedEnergyOutcome.CooldownActive, game.FireDirectedEnergy(intent).Outcome);
                Assert.Equal(FireDirectedEnergyOutcome.CooldownActive, resumed.FireDirectedEnergy(intent).Outcome);
            }
        }
        FireDirectedEnergyResult fired = game.FireDirectedEnergy(intent);
        Assert.Equal(FireDirectedEnergyOutcome.Accepted, fired.Outcome);
        Assert.Equal(fired, resumed.FireDirectedEnergy(intent));
        _fixture.AssertEquivalent(game, resumed);
        SimulationTime future = game.CaptureState().Time.AdvanceBy(new SimulationDuration(60_000));
        SimulationAdvanceTraceResult uninterruptedFuture = _fixture.AdvanceTrace(game, future);
        SimulationAdvanceTraceResult resumedFuture = _fixture.AdvanceTrace(resumed, future);
        Assert.Equal(uninterruptedFuture.Traces.ToArray(), resumedFuture.Traces.ToArray());
        Assert.Equal(uninterruptedFuture.PlayerEvents.ToArray(), resumedFuture.PlayerEvents.ToArray());
        _fixture.AssertEquivalent(_fixture.Restore(uninterruptedFuture.State), _fixture.Restore(resumedFuture.State));
    }

    private (GameSimulation Game, SensorContactId Contact, ShipInstanceId Victim) ProductionCheckpoint()
    {
        GameSimulation game = _fixture.Production();
        SensorContactId contact = _fixture.EnterCombatRange(game);
        ShipInstanceId kestrel = Milestone3ProofFixture.Kestrel(game).InstanceId;
        bool penetrated = false;
        for (int shot = 0; shot < 16 && !penetrated; shot++)
        {
            if (shot > 0)
                game = _fixture.AdvanceTo(game, M6CombatProofFixture.Player(game).Combat.NextDirectedEnergyReadyAt);
            FireDirectedEnergyResult result = game.FireDirectedEnergy(new(contact, ShipSystemId.ImpulsePropulsion));
            Assert.Equal(FireDirectedEnergyOutcome.Accepted, result.Outcome);
            penetrated = result.ResolvedEvents.Any(item => item.Kind == PlayerAdvanceEventKind.SubsystemPenetration);
        }
        Assert.True(penetrated);
        return (game, contact, kestrel);
    }

    /// <summary>Distinguishes reserve consumption from deterministic four-consumer brownout.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GenerationDamageUsesReserveThenExactBrownout(bool brownout)
    {
        GameSimulation game = _fixture.Pair();
        PowerAllocation allocation = brownout
            ? M6CombatProofFixture.Allocation(70, 20, 0, 30)
            : M6CombatProofFixture.Allocation(40, 10, 0, 30);
        Assert.Equal(PowerAllocationOutcome.Accepted, game.SetPowerAllocation(allocation).Outcome);
        ShipDirectedEnergyApplicationResult hit = _fixture.Incoming(game, ShipSystemId.PowerGeneration);
        ShipState player = hit.CandidateState.GetRequiredShip(hit.CandidateState.PlayerShipId);
        Assert.Equal(0.75, player.Engineering.GenerationCondition.Value);
        Assert.Equal(
            brownout ? M6CombatProofFixture.Allocation(53, 15, 0, 22) : allocation,
            player.Engineering.Allocation
        );
        Assert.Equal(brownout, hit.ResolvedEvents.Any(item => item.Kind == PlayerAdvanceEventKind.PowerBrownout));
    }

    /// <summary>Proves involuntary propulsion damage retains the chosen direction.</summary>
    [Fact]
    public void ImpulseDamagePreservesHeadingAndForcesLegalDeceleration()
    {
        GameSimulation game = _fixture.Pair();
        var heading = new HeadingDegrees(37);
        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(new(heading, new SpeedKilometersPerSecond(4))).Outcome
        );
        ShipDirectedEnergyApplicationResult hit = _fixture.Incoming(game, ShipSystemId.ImpulsePropulsion);
        ShipState player = hit.CandidateState.GetRequiredShip(hit.CandidateState.PlayerShipId);
        Assert.Equal(heading, player.TacticalMotion.Heading);
        Assert.Equal(3, player.TacticalMotion.Speed.Value, 12);
        Assert.Contains(hit.ResolvedEvents, item => item.Kind == PlayerAdvanceEventKind.ForcedDeceleration);
    }

    /// <summary>Proves lost observation cancels scanning without a later identity upgrade.</summary>
    [Fact]
    public void SensorDamageInterruptsRealActiveScanAndCancelsExactWork()
    {
        GameSimulation game = _fixture.Pair();
        SensorContactId contact = M6CombatProofFixture.Contact(game, M6CombatProofFixture.ScanTarget);
        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(contact).Outcome);
        ActiveSensorScanState scan = M6CombatProofFixture.Player(game).SensorKnowledge.ActiveScan!;
        ShipDirectedEnergyApplicationResult hit = _fixture.Incoming(game, ShipSystemId.Sensors);
        Assert.Contains(hit.ResolvedEvents, item => item.Kind == PlayerAdvanceEventKind.ActiveSensorScanInterrupted);
        GameSimulation damaged = _fixture.Restore(hit.CandidateState);
        Assert.Null(M6CombatProofFixture.Player(damaged).SensorKnowledge.ActiveScan);
        Assert.DoesNotContain(
            hit.CandidateState.Scheduler.OutstandingWork,
            work => work.Id == scan.ScheduledCompletionId
        );
        damaged = _fixture.AdvanceTo(damaged, scan.ExpectedCompletion.AdvanceBy(SimulationFixedStep.Duration));
        Assert.Equal(
            SensorContactIdentification.Detected,
            M6CombatProofFixture
                .Player(damaged)
                .SensorKnowledge.Contacts.Single(item => item.Id == contact)
                .Identification
        );
    }

    /// <summary>Proves damage invalidates only the matching analytical repair.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DamageCancelsOnlyRepairOfTheDamagedSystem(bool sameSystem)
    {
        GameSimulation game = _fixture.Pair();
        ShipState player = M6CombatProofFixture.Player(game);
        // The pre-damaged condition is trusted setup; repair admission and incoming damage use real transitions.
        game = _fixture.Restore(
            game.CaptureState()
                .ReplaceShip(
                    player.InstanceId,
                    player with
                    {
                        Engineering = player.Engineering.WithCondition(ShipSystemId.Sensors, new SystemCondition(0.8)),
                    }
                )
        );
        Assert.Equal(
            SystemRepairOutcome.Accepted,
            game.BeginSystemRepair(ShipSystemId.Sensors, new SystemCondition(1)).Outcome
        );
        SystemRepairState repair = M6CombatProofFixture.Player(game).Engineering.ActiveRepair!;
        ShipDirectedEnergyApplicationResult hit = _fixture.Incoming(
            game,
            sameSystem ? ShipSystemId.Sensors : ShipSystemId.ImpulsePropulsion
        );
        Assert.Equal(
            !sameSystem,
            hit.CandidateState.Scheduler.OutstandingWork.Any(work => work.Id == repair.ScheduledCompletionId)
        );
        GameSimulation later = _fixture.AdvanceTo(
            _fixture.Restore(hit.CandidateState),
            repair.ExpectedCompletion.AdvanceBy(SimulationFixedStep.Duration)
        );
        Assert.Null(M6CombatProofFixture.Player(later).Engineering.ActiveRepair);
        Assert.Equal(sameSystem ? 0.55 : 1, M6CombatProofFixture.Player(later).Engineering.SensorCondition.Value, 12);
        later.CaptureState().Validate(_fixture.Catalog);
    }

    /// <summary>Proves legitimate attack knowledge drives one delayed explained response.</summary>
    [Fact]
    public void PublicAttackCreatesOneFutureActorLocalExplainedDefense()
    {
        GameSimulation game = _fixture.Pair();
        SensorContactId contact = M6CombatProofFixture.Contact(game, M6CombatProofFixture.Defender);
        ShipState before = game.CaptureState().GetRequiredShip(M6CombatProofFixture.Defender);
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            game.FireDirectedEnergy(new(contact, ShipSystemId.Shields)).Outcome
        );
        SimulationState state = game.CaptureState();
        CombatStimulus stimulus = state.GetRequiredShip(before.InstanceId).Combat.PendingStimulus!;
        Assert.Equal(
            before.Combat.NextDirectedEnergyReadyAt,
            state.GetRequiredShip(before.InstanceId).Combat.NextDirectedEnergyReadyAt
        );
        Assert.Equal(state.Time.AdvanceBy(SimulationFixedStep.Duration), stimulus.DueTime);
        Assert.Single(
            state.Scheduler.OutstandingWork.Where(work => work.Kind == ScheduledWorkKind.ShipCombatDecisionWake)
        );
        Assert.All(
            state.Ships.Where(ship => ship.InstanceId != before.InstanceId),
            ship => Assert.Null(ship.Combat.PendingStimulus)
        );
        Assert.Equal(
            before.SensorKnowledge.Contacts.Single(item => item.TargetShipId == state.PlayerShipId).Id,
            stimulus.ContactId
        );
        game.AdvanceFixedSteps(1);
        DefensiveCombatDecisionExplanation decision = game.LastDefensiveCombatDecisionExplanation!;
        Assert.Equal(DefensiveCombatDecisionAction.ReturnFire, decision.SelectedAction);
        Assert.Equal(DefensiveCombatDecisionTieRule.ReturnFireThenWithdrawThenHold, decision.TieRule);
        Assert.Equal(
            new[]
            {
                DefensiveCombatDecisionAction.ReturnFire,
                DefensiveCombatDecisionAction.Withdraw,
                DefensiveCombatDecisionAction.Hold,
            },
            decision.Candidates.Select(item => item.Action)
        );
        Assert.All(decision.Candidates[0].Constraints, constraint => Assert.True(constraint.Satisfied));
        Assert.Equal(FireDirectedEnergyOutcome.Accepted, decision.ApplicationOutcome);
        Assert.Null(game.CaptureState().GetRequiredShip(before.InstanceId).Combat.PendingStimulus);
        Assert.DoesNotContain(
            game.CaptureState().Scheduler.OutstandingWork,
            work => work.Kind == ScheduledWorkKind.ShipCombatDecisionWake
        );
    }

    /// <summary>Compares permitted decision facts while hidden target Engineering differs.</summary>
    [Fact]
    public void DefensiveDecisionIgnoresHiddenAttackerShieldCondition()
    {
        GameSimulation first = _fixture.Pair();
        ShipState player = M6CombatProofFixture.Player(first);
        GameSimulation second = _fixture.Restore(
            first
                .CaptureState()
                .ReplaceShip(
                    player.InstanceId,
                    player with
                    {
                        Engineering = player.Engineering with { ShieldCondition = new SystemCondition(0.2) },
                    }
                )
        );
        SensorContactId contact = M6CombatProofFixture.Contact(first, M6CombatProofFixture.Defender);
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            first.FireDirectedEnergy(new(contact, ShipSystemId.Shields)).Outcome
        );
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            second.FireDirectedEnergy(new(contact, ShipSystemId.Shields)).Outcome
        );
        first.AdvanceFixedSteps(1);
        second.AdvanceFixedSteps(1);
        Assert.Equal(first.LastDefensiveCombatDecisionExplanation, second.LastDefensiveCombatDecisionExplanation);
        Assert.NotEqual(
            M6CombatProofFixture.Player(first).Engineering.ShieldCondition,
            M6CombatProofFixture.Player(second).Engineering.ShieldCondition
        );
    }

    /// <summary>Proves a public attack selects ordinary withdrawal when return fire lacks power.</summary>
    [Fact]
    public void UnpoweredDefenderExplainsRejectionAndWithdrawsWithoutCombatLoop()
    {
        GameSimulation game = _fixture.Pair();
        ShipState defender = game.CaptureState().GetRequiredShip(M6CombatProofFixture.Defender);
        game = _fixture.Restore(
            game.CaptureState()
                .ReplaceShip(
                    defender.InstanceId,
                    defender with
                    {
                        Engineering = defender.Engineering with
                        {
                            Allocation = M6CombatProofFixture.Allocation(70, 5, 15, 0),
                        },
                    }
                )
        );
        SensorContactId contact = M6CombatProofFixture.Contact(game, defender.InstanceId);
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            game.FireDirectedEnergy(new(contact, ShipSystemId.Shields)).Outcome
        );
        double before = M6CombatProofFixture.Separation(game, defender.InstanceId);
        game.AdvanceFixedSteps(1);
        DefensiveCombatDecisionExplanation decision = game.LastDefensiveCombatDecisionExplanation!;
        Assert.Equal(DefensiveCombatDecisionAction.Withdraw, decision.SelectedAction);
        Assert.Equal(FireDirectedEnergyOutcome.WeaponUnpowered, decision.Candidates[0].FireRejection);
        Assert.Contains(
            decision.Candidates[0].Constraints,
            item => item.Constraint == DefensiveCombatConstraint.WeaponPowered && !item.Satisfied
        );
        Assert.All(decision.Candidates[1].Constraints, item => Assert.True(item.Satisfied));
        Assert.Equal(SetTacticalCourseOutcome.Accepted, decision.CourseOutcome);
        game.AdvanceFixedSteps(10);
        Assert.True(M6CombatProofFixture.Separation(game, defender.InstanceId) > before);
        Assert.DoesNotContain(
            game.CaptureState().Scheduler.OutstandingWork,
            work => work.Kind == ScheduledWorkKind.ShipCombatDecisionWake
        );
        game.CaptureState().Validate(_fixture.Catalog);
    }

    /// <summary>Proves ordinary motion ends local fire eligibility without encounter state.</summary>
    [Fact]
    public void OrdinaryWithdrawalGrowsSeparationUntilLocalFireRejects()
    {
        GameSimulation game = _fixture.Pair();
        SensorContactId contact = M6CombatProofFixture.Contact(game, M6CombatProofFixture.Defender);
        double initial = M6CombatProofFixture.Separation(game, M6CombatProofFixture.Defender);
        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(new(new HeadingDegrees(270), new SpeedKilometersPerSecond(4))).Outcome
        );
        game.AdvanceFixedSteps(30);
        Assert.True(M6CombatProofFixture.Separation(game, M6CombatProofFixture.Defender) > initial);
        FireDirectedEnergyResult rejected = game.FireDirectedEnergy(new(contact, ShipSystemId.Sensors));
        Assert.Equal(FireDirectedEnergyOutcome.OutOfRange, rejected.Outcome);
        Assert.Empty(rejected.ResolvedEvents);
        game.AdvanceFixedSteps(30);
        Assert.Equal(
            FireDirectedEnergyOutcome.ContactNotCurrent,
            game.FireDirectedEnergy(new(contact, ShipSystemId.Sensors)).Outcome
        );
        game.CaptureState().Validate(_fixture.Catalog);
    }
}
