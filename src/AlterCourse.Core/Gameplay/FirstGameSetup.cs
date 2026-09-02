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
    /// <summary>Creates a three-location game with one damaged ship under active repair.</summary>
    public static GameSimulation Create(ShipDefinition playerShipDefinition)
    {
        ArgumentNullException.ThrowIfNull(playerShipDefinition);
        var initialSensorIntegrity = new SensorIntegrity(0.4);

        (StrategicMap map, LocationId startingLocation) = CreateMap(playerShipDefinition.SensorRepairDuration);

        var initialTime = new SimulationTime(0);
        var allocator = ShipInstanceIdAllocator.Create();
        (ShipInstanceIdAllocator followingAllocator, ShipInstanceId playerId) = allocator.Allocate();
        SimulationTime repairCompletion = initialTime.AdvanceBy(playerShipDefinition.SensorRepairDuration);
        (SimulationScheduler scheduler, ScheduledWork repairWork) = SimulationScheduler
            .Create()
            .Schedule(repairCompletion, playerId, ScheduledWorkKind.SensorRepairCompletion);
        var repair = new SensorRepairState(
            initialSensorIntegrity,
            new SensorIntegrity(1),
            initialTime,
            repairCompletion,
            repairWork.Id
        );
        var playerShip = new ShipState(
            playerId,
            playerShipDefinition.Id,
            "USS Pathfinder",
            new TacticalPosition(3.25, -7.5),
            new TacticalMotion(new HeadingDegrees(0), new SpeedKilometersPerSecond(0)),
            initialSensorIntegrity,
            repair,
            new AtLocationState(startingLocation)
        );
        var state = new SimulationState(initialTime, scheduler, followingAllocator, map, playerId, [playerShip]);
        var catalog = new ShipDefinitionCatalog(
            new Dictionary<ShipDefinitionId, ShipDefinition> { [playerShipDefinition.Id] = playerShipDefinition }
        );
        return GameSimulation.RestoreState(state, catalog);
    }

    private static (StrategicMap Map, LocationId StartingLocation) CreateMap(SimulationDuration repairDuration)
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
                new StrategicRoute(dawn.Id, vesper.Id, repairDuration.Add(new SimulationDuration(4000))),
                new StrategicRoute(vesper.Id, meridian.Id, repairDuration.Add(new SimulationDuration(6000))),
            ]
        );

        return (map, dawn.Id);
    }
}
