using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using static AlterCourse.Core.Tests.Characterization.M6ABaselineRecords;

namespace AlterCourse.Core.Tests.Characterization;

/// <summary>
/// The pinned M6A baseline: expected outcome records observed on the unmodified fixed-field code at a86206d.
/// </summary>
/// <remarks>
/// <para>
/// These values are the equivalence target for issue #121 (ledger 13/14 "Baseline equivalence"). They were
/// observed by running <see cref="M6ABaselineScenarios"/> at a86206d and cross-checked against the authored
/// proof values (generation 120; demands 70/50/40/30; passive range 30 km; 10 km/s; scan and cooldown 2000 ms;
/// weapon range 20 km; damage 0.25; repair 8000/6000/8000/6000 ms). A migration may change how the probe reads
/// state; it must not change a value here. Doubles are rounded to <see cref="M6ABaselineProbe.PinnedDigits"/>
/// places by the probe.
/// </para>
/// <para>
/// Times are simulation milliseconds. The proof pair reaches its first command boundary at 2100 ms (one
/// observation step plus the 2000 ms identifying scan); the first game starts at 0 ms.
/// </para>
/// </remarks>
internal static class M6ABaselineExpectations
{
    internal const long PairReadyMs = 2100;

    internal static DefinitionFacts PathfinderContent { get; } =
        new(120, A(70, 50, 40, 30), 30, 10, 2000, 20, 0.25, 2000, new RepairDurations(8000, 6000, 8000, 6000));

    // Balanced over the full 190-unit four-consumer demand at 75 available: floors 27/19/15/11 (sum 72) plus
    // one remainder unit each to the first three consumers in canonical order. The two-consumer (70/50) legacy
    // Balanced at 75 is 43/31 plus one remainder unit to sensors, i.e. the 44/31 first-game literal.
    internal static AllocationTuple FirstGamePlayerStartAllocation { get; } = A(44, 31, 0, 0);
    internal static AllocationTuple ConstrainedBalanced { get; } = A(28, 20, 16, 11);
    internal static AllocationTuple KestrelStartAllocation { get; } = A(70, 5, 15, 30);
    internal static AllocationTuple FactionShipStartAllocation { get; } = A(70, 50, 0, 0);

    private static readonly EngineeringOutcome FirstGamePlayer = new(
        75,
        0,
        FirstGamePlayerStartAllocation,
        C(0.625, 0.4, 1, 1, 1),
        K(0.251428571, 0.62, 0, 0),
        new RepairOutcome(ShipSystemKind.Sensors, 0.4, 1, 0, 8000)
    );

    private static readonly EngineeringOutcome NominalLegacyAllocation = new(
        120,
        0,
        FactionShipStartAllocation,
        C(1, 1, 1, 1, 1),
        K(1, 1, 0, 0),
        null
    );

    private static readonly EngineeringOutcome KestrelStart = new(
        120,
        0,
        KestrelStartAllocation,
        C(1, 1, 1, 1, 1),
        K(1, 0.1, 0.375, 1),
        null
    );

    internal static NewGameOutcome NewGame { get; } =
        new(
            View(A(44, 31, 0, 0), 7.542857143, 6.2, repairProgress: 0),
            Sequence(FirstGamePlayer, NominalLegacyAllocation, NominalLegacyAllocation, KestrelStart),
            Sequence(
                FirstGamePlayer,
                NominalLegacyAllocation,
                NominalLegacyAllocation,
                KestrelStart,
                NominalLegacyAllocation,
                NominalLegacyAllocation
            )
        );

