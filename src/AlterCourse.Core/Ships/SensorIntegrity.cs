
namespace AlterCourse.Core.Ships;

/// <summary>Represents bounded ship-system integrity.</summary>
public readonly record struct SensorIntegrity
{
    /// <summary>Initializes integrity on the inclusive unit interval.</summary>
    public SensorIntegrity(double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "Integrity must be from zero through one.");
        }

        Value = value == 0 ? 0 : value;
    }

    /// <summary>Gets integrity on the inclusive unit interval.</summary>
    public double Value { get; }
}
