using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tests.Gameplay;
using static AlterCourse.Core.Tests.Characterization.M6ABaselineRecords;

namespace AlterCourse.Core.Tests.Characterization;

/// <summary>
/// Drives each M6A behavior through real Core commands and returns its outcome record.
/// </summary>
/// <remarks>
/// <para>
/// Scenarios reuse the existing proof worlds rather than inventing new ones: the production first game
/// (<see cref="FirstGameSetup"/> over the authored <c>pathfinder.json</c> V5 content, four- and six-ship
/// compositions) and the <see cref="M6CombatProofFixture.Pair"/> three-ship engagement (player 1 at x=0 with
/// allocation 70/20/0/30, defender 2 at x=10 with 70/5/15/30, scan target 3 at x=28, all systems nominal, the
/// defender already identified and hailed at 2100 ms). Both worlds are also what the existing M6 scenario tests
/// prove, so a migration that re-plumbs those fixtures re-plumbs these scenarios too.
/// </para>
/// <para>
/// All reads and kind-selecting commands go through <see cref="Probe"/>; the only state authoring here is the
/// withdrawal defender's allocation, again through the probe. Time only advances by whole fixed steps, so
/// every scenario is deterministic and needs no clock.
/// </para>
/// </remarks>
internal sealed class M6ABaselineScenarios
{
    internal const long Player = 1;
    internal const long Defender = 2;
    internal const long ScanTarget = 3;
    internal const long Kestrel = 4;

    internal M6ABaselineScenarios(bool exact = false)
    {
        Fixture = new M6CombatProofFixture();
        Probe = new M6ABaselineProbe(Fixture.Catalog, exact);
    }

    internal M6CombatProofFixture Fixture { get; }

    internal M6ABaselineProbe Probe { get; }

    internal DefinitionFacts ProductionContent() => Probe.Definition("pathfinder");

    internal NewGameOutcome NewGame()
    {
        GameSimulation four = Fixture.FourShipFirstGame();
        GameSimulation six = Fixture.Production();
        return new NewGameOutcome(
            Probe.View(four),
            Sequence([.. Probe.AllShips(four).Items.Select(ship => ship.Engineering)]),
            Sequence([.. Probe.AllShips(six).Items.Select(ship => ship.Engineering)])
        );
    }

    internal AllocationChange Preset(PresetChoice choice)
    {
        GameSimulation game = Fixture.FourShipFirstGame();
        PowerAllocationResult result = M6ABaselineProbe.ApplyPreset(game, choice);
        return new AllocationChange(result.Outcome, Probe.View(game), M6ABaselineProbe.Events(result.ResolvedEvents));
    }

    /// <summary>
    /// Submits exact allocations in a fixed order to one first game; each rejection must leave the prior
    /// allocation in place, which the Resulting column pins.
    /// </summary>
    internal (OutcomeSequence<AllocationAttempt> Attempts, SetTacticalCourseOutcome SpeedSetup) ExactAllocation()
    {
        GameSimulation game = Fixture.FourShipFirstGame();
        var attempts = new List<AllocationAttempt>();
        void Attempt(AllocationTuple requested) =>
            attempts.Add(
                new AllocationAttempt(
                    requested,
                    M6ABaselineProbe.SetAllocation(game, requested).Outcome,
                    Probe.Player(game).Engineering.Allocation
                )
            );
        Attempt(new(71, 0, 0, 0));
        Attempt(new(0, 51, 0, 0));
        Attempt(new(0, 0, 41, 0));
        Attempt(new(0, 0, 0, 31));
        Attempt(new(45, 31, 0, 0));
        Attempt(new(20, 5, 20, 30));
        Attempt(new(44, 31, 0, 0));
        // 31 of 50 impulse on a nominal drive allows exactly 6.2 km/s; one unit less makes that speed illegal.
        SetTacticalCourseOutcome speed = game.SetTacticalCourse(
            new(new HeadingDegrees(0), new SpeedKilometersPerSecond(6.2))
        ).Outcome;
        Attempt(new(44, 30, 0, 0));
        return (Sequence([.. attempts]), speed);
    }

