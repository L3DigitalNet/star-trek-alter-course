using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>Verifies bootstrap admits heterogeneous explicit loadouts and each absence has its typed consequence.</summary>
public sealed class HeterogeneousBootstrapTests
{
    private static readonly SensorSystemDefinition LongRangeSensors = new(
        new SystemDefinitionId("test.long-range-sensors"),
        "Long-range sensors",
        200,
        true,
        new SystemRepairCapability(new SimulationDuration(8000), 300),
        new SystemPowerDemand(new PowerUnits(70)),
        new DistanceKilometers(60),
        new SimulationDuration(2000)
    );

    private static readonly SystemDefinition[] Definitions = [.. TestShipContent.PathfinderSystems(), LongRangeSensors];

    private static readonly ShipDefinitionCatalog Catalog = TestShipContent.Catalog(
        TestShipContent.Systems(Definitions),
        TestShipContent.Design("pathfinder", "Pathfinder class", TestShipContent.PathfinderSystems())
    );

    private static readonly LocationId Origin = new("origin");
    private static readonly LocationId Destination = new("destination");

    /// <summary>Design defaults install the design's loadout; an explicit empty loadout installs nothing.</summary>
    [Fact]
    public void DesignDefaultAndExplicitEmptyLoadoutsDiffer()
    {
        SimulationState state = Create((TestShipStarts.Pathfinder(), 0), (ShipSystemsStart.Explicit(1, []), 5))
            .CaptureState();

        ShipState defaulted = state.GetRequiredShip(new ShipInstanceId(1));
        ShipState empty = state.GetRequiredShip(new ShipInstanceId(2));
        Assert.Equal(5, defaulted.Engineering.Systems.Count);
        Assert.Equal(6, defaulted.Engineering.InstallationIds.NextId);
        Assert.Empty(empty.Engineering.Systems);
        Assert.Equal(1, empty.Engineering.InstallationIds.NextId);
        Assert.Empty(empty.Combat.WeaponReadiness);
        Assert.Equal(0, empty.Engineering.AvailablePower.Value);
        Assert.Empty(empty.Engineering.Allocation.Entries);
        state.Validate(Catalog);
    }

    /// <summary>
    /// Two ships of one design differ only in installations: one carries alternate long-range sensors and observes
    /// at 45 km, the other lacks shields and, with standard sensors, does not.
    /// </summary>
    [Fact]
    public void SameDesignShipsCarryDifferentInstallations()
    {
        GameSimulation game = Create(
            (TestPairs.Loadout(sensorDefinition: LongRangeSensors.Id.Value, shieldPower: 0, weaponPower: 0), 0),
            (TestPairs.Loadout(shields: false, weaponPower: 0), 45)
        );

        game.AdvanceFixedSteps(1);

        SimulationState state = game.CaptureState();
        ShipState longRange = state.GetRequiredShip(new ShipInstanceId(1));
        ShipState shieldless = state.GetRequiredShip(new ShipInstanceId(2));
        Assert.Equal(LongRangeSensors, TestEngineering.Of(longRange.Engineering, ShipSystemKind.Sensors).Definition);
        Assert.Empty(shieldless.Engineering.Systems.OfKind(ShipSystemKind.Shields));
        Assert.Single(game.GetPlayerProjection().Ship.Sensors.Contacts);
        Assert.Empty(shieldless.SensorKnowledge.Contacts);
    }

    /// <summary>Each absent kind yields its typed zero capability, never an invented default.</summary>
    [Theory]
    [InlineData("power-generation")]
    [InlineData("sensors")]
    [InlineData("impulse-propulsion")]
    [InlineData("directed-energy-weapons")]
    public void EachAbsentKindHasItsTypedConsequence(string kind)
    {
        var absent = ShipSystemKind.Parse(kind);
        GameSimulation game = Create((Without(absent), 0), (TestShipStarts.Pathfinder(), 5));
        game.AdvanceFixedSteps(1);
        ShipState player = game.CaptureState().GetRequiredShip(new ShipInstanceId(1));
        Assert.Empty(player.Engineering.Systems.OfKind(absent));

        switch (kind)
        {
            case "power-generation":
                Assert.Equal(0, player.Engineering.AvailablePower.Value);
                Assert.Equal(PowerAllocationOutcome.Accepted, game.ApplyBalancedAllocation().Outcome);
                Assert.Equal(0, game.CaptureState().GetRequiredShip(player.InstanceId).Engineering.Allocation.Total);
                break;
            case "sensors":
                Assert.Empty(game.GetPlayerProjection().Ship.Sensors.Contacts);
                Assert.Equal(0, GameSimulation.EffectivePassiveSensorRange(player.Engineering).Value);
                break;
            case "impulse-propulsion":
                Assert.Equal(0, GameSimulation.EffectiveMaximumTacticalSpeed(player.Engineering).Value);
                Assert.Equal(
                    SetTacticalCourseOutcome.PropulsionOffline,
                    game.SetTacticalCourse(new(new HeadingDegrees(0), new SpeedKilometersPerSecond(1))).Outcome
                );
                break;
            default:
                Assert.Empty(player.Combat.WeaponReadiness);
                Assert.Null(game.GetPlayerProjection().Ship.Combat.WeaponRange);
                break;
        }
    }

