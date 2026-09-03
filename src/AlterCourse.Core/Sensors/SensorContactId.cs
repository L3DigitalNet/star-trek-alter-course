using System.Runtime.InteropServices;

namespace AlterCourse.Core.Sensors;

/// <summary>Identifies one contact within a single observer's sensor knowledge.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SensorContactId
{
    /// <summary>Initializes an observer-local contact identity.</summary>
    /// <param name="value">The positive persisted identity value.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="value"/> is not positive.</exception>
    public SensorContactId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the observer-local persisted identity value.</summary>
    public long Value { get; }
}
