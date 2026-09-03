namespace AlterCourse.Core.Sensors;

/// <summary>Describes whether an observer's retained contact is currently observable.</summary>
public enum SensorContactStatus
{
    /// <summary>The target is currently observable.</summary>
    Current = 1,

    /// <summary>The target is temporarily unobserved and awaiting exact loss work.</summary>
    Stale = 2,

    /// <summary>The target is retained knowledge but is no longer an active contact.</summary>
    Lost = 3,
}
