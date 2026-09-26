using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using AlterCourse.Core.Tests.Gameplay;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies that combat state survives the explicit V9 snapshot boundary.</summary>
public sealed class GamePersistenceV9CombatTests
{
    /// <summary>Current snapshots retain new-game conditions instead of silently dropping them.</summary>
    [Fact]
    public void CurrentRoundTripRetainsCombatEngineering()
    {
        var fixture = new Milestone3ProofFixture();
        GameSimulation game = fixture.CreateDefault();
        byte[] json = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        JsonObject root = JsonNode.Parse(json)!.AsObject();
        Assert.Equal(9, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("first-combat-engagement-v1", root["simulationRulesVersion"]!.GetValue<string>());
        GameSimulation restored = GamePersistence.Deserialize(json, fixture.Catalog, "combat-v9.json").Simulation;
        foreach (ShipState ship in game.CaptureState().Ships)
        {
            ShipState loaded = restored.CaptureState().GetRequiredShip(ship.InstanceId);
            Assert.Equal(ship.Engineering, loaded.Engineering);
            Assert.Equal(ship.Combat, loaded.Combat);
        }
    }

    /// <summary>Pending defense, exact readiness, damage, and all allocations retain deterministic continuation.</summary>
    [Fact]
    public void PendingDefenseAndCooldownContinueExactly()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(15);
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors)).Outcome
        );
        byte[] saved = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        GameSimulation resumed = GamePersistence.Deserialize(saved, fixture.Catalog, "pending-defense.json").Simulation;
        foreach (ShipState ship in game.CaptureState().Ships)
        {
            ShipState loaded = resumed.CaptureState().GetRequiredShip(ship.InstanceId);
            Assert.Equal(ship.Engineering, loaded.Engineering);
            Assert.Equal(ship.Combat, loaded.Combat);
        }
        Assert.Equal(saved, GamePersistence.Serialize(resumed, Milestone3ProofFixture.Metadata));
        foreach (int steps in new[] { 1, 1, 17, 1, 1, 20 })
        {
            game.AdvanceFixedSteps(steps);
            resumed.AdvanceFixedSteps(steps);
            Assert.Equal(
                GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata),
                GamePersistence.Serialize(resumed, Milestone3ProofFixture.Metadata)
            );
        }
    }

    /// <summary>A load during cooldown rejects early fire and accepts its exact readiness boundary.</summary>
    [Fact]
    public void RestoredCooldownRejectsEarlyThenAcceptsExactReady()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(15);
        SimulationState state = game.CaptureState();
        ShipState npc = state.GetRequiredShip(new ShipInstanceId(2));
        game = GameSimulation.RestoreState(
            state.ReplaceShip(npc.InstanceId, npc with { SensorKnowledge = SensorKnowledge.Empty }),
            fixture.Catalog
        );
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors)).Outcome
        );
        GameSimulation restored = fixture.RoundTrip(game, "cooldown.json");
        restored.AdvanceFixedSteps(19);
        byte[] before = GamePersistence.Serialize(restored, Milestone3ProofFixture.Metadata);
        Assert.Equal(
            FireDirectedEnergyOutcome.CooldownActive,
            restored.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors)).Outcome
        );
        Assert.Equal(before, GamePersistence.Serialize(restored, Milestone3ProofFixture.Metadata));
        restored.AdvanceFixedSteps(1);
        Assert.Equal(
            FireDirectedEnergyOutcome.Accepted,
            restored.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors)).Outcome
        );
    }

    /// <summary>Both newly repairable systems retain their exact work identity and analytical completion.</summary>
    [Theory]
    [InlineData("shields")]
    [InlineData("directed-energy-weapons")]
    public void ActiveCombatRepairRoundTripsAndCompletes(string systemName)
    {
        (GameSimulation game, Milestone3ProofFixture fixture, _) = Pair(0);
        SimulationState state = game.CaptureState();
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        var system = ShipSystemKind.Parse(systemName);
        player = player with { Engineering = TestEngineering.WithCondition(player.Engineering, system, 0.25) };
        state = state.ReplaceShip(player.InstanceId, player);
        game = GameSimulation.RestoreState(state, fixture.Catalog);
        Assert.Equal(SystemRepairOutcome.Accepted, game.BeginSystemRepair(system, new SystemCondition(1)).Outcome);
        game.AdvanceFixedSteps(7);
        GameSimulation restored = fixture.RoundTrip(game, "combat-repair.json");
        Assert.Equal(
            game.CaptureState().GetRequiredShip(player.InstanceId).Engineering.ActiveRepair,
            restored.CaptureState().GetRequiredShip(player.InstanceId).Engineering.ActiveRepair
        );
        game.AdvanceFixedSteps(80);
        restored.AdvanceFixedSteps(80);
        Assert.Equal(
            GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata),
            GamePersistence.Serialize(restored, Milestone3ProofFixture.Metadata)
        );
        Assert.Null(restored.CaptureState().GetRequiredShip(player.InstanceId).Engineering.ActiveRepair);
        Assert.Equal(
            1,
            TestEngineering.ConditionOf(restored.CaptureState().GetRequiredShip(player.InstanceId).Engineering, system)
        );
    }

    /// <summary>Adjacent V8 migration preserves every existing field and supplies only offline combat defaults.</summary>
    [Fact]
    public void V8MigrationDoesNotInventCombatOrRewriteExistingState()
    {
        var fixture = new Milestone3ProofFixture();
        GameSimulation game = fixture.CreateDefault();
        JsonObject historical = Parse(GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata));
        StripCombat(historical);
        historical["schemaVersion"] = 8;
        historical["simulationRulesVersion"] = "observation-driven-faction-response-v1";
        byte[] source = Encode(historical);
        GameSimulation migrated = GamePersistence.Deserialize(source, fixture.Catalog, "historical-v8.json").Simulation;
        foreach (ShipState ship in migrated.CaptureState().Ships)
        {
            Assert.Equal(0, TestEngineering.ConditionOf(ship.Engineering, ShipSystemKind.Shields));
            Assert.Equal(0, TestEngineering.ConditionOf(ship.Engineering, ShipSystemKind.DirectedEnergyWeapons));
            Assert.Equal(0, TestEngineering.AllocationOf(ship.Engineering, ShipSystemKind.Shields));
            Assert.Equal(0, TestEngineering.AllocationOf(ship.Engineering, ShipSystemKind.DirectedEnergyWeapons));
            Assert.Equal(ShipCombatState.InitialFor(ship.Engineering.Systems), ship.Combat);
        }
        Assert.DoesNotContain(
            migrated.CaptureState().Scheduler.OutstandingWork,
            work => work.Kind == ScheduledWorkKind.ShipCombatDecisionWake
        );
        JsonObject projected = Parse(GamePersistence.Serialize(migrated, Milestone3ProofFixture.Metadata));
        StripCombat(projected);
        projected["schemaVersion"] = 8;
        projected["simulationRulesVersion"] = "observation-driven-faction-response-v1";
        Assert.True(JsonNode.DeepEquals(historical, projected));
    }

    /// <summary>Malformed combat additions reject without changing an existing aggregate.</summary>
    [Theory]
    [InlineData("missingCombat")]
    [InlineData("nullCombat")]
    [InlineData("missingEngineeringField")]
    [InlineData("extraCombatField")]
    [InlineData("nullReady")]
    [InlineData("negativeReady")]
    [InlineData("unalignedReady")]
    [InlineData("futureReady")]
    [InlineData("shieldCondition")]
    [InlineData("weaponCondition")]
    [InlineData("negativeAllocation")]
    [InlineData("overDemand")]
    [InlineData("overAvailable")]
    [InlineData("missingStimulusField")]
    [InlineData("nullContact")]
    [InlineData("invalidContact")]
    [InlineData("futureObserved")]
    [InlineData("unalignedObserved")]
    [InlineData("wrongDue")]
    [InlineData("wrongWorkId")]
    [InlineData("wrongWorkKind")]
    [InlineData("wrongOwner")]
    [InlineData("wrongDomain")]
    [InlineData("duplicateWork")]
    [InlineData("orphanWork")]
    [InlineData("playerStimulus")]
    [InlineData("nullShip")]
    [InlineData("nullWork")]
    public void RejectsMalformedV9(string mutation)
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(15);
        game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors));
        byte[] valid = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        JsonObject root = Parse(valid);
        JsonObject npc = root["simulation"]!["ships"]![1]!.AsObject();
        if (!TryMutateEngineeringV9(npc, mutation))
            MutateStimulusV9(root, mutation);
        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(Encode(root), fixture.Catalog, mutation + ".json")
        );
        Assert.Equal(valid, GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata));
    }

    /// <summary>Malformed current knowledge rejects at load without mutating the live aggregate.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void RejectsCurrentContactWithRemoteTargetWithoutChangingLiveGame(bool legacyFrame)
    {
        (GameSimulation game, Milestone3ProofFixture fixture, _) = Pair(0);
        byte[] valid = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        JsonObject root = Parse(valid);
        JsonObject simulation = root["simulation"]!.AsObject();
        JsonObject remote = simulation["strategicMap"]!["locations"]![0]!.DeepClone().AsObject();
        remote["id"] = "remote";
        simulation["strategicMap"]!["locations"]!.AsArray().Add(remote);
        JsonObject npc = simulation["ships"]![1]!.AsObject();
        npc["strategicState"]!["locationId"] = "remote";
        npc["sensorKnowledge"]!["contacts"]!.AsArray().Clear();
        if (legacyFrame)
            simulation["ships"]![0]!["sensorKnowledge"]!["contacts"]![0]!["observedAtLocationId"] = null;
        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(Encode(root), fixture.Catalog, "remote-current-contact.json")
        );
        Assert.Equal(valid, GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata));
    }

    private static bool TryMutateEngineeringV9(JsonObject npc, string mutation)
    {
        JsonObject combat = npc["combat"]!.AsObject();
        JsonObject engineering = npc["engineering"]!.AsObject();
        long now = combat["pendingStimulus"]!["observedAtMilliseconds"]!.GetValue<long>();
        switch (mutation)
        {
            case "missingCombat":
                npc.Remove("combat");
                break;
            case "nullCombat":
                npc["combat"] = null;
                break;
            case "missingEngineeringField":
                engineering.Remove("shieldCondition");
                break;
            case "extraCombatField":
                combat["attackerShipId"] = 1;
                break;
            case "nullReady":
                combat["nextDirectedEnergyReadyAtMilliseconds"] = null;
                break;
            case "negativeReady":
                combat["nextDirectedEnergyReadyAtMilliseconds"] = -100;
                break;
            case "unalignedReady":
                combat["nextDirectedEnergyReadyAtMilliseconds"] = now + 1;
                break;
            case "futureReady":
                combat["nextDirectedEnergyReadyAtMilliseconds"] = now + 2100;
                break;
            case "shieldCondition":
                engineering["shieldCondition"] = 1.01;
                break;
            case "weaponCondition":
                engineering["directedEnergyCondition"] = -0.01;
                break;
            case "negativeAllocation":
                engineering["shieldAllocation"] = -1;
                break;
            case "overDemand":
                engineering["shieldAllocation"] = 41;
                break;
            case "overAvailable":
                engineering["shieldAllocation"] = 40;
                break;
            default:
                return false;
        }
        return true;
    }

    private static void MutateStimulusV9(JsonObject root, string mutation)
    {
        JsonArray ships = root["simulation"]!["ships"]!.AsArray();
        JsonObject combat = ships[1]!["combat"]!.AsObject();
        JsonObject stimulus = combat["pendingStimulus"]!.AsObject();
        JsonArray work = root["simulation"]!["scheduler"]!["outstandingWork"]!.AsArray();
        JsonObject wake = work.Select(node => node!.AsObject())
            .Single(node =>
                string.Equals(node["kind"]!.GetValue<string>(), "shipCombatDecisionWake", StringComparison.Ordinal)
            );
        long now = root["simulation"]!["timeMilliseconds"]!.GetValue<long>();
        switch (mutation)
        {
            case "missingStimulusField":
                stimulus.Remove("scheduledWorkId");
                break;
            case "nullContact":
                stimulus["contactId"] = null;
                break;
            case "invalidContact":
                stimulus["contactId"] = 999;
                break;
            case "futureObserved":
                stimulus["observedAtMilliseconds"] = now + 100;
                break;
            case "unalignedObserved":
                stimulus["observedAtMilliseconds"] = now - 1;
                break;
            case "wrongDue":
                stimulus["dueTimeMilliseconds"] = now + 200;
                break;
            case "wrongWorkId":
                stimulus["scheduledWorkId"] = 1;
                break;
            case "wrongWorkKind":
                wake["kind"] = "shipContactDecisionWake";
                break;
            case "wrongOwner":
                wake["targetShipId"] = 1;
                break;
            case "wrongDomain":
                wake["targetKind"] = "faction";
                break;
            case "duplicateWork":
                work.Add(wake.DeepClone());
                break;
            case "orphanWork":
                combat["pendingStimulus"] = null;
                break;
            case "playerStimulus":
                ships[0]!["combat"]!["pendingStimulus"] = stimulus.DeepClone();
                break;
            case "nullShip":
                ships[1] = null;
                break;
            case "nullWork":
                work.Add((JsonNode?)null);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }
    }

    /// <summary>Frozen V8 admits neither new repair targets nor the combat work token.</summary>
    [Theory]
    [InlineData("shields")]
    [InlineData("directed-energy-weapons")]
    [InlineData("shipCombatDecisionWake")]
    public void HistoricalV8RejectsNewSemantics(string token)
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(15);
        if (string.Equals(token, "shipCombatDecisionWake", StringComparison.Ordinal))
            game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors));
        else
        {
            var system = ShipSystemKind.Parse(token);
            SimulationState state = game.CaptureState();
            ShipState player = state.GetRequiredShip(state.PlayerShipId);
            player = player with { Engineering = TestEngineering.WithCondition(player.Engineering, system, 0.25) };
            game = GameSimulation.RestoreState(state.ReplaceShip(player.InstanceId, player), fixture.Catalog);
            game.BeginSystemRepair(system, new SystemCondition(1));
        }
        JsonObject historical = Parse(GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata));
        StripCombat(historical);
        historical["schemaVersion"] = 8;
        historical["simulationRulesVersion"] = "observation-driven-faction-response-v1";
        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(Encode(historical), fixture.Catalog, "unsupported-v8.json")
        );
    }

    /// <summary>New repair correlations and timing remain strict for both combat consumers.</summary>
    [Theory]
    [InlineData("shields")]
    [InlineData("directed-energy-weapons")]
    public void RejectsMalformedCombatRepairs(string systemName)
    {
        (GameSimulation game, Milestone3ProofFixture fixture, _) = Pair(15);
        var system = ShipSystemKind.Parse(systemName);
        SimulationState state = game.CaptureState();
        ShipState player = state.GetRequiredShip(state.PlayerShipId);
        player = player with { Engineering = TestEngineering.WithCondition(player.Engineering, system, 0.25) };
        game = GameSimulation.RestoreState(state.ReplaceShip(player.InstanceId, player), fixture.Catalog);
        Assert.Equal(SystemRepairOutcome.Accepted, game.BeginSystemRepair(system, new SystemCondition(1)).Outcome);
        byte[] valid = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        foreach (string mutation in new[] { "workId", "dueTime", "duration", "condition", "target", "owner", "orphan" })
        {
            JsonObject root = Parse(valid);
            JsonObject engineering = root["simulation"]!["ships"]![0]!["engineering"]!.AsObject();
            JsonObject repair = engineering["activeRepair"]!.AsObject();
            JsonObject work = root["simulation"]!["scheduler"]!["outstandingWork"]!
                .AsArray()
                .Select(node => node!.AsObject())
                .Single(node =>
                    string.Equals(node["kind"]!.GetValue<string>(), "systemRepairCompletion", StringComparison.Ordinal)
                );
            switch (mutation)
            {
                case "workId":
                    repair["scheduledCompletionId"] = 999;
                    break;
                case "dueTime":
                    work["dueTimeMilliseconds"] = work["dueTimeMilliseconds"]!.GetValue<long>() + 100;
                    break;
                case "duration":
                    repair["expectedCompletionMilliseconds"] =
                        repair["expectedCompletionMilliseconds"]!.GetValue<long>() + 100;
                    break;
                case "condition":
                    engineering[
                        string.Equals(systemName, "shields", StringComparison.Ordinal)
                            ? "shieldCondition"
                            : "directedEnergyCondition"
                    ] = 0.5;
                    break;
                case "target":
                    repair["targetSystem"] = "power-generation";
                    break;
                case "owner":
                    work["targetShipId"] = 2;
                    break;
                case "orphan":
                    engineering["activeRepair"] = null;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(systemName));
            }
            Assert.Throws<GamePersistenceException>(() =>
                GamePersistence.Deserialize(Encode(root), fixture.Catalog, "corrupt-combat-repair.json")
            );
            Assert.Equal(valid, GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata));
        }
    }

    /// <summary>Required V9 additions cannot be omitted, even when their legal value is null or zero.</summary>
    [Fact]
    public void RequiresEveryNewField()
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(15);
        game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors));
        byte[] valid = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        foreach (
            (string parent, string field) in new[]
            {
                ("engineering", "shieldCondition"),
                ("engineering", "directedEnergyCondition"),
                ("engineering", "shieldAllocation"),
                ("engineering", "directedEnergyAllocation"),
                ("combat", "nextDirectedEnergyReadyAtMilliseconds"),
                ("combat", "pendingStimulus"),
                ("stimulus", "contactId"),
                ("stimulus", "observedAtMilliseconds"),
                ("stimulus", "dueTimeMilliseconds"),
                ("stimulus", "scheduledWorkId"),
            }
        )
        {
            JsonObject root = Parse(valid);
            JsonObject ship = root["simulation"]!["ships"]![1]!.AsObject();
            JsonObject target = string.Equals(parent, "stimulus", StringComparison.Ordinal)
                ? ship["combat"]!["pendingStimulus"]!.AsObject()
                : ship[parent]!.AsObject();
            target.Remove(field);
            Assert.Throws<GamePersistenceException>(() =>
                GamePersistence.Deserialize(Encode(root), fixture.Catalog, "missing-v9-field.json")
            );
        }
    }

    /// <summary>Strict JSON rejects duplicate members, non-finite numbers, and primitive overflow before restoration.</summary>
    [Theory]
    [InlineData("duplicate")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e309")]
    [InlineData("-1e309")]
    [InlineData("null")]
    [InlineData("allocationOverflow")]
    [InlineData("readyOverflow")]
    public void RejectsMalformedCombatJson(string mutation)
    {
        (GameSimulation game, Milestone3ProofFixture fixture, SensorContactId contact) = Pair(15);
        game.FireDirectedEnergy(new(contact, ShipSystemKind.Sensors));
        byte[] valid = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        string json = Encoding.UTF8.GetString(valid);
        json = mutation switch
        {
            "duplicate" => json.Replace(
                "\"shieldCondition\":0.75",
                "\"shieldCondition\":0.75,\"shieldCondition\":0.75",
                StringComparison.Ordinal
            ),
            "allocationOverflow" => json.Replace(
                "\"shieldAllocation\":15",
                "\"shieldAllocation\":2147483648",
                StringComparison.Ordinal
            ),
            "readyOverflow" => json.Replace(
                "\"nextDirectedEnergyReadyAtMilliseconds\":0",
                "\"nextDirectedEnergyReadyAtMilliseconds\":9223372036854775808",
                StringComparison.Ordinal
            ),
            _ => json.Replace("\"shieldCondition\":0.75", "\"shieldCondition\":" + mutation, StringComparison.Ordinal),
        };
        Assert.False(string.Equals(Encoding.UTF8.GetString(valid), json, StringComparison.Ordinal));
        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(Encoding.UTF8.GetBytes(json), fixture.Catalog, "malformed-v9-json.json")
        );
        Assert.Equal(valid, GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata));
    }

    internal static void StripCombat(JsonObject root)
    {
        foreach (JsonNode? node in root["simulation"]!["ships"]!.AsArray())
        {
            JsonObject ship = node!.AsObject();
            ship.Remove("combat");
            JsonObject engineering = ship["engineering"]!.AsObject();
            foreach (
                string field in new[]
                {
                    "shieldCondition",
                    "directedEnergyCondition",
                    "shieldAllocation",
                    "directedEnergyAllocation",
                }
            )
                engineering.Remove(field);
        }
    }

    private static JsonObject Parse(byte[] json) => JsonNode.Parse(json)!.AsObject();

    private static byte[] Encode(JsonObject root) => Encoding.UTF8.GetBytes(root.ToJsonString());

    private static (GameSimulation Game, Milestone3ProofFixture Fixture, SensorContactId Contact) Pair(int shieldPower)
    {
        var fixture = new Milestone3ProofFixture();
        var location = new LocationId("pair");
        var map = new StrategicMap([new StrategicLocation(location, "Pair", default)], []);
        ShipStart Start(long id, double x, int impulsePower, int shieldPower) =>
            new(
                new ShipInstanceId(id),
                new ShipDefinitionId("pathfinder"),
                "Ship " + id,
                new TacticalPosition(x, 0),
                default,
                new AtLocationStart(location),
                TestShipStarts.Pathfinder(
                    impulsePower: impulsePower,
                    shieldPower: shieldPower,
                    weaponPower: 30,
                    shields: 1,
                    weapons: 1
                )
            );
        GameSimulation game = new GameBootstrap(
            new SimulationTime(6000),
            map,
            new ShipInstanceId(1),
            [Start(1, 0, impulsePower: 20, shieldPower: 0), Start(2, 10, impulsePower: 5, shieldPower: shieldPower)]
        ).CreateSimulation(fixture.Catalog);
        SimulationState initial = game.CaptureState();
        ShipState npc = initial.GetRequiredShip(new ShipInstanceId(2));
        game = GameSimulation.RestoreState(
            initial.ReplaceShip(
                npc.InstanceId,
                npc with
                {
                    AutonomousState = npc.AutonomousState with { ContactPosture = ShipContactPosture.CautiousContact },
                }
            ),
            fixture.Catalog
        );
        game.AdvanceFixedSteps(1);
        SensorContactId contact = game.GetPlayerProjection().Ship.Sensors.Contacts.Single().Id;
        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(contact).Outcome);
        game.AdvanceFixedSteps(20);
        Assert.Equal(HailOutcome.Acknowledged, game.RequestHail(contact).Outcome);
        return (game, fixture, contact);
    }
}