    internal DamageOutcome Brownout(bool exceedReducedGeneration)
    {
        GameSimulation game = Fixture.Pair();
        AllocationTuple allocation = exceedReducedGeneration ? new(70, 20, 0, 30) : new(40, 10, 0, 30);
        Assert.Equal(PowerAllocationOutcome.Accepted, M6ABaselineProbe.SetAllocation(game, allocation).Outcome);
        return Damage(game, ShipSystemKind.PowerGeneration);
    }

    internal DamageOutcome SubsystemDamage(ShipSystemKind system) => Damage(Fixture.Pair(), system);

    internal SensingOutcome SensingAndScanning()
    {
        GameSimulation game = Fixture.FourShipFirstGame();
        Assert.Equal(
            PowerAllocationOutcome.Accepted,
            M6ABaselineProbe.ApplyPreset(game, new PresetChoice(ShipSystemKind.Sensors)).Outcome
        );
        PlayerViewOutcome afterPreset = Probe.View(game);
        AdvanceUntilResult detection = game.AdvanceUntilNextPlayerRelevantEvent();
        PlayerViewOutcome atDetection = Probe.View(game);
        OutcomeSequence<ContactOutcome> contacts = Probe.Player(game).Contacts;
        long contact = M6ABaselineProbe.ContactOf(game, Player, Kestrel);
        ActiveSensorScanOutcome request = game.RequestActiveSensorScan(new SensorContactId(contact)).Outcome;
        ScanOutcome? inFlight = Probe.Player(game).ActiveScan;
        game.AdvanceFixedSteps(10);
        PlayerViewOutcome midpoint = Probe.View(game);
        SimulationAdvanceResult completion = game.AdvanceFixedSteps(10);
        return new SensingOutcome(
            afterPreset,
            detection.StoppedAt.Milliseconds,
            M6ABaselineProbe.Events(detection.ResolvedEvents),
            atDetection,
            contacts,
            request,
            inFlight,
            midpoint,
            M6ABaselineProbe.Events(completion.ResolvedEvents),
            Probe.Player(game).Contacts,
            Probe.Player(game).ActiveScan
        );
    }

    internal MotionScenarioOutcome TacticalMotion()
    {
        GameSimulation game = Fixture.Pair();
        double maximum = Probe.View(game).EffectiveMaximumSpeed;
        var heading = new HeadingDegrees(90);
        SetTacticalCourseOutcome above = game.SetTacticalCourse(
            new(heading, new SpeedKilometersPerSecond(4.01))
        ).Outcome;
        SetTacticalCourseOutcome at = game.SetTacticalCourse(new(heading, new SpeedKilometersPerSecond(4))).Outcome;
        game.AdvanceFixedSteps(10);
        MotionOutcome afterOneSecond = Probe.Player(game).Motion;
        (GameSimulation hit, OutcomeSequence<EventOutcome> events) = M6ABaselineProbe.Incoming(
            Fixture,
            game,
            ShipSystemKind.ImpulsePropulsion
        );
        ShipOutcome player = Probe.Player(hit);
        return new MotionScenarioOutcome(
            maximum,
            above,
            at,
            afterOneSecond,
            new DamageOutcome(player.Engineering, player.Motion, events),
            Probe.View(hit).EffectiveMaximumSpeed
        );
    }

    /// <summary>
    /// Fires at the proof defender's impulse at every readiness boundary until a shot is refused. The defender's
    /// delayed return fire targets the player's weapon each time, so every later shot carries less damage and
    /// the shields absorb all of it; the sequence pins absorption against a degrading attacker until the
    /// player's weapon goes offline.
    /// </summary>
    internal OutcomeSequence<ShotOutcome> ShieldAbsorption()
    {
        GameSimulation game = Fixture.Pair();
        return Engagement(game, M6ABaselineProbe.ContactOf(game, Player, Defender), Defender);
    }

