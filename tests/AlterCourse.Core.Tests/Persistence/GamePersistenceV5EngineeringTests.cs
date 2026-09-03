using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Tests.Gameplay;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies the explicit V5 Engineering snapshot and its strict correlation boundary.</summary>
public sealed class GamePersistenceV5EngineeringTests
{
    private readonly Milestone3ProofFixture _fixture = new();

    /// <summary>Confirms every consequential Engineering value round trips while derived values stay absent.</summary>
    [Fact]
    public void RoundTripsExplicitEngineeringStateWithoutDerivedValues()
    {
        byte[] first = GamePersistence.Serialize(_fixture.CreateDefault(), Milestone3ProofFixture.Metadata);
        JsonObject root = Parse(first);
        JsonNode simulation = root["simulation"]!;
        JsonObject ship = simulation["ships"]![0]!.AsObject();
        JsonObject engineering = ship["engineering"]!.AsObject();

        Assert.Equal(5, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("engineering-backbone-v1", root["simulationRulesVersion"]!.GetValue<string>());
        Assert.Equal(0.625, engineering["generationCondition"]!.GetValue<double>());
        Assert.Equal(0.4, engineering["sensorCondition"]!.GetValue<double>());
        Assert.Equal(1, engineering["impulseCondition"]!.GetValue<double>());
        Assert.Equal(44, engineering["sensorAllocation"]!.GetValue<int>());
        Assert.Equal(31, engineering["impulseAllocation"]!.GetValue<int>());
        Assert.Equal("sensors", engineering["activeRepair"]!["targetSystem"]!.GetValue<string>());
        Assert.Contains(
            simulation["scheduler"]!["outstandingWork"]!.AsArray(),
            work => string.Equals(
                work!["kind"]!.GetValue<string>(),
                "systemRepairCompletion",
                StringComparison.Ordinal
            )
        );
        Assert.False(ship.ContainsKey("sensorIntegrity"));
        Assert.False(ship.ContainsKey("sensorRepair"));
        Assert.False(engineering.ContainsKey("availablePower"));
        Assert.False(engineering.ContainsKey("reserve"));
        Assert.False(engineering.ContainsKey("sensorCapability"));
        Assert.False(engineering.ContainsKey("effectiveSensorRange"));

        LoadedGameSave loaded = GamePersistence.Deserialize(first, _fixture.Catalog, "engineering-v5.json");
        byte[] second = GamePersistence.Serialize(loaded.Simulation, loaded.Metadata);
        Assert.Equal(first, second);
    }

    /// <summary>Confirms malformed Engineering state and repair work fail before any aggregate is returned.</summary>
    [Theory]
    [InlineData("system-id")]
    [InlineData("condition")]
    [InlineData("nonfinite")]
    [InlineData("allocation")]
    [InlineData("orphan")]
    [InlineData("mismatch")]
    [InlineData("duplicate")]
    public void RejectsMalformedEngineeringAndRepairCorrelation(string mutation)
    {
        byte[] valid = GamePersistence.Serialize(_fixture.CreateDefault(), Milestone3ProofFixture.Metadata);
        byte[] invalid = Mutate(valid, root => ApplyMutation(root, mutation));
        if (string.Equals(mutation, "nonfinite", StringComparison.Ordinal))
        {
            invalid = Encoding.UTF8.GetBytes(
                Encoding.UTF8.GetString(valid).Replace(
                    "\"generationCondition\": 0.625",
                    "\"generationCondition\": 1e999",
                    StringComparison.Ordinal
                )
            );
        }

        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(invalid, _fixture.Catalog, $"{mutation}.json")
        );
        Assert.Equal(valid, GamePersistence.Serialize(_fixture.CreateDefault(), Milestone3ProofFixture.Metadata));
    }

    private static void ApplyMutation(JsonObject root, string mutation)
    {
        JsonNode simulation = root["simulation"]!;
        JsonNode engineering = simulation["ships"]![0]!["engineering"]!;
        JsonNode repair = engineering["activeRepair"]!;
        switch (mutation)
        {
            case "system-id":
                repair["targetSystem"] = "sensor-array";
                break;
            case "condition":
                engineering["generationCondition"] = 1.1;
                break;
            case "nonfinite":
                break;
            case "allocation":
                engineering["sensorAllocation"] = 71;
                break;
            case "orphan":
                engineering["activeRepair"] = null;
                break;
            case "mismatch":
                repair["scheduledCompletionId"] = 999;
                break;
            case "duplicate":
                JsonArray work = simulation["scheduler"]!["outstandingWork"]!.AsArray();
                work.Add(work[0]!.DeepClone());
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
        }
    }

    private static byte[] Mutate(byte[] source, Action<JsonObject> mutation)
    {
        JsonObject root = Parse(source);
        mutation(root);
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static JsonObject Parse(byte[] json) => JsonNode.Parse(json)!.AsObject();
}
