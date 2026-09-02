namespace AlterCourse.Core.Quantities;

/// <summary>Represents a finite heading normalized to the interval from zero inclusive to 360 exclusive.</summary>
public readonly record struct HeadingDegrees
{
    private const double FullTurn = 360;

    /// <summary>Initializes and normalizes a heading expressed in degrees.</summary>
    /// <param name="value">The finite heading in degrees.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is nonfinite.</exception>
    public HeadingDegrees(double value)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Heading must be finite.");
        }

        double normalized = value % FullTurn;
        if (normalized < 0)
        {
            normalized += FullTurn;
        }

        Value = normalized == 0 ? 0 : normalized;
    }

    /// <summary>Gets the normalized heading in degrees.</summary>
    public double Value { get; }
}
