namespace AlterCourse.Core.Quantities;

/// <summary>Represents a finite nonnegative physical speed in kilometers per second.</summary>
public readonly record struct SpeedKilometersPerSecond
{
    /// <summary>Initializes a speed in kilometers per second.</summary>
    /// <param name="value">The finite nonnegative speed.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative or nonfinite.
    /// </exception>
    public SpeedKilometersPerSecond(double value)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Speed must be finite and nonnegative.");
        }

        Value = value == 0 ? 0 : value;
    }

    /// <summary>Gets the speed in kilometers per second.</summary>
    public double Value { get; }
}
