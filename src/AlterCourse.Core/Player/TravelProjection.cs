using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Player;

/// <summary>Projects active strategic travel without exposing mutable aggregate state.</summary>
public sealed record TravelProjection
{
    internal TravelProjection(
        LocationId origin,
        LocationId destination,
        SimulationTime departure,
        SimulationTime expectedArrival,
        bool isActive
    ) =>
        (Origin, Destination, Departure, ExpectedArrival, IsActive) = (
            origin,
            destination,
            departure,
            expectedArrival,
            isActive
        );

    /// <summary>Gets the departed location.</summary>
    public LocationId Origin { get; }

    /// <summary>Gets the pending destination.</summary>
    public LocationId Destination { get; }

    /// <summary>Gets departure time.</summary>
    public SimulationTime Departure { get; }

    /// <summary>Gets expected arrival time.</summary>
    public SimulationTime ExpectedArrival { get; }

    /// <summary>Gets whether travel remains active.</summary>
    public bool IsActive { get; }
}
