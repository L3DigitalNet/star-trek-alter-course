using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

/// <summary>Declares one active repair whose completion derives from immutable ship content.</summary>
public sealed record SystemRepairStart(
    ShipSystemId TargetSystem,
    SystemCondition StartingCondition,
    SystemCondition TargetCondition,
    SimulationTime StartedAt
);
