
using System.Collections.Immutable;

namespace AlterCourse.Core.Strategic;

/// <summary>Owns validated strategic locations and explicit connections.</summary>
public sealed class StrategicMap
{
    /// <summary>Initializes a strategic map with unique locations and routes.</summary>
    public StrategicMap(
        IEnumerable<StrategicLocation> locations,
        IEnumerable<StrategicRoute> routes
    )
    {
        ArgumentNullException.ThrowIfNull(locations);
        ArgumentNullException.ThrowIfNull(routes);
        ImmutableArray<StrategicLocation> locationArray = [.. locations];
        ImmutableArray<StrategicRoute> routeArray = [.. routes];

        if (locationArray.IsDefaultOrEmpty)
        {
            throw new ArgumentException("A strategic map requires at least one location.", nameof(locations));
        }

        if (locationArray.Select(location => location.Id).Distinct().Count() != locationArray.Length)
        {
            throw new ArgumentException("Strategic location identities must be unique.", nameof(locations));
        }

        HashSet<LocationId> locationIds = [.. locationArray.Select(location => location.Id)];
        foreach (StrategicRoute route in routeArray)
        {
            if (!locationIds.Contains(route.Origin) || !locationIds.Contains(route.Destination))
            {
                throw new ArgumentException("Every route endpoint must exist in the map.", nameof(routes));
            }
        }

        for (int left = 0; left < routeArray.Length; left++)
        {
            for (int right = left + 1; right < routeArray.Length; right++)
            {
                if (routeArray[left].Connects(routeArray[right].Origin, routeArray[right].Destination))
                {
                    throw new ArgumentException("A strategic connection may be declared only once.", nameof(routes));
                }
            }
        }

        Locations = locationArray;
        Routes = routeArray;
    }

    /// <summary>Gets the immutable location definitions.</summary>
    public ImmutableArray<StrategicLocation> Locations { get; }

    /// <summary>Gets the immutable explicit connections.</summary>
    public ImmutableArray<StrategicRoute> Routes { get; }

    internal StrategicLocation GetLocation(LocationId id) =>
        Locations.FirstOrDefault(location => location.Id == id)
        ?? throw new ArgumentException("Location does not exist in the strategic map.", nameof(id));

    internal StrategicRoute? FindRoute(LocationId origin, LocationId destination) =>
        Routes.FirstOrDefault(route => route.Connects(origin, destination));
}
