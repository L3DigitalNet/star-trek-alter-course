using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Tests.Gameplay;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Exercises untrusted historical candidates before migration can dereference their members.</summary>
public sealed class GamePersistenceAdmissionTests
{
    /// <summary>Rejects null scheduler collections and elements through the typed failure boundary.</summary>
    [Theory]
    [InlineData(5, false)]
    [InlineData(6, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void NullHistoricalSchedulerWorkReturnsInvalidDataWithoutChangingLiveWorld(int version, bool nullArray)
    {
        var fixture = new Milestone3ProofFixture();
        GameSimulation live = fixture.CreateDefault();
        byte[] before = GamePersistence.Serialize(live, Milestone3ProofFixture.Metadata);
        JsonObject root = HistoricalDocument(before, version);
        byte[] valid = Encoding.UTF8.GetBytes(root.ToJsonString());
        _ = GamePersistence.Deserialize(valid, fixture.Catalog, "valid-historical.json");
        root["simulation"]!["scheduler"]!["outstandingWork"] = nullArray ? null : new JsonArray((JsonNode?)null);

        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(Encoding.UTF8.GetBytes(root.ToJsonString()), fixture.Catalog, "null-work.json")
        );

        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.Equal(before, GamePersistence.Serialize(live, Milestone3ProofFixture.Metadata));
    }

    /// <summary>Rejects excess records before allocating the document or examining its malformed suffix.</summary>
    [Fact]
    public void ExcessShipsAreRejectedBeforeParsingTheRestOfTheDocument()
    {
        var fixture = new Milestone3ProofFixture();
        // The suffix is intentionally malformed: admission must stop at the first excess array
        // element, before a DOM or DTO is built or the remaining payload is examined.
        string json = "{\"simulation\":{\"ships\":[" + string.Join(',', Enumerable.Repeat("null", 257)) + ",invalid";
        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(Encoding.UTF8.GetBytes(json), fixture.Catalog, "excess-ships.json")
        );

        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.Contains("ships", failure.Message, StringComparison.Ordinal);
        Assert.Contains("256", failure.Message, StringComparison.Ordinal);
    }

    internal static JsonObject HistoricalDocument(byte[] current, int version)
    {
        JsonObject root = JsonNode.Parse(current)!.AsObject();
        GamePersistenceV9CombatTests.StripCombat(root);
        root["schemaVersion"] = version;
        root["simulationRulesVersion"] = version == 5 ? "engineering-backbone-v1" : "strategic-contact-reporting-v1";
        JsonObject simulation = root["simulation"]!.AsObject();
        simulation.Remove("observationReportAllocatorNextId");
        simulation.Remove("factions");
        foreach (JsonNode? ship in simulation["ships"]!.AsArray())
        {
            ship!.AsObject().Remove("directControllerFactionId");
            if (version == 5)
                foreach (JsonNode? contact in ship["sensorKnowledge"]!["contacts"]!.AsArray())
                    contact!.AsObject().Remove("observedAtLocationId");
        }
        foreach (JsonNode? work in simulation["scheduler"]!["outstandingWork"]!.AsArray())
        {
            work!.AsObject().Remove("targetKind");
            work.AsObject().Remove("targetFactionId");
        }
        return root;
    }
}
