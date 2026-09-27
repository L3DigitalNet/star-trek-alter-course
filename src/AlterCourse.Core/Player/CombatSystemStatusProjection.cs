using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Player;

/// <summary>Projects the player's own installed shield or weapon status for the Combat telemetry.</summary>
public sealed record CombatSystemStatusProjection(
    InstalledSystemId Id,
    SystemCondition Condition,
    PowerUnits Allocation,
    double Capability
);
