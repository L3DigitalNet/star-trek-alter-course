namespace AlterCourse.Core.Player;

/// <summary>Identifies why a projected Engineering action cannot currently be applied.</summary>
public enum EngineeringActionUnavailableReason
{
    /// <summary>Current tactical speed prevents the requested allocation.</summary>
    CurrentSpeedTooHigh = 1,

    /// <summary>Another repair already occupies the ship repair slot.</summary>
    RepairAlreadyActive = 2,

    /// <summary>The target system is already nominal.</summary>
    SystemAlreadyNominal = 3,
}
