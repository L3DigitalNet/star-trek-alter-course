namespace AlterCourse.Core.Player;

/// <summary>Identifies actions currently suggested by the player-known simulation slice.</summary>
public enum PlayerAction
{
    /// <summary>Request connected strategic travel.</summary>
    Travel = 1,

    /// <summary>Change local tactical heading and speed.</summary>
    SetTacticalCourse = 2,

    /// <summary>Advance authoritative simulation time.</summary>
    AdvanceTime = 3,

    /// <summary>Actively identify a current sensor contact.</summary>
    ActiveSensorScan = 4,
}
