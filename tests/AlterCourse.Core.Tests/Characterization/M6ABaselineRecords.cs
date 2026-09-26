using System.Collections.Immutable;
using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Tests.Characterization;

/// <summary>
/// Representation-neutral outcome records for the M6A ship-system baseline (issue #121, ledger 2/13/14).
/// </summary>
/// <remarks>
/// <para>
/// Every record here is plain data: numbers, times in milliseconds, event kinds, contact knowledge states, and
/// per-kind values keyed by the stable semantic <see cref="ShipSystemId"/> strings the substrate wiki preserves.
/// Nothing here names a fixed-field Core type (<c>PowerAllocation</c>, <c>ShipEngineeringState</c>,
/// <c>SystemRepairState</c>, <c>EngineeringProjection</c>). Only <see cref="M6ABaselineProbe"/> builds these
/// records, so replacing the ship-system representation must leave this file and every pinned expectation
/// untouched; a changed expectation after migration is a behavior change, not an adapter change.
/// </para>
/// <para>
/// Per-kind tuples always use the canonical consumer order Sensors, Impulse, Shields, DirectedEnergy, and
/// condition sets prefix Generation. That order is the preset and remainder order the wiki keeps as the common
/// installed-consumer order, so the tuple layout itself is part of the characterized contract.
/// </para>
/// </remarks>
internal static class M6ABaselineRecords
{
    /// <summary>Exact integer power units in canonical consumer order.</summary>
    internal sealed record AllocationTuple(int Sensors, int Impulse, int Shields, int DirectedEnergy);

    /// <summary>Condition of every installed system, generation first then canonical consumer order.</summary>
    internal sealed record ConditionSet(
        double Generation,
        double Sensors,
        double Impulse,
        double Shields,
        double DirectedEnergy
    );

    /// <summary>Effective capability (condition times capped power satisfaction) in canonical consumer order.</summary>
    internal sealed record CapabilitySet(double Sensors, double Impulse, double Shields, double DirectedEnergy);

    /// <summary>Authored repair durations in milliseconds, canonical consumer order.</summary>
    internal sealed record RepairDurations(long Sensors, long Impulse, long Shields, long DirectedEnergy);

    /// <summary>The one active analytical repair, with the condition the state currently holds for its target.</summary>
    internal sealed record RepairOutcome(
        ShipSystemId Target,
        double From,
        double To,
        long StartedAtMs,
        long CompletesAtMs
    );

    /// <summary>Power budget, allocation, conditions, capabilities and repair of one ship at one instant.</summary>
    internal sealed record EngineeringOutcome(
        int AvailablePower,
        int Reserve,
        AllocationTuple Allocation,
        ConditionSet Conditions,
        CapabilitySet Capabilities,
        RepairOutcome? Repair
    );

    /// <summary>Tactical position and motion; heading in degrees and speed in km/s.</summary>
    internal sealed record MotionOutcome(double X, double Y, double Heading, double Speed);

    /// <summary>Observer-local knowledge of one contact.</summary>
    internal sealed record ContactOutcome(
        long ContactId,
        long TargetShipId,
        SensorContactStatus Status,
        SensorContactIdentification Identification,
        string? KnownVessel,
        string? KnownDesign
    );

    /// <summary>One in-flight active scan.</summary>
    internal sealed record ScanOutcome(long ContactId, long StartedAtMs, long CompletesAtMs);

    /// <summary>Directed-energy readiness and the pending defensive stimulus, when one exists.</summary>
    internal sealed record CombatOutcome(long WeaponReadyAtMs, StimulusOutcome? Stimulus);

    /// <summary>One pending defensive stimulus: whom it concerns and when it was observed and falls due.</summary>
    internal sealed record StimulusOutcome(long ContactId, long ObservedAtMs, long DueAtMs);

