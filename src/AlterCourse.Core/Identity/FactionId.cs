namespace AlterCourse.Core.Identity;

/// <summary>Identifies one persistent faction.</summary>
public readonly record struct FactionId
{
    /// <summary>Initializes a faction identity.</summary>
    /// <param name="value">The positive persisted identity value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public FactionId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the persisted identity value.</summary>
    public long Value { get; }
}
