using AlterCourse.Core.Content;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Persistence;

/// <summary>
/// The explicit, version-qualified map from a V9 ship's fixed-field systems to installed systems.
/// </summary>
/// <remarks>
/// <para>
/// PERMANENT HISTORICAL BOUNDARY. The map is a literal keyed by (historical ship definition, kind). It is not
/// derived from the current <c>pathfinder.json</c> loadout, its order, its labels, or a first-match search, even
/// though its values coincide with that content today; a later default-loadout change must never re-map old saves.
/// </para>
/// <para>
/// Each row pins the semantics descriptor of the definition V9 play was simulated under. A current catalog whose
/// definition describes differently is refused rather than silently adopted.
/// </para>
/// </remarks>
internal static class HistoricalShipSystemsV9
{
    /// <summary>The continuation every migrated V9 ship carries (installed ids 1–5 were issued).</summary>
    internal const long NextInstalledSystemId = 6;

    private const string PathfinderShip = "pathfinder";

    private static readonly Row[] Rows =
    [
        new(
            "power-generation",
            "pathfinder.power-generation",
            1,
            "sd1;kind=power-generation;condition=true;order=100;power=none;repair=none;outputPu=120"
        ),
        new(
            "sensors",
            "pathfinder.sensors",
            2,
            "sd1;kind=sensors;condition=true;order=200;power=70;repair=8000;passiveRangeKm=30;scanMs=2000"
        ),
        new(
            "impulse-propulsion",
            "pathfinder.impulse-propulsion",
            3,
            "sd1;kind=impulse-propulsion;condition=true;order=300;power=50;repair=6000;maxSpeedKmS=10"
        ),
        new("shields", "pathfinder.shields", 4, "sd1;kind=shields;condition=true;order=400;power=40;repair=8000"),
        new(
            "directed-energy-weapons",
            "pathfinder.directed-energy-weapons",
            5,
            "sd1;kind=directed-energy-weapons;condition=true;order=500;power=30;repair=6000;rangeKm=20;"
                + "baseDamage=0.25;cooldownMs=2000"
        ),
    ];

    /// <summary>Gets the installed identity a V9 kind maps to for a historical ship definition.</summary>
    internal static InstalledSystemId InstalledIdFor(string shipDefinitionId, string kind) =>
        new(RowFor(shipDefinitionId, kind).InstalledSystemId);

    /// <summary>
    /// Resolves the current catalog definition for a V9 kind and verifies it still describes the historical semantics.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The ship definition or kind has no V9 mapping.</exception>
    /// <exception cref="InvalidOperationException">The current catalog lacks or changed the mapped definition.</exception>
    internal static SystemDefinition ResolveDefinition(
        string shipDefinitionId,
        string kind,
        SystemDefinitionCatalog catalog
    )
    {
        Row row = RowFor(shipDefinitionId, kind);
        if (!catalog.TryGet(new SystemDefinitionId(row.DefinitionId), out SystemDefinition? definition))
        {
            throw new InvalidOperationException(
                $"V9 ship definition '{shipDefinitionId}' maps '{kind}' to system definition '{row.DefinitionId}', "
                    + "which the current content does not provide."
            );
        }

        string current = SystemDefinitionSemantics.Describe(definition);
        if (!string.Equals(current, row.ExpectedSemantics, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"System definition '{row.DefinitionId}' describes '{current}', but V9 saves require "
                    + $"'{row.ExpectedSemantics}'."
            );
        }

        return definition;
    }

    private static Row RowFor(string shipDefinitionId, string kind) =>
        string.Equals(shipDefinitionId, PathfinderShip, StringComparison.Ordinal)
            ? Rows.SingleOrDefault(row => string.Equals(row.Kind, kind, StringComparison.Ordinal))
                ?? throw new KeyNotFoundException($"V9 system kind '{kind}' has no installed-system mapping.")
            : throw new KeyNotFoundException(
                $"V9 ship definition '{shipDefinitionId}' has no installed-system mapping; load it with a build that "
                    + "supports it or start a new game."
            );

    private sealed record Row(string Kind, string DefinitionId, long InstalledSystemId, string ExpectedSemantics);
}