    /// <summary>
    /// Storage admits two sensors, but the typed world boundary refuses them at bootstrap as a distinct cardinality
    /// rule naming both identities, rather than silently using one.
    /// </summary>
    [Fact]
    public void BootstrapRefusesUnsupportedCardinality()
    {
        var twoSensors = ShipSystemsStart.Explicit(
            7,
            [
                new(
                    TestShipContent.Generator,
                    new SystemDefinitionId("pathfinder.power-generation"),
                    new SystemCondition(1),
                    null
                ),
                new(
                    TestShipContent.Sensors,
                    new SystemDefinitionId("pathfinder.sensors"),
                    new SystemCondition(1),
                    new PowerUnits(30)
                ),
                new(new InstalledSystemId(6), LongRangeSensors.Id, new SystemCondition(1), new PowerUnits(30)),
            ]
        );

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => Create((twoSensors, 0)));

        Assert.Contains("at most one installed 'sensors'", exception.Message, StringComparison.Ordinal);
        Assert.Contains("ids 2, 6", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Strategic travel needs no impulse installation.</summary>
    [Fact]
    public void StrategicTravelDoesNotRequireImpulse()
    {
        GameSimulation game = Create((Without(ShipSystemKind.ImpulsePropulsion), 0));

        Assert.Equal(TravelOutcome.Accepted, game.RequestTravel(new TravelIntent(Destination)).Outcome);
        game.AdvanceUntilNextPlayerRelevantEvent();

        ShipState player = game.CaptureState().GetRequiredShip(new ShipInstanceId(1));
        Assert.Equal(new AtLocationState(Destination), player.StrategicState);
    }

    private static ShipSystemsStart Without(ShipSystemKind absent)
    {
        bool powered = absent != ShipSystemKind.PowerGeneration;
        InstalledSystemStart[] all =
        [
            new(
                TestShipContent.Generator,
                new SystemDefinitionId("pathfinder.power-generation"),
                new SystemCondition(1),
                null
            ),
            new(
                TestShipContent.Sensors,
                new SystemDefinitionId("pathfinder.sensors"),
                new SystemCondition(1),
                new PowerUnits(powered ? 70 : 0)
            ),
            new(
                TestShipContent.Impulse,
                new SystemDefinitionId("pathfinder.impulse-propulsion"),
                new SystemCondition(1),
                new PowerUnits(powered ? 50 : 0)
            ),
            new(
                TestShipContent.Shields,
                new SystemDefinitionId("pathfinder.shields"),
                new SystemCondition(1),
                new PowerUnits(0)
            ),
            new(
                TestShipContent.Weapons,
                new SystemDefinitionId("pathfinder.directed-energy-weapons"),
                new SystemCondition(1),
                new PowerUnits(0)
            ),
        ];
        return ShipSystemsStart.Explicit(
            6,
            all.Where(start => Definitions.Single(definition => definition.Id == start.DefinitionId).Kind != absent)
        );
    }

    private static GameSimulation Create(params (ShipSystemsStart Systems, double X)[] ships)
    {
        var map = new StrategicMap(
            [
                new StrategicLocation(Origin, "Origin", default),
                new StrategicLocation(Destination, "Destination", default),
            ],
            [new StrategicRoute(Origin, Destination, new SimulationDuration(4000))]
        );
        ShipStart[] starts =
        [
            .. ships.Select(
                (ship, index) =>
                    new ShipStart(
                        new ShipInstanceId(index + 1),
                        new ShipDefinitionId("pathfinder"),
                        "Ship " + (index + 1),
                        new TacticalPosition(ship.X, 0),
                        default,
                        new AtLocationStart(Origin),
                        ship.Systems
                    )
            ),
        ];
        return new GameBootstrap(new SimulationTime(0), map, starts[0].InstanceId, starts).CreateSimulation(Catalog);
    }
}
