namespace AlterCourse.Core.Gameplay;

/// <summary>Describes the validated result of an active sensor scan request.</summary>
public enum ActiveSensorScanOutcome
{
    /// <summary>The observer has no retained contact with the supplied local identity.</summary>
    ContactNotFound = 1,

    /// <summary>The retained contact is not currently observable.</summary>
    ContactNotCurrent = 2,

    /// <summary>The contact's identity is already known.</summary>
    AlreadyIdentified = 3,

    /// <summary>The observer's sensors cannot perform a scan.</summary>
    SensorsUnavailable = 4,

    /// <summary>The observer already has an active scan.</summary>
    ScanAlreadyActive = 5,

    /// <summary>The scan was scheduled.</summary>
    Accepted = 6,
}
