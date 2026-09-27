using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tests.Gameplay;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Player;

/// <summary>
/// Verifies the generic installed-system Engineering projection: rows are the player's actual installations in
/// common order, actions are Core-owned with deterministic order and reasons, and absence yields no row or action.
/// </summary>
public sealed class EngineeringProjectionTests
{
    private static readonly ShipDefinitionCatalog Catalog = TestShipContent.Pathfinder();

    /// <summary>
    /// The production player projects its five installations in common order and exactly the pre-substrate action
    /// sequence: Balance; Prioritize sensors, impulse, shields, weapons; repair shields, weapons, sensors, impulse.
    /// </summary>
    [Fact]
    public void ProductionProjectionReproducesBaseRowsAndActionSequence()
    {
        EngineeringProjection engineering = new Milestone3ProofFixture()
            .CreateDefault()
            .GetPlayerProjection()
            .Ship.Engineering;

        Assert.Equal(
            [
                (1L, ShipSystemKind.PowerGeneration, "Power generation"),
                (2L, ShipSystemKind.Sensors, "Sensors"),
                (3L, ShipSystemKind.ImpulsePropulsion, "Impulse propulsion"),
                (4L, ShipSystemKind.Shields, "Shields"),
                (5L, ShipSystemKind.DirectedEnergyWeapons, "Directed-energy weapons"),
            ],
            engineering.Systems.Select(row => (row.Id.Value, row.Kind, row.ComponentLabel))
        );
        InstalledSystemProjection generator = engineering.Systems[0];
        Assert.Null(generator.NominalDemand);
        Assert.Null(generator.Allocation);
        Assert.Null(generator.Capability);
        Assert.Null(generator.FullRepairDuration);
        InstalledSystemProjection sensors = engineering.Systems[1];
        Assert.Equal(new PowerUnits(70), sensors.NominalDemand);
        Assert.Equal(new PowerUnits(44), sensors.Allocation);
        Assert.Equal(44d / 70 * 0.4, sensors.Capability!.Value, 12);
        Assert.Equal(new SimulationDuration(8000), sensors.FullRepairDuration);
        Assert.Equal(new PowerUnits(75), engineering.AllocatedPower);

        // Production starts with the sensor repair already running, so the occupied slot outranks "already
        // nominal" on every repair action, including the nominal shields, weapons, and impulse drive.
        const EngineeringActionUnavailableReason busy = EngineeringActionUnavailableReason.RepairAlreadyActive;
        Assert.Equal(
            [
                Action(EngineeringOperation.Balance, null),
                Action(EngineeringOperation.Prioritize, 2),
                Action(EngineeringOperation.Prioritize, 3),
                Action(EngineeringOperation.Prioritize, 4),
                Action(EngineeringOperation.Prioritize, 5),
                Action(EngineeringOperation.BeginRepair, 4, busy),
                Action(EngineeringOperation.BeginRepair, 5, busy),
                Action(EngineeringOperation.BeginRepair, 2, busy),
                Action(EngineeringOperation.BeginRepair, 3, busy),
                Action(EngineeringOperation.ReturnToCommand, null),
            ],
            engineering.Actions
        );
        Assert.Equal(TestShipContent.Sensors, engineering.ActiveRepair!.Target);
    }

    /// <summary>
    /// An absent installation contributes no row and no action; specialized combat status reports the missing
    /// capability as null instead of inventing a zero-condition placeholder.
    /// </summary>
    [Fact]
    public void AbsentInstallationsProduceNoRowsOrActions()
    {
        (GameSimulation game, _) = TestPairs.Create(
            Catalog,
            TestPairs.Loadout(impulse: false, shields: false, weapons: false),
            TestPairs.Loadout()
        );
        PlayerShipProjection ship = game.GetPlayerProjection().Ship;

        Assert.Equal([1L, 2L], ship.Engineering.Systems.Select(row => row.Id.Value));
        Assert.Equal(
            [
                (EngineeringOperation.Balance, (long?)null),
                (EngineeringOperation.Prioritize, 2),
                (EngineeringOperation.BeginRepair, 2),
                (EngineeringOperation.ReturnToCommand, null),
            ],
            ship.Engineering.Actions.Select(action => (action.Operation, action.Target?.Value))
        );
        Assert.Null(ship.Combat.Shields);
        Assert.Null(ship.Combat.Weapon);
        Assert.Null(ship.Combat.NextDirectedEnergyReadyAt);
        Assert.Equal(new SimulationDuration(0), ship.Combat.RemainingCooldown);
    }

    /// <summary>Installed combat systems are projected by identity from the player's own installations.</summary>
    [Fact]
    public void CombatStatusReadsOwnInstalledShieldAndWeapon()
    {
        (GameSimulation game, _) = TestPairs.Create(
            Catalog,
            TestPairs.Loadout(shieldCondition: 0.5),
            TestPairs.Loadout()
        );
        CombatProjection combat = game.GetPlayerProjection().Ship.Combat;

        Assert.Equal(TestShipContent.Shields, combat.Shields!.Id);
        Assert.Equal(0.5, combat.Shields.Condition.Value);
        Assert.Equal(new PowerUnits(5), combat.Shields.Allocation);
        Assert.Equal(0.5 * 5 / 40, combat.Shields.Capability, 12);
        Assert.Equal(TestShipContent.Weapons, combat.Weapon!.Id);
        Assert.Equal(new PowerUnits(30), combat.Weapon.Allocation);
        Assert.NotNull(combat.NextDirectedEnergyReadyAt);
    }

