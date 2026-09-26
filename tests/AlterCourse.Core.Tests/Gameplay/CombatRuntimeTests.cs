using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies atomic combat command boundaries.</summary>
public sealed class CombatRuntimeTests
{
    /// <summary>Rejects a four-consumer over-allocation without mutating the aggregate.</summary>
    [Fact]
    public void ExactAllocationCountsCombatConsumersBeforeCommit()
    {
        GameSimulation game = new Milestone3ProofFixture().CreateDefault();
        var allocation = new PowerAllocation(new PowerUnits(44), new PowerUnits(31), new PowerUnits(1));
        Assert.Equal(PowerAllocationOutcome.AvailablePowerExceeded, game.SetPowerAllocation(allocation).Outcome);
        Assert.Equal(new PowerUnits(44), game.GetPlayerProjection().Ship.Engineering.SensorAllocation);
    }

    /// <summary>Consumes powered shields before the selected subsystem and rejects cooldown without any state change.</summary>
    [Fact]
    public void ShotConsumesShieldsAndCommitsReadinessAtomically()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(shieldPower: 15);
        SimulationState before = game.CaptureState();
        ShipState victim = before.GetRequiredShip(new ShipInstanceId(2));
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            game.FireDirectedEnergy(new(contact, ShipSystemId.Sensors)).Outcome
        );
        ShipState after = game.CaptureState().GetRequiredShip(victim.InstanceId);
        Assert.Equal(0.75, after.Engineering.ShieldCondition.Value, 12);
        Assert.Equal(1, after.Engineering.SensorCondition.Value);
        Assert.Equal(
            before.Time.Milliseconds + 2000,
            game.CaptureState().GetRequiredShip(before.PlayerShipId).Combat.NextDirectedEnergyReadyAt.Milliseconds
        );
        SimulationState committed = game.CaptureState();
        FireDirectedEnergyResult rejected = game.FireDirectedEnergy(new(contact, ShipSystemId.Sensors));
        Assert.Equal(FireDirectedEnergyOutcome.CooldownActive, rejected.Outcome);
        Assert.Empty(rejected.ResolvedEvents);
        Assert.Same(committed, game.CaptureState());
        Assert.NotNull(after.Combat.PendingStimulus);
        Assert.Equal(before.Time.Milliseconds + 100, after.Combat.PendingStimulus.DueTime.Milliseconds);
        Assert.Equal(
            fixture.Catalog.GetRequired(after.DefinitionId).DirectedEnergyWeapon!.Range,
            game.GetPlayerProjection().Ship.Combat.WeaponRange
        );
    }

    /// <summary>Applies penetration to each semantic system and reconciles any generation brownout.</summary>
    [Theory]
    [InlineData("power-generation")]
    [InlineData("sensors")]
    [InlineData("impulse-propulsion")]
    [InlineData("shields")]
    [InlineData("directed-energy-weapons")]
    public void UnpoweredShieldsPermitConcreteSubsystemDamage(string systemName)
    {
        (GameSimulation game, _, SensorContactId contact) = Pair(shieldPower: 0);
        var system = ShipSystemId.Parse(systemName);
        Assert.Equal(FireDirectedEnergyOutcome.Accepted, game.FireDirectedEnergy(new(contact, system)).Outcome);
        ShipState victim = game.CaptureState().GetRequiredShip(new ShipInstanceId(2));
        Assert.Equal(0.75, victim.Engineering.ConditionFor(system).Value, 12);
        Assert.True(
            victim.Engineering.Allocation.Total
                <= victim
                    .Engineering.AvailablePower(
                        new Milestone3ProofFixture().Catalog.GetRequired(victim.DefinitionId).Engineering
                    )
                    .Value
        );
    }

    /// <summary>Returns fire at a future boundary and delivers actual player damage through an NPC-owned wake.</summary>
    [Fact]
    public void ReactiveWakeIsFutureAndPlayerAdvanceFindsIncomingDamage()
    {
        (GameSimulation game, _, SensorContactId contact) = Pair(shieldPower: 0);
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            game.FireDirectedEnergy(new(contact, ShipSystemId.Shields)).Outcome
        );
        long firedAt = game.CaptureState().Time.Milliseconds;
        Assert.Equal(
            1,
            game.CaptureState()
                .Scheduler.OutstandingWork.Count(work => work.Kind == ScheduledWorkKind.ShipCombatDecisionWake)
        );
        AdvanceUntilResult response = game.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Equal(firedAt + 100, response.StoppedAt.Milliseconds);
        Assert.Contains(
            response.ResolvedEvents,
            item =>
                item.Kind == PlayerAdvanceEventKind.OwnSystemDamaged
                && item.ShipSystemId == ShipSystemId.DirectedEnergyWeapons
        );
        Assert.Equal(
            DefensiveCombatDecisionAction.ReturnFire,
            game.LastDefensiveCombatDecisionExplanation!.SelectedAction
        );
        Assert.Null(game.CaptureState().GetRequiredShip(new ShipInstanceId(2)).Combat.PendingStimulus);
        Assert.Empty(
            game.CaptureState()
                .Scheduler.OutstandingWork.Where(work => work.Kind == ScheduledWorkKind.ShipCombatDecisionWake)
        );
    }

    /// <summary>Retains the first pending stimulus rather than postponing it after a second accepted shot.</summary>
    [Fact]
    public void FirstStimulusWinsWithoutGrowingWork()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(shieldPower: 0);
        game.FireDirectedEnergy(new(contact, ShipSystemId.Shields));
        SimulationState state = game.CaptureState();
        CombatStimulus first = state.GetRequiredShip(new ShipInstanceId(2)).Combat.PendingStimulus!;
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        game = GameSimulation.RestoreState(
            state.ReplaceShip(
                player.InstanceId,
                player with
                {
                    Combat = player.Combat with { NextDirectedEnergyReadyAt = default },
                }
            ),
            fixture.Catalog
        );
        game.FireDirectedEnergy(new(contact, ShipSystemId.Shields));
        Assert.Equal(first, game.CaptureState().GetRequiredShip(new ShipInstanceId(2)).Combat.PendingStimulus);
        Assert.Single(
            game.CaptureState()
                .Scheduler.OutstandingWork.Where(work => work.Kind == ScheduledWorkKind.ShipCombatDecisionWake)
        );
    }

    /// <summary>Suppresses only the exact repair canceled by earlier same-boundary defensive damage.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SameBatchRepairCompletionRespectsStableOrdering(bool damageFirst)
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(shieldPower: 0);
        SimulationState state = SameBoundaryRepairState(game.CaptureState(), damageFirst);
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        game = GameSimulation.RestoreState(state, fixture.Catalog);
        SimulationAdvanceResult result = game.AdvanceFixedSteps(1);
        Assert.Equal(
            0.75,
            game.CaptureState().GetRequiredShip(player.InstanceId).Engineering.DirectedEnergyCondition.Value,
            12
        );
        Assert.Null(game.CaptureState().GetRequiredShip(player.InstanceId).Engineering.ActiveRepair);
        Assert.Equal(
            damageFirst,
            result.ResolvedEvents.Any(item => item.Kind == PlayerAdvanceEventKind.SystemRepairInterrupted)
        );
        Assert.Equal(
            !damageFirst,
            result.ResolvedEvents.Any(item => item.Kind == PlayerAdvanceEventKind.SystemRepairCompleted)
        );
    }

    private static SimulationState SameBoundaryRepairState(SimulationState state, bool damageFirst)
    {
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(2));
        SensorContactId reciprocal = npc.SensorKnowledge.Contacts.Single().Id;
        SimulationTime due = state.Time.AdvanceBy(SimulationFixedStep.Duration);
        SimulationScheduler scheduler = state.Scheduler;
        ScheduledWork repairWork;
        ScheduledWork wakeWork;
        if (damageFirst)
        {
            (scheduler, wakeWork) = scheduler.Schedule(due, npc.InstanceId, ScheduledWorkKind.ShipCombatDecisionWake);
            (scheduler, repairWork) = scheduler.Schedule(
                due,
                player.InstanceId,
                ScheduledWorkKind.SystemRepairCompletion
            );
        }
        else
        {
            (scheduler, repairWork) = scheduler.Schedule(
                due,
                player.InstanceId,
                ScheduledWorkKind.SystemRepairCompletion
            );
            (scheduler, wakeWork) = scheduler.Schedule(due, npc.InstanceId, ScheduledWorkKind.ShipCombatDecisionWake);
        }
        SimulationTime repairStart = new(due.Milliseconds - 6000);
        var repair = new SystemRepairState(
            ShipSystemId.DirectedEnergyWeapons,
            new SystemCondition(0.5),
            new SystemCondition(1),
            repairStart,
            due,
            repairWork.Id
        );
        ShipEngineeringState engineering = player.Engineering.WithCondition(
            ShipSystemId.DirectedEnergyWeapons,
            repair.ConditionAt(state.Time)
        ) with
        {
            ActiveRepair = repair,
        };
        state = state
            .ReplaceShip(player.InstanceId, player with { Engineering = engineering })
            .ReplaceShip(
                npc.InstanceId,
                npc with
                {
                    Combat = npc.Combat with
                    {
                        PendingStimulus = new CombatStimulus(reciprocal, state.Time, due, wakeWork.Id),
                    },
                }
            ) with
        {
            Scheduler = scheduler,
        };
        return state;
    }

    /// <summary>Creates reciprocal identity through the existing scan and hail commands.</summary>
    private static (GameSimulation Game, Milestone3ProofFixture Fixture, SensorContactId Contact) Pair(int shieldPower)
    {
        var fixture = new Milestone3ProofFixture();
        var location = new LocationId("pair");
        var map = new StrategicMap([new StrategicLocation(location, "Pair", default)], []);
        ShipStart Start(long id, double x, PowerAllocation allocation) =>
            new(
                new ShipInstanceId(id),
                new ShipDefinitionId("pathfinder"),
                "Ship " + id,
                new TacticalPosition(x, 0),
                default,
                new SystemCondition(1),
                new SystemCondition(1),
                new SystemCondition(1),
                allocation,
                new AtLocationStart(location)
            )
            {
                ShieldCondition = new SystemCondition(1),
                DirectedEnergyCondition = new SystemCondition(1),
            };
        GameSimulation game = new GameBootstrap(
            new SimulationTime(6000),
            map,
            new ShipInstanceId(1),
            [
                Start(1, 0, new PowerAllocation(new PowerUnits(70), new PowerUnits(20), default, new PowerUnits(30))),
                Start(
                    2,
                    10,
                    new PowerAllocation(
                        new PowerUnits(70),
                        new PowerUnits(5),
                        new PowerUnits(shieldPower),
                        new PowerUnits(30)
                    )
                ),
            ]
        ).CreateSimulation(fixture.Catalog);
        SimulationState initial = game.CaptureState();
        ShipState npc = initial.GetRequiredShip(new ShipInstanceId(2));
        game = GameSimulation.RestoreState(
            initial.ReplaceShip(
                npc.InstanceId,
                npc with
                {
                    AutonomousState = npc.AutonomousState with { ContactPosture = ShipContactPosture.CautiousContact },
                }
            ),
            fixture.Catalog
        );
        game.AdvanceFixedSteps(1);
        SensorContactId contact = game.GetPlayerProjection().Ship.Sensors.Contacts.Single().Id;
        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(contact).Outcome);
        game.AdvanceFixedSteps(20);
        Assert.Equal(HailOutcome.Acknowledged, game.RequestHail(contact).Outcome);
        return (game, fixture, contact);
    }

    /// <summary>Every invalid public fire command preserves the exact aggregate and scheduler counters.</summary>
    [Theory]
    [InlineData(FireDirectedEnergyOutcome.ContactNotFound)]
    [InlineData(FireDirectedEnergyOutcome.ContactNotCurrent)]
    [InlineData(FireDirectedEnergyOutcome.ContactNotIdentified)]
    [InlineData(FireDirectedEnergyOutcome.OutOfRange)]
    [InlineData(FireDirectedEnergyOutcome.WeaponUnpowered)]
    [InlineData(FireDirectedEnergyOutcome.WeaponOffline)]
    [InlineData(FireDirectedEnergyOutcome.UnsupportedSystem)]
    public void RejectedFireNeverChangesState(FireDirectedEnergyOutcome expected)
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(0);
        SimulationState state = game.CaptureState();
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        SensorContactTrack track = player.SensorKnowledge.Contacts.Single();
        if (expected == FireDirectedEnergyOutcome.ContactNotCurrent)
            track = track with { Status = SensorContactStatus.Lost };
        if (expected == FireDirectedEnergyOutcome.ContactNotIdentified)
            track = track with
            {
                Identification = SensorContactIdentification.Detected,
                KnownVesselDisplayName = null,
                KnownDesignDisplayName = null,
            };
        if (expected == FireDirectedEnergyOutcome.OutOfRange)
            track = track with { LastObservedPosition = new TacticalPosition(21, 0) };
        ShipEngineeringState engineering = player.Engineering;
        if (expected == FireDirectedEnergyOutcome.WeaponUnpowered)
            engineering = engineering with
            {
                Allocation = engineering.Allocation with { DirectedEnergyWeapons = default },
            };
        if (expected == FireDirectedEnergyOutcome.WeaponOffline)
            engineering = engineering with { DirectedEnergyCondition = default };
        player = player with
        {
            SensorKnowledge = new SensorKnowledge(player.SensorKnowledge.NextContactId, [track], null),
            Engineering = engineering,
        };
        game = GameSimulation.RestoreState(state.ReplaceShip(player.InstanceId, player), fixture.Catalog);
        SimulationState before = game.CaptureState();
        var intent = new FireDirectedEnergyIntent(
            expected == FireDirectedEnergyOutcome.ContactNotFound ? new SensorContactId(99) : contact,
            expected == FireDirectedEnergyOutcome.UnsupportedSystem ? default : ShipSystemId.Sensors
        );
        FireDirectedEnergyResult result = game.FireDirectedEnergy(intent);
        Assert.Equal(expected, result.Outcome);
        Assert.Empty(result.ResolvedEvents);
        Assert.Same(before, game.CaptureState());
    }

    /// <summary>Range overflow yields a typed rejection and a nullable projection rather than exposing hidden truth.</summary>
    [Fact]
    public void ExtremeFiniteSeparationIsOutOfRange()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(0);
        SimulationState state = game.CaptureState();
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        SensorContactTrack track = player.SensorKnowledge.Contacts.Single() with
        {
            LastObservedPosition = new TacticalPosition(double.MaxValue, 0),
        };
        player = player with
        {
            TacticalPosition = new TacticalPosition(-double.MaxValue, 0),
            SensorKnowledge = new SensorKnowledge(player.SensorKnowledge.NextContactId, [track], null),
        };
        game = GameSimulation.RestoreState(state.ReplaceShip(player.InstanceId, player), fixture.Catalog);
        Assert.Null(game.GetPlayerProjection().Ship.Combat.Targets.Single().Range);
        Assert.Equal(
            FireDirectedEnergyOutcome.OutOfRange,
            game.FireDirectedEnergy(new(contact, ShipSystemId.Sensors)).Outcome
        );
        Assert.False(CombatLegality.WithinRange(double.PositiveInfinity, double.MaxValue));
    }

    /// <summary>The exact readiness instant permits fire and retains supported semantic choices while cooling.</summary>
    [Fact]
    public void ExactCooldownBoundaryPermitsFire()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(15);
        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(2));
        npc = npc with { SensorKnowledge = SensorKnowledge.Empty };
        game = GameSimulation.RestoreState(state.ReplaceShip(npc.InstanceId, npc), fixture.Catalog);
        game.FireDirectedEnergy(new(contact, ShipSystemId.Sensors));
        Assert.Equal(5, game.GetPlayerProjection().Ship.Combat.Targets.Single().SupportedSystems.Count);
        game.AdvanceFixedSteps(19);
        Assert.Equal(
            FireDirectedEnergyOutcome.CooldownActive,
            game.FireDirectedEnergy(new(contact, ShipSystemId.Sensors)).Outcome
        );
        game.AdvanceFixedSteps(1);
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            game.FireDirectedEnergy(new(contact, ShipSystemId.Sensors)).Outcome
        );
    }

    /// <summary>An unobserved attack never manufactures defensive knowledge later.</summary>
    [Fact]
    public void UnobservedAttackerDoesNotCreateStimulus()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(0);
        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(2));
        npc = npc with { SensorKnowledge = SensorKnowledge.Empty };
        game = GameSimulation.RestoreState(state.ReplaceShip(npc.InstanceId, npc), fixture.Catalog);
        game.FireDirectedEnergy(new(contact, ShipSystemId.Shields));
        Assert.Null(game.CaptureState().GetRequiredShip(npc.InstanceId).Combat.PendingStimulus);
        game.AdvanceFixedSteps(1);
        Assert.Null(game.CaptureState().GetRequiredShip(npc.InstanceId).Combat.PendingStimulus);
    }

    /// <summary>Damage cannot overflow its sensor-loss reconciliation after passing weapon cooldown bounds.</summary>
    [Fact]
    public void NearTimeLimitRejectsBeforeAnyDamageOrFutureWork()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(0);
        SimulationState state = game.CaptureState() with
        {
            Time = new SimulationTime((long.MaxValue - 3000) / 100 * 100),
        };
        game = GameSimulation.RestoreState(state, fixture.Catalog);
        FireDirectedEnergyResult result = game.FireDirectedEnergy(new(contact, ShipSystemId.Sensors));
        Assert.Equal(FireDirectedEnergyOutcome.TimeLimitExceeded, result.Outcome);
        Assert.Empty(result.ResolvedEvents);
        Assert.Same(state, game.CaptureState());
    }
}
