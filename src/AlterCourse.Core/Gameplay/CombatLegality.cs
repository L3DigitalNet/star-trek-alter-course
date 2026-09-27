using AlterCourse.Core.Factions;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Gameplay;

/// <summary>Shares actor-safe fire constraints among commands, projections, and defensive decisions.</summary>
internal static class CombatLegality
{
    /// <summary>Evaluates a shot, beginning with whether the aim kind is in the admitted public vocabulary.</summary>
    /// <remarks>
    /// The vocabulary is the loaded catalog's damage-target kinds: public content, never the victim's installations
    /// (an inventory oracle) and never the attacker's own (a shieldless attacker may aim at shields). An aimed kind
    /// the victim lacks is therefore not refused here; the shot discharges and its residual damage has no receiver.
    /// </remarks>
    internal static FireDirectedEnergyOutcome Evaluate(
        CombatOwnFacts own,
        SensorContactSnapshot? contact,
        bool sameContext,
        ShipSystemKind aimKind,
        IReadOnlyList<ShipSystemKind> admittedAimKinds
    )
    {
        ArgumentNullException.ThrowIfNull(admittedAimKinds);
        return !admittedAimKinds.Contains(aimKind)
            ? FireDirectedEnergyOutcome.UnsupportedSystem
            : EvaluatePrerequisites(own, contact, sameContext);
    }

    /// <summary>
    /// Evaluates the actor-safe prerequisites independent of any aim kind, so pre-shot projections and defensive
    /// doctrine never vary with which kinds the catalog admits or which systems the target has.
    /// </summary>
    internal static FireDirectedEnergyOutcome EvaluatePrerequisites(
        CombatOwnFacts own,
        SensorContactSnapshot? contact,
        bool sameContext
    )
    {
        ArgumentNullException.ThrowIfNull(own);
        if (contact is null)
            return FireDirectedEnergyOutcome.ContactNotFound;
        if (contact.Status != SensorContactStatus.Current)
            return FireDirectedEnergyOutcome.ContactNotCurrent;
        if (contact.Identification != SensorContactIdentification.Identified)
            return FireDirectedEnergyOutcome.ContactNotIdentified;
        if (!own.AtLocation || !sameContext)
            return FireDirectedEnergyOutcome.NotAtSameLocation;
        if (own.Weapon is null || own.WeaponCondition.Value == 0)
            return FireDirectedEnergyOutcome.WeaponOffline;
        if (own.WeaponCapability <= 0)
            return FireDirectedEnergyOutcome.WeaponUnpowered;
        if (own.Time.Milliseconds < own.ReadyAt.Milliseconds)
            return FireDirectedEnergyOutcome.CooldownActive;
        if (!WithinRange(Distance(own.Position, contact.LastObservedPosition), own.Weapon.Range.Value))
            return FireDirectedEnergyOutcome.OutOfRange;
        long headroom = long.MaxValue - SimulationFixedStep.Duration.Milliseconds - own.Time.Milliseconds;
        if (headroom < RequiredHeadroom(own.Weapon))
            return FireDirectedEnergyOutcome.TimeLimitExceeded;
        return FireDirectedEnergyOutcome.Accepted;
    }

    // Fire can invalidate observations or publish an observation in the existing reconciliation pass. Reserve the
    // entire longest required future correlation before constructing damage, rather than catching overflow later.
    internal static long RequiredHeadroom(DirectedEnergyWeaponDefinition weapon) =>
        Math.Max(
            weapon.Cooldown.Milliseconds,
            Math.Max(
                GameSimulation.SensorContactLossMilliseconds,
                FactionObservationState.ReportDeliveryDelayMilliseconds
            )
        );

    internal static bool WithinRange(double distance, double range) =>
        double.IsFinite(distance) && (distance <= range || distance - range <= Math.Max(1, range) * 1e-12);

    internal static double Distance(TacticalPosition left, TacticalPosition right)
    {
        double x = Math.Abs(left.XKilometers - right.XKilometers);
        double y = Math.Abs(left.YKilometers - right.YKilometers);
        if (x < y)
            (x, y) = (y, x);
        if (double.IsPositiveInfinity(x) || x == 0)
            return x;
        double ratio = y / x;
        return x * Math.Sqrt(1 + ratio * ratio);
    }
}
