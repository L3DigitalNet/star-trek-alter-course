using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using AlterCourse.Core.Tests.Gameplay;
using AlterCourse.Core.Tests.Support;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>
/// Verifies V10 component compatibility (definition and aim-vocabulary descriptors) and the strict V9→V10 migration
/// through the frozen, version-qualified map.
/// </summary>
public sealed class GamePersistenceV10CompatibilityTests
{
    private static readonly DateTimeOffset Timestamp = new(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);

    private static readonly GameSaveMetadata Metadata = new("v10-compat", "V10 compatibility", Timestamp, Timestamp);

    private static readonly LocationId Origin = new("origin");

    /// <summary>Changed semantics of a referenced definition fail closed, naming the definition.</summary>
    [Fact]
    public void ChangedReferencedDefinitionIsIncompatibleContent()
    {
        ShipDefinitionCatalog original = TestShipContent.Pathfinder();
        GameSimulation live = CreateShieldless(original);
        byte[] saved = GamePersistence.Serialize(live, Metadata);
        ShipDefinitionCatalog retuned = TestShipContent.Pathfinder(
            PathfinderTuning.Production with
            {
                PassiveRange = 31,
            }
        );

        GamePersistenceException failure = AssertIncompatible(saved, retuned);

        Assert.Contains("pathfinder.sensors", failure.Message, StringComparison.Ordinal);
        Assert.Contains("passiveRangeKm=31", failure.Message, StringComparison.Ordinal);
        Assert.Equal(saved, GamePersistence.Serialize(live, Metadata));
    }

