
using System.Runtime.InteropServices;

namespace AlterCourse.Core.Strategic;

/// <summary>Represents a finite continuous position on the strategic map.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct StrategicMapPosition
{
    /// <summary>Initializes an arbitrary map position.</summary>
    /// <param name="x">The finite horizontal coordinate.</param>
    /// <param name="y">The finite vertical coordinate.</param>
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

    /// <summary>Gets the horizontal coordinate.</summary>
    public double X { get; }

    /// <summary>Gets the vertical coordinate.</summary>
    public double Y { get; }
}
