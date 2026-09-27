using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>
/// Verifies V10 persistence of installed ship systems: byte-stable round trips of heterogeneous, gapped, empty, and
/// exhausted loadouts; independence from class defaults; strict structural rejection with the live game untouched;
/// the typed cardinality refusal; and V10 byte comparisons around rejected commands.
/// </summary>
public sealed class GamePersistenceV10SubstrateTests
{
    // An alternate definition of an existing kind: same order as the standard sensors, doubled passive range.
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

    private static readonly DateTimeOffset Timestamp = new(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);

    private static readonly GameSaveMetadata Metadata = new("v10-substrate", "V10 substrate", Timestamp, Timestamp);

    private static readonly LocationId Origin = new("origin");

    private static ShipDefinitionCatalog Catalog { get; } =
        TestShipContent.Catalog(
            TestShipContent.Systems([.. TestShipContent.PathfinderSystems(), LongRangeSensors]),
            TestShipContent.Design("pathfinder", "Pathfinder class", TestShipContent.PathfinderSystems())
        );

    /// <summary>Two same-design ships with different actual installations survive V10 exactly and byte-stably.</summary>
    [Fact]
    public void HeterogeneousSameDesignWorldRoundTripsByteStable()
    {
        GameSimulation game = CreateHeterogeneous();
        byte[] saved = GamePersistence.Serialize(game, Metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(saved, Catalog, "heterogeneous.json");

        AssertSameInstallations(game.CaptureState(), loaded.Simulation.CaptureState());
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
        JsonObject root = Parse(saved);
        Assert.Equal(10, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal(SaveJsonV10.V10RulesVersion, root["simulationRulesVersion"]!.GetValue<string>());
        Assert.Equal<string>(
            [
                "pathfinder.directed-energy-weapons",
                "pathfinder.impulse-propulsion",
                "pathfinder.power-generation",
                "pathfinder.sensors",
                "pathfinder.shields",
                "test.long-range-sensors",
            ],
            [
                .. root["simulation"]!["systemDefinitions"]!
                    .AsArray()
                    .Select(row => row!["definitionId"]!.GetValue<string>()),
            ],
            StringComparer.Ordinal
        );
        Assert.Equal(
            "av1;power-generation,sensors,impulse-propulsion,shields,directed-energy-weapons",
            root["simulation"]!["aimVocabulary"]!.GetValue<string>()
        );
        JsonNode shieldless = root["simulation"]!["ships"]![0]!;
        Assert.DoesNotContain(
            shieldless["engineering"]!["installedSystems"]!.AsArray(),
            system => system!["installedSystemId"]!.GetValue<long>() == 4
        );
    }

    /// <summary>Explicit empty, gapped, and exhausted allocators persist verbatim and are never recomputed.</summary>
    [Fact]
    public void EmptyGappedAndExhaustedLoadoutsRoundTripVerbatim()
    {
        GameSimulation game = Create(
            (ShipSystemsStart.Explicit(1, []), 0),
            (Gapped(900), 5),
            (Gapped(long.MaxValue), 10)
        );
        byte[] saved = GamePersistence.Serialize(game, Metadata);
        SimulationState restored = GamePersistence.Deserialize(saved, Catalog, "gaps.json").Simulation.CaptureState();

        Assert.Empty(restored.Ships[0].Engineering.Systems);
        Assert.Equal(1, restored.Ships[0].Engineering.InstallationIds.NextId);
        Assert.Equal([17L, 200L], restored.Ships[1].Engineering.Systems.ByIdentity.Select(system => system.Id.Value));
        Assert.Equal(900, restored.Ships[1].Engineering.InstallationIds.NextId);
        Assert.True(restored.Ships[2].Engineering.InstallationIds.IsExhausted);
        Assert.Equal(saved, GamePersistence.Serialize(GameSimulation.RestoreState(restored, Catalog), Metadata));
    }

    /// <summary>A compatible change to the design's default loadout never alters a saved world's installations.</summary>
    [Fact]
    public void CompatibleClassDefaultChangeNeverAltersSavedInstallations()
    {
        GameSimulation game = CreateHeterogeneous();
        byte[] saved = GamePersistence.Serialize(game, Metadata);
        SystemDefinition[] standard = TestShipContent.PathfinderSystems();
        ShipDefinitionCatalog changedDefault = TestShipContent.Catalog(
            TestShipContent.Systems([.. standard, LongRangeSensors]),
            TestShipContent.Design(
                "pathfinder",
                "Pathfinder class",
                [.. standard.Where(definition => definition.Kind != ShipSystemKind.DirectedEnergyWeapons)]
            )
        );

        LoadedGameSave loaded = GamePersistence.Deserialize(saved, changedDefault, "changed-default.json");

        AssertSameInstallations(game.CaptureState(), loaded.Simulation.CaptureState());
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
    }

    /// <summary>
    /// Malformed V10 documents fail as invalid data for the reason each case names, and leave the live simulation
    /// byte-for-byte unchanged.
    /// </summary>
    /// <remarks>
    /// The diagnostic fragment pins which rule refused the document, so a case cannot pass because an unrelated,
    /// earlier check happens to reject its mutated JSON. Duplicate and unsorted installations share one canonical
    /// ordering rule and therefore one message; repair-wrong-work is refused by the runtime scheduler correlation.
    /// </remarks>
    [Theory]
    [InlineData("unknown-member", "'kind' could not be mapped")]
    [InlineData("missing-allocation-member", "missing required properties, including the following: allocation")]
    [InlineData("duplicate-installation", "Installed systems must be unique and in ascending identity order")]
    [InlineData("unsorted-installations", "Installed systems must be unique and in ascending identity order")]
    [InlineData("zero-installed-id", "Installed system identity must be from 1 through")]
    [InlineData(
        "nonconsumer-allocation",
        "installation 1 must carry an allocation exactly when its definition consumes power"
    )]
    [InlineData(
        "consumer-without-allocation",
        "installation 2 must carry an allocation exactly when its definition consumes power"
    )]
    [InlineData("over-demand", "exceeds authored demand of installed system 2")]
    [InlineData("allocator-behind", "allocator must follow every installed identity")]
    [InlineData("allocator-overflow", "nextInstalledSystemId': The JSON value could not be converted to System.Int64")]
    [InlineData("repair-missing-target", "Active repair must target an installation on the same ship")]
    [InlineData("repair-wrong-kind", "Active repair must target a repairable installation")]
    [InlineData("repair-wrong-work", "lacks exactly one correlated scheduled work item")]
    [InlineData("scan-wrong-kind", "source installation must be a sensor")]
    [InlineData("scan-missing-sensor", "must name a sensor installed on the same ship")]
    [InlineData("readiness-missing", "must list exactly the installed weapons")]
    [InlineData("readiness-wrong-kind", "keyed by directed-energy weapon installations")]
    [InlineData("readiness-misaligned", "Weapon readiness must be fixed-step aligned")]
    [InlineData("definition-not-in-table", "which the V10 system-definition table does not list")]
    [InlineData("unreferenced-definition", "lists an unreferenced definition")]
    [InlineData("unsorted-definitions", "System definition references must be unique and in ascending ordinal order")]
    [InlineData("malformed-semantics", "has a malformed semantics descriptor")]
    [InlineData("malformed-aim-vocabulary", "names unknown kind 'warp-core'")]
    [InlineData("duplicate-aim-kind", "lists 'sensors' twice")]
    [InlineData("bad-enum", "Scheduled work kind is unknown")]
    [InlineData("unsorted-ships", "V10 ships must be unique and in ascending identity order")]
    public void MalformedV10FailsAsInvalidDataWithoutTouchingLiveGame(string mutation, string diagnostic)
    {
        GameSimulation live = CreateActive();
        byte[] before = GamePersistence.Serialize(live, Metadata);
        JsonObject root = Parse(before);
        MutateV10(root, mutation);
        byte[] candidate = Encode(root);
        Assert.NotEqual(before, candidate);

        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(candidate, Catalog, mutation + ".json")
        );

        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.Contains(diagnostic, failure.Message, StringComparison.Ordinal);
        Assert.Equal(before, GamePersistence.Serialize(live, Metadata));
    }

    /// <summary>
    /// Two installed sensors are a valid common snapshot but fail the typed world's cardinality rule, with its own
    /// message rather than a structural one.
    /// </summary>
    [Fact]
    public void TwoSensorSnapshotPassesStructureButFailsTypedCardinality()
    {
        GameSimulation live = CreateActive();
        byte[] before = GamePersistence.Serialize(live, Metadata);
        JsonObject root = Parse(before);
        JsonNode ship = root["simulation"]!["ships"]![1]!;
        ship["engineering"]!["installedSystems"]!
            .AsArray()
            .Add(
                new JsonObject
                {
                    ["installedSystemId"] = 6,
                    ["definitionId"] = "pathfinder.sensors",
                    ["condition"] = 1.0,
                    ["allocation"] = 0,
                }
            );
        ship["engineering"]!["nextInstalledSystemId"] = 7;

        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(Encode(root), Catalog, "two-sensors.json")
        );

        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.Contains("at most one installed 'sensors'", failure.Message, StringComparison.Ordinal);
        Assert.Equal(before, GamePersistence.Serialize(live, Metadata));
    }