    /// <summary>
    /// Catalog-only changes that alter the aim vocabulary fail closed even though every referenced definition is
    /// unchanged: (i) adding a kind, (ii) removing the only definition of a kind, (iii) reordering through an
    /// uninstalled definition.
    /// </summary>
    [Theory]
    [InlineData("add-kind")]
    [InlineData("remove-kind")]
    [InlineData("reorder")]
    public void CatalogOnlyAimVocabularyChangeIsIncompatibleContent(string change)
    {
        SystemDefinition[] all = TestShipContent.PathfinderSystems();
        SystemDefinition[] withoutShields = [.. all.Where(definition => definition.Kind != ShipSystemKind.Shields)];
        SystemDefinition shields = all.Single(definition => definition.Kind == ShipSystemKind.Shields);
        ShipDefinitionCatalog saveCatalog = string.Equals(change, "add-kind", StringComparison.Ordinal)
            ? CatalogOf(withoutShields)
            : CatalogOf(all, withoutShields);
        ShipDefinitionCatalog loadCatalog = change switch
        {
            "add-kind" => CatalogOf(all, withoutShields),
            "remove-kind" => CatalogOf(withoutShields),
            _ => CatalogOf([.. withoutShields, Reordered((ShieldSystemDefinition)shields, 50)], withoutShields),
        };
        byte[] saved = GamePersistence.Serialize(CreateShieldless(saveCatalog), Metadata);

        GamePersistenceException failure = AssertIncompatible(saved, loadCatalog);

        Assert.Contains("aim vocabulary", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>An uninstalled definition of an already-present kind at a higher order leaves the vocabulary alone.</summary>
    [Fact]
    public void AddingSameKindDefinitionAtHigherOrderIsCompatible()
    {
        SystemDefinition[] all = TestShipContent.PathfinderSystems();
        var extraSensors = (SensorSystemDefinition)
            TestShipContent
                .PathfinderSystems(prefix: "test.extra")
                .Single(definition => definition.Kind == ShipSystemKind.Sensors);
        extraSensors = new SensorSystemDefinition(
            extraSensors.Id,
            extraSensors.ComponentLabel,
            250,
            true,
            extraSensors.Repair,
            extraSensors.Power!,
            extraSensors.PassiveRange,
            extraSensors.ActiveScanDuration
        );
        byte[] saved = GamePersistence.Serialize(CreateShieldless(CatalogOf(all)), Metadata);

        LoadedGameSave loaded = GamePersistence.Deserialize(
            saved,
            CatalogOf([.. all, extraSensors], all),
            "extra.json"
        );

        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
    }

    /// <summary>A referenced definition absent from the supplied content fails closed as incompatible content.</summary>
    [Fact]
    public void AbsentReferencedDefinitionIsIncompatibleContent()
    {
        SystemDefinition[] all = TestShipContent.PathfinderSystems();
        byte[] saved = GamePersistence.Serialize(CreateShieldless(CatalogOf(all)), Metadata);
        JsonObject root = JsonNode.Parse(saved)!.AsObject();
        foreach (JsonNode? row in root["simulation"]!["systemDefinitions"]!.AsArray())
            row!["definitionId"] = row["definitionId"]!
                .GetValue<string>()
                .Replace("pathfinder.", "retired.", StringComparison.Ordinal);
        foreach (JsonNode? ship in root["simulation"]!["ships"]!.AsArray())
        foreach (JsonNode? system in ship!["engineering"]!["installedSystems"]!.AsArray())
            system!["definitionId"] = system["definitionId"]!
                .GetValue<string>()
                .Replace("pathfinder.", "retired.", StringComparison.Ordinal);

        GamePersistenceException failure = AssertIncompatible(
            Encoding.UTF8.GetBytes(root.ToJsonString()),
            CatalogOf(all)
        );

        Assert.Contains("'absent'", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Strict V9 conversion: a V9 document of a production world migrates to exactly the V10 document the same world
    /// writes directly — every installation, allocation, repair, scan, readiness, allocator, and scheduler value.
    /// </summary>
    [Fact]
    public void V9ConvertsToTheExactExpectedV10Snapshot()
    {
        var fixture = new Milestone3ProofFixture();
        GameSimulation game = fixture.CreateDefault();
        game.AdvanceFixedSteps(12);
        byte[] expected = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        JsonObject v9 = JsonNode.Parse(expected)!.AsObject();
        SaveJsonV10.ToV9(v9);

        LoadedGameSave loaded = GamePersistence.Deserialize(
            Encoding.UTF8.GetBytes(v9.ToJsonString()),
            fixture.Catalog,
            "v9.json"
        );

        byte[] converted = GamePersistence.Serialize(loaded.Simulation, loaded.Metadata);
        Assert.Equal(Encoding.UTF8.GetString(expected), Encoding.UTF8.GetString(converted));
        JsonNode player = JsonNode.Parse(converted)!["simulation"]!["ships"]![0]!;
        Assert.Equal(6, player["engineering"]!["nextInstalledSystemId"]!.GetValue<long>());
        Assert.Equal(2, player["engineering"]!["activeRepair"]!["targetInstalledSystemId"]!.GetValue<long>());
        Assert.Equal(5, player["combat"]!["directedEnergyReadiness"]![0]!["weaponInstalledSystemId"]!.GetValue<long>());
    }

    /// <summary>
    /// A V9 ship whose design has no frozen history fails closed as incompatible content with the actionable
    /// historical-tuning diagnostic.
    /// </summary>
    /// <remarks>
    /// V9 input passes the V1–V9 validators before the V9→V10 map runs, and those validators already need the frozen
    /// <c>HistoricalShipContentV5</c> tuning for every ship, so the reachable refusal is that table's message. The
    /// map's own ship-naming message cannot be reached by a document that names an unknown design; this test pins
    /// which boundary actually refuses so a reordering of the chain is noticed.
    /// </remarks>
    [Fact]
    public void V9ShipWithoutFrozenMappingIsIncompatibleContent()
    {
        var fixture = new Milestone3ProofFixture();
        JsonObject v9 = JsonNode
            .Parse(GamePersistence.Serialize(fixture.CreateDefault(), Milestone3ProofFixture.Metadata))!
            .AsObject();
        SaveJsonV10.ToV9(v9);
        v9["simulation"]!["ships"]![2]!["definitionId"] = "frigate";

        GamePersistenceException failure = AssertIncompatible(
            Encoding.UTF8.GetBytes(v9.ToJsonString()),
            fixture.Catalog
        );

        Assert.Equal(
            "Save 'incompatible.json' uses historical ship definition 'frigate', which has no frozen V1–V9 tuning; "
                + "load it with a build that supports it or start a new game.",
            failure.Message
        );
        Assert.DoesNotContain("installed-system mapping", failure.Message, StringComparison.Ordinal);
    }

    /// <summary>The frozen V9 map is literal: installed ids 1–5, pathfinder definitions, and pinned descriptors.</summary>
    [Fact]
    public void FrozenV9MapPinsIdsDefinitionsAndProductionDescriptors()
    {
        string[] kinds = ["power-generation", "sensors", "impulse-propulsion", "shields", "directed-energy-weapons"];
        SystemDefinitionCatalog production = TestShipContent.ProductionSystems();
        for (int index = 0; index < kinds.Length; index++)
        {
            Assert.Equal(index + 1, HistoricalShipSystemsV9.InstalledIdFor("pathfinder", kinds[index]).Value);
            string definitionId = HistoricalShipSystemsV9.DefinitionIdFor("pathfinder", kinds[index]);
            Assert.Equal("pathfinder." + kinds[index], definitionId);
            Assert.Equal(
                SystemDefinitionSemantics.Describe(production.GetRequired(new SystemDefinitionId(definitionId))),
                HistoricalShipSystemsV9.ExpectedSemanticsFor("pathfinder", kinds[index])
            );
        }

        Assert.Equal(6, HistoricalShipSystemsV9.NextInstalledSystemId);
        Assert.False(HistoricalShipSystemsV9.TryGetRows("test-ship", out _));
        Assert.Throws<SaveContentIncompatibleException>(() =>
            HistoricalShipSystemsV9.InstalledIdFor("test-ship", "sensors")
        );
        Assert.Equal(
            "sd1;kind=sensors;condition=true;order=200;power=70;repair=8000;passiveRangeKm=30;scanMs=2000",
            HistoricalShipSystemsV9.ExpectedSemanticsFor("pathfinder", "sensors")
        );
    }

    /// <summary>The aim-vocabulary descriptor renders list order and rejects malformed input as invalid data.</summary>
    [Fact]
    public void AimVocabularyDescriptorRendersOrderAndValidatesFormat()
    {
        Assert.Equal("av1;", AimVocabularySemantics.Describe([]));
        Assert.Equal(
            AimVocabularySemantics.HistoricalV9,
            AimVocabularySemantics.Describe(TestShipContent.ProductionSystems().DamageTargetKinds)
        );
        Assert.Equal(
            "av1;shields,sensors",
            AimVocabularySemantics.Describe([ShipSystemKind.Shields, ShipSystemKind.Sensors])
        );
        foreach (
            string malformed in new[]
            {
                "",
                "av2;sensors",
                "av1;Sensors",
                "av1;sensors,sensors",
                "av1;warp",
                "av1;sensors;",
            }
        )
            Assert.Throws<InvalidOperationException>(() => AimVocabularySemantics.ValidateFormat(malformed));
        Assert.Throws<InvalidOperationException>(() =>
            AimVocabularySemantics.ValidateFormat("av1;" + new string('a', 1_024))
        );
    }

    private static GamePersistenceException AssertIncompatible(byte[] saved, ShipDefinitionCatalog catalog)
    {
        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(saved, catalog, "incompatible.json")
        );
        Assert.Equal(GamePersistenceFailure.IncompatibleContent, failure.Failure);
        Assert.Contains("start a new game", failure.Message, StringComparison.Ordinal);
        return failure;
    }

    private static ShieldSystemDefinition Reordered(ShieldSystemDefinition shields, int order) =>
        new(shields.Id, shields.ComponentLabel, order, true, shields.Repair, shields.Power!);

    /// <summary>Builds a catalog whose system content and design default loadout are independent.</summary>
    private static ShipDefinitionCatalog CatalogOf(
        SystemDefinition[] systems,
        SystemDefinition[]? designLoadout = null
    ) =>
        TestShipContent.Catalog(
            TestShipContent.Systems(systems),
            TestShipContent.Design("pathfinder", "Pathfinder class", designLoadout ?? systems)
        );

    /// <summary>A two-ship world whose explicit loadouts omit shields, so no shield definition is referenced.</summary>
    private static GameSimulation CreateShieldless(ShipDefinitionCatalog catalog)
    {
        var map = new StrategicMap([new StrategicLocation(Origin, "Origin", default)], []);
        ShipStart Start(long id, double x) =>
            new(
                new ShipInstanceId(id),
                new ShipDefinitionId("pathfinder"),
                "Ship " + id,
                new TacticalPosition(x, 0),
                default,
                new AtLocationStart(Origin),
                TestPairs.Loadout(shields: false, weaponPower: 0)
            );
        return new GameBootstrap(
            new SimulationTime(0),
            map,
            new ShipInstanceId(1),
            [Start(1, 0), Start(2, 5)]
        ).CreateSimulation(catalog);
    }
}
