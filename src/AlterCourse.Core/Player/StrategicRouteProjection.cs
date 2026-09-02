
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Player;

/// <summary>Projects one player-known strategic connection.</summary>
public sealed record StrategicRouteProjection
{
    internal StrategicRouteProjection(
        LocationId origin,
        LocationId destination,
        SimulationDuration duration
    ) => (Origin, Destination, Duration) = (origin, destination, duration);

    /// <summary>Gets one route endpoint.</summary>
    public LocationId Origin { get; }

    /// <summary>Gets the other route endpoint.</summary>
    public LocationId Destination { get; }

    /// <summary>Gets traversal duration.</summary>
    public SimulationDuration Duration { get; }
}
