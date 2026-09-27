namespace AlterCourse.Core.Ships;

/// <summary>Represents an installed system's finite effective capability from zero through nominal capability.</summary>
public readonly record struct SystemCapability
{
    /// <summary>Initializes an effective capability fraction from zero through one.</summary>
    public SystemCapability(double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "System capability must be finite and normalized."
            );
        }

        Value = value == 0 ? 0 : value;
    }

    /// <summary>Gets the effective fraction of nominal capability.</summary>
    public double Value { get; }
}
