using AlterCourse.Core.Identity;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Ships;

/// <summary>Correlates one future defensive wake with knowledge acquired at the accepted shot boundary.</summary>
internal sealed record CombatStimulus(
    SensorContactId ContactId,
    SimulationTime ObservedAt,
    SimulationTime DueTime,
    ScheduledWorkId ScheduledWorkId
);
