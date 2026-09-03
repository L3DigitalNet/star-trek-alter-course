namespace AlterCourse.Core.Player;

/// <summary>Identifies one player command that Core currently permits for a specific local contact.</summary>
public enum SensorContactAction
{
    /// <summary>Actively identify the current detected contact.</summary>
    ActiveScan = 1,

    /// <summary>Send a bounded hail to the current identified contact.</summary>
    Hail = 2,
}
