using AlterCourse.Core.Identity;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Sensors;

/// <summary>Stores one contact-targeted active scan and its exact scheduled completion.</summary>
internal sealed record ActiveSensorScanState(
    SensorContactId TargetContactId,
    InstalledSystemId Sensor,
    SimulationTime StartedAt,
    SimulationTime ExpectedCompletion,
    ScheduledWorkId ScheduledCompletionId
);
