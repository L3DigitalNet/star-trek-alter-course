using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

/// <summary>Declares an in-progress repair of one installation on the declaring ship.</summary>
public sealed record SystemRepairStart(
    InstalledSystemId Target,
    SystemCondition StartingCondition,
    SystemCondition TargetCondition,
    SimulationTime StartedAt
);