    /// <summary>Complete ship-system-relevant outcome of one ship at one simulation time.</summary>
    internal sealed record ShipOutcome(
        long ShipId,
        long TimeMs,
        EngineeringOutcome Engineering,
        MotionOutcome Motion,
        CombatOutcome Combat,
        OutcomeSequence<ContactOutcome> Contacts,
        ScanOutcome? ActiveScan
    );

    /// <summary>What the player is shown about its own systems: the derived ranges and repair progress.</summary>
    internal sealed record PlayerViewOutcome(
        int NominalGeneration,
        int AvailablePower,
        int Reserve,
        AllocationTuple Allocation,
        double EffectivePassiveRangeKm,
        double EffectiveMaximumSpeed,
        double? RepairProgress,
        double? ActiveScanProgress,
        long WeaponReadyAtMs
    );

    /// <summary>One player-visible advancement event.</summary>
    internal sealed record EventOutcome(PlayerAdvanceEventKind Kind, long AtMs, long? ContactId, ShipSystemId? System);

    /// <summary>Authored ship-design facts every behavior below derives from.</summary>
    internal sealed record DefinitionFacts(
        int NominalGeneration,
        AllocationTuple NominalDemands,
        double PassiveRangeKm,
        double MaximumTacticalSpeed,
        long ActiveScanMs,
        double WeaponRangeKm,
        double WeaponDamage,
        long WeaponCooldownMs,
        RepairDurations Repairs
    );

    /// <summary>One explained defensive decision and the outcome of applying it.</summary>
    internal sealed record DefenseOutcome(
        DefensiveCombatDecisionAction Selected,
        DefensiveCombatDecisionTieRule TieRule,
        OutcomeSequence<DefensiveCombatDecisionAction> CandidateOrder,
        FireDirectedEnergyOutcome? ReturnFireRejection,
        ShipSystemId TargetSystem,
        double? ResultingHeading,
        double? ResultingSpeed,
        FireDirectedEnergyOutcome? ApplicationOutcome,
        SetTacticalCourseOutcome? CourseOutcome
    );

    /// <summary>A power-preset choice: Balanced, or priority for one consumer kind.</summary>
    internal sealed record PresetChoice(ShipSystemId? Priority)
    {
        internal static PresetChoice Balanced { get; } = new((ShipSystemId?)null);
    }

    /// <summary>Result of one command-level power change on the player ship.</summary>
    internal sealed record AllocationChange(
        PowerAllocationOutcome Outcome,
        PlayerViewOutcome View,
        OutcomeSequence<EventOutcome> Events
    );

    /// <summary>One exact allocation request and the allocation the ship holds afterwards.</summary>
    internal sealed record AllocationAttempt(
        AllocationTuple Requested,
        PowerAllocationOutcome Outcome,
        AllocationTuple Resulting
    );

    /// <summary>Starting engineering of every ship in the two first-game compositions.</summary>
    internal sealed record NewGameOutcome(
        PlayerViewOutcome PlayerView,
        OutcomeSequence<EngineeringOutcome> FourShip,
        OutcomeSequence<EngineeringOutcome> SixShip
    );

    /// <summary>Damage consequence on the player: engineering, motion, and visible events.</summary>
    internal sealed record DamageOutcome(
        EngineeringOutcome Engineering,
        MotionOutcome Motion,
        OutcomeSequence<EventOutcome> Events
    );

    /// <summary>Passive acquisition then active scan of the first-game Kestrel contact.</summary>
    internal sealed record SensingOutcome(
        PlayerViewOutcome AfterPreset,
        long DetectedAtMs,
        OutcomeSequence<EventOutcome> DetectionEvents,
        PlayerViewOutcome AtDetection,
        OutcomeSequence<ContactOutcome> ContactsAtDetection,
        ActiveSensorScanOutcome ScanRequest,
        ScanOutcome? ScanInFlight,
        PlayerViewOutcome AtScanMidpoint,
        OutcomeSequence<EventOutcome> ScanEvents,
        OutcomeSequence<ContactOutcome> ContactsAfterScan,
        ScanOutcome? ScanAfterCompletion
    );

