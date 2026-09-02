using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

/// <summary>Declares an active repair whose completion is derived from immutable ship content.</summary>
public sealed record SensorRepairStart(
    SensorIntegrity StartingIntegrity,
    SensorIntegrity TargetIntegrity,
    SimulationTime StartedAt
);
