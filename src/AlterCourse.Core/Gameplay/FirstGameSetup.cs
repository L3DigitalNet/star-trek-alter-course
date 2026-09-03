using AlterCourse.Core.Content;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Gameplay;

/// <summary>Builds the deterministic first playable simulation aggregate.</summary>
public static class FirstGameSetup
{
    /// <summary>Creates the representative four-ship proof world from validated content.</summary>
    public static GameSimulation Create(ShipDefinitionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ShipDefinition playerShipDefinition = catalog.GetRequired(new ShipDefinitionId("pathfinder"));
        (StrategicMap map, LocationId dawn, LocationId vesper, LocationId meridian) = CreateMap();
        var initialTime = new SimulationTime(0);
        ShipStart[] starts = CreateShipStarts(playerShipDefinition, dawn, vesper, meridian, initialTime);
        GameSimulation simulation = new GameBootstrap(initialTime, map, starts[0].InstanceId, starts).CreateSimulation(
            catalog
        );
        simulation.BootstrapHiddenCautiousContactObservation(starts[3].InstanceId);
        return simulation;
    }

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
        var constrainedGeneration = new SystemCondition(0.625);
        var fullAllocation = new PowerAllocation(new PowerUnits(70), new PowerUnits(50));
        var balancedAllocation = new PowerAllocation(new PowerUnits(44), new PowerUnits(31));
        var zeroMotion = new TacticalMotion(new HeadingDegrees(0), new SpeedKilometersPerSecond(0));
        return
        [
            new(
                new ShipInstanceId(1),
                definition.Id,
                "USS Pathfinder",
                new TacticalPosition(3.25, -7.5),
                zeroMotion,
                constrainedGeneration,
                damagedSensors,
                nominal,
                balancedAllocation,
                new AtLocationStart(dawn),
                new SystemRepairStart(ShipSystemId.Sensors, damagedSensors, nominal, initialTime)
            ),
            new(
                new ShipInstanceId(2),
                definition.Id,
                "USS Wayfarer",
                new TacticalPosition(-2, 4),
                zeroMotion,
                nominal,
                nominal,
                nominal,
                fullAllocation,
                new AtLocationStart(vesper)
            ),
            new(
                new ShipInstanceId(3),
                definition.Id,
                "USS Horizon",
                new TacticalPosition(6, 1.5),
                zeroMotion,
                nominal,
                nominal,
                nominal,
                fullAllocation,
                new TravelingStart(vesper, meridian, initialTime)
            ),
            new(
                new ShipInstanceId(4),
                definition.Id,
                "Survey Vessel Kestrel",
                new TacticalPosition(21.25, -7.5),
                zeroMotion,
                nominal,
                nominal,
                nominal,
                fullAllocation,
                new AtLocationStart(dawn)
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
