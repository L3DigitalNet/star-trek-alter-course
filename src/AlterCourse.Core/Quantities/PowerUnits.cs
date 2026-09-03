namespace AlterCourse.Core.Quantities;

/// <summary>Represents a bounded nonnegative integral amount of abstract ship power.</summary>
public readonly record struct PowerUnits : IComparable<PowerUnits>
{
    /// <summary>Gets the largest representable power amount.</summary>
    public const int MaximumValue = 1_000_000;

    /// <summary>Initializes a bounded power amount.</summary>
    public PowerUnits(int value)
    {
        if (value is < 0 or > MaximumValue)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                $"Power must be from zero through {MaximumValue}."
            );
        }

        Value = value;
    }

    /// <summary>Gets the abstract whole-unit amount.</summary>
    public int Value { get; }

    /// <inheritdoc />
    public int CompareTo(PowerUnits other) => Value.CompareTo(other.Value);

    /// <summary>Adds two bounded power amounts with checked arithmetic.</summary>
    public static PowerUnits operator +(PowerUnits left, PowerUnits right) => new(checked(left.Value + right.Value));

    /// <summary>Determines whether one amount is less than another.</summary>
    public static bool operator <(PowerUnits left, PowerUnits right) => left.Value < right.Value;

    /// <summary>Determines whether one amount is no greater than another.</summary>
    public static bool operator <=(PowerUnits left, PowerUnits right) => left.Value <= right.Value;

    /// <summary>Determines whether one amount is greater than another.</summary>
    public static bool operator >(PowerUnits left, PowerUnits right) => left.Value > right.Value;

    /// <summary>Determines whether one amount is no less than another.</summary>
    public static bool operator >=(PowerUnits left, PowerUnits right) => left.Value >= right.Value;
}
