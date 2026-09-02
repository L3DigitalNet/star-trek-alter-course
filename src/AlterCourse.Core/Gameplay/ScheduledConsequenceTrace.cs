using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Gameplay;

internal sealed record ScheduledConsequenceTrace(
    ScheduledWorkId WorkId,
    ShipInstanceId TargetShipId,
    ScheduledWorkKind WorkKind,
    SimulationTime ResolutionTime,
    ShipOrderId? OrderId,
    ShipOrderKind? OrderKind,
    ScheduledConsequenceRule Rule,
    ScheduledConsequenceAction Action,
    bool Completed,
    bool RandomnessUsed
);