    /// <summary>Rejected commands leave the V10 save bytes, and therefore every persisted counter, unchanged.</summary>
    [Fact]
    public void RejectedCommandsLeaveV10BytesUnchanged()
    {
        GameSimulation game = CreateHeterogeneous();
        byte[] before = GamePersistence.Serialize(game, Metadata);

        // Id 4 exists only on the other ship (the player is shieldless), so ownership must reject it.
        Assert.Equal(
            SystemRepairOutcome.UnknownSystem,
            game.BeginSystemRepair(new InstalledSystemId(4), new SystemCondition(1)).Outcome
        );
        Assert.Equal(
            PowerAllocationOutcome.UnknownConsumer,
            game.ApplyPriorityAllocation(new InstalledSystemId(99)).Outcome
        );
        Assert.NotEqual(
            PowerAllocationOutcome.Accepted,
            game.SetPowerAllocation(
                new PowerAllocation([new PowerAllocationEntry(new InstalledSystemId(1), new PowerUnits(1))])
            ).Outcome
        );

        Assert.Equal(before, GamePersistence.Serialize(game, Metadata));
    }

    /// <summary>Saving and loading mid-operation continues exactly like the uninterrupted heterogeneous world.</summary>
    [Fact]
    public void ActiveMixedOperationsContinueLikeUninterrupted()
    {
        GameSimulation uninterrupted = CreateActive();
        byte[] partway = GamePersistence.Serialize(uninterrupted, Metadata);
        GameSimulation resumed = GamePersistence.Deserialize(partway, Catalog, "partway.json").Simulation;

        uninterrupted.AdvanceFixedSteps(90);
        resumed.AdvanceFixedSteps(90);

        Assert.Equal(GamePersistence.Serialize(uninterrupted, Metadata), GamePersistence.Serialize(resumed, Metadata));
        Assert.Null(resumed.CaptureState().Ships[0].Engineering.ActiveRepair);
    }

