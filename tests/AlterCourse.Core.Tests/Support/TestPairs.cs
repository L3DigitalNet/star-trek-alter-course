using AlterCourse.Core.AI;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Support;

/// <summary>
/// Builds two-ship engagements from explicit loadouts: ship 1 (the player) at the origin and ship 2 ten kilometres
/// away, identified to each other through the ordinary scan and hail commands.
/// </summary>
internal static class TestPairs
{
    /// <summary>The non-player ship.</summary>
    internal static readonly ShipInstanceId Npc = new(2);

    /// <summary>Declares an explicit pathfinder-style loadout at ids 1–5, omitting any system passed as absent.</summary>
    internal static ShipSystemsStart Loadout(
        bool impulse = true,
        bool shields = true,
        bool weapons = true,
        double shieldCondition = 1,
        int shieldPower = 5,
        int impulsePower = 5,
        int weaponPower = 30,
        string sensorDefinition = "pathfinder.sensors"
    )
    {
        List<InstalledSystemStart> installations =
        [
            Start(TestShipContent.Generator, "pathfinder.power-generation", 1, null),
            Start(TestShipContent.Sensors, sensorDefinition, 1, 70),
        ];
        if (impulse)
        {
            installations.Add(Start(TestShipContent.Impulse, "pathfinder.impulse-propulsion", 1, impulsePower));
        }

        if (shields)
        {
            installations.Add(Start(TestShipContent.Shields, "pathfinder.shields", shieldCondition, shieldPower));
        }

        if (weapons)
        {
            installations.Add(Start(TestShipContent.Weapons, "pathfinder.directed-energy-weapons", 1, weaponPower));
        }

        return ShipSystemsStart.Explicit(6, installations);
    }

    /// <summary>Creates the identified pair; the NPC keeps a cautious posture so the hail is acknowledged.</summary>
    internal static (GameSimulation Game, SensorContactId Contact) Create(
        ShipDefinitionCatalog catalog,
        ShipSystemsStart player,
        ShipSystemsStart npc
    )
    {
        var location = new LocationId("pair");
        var map = new StrategicMap([new StrategicLocation(location, "Pair", default)], []);
        ShipStart Start(long id, double x, ShipSystemsStart systems) =>
            new(
                new ShipInstanceId(id),
                new ShipDefinitionId("pathfinder"),
                "Ship " + id,
                new TacticalPosition(x, 0),
                default,
                new AtLocationStart(location),
                systems
            );
        GameSimulation game = new GameBootstrap(
            new SimulationTime(6000),
            map,
            new ShipInstanceId(1),
            [Start(1, 0, player), Start(2, 10, npc)]
        ).CreateSimulation(catalog);
        SimulationState initial = game.CaptureState();
        ShipState other = initial.GetRequiredShip(Npc);
        game = GameSimulation.RestoreState(
            initial.ReplaceShip(
                other.InstanceId,
                other with
                {
                    AutonomousState = other.AutonomousState with
                    {
                        ContactPosture = ShipContactPosture.CautiousContact,
                    },
                }
            ),
            catalog
        );
        game.AdvanceFixedSteps(1);
        SensorContactId contact = game.GetPlayerProjection().Ship.Sensors.Contacts.Single().Id;
        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(contact).Outcome);
        game.AdvanceFixedSteps(20);
        Assert.Equal(HailOutcome.Acknowledged, game.RequestHail(contact).Outcome);
        return (game, contact);
    }

    private static InstalledSystemStart Start(InstalledSystemId id, string definition, double condition, int? power) =>
        new(
            id,
            new SystemDefinitionId(definition),
            new SystemCondition(condition),
            power is null ? null : new PowerUnits(power.Value)
        );
}
