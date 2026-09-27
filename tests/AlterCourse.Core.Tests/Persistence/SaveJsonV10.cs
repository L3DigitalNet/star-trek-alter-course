using System.Text.Json.Nodes;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>
/// Test-only JSON helpers over written V10 saves: installation lookup by installed id, and a fixture writer that
/// re-expresses a V10 document of five-slot <c>pathfinder</c> ships in the frozen V9 wire shape.
/// </summary>
/// <remarks>
/// <para>
/// The V9 writer exists because many historical-migration tests derive their V1–V9 input from a live world ("take a
/// current save, strip the newer members, relabel it"). Production no longer writes V9 — and must not: there is no
/// downgrade path — so the tests produce that historical input here. It is a fixture generator, not a relabeling of
/// V10 as V9: the output carries the V9 wire meaning exactly (the five fixed condition and allocation fields, the
/// repair target as a kind string, one readiness time) and is then loaded through the real V9→V10 migration.
/// </para>
/// <para>
/// The kind↔slot table below is a deliberate, independent copy of the frozen V9 map
/// (<c>HistoricalShipSystemsV9</c>): the tests must fail if production ever re-maps V9 slots, which a shared table
/// would hide. A ship whose installations are not exactly those five slots is refused — such a world has no V9
/// expression, and the test should build a <c>pathfinder</c> world instead.
/// </para>
/// </remarks>
internal static class SaveJsonV10
{
    internal const string V10RulesVersion = "installed-ship-system-substrate-v1";
    internal const string V9RulesVersion = "first-combat-engagement-v1";

    private static readonly (long Id, string Kind, string Definition)[] V9Slots =
    [
        (1, "power-generation", "pathfinder.power-generation"),
        (2, "sensors", "pathfinder.sensors"),
        (3, "impulse-propulsion", "pathfinder.impulse-propulsion"),
        (4, "shields", "pathfinder.shields"),
        (5, "directed-energy-weapons", "pathfinder.directed-energy-weapons"),
    ];

    /// <summary>Gets the installation with the given installed id from a V10 ship node.</summary>
    internal static JsonObject Installation(JsonNode ship, long installedId) =>
        ship["engineering"]!["installedSystems"]!
            .AsArray()
            .Single(system => system!["installedSystemId"]!.GetValue<long>() == installedId)!
            .AsObject();

    /// <summary>Gets an installation's condition from a V10 ship node.</summary>
    internal static double Condition(JsonNode ship, long installedId) =>
        Installation(ship, installedId)["condition"]!.GetValue<double>();

    /// <summary>Rewrites a V10 document of five-slot pathfinder ships, in place, as the equivalent V9 document.</summary>
    internal static void ToV9(JsonObject root)
    {
        if (root["schemaVersion"]!.GetValue<int>() != 10)
            throw new InvalidOperationException("The V9 fixture writer expects a V10 document.");
        root["schemaVersion"] = 9;
        root["simulationRulesVersion"] = V9RulesVersion;
        JsonObject simulation = root["simulation"]!.AsObject();
        simulation.Remove("systemDefinitions");
        simulation.Remove("aimVocabulary");
        JsonArray ships = simulation["ships"]!.AsArray();
        for (int index = 0; index < ships.Count; index++)
        {
            ships[index] = ShipToV9(ships[index]!.AsObject());
        }
    }

    private static JsonObject ShipToV9(JsonObject ship)
    {
        JsonObject engineering = ship["engineering"]!.AsObject();
        JsonArray installed = engineering["installedSystems"]!.AsArray();
        if (
            engineering["nextInstalledSystemId"]!.GetValue<long>() != 6
            || installed.Count != V9Slots.Length
            || !installed
                .Select(system =>
                    (system!["installedSystemId"]!.GetValue<long>(), system["definitionId"]!.GetValue<string>())
                )
                .SequenceEqual(V9Slots.Select(slot => (slot.Id, slot.Definition)))
        )
            throw new InvalidOperationException("Only a five-slot pathfinder loadout has a V9 expression.");

        JsonObject engineeringV9 = EngineeringToV9(installed, engineering["activeRepair"]);

        JsonObject knowledge = ship["sensorKnowledge"]!.AsObject();
        if (knowledge["activeScan"] is JsonObject scan)
        {
            if (scan["sensorInstalledSystemId"]!.GetValue<long>() != 2)
                throw new InvalidOperationException("A V9 scan can only originate from the sensor slot.");
            scan.Remove("sensorInstalledSystemId");
        }

        JsonObject combat = ship["combat"]!.AsObject();
        JsonArray readiness = combat["directedEnergyReadiness"]!.AsArray();
        if (readiness.Count != 1 || readiness[0]!["weaponInstalledSystemId"]!.GetValue<long>() != 5)
            throw new InvalidOperationException("A V9 ship carries exactly the weapon-slot readiness.");
        var combatV9 = new JsonObject
        {
            ["nextDirectedEnergyReadyAtMilliseconds"] = readiness[0]!["readyAtMilliseconds"]!.DeepClone(),
            ["pendingStimulus"] = combat["pendingStimulus"]?.DeepClone(),
        };

        var result = new JsonObject();
        foreach ((string name, JsonNode? value) in ship.ToArray())
        {
            result[name] = name switch
            {
                "engineering" => engineeringV9,
                "combat" => combatV9,
                _ => value?.DeepClone(),
            };
        }

        return result;
    }

    private static JsonObject EngineeringToV9(JsonArray installed, JsonNode? repair)
    {
        double Condition(long id) => installed[(int)id - 1]!["condition"]!.GetValue<double>();
        int Allocation(long id) => installed[(int)id - 1]!["allocation"]!.GetValue<int>();
        return new JsonObject
        {
            ["generationCondition"] = Condition(1),
            ["sensorCondition"] = Condition(2),
            ["impulseCondition"] = Condition(3),
            ["shieldCondition"] = Condition(4),
            ["directedEnergyCondition"] = Condition(5),
            ["sensorAllocation"] = Allocation(2),
            ["impulseAllocation"] = Allocation(3),
            ["shieldAllocation"] = Allocation(4),
            ["directedEnergyAllocation"] = Allocation(5),
            ["activeRepair"] = repair is null
                ? null
                : new JsonObject
                {
                    ["targetSystem"] = V9Slots
                        .Single(slot => slot.Id == repair["targetInstalledSystemId"]!.GetValue<long>())
                        .Kind,
                    ["startingCondition"] = repair["startingCondition"]!.DeepClone(),
                    ["targetCondition"] = repair["targetCondition"]!.DeepClone(),
                    ["startedAtMilliseconds"] = repair["startedAtMilliseconds"]!.DeepClone(),
                    ["expectedCompletionMilliseconds"] = repair["expectedCompletionMilliseconds"]!.DeepClone(),
                    ["scheduledCompletionId"] = repair["scheduledCompletionId"]!.DeepClone(),
                },
        };
    }
}
