using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies current snapshots, strict validation, and adjacent historical migration.</summary>
public sealed class GamePersistenceTests
{
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 1, 12, 30, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset SavedAt = new(2026, 9, 2, 14, 45, 0, TimeSpan.Zero);

    /// <summary>Confirms every historical ship-owned state and work target migrates into V3.</summary>
    [Fact]
    public void RoundTripsPluralV2WorldWithPerShipOperations()
    {
        LoadedGameSave loaded = LoadV2(CreatePluralV2());
        byte[] normalized = GamePersistence.Serialize(loaded.Simulation, loaded.Metadata);
        JsonObject root = Parse(normalized);
        JsonObject simulation = root["simulation"]!.AsObject();
        JsonArray ships = simulation["ships"]!.AsArray();
        JsonArray work = simulation["scheduler"]!["outstandingWork"]!.AsArray();

        Assert.Equal(3, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("active-world-orders-v1", root["simulationRulesVersion"]!.GetValue<string>());
        Assert.Equal(1, simulation["orderAllocatorNextId"]!.GetValue<long>());
        Assert.Equal(2, simulation["playerShipId"]!.GetValue<long>());
        Assert.Equal([1L, 2L, 3L], ships.Select(ship => ship!["instanceId"]!.GetValue<long>()));
        Assert.Equal([1L, 2L, 3L], work.Select(item => item!["targetShipId"]!.GetValue<long>()));
        Assert.Equal("Alpha", ships[0]!["displayName"]!.GetValue<string>());
        Assert.Equal("Player Vessel", ships[1]!["displayName"]!.GetValue<string>());
        Assert.NotNull(ships[0]!["sensorRepair"]);
        Assert.NotNull(ships[1]!["sensorRepair"]);
        Assert.Equal("traveling", ships[2]!["strategicState"]!["kind"]!.GetValue<string>());
        Assert.Equal(2, loaded.Simulation.GetPlayerProjection().Ship.InstanceId.Value);
    }

    /// <summary>Confirms scheduler and per-ship evolution continue identically after reload.</summary>
    [Fact]
    public void ReloadedPluralWorldContinuesLikeUninterruptedWorld()
    {
        GameSimulation uninterrupted = LoadV2(CreatePluralV2()).Simulation;
        LoadedGameSave firstLoad = LoadV2(CreatePluralV2());
        byte[] savedPartway = GamePersistence.Serialize(firstLoad.Simulation, firstLoad.Metadata);
        GameSimulation resumed = GamePersistence.Deserialize(savedPartway, CreateCatalog(), "partway.json").Simulation;

        ContinueWorld(uninterrupted);
        ContinueWorld(resumed);

        Assert.Equal(
            GamePersistence.Serialize(uninterrupted, CreateMetadata()),
            GamePersistence.Serialize(resumed, CreateMetadata())
        );
    }

    /// <summary>Confirms ship input order cannot change normalized bytes or semantic continuation.</summary>
    [Fact]
    public void ReversedShipInputNormalizesToCanonicalV2State()
    {
        byte[] forward = CreatePluralV2();
        byte[] reversed = Mutate(
            forward,
            root =>
            {
                JsonArray ships = root["simulation"]!["ships"]!.AsArray();
                JsonNode?[] values = [.. ships.Select(node => node!.DeepClone()).Reverse()];
                ships.Clear();
                foreach (JsonNode? value in values)
                {
                    ships.Add(value);
                }
            }
        );

        LoadedGameSave forwardLoad = LoadV2(forward);
        LoadedGameSave reversedLoad = LoadV2(reversed);
        Assert.Equal(
            GamePersistence.Serialize(forwardLoad.Simulation, forwardLoad.Metadata),
            GamePersistence.Serialize(reversedLoad.Simulation, reversedLoad.Metadata)
        );

        ContinueWorld(forwardLoad.Simulation);
        ContinueWorld(reversedLoad.Simulation);
        Assert.Equal(
            GamePersistence.Serialize(forwardLoad.Simulation, forwardLoad.Metadata),
            GamePersistence.Serialize(reversedLoad.Simulation, reversedLoad.Metadata)
        );
    }

    /// <summary>Confirms same-kind same-time work retains targets and cross-target mutation fails closed.</summary>
    [Fact]
    public void PreservesSameTimeTargetsAndRejectsCrossTargetCorrelation()
    {
        LoadedGameSave loaded = LoadV2(CreatePluralV2());
        JsonArray work = Parse(GamePersistence.Serialize(loaded.Simulation, loaded.Metadata))["simulation"]![
            "scheduler"
        ]!["outstandingWork"]!.AsArray();
        Assert.Equal(
            [(1L, "sensorRepairCompletion"), (2L, "sensorRepairCompletion")],
            work.Take(2).Select(item => (item!["targetShipId"]!.GetValue<long>(), item["kind"]!.GetValue<string>()))
        );

        byte[] crossed = Mutate(
            CreatePluralV2(),
            root => root["simulation"]!["scheduler"]!["outstandingWork"]![0]!["targetShipId"] = 2
        );
        AssertFailure(crossed, "cross-target.json", "same-target");
    }

    /// <summary>Confirms identity, content, allocator, and target invariants are checked before construction.</summary>
    [Fact]
    public void RejectsMalformedPluralIdentityAndReferenceGraphs()
    {
        AssertFailure(MutateV2(root => Ship(root, 1)["instanceId"] = 1), "duplicate-ship.json", "unique");
        AssertFailure(MutateV2(root => Ship(root, 0)["instanceId"] = 0), "zero-ship.json", "positive");
        AssertFailure(MutateV2(root => root["simulation"]!["playerShipId"] = 99), "player.json", "player");
        AssertFailure(MutateV2(root => Ship(root, 0)["definitionId"] = "missing"), "definition.json", "missing");
        AssertFailure(MutateV2(root => root["simulation"]!["shipAllocatorNextId"] = 3), "allocator.json", "allocator");
        AssertFailure(
            MutateV2(root => root["simulation"]!["scheduler"]!["outstandingWork"]![0]!["targetShipId"] = 99),
            "missing-target.json",
            "target"
        );
        AssertFailure(MutateV2(root => Ship(root, 0)["sensorRepair"] = null), "orphan.json", "no correlated");
        AssertFailure(
            MutateV2(root => Ship(root, 0)["strategicState"]!["locationId"] = "missing"),
            "location.json",
            "location"
        );
        AssertFailure(
            MutateV2(root => Ship(root, 2)["strategicState"]!["travel"]!["destination"] = "gamma"),
            "route.json",
            "route"
        );
    }

    /// <summary>Confirms collection, null-shape, temporal, and grid bounds reject hostile input.</summary>
    [Fact]
    public void RejectsBoundedShapeAndTemporalViolations()
    {
        AssertFailure(CreateTooManyShips(), "too-many-ships.json", "256");
        AssertFailure(MutateV2(root => root["simulation"]!["ships"] = null), "null-ships.json", "cannot be null");
        AssertFailure(MutateV2(root => Ship(root, 0)["tacticalPosition"] = null), "null-state.json", "required");
        AssertFailure(
            MutateV2(root => root["simulation"]!["scheduler"]!["outstandingWork"]![0]!["dueTimeMilliseconds"] = 3900),
            "overdue.json",
            "overdue"
        );
        AssertFailure(
            MutateV2(root => root["simulation"]!["timeMilliseconds"] = 4050),
            "off-grid-current.json",
            "fixed-step"
        );
        AssertFailure(
            MutateV2(root => root["simulation"]!["scheduler"]!["outstandingWork"]![0]!["dueTimeMilliseconds"] = 8050),
            "off-grid-work.json",
            "fixed-step"
        );
    }

    /// <summary>Confirms malformed and truncated documents remain parser failures with source identity.</summary>
    [Theory]
    [InlineData("{\"schemaVersion\":2")]
    [InlineData("{\"schemaVersion\":2} trailing")]
    public void RejectsMalformedOrTruncatedJson(string json)
    {
        AssertFailure(Encoding.UTF8.GetBytes(json), "malformed.json", "malformed");
    }

    /// <summary>Confirms required-member presence, nullability, and casing are strict wire contracts.</summary>
    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("case")]
    public void RejectsInvalidRequiredMemberForms(string form)
    {
        byte[] invalid = MutateV2(root =>
        {
            switch (form)
            {
                case "missing":
                    root.Remove("simulation");
                    break;
                case "null":
                    root["metadata"] = null;
                    break;
                case "case":
                    JsonNode version = root["schemaVersion"]!.DeepClone();
                    root.Remove("schemaVersion");
                    root["SchemaVersion"] = version;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(form));
            }
        });

