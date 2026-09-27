using System.Diagnostics.CodeAnalysis;
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
/// Each row also pins the semantics descriptor of the definition V9 play was simulated under, for the V9→V10
/// compatibility check.
/// </para>
/// </remarks>
internal static class HistoricalShipSystemsV9
{
    /// <summary>The continuation every migrated V9 ship carries (installed ids 1–5 were issued).</summary>
    internal const long NextInstalledSystemId = 6;

    private const string PathfinderShip = "pathfinder";

    private static readonly IReadOnlyList<Row> Rows =
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

    /// <summary>
    /// Gets every row for a historical ship definition, or false when it has no V9 mapping (the caller fails the
    /// load as incompatible content, naming the ship).
    /// </summary>
    internal static bool TryGetRows(string shipDefinitionId, [MaybeNullWhen(false)] out IReadOnlyList<Row> rows)
    {
        rows = string.Equals(shipDefinitionId, PathfinderShip, StringComparison.Ordinal) ? Rows : null;
        return rows is not null;
    }

    /// <summary>Gets the installed identity a V9 kind maps to for a historical ship definition.</summary>
    internal static InstalledSystemId InstalledIdFor(string shipDefinitionId, string kind) =>
        new(RowFor(shipDefinitionId, kind).InstalledSystemId);

    /// <summary>Gets the system definition identity a V9 kind maps to for a historical ship definition.</summary>
    internal static string DefinitionIdFor(string shipDefinitionId, string kind) =>
        RowFor(shipDefinitionId, kind).DefinitionId;

    /// <summary>Gets the pinned semantics descriptor of the definition a V9 kind maps to.</summary>
    /// <remarks>
    /// The V9→V10 migration compares the supplied catalog's descriptor for the mapped definition with this value and
    /// fails closed on any difference, so a V9 world is never reinterpreted under changed tuning.
    /// </remarks>
    internal static string ExpectedSemanticsFor(string shipDefinitionId, string kind) =>
        RowFor(shipDefinitionId, kind).ExpectedSemantics;

    // Callers resolve the ship through TryGetRows first, so an unknown ship here is a programming error; an unknown
    // kind cannot pass V9 validation either (the kind strings are the V9 fixed fields and the parsed repair target).
    private static Row RowFor(string shipDefinitionId, string kind) =>
        TryGetRows(shipDefinitionId, out IReadOnlyList<Row>? rows)
            ? rows.SingleOrDefault(row => string.Equals(row.Kind, kind, StringComparison.Ordinal))
                ?? throw new InvalidOperationException($"V9 system kind '{kind}' has no installed-system mapping.")
            : throw new SaveContentIncompatibleException(
                $"uses V9 ship definition '{shipDefinitionId}', which has no installed-system mapping; load it with a "
                    + "build that supports it or start a new game."
            );

    /// <summary>One frozen mapping row: historical kind → target definition, installed identity, and semantics.</summary>
    internal sealed record Row(string Kind, string DefinitionId, long InstalledSystemId, string ExpectedSemantics);
}