    /// <summary>
    /// The production first-game engagement: the existing combat-range approach, then shots at the Kestrel's
    /// impulse until the first penetration or refusal.
    /// </summary>
    internal OutcomeSequence<ShotOutcome> FirstGameEngagement()
    {
        GameSimulation game = Fixture.FourShipFirstGame();
        long contact = Fixture.EnterCombatRange(game).Value;
        return Engagement(game, contact, Kestrel);
    }

    /// <summary>
    /// One hit on the player's impulse through weakly powered shields (4 of 40): capacity 0.1 absorbs part of
    /// the 0.25 shot and the remainder penetrates to the selected system.
    /// </summary>
    internal DamageOutcome PartialAbsorption()
    {
        GameSimulation game = Fixture.Pair();
        Assert.Equal(PowerAllocationOutcome.Accepted, M6ABaselineProbe.SetAllocation(game, new(70, 20, 4, 26)).Outcome);
        return Damage(game, ShipSystemKind.ImpulsePropulsion);
    }

    private OutcomeSequence<ShotOutcome> Engagement(GameSimulation game, long contact, long target)
    {
        var shots = new List<ShotOutcome>();
        for (int shot = 0; shot < 16; shot++)
        {
            AdvanceTo(game, Math.Max(Probe.Player(game).Combat.WeaponReadyAtMs, M6ABaselineProbe.TimeMs(game)));
            FireDirectedEnergyResult result = M6ABaselineProbe.Fire(game, contact, ShipSystemKind.ImpulsePropulsion);
            OutcomeSequence<EventOutcome> events = M6ABaselineProbe.Events(result.ResolvedEvents);
            shots.Add(
                new ShotOutcome(
                    M6ABaselineProbe.TimeMs(game),
                    result.Outcome,
                    events,
                    Probe.Ship(game, target).Engineering.Conditions,
                    Probe.Player(game).Combat.WeaponReadyAtMs
                )
            );
            if (
                result.Outcome != FireDirectedEnergyOutcome.Accepted
                || events.Items.Any(item => item.Kind == PlayerAdvanceEventKind.SubsystemPenetration)
            )
                break;
        }
        return Sequence([.. shots]);
    }

    /// <summary>Damages <paramref name="system"/> by one real hit, then repairs it to nominal.</summary>
    internal RepairLifecycleOutcome RepairLifecycle(ShipSystemKind system)
    {
        (GameSimulation game, _) = M6ABaselineProbe.Incoming(Fixture, Fixture.Pair(), system);
        SystemRepairOutcome begin = M6ABaselineProbe.BeginRepair(game, system, 1).Outcome;
        RepairOutcome? started = Probe.Player(game).Engineering.Repair;
        long start = started!.StartedAtMs;
        long completion = started.CompletesAtMs;
        AdvanceTo(game, start + ((completion - start) / 2));
        EngineeringOutcome midpoint = Probe.Player(game).Engineering;
        double? progress = Probe.View(game).RepairProgress;
        OutcomeSequence<EventOutcome> events = AdvanceTo(game, completion);
        return new RepairLifecycleOutcome(begin, started, midpoint, progress, events, Probe.Player(game).Engineering);
    }

