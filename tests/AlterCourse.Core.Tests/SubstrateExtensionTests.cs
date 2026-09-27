using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using AlterCourse.Core.Tests.Gameplay;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests;

/// <summary>
/// Demonstrates ADR 0014 extensibility: another definition of an existing kind, installed at gapped identities on one
/// ship, works through bootstrap, power, repair, scan, projection, persistence, and compatibility with no production
/// change and no sixth kind.
/// </summary>
/// <remarks>
/// <para>
/// Everything this file adds lives in tests: the definition is test-only content (<c>test.long-range-sensors</c>:
/// sensors kind, order 200, demand 60, repair 5,000 ms, passive range 45 km, scan 1,500 ms) and every call goes
/// through public or <c>InternalsVisibleTo</c> Core APIs. The expected numbers below are derived here from that
/// content with the stated rules, so a common algorithm that secretly special-cased the production definition would
/// fail them.
/// </para>
/// <para>
/// The player installs generation 1, the alternate sensors 7, impulse 12, shields 20, and weapons 33 with the
/// allocator at 50 — gapped ids that share no position with the production 1–5 layout. The second ship is the
/// unchanged design default forty kilometres away: beyond the standard 30 km sensors, inside the alternate 45 km.
/// </para>
/// </remarks>
public sealed class SubstrateExtensionTests
{
    private static readonly InstalledSystemId Generator = new(1);
    private static readonly InstalledSystemId Sensors = new(7);
    private static readonly InstalledSystemId Impulse = new(12);
    private static readonly InstalledSystemId Shields = new(20);
    private static readonly InstalledSystemId Weapons = new(33);
    private static readonly LocationId Station = new("extension-station");
    private static readonly GameSaveMetadata Metadata = new(
        "substrate-extension",
        "Substrate extension",
        new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero),
        new DateTimeOffset(2026, 9, 27, 0, 0, 0, TimeSpan.Zero)
    );

    private static SensorSystemDefinition LongRange => HeterogeneousCombatWorld.LongRangeSensors;

    private static ShipDefinitionCatalog Catalog { get; } =
        TestShipContent.Catalog(
            TestShipContent.Systems([.. TestShipContent.PathfinderSystems(), LongRange]),
            TestShipContent.Design("pathfinder", "Pathfinder class", TestShipContent.PathfinderSystems())
        );

    /// <summary>The extension is test content over the unchanged closed kind vocabulary, not a production kind.</summary>
    [Fact]
    public void ExtensionIsTestContentOfAnExistingKind()
    {
        Assert.Equal(ShipSystemKind.Sensors, LongRange.Kind);
        Assert.False(TestShipContent.ProductionSystems().TryGet(LongRange.Id, out _));
        Assert.Equal(
            "sd1;kind=sensors;condition=true;order=200;power=60;repair=5000;passiveRangeKm=45;scanMs=1500",
            SystemDefinitionSemantics.Describe(LongRange)
        );
    }

    /// <summary>
    /// Balanced, priority, exact, and brownout allocation read the alternate demand of 60 through installed ids.
    /// </summary>
    /// <remarks>
    /// Oracle for Balanced at 120 available over demands 60/50/40/30 (sum 180), in common order: floors
    /// 120·60/180 = 40, 120·50/180 = 33, 120·40/180 = 26, 120·30/180 = 20 (sum 119); one remainder unit goes to the
    /// first eligible consumer, the sensors, giving 41/33/26/20. Priority on the sensors satisfies 60 first, then 50,
    /// then the remaining 10 to shields: 60/50/10/0. Brownout of 60/50/10/0 to 60 available scales committed shares
    /// exactly: 30/25/5/0.
    /// </remarks>
    [Fact]
    public void PowerAllocationUsesAlternateDemandThroughInstalledIds()
    {
        GameSimulation game = Create();

        Assert.Equal(PowerAllocationOutcome.Accepted, game.ApplyBalancedAllocation().Outcome);
        Assert.Equal([41, 33, 26, 20], Shares(game));
        Assert.Equal(PowerAllocationOutcome.Accepted, game.ApplyPriorityAllocation(Sensors).Outcome);
        Assert.Equal([60, 50, 10, 0], Shares(game));

        PowerAllocationResult overDemand = game.SetPowerAllocation(Exact(61, 50, 9, 0));
        Assert.Equal(PowerAllocationOutcome.ConsumerDemandExceeded, overDemand.Outcome);
        Assert.Equal(Sensors, overDemand.Consumer);
        Assert.Equal(PowerAllocationOutcome.Accepted, game.SetPowerAllocation(Exact(60, 40, 20, 0)).Outcome);
        Assert.Equal([60, 40, 20, 0], Shares(game));

        ShipEngineeringState browned = PlayerShip(game)
            .Engineering.WithAllocation(Exact(60, 50, 10, 0))
            .WithCondition(Generator, new SystemCondition(0.5));
        Assert.Equal(60, browned.AvailablePower.Value);
        Assert.Equal(Exact(30, 25, 5, 0), browned.ReconcileAvailablePower());
    }

    /// <summary>Effective range is 45 km times capability, so the 40 km contact appears only at full sensor power.</summary>
    [Fact]
    public void SensorRangeScalesTheAlternatePassiveRange()
    {
        GameSimulation game = Create();
        game.ApplyBalancedAllocation();
        game.AdvanceFixedSteps(1);

        // 41 of 60 units: 45 × 41/60 = 30.75 km, short of the 40 km contact.
        Assert.Equal(45d * 41 / 60, game.GetPlayerProjection().Ship.Engineering.EffectivePassiveSensorRange.Value, 12);
        Assert.Empty(game.GetPlayerProjection().Ship.Sensors.Contacts);

        game.ApplyPriorityAllocation(Sensors);
        game.AdvanceFixedSteps(1);

        Assert.Equal(45, GameSimulation.EffectivePassiveSensorRange(PlayerShip(game).Engineering).Value);
        Assert.Single(game.GetPlayerProjection().Ship.Sensors.Contacts);
        Assert.Equal(Sensors, game.GetPlayerProjection().Ship.Sensors.SensorInstallation);
    }

    /// <summary>A scan started from installation 7 completes exactly 1,500 ms later and survives save and load.</summary>
    [Fact]
    public void ScanFromAlternateSensorsCompletesAtItsAuthoredDurationAcrossSaveAndLoad()
    {
        GameSimulation game = Create();
        game.ApplyPriorityAllocation(Sensors);
        game.AdvanceFixedSteps(1);
        SimulationTime started = game.CaptureState().Time;
        Assert.Equal(
            ActiveSensorScanOutcome.Accepted,
            game.RequestActiveSensorScan(game.GetPlayerProjection().Ship.Sensors.Contacts.Single().Id).Outcome
        );
        Assert.Equal(Sensors, PlayerShip(game).SensorKnowledge.ActiveScan!.Sensor);
        Assert.Equal(
            started.Milliseconds + 1500,
            PlayerShip(game).SensorKnowledge.ActiveScan!.ExpectedCompletion.Milliseconds
        );

        game.AdvanceFixedSteps(7);
        GameSimulation resumed = RoundTrip(game);
        game.AdvanceFixedSteps(7);
        resumed.AdvanceFixedSteps(7);
        Assert.NotNull(PlayerShip(resumed).SensorKnowledge.ActiveScan);

        SimulationAdvanceResult completion = game.AdvanceFixedSteps(1);
        Assert.Equal(completion.ResolvedEvents.ToArray(), resumed.AdvanceFixedSteps(1).ResolvedEvents.ToArray());
        Assert.Contains(
            completion.ResolvedEvents,
            item => item.Kind == PlayerAdvanceEventKind.ActiveSensorScanCompleted
        );
        Assert.Null(PlayerShip(resumed).SensorKnowledge.ActiveScan);
        Assert.Equal(GamePersistence.Serialize(game, Metadata), GamePersistence.Serialize(resumed, Metadata));
    }

    /// <summary>A repair of installation 7 takes the alternate 5,000 ms and completes identically after a mid-repair load.</summary>
    [Fact]
    public void RepairOfAlternateSensorsUsesItsAuthoredDurationAcrossSaveAndLoad()
    {
        GameSimulation game = Create(sensorCondition: 0.5);
        SimulationTime started = game.CaptureState().Time;

        Assert.Equal(SystemRepairOutcome.Accepted, game.BeginSystemRepair(Sensors, new SystemCondition(1)).Outcome);
        Assert.Equal(
            started.Milliseconds + 5000,
            PlayerShip(game).Engineering.ActiveRepair!.ExpectedCompletion.Milliseconds
        );
        game.AdvanceFixedSteps(25);
        GameSimulation resumed = RoundTrip(game);
        Assert.Equal(
            game.AdvanceFixedSteps(24).ResolvedEvents.ToArray(),
            resumed.AdvanceFixedSteps(24).ResolvedEvents.ToArray()
        );
        Assert.NotNull(PlayerShip(resumed).Engineering.ActiveRepair);

        SimulationAdvanceResult completion = resumed.AdvanceFixedSteps(1);
        Assert.Equal(completion.ResolvedEvents.ToArray(), game.AdvanceFixedSteps(1).ResolvedEvents.ToArray());
        Assert.Equal(GamePersistence.Serialize(game, Metadata), GamePersistence.Serialize(resumed, Metadata));
        PlayerAdvanceEvent completed = Assert.Single(
            completion.ResolvedEvents,
            item => item.Kind == PlayerAdvanceEventKind.SystemRepairCompleted
        );
        Assert.Equal(Sensors, completed.InstalledSystemId);
        Assert.Equal(ShipSystemKind.Sensors, completed.SystemKind);
        Assert.Equal(1, PlayerShip(resumed).Engineering.Systems.GetRequired(Sensors).Condition.Value);
    }

    /// <summary>The projection lists the alternate row with its own facts, and actions address installed ids in order.</summary>
    [Fact]
    public void ProjectionRowsAndActionsFollowInstalledIdsAndAuthoredOrder()
    {
        GameSimulation game = Create(sensorCondition: 0.5);
        game.ApplyPriorityAllocation(Sensors);
        EngineeringProjection engineering = game.GetPlayerProjection().Ship.Engineering;

        Assert.Equal([Generator, Sensors, Impulse, Shields, Weapons], engineering.Systems.Select(row => row.Id));
        InstalledSystemProjection sensors = engineering.Systems[1];
        Assert.Equal(LongRange.Id, sensors.DefinitionId);
        Assert.Equal("Long-range sensors", sensors.ComponentLabel);
        Assert.Equal(60, sensors.NominalDemand!.Value.Value);
        Assert.Equal(60, sensors.Allocation!.Value.Value);
        Assert.Equal(0.5, sensors.Capability);
        Assert.Equal(new SimulationDuration(5000), sensors.FullRepairDuration);
        Assert.Equal<(EngineeringOperation, InstalledSystemId?, bool)>(
            [
                (EngineeringOperation.Balance, null, true),
                (EngineeringOperation.Prioritize, Sensors, true),
                (EngineeringOperation.Prioritize, Impulse, true),
                (EngineeringOperation.Prioritize, Shields, true),
                (EngineeringOperation.Prioritize, Weapons, true),
                (EngineeringOperation.BeginRepair, Shields, false),
                (EngineeringOperation.BeginRepair, Weapons, false),
                (EngineeringOperation.BeginRepair, Sensors, true),
                (EngineeringOperation.BeginRepair, Impulse, false),
                (EngineeringOperation.ReturnToCommand, null, true),
            ],
            [.. engineering.Actions.Select(action => (action.Operation, action.Target, action.IsAvailable))]
        );
    }

    /// <summary>
    /// The alternate definition persists by reference with its descriptor, round-trips byte-stably, and fails closed
    /// when the supplied content changes its semantics.
    /// </summary>
    [Fact]
    public void AlternateDefinitionPersistsAndIsCompatibilityChecked()
    {
        GameSimulation game = Create();
        byte[] saved = GamePersistence.Serialize(game, Metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(saved, Catalog, "extension.json");
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
        Assert.Equal(PlayerShip(game).Engineering, PlayerShip(loaded.Simulation).Engineering);

        SensorSystemDefinition retuned = new(
            LongRange.Id,
            LongRange.ComponentLabel,
            LongRange.CommonOrder,
            true,
            LongRange.Repair,
            LongRange.Power!,
            new DistanceKilometers(46),
            LongRange.ActiveScanDuration
        );
        ShipDefinitionCatalog changed = TestShipContent.Catalog(
            TestShipContent.Systems([.. TestShipContent.PathfinderSystems(), retuned]),
            TestShipContent.Design("pathfinder", "Pathfinder class", TestShipContent.PathfinderSystems())
        );

        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(saved, changed, "extension.json")
        );
        Assert.Equal(GamePersistenceFailure.IncompatibleContent, failure.Failure);
        Assert.Contains("test.long-range-sensors", failure.Message, StringComparison.Ordinal);
        Assert.Contains("passiveRangeKm=46", failure.Message, StringComparison.Ordinal);
    }

    private static GameSimulation Create(double sensorCondition = 1)
    {
        var map = new StrategicMap([new StrategicLocation(Station, "Extension station", default)], []);
        ShipStart player = new(
            new ShipInstanceId(1),
            new ShipDefinitionId("pathfinder"),
            "Extension ship",
            new TacticalPosition(0, 0),
            default,
            new AtLocationStart(Station),
            ShipSystemsStart.Explicit(
                50,
                [
                    Installed(Generator, "pathfinder.power-generation", 1, null),
                    Installed(Sensors, LongRange.Id.Value, sensorCondition, 0),
                    Installed(Impulse, "pathfinder.impulse-propulsion", 1, 0),
                    Installed(Shields, "pathfinder.shields", 1, 0),
                    Installed(Weapons, "pathfinder.directed-energy-weapons", 1, 0),
                ]
            )
        );
        ShipStart standard = new(
            new ShipInstanceId(2),
            new ShipDefinitionId("pathfinder"),
            "Standard ship",
            new TacticalPosition(40, 0),
            default,
            new AtLocationStart(Station),
            TestShipStarts.Pathfinder()
        );
        return new GameBootstrap(new SimulationTime(0), map, player.InstanceId, [player, standard]).CreateSimulation(
            Catalog
        );
    }

    private static InstalledSystemStart Installed(
        InstalledSystemId id,
        string definition,
        double condition,
        int? power
    ) =>
        new(
            id,
            new SystemDefinitionId(definition),
            new SystemCondition(condition),
            power is null ? null : new PowerUnits(power.Value)
        );

    private static PowerAllocation Exact(int sensors, int impulse, int shields, int weapons) =>
        new([
            new(Sensors, new PowerUnits(sensors)),
            new(Impulse, new PowerUnits(impulse)),
            new(Shields, new PowerUnits(shields)),
            new(Weapons, new PowerUnits(weapons)),
        ]);

    private static int[] Shares(GameSimulation game) =>
        [
            .. new[] { Sensors, Impulse, Shields, Weapons }.Select(id =>
                PlayerShip(game).Engineering.Systems.GetRequired(id).Allocation!.Value.Value
            ),
        ];

    private static ShipState PlayerShip(GameSimulation game) =>
        game.CaptureState().GetRequiredShip(game.CaptureState().PlayerShipId);

    private static GameSimulation RoundTrip(GameSimulation game) =>
        GamePersistence.Deserialize(GamePersistence.Serialize(game, Metadata), Catalog, "extension.json").Simulation;
}
