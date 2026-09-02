
using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Strategic;

/// <summary>Correlates persistent active travel with its scheduled arrival consequence.</summary>
public sealed record TravelState
{
    internal TravelState(
        LocationId origin,
        LocationId destination,
        SimulationTime departure,
        SimulationTime expectedArrival,
        ScheduledWorkId scheduledArrivalId
    )
    {
        if (string.IsNullOrWhiteSpace(origin.Value))
        {
            throw new ArgumentException(
                "Travel origin requires an initialized identity.",
                nameof(origin)
            );
        }

        if (string.IsNullOrWhiteSpace(destination.Value))
        {
            throw new ArgumentException(
                "Travel destination requires an initialized identity.",
                nameof(destination)
            );
        }

        if (origin == destination)
        {
            throw new ArgumentException("Active travel requires distinct endpoints.", nameof(destination));
        }

        if (expectedArrival.Milliseconds <= departure.Milliseconds)
        {
            throw new ArgumentException("Arrival must follow departure.", nameof(expectedArrival));
        }

        if (scheduledArrivalId.Value <= 0)
        {
            throw new ArgumentException("Travel requires initialized scheduled work.", nameof(scheduledArrivalId));
        }

        Origin = origin;
        Destination = destination;
        Departure = departure;
        ExpectedArrival = expectedArrival;
        ScheduledArrivalId = scheduledArrivalId;
        IsActive = true;
    }

    /// <summary>Gets the departed location.</summary>
    public LocationId Origin { get; }

    /// <summary>Gets the pending destination.</summary>
    public LocationId Destination { get; }

    /// <summary>Gets departure simulation time.</summary>
    public SimulationTime Departure { get; }

    /// <summary>Gets scheduled arrival simulation time.</summary>
    public SimulationTime ExpectedArrival { get; }

    /// <summary>Gets the correlated scheduled arrival identity.</summary>
    public ScheduledWorkId ScheduledArrivalId { get; }

    /// <summary>Gets the active status represented by this state.</summary>
    public bool IsActive { get; }
}