        AssertFailure(
            invalid,
            $"member-{form}.json",
            string.Equals(form, "case", StringComparison.Ordinal) ? "schemaVersion" : "required"
        );
    }

    /// <summary>Confirms restored counters retain persisted ranges and time retains fixed-step headroom.</summary>
    [Theory]
    [InlineData("time")]
    [InlineData("work")]
    [InlineData("sequence")]
    [InlineData("allocator")]
    [InlineData("zero-work")]
    public void RejectsInvalidOrTerminalCountersAndTime(string member)
    {
        byte[] invalid = MutateV2(root =>
        {
            JsonNode simulation = root["simulation"]!;
            JsonNode scheduler = simulation["scheduler"]!;
            switch (member)
            {
                case "time":
                    simulation["timeMilliseconds"] = 9223372036854775800L;
                    break;
                case "work":
                    scheduler["nextWorkId"] = long.MaxValue - 1;
                    break;
                case "sequence":
                    scheduler["nextSequence"] = long.MaxValue - 1;
                    break;
                case "allocator":
                    simulation["shipAllocatorNextId"] = long.MaxValue;
                    break;
                case "zero-work":
                    scheduler["nextWorkId"] = 0;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(member));
            }
        });

        AssertFailure(
            invalid,
            $"terminal-{member}.json",
            member switch
            {
                "time" => "headroom",
                "allocator" => "allocator",
                _ => "counter",
            }
        );
    }

    /// <summary>Confirms the maximum admitted scheduler counters remain writer-loader compatible.</summary>
    [Fact]
    public void RoundTripsMaximumPersistedSchedulerCounters()
    {
        byte[] candidate = MutateV2(root =>
        {
            JsonNode scheduler = root["simulation"]!["scheduler"]!;
            scheduler["nextWorkId"] = long.MaxValue - 2;
            scheduler["nextSequence"] = long.MaxValue - 2;
        });

        LoadedGameSave loaded = GamePersistence.Deserialize(candidate, CreateCatalog(), "maximum-counters.json");
        byte[] normalized = GamePersistence.Serialize(loaded.Simulation, loaded.Metadata);

        Assert.Equal(
            normalized,
            GamePersistence.Serialize(
                GamePersistence.Deserialize(normalized, CreateCatalog(), "maximum-counters-reload.json").Simulation,
                loaded.Metadata
            )
        );
    }

    /// <summary>Confirms the maximum definition identity survives normalized V2 serialization and reload.</summary>
    [Fact]
    public void RoundTripsMaximumShipDefinitionIdentity()
    {
        string maximumId = new('i', ShipDefinitionId.MaximumLength);
        byte[] candidate = MutateV2(root =>
        {
            foreach (JsonNode? ship in root["simulation"]!["ships"]!.AsArray())
            {
                ship!["definitionId"] = maximumId;
            }
        });
        ShipDefinitionCatalog catalog = CreateCatalog(maximumId);

        LoadedGameSave loaded = GamePersistence.Deserialize(candidate, catalog, "maximum-definition-id.json");
        byte[] normalized = GamePersistence.Serialize(loaded.Simulation, loaded.Metadata);

        Assert.Equal(
            normalized,
            GamePersistence.Serialize(
                GamePersistence.Deserialize(normalized, catalog, "maximum-definition-id-reload.json").Simulation,
                loaded.Metadata
            )
        );
    }

    /// <summary>Confirms operation state must match scheduled identity, due time, kind, and target.</summary>
    [Theory]
    [InlineData("travel-id")]
    [InlineData("travel-due")]
    [InlineData("travel-kind")]
    [InlineData("repair-id")]
    [InlineData("repair-due")]
    [InlineData("repair-kind")]
    public void RejectsBrokenOperationCorrelations(string mutation)
    {
        byte[] invalid = MutateV2(root =>
        {
            JsonArray work = root["simulation"]!["scheduler"]!["outstandingWork"]!.AsArray();
            switch (mutation)
            {
                case "travel-id":
                    Ship(root, 2)["strategicState"]!["travel"]!["scheduledArrivalId"] = 2;
                    break;
                case "travel-due":
                    work[2]!["dueTimeMilliseconds"] = 14100;
                    break;
                case "travel-kind":
                    work[2]!["kind"] = "sensorRepairCompletion";
                    break;
                case "repair-id":
                    Ship(root, 0)["sensorRepair"]!["scheduledCompletionId"] = 2;
                    break;
                case "repair-due":
                    work[0]!["dueTimeMilliseconds"] = 7900;
                    break;
                case "repair-kind":
                    work[0]!["kind"] = "travelArrival";
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mutation));
            }
        });

        AssertFailure(invalid, $"correlation-{mutation}.json", "correlat");
    }

    /// <summary>Confirms active travel and repair cannot remain live at completion boundaries.</summary>
    [Theory]
    [InlineData("travel")]
    [InlineData("repair")]
    public void RejectsActiveOperationsAtCompletionBoundary(string operation)
    {
        byte[] invalid = MutateV2(root =>
        {
            JsonNode simulation = root["simulation"]!;
            if (string.Equals(operation, "repair", StringComparison.Ordinal))
            {
                simulation["timeMilliseconds"] = 8000;
                Ship(root, 0)["sensorIntegrity"] = 1;
                Ship(root, 1)["sensorIntegrity"] = 1;
                return;
            }

            simulation["timeMilliseconds"] = 14000;
            Ship(root, 0)["sensorIntegrity"] = 1;
            Ship(root, 0)["sensorRepair"] = null;
            Ship(root, 1)["sensorIntegrity"] = 1;
            Ship(root, 1)["sensorRepair"] = null;
            JsonArray work = simulation["scheduler"]!["outstandingWork"]!.AsArray();
            work.RemoveAt(0);
            work.RemoveAt(0);
        });

        AssertFailure(
            invalid,
            $"completion-{operation}.json",
            string.Equals(operation, "repair", StringComparison.Ordinal) ? "active sensor repair" : "active travel"
        );
    }

    /// <summary>Confirms persisted repair integrity is the exact value derived at save time.</summary>
    [Fact]
    public void RejectsSensorIntegrityMismatch()
    {
        byte[] invalid = MutateV2(root => Ship(root, 0)["sensorIntegrity"] = 0.7);
        AssertFailure(invalid, "sensor-mismatch.json", "does not match");
    }

    /// <summary>Confirms repair timing cannot diverge from the resolved ship definition while retaining correlation.</summary>
    [Fact]
    public void RejectsSensorRepairDurationMismatch()
    {
        byte[] invalid = MutateV2(root =>
        {
            JsonNode repair = Ship(root, 0)["sensorRepair"]!;
            repair["expectedCompletionMilliseconds"] = 8100;
            Ship(root, 0)["sensorIntegrity"] = 0.5 + (0.5 * (4000d / 8100d));
            root["simulation"]!["scheduler"]!["outstandingWork"]![0]!["dueTimeMilliseconds"] = 8100;
        });

        AssertFailure(invalid, "repair-duration.json", "duration");
    }

    /// <summary>Confirms nonfinite tactical data and unknown work kinds fail before domain construction.</summary>
    [Theory]
    [InlineData("nonfinite")]
    [InlineData("kind")]
    public void RejectsNonfiniteTacticalValuesAndInvalidKind(string mutation)
    {
        byte[] invalid = string.Equals(mutation, "kind", StringComparison.Ordinal)
            ? MutateV2(root => root["simulation"]!["scheduler"]!["outstandingWork"]![0]!["kind"] = "unknownWork")
            : Encoding.UTF8.GetBytes(
                ReplaceNumber(Encoding.UTF8.GetString(CreatePluralV2()), "\"xKilometers\": ", "1e400")
            );

        AssertFailure(
            invalid,
            $"invalid-{mutation}.json",
            string.Equals(mutation, "kind", StringComparison.Ordinal) ? "kind" : "finite"
        );
    }

    /// <summary>Confirms a ship at its authored maximum tactical speed round trips unchanged.</summary>
    [Fact]
    public void RoundTripsMaximumTacticalSpeed()
    {
        byte[] maximum = MutateV2(root => Ship(root, 1)["tacticalMotion"]!["speedKilometersPerSecond"] = 10);
        LoadedGameSave loaded = LoadV2(maximum);

        Assert.Equal(10, loaded.Simulation.GetPlayerProjection().Ship.Tactical.SpeedKilometersPerSecond);
        Assert.Equal(
            GamePersistence.Serialize(loaded.Simulation, loaded.Metadata),
            GamePersistence.Serialize(
                GamePersistence
                    .Deserialize(
                        GamePersistence.Serialize(loaded.Simulation, loaded.Metadata),
                        CreateCatalog(),
                        "maximum.json"
                    )
                    .Simulation,
                loaded.Metadata
            )
        );
    }

    /// <summary>Confirms travel and repairs are restorable at their inclusive start boundary.</summary>
    [Fact]
    public void RoundTripsOperationsAtStartBoundary()
    {
        byte[] start = MutateV2(root =>
        {
            JsonNode simulation = root["simulation"]!;
            simulation["timeMilliseconds"] = 0;
            Ship(root, 0)["sensorIntegrity"] = 0.5;
            Ship(root, 1)["sensorIntegrity"] = 0;
            Ship(root, 2)["strategicState"]!["travel"]!["departureMilliseconds"] = 0;
            Ship(root, 2)["strategicState"]!["travel"]!["expectedArrivalMilliseconds"] = 10000;
            simulation["scheduler"]!["outstandingWork"]![2]!["dueTimeMilliseconds"] = 10000;
        });
        LoadedGameSave loaded = LoadV2(start);
        byte[] normalized = GamePersistence.Serialize(loaded.Simulation, loaded.Metadata);
        JsonNode normalizedSimulation = Parse(normalized)["simulation"]!;

        Assert.Equal(normalized, GamePersistence.Serialize(LoadV2(normalized).Simulation, loaded.Metadata));
        Assert.Equal(0, normalizedSimulation["timeMilliseconds"]!.GetValue<long>());
        Assert.Equal(0, normalizedSimulation["ships"]![0]!["sensorRepair"]!["startedAtMilliseconds"]!.GetValue<long>());
        Assert.Equal(
            0,
            normalizedSimulation["ships"]![2]!["strategicState"]!["travel"]!["departureMilliseconds"]!.GetValue<long>()
        );
    }

    /// <summary>Confirms completed operations remain absent after a later save and reload.</summary>
    [Fact]
    public void RoundTripsAfterOperationsComplete()
    {
        LoadedGameSave completed = LoadV2(CreatePluralV2());
        ContinueWorld(completed.Simulation);
        byte[] saved = GamePersistence.Serialize(completed.Simulation, completed.Metadata);
        LoadedGameSave reloaded = LoadV2(saved);
        JsonArray ships = Parse(GamePersistence.Serialize(reloaded.Simulation, reloaded.Metadata))["simulation"]![
            "ships"
        ]!.AsArray();

        Assert.All(ships, ship => Assert.Null(ship!["sensorRepair"]));
        Assert.Equal("atLocation", ships[2]!["strategicState"]!["kind"]!.GetValue<string>());
    }

    /// <summary>Confirms a rejected candidate cannot mutate a separate valid simulation.</summary>
    [Fact]
    public void FailedLoadLeavesExistingSimulationUnchanged()
    {
        LoadedGameSave existing = LoadV2(CreatePluralV2());
        byte[] before = GamePersistence.Serialize(existing.Simulation, existing.Metadata);
        byte[] invalid = MutateV2(root => root["simulation"]!["shipAllocatorNextId"] = 1);

        AssertFailure(invalid, "isolated.json", "allocator");
        Assert.Equal(before, GamePersistence.Serialize(existing.Simulation, existing.Metadata));
    }

    /// <summary>Confirms candidate validation precedes replacement and leaves no staging residue.</summary>
    [Fact]
    public void InvalidCandidateNeverReplacesPriorSaveOrLeavesTemporaryFile()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "slot-one.json");
        try
        {
            LoadedGameSave valid = LoadV2(CreatePluralV2());
            GamePersistence.Save(path, valid.Simulation, valid.Metadata);
            byte[] prior = File.ReadAllBytes(path);
            var invalidMetadata = new GameSaveMetadata("", "Invalid", CreatedAt, SavedAt);

            Assert.Throws<ArgumentException>(() => GamePersistence.Save(path, valid.Simulation, invalidMetadata));
            Assert.Equal(prior, File.ReadAllBytes(path));
            Assert.Equal(
                ["slot-one.json"],
                Directory.GetFiles(directory).Select(Path.GetFileName),
                StringComparer.Ordinal
            );
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    /// <summary>Confirms current output carries durable meaning without runtime or authored catalog contents.</summary>
    [Fact]
    public void OutputContainsOnlyExplicitPersistenceContract()
    {
        string json = Encoding.UTF8.GetString(
            GamePersistence.Serialize(LoadV2(CreatePluralV2()).Simulation, CreateMetadata())
        );
        string[] excludedTerms =
        [
            "node",
            "resource",
            "service",
            "logger",
            "delegate",
            "cache",
            "render",
            "scene",
            "transform",
            "maximumTacticalSpeed",
            "sensorRepairDuration",
            "designDisplayName",
        ];

        Assert.All(excludedTerms, term => Assert.DoesNotContain(term, json, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Confirms V1 migration preserves singleton identities, state, and active-work ownership.</summary>
    [Fact]
    public void MigratesV1WithActiveWorkThroughV2ToV3()
    {
        byte[] v1 = CreateV1(activeWork: true);
        Assert.DoesNotContain("targetShipId", Encoding.UTF8.GetString(v1), StringComparison.Ordinal);

        LoadedGameSave migrated = GamePersistence.Deserialize(v1, CreateCatalog(), "active-v1.json");
        JsonObject v3 = Parse(GamePersistence.Serialize(migrated.Simulation, migrated.Metadata));
        JsonObject simulation = v3["simulation"]!.AsObject();
        JsonObject ship = simulation["ships"]![0]!.AsObject();

        Assert.Equal(3, v3["schemaVersion"]!.GetValue<int>());
        Assert.Equal("active-world-orders-v1", v3["simulationRulesVersion"]!.GetValue<string>());
        Assert.Equal(1, simulation["orderAllocatorNextId"]!.GetValue<long>());
        Assert.All(simulation["ships"]!.AsArray(), candidate => Assert.Null(candidate!["activeOrder"]));
        Assert.Equal(7, simulation["playerShipId"]!.GetValue<long>());
        Assert.Equal(7, ship["instanceId"]!.GetValue<long>());
        Assert.Equal("Pathfinder class", ship["displayName"]!.GetValue<string>());
        Assert.Equal(
            [7L, 7L],
            simulation["scheduler"]!["outstandingWork"]!
                .AsArray()
                .Select(work => work!["targetShipId"]!.GetValue<long>())
        );
        Assert.Equal("traveling", ship["strategicState"]!["kind"]!.GetValue<string>());
        Assert.NotNull(ship["sensorRepair"]);
    }

    /// <summary>Confirms V1 migration cannot preserve repair timing that contradicts resolved content.</summary>
    [Fact]
    public void RejectsV1SensorRepairDurationMismatch()
    {
        byte[] invalid = Mutate(
            CreateV1(activeWork: true),
            root =>
            {
                JsonNode repair = root["simulation"]!["playerShip"]!["sensorRepair"]!;
                repair["expectedCompletionMilliseconds"] = 8100;
                root["simulation"]!["playerShip"]!["sensorIntegrity"] = 0.5 + (0.5 * (4000d / 8100d));
                root["simulation"]!["scheduler"]!["outstandingWork"]![0]!["dueTimeMilliseconds"] = 8100;
            }
        );

        AssertFailure(invalid, "repair-duration-v1.json", "duration");
    }

    /// <summary>Confirms V1 without active work migrates without inventing scheduler consequences.</summary>
    [Fact]
    public void MigratesV1WithoutActiveWork()
    {
        LoadedGameSave migrated = GamePersistence.Deserialize(
            CreateV1(activeWork: false),
            CreateCatalog(),
            "idle-v1.json"
        );
        JsonObject v3 = Parse(GamePersistence.Serialize(migrated.Simulation, migrated.Metadata));

        Assert.Empty(v3["simulation"]!["scheduler"]!["outstandingWork"]!.AsArray());
        Assert.Equal("atLocation", v3["simulation"]!["ships"]![0]!["strategicState"]!["kind"]!.GetValue<string>());
        Assert.Null(v3["simulation"]!["ships"]![0]!["sensorRepair"]);
    }

    /// <summary>Confirms migrated V1 continuation equals its explicitly materialized current semantics.</summary>
    [Fact]
    public void MigratedV1ContinuationMatchesEquivalentV3()
    {
        LoadedGameSave fromV1 = GamePersistence.Deserialize(CreateV1(activeWork: true), CreateCatalog(), "v1.json");
        byte[] equivalentV3 = GamePersistence.Serialize(fromV1.Simulation, fromV1.Metadata);
        LoadedGameSave fromV3 = GamePersistence.Deserialize(equivalentV3, CreateCatalog(), "v3.json");

        ContinueWorld(fromV1.Simulation);
        ContinueWorld(fromV3.Simulation);

        Assert.Equal(
            GamePersistence.Serialize(fromV1.Simulation, fromV1.Metadata),
            GamePersistence.Serialize(fromV3.Simulation, fromV3.Metadata)
        );
    }

    /// <summary>Confirms the historical V1 mapper does not accept current simulation rules.</summary>
    [Fact]
    public void RejectsCurrentRulesOnV1()
    {
        byte[] invalid = Mutate(
            CreateV1(activeWork: false),
            root => root["simulationRulesVersion"] = "active-world-orders-v1"
        );
        AssertFailure(invalid, "rules-v1.json", "rules version");
    }

    /// <summary>Confirms unsupported versions and established parser bounds remain explicit failures.</summary>
    [Fact]
    public void RejectsUnsupportedDuplicateOversizedDeepAndUnknownInput()
    {
        AssertFailure(
            MutateV2(root => root["schemaVersion"] = 4),
            "future.json",
            "unsupported",
            GamePersistenceFailure.UnsupportedVersion
        );
        AssertFailure(
            Encoding.UTF8.GetBytes("{\"schemaVersion\":2,\"schemaVersion\":2}"),
            "duplicate.json",
            "duplicate"
        );
        AssertFailure(new byte[(1024 * 1024) + 1], "oversized.json", "byte");
        AssertFailure(Encoding.UTF8.GetBytes(new string('[', 40) + new string(']', 40)), "deep.json", "depth");
        AssertFailure(MutateV2(root => root["unexpected"] = true), "unknown.json", "incompatible");
    }

    /// <summary>Confirms ordinary live plural construction serializes only schema V3.</summary>
    [Fact]
    public void SerializeEmitsOnlyV3()
    {
        GameSimulation game = FirstGameSetup.Create(CreateCatalog());
        JsonObject root = Parse(GamePersistence.Serialize(game, CreateMetadata()));

        Assert.Equal(3, root["schemaVersion"]!.GetValue<int>());
        Assert.Equal("active-world-orders-v1", root["simulationRulesVersion"]!.GetValue<string>());
        Assert.Equal(1, root["simulation"]!["orderAllocatorNextId"]!.GetValue<long>());
        Assert.NotNull(root["simulation"]!["ships"]);
        Assert.Null(root["simulation"]!["playerShip"]);
        Assert.Null(root["simulation"]!["strategicState"]);
    }

    /// <summary>Confirms atomic replacement still preserves metadata and removes staging files.</summary>
    [Fact]
    public void AtomicPathSaveReplacesPriorSaveWithV3AndCleansTemporaryFiles()
    {
        string directory = CreateTemporaryDirectory();
        string path = Path.Combine(directory, "slot-one.json");
        try
        {
            LoadedGameSave first = LoadV2(CreatePluralV2());
            GamePersistence.Save(path, first.Simulation, first.Metadata);
            byte[] prior = File.ReadAllBytes(path);
            first.Simulation.AdvanceUntilNextScheduledEvent();
            GamePersistence.Save(path, first.Simulation, first.Metadata);

            Assert.NotEqual(prior, File.ReadAllBytes(path));
            Assert.Equal(first.Metadata, GamePersistence.Load(path, CreateCatalog()).Metadata);
            Assert.Equal(
                ["slot-one.json"],
                Directory.GetFiles(directory).Select(Path.GetFileName),
                StringComparer.Ordinal
            );
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static void ContinueWorld(GameSimulation simulation)
    {
        simulation.AdvanceUntilNextScheduledEvent();
        simulation.AdvanceFixedSteps(60);
        simulation.AdvanceFixedSteps(7);
    }

    private static LoadedGameSave LoadV2(byte[] json) =>
        GamePersistence.Deserialize(json, CreateCatalog(), "plural-v2.json");

    private static byte[] CreatePluralV2() =>
        Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion": 2,
              "simulationRulesVersion": "first-playable-v1",
              "metadata": {{MetadataJson()}},
              "simulation": {
                "timeMilliseconds": 4000,
                "shipAllocatorNextId": 4,
                "playerShipId": 2,
                "scheduler": {
                  "nextWorkId": 4,
                  "nextSequence": 3,
                  "outstandingWork": [
                    { "id": 1, "dueTimeMilliseconds": 8000, "sequence": 0, "kind": "sensorRepairCompletion", "targetShipId": 1 },
                    { "id": 2, "dueTimeMilliseconds": 8000, "sequence": 1, "kind": "sensorRepairCompletion", "targetShipId": 2 },
                    { "id": 3, "dueTimeMilliseconds": 14000, "sequence": 2, "kind": "travelArrival", "targetShipId": 3 }
                  ]
                },
                "strategicMap": {{MapJson()}},
                "ships": [
                  {{ShipJson(1, "Alpha", 1, 90, 1, 0.75, RepairJson(0.5, 1, 0, 8000, 1), AtLocationJson("alpha"))}},
                  {{ShipJson(2, "Player Vessel", 2, 45, 2, 0.5, RepairJson(0, 1, 0, 8000, 2), AtLocationJson("beta"))}},
                  {{ShipJson(3, "Traveler", -1, 0, 0, 1, "null", TravelJson("alpha", "beta", 4000, 14000, 3))}}
                ]
              }
            }
            """
        );

    private static byte[] CreateV1(bool activeWork)
    {
        string scheduler = activeWork
            ? """
                { "nextWorkId": 3, "nextSequence": 2, "outstandingWork": [
                  { "id": 1, "dueTimeMilliseconds": 8000, "sequence": 0, "kind": "sensorRepairCompletion" },
                  { "id": 2, "dueTimeMilliseconds": 14000, "sequence": 1, "kind": "travelArrival" }
                ] }
                """
            : """{ "nextWorkId": 1, "nextSequence": 0, "outstandingWork": [] }""";
        string strategic = activeWork ? TravelJson("alpha", "beta", 4000, 14000, 2) : AtLocationJson("alpha");
        string repair = activeWork ? RepairJson(0.5, 1, 0, 8000, 1) : "null";
        double integrity = activeWork ? 0.75 : 1;
        return Encoding.UTF8.GetBytes(
            $$"""
            {
              "schemaVersion": 1,
              "simulationRulesVersion": "first-playable-v1",
              "metadata": {{MetadataJson()}},
              "simulation": {
                "timeMilliseconds": 4000,
                "shipAllocatorNextId": 8,
                "scheduler": {{scheduler}},
                "strategicMap": {{MapJson()}},
                "strategicState": {{strategic}},
                "playerShip": {
                  "instanceId": 7,
                  "definitionId": "pathfinder",
                  "tacticalPosition": { "xKilometers": 1, "yKilometers": 2 },
                  "tacticalMotion": { "headingDegrees": 0, "speedKilometersPerSecond": 0 },
                  "sensorIntegrity": {{integrity}},
                  "sensorRepair": {{repair}}
                }
              }
            }
            """
        );
    }

    private static string MetadataJson() =>
        """
            { "saveId": "slot-one", "displayName": "Voyage One", "createdAtUtc": "2026-09-01T12:30:00+00:00", "savedAtUtc": "2026-09-02T14:45:00+00:00" }
            """;

    private static string MapJson() =>
        """
            {
              "locations": [
                { "id": "alpha", "displayName": "Alpha", "position": { "xUnitless": 0, "yUnitless": 0 } },
                { "id": "beta", "displayName": "Beta", "position": { "xUnitless": 1, "yUnitless": 1 } },
                { "id": "gamma", "displayName": "Gamma", "position": { "xUnitless": 2, "yUnitless": 2 } }
              ],
              "routes": [
                { "origin": "alpha", "destination": "beta", "durationMilliseconds": 10000 },
                { "origin": "beta", "destination": "gamma", "durationMilliseconds": 12000 }
              ]
            }
            """;

    private static string ShipJson(
        long id,
        string name,
        double x,
        double heading,
        double speed,
        double integrity,
        string repair,
        string strategic
    ) =>
        $$"""
            {
              "instanceId": {{id}}, "definitionId": "pathfinder", "displayName": "{{name}}",
              "tacticalPosition": { "xKilometers": {{x}}, "yKilometers": 0 },
              "tacticalMotion": { "headingDegrees": {{heading}}, "speedKilometersPerSecond": {{speed}} },
              "sensorIntegrity": {{integrity}}, "sensorRepair": {{repair}}, "strategicState": {{strategic}}
            }
            """;

    private static string RepairJson(double start, double target, long began, long due, long id) =>
        $$"""{ "startingIntegrity": {{start}}, "targetIntegrity": {{target}}, "startedAtMilliseconds": {{began}}, "expectedCompletionMilliseconds": {{due}}, "scheduledCompletionId": {{id}} }""";

    private static string AtLocationJson(string location) =>
        $$"""{ "kind": "atLocation", "locationId": "{{location}}", "travel": null }""";

    private static string TravelJson(string origin, string destination, long departure, long arrival, long id) =>
        $$"""{ "kind": "traveling", "locationId": null, "travel": { "origin": "{{origin}}", "destination": "{{destination}}", "departureMilliseconds": {{departure}}, "expectedArrivalMilliseconds": {{arrival}}, "scheduledArrivalId": {{id}} } }""";

    private static byte[] CreateTooManyShips()
    {
        JsonObject root = Parse(CreatePluralV2());
        JsonArray ships = root["simulation"]!["ships"]!.AsArray();
        JsonObject template = ships[0]!.AsObject();
        ships.Clear();
        for (int id = 1; id <= 257; id++)
        {
            JsonObject ship = template.DeepClone().AsObject();
            ship["instanceId"] = id;
            ship["sensorRepair"] = null;
            ships.Add(ship);
        }

        root["simulation"]!["playerShipId"] = 1;
        root["simulation"]!["shipAllocatorNextId"] = 258;
        root["simulation"]!["scheduler"]!["nextWorkId"] = 1;
        root["simulation"]!["scheduler"]!["nextSequence"] = 0;
        root["simulation"]!["scheduler"]!["outstandingWork"] = new JsonArray();
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static JsonObject Ship(JsonObject root, int index) => root["simulation"]!["ships"]![index]!.AsObject();

    private static byte[] MutateV2(Action<JsonObject> mutation) => Mutate(CreatePluralV2(), mutation);

    private static byte[] Mutate(byte[] source, Action<JsonObject> mutation)
    {
        JsonObject root = Parse(source);
        mutation(root);
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static string ReplaceNumber(string json, string propertyPrefix, string replacement)
    {
        int valueStart = json.IndexOf(propertyPrefix, StringComparison.Ordinal) + propertyPrefix.Length;
        int valueEnd = json.IndexOf(',', valueStart);
        Assert.True(valueStart >= propertyPrefix.Length && valueEnd > valueStart);
        return string.Concat(json.AsSpan(0, valueStart), replacement, json.AsSpan(valueEnd));
    }

    private static JsonObject Parse(byte[] json) => JsonNode.Parse(json)!.AsObject();

    private static void AssertFailure(
        byte[] json,
        string source,
        string messageFragment,
        GamePersistenceFailure failure = GamePersistenceFailure.InvalidData
    )
    {
        GamePersistenceException exception = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(json, CreateCatalog(), source)
        );
        Assert.Equal(failure, exception.Failure);
        Assert.Equal(source, exception.SourceIdentity);
        Assert.Contains(messageFragment, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ShipDefinition CreateDefinition() =>
        new(
            new ShipDefinitionId("pathfinder"),
            "Pathfinder class",
            new SpeedKilometersPerSecond(10),
            new SimulationDuration(8000)
        );

    private static ShipDefinitionCatalog CreateCatalog(string definitionId = "pathfinder")
    {
        string definition = $$"""
            {
              "schemaVersion": 2,
              "id": "{{definitionId}}",
              "designDisplayName": "Pathfinder class",
              "maximumTacticalSpeedKilometersPerSecond": 10,
              "sensorRepairDurationMilliseconds": 8000
            }
            """;
        string schema = File.ReadAllText(
            Path.Combine(FindRepositoryRoot(), "src/AlterCourse.Godot/content/schemas/ship-definition-v2.schema.json")
        );
        return new ShipDefinitionCatalogLoader(schema).LoadCatalog([
            ShipDefinitionContent.FromText("pathfinder.json", definition),
        ]);
    }

    private static GameSaveMetadata CreateMetadata() => new("slot-one", "Voyage One", CreatedAt, SavedAt);

    private static string CreateTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"alter-course-persistence-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
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
