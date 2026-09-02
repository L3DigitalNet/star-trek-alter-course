namespace AlterCourse.Core.Identity;

/// <summary>Identifies one deterministic scheduled-work item.</summary>
public readonly record struct ScheduledWorkId
{
    /// <summary>Initializes a scheduled-work identity.</summary>
    /// <param name="value">The positive persisted identity value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public ScheduledWorkId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the persisted identity value.</summary>
    public long Value { get; }
}
