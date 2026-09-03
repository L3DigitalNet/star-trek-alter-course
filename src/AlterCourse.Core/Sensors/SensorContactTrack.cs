using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Sensors;

/// <summary>Stores authoritative observer-local knowledge and its internal target correlation.</summary>
internal sealed record SensorContactTrack(
    SensorContactId Id,
    ShipInstanceId TargetShipId,
    TacticalPosition LastObservedPosition,
    SimulationTime LastObservedAt,
    SensorContactStatus Status,
    SensorContactIdentification Identification,
    string? KnownVesselDisplayName = null,
    string? KnownDesignDisplayName = null,
    ScheduledWorkId? LossWorkId = null,
    SimulationTime? LossDueTime = null
)
{
    internal SensorContactSnapshot ToActorSafeSnapshot() =>
        new(
            Id,
            LastObservedPosition,
            LastObservedAt,
            Status,
            Identification,
            KnownVesselDisplayName,
            KnownDesignDisplayName
        );
}
