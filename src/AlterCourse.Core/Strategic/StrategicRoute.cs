
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Strategic;

/// <summary>Defines a bidirectionally traversable connection between two locations.</summary>
public sealed record StrategicRoute
{
    /// <summary>Initializes a strategic connection with an aligned positive duration.</summary>
    public StrategicRoute(LocationId origin, LocationId destination, SimulationDuration duration)
    {
        if (string.IsNullOrWhiteSpace(origin.Value))
        {
            throw new ArgumentException("Route origin requires an initialized identity.", nameof(origin));
        }

        if (string.IsNullOrWhiteSpace(destination.Value))
        {
            throw new ArgumentException(
                "Route destination requires an initialized identity.",
                nameof(destination)
            );
        }

        if (origin == destination)
        {
            throw new ArgumentException("A route must connect distinct locations.", nameof(destination));
        }

        if (duration.Milliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Route duration must be positive.");
        }

        if (duration.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            throw new ArgumentException("Route duration must align to the fixed simulation step.", nameof(duration));
        }

        Origin = origin;
        Destination = destination;
        Duration = duration;
    }

    /// <summary>Gets one endpoint.</summary>
    public LocationId Origin { get; }

    /// <summary>Gets the other endpoint.</summary>
    public LocationId Destination { get; }

    /// <summary>Gets the scheduled traversal duration.</summary>
    public SimulationDuration Duration { get; }

    internal bool Connects(LocationId left, LocationId right) =>
        (Origin == left && Destination == right) || (Origin == right && Destination == left);
}
