namespace AlterCourse.Core.Player;

/// <summary>Projects continuous tactical position, heading, and speed.</summary>
public sealed record TacticalProjection
{
    internal TacticalProjection(
        TacticalPositionProjection position,
        double headingDegrees,
        double speedKilometersPerSecond
    ) => (Position, HeadingDegrees, SpeedKilometersPerSecond) = (position, headingDegrees, speedKilometersPerSecond);

    /// <summary>Gets tactical position in kilometers.</summary>
    public TacticalPositionProjection Position { get; }

    /// <summary>Gets clockwise heading degrees from north.</summary>
    public double HeadingDegrees { get; }

    /// <summary>Gets tactical speed in kilometers per second.</summary>
    public double SpeedKilometersPerSecond { get; }
}
