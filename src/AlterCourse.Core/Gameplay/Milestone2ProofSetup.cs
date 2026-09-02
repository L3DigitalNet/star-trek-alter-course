using AlterCourse.Core.Content;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

/// <summary>Builds the headless Milestone 2 proof world with active NPC patrol and hold intent.</summary>
public static class Milestone2ProofSetup
{
    private const long HourMilliseconds = 60 * 60 * 1000;

    /// <summary>Creates a 03:00 world whose patrol is halfway through its first six-hour leg.</summary>
    public static GameSimulation Create(ShipDefinitionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ShipDefinition definition = catalog.GetRequired(new ShipDefinitionId("pathfinder"));

        var alpha = new StrategicLocation(new LocationId("alpha-watch"), "Alpha Watch", default);
        var beta = new StrategicLocation(new LocationId("beta-watch"), "Beta Watch", default);
        var refuge = new StrategicLocation(new LocationId("quiet-refuge"), "Quiet Refuge", default);
        var map = new StrategicMap(
            [alpha, beta, refuge],
            [new StrategicRoute(alpha.Id, beta.Id, new SimulationDuration(6 * HourMilliseconds))]
        );
        var initialTime = new SimulationTime(3 * HourMilliseconds);
        var fullIntegrity = new SensorIntegrity(1);
        ShipStart[] starts =
        [
            new(
                new ShipInstanceId(1),
                definition.Id,
                "USS Pathfinder",
                default,
                default,
                fullIntegrity,
                new AtLocationStart(refuge.Id)
            ),
            new(
                new ShipInstanceId(2),
                definition.Id,
                "USS Sentinel",
                default,
                default,
                fullIntegrity,
                new TravelingStart(alpha.Id, beta.Id, new SimulationTime(0)),
                ActiveOrder: new PatrolRouteOrderStart([alpha.Id, beta.Id], 1)
            ),
            new(
                new ShipInstanceId(3),
                definition.Id,
                "USS Vigilant",
                default,
                default,
                fullIntegrity,
                new AtLocationStart(alpha.Id),
                ActiveOrder: new HoldUntilOrderStart(new SimulationTime(9 * HourMilliseconds))
            ),
        ];

        return new GameBootstrap(initialTime, map, starts[0].InstanceId, starts).CreateSimulation(catalog);
    }
}
