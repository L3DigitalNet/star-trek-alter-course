using System.Collections.Immutable;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

/// <summary>Declares a bounded cyclic patrol and its active next-waypoint progress.</summary>
public sealed record PatrolRouteOrderStart : ShipOrderStart
{
    /// <summary>Initializes immutable patrol intent without runtime identity or physical progress.</summary>
    public PatrolRouteOrderStart(IEnumerable<LocationId> waypoints, int nextWaypointIndex)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        ImmutableArray<LocationId> materialized = [.. waypoints.Take(PatrolRouteOrder.MaximumWaypointCount + 1)];
        if (materialized.Length is < 2 or > PatrolRouteOrder.MaximumWaypointCount)
        {
            throw new ArgumentException(
                $"A patrol route requires between 2 and {PatrolRouteOrder.MaximumWaypointCount} waypoints.",
                nameof(waypoints)
            );
        }

        for (int index = 0; index < materialized.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(materialized[index].Value))
            {
                throw new ArgumentException("A patrol route requires initialized waypoints.", nameof(waypoints));
            }

            if (materialized[index] == materialized[(index + 1) % materialized.Length])
            {
                throw new ArgumentException(
                    "Adjacent waypoints in a cyclic patrol route must be distinct.",
                    nameof(waypoints)
                );
            }
        }

        if ((uint)nextWaypointIndex >= (uint)materialized.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextWaypointIndex),
                nextWaypointIndex,
                "The next waypoint index must identify a patrol waypoint."
            );
        }

        Waypoints = materialized;
        NextWaypointIndex = nextWaypointIndex;
    }

    /// <summary>Gets the immutable explicit cyclic waypoint sequence.</summary>
    public ImmutableArray<LocationId> Waypoints { get; }

    /// <summary>Gets the zero-based waypoint the ship must visit next.</summary>
    public int NextWaypointIndex { get; }
}
