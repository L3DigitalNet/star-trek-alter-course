using AlterCourse.Core.Content;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Gameplay;

/// <summary>Builds the headless Milestone 2 proof world with active NPC patrol and hold intent.</summary>
internal static class Milestone2ProofSetup
{
    private const long HourMilliseconds = 60 * 60 * 1000;

    /// <summary>Creates a 03:00 world whose patrol is halfway through its first six-hour leg.</summary>
    internal static GameSimulation Create(ShipDefinitionCatalog catalog)
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
        ShipSystemsStart systems = LegacySystems();
        ShipStart[] starts =
        [
            new(
                new ShipInstanceId(1),
                definition.Id,
                "USS Pathfinder",
                default,
                default,
                new AtLocationStart(refuge.Id),
                systems
            ),
            new(
                new ShipInstanceId(2),
                definition.Id,
                "USS Sentinel",
                default,
                default,
                new TravelingStart(alpha.Id, beta.Id, new SimulationTime(0)),
                systems,
                ActiveOrder: new PatrolRouteOrderStart([alpha.Id, beta.Id], 1)
            ),
            new(
                new ShipInstanceId(3),
                definition.Id,
                "USS Vigilant",
                default,
                default,
                new AtLocationStart(alpha.Id),
                systems,
                ActiveOrder: new HoldUntilOrderStart(new SimulationTime(9 * HourMilliseconds))
            ),
        ];

        return new GameBootstrap(initialTime, map, starts[0].InstanceId, starts).CreateSimulation(catalog);
    }

    /// <summary>
    /// The Milestone 2 proof predates combat: its ships carry the design's shields and weapons installed but offline
    /// and unpowered, which is exactly the state its pre-substrate declarations produced.
    /// </summary>
    private static ShipSystemsStart LegacySystems() =>
        ShipSystemsStart.FromDesignDefaults([
            new(new InstalledSystemId(1), new SystemCondition(1), null),
            new(new InstalledSystemId(2), new SystemCondition(1), new PowerUnits(70)),
            new(new InstalledSystemId(3), new SystemCondition(1), new PowerUnits(50)),
            new(new InstalledSystemId(4), new SystemCondition(0), new PowerUnits(0)),
            new(new InstalledSystemId(5), new SystemCondition(0), new PowerUnits(0)),
        ]);
}
