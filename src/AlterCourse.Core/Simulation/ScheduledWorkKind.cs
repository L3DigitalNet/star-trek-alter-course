namespace AlterCourse.Core.Simulation;

/// <summary>Identifies the currently supported data-only scheduled consequences.</summary>
public enum ScheduledWorkKind
{
    /// <summary>Completes travel at a destination.</summary>
    TravelArrival = 1,

    /// <summary>Completes repair of a ship sensor system.</summary>
    SensorRepairCompletion = 2,
}
