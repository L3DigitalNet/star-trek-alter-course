using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Player;

/// <summary>
/// Projects the common facts of one player-ship installation. <see cref="NominalDemand"/>,
/// <see cref="Allocation"/>, and <see cref="Capability"/> are null together exactly when the installation is not a
/// power consumer; <see cref="FullRepairDuration"/> is null exactly when its definition is not repairable.
/// </summary>
public sealed record InstalledSystemProjection(
    InstalledSystemId Id,
    ShipSystemKind Kind,
    SystemDefinitionId DefinitionId,
    string ComponentLabel,
    SystemCondition Condition,
    PowerUnits? NominalDemand,
    PowerUnits? Allocation,
    double? Capability,
    SimulationDuration? FullRepairDuration
);
