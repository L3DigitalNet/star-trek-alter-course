using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tests.Gameplay;
using FactionTestWorld = AlterCourse.Core.Tests.Gameplay.FactionBootstrapTests.FactionTestWorld;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies the closed V7 faction, controller, objective, and typed-work persistence contract.</summary>
public sealed class GamePersistenceV7FactionTests
{
    /// <summary>Exact wire correlation cannot postpone an initial decision to an arbitrary future time.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CorrelatedPendingWakeRequiresAMeaningfulStrategicBoundary(bool anotherShipHasFutureRelease)
    {
        GameSimulation live = FactionTestWorld
            .CreateBootstrap(
                [new FactionStart(FactionTestWorld.FactionA, FactionTestWorld.DefinitionA, FactionTestWorld.Beta)],
                controlledShips: true,
                firstNpcOrder: anotherShipHasFutureRelease
                    ? new HoldUntilOrderStart(new SimulationTime(86_400_000))
                    : null
            )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
        byte[] before = GamePersistence.Serialize(live, Milestone3ProofFixture.Metadata);
        byte[] corrupted = Mutate(
            before,
            root =>
            {
                JsonNode simulation = root["simulation"]!;
                simulation["factions"]![0]!["pendingDecisionWake"]!["dueTimeMilliseconds"] = 86_400_000L;
                JsonArray work = simulation["scheduler"]!["outstandingWork"]!.AsArray();
                JsonNode wake = Assert.Single(
                    work,
                    item => string.Equals(item!["targetKind"]!.GetValue<string>(), "faction", StringComparison.Ordinal)
                )!;
                wake["dueTimeMilliseconds"] = 86_400_000L;
                simulation["scheduler"]!["outstandingWork"] = new JsonArray(
                    work.OrderBy(item => item!["dueTimeMilliseconds"]!.GetValue<long>())
                        .ThenBy(item => item!["sequence"]!.GetValue<long>())
                        .Select(item => item!.DeepClone())
                        .ToArray()
                );
            }
        );

        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(
                corrupted,
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog,
                "postponed-pending-wake.json"
            )
        );

        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.Equal(before, GamePersistence.Serialize(live, Milestone3ProofFixture.Metadata));
    }

    /// <summary>Removing both sides of a pending wake correlation must not silently disable an actionable objective.</summary>
    [Fact]
    public void MissingPendingDecisionContinuationFailsWithoutReplacingLiveState()
    {
        GameSimulation live = FactionTestWorld
            .CreateBootstrap(
                [new FactionStart(FactionTestWorld.FactionA, FactionTestWorld.DefinitionA, FactionTestWorld.Beta)],
                controlledShips: true
            )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
        byte[] before = GamePersistence.Serialize(live, Milestone3ProofFixture.Metadata);
        byte[] corrupted = Mutate(
            before,
            root =>
            {
                JsonNode simulation = root["simulation"]!;
                simulation["factions"]![0]!["pendingDecisionWake"] = null;
                JsonArray work = simulation["scheduler"]!["outstandingWork"]!.AsArray();
                JsonNode? wake = Assert.Single(
                    work,
                    item => string.Equals(item!["targetKind"]!.GetValue<string>(), "faction", StringComparison.Ordinal)
                );
                Assert.True(work.Remove(wake));
            }
        );

        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(
                corrupted,
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog,
                "missing-pending-continuation.json"
            )
        );

        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.Equal(before, GamePersistence.Serialize(live, Milestone3ProofFixture.Metadata));
    }

    /// <summary>Confirms a midflight faction commitment round trips and continues byte-identically.</summary>
    [Fact]
    public void AssignedFactionRoundTripsAndContinuesDeterministically()
    {
        GameSimulation uninterrupted = CreateAssignedGame();
        byte[] saved = GamePersistence.Serialize(uninterrupted, Milestone3ProofFixture.Metadata);
        JsonObject root = Parse(saved);

        LoadedGameSave loaded = GamePersistence.Deserialize(
            saved,
            FactionTestWorld.ShipCatalog,
            FactionTestWorld.FactionCatalog,
            "assigned-v7.json"
        );

        Assert.Equal(7, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("faction-intent-autonomous-assignment-v1", root["simulationRulesVersion"]!.GetValue<string>());
        Assert.Equal(2, root["simulation"]!["factions"]!.AsArray().Count);
        Assert.Contains(
            root["simulation"]!["scheduler"]!["outstandingWork"]!.AsArray(),
            work => string.Equals(work!["targetKind"]!.GetValue<string>(), "faction", StringComparison.Ordinal)
        );
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));

        uninterrupted.AdvanceFixedSteps(10);
        loaded.Simulation.AdvanceFixedSteps(10);
        Assert.Equal(
            GamePersistence.Serialize(uninterrupted, loaded.Metadata),
            GamePersistence.Serialize(loaded.Simulation, loaded.Metadata)
        );
    }

    /// <summary>Confirms V6 migrates as an actual faction-free historical document without inferred politics.</summary>
    [Fact]
    public void HistoricalV6MigratesWithShipTargetsAndNoFactionState()
    {
        GameSimulation legacyWorld = new Milestone3ProofFixture().CreateDefault();
        byte[] v6 = ToHistoricalV6(GamePersistence.Serialize(legacyWorld, Milestone3ProofFixture.Metadata));

        LoadedGameSave loaded = GamePersistence.Deserialize(
            v6,
            new Milestone3ProofFixture().Catalog,
            FactionTestWorld.FactionCatalog,
            "historical-v6.json"
        );
        JsonObject normalized = Parse(GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));

        Assert.Empty(loaded.Simulation.CaptureState().Factions);
        Assert.All(loaded.Simulation.CaptureState().Ships, ship => Assert.Null(ship.DirectControllerFactionId));
        Assert.Empty(normalized["simulation"]!["factions"]!.AsArray());
        Assert.All(
            normalized["simulation"]!["scheduler"]!["outstandingWork"]!.AsArray(),
            work => Assert.Equal("ship", work!["targetKind"]!.GetValue<string>())
        );
        Assert.Equal(v6, ToHistoricalV6(GamePersistence.Serialize(loaded.Simulation, loaded.Metadata)));
    }

    /// <summary>Confirms hostile V7 graph mutations fail before the source aggregate can change.</summary>
    [Theory]
    [InlineData("duplicate-faction")]
    [InlineData("missing-definition")]
    [InlineData("missing-controller")]
    [InlineData("missing-location")]
    [InlineData("missing-objective")]
    [InlineData("invalid-status")]
    [InlineData("assigned-ship")]
    [InlineData("assigned-order")]
    [InlineData("unknown-target-kind")]
    [InlineData("both-targets")]
    [InlineData("missing-target")]
    [InlineData("missing-faction-target")]
    [InlineData("wrong-domain")]
    [InlineData("incompatible-work-kind")]
    [InlineData("wake-work")]
    [InlineData("wake-time")]
    [InlineData("duplicate-work")]
    [InlineData("invalid-counter")]
    [InlineData("null-work")]
    [InlineData("null-factions")]
    [InlineData("unknown-member")]
    [InlineData("too-many-factions")]
    public void RejectsInvalidFactionGraphsAtomically(string mutation)
    {
        GameSimulation live = CreateAssignedGame();
        byte[] before = GamePersistence.Serialize(live, Milestone3ProofFixture.Metadata);
        byte[] invalid = Mutate(before, root => ApplyMutation(root, mutation));

        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(
                invalid,
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog,
                $"{mutation}.json"
            )
        );

        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.Equal(before, GamePersistence.Serialize(live, Milestone3ProofFixture.Metadata));
    }

    /// <summary>Confirms legacy ship-only APIs remain compatible only with faction-free saves.</summary>
    [Fact]
    public void ShipOnlyDeserializeRejectsFactionSaveButLoadsMigratedV6()
    {
        byte[] factionSave = GamePersistence.Serialize(CreateAssignedGame(), Milestone3ProofFixture.Metadata);
        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(factionSave, FactionTestWorld.ShipCatalog, "missing-faction-catalog.json")
        );

        GameSimulation legacyWorld = new Milestone3ProofFixture().CreateDefault();
        byte[] v6 = ToHistoricalV6(GamePersistence.Serialize(legacyWorld, Milestone3ProofFixture.Metadata));
        LoadedGameSave loaded = GamePersistence.Deserialize(
            v6,
            new Milestone3ProofFixture().Catalog,
            "ship-only-v6.json"
        );
        Assert.Empty(loaded.Simulation.CaptureState().Factions);
    }

    private static GameSimulation CreateAssignedGame()
    {
        GameSimulation initial = FactionTestWorld
            .CreateBootstrap(
                [
                    new FactionStart(FactionTestWorld.FactionA, FactionTestWorld.DefinitionA, FactionTestWorld.Beta),
                    new FactionStart(FactionTestWorld.FactionB, FactionTestWorld.DefinitionB),
                ],
                controlledShips: true
            )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
        SimulationState assigned = GameSimulation
            .AdvanceTo(
                initial.CaptureState(),
                new SimulationTime(0),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        return GameSimulation.RestoreState(assigned, FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
    }

    private static void ApplyMutation(JsonObject root, string mutation)
    {
        JsonObject simulation = root["simulation"]!.AsObject();
        JsonArray factions = simulation["factions"]!.AsArray();
        JsonObject faction = factions[0]!.AsObject();
        JsonObject objective = faction["presenceObjective"]!.AsObject();
        JsonArray work = simulation["scheduler"]!["outstandingWork"]!.AsArray();
        JsonObject factionWork = work.Single(item =>
                    string.Equals(item!["targetKind"]!.GetValue<string>(), "faction", StringComparison.Ordinal)
                )!
            .AsObject();
        if (ApplyWorkMutation(factionWork, mutation))
        {
            return;
        }
        if (ApplyAggregateMutation(simulation, factions, faction, work, factionWork, mutation))
        {
            return;
        }

        switch (mutation)
        {
            case "duplicate-faction":
                factions.Add(faction.DeepClone());
                break;
            case "missing-definition":
                faction["definitionId"] = "missing";
                break;
            case "missing-controller":
                simulation["ships"]![1]!["directControllerFactionId"] = 99;
                break;
            case "missing-location":
                objective["targetLocationId"] = "missing";
                break;
            case "missing-objective":
                faction["presenceObjective"] = null;
                break;
            case "invalid-status":
                objective["status"] = "unknown";
                break;
            case "assigned-ship":
                objective["assignedShipId"] = 4;
                break;
            case "assigned-order":
                objective["assignedOrderId"] = 999;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
        }
    }

    private static bool ApplyAggregateMutation(
        JsonObject simulation,
        JsonArray factions,
        JsonObject faction,
        JsonArray work,
        JsonObject factionWork,
        string mutation
    )
    {
        switch (mutation)
        {
            case "wake-work":
                faction["pendingDecisionWake"]!["workId"] = 999;
                return true;
            case "wake-time":
                faction["pendingDecisionWake"]!["dueTimeMilliseconds"] = 900;
                return true;
            case "duplicate-work":
                work.Add(factionWork.DeepClone());
                return true;
            case "invalid-counter":
                simulation["scheduler"]!["nextWorkId"] = 0;
                return true;
            case "null-work":
                simulation["scheduler"]!["outstandingWork"] = null;
                return true;
            case "null-factions":
                simulation["factions"] = null;
                return true;
            case "unknown-member":
                faction["explanation"] = "derived";
                return true;
            case "too-many-factions":
                AddTooManyFactions(factions, faction);
                return true;
            default:
                return false;
        }
    }

    private static void AddTooManyFactions(JsonArray factions, JsonObject template)
    {
        for (int identity = 3; identity <= 257; identity++)
        {
            JsonObject extra = template.DeepClone().AsObject();
            extra["id"] = identity;
            factions.Add(extra);
        }
    }

    private static bool ApplyWorkMutation(JsonObject factionWork, string mutation)
    {
        switch (mutation)
        {
            case "unknown-target-kind":
                factionWork["targetKind"] = "unknown";
                return true;
            case "both-targets":
                factionWork["targetShipId"] = 2;
                return true;
            case "missing-target":
                factionWork["targetFactionId"] = null;
                return true;
            case "missing-faction-target":
                factionWork["targetFactionId"] = 99;
                return true;
            case "wrong-domain":
                factionWork["targetKind"] = "ship";
                factionWork["targetShipId"] = 1;
                factionWork["targetFactionId"] = null;
                return true;
            case "incompatible-work-kind":
                factionWork["kind"] = "travelArrival";
                return true;
            default:
                return false;
        }
    }

    private static byte[] ToHistoricalV6(byte[] current) =>
        Mutate(
            current,
            root =>
            {
                root["schemaVersion"] = 6;
                root["simulationRulesVersion"] = "strategic-contact-reporting-v1";
                JsonObject simulation = root["simulation"]!.AsObject();
                simulation.Remove("factions");
                foreach (JsonNode? ship in simulation["ships"]!.AsArray())
                {
                    ship!.AsObject().Remove("directControllerFactionId");
                }
                foreach (JsonNode? work in simulation["scheduler"]!["outstandingWork"]!.AsArray())
                {
                    work!.AsObject().Remove("targetKind");
                    work.AsObject().Remove("targetFactionId");
                }
            }
        );

    private static byte[] Mutate(byte[] source, Action<JsonObject> mutation)
    {
        JsonObject root = Parse(source);
        mutation(root);
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static JsonObject Parse(byte[] json) => JsonNode.Parse(json)!.AsObject();
}
