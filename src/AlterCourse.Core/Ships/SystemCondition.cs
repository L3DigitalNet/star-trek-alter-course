namespace AlterCourse.Core.Ships;

/// <summary>Represents a finite ship-system condition on the inclusive unit interval.</summary>
public readonly record struct SystemCondition
{
    /// <summary>Initializes a bounded system condition.</summary>
    public SystemCondition(double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Condition must be from zero through one.");
        }

        Value = value == 0 ? 0 : value;
    }

    /// <summary>Gets the condition on the inclusive unit interval.</summary>
    public double Value { get; }

    /// <summary>Gets the derived presentation status.</summary>
    public SystemConditionStatus Status =>
        Value switch
        {
            0 => SystemConditionStatus.Offline,
            1 => SystemConditionStatus.Nominal,
            _ => SystemConditionStatus.Degraded,
        };
}