    internal RepairAdmissionOutcome RepairAdmission()
    {
        GameSimulation game = Fixture.Pair();
        SystemRepairOutcome nominal = M6ABaselineProbe.BeginRepair(game, ShipSystemKind.Sensors, 1).Outcome;
        (game, _) = M6ABaselineProbe.Incoming(Fixture, game, ShipSystemKind.PowerGeneration);
        SystemRepairOutcome generation = M6ABaselineProbe.BeginRepair(game, ShipSystemKind.PowerGeneration, 1).Outcome;
        AdvanceTo(game, Probe.Ship(game, Defender).Combat.WeaponReadyAtMs);
        (game, _) = M6ABaselineProbe.Incoming(Fixture, game, ShipSystemKind.Sensors);
        SystemRepairOutcome first = M6ABaselineProbe.BeginRepair(game, ShipSystemKind.Sensors, 1).Outcome;
        SystemRepairOutcome second = M6ABaselineProbe.BeginRepair(game, ShipSystemKind.Sensors, 1).Outcome;
        return new RepairAdmissionOutcome(nominal, generation, first, second);
    }

    /// <summary>
    /// A repair running on <paramref name="repaired"/> takes a second hit on <paramref name="hit"/> once the
    /// defender's weapon has recycled, then time runs one step past the repair's original completion.
    /// </summary>
    /// <remarks>
    /// For shield-absorption interruption the player first powers shields (50/20/20/30): with no shield power a
    /// hit is never absorbed, so the absorption path would be unreachable and the case would silently degrade
    /// to the damage-elsewhere case.
    /// </remarks>
    internal RepairHitOutcome RepairHit(ShipSystemKind repaired, ShipSystemKind hit, bool powerShields)
    {
        (GameSimulation game, _) = M6ABaselineProbe.Incoming(Fixture, Fixture.Pair(), repaired);
        Assert.Equal(SystemRepairOutcome.Accepted, M6ABaselineProbe.BeginRepair(game, repaired, 1).Outcome);
        if (powerShields)
            Assert.Equal(
                PowerAllocationOutcome.Accepted,
                M6ABaselineProbe.SetAllocation(game, new(50, 20, 20, 30)).Outcome
            );
        long completion = Probe.Player(game).Engineering.Repair!.CompletesAtMs;
        AdvanceTo(game, Probe.Ship(game, Defender).Combat.WeaponReadyAtMs);
        EngineeringOutcome before = Probe.Player(game).Engineering;
        (game, OutcomeSequence<EventOutcome> hitEvents) = M6ABaselineProbe.Incoming(Fixture, game, hit);
        EngineeringOutcome after = Probe.Player(game).Engineering;
        OutcomeSequence<EventOutcome> through = AdvanceTo(game, completion + SimulationFixedStep.Duration.Milliseconds);
        return new RepairHitOutcome(before, hitEvents, after, through, Probe.Player(game).Engineering);
    }

    internal DelayedDefenseOutcome DelayedDefense()
    {
        GameSimulation game = Fixture.Pair();
        long contact = M6ABaselineProbe.ContactOf(game, Player, Defender);
        FireDirectedEnergyResult shot = M6ABaselineProbe.Fire(game, contact, ShipSystemKind.Shields);
        Assert.Equal(FireDirectedEnergyOutcome.Accepted, shot.Outcome);
        CombatOutcome afterShot = Probe.Ship(game, Defender).Combat;
        DefenseOutcome? beforeDue = Probe.Defense(game);
        SimulationAdvanceResult step = game.AdvanceFixedSteps(1);
        return new DelayedDefenseOutcome(
            M6ABaselineProbe.Events(shot.ResolvedEvents),
            afterShot,
            beforeDue,
            M6ABaselineProbe.Events(step.ResolvedEvents),
            Probe.Defense(game),
            Probe.Ship(game, Defender).Combat,
            Probe.Player(game).Engineering
        );
    }

