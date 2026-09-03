using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Player;

/// <summary>Projects player-owned repair identity and analytical progress.</summary>
public sealed record SystemRepairProjection(
    ShipSystemId TargetSystem,
    double Progress,
    SimulationTime ExpectedCompletion
);
