using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.Content;
using AlterCourse.Core.Persistence;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies V3 order persistence, migration defaults, and aggregate correlations.</summary>
public sealed class GamePersistenceV3OrderTests
{
    /// <summary>Confirms a current orderless world round trips with an explicit empty allocator.</summary>
    [Fact]
    public void RoundTripsCurrentV3WithoutOrders()
    {
        LoadedGameSave loaded = Load(CreateV3NoOrders());
        JsonObject normalized = Parse(GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));

        Assert.Equal(4, normalized["schemaVersion"]!.GetValue<int>());
        Assert.Equal("sensor-knowledge-first-contact-v1", normalized["simulationRulesVersion"]!.GetValue<string>());
        Assert.Equal(1, normalized["simulation"]!["orderAllocatorNextId"]!.GetValue<long>());
        Assert.Null(normalized["simulation"]!["ships"]![0]!["activeOrder"]);
        Assert.Equal(
            normalized.ToJsonString(),
            Parse(
                    GamePersistence.Serialize(
                        Load(Encoding.UTF8.GetBytes(normalized.ToJsonString())).Simulation,
                        loaded.Metadata
                    )
                )
                .ToJsonString()
        );
    }

    /// <summary>Confirms all order variants, patrol progress, wake identity, and allocator survive continuation.</summary>
    [Fact]
    public void RoundTripsEveryOrderKindAndContinuesDeterministically()
    {
        LoadedGameSave uninterrupted = Load(CreateV3Orders());
        LoadedGameSave resumed = Load(GamePersistence.Serialize(uninterrupted.Simulation, uninterrupted.Metadata));
        JsonNode simulation = Parse(GamePersistence.Serialize(resumed.Simulation, resumed.Metadata))["simulation"]!;
        JsonArray ships = simulation["ships"]!.AsArray();

        Assert.Equal(10, simulation["orderAllocatorNextId"]!.GetValue<long>());
        Assert.Equal("travelTo", ships[1]!["activeOrder"]!["kind"]!.GetValue<string>());
        Assert.Equal("beta", ships[1]!["activeOrder"]!["destination"]!.GetValue<string>());
        Assert.Equal(
            ["kind", "id", "destination"],
            ships[1]!["activeOrder"]!.AsObject().Select(pair => pair.Key),
            StringComparer.Ordinal
        );
        Assert.Equal("patrolRoute", ships[2]!["activeOrder"]!["kind"]!.GetValue<string>());
        Assert.Equal(1, ships[2]!["activeOrder"]!["nextWaypointIndex"]!.GetValue<int>());
        Assert.Equal(
            ["alpha", "beta", "gamma"],
            ships[2]!["activeOrder"]!["waypoints"]!.AsArray().Select(node => node!.GetValue<string>()),
            StringComparer.Ordinal
        );
        Assert.Equal("holdUntil", ships[3]!["activeOrder"]!["kind"]!.GetValue<string>());
        Assert.Equal(6000, ships[3]!["activeOrder"]!["untilMilliseconds"]!.GetValue<long>());
        Assert.Equal(3, ships[3]!["activeOrder"]!["scheduledWakeId"]!.GetValue<long>());
        Assert.Equal("orderWake", simulation["scheduler"]!["outstandingWork"]![0]!["kind"]!.GetValue<string>());

        uninterrupted.Simulation.AdvanceFixedSteps(60);
        resumed.Simulation.AdvanceFixedSteps(60);
        JsonArray continuedShips = Parse(GamePersistence.Serialize(resumed.Simulation, resumed.Metadata))[
            "simulation"
        ]!["ships"]!.AsArray();

        Assert.Null(continuedShips[1]!["activeOrder"]);
        Assert.Equal("atLocation", continuedShips[1]!["strategicState"]!["kind"]!.GetValue<string>());
        Assert.Equal("beta", continuedShips[1]!["strategicState"]!["locationId"]!.GetValue<string>());
        Assert.Equal("patrolRoute", continuedShips[2]!["activeOrder"]!["kind"]!.GetValue<string>());
        Assert.Equal("traveling", continuedShips[2]!["strategicState"]!["kind"]!.GetValue<string>());
        Assert.Equal("gamma", continuedShips[2]!["strategicState"]!["travel"]!["destination"]!.GetValue<string>());
        Assert.Null(continuedShips[3]!["activeOrder"]);
        Assert.Equal("atLocation", continuedShips[3]!["strategicState"]!["kind"]!.GetValue<string>());
        Assert.Equal(
            GamePersistence.Serialize(uninterrupted.Simulation, uninterrupted.Metadata),
            GamePersistence.Serialize(resumed.Simulation, resumed.Metadata)
        );
    }

    /// <summary>Confirms the discriminator admits only the three exact order payload shapes.</summary>
    [Theory]
    [InlineData("unknown")]
    [InlineData("missing-id")]
    [InlineData("extra-member")]
    public void RejectsMalformedOrUnknownOrderPayload(string mutation)
    {
        byte[] invalid = Mutate(
            CreateV3Orders(),
            root =>
            {
                JsonObject order = Ship(root, 1)["activeOrder"]!.AsObject();
                switch (mutation)
                {
                    case "unknown":
                        order["kind"] = "escort";
                        break;
                    case "missing-id":
                        order.Remove("id");
                        break;
                    case "extra-member":
                        order["waypoints"] = new JsonArray("alpha", "beta");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mutation));
                }
            }
        );

        AssertFailure(invalid, $"order-{mutation}.json", "incompatible");
    }

    /// <summary>Confirms active-order identities and the allocator form one global identity space.</summary>
    [Theory]
    [InlineData("zero")]
    [InlineData("duplicate")]
    [InlineData("allocator-head")]
    [InlineData("allocator-terminal")]
    public void RejectsInvalidOrderIdentityGraphs(string mutation)
    {
        byte[] invalid = Mutate(
            CreateV3Orders(),
            root =>
            {
                switch (mutation)
                {
                    case "zero":
                        Ship(root, 1)["activeOrder"]!["id"] = 0;
                        break;
                    case "duplicate":
                        Ship(root, 2)["activeOrder"]!["id"] = 7;
                        break;
                    case "allocator-head":
                        root["simulation"]!["orderAllocatorNextId"] = 9;
                        break;
                    case "allocator-terminal":
                        root["simulation"]!["orderAllocatorNextId"] = long.MaxValue;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mutation));
                }
            }
        );

        AssertFailure(
            invalid,
            $"identity-{mutation}.json",
            mutation.Contains("allocator", StringComparison.Ordinal) ? "allocator" : "identit"
        );
    }

    /// <summary>Confirms patrol collections, members, and progress remain bounded and map-valid.</summary>
    [Theory]
    [InlineData("too-few")]
    [InlineData("too-many")]
    [InlineData("missing-waypoint")]
    [InlineData("bad-index")]
    public void RejectsInvalidPatrolState(string mutation)
    {
        byte[] invalid = Mutate(
            CreateV3Orders(),
            root =>
            {
                JsonNode patrol = Ship(root, 2)["activeOrder"]!;
                switch (mutation)
                {
                    case "too-few":
                        patrol["waypoints"] = new JsonArray("alpha");
                        break;
                    case "too-many":
                        patrol["waypoints"] = new JsonArray(
                            Enumerable.Range(0, 17).Select(index => JsonValue.Create($"p{index}")).ToArray()
                        );
                        break;
                    case "missing-waypoint":
                        patrol["waypoints"]![1] = "missing";
                        break;
                    case "bad-index":
                        patrol["nextWaypointIndex"] = 3;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mutation));
                }
            }
        );

        AssertFailure(
            invalid,
            $"patrol-{mutation}.json",
            mutation switch
            {
                "too-many" => "16",
                "missing-waypoint" => "location",
                _ => "patrol",
            }
        );
    }

    /// <summary>Confirms every persisted wake belongs to the exact target HoldUntil order.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("orphan")]
    [InlineData("wrong-target")]
    public void RejectsBrokenOrderWakeCorrelations(string mutation)
    {
        byte[] invalid = Mutate(
            CreateV3Orders(),
            root =>
            {
                JsonNode scheduler = root["simulation"]!["scheduler"]!;
                switch (mutation)
                {
                    case "missing":
                        Ship(root, 3)["activeOrder"]!["scheduledWakeId"] = 99;
                        break;
                    case "orphan":
                        Ship(root, 3)["activeOrder"] = null;
                        break;
                    case "wrong-target":
                        scheduler["outstandingWork"]![0]!["targetShipId"] = 2;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(mutation));
                }
            }
        );

        AssertFailure(invalid, $"wake-{mutation}.json", "correlat");
    }

    /// <summary>Confirms V2 travel remains orderless and historical rules migrate only through the adjacent path.</summary>
    [Fact]
    public void MigratesHistoricalV2TravelWithoutInferringAnOrder()
    {
        LoadedGameSave migrated = Load(CreateV2Travel());
        JsonObject current = Parse(GamePersistence.Serialize(migrated.Simulation, migrated.Metadata));

        Assert.Equal(4, current["schemaVersion"]!.GetValue<int>());
        Assert.Equal("sensor-knowledge-first-contact-v1", current["simulationRulesVersion"]!.GetValue<string>());
        Assert.Equal(1, current["simulation"]!["orderAllocatorNextId"]!.GetValue<long>());
        Assert.Null(current["simulation"]!["ships"]![0]!["activeOrder"]);
        Assert.Equal("traveling", current["simulation"]!["ships"]![0]!["strategicState"]!["kind"]!.GetValue<string>());
    }

    /// <summary>Confirms each schema accepts only its assigned rules identity.</summary>
    [Theory]
    [InlineData(2, "active-world-orders-v1")]
    [InlineData(3, "first-playable-v1")]
    public void RejectsRulesIdentityFromAnotherSchema(int schema, string rules)
    {
        byte[] source = schema == 2 ? CreateV2Travel() : CreateV3NoOrders();
        byte[] invalid = Mutate(source, root => root["simulationRulesVersion"] = rules);
        AssertFailure(invalid, $"rules-v{schema}.json", "rules version");
    }

    /// <summary>Confirms the historical scheduler contract does not admit V3 order wakes.</summary>
    [Fact]
    public void RejectsOrderWakeInV2()
    {
        byte[] invalid = Mutate(
            CreateV2Travel(),
            root => root["simulation"]!["scheduler"]!["outstandingWork"]![0]!["kind"] = "orderWake"
        );
        AssertFailure(invalid, "order-wake-v2.json", "kind");
    }

    private static LoadedGameSave Load(byte[] json) =>
        GamePersistence.Deserialize(json, CreateCatalog(), "orders.json");

    private static byte[] CreateV3NoOrders() =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion": 3,
              "simulationRulesVersion": "active-world-orders-v1",
              "metadata": {{MetadataJson()}},
              "simulation": {
                "timeMilliseconds": 0, "shipAllocatorNextId": 2, "orderAllocatorNextId": 1, "playerShipId": 1,
                "scheduler": { "nextWorkId": 1, "nextSequence": 0, "outstandingWork": [] },
                "strategicMap": {{MapJson()}},
                "ships": [{{ShipJson(1, AtLocationJson("alpha"), "null")}}]
              }
            }
            """
        );

    private static byte[] CreateV3Orders() =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion": 3,
              "simulationRulesVersion": "active-world-orders-v1",
              "metadata": {{MetadataJson()}},
              "simulation": {
                "timeMilliseconds": 4000, "shipAllocatorNextId": 5, "orderAllocatorNextId": 10, "playerShipId": 1,
                "scheduler": {
                  "nextWorkId": 4, "nextSequence": 3,
                  "outstandingWork": [
                    { "id": 3, "dueTimeMilliseconds": 6000, "sequence": 2, "kind": "orderWake", "targetShipId": 4 },
                    { "id": 1, "dueTimeMilliseconds": 10000, "sequence": 0, "kind": "travelArrival", "targetShipId": 2 },
                    { "id": 2, "dueTimeMilliseconds": 10000, "sequence": 1, "kind": "travelArrival", "targetShipId": 3 }
                  ]
                },
                "strategicMap": {{MapJson()}},
                "ships": [
                  {{ShipJson(1, AtLocationJson("alpha"), "null")}},
                  {{ShipJson(
                2,
                TravelJson("alpha", "beta", 0, 10000, 1),
                "{ \"kind\": \"travelTo\", \"id\": 7, \"destination\": \"beta\" }"
            )}},
                  {{ShipJson(
                3,
                TravelJson("alpha", "beta", 0, 10000, 2),
                "{ \"kind\": \"patrolRoute\", \"id\": 8, \"waypoints\": [\"alpha\", \"beta\", \"gamma\"], \"nextWaypointIndex\": 1 }"
            )}},
                  {{ShipJson(
                4,
                AtLocationJson("gamma"),
                "{ \"kind\": \"holdUntil\", \"id\": 9, \"untilMilliseconds\": 6000, \"scheduledWakeId\": 3 }"
            )}}
                ]
              }
            }
            """
        );

    private static byte[] CreateV2Travel() =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion": 2,
              "simulationRulesVersion": "first-playable-v1",
              "metadata": {{MetadataJson()}},
              "simulation": {
                "timeMilliseconds": 4000, "shipAllocatorNextId": 2, "playerShipId": 1,
                "scheduler": { "nextWorkId": 2, "nextSequence": 1, "outstandingWork": [
                  { "id": 1, "dueTimeMilliseconds": 10000, "sequence": 0, "kind": "travelArrival", "targetShipId": 1 }
                ] },
                "strategicMap": {{MapJson()}},
                "ships": [{{ShipJsonV2(1, TravelJson("alpha", "beta", 0, 10000, 1))}}]
              }
            }
            """
        );

    private static string ShipJson(long id, string strategic, string order) =>
        $$"""{ "instanceId": {{id}}, "definitionId": "pathfinder", "displayName": "Ship {{id}}", "tacticalPosition": { "xKilometers": 0, "yKilometers": 0 }, "tacticalMotion": { "headingDegrees": 0, "speedKilometersPerSecond": 0 }, "sensorIntegrity": 1, "sensorRepair": null, "strategicState": {{strategic}}, "activeOrder": {{order}} }""";

    private static string ShipJsonV2(long id, string strategic) =>
        $$"""{ "instanceId": {{id}}, "definitionId": "pathfinder", "displayName": "Ship {{id}}", "tacticalPosition": { "xKilometers": 0, "yKilometers": 0 }, "tacticalMotion": { "headingDegrees": 0, "speedKilometersPerSecond": 0 }, "sensorIntegrity": 1, "sensorRepair": null, "strategicState": {{strategic}} }""";

    private static string AtLocationJson(string location) =>
        $$"""{ "kind": "atLocation", "locationId": "{{location}}", "travel": null }""";

    private static string TravelJson(string origin, string destination, long departure, long arrival, long workId) =>
        $$"""{ "kind": "traveling", "locationId": null, "travel": { "origin": "{{origin}}", "destination": "{{destination}}", "departureMilliseconds": {{departure}}, "expectedArrivalMilliseconds": {{arrival}}, "scheduledArrivalId": {{workId}} } }""";

    private static string MapJson() =>
        """
            { "locations": [
              { "id": "alpha", "displayName": "Alpha", "position": { "xUnitless": 0, "yUnitless": 0 } },
              { "id": "beta", "displayName": "Beta", "position": { "xUnitless": 1, "yUnitless": 1 } },
              { "id": "gamma", "displayName": "Gamma", "position": { "xUnitless": 2, "yUnitless": 2 } }
            ], "routes": [
              { "origin": "alpha", "destination": "beta", "durationMilliseconds": 10000 },
              { "origin": "beta", "destination": "gamma", "durationMilliseconds": 10000 },
              { "origin": "gamma", "destination": "alpha", "durationMilliseconds": 10000 }
            ] }
            """;

    private static string MetadataJson() =>
        """{ "saveId": "slot", "displayName": "Orders", "createdAtUtc": "2026-09-01T00:00:00+00:00", "savedAtUtc": "2026-09-02T00:00:00+00:00" }""";

    private static JsonObject Ship(JsonObject root, int index) => root["simulation"]!["ships"]![index]!.AsObject();

    private static byte[] Mutate(byte[] source, Action<JsonObject> mutation)
    {
        JsonObject root = Parse(source);
        mutation(root);
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static JsonObject Parse(byte[] json) => JsonNode.Parse(json)!.AsObject();

    private static void AssertFailure(byte[] json, string source, string messageFragment)
    {
        GamePersistenceException exception = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(json, CreateCatalog(), source)
        );
        Assert.Equal(GamePersistenceFailure.InvalidData, exception.Failure);
        Assert.Equal(source, exception.SourceIdentity);
        Assert.Contains(messageFragment, FailureReason(exception), StringComparison.OrdinalIgnoreCase);
    }

    private static string FailureReason(GamePersistenceException exception) =>
        exception.Message[$"Save '{exception.SourceIdentity}' ".Length..];

    private static ShipDefinitionCatalog CreateCatalog()
    {
        const string definition = """
            { "schemaVersion": 3, "id": "pathfinder", "designDisplayName": "Pathfinder", "maximumTacticalSpeedKilometersPerSecond": 10, "passiveSensorRangeKilometers": 30.0, "activeScanDurationMilliseconds": 2000, "sensorRepairDurationMilliseconds": 8000 }
            """;
        string schema = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "src/AlterCourse.Godot/content/schemas/ship-definition-v3.schema.json")
        );
        return new ShipDefinitionCatalogLoader(schema).LoadCatalog([
            ShipDefinitionContent.FromText("pathfinder.json", definition),
        ]);
    }

    private static string FindRepositoryRoot()
    {
        for (
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            directory is not null;
            directory = directory.Parent
        )
        {
            if (File.Exists(Path.Combine(directory.FullName, "AlterCourse.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the repository root from the test output directory.");
    }
}
