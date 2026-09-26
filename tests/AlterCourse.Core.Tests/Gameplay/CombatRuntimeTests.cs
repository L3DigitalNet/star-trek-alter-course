using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Player;
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
            game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors)).Outcome
        );
        ShipState after = game.CaptureState().GetRequiredShip(victim.InstanceId);
        Assert.Equal(0.75, after.Engineering.ShieldCondition.Value, 12);
        Assert.Equal(1, after.Engineering.SensorCondition.Value);
        Assert.Equal(
            before.Time.Milliseconds + 2000,
            game.CaptureState().GetRequiredShip(before.PlayerShipId).Combat.NextDirectedEnergyReadyAt.Milliseconds
        );
        SimulationState committed = game.CaptureState();
        FireDirectedEnergyResult rejected = game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors));
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
        var system = ShipSystemKind.Parse(systemName);
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
            game.FireDirectedEnergy(new(contact, ShipSystemKind.Shields)).Outcome
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
                && item.ShipSystemId == ShipSystemKind.DirectedEnergyWeapons
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
        game.FireDirectedEnergy(new(contact, ShipSystemKind.Shields));
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
        game.FireDirectedEnergy(new(contact, ShipSystemKind.Shields));
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
            ShipSystemKind.DirectedEnergyWeapons,
            new SystemCondition(0.5),
            new SystemCondition(1),
            repairStart,
            due,
            repairWork.Id
        );
        ShipEngineeringState engineering = player.Engineering.WithCondition(
            ShipSystemKind.DirectedEnergyWeapons,
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
            expected == FireDirectedEnergyOutcome.UnsupportedSystem ? default : ShipSystemKind.Sensors
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
            game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors)).Outcome
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
        game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors));
        Assert.Equal(5, game.GetPlayerProjection().Ship.Combat.Targets.Single().SupportedSystems.Count);
        game.AdvanceFixedSteps(19);
        Assert.Equal(
            FireDirectedEnergyOutcome.CooldownActive,
            game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors)).Outcome
        );
        game.AdvanceFixedSteps(1);
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors)).Outcome
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
        game.FireDirectedEnergy(new(contact, ShipSystemKind.Shields));
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
        FireDirectedEnergyResult result = game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors));
        Assert.Equal(FireDirectedEnergyOutcome.TimeLimitExceeded, result.Outcome);
        Assert.Empty(result.ResolvedEvents);
        Assert.Same(state, game.CaptureState());
    }

    /// <summary>Absent historical capacity yields typed rejection rather than inventing a repair duration.</summary>
    [Theory]
    [InlineData("shields")]
    [InlineData("directed-energy-weapons")]
    public void HistoricalAbsentSystemRepairIsUnsupported(string systemName)
    {
        var engineering = new ShipEngineeringDefinition(
            new PowerUnits(120),
            new PowerUnits(70),
            new PowerUnits(50),
            new SimulationDuration(8000),
            new SimulationDuration(6000)
        );
        var definition = new ShipDefinition(
            new ShipDefinitionId("pathfinder"),
            "Historical design",
            new SpeedKilometersPerSecond(10),
            new DistanceKilometers(30),
            new SimulationDuration(2000),
            engineering
        );
        var catalog = new ShipDefinitionCatalog(
            new Dictionary<ShipDefinitionId, ShipDefinition> { [definition.Id] = definition }
        );
        var location = new LocationId("historical");
        var map = new StrategicMap([new StrategicLocation(location, "Historical", default)], []);
        var start = new ShipStart(
            new ShipInstanceId(1),
            definition.Id,
            "Historical vessel",
            default,
            default,
            new SystemCondition(1),
            new SystemCondition(1),
            new SystemCondition(1),
            new PowerAllocation(new PowerUnits(70), new PowerUnits(50)),
            new AtLocationStart(location)
        );
        GameSimulation game = new GameBootstrap(default, map, start.InstanceId, [start]).CreateSimulation(catalog);
        var system = ShipSystemKind.Parse(systemName);
        SimulationState before = game.CaptureState();
        Assert.Equal(
            SystemRepairOutcome.UnsupportedSystem,
            game.BeginSystemRepair(system, new SystemCondition(1)).Outcome
        );
        Assert.Same(before, game.CaptureState());
        EngineeringAction action =
            system == ShipSystemKind.Shields
                ? EngineeringAction.BeginShieldRepair
                : EngineeringAction.BeginDirectedEnergyRepair;
        Assert.False(
            game.GetPlayerProjection()
                .Ship.Engineering.Actions.Single(projected => projected.Action == action)
                .IsAvailable
        );
    }

    /// <summary>Forced generation reconciliation preserves heading and reports player-owned brownout and deceleration.</summary>
    [Fact]
    public void IncomingGenerationDamageReconcilesOwnPowerAndSpeed()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, _) = Pair(0);
        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(new(new HeadingDegrees(90), new SpeedKilometersPerSecond(4))).Outcome
        );
        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(2));
        SensorContactId localPlayer = npc.SensorKnowledge.Contacts.Single().Id;
        ShipDirectedEnergyApplicationResult shot = GameSimulation.ApplyShipDirectedEnergy(
            state,
            fixture.Catalog,
            npc.InstanceId,
            new(localPlayer, ShipSystemKind.PowerGeneration)
        );
        Assert.Equal(FireDirectedEnergyOutcome.Accepted, shot.Outcome);
        game = GameSimulation.RestoreState(shot.CandidateState, fixture.Catalog);
        ShipState player = game.CaptureState().GetRequiredShip(state.PlayerShipId);
        Assert.Equal(0.75, player.Engineering.GenerationCondition.Value, 12);
        Assert.Equal(90, player.Engineering.Allocation.Total);
        Assert.Equal(90, player.TacticalMotion.Heading.Value);
        Assert.Equal(3, player.TacticalMotion.Speed.Value, 12);
        Assert.Contains(shot.ResolvedEvents, item => item.Kind == PlayerAdvanceEventKind.PowerBrownout);
        Assert.Contains(shot.ResolvedEvents, item => item.Kind == PlayerAdvanceEventKind.ForcedDeceleration);
        Assert.Contains(
            shot.ResolvedEvents,
            item => item.Kind == PlayerAdvanceEventKind.OwnSystemDamaged && item.SensorContactId is not null
        );
    }

    /// <summary>Any positive applied penetration cancels repair even when condition was already zero.</summary>
    [Fact]
    public void PositiveDamageToAlreadyOfflineRepairCannotBeReconstituted()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, _) = Pair(0);
        game = WithPlayerRepair(game, fixture, ShipSystemKind.DirectedEnergyWeapons, default);
        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(2));
        ShipDirectedEnergyApplicationResult shot = GameSimulation.ApplyShipDirectedEnergy(
            state,
            fixture.Catalog,
            npc.InstanceId,
            new(npc.SensorKnowledge.Contacts.Single().Id, ShipSystemKind.DirectedEnergyWeapons)
        );
        game = GameSimulation.RestoreState(shot.CandidateState, fixture.Catalog);
        ShipState player = game.CaptureState().GetRequiredShip(state.PlayerShipId);
        Assert.Null(player.Engineering.ActiveRepair);
        Assert.Equal(0, player.Engineering.DirectedEnergyCondition.Value);
        Assert.DoesNotContain(
            game.CaptureState().Scheduler.OutstandingWork,
            work => work.Kind == ScheduledWorkKind.SystemRepairCompletion
        );
        game.AdvanceFixedSteps(1);
        Assert.Equal(
            0,
            game.CaptureState().GetRequiredShip(state.PlayerShipId).Engineering.DirectedEnergyCondition.Value
        );
    }

    /// <summary>Shield absorption damages and interrupts shield repair, while a protected different repair continues.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AbsorptionInterruptsOnlyDamagedRepair(bool repairShields)
    {
        (GameSimulation game, Milestone3ProofFixture fixture, _) = Pair(0);
        ShipSystemKind repaired = repairShields ? ShipSystemKind.Shields : ShipSystemKind.Sensors;
        game = WithPlayerRepair(game, fixture, repaired, new SystemCondition(repairShields ? 0.4 : 0.8));
        SimulationState state = game.CaptureState();
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        player = player with
        {
            Engineering = player.Engineering with
            {
                Allocation = new PowerAllocation(new PowerUnits(70), default, new PowerUnits(20), new PowerUnits(30)),
            },
        };
        state = state.ReplaceShip(player.InstanceId, player);
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(2));
        ShipDirectedEnergyApplicationResult shot = GameSimulation.ApplyShipDirectedEnergy(
            state,
            fixture.Catalog,
            npc.InstanceId,
            new(npc.SensorKnowledge.Contacts.Single().Id, ShipSystemKind.Sensors)
        );
        game = GameSimulation.RestoreState(shot.CandidateState, fixture.Catalog);
        player = game.CaptureState().GetRequiredShip(player.InstanceId);
        Assert.Equal(repairShields, player.Engineering.ActiveRepair is null);
        Assert.Equal(
            !repairShields,
            game.CaptureState()
                .Scheduler.OutstandingWork.Any(work =>
                    work.Id == state.GetRequiredShip(player.InstanceId).Engineering.ActiveRepair!.ScheduledCompletionId
                )
        );
        Assert.Equal(
            repairShields,
            shot.ResolvedEvents.Any(item => item.Kind == PlayerAdvanceEventKind.SystemRepairInterrupted)
        );
    }

    /// <summary>A pre-hit current contact admits one stimulus even when that hit disables its observations.</summary>
    [Fact]
    public void SensorDamageMakesAdmittedStimulusFailCurrentConstraint()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(0);
        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(2));
        npc = npc with { Engineering = npc.Engineering with { SensorCondition = new SystemCondition(0.4) } };
        game = GameSimulation.RestoreState(state.ReplaceShip(npc.InstanceId, npc), fixture.Catalog);
        game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors));
        npc = game.CaptureState().GetRequiredShip(npc.InstanceId);
        Assert.NotNull(npc.Combat.PendingStimulus);
        Assert.Equal(SensorContactStatus.Stale, npc.SensorKnowledge.Contacts.Single().Status);
        game.AdvanceFixedSteps(1);
        Assert.Null(game.CaptureState().GetRequiredShip(npc.InstanceId).Combat.PendingStimulus);
        Assert.Equal(DefensiveCombatDecisionAction.Hold, game.LastDefensiveCombatDecisionExplanation!.SelectedAction);
        Assert.Contains(
            game.LastDefensiveCombatDecisionExplanation.Candidates[0].Constraints,
            constraint => constraint.Constraint == DefensiveCombatConstraint.CurrentContact && !constraint.Satisfied
        );
    }

    /// <summary>Withdrawal is ordinary movement and naturally loses weapon range and contact without a combat lock.</summary>
    [Fact]
    public void OfflineDefenderWithdrawsUntilContactIsLost()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(0);
        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(2));
        npc = npc with { Engineering = npc.Engineering with { DirectedEnergyCondition = default } };
        game = GameSimulation.RestoreState(state.ReplaceShip(npc.InstanceId, npc), fixture.Catalog);
        game.FireDirectedEnergy(new(contact, ShipSystemKind.Shields));
        game.AdvanceFixedSteps(1);
        Assert.Equal(
            DefensiveCombatDecisionAction.Withdraw,
            game.LastDefensiveCombatDecisionExplanation!.SelectedAction
        );
        Assert.Equal(1, game.CaptureState().GetRequiredShip(npc.InstanceId).TacticalMotion.Speed.Value);
        game.AdvanceFixedSteps(400);
        Assert.True(
            CombatLegality.Distance(
                game.CaptureState().GetRequiredShip(state.PlayerShipId).TacticalPosition,
                game.CaptureState().GetRequiredShip(npc.InstanceId).TacticalPosition
            ) > 30
        );
        Assert.Equal(
            SensorContactStatus.Lost,
            game.CaptureState().GetRequiredShip(state.PlayerShipId).SensorKnowledge.Contacts.Single().Status
        );
    }

    private static GameSimulation WithPlayerRepair(
        GameSimulation game,
        Milestone3ProofFixture fixture,
        ShipSystemKind system,
        SystemCondition starting
    )
    {
        SimulationState state = game.CaptureState();
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        SimulationTime due = state.Time.AdvanceBy(
            fixture.Catalog.GetRequired(player.DefinitionId).Engineering.RepairDurationFor(system)
        );
        (SimulationScheduler scheduler, ScheduledWork work) = state.Scheduler.Schedule(
            due,
            player.InstanceId,
            ScheduledWorkKind.SystemRepairCompletion
        );
        var repair = new SystemRepairState(system, starting, new SystemCondition(1), state.Time, due, work.Id);
        player = player with
        {
            Engineering = player.Engineering.WithCondition(system, starting) with { ActiveRepair = repair },
        };
        return GameSimulation.RestoreState(
            state.ReplaceShip(player.InstanceId, player) with
            {
                Scheduler = scheduler,
            },
            fixture.Catalog
        );
    }

    /// <summary>Hidden target condition does not change targeting facts or disclose shield percentages.</summary>
    [Fact]
    public void TargetProjectionIgnoresHiddenEngineeringState()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, _) = Pair(0);
        PlayerProjection projected = game.GetPlayerProjection();
        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(2));
        npc = npc with
        {
            Engineering = npc.Engineering with
            {
                ShieldCondition = new SystemCondition(0.1),
                DirectedEnergyCondition = new SystemCondition(0.2),
            },
        };
        game = GameSimulation.RestoreState(state.ReplaceShip(npc.InstanceId, npc), fixture.Catalog);
        Assert.Equal(projected, game.GetPlayerProjection());
        Assert.DoesNotContain(
            typeof(CombatTargetProjection).GetProperties(),
            property =>
                property.Name.Contains("ShipId", StringComparison.Ordinal)
                || property.Name.Contains("Condition", StringComparison.Ordinal)
                || property.Name.Contains("Faction", StringComparison.Ordinal)
        );
    }

    /// <summary>A defensive wake with no actual player consequences leaves no-event advancement unchanged.</summary>
    [Fact]
    public void HiddenHoldDoesNotAdvancePlayerTimeOrExposePendingIntent()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(0);
        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(2));
        npc = npc with
        {
            TacticalMotion = default,
            Engineering = npc.Engineering with { DirectedEnergyCondition = default, ImpulseCondition = default },
        };
        game = GameSimulation.RestoreState(state.ReplaceShip(npc.InstanceId, npc), fixture.Catalog);
        game.FireDirectedEnergy(new(contact, ShipSystemKind.Shields));
        SimulationState before = game.CaptureState();
        AdvanceUntilResult result = game.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Equal(AdvanceUntilOutcome.NoPlayerEvent, result.Outcome);
        Assert.Empty(result.ResolvedEvents);
        Assert.Same(before, game.CaptureState());
        Assert.Equal(before.Time, result.StoppedAt);
    }

    /// <summary>Historical definitions retain their authored absence rather than gaining bootstrap combat allocations.</summary>
    [Fact]
    public void FirstGameSupportsHistoricalDefinitionsWithoutInventedCombatCapacity()
    {
        var engineering = new ShipEngineeringDefinition(
            new PowerUnits(120),
            new PowerUnits(70),
            new PowerUnits(50),
            new SimulationDuration(8000),
            new SimulationDuration(6000)
        );
        var definition = new ShipDefinition(
            new ShipDefinitionId("pathfinder"),
            "Historical design",
            new SpeedKilometersPerSecond(10),
            new DistanceKilometers(30),
            new SimulationDuration(2000),
            engineering
        );
        var catalog = new ShipDefinitionCatalog(
            new Dictionary<ShipDefinitionId, ShipDefinition> { [definition.Id] = definition }
        );
        GameSimulation game = FirstGameSetup.Create(catalog);
        ShipState kestrel = game.CaptureState().GetRequiredShip(new ShipInstanceId(4));
        Assert.Equal(default, kestrel.Engineering.ShieldCondition);
        Assert.Equal(default, kestrel.Engineering.DirectedEnergyCondition);
        Assert.Equal(new PowerAllocation(new PowerUnits(70), new PowerUnits(50)), kestrel.Engineering.Allocation);
    }
}
