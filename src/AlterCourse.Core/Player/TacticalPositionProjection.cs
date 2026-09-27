using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Player;

/// <summary>Projects continuous tactical coordinates in kilometers.</summary>
public sealed record TacticalPositionProjection
{
    internal TacticalPositionProjection(TacticalPosition position) =>
        (XKilometers, YKilometers) = (position.XKilometers, position.YKilometers);

    /// <summary>Gets east-positive kilometers.</summary>
    public double XKilometers { get; }

    /// <summary>Gets north-positive kilometers.</summary>
    public double YKilometers { get; }
}
