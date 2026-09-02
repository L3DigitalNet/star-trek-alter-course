namespace AlterCourse.Core.Player;

/// <summary>Projects strategic map knowledge and the player's current strategic state.</summary>
public sealed record StrategicProjection
{
    internal StrategicProjection(
        IReadOnlyList<StrategicLocationProjection> locations,
        IReadOnlyList<StrategicRouteProjection> routes,
        StrategicLocationProjection? currentLocation,
        TravelProjection? travel
    ) => (Locations, Routes, CurrentLocation, Travel) = (locations, routes, currentLocation, travel);

    /// <summary>Gets a fresh read-only location collection.</summary>
    public IReadOnlyList<StrategicLocationProjection> Locations { get; }

    /// <summary>Gets a fresh read-only route collection.</summary>
    public IReadOnlyList<StrategicRouteProjection> Routes { get; }

    /// <summary>Gets the current location, or null while traveling.</summary>
    public StrategicLocationProjection? CurrentLocation { get; }

    /// <summary>Gets active travel, or null while at a location.</summary>
    public TravelProjection? Travel { get; }
}
