using System.Runtime.InteropServices;

namespace AlterCourse.Core.Identity;

/// <summary>Identifies one admitted historical observation report across its bounded lifecycle.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct ObservationReportId
{
    /// <summary>Initializes an observation-report identity.</summary>
    public ObservationReportId(long value)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(value);
        Value = value;
    }

    /// <summary>Gets the positive persisted identity value.</summary>
    public long Value { get; }
}
