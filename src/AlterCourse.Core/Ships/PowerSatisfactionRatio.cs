namespace AlterCourse.Core.Ships;

/// <summary>Represents the finite fraction of an installed consumer's power demand that is satisfied.</summary>
public readonly record struct PowerSatisfactionRatio
{
    /// <summary>Initializes a satisfaction fraction from zero through one.</summary>
    public PowerSatisfactionRatio(double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Power satisfaction must be finite and normalized."
            );
        }

        Value = value == 0 ? 0 : value;
    }

    /// <summary>Gets the satisfied fraction of nominal power demand.</summary>
    public double Value { get; }
}
