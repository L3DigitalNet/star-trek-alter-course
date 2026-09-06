namespace AlterCourse.Core.Player;

/// <summary>Projects strategic map knowledge and the player's current strategic state.</summary>
public sealed record StrategicProjection
{
    internal StrategicProjection(
        IReadOnlyList<StrategicLocationProjection> locations,
        IReadOnlyList<StrategicRouteProjection> routes,
        StrategicLocationProjection? currentLocation,
        TravelProjection? travel,
        IReadOnlyList<StrategicContactReportProjection> knownContactReports
    ) =>
        (Locations, Routes, CurrentLocation, Travel, KnownContactReports) = (
            locations,
            routes,
            currentLocation,
            travel,
            knownContactReports
        );

    /// <summary>Gets a fresh read-only location collection.</summary>
    public IReadOnlyList<StrategicLocationProjection> Locations { get; }

    /// <summary>Gets a fresh read-only route collection.</summary>
    public IReadOnlyList<StrategicRouteProjection> Routes { get; }

    /// <summary>Gets the current location, or null while traveling.</summary>
    public StrategicLocationProjection? CurrentLocation { get; }

    /// <summary>Gets active travel, or null while at a location.</summary>
    public TravelProjection? Travel { get; }

    /// <summary>Gets retained contact reports, each qualified by the location it was observed in.</summary>
    /// <remarks>
    /// Unlike the tactical contact surface, this collection retains lost contacts: a report is a
    /// record of a past observation, not a claim about the present.
    /// </remarks>
    public IReadOnlyList<StrategicContactReportProjection> KnownContactReports { get; }
}
