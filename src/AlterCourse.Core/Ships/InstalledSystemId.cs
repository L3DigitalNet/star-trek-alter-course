namespace AlterCourse.Core.Ships;

/// <summary>
/// Identifies one installed system within a single ship. The value is ship-local: the same value on two ships
/// names two unrelated installations, so cross-ship work must pair it with the ship in a
/// <see cref="ShipSystemAddress"/>.
/// </summary>
public readonly record struct InstalledSystemId
{
    /// <summary>
    /// Gets the largest valid identity. It is one below <see cref="long.MaxValue"/> so that an allocator which has
    /// issued every identity can still represent its exhausted continuation as <see cref="long.MaxValue"/>.
    /// </summary>
    public const long MaximumValue = long.MaxValue - 1;

    /// <summary>Initializes an installed-system identity.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is outside one through <see cref="MaximumValue"/>.</exception>
    public InstalledSystemId(long value)
    {
        if (value is < 1 or > MaximumValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Installed system identity must be from 1 through {MaximumValue}."
            );
        }

        Value = value;
    }

    /// <summary>Gets the persisted identity value.</summary>
    public long Value { get; }
}
