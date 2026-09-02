
using System.Runtime.InteropServices;

namespace AlterCourse.Core.Tactical;

/// <summary>Represents a signed finite continuous tactical position in kilometers.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TacticalPosition
{
    /// <summary>Initializes a tactical position.</summary>
    public TacticalPosition(double xKilometers, double yKilometers)
    {
        if (!double.IsFinite(xKilometers))
        {
            throw new ArgumentOutOfRangeException(nameof(xKilometers), "Coordinate must be finite.");
        }

        if (!double.IsFinite(yKilometers))
        {
            throw new ArgumentOutOfRangeException(nameof(yKilometers), "Coordinate must be finite.");
        }

        XKilometers = xKilometers == 0 ? 0 : xKilometers;
        YKilometers = yKilometers == 0 ? 0 : yKilometers;
    }

    /// <summary>Gets the east-positive coordinate in kilometers.</summary>
    public double XKilometers { get; }

    /// <summary>Gets the north-positive coordinate in kilometers.</summary>
    public double YKilometers { get; }

    internal TacticalPosition Advance(TacticalMotion motion, double seconds)
    {
        double radians = motion.Heading.Value * Math.PI / 180;
        double distance = motion.Speed.Value * seconds;
        return new TacticalPosition(
            XKilometers + (Math.Sin(radians) * distance),
            YKilometers + (Math.Cos(radians) * distance)
        );
    }
}