    internal WithdrawalOutcome Withdrawal()
    {
        GameSimulation game = M6ABaselineProbe.WithAllocation(Fixture, Fixture.Pair(), Defender, new(70, 5, 15, 0));
        long contact = M6ABaselineProbe.ContactOf(game, Player, Defender);
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            M6ABaselineProbe.Fire(game, contact, ShipSystemKind.Shields).Outcome
        );
        MotionOutcome before = Probe.Ship(game, Defender).Motion;
        double separationBefore = Separation(game);
        game.AdvanceFixedSteps(1);
        DefenseOutcome? decision = Probe.Defense(game);
        MotionOutcome afterDecision = Probe.Ship(game, Defender).Motion;
        game.AdvanceFixedSteps(10);
        return new WithdrawalOutcome(
            before,
            separationBefore,
            decision,
            afterDecision,
            Probe.Ship(game, Defender).Motion,
            Separation(game),
            Probe.Ship(game, Defender).Combat
        );
    }

    /// <summary>
    /// Builds the mixed continuation checkpoint: a real impulse hit at 2100 ms, then at 4100 ms (defender weapon
    /// recycled) an impulse repair, an active scan of the scan target, and a player shot at the defender — so an
    /// active repair, an in-flight scan, player weapon cooldown, and a pending defensive stimulus coexist.
    /// </summary>
    internal GameSimulation MixedCheckpoint()
    {
        (GameSimulation game, _) = M6ABaselineProbe.Incoming(Fixture, Fixture.Pair(), ShipSystemKind.ImpulsePropulsion);
        AdvanceTo(game, Probe.Ship(game, Defender).Combat.WeaponReadyAtMs);
        Assert.Equal(
            SystemRepairOutcome.Accepted,
            M6ABaselineProbe.BeginRepair(game, ShipSystemKind.ImpulsePropulsion, 1).Outcome
        );
        long scanContact = M6ABaselineProbe.ContactOf(game, Player, ScanTarget);
        Assert.Equal(
            ActiveSensorScanOutcome.Accepted,
            game.RequestActiveSensorScan(new SensorContactId(scanContact)).Outcome
        );
        long defenderContact = M6ABaselineProbe.ContactOf(game, Player, Defender);
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            M6ABaselineProbe.Fire(game, defenderContact, ShipSystemKind.Sensors).Outcome
        );
        return game;
    }

    /// <summary>Advances one fixed step at a time, recording every step's full outcome.</summary>
    internal OutcomeSequence<StepOutcome> Continue(GameSimulation game, int steps)
    {
        var outcomes = new List<StepOutcome>(steps);
        for (int step = 0; step < steps; step++)
        {
            SimulationAdvanceResult result = game.AdvanceFixedSteps(1);
            outcomes.Add(
                new StepOutcome(
                    result.FinalTime.Milliseconds,
                    M6ABaselineProbe.Events(result.ResolvedEvents),
                    Probe.AllShips(game),
                    Probe.Defense(game)
                )
            );
        }
        return Sequence([.. outcomes]);
    }

    private DamageOutcome Damage(GameSimulation game, ShipSystemKind system)
    {
        (GameSimulation hit, OutcomeSequence<EventOutcome> events) = M6ABaselineProbe.Incoming(Fixture, game, system);
        ShipOutcome player = Probe.Player(hit);
        return new DamageOutcome(player.Engineering, player.Motion, events);
    }

    private double Separation(GameSimulation game)
    {
        MotionOutcome own = Probe.Player(game).Motion;
        MotionOutcome other = Probe.Ship(game, Defender).Motion;
        return Math.Round(
            Math.Sqrt(Math.Pow(own.X - other.X, 2) + Math.Pow(own.Y - other.Y, 2)),
            M6ABaselineProbe.PinnedDigits
        );
    }

    private static OutcomeSequence<EventOutcome> AdvanceTo(GameSimulation game, long timeMs)
    {
        long remaining = timeMs - M6ABaselineProbe.TimeMs(game);
        Assert.True(remaining >= 0, "Scenario time must not move backwards.");
        Assert.Equal(0, remaining % SimulationFixedStep.Duration.Milliseconds);
        if (remaining == 0)
            return Sequence<EventOutcome>();
        int steps = checked((int)(remaining / SimulationFixedStep.Duration.Milliseconds));
        return M6ABaselineProbe.Events(game.AdvanceFixedSteps(steps).ResolvedEvents);
    }
}
