using AlterCourse.Core.Content;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Support;

/// <summary>Builds ship and system content for tests: the production files, or typed test catalogs.</summary>
/// <remarks>
/// Test catalogs keep the production definition ids (<c>pathfinder.*</c>) and installed ids 1–5 so worlds built from
/// them stay representable by the temporary V9 persistence bridge; only tuning differs.
/// </remarks>
internal static class TestShipContent
{
    internal static readonly InstalledSystemId Generator = new(1);
    internal static readonly InstalledSystemId Sensors = new(2);
    internal static readonly InstalledSystemId Impulse = new(3);
    internal static readonly InstalledSystemId Shields = new(4);
    internal static readonly InstalledSystemId Weapons = new(5);

    private static readonly Lazy<ShipDefinitionCatalog> ProductionCatalog = new(LoadProduction);

    /// <summary>Gets the repository root located from the test assembly directory.</summary>
    internal static string RepositoryRoot { get; } = FindRepositoryRoot();

    /// <summary>Reads a repository file by relative path.</summary>
    internal static string ReadRepositoryFile(string relativePath) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, relativePath));

    /// <summary>Gets the production system and ship catalogs loaded through the real V1/V6 loaders.</summary>
    internal static ShipDefinitionCatalog Production() => ProductionCatalog.Value;

    /// <summary>Loads the production system-definition catalog.</summary>
    internal static SystemDefinitionCatalog ProductionSystems() =>
        new SystemDefinitionCatalogLoader(
            ReadRepositoryFile("src/AlterCourse.Godot/content/schemas/system-definition-v1.schema.json")
        ).LoadCatalog([
            SystemDefinitionContent.FromText(
                "res://content/systems/pathfinder-systems.json",
                ReadRepositoryFile("src/AlterCourse.Godot/content/systems/pathfinder-systems.json")
            ),
        ]);

    /// <summary>Creates a ship-definition V6 loader over a system catalog.</summary>
    internal static ShipDefinitionCatalogLoader ShipLoader(SystemDefinitionCatalog systems) =>
        new(ReadRepositoryFile("src/AlterCourse.Godot/content/schemas/ship-definition-v6.schema.json"), systems);

    /// <summary>Builds a typed system catalog.</summary>
    internal static SystemDefinitionCatalog Systems(IEnumerable<SystemDefinition> definitions) => new(definitions);

    /// <summary>Builds a ship catalog resolved against a system catalog.</summary>
    internal static ShipDefinitionCatalog Catalog(SystemDefinitionCatalog systems, params ShipDefinition[] designs) =>
        new(designs.ToDictionary(design => design.Id), systems);

    /// <summary>Builds a one-design catalog from tuning; the default reproduces production content.</summary>
    internal static ShipDefinitionCatalog Pathfinder(
        PathfinderTuning? tuning = null,
        string designId = "pathfinder",
        string designDisplayName = "Pathfinder class"
    )
    {
        tuning ??= PathfinderTuning.Production;
        SystemDefinition[] definitions = PathfinderSystems(tuning);
        return Catalog(Systems(definitions), Design(designId, designDisplayName, definitions));
    }

    /// <summary>Builds a design whose default loadout installs the definitions at ids 1..n in order.</summary>
    internal static ShipDefinition Design(string id, string displayName, IReadOnlyList<SystemDefinition> definitions) =>
        new(
            new ShipDefinitionId(id),
            displayName,
            new ShipLoadoutDefinition(
                definitions.Count + 1,
                definitions.Select(
                    (definition, index) => new InitialInstalledSystem(new InstalledSystemId(index + 1), definition.Id)
                )
            )
        );

    /// <summary>
    /// Builds a catalog of several designs, each with its own tuned definitions under a distinct id prefix so their
    /// system definitions do not collide. Such worlds are not representable by the V9 bridge unless a design keeps
    /// the <c>pathfinder</c> prefix.
    /// </summary>
    internal static ShipDefinitionCatalog Designs(
        params (string Id, string DisplayName, PathfinderTuning Tuning, string Prefix)[] designs
    )
    {
        var definitions = new List<SystemDefinition>();
        var ships = new List<ShipDefinition>();
        foreach ((string id, string displayName, PathfinderTuning tuning, string prefix) in designs)
        {
            SystemDefinition[] systems = PathfinderSystems(tuning, prefix);
            definitions.AddRange(systems);
            ships.Add(Design(id, displayName, systems));
        }

        return Catalog(Systems(definitions), [.. ships]);
    }

    /// <summary>Builds the pathfinder-style definitions in installed-id order (generation, sensors, impulse, …).</summary>
    internal static SystemDefinition[] PathfinderSystems(PathfinderTuning? tuning = null, string prefix = "pathfinder")
    {
        tuning ??= PathfinderTuning.Production;
        SystemDefinition[] core =
        [
            new PowerGenerationSystemDefinition(
                new SystemDefinitionId(prefix + ".power-generation"),
                "Power generation",
                100,
                true,
                new PowerUnits(tuning.Generation)
            ),
            new SensorSystemDefinition(
                new SystemDefinitionId(prefix + ".sensors"),
                "Sensors",
                200,
                true,
                Repair(tuning.SensorRepairMilliseconds, 300),
                Power(tuning.SensorDemand),
                new DistanceKilometers(tuning.PassiveRange),
                new SimulationDuration(tuning.ActiveScanMilliseconds)
            ),
            new ImpulsePropulsionSystemDefinition(
                new SystemDefinitionId(prefix + ".impulse-propulsion"),
                "Impulse propulsion",
                300,
                true,
                Repair(tuning.ImpulseRepairMilliseconds, 400),
                Power(tuning.ImpulseDemand),
                new SpeedKilometersPerSecond(tuning.MaximumTacticalSpeed)
            ),
        ];
        return tuning.Combat ? [.. core, .. CombatSystems(tuning, prefix)] : core;
    }

    private static SystemDefinition[] CombatSystems(PathfinderTuning tuning, string prefix) =>
        [
            new ShieldSystemDefinition(
                new SystemDefinitionId(prefix + ".shields"),
                "Shields",
                400,
                true,
                Repair(tuning.ShieldRepairMilliseconds, 100),
                Power(tuning.ShieldDemand)
            ),
            new DirectedEnergyWeaponSystemDefinition(
                new SystemDefinitionId(prefix + ".directed-energy-weapons"),
                "Directed-energy weapons",
                500,
                true,
                Repair(tuning.WeaponRepairMilliseconds, 200),
                Power(tuning.WeaponDemand),
                new DirectedEnergyWeaponDefinition(
                    new DistanceKilometers(tuning.WeaponRange),
                    tuning.BaseDamage,
                    new SimulationDuration(tuning.CooldownMilliseconds)
                )
            ),
        ];

    private static SystemRepairCapability Repair(long milliseconds, int actionOrder) =>
        new(new SimulationDuration(milliseconds), actionOrder);

    private static SystemPowerDemand Power(int demand) => new(new PowerUnits(demand));

    private static ShipDefinitionCatalog LoadProduction() =>
        ShipLoader(ProductionSystems())
            .LoadCatalog([
                ShipDefinitionContent.FromText(
                    "res://content/ships/pathfinder.json",
                    ReadRepositoryFile("src/AlterCourse.Godot/content/ships/pathfinder.json")
                ),
            ]);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AlterCourse.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root was not found.");
    }

    /// <summary>Gets the definition a design's default loadout installs for its sole system of a kind.</summary>
    internal static TDefinition DefaultDefinition<TDefinition>(ShipDefinitionCatalog catalog, ShipDefinitionId design)
        where TDefinition : SystemDefinition =>
        catalog
            .GetRequired(design)
            .InitialLoadout.Systems.Select(system => catalog.SystemDefinitions.GetRequired(system.DefinitionId))
            .OfType<TDefinition>()
            .Single();
}
