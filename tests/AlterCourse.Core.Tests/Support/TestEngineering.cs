using AlterCourse.Core.Content;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Tests.Support;

/// <summary>Builds engineering state and allocation values over the pathfinder-style installations (ids 1–5).</summary>
internal static class TestEngineering
{
    /// <summary>Builds a complete four-consumer allocation for sensors, impulse, shields, and weapons (ids 2–5).</summary>
    internal static PowerAllocation Allocation(int sensors, int impulse, int shields, int weapons) =>
        new([
            new(TestShipContent.Sensors, new PowerUnits(sensors)),
            new(TestShipContent.Impulse, new PowerUnits(impulse)),
            new(TestShipContent.Shields, new PowerUnits(shields)),
            new(TestShipContent.Weapons, new PowerUnits(weapons)),
        ]);

    /// <summary>Builds a complete two-consumer allocation for a loadout without shields and weapons.</summary>
    internal static PowerAllocation Allocation(int sensors, int impulse) =>
        new([
            new(TestShipContent.Sensors, new PowerUnits(sensors)),
            new(TestShipContent.Impulse, new PowerUnits(impulse)),
        ]);

    /// <summary>
    /// Builds five-installation engineering state (or three installations when the tuning omits combat).
    /// </summary>
    internal static ShipEngineeringState State(
        double generation,
        PowerAllocation allocation,
        PathfinderTuning? tuning = null,
        double sensors = 1,
        double impulse = 1,
        double shields = 1,
        double weapons = 1
    )
    {
        return Build(
            TestShipContent.PathfinderSystems(tuning),
            allocation,
            [generation, sensors, impulse, shields, weapons]
        );
    }

    /// <summary>
    /// Builds engineering state over a pathfinder-style design's own default definitions (installed ids 1..n in
    /// generation, sensors, impulse, shields, weapons order), so it validates against that catalog.
    /// </summary>
    internal static ShipEngineeringState FromDesign(
        ShipDefinitionCatalog catalog,
        ShipDefinitionId design,
        PowerAllocation allocation,
        double generation = 1,
        double sensors = 1,
        double impulse = 1,
        double shields = 1,
        double weapons = 1
    )
    {
        SystemDefinition[] definitions =
        [
            .. catalog
                .GetRequired(design)
                .InitialLoadout.Systems.Select(system => catalog.SystemDefinitions.GetRequired(system.DefinitionId)),
        ];
        return Build(definitions, allocation, [generation, sensors, impulse, shields, weapons]);
    }

    private static ShipEngineeringState Build(
        SystemDefinition[] definitions,
        PowerAllocation allocation,
        double[] conditions
    )
    {
        InstalledSystem[] installations =
        [
            .. definitions.Select(
                (definition, index) =>
                {
                    var id = new InstalledSystemId(index + 1);
                    PowerUnits? power =
                        definition.Power is null ? null
                        : allocation.TryGet(id, out PowerUnits value) ? value
                        : new PowerUnits(0);
                    return new InstalledSystem(id, definition, new SystemCondition(conditions[index]), power);
                }
            ),
        ];
        return new ShipEngineeringState(
            InstalledSystemCollection.Create(installations),
            InstalledSystemIdAllocator.Restore(installations.Length + 1)
        );
    }

    /// <summary>Gets the sole installation of a kind (tests only; mirrors the typed singleton rule).</summary>
    internal static InstalledSystem Of(ShipEngineeringState engineering, ShipSystemKind kind) =>
        engineering.Systems.OfKind(kind).Single();

    /// <summary>Gets the condition of the sole installation of a kind, or 0 when absent.</summary>
    internal static double ConditionOf(ShipEngineeringState engineering, ShipSystemKind kind) =>
        engineering.Systems.OfKind(kind).SingleOrDefault()?.Condition.Value ?? 0;

    /// <summary>Gets the allocation of the sole installation of a kind, or 0 when absent.</summary>
    internal static int AllocationOf(ShipEngineeringState engineering, ShipSystemKind kind) =>
        engineering.Systems.OfKind(kind).SingleOrDefault()?.Allocation?.Value ?? 0;

    /// <summary>Gets the capability of the sole installation of a kind, or 0 when absent.</summary>
    internal static double CapabilityOf(ShipEngineeringState engineering, ShipSystemKind kind) =>
        engineering.Systems.OfKind(kind).SingleOrDefault() is { } system ? ShipEngineeringState.Capability(system) : 0;

    /// <summary>Replaces the condition of the sole installation of a kind.</summary>
    internal static ShipEngineeringState WithCondition(
        ShipEngineeringState engineering,
        ShipSystemKind kind,
        double condition
    ) => engineering.WithCondition(Of(engineering, kind).Id, new SystemCondition(condition));

    /// <summary>Replaces the allocation of the sole consumer of a kind, keeping every other consumer's share.</summary>
    internal static ShipEngineeringState WithAllocation(
        ShipEngineeringState engineering,
        ShipSystemKind kind,
        int power
    )
    {
        InstalledSystemId target = Of(engineering, kind).Id;
        return engineering.WithAllocation(
            new PowerAllocation(
                engineering.Allocation.Entries.Select(entry =>
                    entry.Consumer == target ? entry with { Allocation = new PowerUnits(power) } : entry
                )
            )
        );
    }

    /// <summary>Gets the weapon tuning of the sole installed directed-energy weapon.</summary>
    internal static DirectedEnergyWeaponDefinition Weapon(ShipEngineeringState engineering) =>
        ((DirectedEnergyWeaponSystemDefinition)Of(engineering, ShipSystemKind.DirectedEnergyWeapons).Definition).Weapon;

    /// <summary>Gets the definition of the sole installation of a kind.</summary>
    internal static TDefinition DefinitionOf<TDefinition>(ShipEngineeringState engineering, ShipSystemKind kind)
        where TDefinition : SystemDefinition => (TDefinition)Of(engineering, kind).Definition;
}
