using System.Runtime.InteropServices;

namespace AlterCourse.Core.Strategic;

/// <summary>
/// Represents a finite continuous position in unitless strategic layout space, never pixels or
/// tactical distance.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct StrategicMapPosition
{
    /// <summary>Initializes an arbitrary map position.</summary>
    /// <param name="x">The finite unitless horizontal layout coordinate.</param>
    /// <param name="y">The finite unitless vertical layout coordinate.</param>
    public StrategicMapPosition(double x, double y)
    {
        if (!double.IsFinite(x))
        {
            throw new ArgumentOutOfRangeException(nameof(x), x, "Coordinate must be finite.");
        }

        if (!double.IsFinite(y))
        {
            throw new ArgumentOutOfRangeException(nameof(y), y, "Coordinate must be finite.");
        }

        X = x == 0 ? 0 : x;
        Y = y == 0 ? 0 : y;
    }

    /// <summary>Gets the unitless horizontal layout coordinate.</summary>
    public double X { get; }

    /// <summary>Gets the unitless vertical layout coordinate.</summary>
    public double Y { get; }
}
