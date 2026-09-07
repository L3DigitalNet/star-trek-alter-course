using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.AI;

/// <summary>Describes one actor-known direct strategic connection and its traversal duration.</summary>
public sealed record FactionKnownRouteSnapshot
{
    /// <summary>Initializes a canonically oriented direct route.</summary>
    public FactionKnownRouteSnapshot(LocationId a, LocationId b, SimulationDuration duration)
    {
        if (string.IsNullOrWhiteSpace(a.Value))
        {
            throw new ArgumentException("A known route requires an initialized first endpoint.", nameof(a));
        }

        if (string.IsNullOrWhiteSpace(b.Value))
        {
            throw new ArgumentException("A known route requires an initialized second endpoint.", nameof(b));
        }

        if (a == b)
        {
            throw new ArgumentException("A known route must connect distinct locations.", nameof(b));
        }

        if (duration.Milliseconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(duration), "Known route duration must be positive.");
        }

        if (duration.Milliseconds % SimulationFixedStep.Duration.Milliseconds != 0)
        {
            throw new ArgumentException(
                "Known route duration must align to the fixed simulation step.",
                nameof(duration)
            );
        }

        // Routes are bidirectional domain facts, so canonical endpoints make equivalent snapshots value-equal.
        (A, B) = string.CompareOrdinal(a.Value, b.Value) <= 0 ? (a, b) : (b, a);
        Duration = duration;
    }

    /// <summary>Gets the lexically first endpoint.</summary>
    public LocationId A { get; }

    /// <summary>Gets the lexically second endpoint.</summary>
    public LocationId B { get; }

    /// <summary>Gets the direct traversal duration.</summary>
    public SimulationDuration Duration { get; }

    internal bool Connects(LocationId left, LocationId right) => (A == left && B == right) || (A == right && B == left);
}