    /// <summary>Voluntary course limits and involuntary deceleration of the proof player.</summary>
    internal sealed record MotionScenarioOutcome(
        double MaximumSpeed,
        SetTacticalCourseOutcome AboveMaximum,
        SetTacticalCourseOutcome AtMaximum,
        MotionOutcome AfterOneSecond,
        DamageOutcome ImpulseHit,
        double MaximumSpeedAfterHit
    );

    /// <summary>One player shot attempt and the target conditions it leaves.</summary>
    internal sealed record ShotOutcome(
        long AtMs,
        FireDirectedEnergyOutcome Outcome,
        OutcomeSequence<EventOutcome> Events,
        ConditionSet TargetConditions,
        long PlayerWeaponReadyAtMs
    );

    /// <summary>Begin, midpoint, and completion of one analytical repair.</summary>
    internal sealed record RepairLifecycleOutcome(
        SystemRepairOutcome Begin,
        RepairOutcome? Started,
        EngineeringOutcome AtMidpoint,
        double? MidpointProgress,
        OutcomeSequence<EventOutcome> CompletionEvents,
        EngineeringOutcome AfterCompletion
    );

    /// <summary>Repair admission refusals, in the order they were requested.</summary>
    internal sealed record RepairAdmissionOutcome(
        SystemRepairOutcome NominalTarget,
        SystemRepairOutcome Generation,
        SystemRepairOutcome FirstDamaged,
        SystemRepairOutcome SecondWhileActive
    );

    /// <summary>How a running repair responds to a later hit, through its original completion time.</summary>
    internal sealed record RepairHitOutcome(
        EngineeringOutcome BeforeHit,
        OutcomeSequence<EventOutcome> HitEvents,
        EngineeringOutcome AfterHit,
        OutcomeSequence<EventOutcome> ThroughCompletionEvents,
        EngineeringOutcome AfterOriginalCompletion
    );

    /// <summary>A defensive response one fixed step after a public attack.</summary>
    internal sealed record DelayedDefenseOutcome(
        OutcomeSequence<EventOutcome> ShotEvents,
        CombatOutcome DefenderAfterShot,
        DefenseOutcome? BeforeDue,
        OutcomeSequence<EventOutcome> DecisionEvents,
        DefenseOutcome? Decision,
        CombatOutcome DefenderAfterDecision,
        EngineeringOutcome PlayerAfterDecision
    );

    /// <summary>A defender without weapon power choosing ordinary withdrawal and its tactical result.</summary>
    internal sealed record WithdrawalOutcome(
        MotionOutcome DefenderBefore,
        double SeparationBefore,
        DefenseOutcome? Decision,
        MotionOutcome DefenderAfterDecision,
        MotionOutcome DefenderAfterOneSecond,
        double SeparationAfterOneSecond,
        CombatOutcome DefenderCombatAfter
    );

    /// <summary>Everything observable after one fixed step of a continuation run.</summary>
    internal sealed record StepOutcome(
        long AtMs,
        OutcomeSequence<EventOutcome> Events,
        OutcomeSequence<ShipOutcome> Ships,
        DefenseOutcome? LastDefense
    );

    /// <summary>Immutable ordered sequence with structural equality, so records containing it compare by value.</summary>
    internal sealed record OutcomeSequence<T>(ImmutableArray<T> Items)
    {
        public bool Equals(OutcomeSequence<T>? other) => other is not null && Items.SequenceEqual(other.Items);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            foreach (T item in Items)
                hash.Add(item);
            return hash.ToHashCode();
        }

        // Failure messages print the elements; the default record printer would show only the array type.
        public override string ToString() => "[" + string.Join(", ", Items) + "]";
    }

    internal static OutcomeSequence<T> Sequence<T>(params T[] items) => new([.. items]);
}
