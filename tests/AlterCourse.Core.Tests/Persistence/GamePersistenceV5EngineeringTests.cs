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

        Assert.Equal(10, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("installed-ship-system-substrate-v1", root["simulationRulesVersion"]!.GetValue<string>());
        // Production installed ids 1–5: generation (nonconsumer, null allocation), sensors, impulse, shields, weapons.
        Assert.Equal(6, engineering["nextInstalledSystemId"]!.GetValue<long>());
        Assert.Equal(0.625, SaveJsonV10.Condition(ship, 1));
        Assert.Null(SaveJsonV10.Installation(ship, 1)["allocation"]);
        Assert.True(SaveJsonV10.Installation(ship, 1).ContainsKey("allocation"));
        Assert.Equal(0.4, SaveJsonV10.Condition(ship, 2));
        Assert.Equal(1, SaveJsonV10.Condition(ship, 3));
        Assert.Equal(44, SaveJsonV10.Installation(ship, 2)["allocation"]!.GetValue<int>());
        Assert.Equal(31, SaveJsonV10.Installation(ship, 3)["allocation"]!.GetValue<int>());
        Assert.Equal(2, engineering["activeRepair"]!["targetInstalledSystemId"]!.GetValue<long>());
        Assert.False(SaveJsonV10.Installation(ship, 2).ContainsKey("kind"));
        Assert.False(SaveJsonV10.Installation(ship, 2).ContainsKey("capability"));
        Assert.Contains(
            simulation["scheduler"]!["outstandingWork"]!.AsArray(),
            work => string.Equals(work!["kind"]!.GetValue<string>(), "systemRepairCompletion", StringComparison.Ordinal)
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
                Encoding
                    .UTF8.GetString(valid)
                    .Replace("\"condition\":0.625", "\"condition\":1e999", StringComparison.Ordinal)
            );
        }

        // Guards against a vacuous pass: every mutation must change the document it claims to corrupt.
        Assert.NotEqual(valid, invalid);
        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(invalid, _fixture.Catalog, $"{mutation}.json")
        );
        Assert.Equal(valid, GamePersistence.Serialize(_fixture.CreateDefault(), Milestone3ProofFixture.Metadata));
    }

    private static void ApplyMutation(JsonObject root, string mutation)
    {
        JsonNode simulation = root["simulation"]!;
        JsonNode ship = simulation["ships"]![0]!;
        JsonNode engineering = ship["engineering"]!;
        JsonNode repair = engineering["activeRepair"]!;
        switch (mutation)
        {
            case "system-id":
                repair["targetInstalledSystemId"] = 99;
                break;
            case "condition":
                SaveJsonV10.Installation(ship, 1)["condition"] = 1.1;
                break;
            case "nonfinite":
                break;
            case "allocation":
                SaveJsonV10.Installation(ship, 2)["allocation"] = 71;
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
