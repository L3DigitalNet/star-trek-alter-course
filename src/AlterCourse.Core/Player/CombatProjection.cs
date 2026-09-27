using AlterCourse.Core.Quantities;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Player;

/// <summary>Projects only own weapon timing, own combat-system status, and observer-local target legality.</summary>
/// <remarks>
/// Without an installed weapon, <see cref="WeaponRange"/>, <see cref="Cooldown"/>,
/// <see cref="NextDirectedEnergyReadyAt"/>, and <see cref="Weapon"/> are null and the remaining cooldown is zero;
/// without shields, <see cref="Shields"/> is null. Both are the player's own installations resolved by Core.
/// </remarks>
public sealed record CombatProjection(
    DistanceKilometers? WeaponRange,
    SimulationDuration? Cooldown,
    SimulationTime? NextDirectedEnergyReadyAt,
    SimulationDuration RemainingCooldown,
    IReadOnlyList<CombatTargetProjection> Targets,
    CombatSystemStatusProjection? Shields,
    CombatSystemStatusProjection? Weapon
);
