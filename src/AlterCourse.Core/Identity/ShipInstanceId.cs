namespace AlterCourse.Core.Identity;

/// <summary>Identifies one deterministic ship instance.</summary>
public readonly record struct ShipInstanceId
{
    /// <summary>Initializes a ship instance identity.</summary>
    /// <param name="value">The positive persisted identity value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public ShipInstanceId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the persisted identity value.</summary>
    public long Value { get; }
}
