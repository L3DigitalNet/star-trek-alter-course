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

/// <summary>
/// Verifies installed identity is ship-local: nonconsecutive ids, the same local ids on two ships, and commands that
/// resolve an installation only within the addressed ship.
/// </summary>
public sealed class ShipSystemIsolationTests
{
    private static readonly ShipDefinitionCatalog Catalog = TestShipContent.Pathfinder();
    private static readonly ShipInstanceId Player = new(1);
    private static readonly ShipInstanceId Other = new(4);
    private static readonly InstalledSystemId Sensors = new(2);
    private static readonly InstalledSystemId Impulse = new(17);
    private static readonly InstalledSystemId Generator = new(900);

    /// <summary>Nonconsecutive identities and their continuation are kept exactly on both ships.</summary>
    [Fact]
    public void KeepsNonconsecutiveIdentitiesOnEachShip()
    {
        SimulationState state = World(reversed: false).CaptureState();

        foreach (ShipInstanceId ship in new[] { Player, Other })
        {
            ShipEngineeringState engineering = state.GetRequiredShip(ship).Engineering;
            Assert.Equal([2L, 17L, 900L], engineering.Systems.ByIdentity.Select(system => system.Id.Value));
            Assert.Equal(901, engineering.InstallationIds.NextId);
        }
    }

    /// <summary>A repair addressed to ship 4's installation 2 changes ship 4 only, although ship 1 also has an id 2.</summary>
    [Fact]
    public void RepairAddressMutatesOnlyTheAddressedShip()
    {
        SimulationState state = World(reversed: false).CaptureState();

        RepairApplication repair = GameSimulation.ApplyShipRepair(
            state,
            Catalog,
            new ShipSystemAddress(Other, Sensors),
            new SystemCondition(1)
        );

        Assert.Equal(SystemRepairOutcome.Accepted, repair.Outcome);
        Assert.Equal(Sensors, repair.CandidateState.GetRequiredShip(Other).Engineering.ActiveRepair!.Target);
        Assert.Equal(state.GetRequiredShip(Player), repair.CandidateState.GetRequiredShip(Player));
        repair.CandidateState.Validate(Catalog);
    }

    /// <summary>An identity the addressed ship does not have is unknown there, and nothing changes.</summary>
    [Fact]
    public void ForeignIdentityIsUnknownSystem()
    {
        SimulationState state = World(reversed: false).CaptureState();

        RepairApplication repair = GameSimulation.ApplyShipRepair(
            state,
            Catalog,
            new ShipSystemAddress(Other, TestShipContent.Weapons),
            new SystemCondition(1)
        );

        Assert.Equal(SystemRepairOutcome.UnknownSystem, repair.Outcome);
        Assert.Same(state, repair.CandidateState);
    }

    /// <summary>Declaring the same installations in another order produces identical ship state.</summary>
    [Fact]
    public void ReorderedDeclarationsYieldIdenticalState()
    {
        SimulationState forward = World(reversed: false).CaptureState();
        SimulationState reversed = World(reversed: true).CaptureState();

        foreach (ShipInstanceId ship in new[] { Player, Other })
        {
            Assert.Equal(forward.GetRequiredShip(ship).Engineering, reversed.GetRequiredShip(ship).Engineering);
            Assert.Equal(forward.GetRequiredShip(ship).Combat, reversed.GetRequiredShip(ship).Combat);
        }

        Assert.Equal(forward.Scheduler.OutstandingWork, reversed.Scheduler.OutstandingWork);
    }

    /// <summary>
    /// Every refused command leaves state, time, events, scheduler, and allocators unchanged.
    /// </summary>
    [Fact]
    public void RejectionsChangeNothing()
    {
        GameSimulation game = World(reversed: false);

        AssertUnchanged(
            game,
            () =>
                Assert.Equal(
                    SystemRepairOutcome.UnknownSystem,
                    game.BeginSystemRepair(new InstalledSystemId(5), new(1)).Outcome
                )
        );
        AssertUnchanged(
            game,
            () => Assert.Equal(SystemRepairOutcome.NotRepairable, game.BeginSystemRepair(Generator, new(1)).Outcome)
        );
        AssertUnchanged(
            game,
            () =>
            {
                PowerAllocationResult result = game.ApplyPriorityAllocation(Generator);
                Assert.Equal(PowerAllocationOutcome.UnknownConsumer, result.Outcome);
                Assert.Empty(result.ResolvedEvents);
            }
        );
        AssertUnchanged(
            game,
            () =>
                Assert.Equal(
                    PowerAllocationOutcome.IncompleteAllocation,
                    game.SetPowerAllocation(new PowerAllocation([new(Sensors, new PowerUnits(70))])).Outcome
                )
        );
    }

    private static void AssertUnchanged(GameSimulation game, Action command)
    {
        SimulationState before = game.CaptureState();
        command();
        SimulationState after = game.CaptureState();
        Assert.Same(before, after);
        Assert.Equal(before.Time, after.Time);
        Assert.Equal(before.Scheduler.NextWorkId, after.Scheduler.NextWorkId);
        Assert.Equal(before.ShipIdAllocator.NextId, after.ShipIdAllocator.NextId);
        Assert.Equal(before.OrderIdAllocator.NextId, after.OrderIdAllocator.NextId);
    }

    private static GameSimulation World(bool reversed)
    {
        ShipSystemsStart Loadout(double sensorCondition)
        {
            InstalledSystemStart[] installations =
            [
                new(Generator, new SystemDefinitionId("pathfinder.power-generation"), new SystemCondition(1), null),
                new(
                    Sensors,
                    new SystemDefinitionId("pathfinder.sensors"),
                    new SystemCondition(sensorCondition),
                    new PowerUnits(70)
                ),
                new(
                    Impulse,
                    new SystemDefinitionId("pathfinder.impulse-propulsion"),
                    new SystemCondition(1),
                    new PowerUnits(50)
                ),
            ];
            return ShipSystemsStart.Explicit(901, reversed ? installations.AsEnumerable().Reverse() : installations);
        }

        var location = new LocationId("isolation");
        var map = new StrategicMap([new StrategicLocation(location, "Isolation", default)], []);
        ShipStart Start(ShipInstanceId id, double x, double sensorCondition) =>
            new(
                id,
                new ShipDefinitionId("pathfinder"),
                "Ship " + id.Value,
                new TacticalPosition(x, 0),
                default,
                new AtLocationStart(location),
                Loadout(sensorCondition)
            );
        return new GameBootstrap(
            new SimulationTime(0),
            map,
            Player,
            [Start(Player, 0, 1), Start(Other, 5, 0.5)]
        ).CreateSimulation(Catalog);
    }
}
