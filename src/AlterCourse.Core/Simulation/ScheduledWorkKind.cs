namespace AlterCourse.Core.Simulation;

/// <summary>Identifies the currently supported data-only scheduled consequences.</summary>
internal enum ScheduledWorkKind
{
    /// <summary>Completes travel at a destination.</summary>
    TravelArrival = 1,

    /// <summary>Completes repair of a ship sensor system.</summary>
    SensorRepairCompletion = 2,

    /// <summary>Wakes an active order at its next decision boundary.</summary>
    OrderWake = 3,

    /// <summary>Transitions one observer-local stale contact to lost.</summary>
    SensorContactLoss = 4,

    /// <summary>Completes one ship-owned active sensor scan.</summary>
    ActiveSensorScanCompletion = 5,

    /// <summary>Wakes one ship for an autonomous contact decision.</summary>
    ShipContactDecisionWake = 6,
}
