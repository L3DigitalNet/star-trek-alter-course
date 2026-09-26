using AlterCourse.Core.Quantities;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Player;

/// <summary>Projects only own weapon timing and observer-local target legality computed by Core.</summary>
public sealed record CombatProjection(
    DistanceKilometers? WeaponRange,
    SimulationDuration? Cooldown,
    SimulationTime NextDirectedEnergyReadyAt,
    SimulationDuration RemainingCooldown,
    IReadOnlyList<CombatTargetProjection> Targets
);