    private static GameSimulation CreateHeterogeneous() =>
        Create(
            (TestPairs.Loadout(shields: false, weaponPower: 0), 0),
            (TestPairs.Loadout(sensorDefinition: LongRangeSensors.Id.Value, shieldPower: 0, weaponPower: 0), 45)
        );

    /// <summary>A shieldless player with an active sensor repair, beside a long-range-sensor ship.</summary>
    private static GameSimulation CreateActive()
    {
        GameSimulation game = CreateHeterogeneous();
        SimulationState state = game.CaptureState();
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        player = player with
        {
            Engineering = TestEngineering.WithCondition(player.Engineering, ShipSystemKind.Sensors, 0.5),
        };
        game = GameSimulation.RestoreState(state.ReplaceShip(player.InstanceId, player), Catalog);
        Assert.Equal(
            SystemRepairOutcome.Accepted,
            game.BeginSystemRepair(TestShipContent.Sensors, new SystemCondition(1)).Outcome
        );
        game.AdvanceFixedSteps(3);
        return game;
    }

    private static ShipSystemsStart Gapped(long next) =>
        ShipSystemsStart.Explicit(
            next,
            [
                new InstalledSystemStart(
                    new InstalledSystemId(17),
                    new SystemDefinitionId("pathfinder.power-generation"),
                    new SystemCondition(1),
                    null
                ),
                new InstalledSystemStart(
                    new InstalledSystemId(200),
                    new SystemDefinitionId("pathfinder.sensors"),
                    new SystemCondition(1),
                    new PowerUnits(0)
                ),
            ]
        );

    private static GameSimulation Create(params (ShipSystemsStart Systems, double X)[] ships)
    {
        var map = new StrategicMap([new StrategicLocation(Origin, "Origin", default)], []);
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
        return new GameBootstrap(new SimulationTime(0), map, new ShipInstanceId(1), starts).CreateSimulation(Catalog);
    }

    private static void AssertSameInstallations(SimulationState expected, SimulationState actual)
    {
        Assert.Equal(expected.Ships.Length, actual.Ships.Length);
        foreach (ShipState ship in expected.Ships)
        {
            ShipState loaded = actual.GetRequiredShip(ship.InstanceId);
            Assert.Equal(ship.Engineering, loaded.Engineering);
            Assert.Equal(ship.Combat, loaded.Combat);
            Assert.Equal(ship.SensorKnowledge, loaded.SensorKnowledge);
        }
    }

