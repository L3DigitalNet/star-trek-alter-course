using AlterCourse.Core.Factions;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Gameplay;

/// <summary>Shares actor-safe fire constraints among commands, projections, and defensive decisions.</summary>
internal static class CombatLegality
{
    internal static IReadOnlyList<ShipSystemKind> SupportedSystems { get; } =
        Array.AsReadOnly(
            new[]
            {
                ShipSystemKind.PowerGeneration,
                ShipSystemKind.Sensors,
                ShipSystemKind.ImpulsePropulsion,
                ShipSystemKind.Shields,
                ShipSystemKind.DirectedEnergyWeapons,
            }
        );

    internal static FireDirectedEnergyOutcome Evaluate(
        CombatOwnFacts own,
        SensorContactSnapshot? contact,
        bool sameContext,
        ShipSystemKind system
    )
    {
        if (!SupportedSystems.Contains(system))
            return FireDirectedEnergyOutcome.UnsupportedSystem;
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
