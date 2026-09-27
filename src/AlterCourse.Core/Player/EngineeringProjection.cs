using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Player;

/// <summary>
/// Projects immutable player-owned Engineering state: ship-level power totals and typed capability, one row per
/// actual installation, the active repair, and the Core-owned action list.
/// </summary>
/// <remarks>
/// <see cref="Systems"/> lists only installations the player ship actually has, in canonical common order; an
/// absent kind has no row and no action, never a zero placeholder. <see cref="Actions"/> is ordered Balance,
/// Prioritize per consumer in common order, BeginRepair per repairable installation in authored repair-action
/// order, then ReturnToCommand. Presentation iterates both lists as given and must not re-sort or re-filter them.
/// </remarks>
public sealed record EngineeringProjection(
    PowerUnits NominalGeneration,
    PowerUnits AvailablePower,
    PowerUnits AllocatedPower,
    PowerUnits Reserve,
    DistanceKilometers EffectivePassiveSensorRange,
    SpeedKilometersPerSecond EffectiveMaximumTacticalSpeed,
    IReadOnlyList<InstalledSystemProjection> Systems,
    SystemRepairProjection? ActiveRepair,
    IReadOnlyList<EngineeringActionProjection> Actions
);