    internal static AllocationChange Preset(string priority) =>
        priority switch
        {
            "balanced" => Accepted(View(ConstrainedBalanced, 4.8, 4, repairProgress: 0)),
            "sensors" => Accepted(View(A(70, 5, 0, 0), 12, 1, repairProgress: 0)),
            "impulse-propulsion" => Accepted(View(A(25, 50, 0, 0), 4.285714286, 10, repairProgress: 0)),
            "shields" => Accepted(View(A(35, 0, 40, 0), 6, 0, repairProgress: 0)),
            "directed-energy-weapons" => Accepted(View(A(45, 0, 0, 30), 7.714285714, 0, repairProgress: 0)),
            _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "No pinned preset."),
        };

    internal static OutcomeSequence<AllocationAttempt> ExactAllocationAttempts { get; } =
        Sequence(
            new AllocationAttempt(A(71, 0, 0, 0), PowerAllocationOutcome.SensorDemandExceeded, A(44, 31, 0, 0)),
            new AllocationAttempt(A(0, 51, 0, 0), PowerAllocationOutcome.ImpulseDemandExceeded, A(44, 31, 0, 0)),
            new AllocationAttempt(A(0, 0, 41, 0), PowerAllocationOutcome.ShieldDemandExceeded, A(44, 31, 0, 0)),
            new AllocationAttempt(A(0, 0, 0, 31), PowerAllocationOutcome.DirectedEnergyDemandExceeded, A(44, 31, 0, 0)),
            new AllocationAttempt(A(45, 31, 0, 0), PowerAllocationOutcome.AvailablePowerExceeded, A(44, 31, 0, 0)),
            new AllocationAttempt(A(20, 5, 20, 30), PowerAllocationOutcome.Accepted, A(20, 5, 20, 30)),
            new AllocationAttempt(A(44, 31, 0, 0), PowerAllocationOutcome.Accepted, A(44, 31, 0, 0)),
            new AllocationAttempt(
                A(44, 30, 0, 0),
                PowerAllocationOutcome.CurrentSpeedExceedsResultingMaximum,
                A(44, 31, 0, 0)
            )
        );

    // The pair player (70/20/0/30, no shield power) takes every hit unabsorbed: 0.25 on the selected system.
    private static readonly MotionOutcome PairPlayerAtRest = new(0, 0, 0, 0);

    internal static DamageOutcome Brownout { get; } =
        new(
            new EngineeringOutcome(
                90,
                0,
                A(53, 15, 0, 22),
                C(0.75, 1, 1, 1, 1),
                K(0.757142857, 0.3, 0, 0.733333333),
                null
            ),
            PairPlayerAtRest,
            Sequence(
                Ev(PlayerAdvanceEventKind.OwnSystemDamaged, PairReadyMs, 1, ShipSystemKind.PowerGeneration),
                Ev(PlayerAdvanceEventKind.PowerBrownout, PairReadyMs),
                Ev(PlayerAdvanceEventKind.SensorContactStale, PairReadyMs, 2)
            )
        );

    internal static DamageOutcome ReserveAbsorbsGenerationLoss { get; } =
        new(
            new EngineeringOutcome(90, 10, A(40, 10, 0, 30), C(0.75, 1, 1, 1, 1), K(0.571428571, 0.2, 0, 1), null),
            PairPlayerAtRest,
            Sequence(Ev(PlayerAdvanceEventKind.OwnSystemDamaged, PairReadyMs, 1, ShipSystemKind.PowerGeneration))
        );

    internal static DamageOutcome SubsystemDamage(string system) =>
        system switch
        {
            "power-generation" => Brownout,
            "sensors" => Unabsorbed(
                C(1, 0.75, 1, 1, 1),
                K(0.75, 0.4, 0, 1),
                ShipSystemKind.Sensors,
                Ev(PlayerAdvanceEventKind.SensorContactStale, PairReadyMs, 2)
            ),
            "impulse-propulsion" => Unabsorbed(C(1, 1, 0.75, 1, 1), K(1, 0.3, 0, 1), ShipSystemKind.ImpulsePropulsion),
            "shields" => Unabsorbed(C(1, 1, 1, 0.75, 1), K(1, 0.4, 0, 1), ShipSystemKind.Shields),
            "directed-energy-weapons" => Unabsorbed(
                C(1, 1, 1, 1, 0.75),
                K(1, 0.4, 0, 0.75),
                ShipSystemKind.DirectedEnergyWeapons
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(system), system, "No pinned damage."),
        };

    // Detection lands at 3500 ms, when the repairing sensors (0.4 -> 1 over 8000 ms) reach 0.6625 and the
    // sensor-priority range is 19.875 km against the Kestrel 18 km away. Scan: exactly 2000 ms.
    internal static SensingOutcome SensingAndScanning { get; } =
        new(
            View(A(70, 5, 0, 0), 12, 1, repairProgress: 0),
            3500,
            Sequence(Ev(PlayerAdvanceEventKind.SensorContactDetected, 3500, 1)),
            View(A(70, 5, 0, 0), 19.875, 1, repairProgress: 0.4375),
            Sequence(Contact(1, 4, SensorContactIdentification.Detected)),
            ActiveSensorScanOutcome.Accepted,
            new ScanOutcome(1, 3500, 5500),
            View(A(70, 5, 0, 0), 22.125, 1, repairProgress: 0.5625, scanProgress: 0.5),
            Sequence(Ev(PlayerAdvanceEventKind.ActiveSensorScanCompleted, 5500, 1)),
            Sequence(
                Contact(1, 4, SensorContactIdentification.Identified, "Survey Vessel Kestrel", "Pathfinder class")
            ),
            null
        );

    internal static MotionScenarioOutcome TacticalMotion { get; } =
        new(
            4,
            SetTacticalCourseOutcome.SpeedExceedsCurrentCapability,
            SetTacticalCourseOutcome.Accepted,
            new MotionOutcome(4, 0, 90, 4),
            new DamageOutcome(
                new EngineeringOutcome(120, 0, A(70, 20, 0, 30), C(1, 1, 0.75, 1, 1), K(1, 0.3, 0, 1), null),
                new MotionOutcome(4, 0, 90, 3),
                Sequence(
                    Ev(PlayerAdvanceEventKind.OwnSystemDamaged, 3100, 1, ShipSystemKind.ImpulsePropulsion),
                    Ev(PlayerAdvanceEventKind.ForcedDeceleration, 3100)
                )
            ),
            3
        );

    // Defender shields 15/40 powered (satisfaction 0.375). Player damage is 0.25 x own weapon capability, and
    // each delayed return fire costs the player 0.25 weapon condition: 0.25, 0.1875, 0.125, 0.0625, offline.
    internal static OutcomeSequence<ShotOutcome> ShieldAbsorption { get; } =
        Sequence(
            AbsorbedShot(2100, 0.75),
            AbsorbedShot(4100, 0.5625),
            AbsorbedShot(6100, 0.4375),
            AbsorbedShot(8100, 0.375),
            new ShotOutcome(
                10100,
                FireDirectedEnergyOutcome.WeaponOffline,
                Sequence<EventOutcome>(),
                C(1, 1, 1, 0.375, 1),
                10100
            )
        );

    // Kestrel shields 15/40: absorb 0.25, 0.25, then capacity 0.5 x 0.375 = 0.1875 absorbs and 0.0625 penetrates.
    internal static OutcomeSequence<ShotOutcome> FirstGameEngagement { get; } =
        Sequence(
            Shot(22000, C(1, 1, 1, 0.75, 1), 24000),
            Shot(24000, C(1, 1, 1, 0.5, 1), 26000),
            Shot(26000, C(1, 1, 0.9375, 0.3125, 1), 28000, penetrated: true)
        );

    internal static DamageOutcome PartialAbsorption { get; } =
        new(
            new EngineeringOutcome(
                120,
                0,
                A(70, 20, 4, 26),
                C(1, 1, 0.85, 0.9, 1),
                K(1, 0.34, 0.09, 0.866666667),
                null
            ),
            PairPlayerAtRest,
            Sequence(
                Ev(PlayerAdvanceEventKind.OwnSystemDamaged, PairReadyMs, 1, ShipSystemKind.Shields),
                Ev(PlayerAdvanceEventKind.OwnSystemDamaged, PairReadyMs, 1, ShipSystemKind.ImpulsePropulsion)
            )
        );

    internal static RepairLifecycleOutcome RepairLifecycle(string system) =>
        system switch
        {
            "sensors" => Lifecycle(
                ShipSystemKind.Sensors,
                8000,
                C(1, 0.875, 1, 1, 1),
                K(0.875, 0.4, 0, 1),
                Ev(PlayerAdvanceEventKind.SensorContactLost, 7100, 2),
                Ev(PlayerAdvanceEventKind.SensorContactReacquired, 8000, 2)
            ),
            "impulse-propulsion" => Lifecycle(
                ShipSystemKind.ImpulsePropulsion,
                6000,
                C(1, 1, 0.875, 1, 1),
                K(1, 0.35, 0, 1)
            ),
            "shields" => Lifecycle(ShipSystemKind.Shields, 8000, C(1, 1, 1, 0.875, 1), K(1, 0.4, 0, 1)),
            "directed-energy-weapons" => Lifecycle(
                ShipSystemKind.DirectedEnergyWeapons,
                6000,
                C(1, 1, 1, 1, 0.875),
                K(1, 0.4, 0, 0.875)
            ),
            _ => throw new ArgumentOutOfRangeException(nameof(system), system, "No pinned repair."),
        };

    internal static RepairAdmissionOutcome RepairAdmission { get; } =
        new(
            SystemRepairOutcome.TargetDoesNotImproveCondition,
            SystemRepairOutcome.UnsupportedSystem,
            SystemRepairOutcome.Accepted,
            SystemRepairOutcome.RepairAlreadyActive
        );

    private static readonly RepairOutcome ShieldRepair = new(ShipSystemKind.Shields, 0.75, 1, PairReadyMs, 10100);
    private static readonly RepairOutcome SensorRepair = new(ShipSystemKind.Sensors, 0.75, 1, PairReadyMs, 10100);

    // Shield repair at 0.8125 after 2000 of 8000 ms; the impulse hit is fully absorbed (capacity 0.40625), and any
    // absorption cancels a shield repair even though the selected system was impulse.
    internal static RepairHitOutcome ShieldRepairInterruptedByAbsorption { get; } =
        new(
            new EngineeringOutcome(
                120,
                0,
                A(50, 20, 20, 30),
                C(1, 1, 1, 0.8125, 1),
                K(0.714285714, 0.4, 0.40625, 1),
                ShieldRepair
            ),
            Sequence(
                Ev(PlayerAdvanceEventKind.OwnSystemDamaged, 4100, 1, ShipSystemKind.Shields),
                Ev(PlayerAdvanceEventKind.SystemRepairInterrupted, 4100, 1, ShipSystemKind.Shields)
            ),
            new EngineeringOutcome(
                120,
                0,
                A(50, 20, 20, 30),
                C(1, 1, 1, 0.5625, 1),
                K(0.714285714, 0.4, 0.28125, 1),
                null
            ),
            Sequence(Ev(PlayerAdvanceEventKind.SensorContactLost, 7100, 2)),
            new EngineeringOutcome(
                120,
                0,
                A(50, 20, 20, 30),
                C(1, 1, 1, 0.5625, 1),
                K(0.714285714, 0.4, 0.28125, 1),
                null
            )
        );

    internal static RepairHitOutcome SensorRepairInterruptedByPenetration { get; } =
        new(
            new EngineeringOutcome(120, 0, A(70, 20, 0, 30), C(1, 0.8125, 1, 1, 1), K(0.8125, 0.4, 0, 1), SensorRepair),
            Sequence(
                Ev(PlayerAdvanceEventKind.OwnSystemDamaged, 4100, 1, ShipSystemKind.Sensors),
                Ev(PlayerAdvanceEventKind.SystemRepairInterrupted, 4100, 1, ShipSystemKind.Sensors)
            ),
            new EngineeringOutcome(120, 0, A(70, 20, 0, 30), C(1, 0.5625, 1, 1, 1), K(0.5625, 0.4, 0, 1), null),
            Sequence(Ev(PlayerAdvanceEventKind.SensorContactLost, 7100, 2)),
            new EngineeringOutcome(120, 0, A(70, 20, 0, 30), C(1, 0.5625, 1, 1, 1), K(0.5625, 0.4, 0, 1), null)
        );

    internal static RepairHitOutcome SensorRepairSurvivesImpulseHit { get; } =
        new(
            new EngineeringOutcome(120, 0, A(70, 20, 0, 30), C(1, 0.8125, 1, 1, 1), K(0.8125, 0.4, 0, 1), SensorRepair),
            Sequence(Ev(PlayerAdvanceEventKind.OwnSystemDamaged, 4100, 1, ShipSystemKind.ImpulsePropulsion)),
            new EngineeringOutcome(
                120,
                0,
                A(70, 20, 0, 30),
                C(1, 0.8125, 0.75, 1, 1),
                K(0.8125, 0.3, 0, 1),
                SensorRepair
            ),
            Sequence(
                Ev(PlayerAdvanceEventKind.SensorContactLost, 7100, 2),
                Ev(PlayerAdvanceEventKind.SensorContactReacquired, 8000, 2),
                Ev(PlayerAdvanceEventKind.SystemRepairCompleted, 10100, system: ShipSystemKind.Sensors)
            ),
            new EngineeringOutcome(120, 0, A(70, 20, 0, 30), C(1, 1, 0.75, 1, 1), K(1, 0.3, 0, 1), null)
        );

    private static readonly OutcomeSequence<DefensiveCombatDecisionAction> CandidateOrder = Sequence(
        DefensiveCombatDecisionAction.ReturnFire,
        DefensiveCombatDecisionAction.Withdraw,
        DefensiveCombatDecisionAction.Hold
    );

    internal static DefenseOutcome ReturnFireDecision { get; } =
        new(
            DefensiveCombatDecisionAction.ReturnFire,
            DefensiveCombatDecisionTieRule.ReturnFireThenWithdrawThenHold,
            CandidateOrder,
            FireDirectedEnergyOutcome.Accepted,
            ShipSystemKind.DirectedEnergyWeapons,
            null,
            null,
            FireDirectedEnergyOutcome.Accepted,
            null
        );

    internal static DelayedDefenseOutcome DelayedDefense { get; } =
        new(
            Sequence(
                Ev(PlayerAdvanceEventKind.DirectedEnergyFired, PairReadyMs, 1, ShipSystemKind.Shields),
                Ev(PlayerAdvanceEventKind.ShieldImpact, PairReadyMs, 1, ShipSystemKind.Shields)
            ),
            new CombatOutcome(0, new StimulusOutcome(1, PairReadyMs, 2200)),
            null,
            Sequence(Ev(PlayerAdvanceEventKind.OwnSystemDamaged, 2200, 1, ShipSystemKind.DirectedEnergyWeapons)),
            ReturnFireDecision,
            new CombatOutcome(4200, null),
            new EngineeringOutcome(120, 0, A(70, 20, 0, 30), C(1, 1, 1, 1, 0.75), K(1, 0.4, 0, 0.75), null)
        );

    // Withdrawal speed is the defender's own propulsion limit: 10 km/s x (5 / 50) = 1 km/s, heading away (90).
    internal static WithdrawalOutcome Withdrawal { get; } =
        new(
            new MotionOutcome(11, 0, 90, 0),
            11,
            new DefenseOutcome(
                DefensiveCombatDecisionAction.Withdraw,
                DefensiveCombatDecisionTieRule.ReturnFireThenWithdrawThenHold,
                CandidateOrder,
                FireDirectedEnergyOutcome.WeaponUnpowered,
                ShipSystemKind.DirectedEnergyWeapons,
                90,
                1,
                null,
                SetTacticalCourseOutcome.Accepted
            ),
            new MotionOutcome(11, 0, 90, 1),
            new MotionOutcome(12, 0, 90, 1),
            12,
            new CombatOutcome(0, null)
        );

    internal const long ContinuationCheckpointMs = 4100;

    /// <summary>Player events of the mixed continuation run from the 4100 ms checkpoint to 12100 ms.</summary>
    internal static OutcomeSequence<EventOutcome> ContinuationEvents { get; } =
        Sequence(
            Ev(PlayerAdvanceEventKind.OwnSystemDamaged, 4200, 1, ShipSystemKind.DirectedEnergyWeapons),
            Ev(PlayerAdvanceEventKind.ActiveSensorScanCompleted, 6100, 2),
            Ev(PlayerAdvanceEventKind.SystemRepairCompleted, 10100, system: ShipSystemKind.ImpulsePropulsion)
        );

    internal static ShipOutcome ContinuationCheckpointPlayer { get; } =
        new(
            1,
            ContinuationCheckpointMs,
            new EngineeringOutcome(
                120,
                0,
                A(70, 20, 0, 30),
                C(1, 1, 0.75, 1, 1),
                K(1, 0.3, 0, 1),
                new RepairOutcome(ShipSystemKind.ImpulsePropulsion, 0.75, 1, 4100, 10100)
            ),
            PairPlayerAtRest,
            new CombatOutcome(6100, null),
            Sequence(
                Contact(1, 2, SensorContactIdentification.Identified, "Proof ship 2", "Pathfinder class"),
                Contact(2, 3, SensorContactIdentification.Detected)
            ),
            new ScanOutcome(2, 4100, 6100)
        );

    internal static CombatOutcome ContinuationCheckpointDefenderCombat { get; } =
        new(4100, new StimulusOutcome(1, 4100, 4200));

    internal static ShipOutcome ContinuationFinalPlayer { get; } =
        new(
            1,
            12100,
            new EngineeringOutcome(120, 0, A(70, 20, 0, 30), C(1, 1, 1, 1, 0.75), K(1, 0.4, 0, 0.75), null),
            PairPlayerAtRest,
            new CombatOutcome(6100, null),
            Sequence(
                Contact(1, 2, SensorContactIdentification.Identified, "Proof ship 2", "Pathfinder class"),
                Contact(2, 3, SensorContactIdentification.Identified, "Proof ship 3", "Pathfinder class")
            ),
            null
        );

    private static AllocationTuple A(int sensors, int impulse, int shields, int weapons) =>
        new(sensors, impulse, shields, weapons);

    private static ConditionSet C(double generation, double sensors, double impulse, double shields, double weapons) =>
        new(generation, sensors, impulse, shields, weapons);

    private static CapabilitySet K(double sensors, double impulse, double shields, double weapons) =>
        new(sensors, impulse, shields, weapons);

    private static EventOutcome Ev(
        PlayerAdvanceEventKind kind,
        long atMs,
        long? contact = null,
        ShipSystemKind? system = null
    ) => new(kind, atMs, contact, system);

    private static ContactOutcome Contact(
        long contact,
        long target,
        SensorContactIdentification identification,
        string? vessel = null,
        string? design = null
    ) => new(contact, target, SensorContactStatus.Current, identification, vessel, design);

    /// <summary>First-game player view; the weapon has never fired, so readiness is 0 ms.</summary>
    private static PlayerViewOutcome View(
        AllocationTuple allocation,
        double rangeKm,
        double maximumSpeed,
        double? repairProgress = null,
        double? scanProgress = null
    ) =>
        new(
            120,
            75,
            75 - allocation.Sensors - allocation.Impulse - allocation.Shields - allocation.DirectedEnergy,
            allocation,
            rangeKm,
            maximumSpeed,
            repairProgress,
            scanProgress,
            0
        );

    private static AllocationChange Accepted(PlayerViewOutcome view) =>
        new(PowerAllocationOutcome.Accepted, view, Sequence<EventOutcome>());

    private static DamageOutcome Unabsorbed(
        ConditionSet conditions,
        CapabilitySet capabilities,
        ShipSystemKind system,
        params EventOutcome[] consequences
    ) =>
        new(
            new EngineeringOutcome(120, 0, A(70, 20, 0, 30), conditions, capabilities, null),
            PairPlayerAtRest,
            Sequence([Ev(PlayerAdvanceEventKind.OwnSystemDamaged, PairReadyMs, 1, system), .. consequences])
        );

    private static ShotOutcome AbsorbedShot(long atMs, double defenderShields) =>
        Shot(atMs, C(1, 1, 1, defenderShields, 1), atMs + 2000);

    private static ShotOutcome Shot(long atMs, ConditionSet target, long readyAtMs, bool penetrated = false)
    {
        List<EventOutcome> events =
        [
            Ev(PlayerAdvanceEventKind.DirectedEnergyFired, atMs, 1, ShipSystemKind.ImpulsePropulsion),
            Ev(PlayerAdvanceEventKind.ShieldImpact, atMs, 1, ShipSystemKind.Shields),
        ];
        if (penetrated)
            events.Add(Ev(PlayerAdvanceEventKind.SubsystemPenetration, atMs, 1, ShipSystemKind.ImpulsePropulsion));
        return new ShotOutcome(atMs, FireDirectedEnergyOutcome.Accepted, Sequence([.. events]), target, readyAtMs);
    }

    /// <summary>A real 0.25 hit then a repair to nominal from 2100 ms, midpoint condition 0.875.</summary>
    private static RepairLifecycleOutcome Lifecycle(
        ShipSystemKind system,
        long durationMs,
        ConditionSet midpoint,
        CapabilitySet midpointCapabilities,
        params EventOutcome[] sensingEvents
    )
    {
        var repair = new RepairOutcome(system, 0.75, 1, PairReadyMs, PairReadyMs + durationMs);
        return new RepairLifecycleOutcome(
            SystemRepairOutcome.Accepted,
            repair,
            new EngineeringOutcome(120, 0, A(70, 20, 0, 30), midpoint, midpointCapabilities, repair),
            0.5,
            Sequence([
                .. sensingEvents,
                Ev(PlayerAdvanceEventKind.SystemRepairCompleted, PairReadyMs + durationMs, system: system),
            ]),
            new EngineeringOutcome(120, 0, A(70, 20, 0, 30), C(1, 1, 1, 1, 1), K(1, 0.4, 0, 1), null)
        );
    }
}
