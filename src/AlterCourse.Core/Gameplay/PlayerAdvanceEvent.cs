namespace AlterCourse.Core.Gameplay;

/// <summary>Identifies a player-visible consequence produced by simulation advancement.</summary>
public enum PlayerAdvanceEvent
{
    /// <summary>The player ship completed strategic travel.</summary>
    TravelArrived = 1,

    /// <summary>The player ship completed sensor repair.</summary>
    SensorRepairCompleted = 2,
}
