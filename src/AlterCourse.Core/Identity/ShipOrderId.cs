namespace AlterCourse.Core.Identity;

/// <summary>Identifies one deterministic ship order.</summary>
public readonly record struct ShipOrderId
{
    /// <summary>Initializes a ship-order identity.</summary>
    /// <param name="value">The positive persisted identity value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public ShipOrderId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the persisted identity value.</summary>
    public long Value { get; }
}
