using System.Collections.Immutable;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Orders;

/// <summary>Directs a ship around a bounded, explicit cyclic sequence of strategic waypoints.</summary>
public sealed record PatrolRouteOrder : ShipOrder
{
    /// <summary>
    /// Caps prototype patrol state so order validation and persistence remain predictably bounded.
    /// </summary>
    public const int MaximumWaypointCount = 16;

    /// <summary>Initializes a patrol order with immutable waypoint state and explicit progress.</summary>
    /// <param name="id">The stable order identity.</param>
    /// <param name="waypoints">Two to sixteen initialized cyclic waypoints.</param>
    /// <param name="nextWaypointIndex">The zero-based waypoint the ship must visit next.</param>
    /// <exception cref="ArgumentNullException"><paramref name="waypoints"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// A waypoint is uninitialized, adjacent cyclic waypoints are equal, or the waypoint count is invalid.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="nextWaypointIndex"/> does not identify a waypoint.
    /// </exception>
    public PatrolRouteOrder(ShipOrderId id, IEnumerable<LocationId> waypoints, int nextWaypointIndex)
        : base(id, ShipOrderKind.PatrolRoute)
    {
        ArgumentNullException.ThrowIfNull(waypoints);
        ImmutableArray<LocationId> waypointArray = [.. waypoints.Take(MaximumWaypointCount + 1)];

        if (waypointArray.Length is < 2 or > MaximumWaypointCount)
        {
            throw new ArgumentException(
                $"A patrol route requires between 2 and {MaximumWaypointCount} waypoints.",
                nameof(waypoints)
            );
        }

        for (int index = 0; index < waypointArray.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(waypointArray[index].Value))
            {
                throw new ArgumentException("A patrol route requires initialized waypoints.", nameof(waypoints));
            }

            LocationId following = waypointArray[(index + 1) % waypointArray.Length];
            if (waypointArray[index] == following)
            {
                throw new ArgumentException(
                    "Adjacent waypoints in a cyclic patrol route must be distinct.",
                    nameof(waypoints)
                );
            }
        }

        if ((uint)nextWaypointIndex >= (uint)waypointArray.Length)
        {
            throw new ArgumentOutOfRangeException(
                nameof(nextWaypointIndex),
                nextWaypointIndex,
                "The next waypoint index must identify a patrol waypoint."
            );
        }

        Waypoints = waypointArray;
        NextWaypointIndex = nextWaypointIndex;
    }

    /// <summary>Gets the immutable explicit cyclic waypoint sequence.</summary>
    public ImmutableArray<LocationId> Waypoints { get; }

    /// <summary>Gets the zero-based waypoint the ship must visit next.</summary>
    public int NextWaypointIndex { get; }
}
