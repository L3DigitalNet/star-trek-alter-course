namespace AlterCourse.Core.Quantities;

/// <summary>Represents a finite nonnegative physical distance in kilometers.</summary>
public readonly record struct DistanceKilometers
{
    /// <summary>Initializes a distance in kilometers.</summary>
    /// <param name="value">The finite nonnegative distance.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="value"/> is negative or nonfinite.
    /// </exception>
    public DistanceKilometers(double value)
    {
        if (!double.IsFinite(value) || value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Distance must be finite and nonnegative.");
        }

        Value = value == 0 ? 0 : value;
    }

    /// <summary>Gets the distance in kilometers.</summary>
    public double Value { get; }
}
