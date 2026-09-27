using AlterCourse.Core.Content;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using Microsoft.Extensions.Logging;

namespace AlterCourse.Core.Gameplay;

/// <summary>Builds the deterministic first playable simulation aggregate.</summary>
public static class FirstGameSetup
{
    /// <summary>Creates the representative four-ship proof world from validated content.</summary>
    public static GameSimulation Create(ShipDefinitionCatalog catalog, ILogger<GameSimulation>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ShipDefinition playerShipDefinition = catalog.GetRequired(new ShipDefinitionId("pathfinder"));
        (StrategicMap map, LocationId dawn, LocationId vesper, LocationId meridian) = CreateMap();
        var initialTime = new SimulationTime(0);
        ShipStart[] starts = CreateShipStarts(playerShipDefinition, dawn, vesper, meridian, initialTime);
        GameSimulation simulation = new GameBootstrap(initialTime, map, starts[0].InstanceId, starts).CreateSimulation(
            catalog,
            logger
        );
        simulation.BootstrapHiddenCautiousContactObservation(starts[3].InstanceId);
        return simulation;
    }

    /// <summary>Creates the six-ship two-faction production proof from validated catalogs.</summary>
    public static GameSimulation Create(
        ShipDefinitionCatalog shipCatalog,
        FactionDefinitionCatalog factionCatalog,
        ILogger<GameSimulation>? logger = null
    )
    {
        ArgumentNullException.ThrowIfNull(shipCatalog);
        ArgumentNullException.ThrowIfNull(factionCatalog);
        ShipDefinition definition = shipCatalog.GetRequired(new ShipDefinitionId("pathfinder"));
        (StrategicMap map, LocationId dawn, LocationId vesper, LocationId meridian) = CreateMap();
        var initialTime = new SimulationTime(0);
        FactionId factionA = new(1);
        FactionId factionB = new(2);
        ShipStart[] starts = CreateShipStarts(definition, dawn, vesper, meridian, initialTime);
        starts[1] = starts[1] with { DirectControllerFactionId = factionB };
        starts[2] = starts[2] with { DirectControllerFactionId = factionB };
        starts =
        [
            .. starts,
            CreateFactionShip(new ShipInstanceId(5), "Expedition Vessel Aurora", definition, meridian, factionA),
            CreateFactionShip(new ShipInstanceId(6), "Expedition Vessel Resolute", definition, meridian, factionA),
        ];
        FactionStart[] factions =
        [
            new(factionA, new FactionDefinitionId("faction-a"), vesper, ObservationResponsePosture.Enabled),
            new(
                factionB,
                new FactionDefinitionId("faction-b"),
                ObservationResponsePosture: ObservationResponsePosture.Enabled
            ),
        ];
        GameSimulation simulation = new GameBootstrap(
            initialTime,
            map,
            starts[0].InstanceId,
            starts,
            factions
        ).CreateSimulation(shipCatalog, factionCatalog, logger);
        simulation.BootstrapHiddenCautiousContactObservation(starts[3].InstanceId);
        return simulation;
    }

    // Production design installed ids, named by role. FirstGameSetup always uses the design default loadout, so
    // these must equal pathfinder.json's initialLoadout; bootstrap's exact key-set check fails loudly otherwise.
    private static readonly InstalledSystemId Generator = new(1);
    private static readonly InstalledSystemId Sensors = new(2);
    private static readonly InstalledSystemId Impulse = new(3);
    private static readonly InstalledSystemId Shields = new(4);
    private static readonly InstalledSystemId Weapons = new(5);

    private static ShipStart CreateFactionShip(
        ShipInstanceId id,
        string name,
        ShipDefinition definition,
        LocationId locationId,
        FactionId controller
    ) =>
        new(
            id,
            definition.Id,
            name,
            default,
            default,
            new AtLocationStart(locationId),
            Systems(1, 1, 70, 50, 0, 0),
            DirectControllerFactionId: controller
        );

    /// <summary>
    /// Declares state for the five default installations. New-game combat systems are nominal (condition 1),
    /// unlike historically migrated ships whose shields and weapons load installed but offline.
    /// </summary>
    private static ShipSystemsStart Systems(
        double generation,
        double sensors,
        int sensorPower,
        int impulsePower,
        int shieldPower,
        int weaponPower
    ) =>
        ShipSystemsStart.FromDesignDefaults([
            new(Generator, new SystemCondition(generation), null),
            new(Sensors, new SystemCondition(sensors), new PowerUnits(sensorPower)),
            new(Impulse, new SystemCondition(1), new PowerUnits(impulsePower)),
            new(Shields, new SystemCondition(1), new PowerUnits(shieldPower)),
            new(Weapons, new SystemCondition(1), new PowerUnits(weaponPower)),
        ]);

    private static ShipStart[] CreateShipStarts(
        ShipDefinition definition,
        LocationId dawn,
        LocationId vesper,
        LocationId meridian,
        SimulationTime initialTime
    )
    {
        var damagedSensors = new SystemCondition(0.4);
        var nominal = new SystemCondition(1);
        var zeroMotion = new TacticalMotion(new HeadingDegrees(0), new SpeedKilometersPerSecond(0));

        // The player's 44/31/0/0 and Kestrel's 70/5/15/30 are authored starting allocations, not computed
        // presets (the player deliberately does not start on the four-consumer Balanced 28/20/16/11).
        return
        [
            new(
                new ShipInstanceId(1),
                definition.Id,
                "USS Pathfinder",
                new TacticalPosition(3.25, -7.5),
                zeroMotion,
                new AtLocationStart(dawn),
                Systems(0.625, 0.4, 44, 31, 0, 0),
                new SystemRepairStart(Sensors, damagedSensors, nominal, initialTime)
            ),
            new(
                new ShipInstanceId(2),
                definition.Id,
                "USS Wayfarer",
                new TacticalPosition(-2, 4),
                zeroMotion,
                new AtLocationStart(vesper),
                Systems(1, 1, 70, 50, 0, 0)
            ),
            new(
                new ShipInstanceId(3),
                definition.Id,
                "USS Horizon",
                new TacticalPosition(6, 1.5),
                zeroMotion,
                new TravelingStart(vesper, meridian, initialTime),
                Systems(1, 1, 70, 50, 0, 0)
            ),
            new(
                new ShipInstanceId(4),
                definition.Id,
                "Survey Vessel Kestrel",
                new TacticalPosition(21.25, -7.5),
                zeroMotion,
                new AtLocationStart(dawn),
                Systems(1, 1, 70, 5, 15, 30)
            ),
        ];
    }

    private static (StrategicMap Map, LocationId Dawn, LocationId Vesper, LocationId Meridian) CreateMap()
    {
        var dawn = new StrategicLocation(
            new LocationId("dawn-anchor"),
            "Dawn Anchor",
            new StrategicMapPosition(-5.5, 2.25)
        );
        var vesper = new StrategicLocation(
            new LocationId("vesper-reach"),
            "Vesper Reach",
            new StrategicMapPosition(8.125, 11.75)
        );
        var meridian = new StrategicLocation(
            new LocationId("meridian-drift"),
            "Meridian Drift",
            new StrategicMapPosition(17.4, -3.6)
        );
        var map = new StrategicMap(
            [dawn, vesper, meridian],
            [
                new StrategicRoute(dawn.Id, vesper.Id, new SimulationDuration(12000)),
                new StrategicRoute(vesper.Id, meridian.Id, new SimulationDuration(14000)),
            ]
        );

        return (map, dawn.Id, vesper.Id, meridian.Id);
    }
}
