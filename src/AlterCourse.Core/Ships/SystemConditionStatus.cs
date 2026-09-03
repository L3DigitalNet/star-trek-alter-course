namespace AlterCourse.Core.Ships;

/// <summary>Classifies a bounded system condition for presentation.</summary>
public enum SystemConditionStatus
{
    /// <summary>The system supplies no capability.</summary>
    Offline = 1,

    /// <summary>The system supplies partial capability.</summary>
    Degraded = 2,

    /// <summary>The system is at its authored condition.</summary>
    Nominal = 3,
}