    /// <summary>
    /// Without a repair, only damaged installations are repairable and nominal ones say so; once a repair starts,
    /// the occupied slot is reported on every repairable row, and the active repair names its installed target,
    /// kind, and component label.
    /// </summary>
    [Fact]
    public void RepairReasonsFollowCommandPrecedence()
    {
        (GameSimulation game, _) = TestPairs.Create(
            Catalog,
            TestPairs.Loadout(shieldCondition: 0.5),
            TestPairs.Loadout()
        );
        const EngineeringActionUnavailableReason nominal = EngineeringActionUnavailableReason.SystemAlreadyNominal;
        Assert.Equal(
            [
                Action(EngineeringOperation.BeginRepair, 4),
                Action(EngineeringOperation.BeginRepair, 5, nominal),
                Action(EngineeringOperation.BeginRepair, 2, nominal),
                Action(EngineeringOperation.BeginRepair, 3, nominal),
            ],
            Repairs(game)
        );

        Assert.Equal(
            SystemRepairOutcome.Accepted,
            game.BeginSystemRepair(TestShipContent.Shields, new SystemCondition(1)).Outcome
        );
        Assert.All(
            Repairs(game),
            action =>
            {
                Assert.False(action.IsAvailable);
                Assert.Equal(EngineeringActionUnavailableReason.RepairAlreadyActive, action.UnavailableReason);
            }
        );
        SystemRepairProjection repair = game.GetPlayerProjection().Ship.Engineering.ActiveRepair!;
        Assert.Equal(TestShipContent.Shields, repair.Target);
        Assert.Equal(ShipSystemKind.Shields, repair.TargetKind);
        Assert.Equal("Shields", repair.TargetLabel);
    }

    /// <summary>Allocation actions whose result would invalidate the current speed report the speed reason.</summary>
    [Fact]
    public void AllocationActionsReportCurrentSpeedTooHigh()
    {
        GameSimulation game = new Milestone3ProofFixture().CreateDefault();
        Assert.Equal(
            SetTacticalCourseOutcome.Accepted,
            game.SetTacticalCourse(new(new HeadingDegrees(90), new SpeedKilometersPerSecond(6))).Outcome
        );
        IReadOnlyList<EngineeringActionProjection> actions = game.GetPlayerProjection().Ship.Engineering.Actions;

        // Balanced (20 of 50 impulse units, 4 km/s) and sensor priority (5 units, 1 km/s) both undercut 6 km/s;
        // impulse priority keeps full propulsion.
        Assert.Equal(EngineeringActionUnavailableReason.CurrentSpeedTooHigh, actions[0].UnavailableReason);
        Assert.Equal(
            EngineeringActionUnavailableReason.CurrentSpeedTooHigh,
            actions
                .Single(action =>
                    action.Operation == EngineeringOperation.Prioritize && action.Target == TestShipContent.Sensors
                )
                .UnavailableReason
        );
        Assert.True(
            actions
                .Single(action =>
                    action.Operation == EngineeringOperation.Prioritize && action.Target == TestShipContent.Impulse
                )
                .IsAvailable
        );
    }

    /// <summary>
    /// Common projection supports several installations of one kind: both sensors get rows, priority actions, and
    /// repair actions, ordered by common order and then installed identity, with no collapse to one.
    /// </summary>
    [Fact]
    public void CommonProjectionKeepsMultipleSameKindInstallations()
    {
        SensorSystemDefinition sensor = TestSystems.Consumer("test.sensor", 200, 70);
        ShipEngineeringState engineering = TestSystems.Engineering(
            TestSystems.Install(9, TestSystems.Generator("test.generator", 120)),
            TestSystems.Install(17, sensor, allocation: 30, condition: 0.5),
            TestSystems.Install(3, sensor, allocation: 20)
        );
        ShipState player = Milestone3ProofFixture.Player(new Milestone3ProofFixture().CreateDefault()) with
        {
            Engineering = engineering,
        };

        Assert.Equal([9L, 3L, 17L], GameSimulation.ProjectSystems(engineering.Systems).Select(row => row.Id.Value));
        Assert.Equal(
            [
                Action(EngineeringOperation.Balance, null),
                Action(EngineeringOperation.Prioritize, 3),
                Action(EngineeringOperation.Prioritize, 17),
                Action(EngineeringOperation.BeginRepair, 3, EngineeringActionUnavailableReason.SystemAlreadyNominal),
                Action(EngineeringOperation.BeginRepair, 17),
                Action(EngineeringOperation.ReturnToCommand, null),
            ],
            GameSimulation.ProjectEngineeringActions(player)
        );
    }

    private static EngineeringActionProjection[] Repairs(GameSimulation game) =>
        [
            .. game.GetPlayerProjection()
                .Ship.Engineering.Actions.Where(action => action.Operation == EngineeringOperation.BeginRepair),
        ];

    private static EngineeringActionProjection Action(
        EngineeringOperation operation,
        long? target,
        EngineeringActionUnavailableReason? reason = null
    ) => new(operation, target is { } id ? new InstalledSystemId(id) : null, reason is null, reason);
}