    private static void MutateV10(JsonObject root, string mutation)
    {
        JsonObject simulation = root["simulation"]!.AsObject();
        JsonNode player = simulation["ships"]![0]!;
        JsonNode other = simulation["ships"]![1]!;
        JsonArray installed = other["engineering"]!["installedSystems"]!.AsArray();
        if (MutateInstallations(mutation, installed, other))
            return;
        if (MutateOperations(mutation, simulation, player, other))
            return;
        MutateDescriptors(mutation, simulation);
    }

    private static bool MutateInstallations(string mutation, JsonArray installed, JsonNode ship)
    {
        switch (mutation)
        {
            case "unknown-member":
                installed[0]!["kind"] = "power-generation";
                return true;
            case "missing-allocation-member":
                installed[1]!.AsObject().Remove("allocation");
                return true;
            case "duplicate-installation":
                installed[2]!["installedSystemId"] = 2;
                return true;
            case "unsorted-installations":
                JsonNode first = installed[0]!;
                installed.RemoveAt(0);
                installed.Add(first);
                return true;
            case "zero-installed-id":
                installed[0]!["installedSystemId"] = 0;
                return true;
            case "nonconsumer-allocation":
                installed[0]!["allocation"] = 0;
                return true;
            case "consumer-without-allocation":
                installed[1]!["allocation"] = null;
                return true;
            case "over-demand":
                installed[1]!["allocation"] = 71;
                return true;
            case "allocator-behind":
                ship["engineering"]!["nextInstalledSystemId"] = 5;
                return true;
            case "allocator-overflow":
                ship["engineering"]!["nextInstalledSystemId"] = JsonNode.Parse("9223372036854775808");
                return true;
            default:
                return false;
        }
    }

    private static bool MutateOperations(string mutation, JsonObject simulation, JsonNode player, JsonNode other)
    {
        JsonNode repair = player["engineering"]!["activeRepair"]!;
        switch (mutation)
        {
            case "repair-missing-target":
                repair["targetInstalledSystemId"] = 4;
                return true;
            case "repair-wrong-kind":
                repair["targetInstalledSystemId"] = 1;
                return true;
            case "repair-wrong-work":
                repair["scheduledCompletionId"] = 999;
                return true;
            case "scan-wrong-kind":
            case "scan-missing-sensor":
                player["sensorKnowledge"]!["activeScan"] = new JsonObject
                {
                    ["targetContactId"] = 1,
                    ["sensorInstalledSystemId"] = string.Equals(mutation, "scan-wrong-kind", StringComparison.Ordinal)
                        ? 3
                        : 9,
                    ["startedAtMilliseconds"] = 0,
                    ["expectedCompletionMilliseconds"] = 2000,
                    ["scheduledCompletionId"] = 1,
                };
                return true;
            case "readiness-missing":
                other["combat"]!["directedEnergyReadiness"] = new JsonArray();
                return true;
            case "readiness-wrong-kind":
                other["combat"]!["directedEnergyReadiness"]![0]!["weaponInstalledSystemId"] = 2;
                return true;
            case "readiness-misaligned":
                other["combat"]!["directedEnergyReadiness"]![0]!["readyAtMilliseconds"] = 7;
                return true;
            case "bad-enum":
                simulation["scheduler"]!["outstandingWork"]![0]!["kind"] = "systemRepairCompletionX";
                return true;
            case "unsorted-ships":
                JsonArray ships = simulation["ships"]!.AsArray();
                JsonNode first = ships[0]!;
                ships.RemoveAt(0);
                ships.Add(first);
                return true;
            default:
                return false;
        }
    }

    private static void MutateDescriptors(string mutation, JsonObject simulation)
    {
        JsonArray table = simulation["systemDefinitions"]!.AsArray();
        switch (mutation)
        {
            case "definition-not-in-table":
                table.RemoveAt(table.Count - 1);
                break;
            case "unreferenced-definition":
                table.Add(new JsonObject { ["definitionId"] = "zzz.unused", ["semantics"] = "sd1;kind=shields" });
                break;
            case "unsorted-definitions":
                JsonNode first = table[0]!;
                table.RemoveAt(0);
                table.Add(first);
                break;
            case "malformed-semantics":
                table[0]!["semantics"] = "sd1;kind = shields";
                break;
            case "malformed-aim-vocabulary":
                simulation["aimVocabulary"] = "av1;power-generation,warp-core";
                break;
            case "duplicate-aim-kind":
                simulation["aimVocabulary"] = "av1;sensors,sensors";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
        }
    }

    private static JsonObject Parse(byte[] json) => JsonNode.Parse(json)!.AsObject();

    private static byte[] Encode(JsonObject root) => Encoding.UTF8.GetBytes(root.ToJsonString());
}
