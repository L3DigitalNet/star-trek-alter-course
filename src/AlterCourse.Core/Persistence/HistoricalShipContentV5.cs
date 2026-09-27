namespace AlterCourse.Core.Persistence;

/// <summary>
/// Frozen, version-qualified ship tuning that V1–V9 save documents were validated and migrated against.
/// </summary>
/// <remarks>
/// <para>
/// PERMANENT HISTORICAL BOUNDARY. Historical validators and migrations read this table, never the current ship
/// or system catalog: reinterpreting an old save under today's content (for example filling V4→V5 allocations from
/// a changed demand) would silently rewrite history. Values are the <c>pathfinder</c> ship-definition V1–V5 tuning,
/// unchanged across that history (generation 120, demands 70/50/40/30, repair 8000/6000/8000/6000, speed 10 km/s,
/// passive range 30 km, scan 2000 ms, weapon 20 km / 0.25 / 2000 ms).
/// </para>
/// <para>
/// Never add rows or edit values to make a test pass: an unknown historical ship definition must fail, not be
/// adopted. Only the compatibility check in the V9→V10 migration consults current content.
/// </para>
/// </remarks>
internal sealed record HistoricalShipContentV5(
    string DefinitionId,
    string DesignDisplayName,
    double MaximumTacticalSpeedKilometersPerSecond,
    int NominalGenerationPowerUnits,
    int NominalSensorDemandPowerUnits,
    int NominalImpulseDemandPowerUnits,
    int NominalShieldDemandPowerUnits,
    int NominalDirectedEnergyDemandPowerUnits,
    long SensorRepairDurationMilliseconds,
    long ImpulseRepairDurationMilliseconds,
    long ShieldRepairDurationMilliseconds,
    long DirectedEnergyRepairDurationMilliseconds
)
{
    private static readonly HistoricalShipContentV5 Pathfinder = new(
        "pathfinder",
        "Pathfinder class",
        10,
        120,
        70,
        50,
        40,
        30,
        8000,
        6000,
        8000,
        6000
    );

    /// <summary>Gets the frozen tuning for one historical ship definition, or throws for an unknown one.</summary>
    /// <exception cref="SaveContentIncompatibleException">
    /// The historical ship definition has no frozen tuning; the load fails as incompatible content, not corrupt data.
    /// </exception>
    internal static HistoricalShipContentV5 GetRequired(string definitionId) =>
        string.Equals(definitionId, Pathfinder.DefinitionId, StringComparison.Ordinal)
            ? Pathfinder
            : throw new SaveContentIncompatibleException(
                $"uses historical ship definition '{definitionId}', which has no frozen V1–V9 tuning; load it with a "
                    + "build that supports it or start a new game."
            );

    /// <summary>Gets the historical repair duration for a repairable kind string, or throws.</summary>
    internal long RepairDurationMillisecondsFor(string kind) =>
        kind switch
        {
            "sensors" => SensorRepairDurationMilliseconds,
            "impulse-propulsion" => ImpulseRepairDurationMilliseconds,
            "shields" => ShieldRepairDurationMilliseconds,
            "directed-energy-weapons" => DirectedEnergyRepairDurationMilliseconds,
            _ => throw new ArgumentException($"Historical system '{kind}' is not repairable.", nameof(kind)),
        };
}
