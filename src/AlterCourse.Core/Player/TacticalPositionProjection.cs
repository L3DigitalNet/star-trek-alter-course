
namespace AlterCourse.Core.Player;

/// <summary>Projects continuous tactical coordinates in kilometers.</summary>
public sealed record TacticalPositionProjection
{
    internal TacticalPositionProjection(double xKilometers, double yKilometers) =>
        (XKilometers, YKilometers) = (xKilometers, yKilometers);

    /// <summary>Gets east-positive kilometers.</summary>
    public double XKilometers { get; }

    /// <summary>Gets north-positive kilometers.</summary>
    public double YKilometers { get; }
}
