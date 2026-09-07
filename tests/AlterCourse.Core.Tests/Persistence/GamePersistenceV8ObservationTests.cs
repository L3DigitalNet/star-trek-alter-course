using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.AI;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;
using AlterCourse.Core.Tests.Gameplay;
using FactionTestWorld = AlterCourse.Core.Tests.Gameplay.FactionBootstrapTests.FactionTestWorld;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies V8 observation snapshots and the strict adjacent V7 migration.</summary>
public sealed class GamePersistenceV8ObservationTests
{
    private static readonly GameSaveMetadata Metadata = Milestone3ProofFixture.Metadata;

    /// <summary>V7 migration preserves prior authority while inventing no observation-response history.</summary>
    [Fact]
    public void MigratesV7AdjacentlyWithDisabledEmptyObservationState()
    {
        GameSimulation current = FirstGameSetup.Create(
            new Milestone3ProofFixture().Catalog,
            FactionTestWorld.FactionCatalog
        );
        SimulationState assigned = GameSimulation
            .AdvanceTo(
                current.CaptureState(),
                new SimulationTime(0),
                new Milestone3ProofFixture().Catalog,
                FactionTestWorld.FactionCatalog
            )
            .State;
        current = GameSimulation.RestoreState(
            assigned,
            new Milestone3ProofFixture().Catalog,
            FactionTestWorld.FactionCatalog
        );
        JsonObject v7 = ToHistoricalV7(GamePersistence.Serialize(current, Milestone3ProofFixture.Metadata));
        JsonNode originalSimulation = v7["simulation"]!;

        LoadedGameSave loaded = GamePersistence.Deserialize(
            Encoding.UTF8.GetBytes(v7.ToJsonString()),
            new Milestone3ProofFixture().Catalog,
            FactionTestWorld.FactionCatalog,
            "adjacent-v7.json"
        );
        JsonObject migrated = Parse(GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
        JsonNode migratedSimulation = migrated["simulation"]!;

        Assert.Equal(8, migrated["schemaVersion"]!.GetValue<int>());
        Assert.Equal("observation-driven-faction-response-v1", migrated["simulationRulesVersion"]!.GetValue<string>());
        Assert.Equal(1, migratedSimulation["observationReportAllocatorNextId"]!.GetValue<long>());
        Assert.Equal(
            originalSimulation["timeMilliseconds"]!.ToJsonString(),
            migratedSimulation["timeMilliseconds"]!.ToJsonString()
        );
        Assert.Equal(
            originalSimulation["shipAllocatorNextId"]!.ToJsonString(),
            migratedSimulation["shipAllocatorNextId"]!.ToJsonString()
        );
        Assert.Equal(
            originalSimulation["orderAllocatorNextId"]!.ToJsonString(),
            migratedSimulation["orderAllocatorNextId"]!.ToJsonString()
        );
        Assert.Equal(originalSimulation["scheduler"]!.ToJsonString(), migratedSimulation["scheduler"]!.ToJsonString());
        Assert.Equal(originalSimulation["ships"]!.ToJsonString(), migratedSimulation["ships"]!.ToJsonString());
        Assert.All(
            migratedSimulation["factions"]!.AsArray(),
            faction => AssertEmptyDisabledObservation(faction!["observation"]!.AsObject())
        );
        Assert.NotNull(migratedSimulation["ships"]![3]!["sensorKnowledge"]!["contacts"]![0]);
        Assert.Contains(
            migratedSimulation["ships"]!.AsArray(),
            ship => ship!["activeOrder"] is not null && ship["directControllerFactionId"] is not null
        );
    }

    /// <summary>V7 source validation remains frozen even after V8 admits a new rules identity and work kind.</summary>
    [Theory]
    [InlineData("rules")]
    [InlineData("report-delivery")]
    public void RejectsV8TokensInjectedIntoHistoricalV7(string mutation)
    {
        JsonObject v7 = ToHistoricalV7(
            GamePersistence.Serialize(
                FirstGameSetup.Create(new Milestone3ProofFixture().Catalog, FactionTestWorld.FactionCatalog),
                Milestone3ProofFixture.Metadata
            )
        );
        if (string.Equals(mutation, "rules", StringComparison.Ordinal))
        {
            v7["simulationRulesVersion"] = "observation-driven-faction-response-v1";
        }
        else
        {
            v7["simulation"]!["scheduler"]!["outstandingWork"]![0]!["kind"] = "reportDelivery";
        }

        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(
                Encoding.UTF8.GetBytes(v7.ToJsonString()),
                new Milestone3ProofFixture().Catalog,
                FactionTestWorld.FactionCatalog,
                $"historical-v7-{mutation}.json"
            )
        );
    }

    /// <summary>V8 remains compatible with a world that has no political state.</summary>
    [Fact]
    public void RoundTripsZeroFactionWorld()
    {
        var fixture = new Milestone3ProofFixture();
        byte[] saved = GamePersistence.Serialize(fixture.CreateDefault(), Milestone3ProofFixture.Metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(saved, fixture.Catalog, "zero-factions-v8.json");
        JsonObject root = Parse(saved);

        Assert.Equal(8, root["schemaVersion"]!.GetValue<int>());
        Assert.Empty(root["simulation"]!["factions"]!.AsArray());
        Assert.Equal(1, root["simulation"]!["observationReportAllocatorNextId"]!.GetValue<long>());
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
    }

    /// <summary>All actor-safe report fields survive a bounded received-state round trip.</summary>
    [Fact]
    public void RoundTripsBoundedReceivedReportWithHistoricalIdentification()
    {
        GameSimulation game = CreateReceivedReportGame();
        byte[] saved = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        LoadedGameSave loaded = GamePersistence.Deserialize(
            saved,
            FactionTestWorld.ShipCatalog,
            FactionTestWorld.FactionCatalog,
            "received-report-v8.json"
        );
        JsonObject simulation = Parse(saved)["simulation"]!.AsObject();
        JsonObject observation = simulation["factions"]![1]!["observation"]!.AsObject();
        JsonObject received = observation["receivedReports"]![0]!.AsObject();
        JsonObject report = received["report"]!.AsObject();

        Assert.Equal(2, simulation["observationReportAllocatorNextId"]!.GetValue<long>());
        Assert.Equal("enabled", observation["posture"]!.GetValue<string>());
        Assert.Equal(1, report["reportId"]!.GetValue<long>());
        Assert.Equal(4, report["observerShipId"]!.GetValue<long>());
        Assert.Equal(37, report["observerContactId"]!.GetValue<long>());
        Assert.Equal("beta", report["observedAtLocationId"]!.GetValue<string>());
        Assert.Equal(12.5, report["observedPosition"]!["xKilometers"]!.GetValue<double>());
        Assert.Equal(-4.25, report["observedPosition"]!["yKilometers"]!.GetValue<double>());
        Assert.Equal(0, report["observedAtMilliseconds"]!.GetValue<long>());
        Assert.Equal("identified", report["identification"]!.GetValue<string>());
        Assert.Equal("Observed Vessel", report["knownVesselDisplayName"]!.GetValue<string>());
        Assert.Equal("Observed Design", report["knownDesignDisplayName"]!.GetValue<string>());
        Assert.Equal(2_000, received["receivedAtMilliseconds"]!.GetValue<long>());
        Assert.Equal("handled", received["handling"]!.GetValue<string>());
        Assert.Equal("beta", observation["completionWatermarks"]![0]!["locationId"]!.GetValue<string>());
        Assert.Equal(2_000, observation["completionWatermarks"]![0]!["observedThroughMilliseconds"]!.GetValue<long>());
        Assert.Null(report["targetShipId"]);
        Assert.Null(report["targetControllerFactionId"]);
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
    }

    /// <summary>Required members, finite bounds, enums, chronology, and actor-safe shape fail before construction.</summary>
    [Theory]
    [InlineData("missing-allocator")]
    [InlineData("missing-observation")]
    [InlineData("null-received")]
    [InlineData("unknown-posture")]
    [InlineData("unknown-handling")]
    [InlineData("future-receipt")]
    [InlineData("late-receipt")]
    [InlineData("overflow-receipt")]
    [InlineData("hidden-target")]
    [InlineData("too-many-received")]
    public void RejectsMalformedV8ObservationShape(string mutation)
    {
        GameSimulation game = CreateReceivedReportGame();
        byte[] before = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        JsonObject root = Parse(before);
        JsonObject simulation = root["simulation"]!.AsObject();
        JsonObject faction = simulation["factions"]![1]!.AsObject();
        JsonObject observation = faction["observation"]!.AsObject();
        JsonObject received = observation["receivedReports"]![0]!.AsObject();
        ApplyMalformedShapeMutation(mutation, simulation, faction, observation, received);

        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(
                Encoding.UTF8.GetBytes(root.ToJsonString()),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog,
                $"malformed-v8-{mutation}.json"
            )
        );
        Assert.Equal(before, GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata));
    }

    private static void ApplyMalformedShapeMutation(
        string mutation,
        JsonObject simulation,
        JsonObject faction,
        JsonObject observation,
        JsonObject received
    )
    {
        switch (mutation)
        {
            case "missing-allocator":
                simulation.Remove("observationReportAllocatorNextId");
                break;
            case "missing-observation":
                faction.Remove("observation");
                break;
            case "null-received":
                observation["receivedReports"] = null;
                break;
            case "unknown-posture":
                observation["posture"] = "unknown";
                break;
            case "unknown-handling":
                received["handling"] = "unknown";
                break;
            case "future-receipt":
                received["receivedAtMilliseconds"] = 2_100;
                break;
            case "late-receipt":
                simulation["timeMilliseconds"] = 3_000;
                received["receivedAtMilliseconds"] = 3_000;
                observation["completionWatermarks"]![0]!["observedThroughMilliseconds"] = 3_000;
                break;
            case "overflow-receipt":
                const long maximumFixedTime = 9_223_372_036_854_775_800;
                simulation["timeMilliseconds"] = maximumFixedTime;
                received["report"]!["observedAtMilliseconds"] = maximumFixedTime - 1_000;
                received["receivedAtMilliseconds"] = maximumFixedTime;
                observation["completionWatermarks"]![0]!["observedThroughMilliseconds"] = maximumFixedTime;
                break;
            case "hidden-target":
                received["report"]!["targetShipId"] = 1;
                break;
            case "too-many-received":
                JsonArray reports = observation["receivedReports"]!.AsArray();
                for (int index = 2; index <= 17; index++)
                {
                    JsonObject extra = received.DeepClone().AsObject();
                    extra["report"]!["reportId"] = index;
                    reports.Add(extra);
                }
                simulation["observationReportAllocatorNextId"] = 18;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
        }
    }

    /// <summary>Invalid persisted observation graphs are rejected without changing the live simulation.</summary>
    [Theory]
    [InlineData("duplicate-report-id")]
    [InlineData("allocator-not-after-report")]
    [InlineData("missing-observer")]
    [InlineData("player-observer")]
    [InlineData("wrong-controller")]
    [InlineData("future-observation")]
    [InlineData("unknown-location")]
    [InlineData("invalid-identification")]
    [InlineData("duplicate-delivery-work")]
    [InlineData("wrong-delivery-due")]
    [InlineData("orphan-delivery-work")]
    [InlineData("wrong-delivery-recipient")]
    public void RejectsInvalidInFlightGraphAtomically(string mutation)
    {
        GameSimulation live = CreatePendingDeliveryGame();
        byte[] before = GamePersistence.Serialize(live, Metadata);
        JsonObject root = Parse(before);
        JsonObject simulation = root["simulation"]!.AsObject();
        JsonObject observation = simulation["factions"]![0]!["observation"]!.AsObject();
        JsonObject first = observation["inFlightReports"]![0]!.AsObject();
        JsonObject second = observation["inFlightReports"]![1]!.AsObject();
        JsonObject report = first["report"]!.AsObject();
        JsonArray work = simulation["scheduler"]!["outstandingWork"]!.AsArray();
        switch (mutation)
        {
            case "duplicate-report-id":
                second["report"]!["reportId"] = report["reportId"]!.DeepClone();
                break;
            case "allocator-not-after-report":
                simulation["observationReportAllocatorNextId"] = report["reportId"]!.DeepClone();
                break;
            case "missing-observer":
                report["observerShipId"] = 999;
                break;
            case "player-observer":
                report["observerShipId"] = simulation["playerShipId"]!.DeepClone();
                break;
            case "wrong-controller":
                report["observerShipId"] = 4;
                break;
            case "future-observation":
                report["observedAtMilliseconds"] = 100;
                first["dueTimeMilliseconds"] = 2_100;
                FindWork(work, first["deliveryWorkId"]!.GetValue<long>())["dueTimeMilliseconds"] = 2_100;
                break;
            case "unknown-location":
                report["observedAtLocationId"] = "missing";
                break;
            case "invalid-identification":
                report["identification"] = "identified";
                report["knownVesselDisplayName"] = null;
                report["knownDesignDisplayName"] = null;
                break;
            case "duplicate-delivery-work":
                second["deliveryWorkId"] = first["deliveryWorkId"]!.DeepClone();
                break;
            case "wrong-delivery-due":
                first["dueTimeMilliseconds"] = 2_100;
                break;
            case "orphan-delivery-work":
                observation["inFlightReports"]!.AsArray().RemoveAt(0);
                break;
            case "wrong-delivery-recipient":
                FindWork(work, first["deliveryWorkId"]!.GetValue<long>())["targetFactionId"] = 2;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
        }

        AssertRejectedWithoutMutation(root, live, before, $"invalid-inflight-{mutation}.json");
    }

    /// <summary>Active-response correlations and completed-report evidence remain authoritative on load.</summary>
    [Theory]
    [InlineData("wrong-responder")]
    [InlineData("player-responder")]
    [InlineData("wrong-order")]
    [InlineData("wrong-origin")]
    [InlineData("wrong-destination")]
    [InlineData("future-assignment")]
    [InlineData("early-receipt")]
    [InlineData("late-active-receipt")]
    [InlineData("missing-wake")]
    [InlineData("wrong-wake-due")]
    [InlineData("handled-without-watermark")]
    [InlineData("unwitnessed-watermark")]
    [InlineData("watermark-after-assignment")]
    public void RejectsInvalidResponseContinuationAtomically(string mutation)
    {
        GameSimulation live = mutation switch
        {
            "handled-without-watermark" or "unwitnessed-watermark" => CreateReceivedReportGame(),
            "watermark-after-assignment" => Restore(CreateActiveWithHistoricalCompletion()),
            _ => Restore(CreateLongActiveInvestigation()),
        };
        byte[] before = GamePersistence.Serialize(live, Metadata);
        JsonObject root = Parse(before);
        JsonObject simulation = root["simulation"]!.AsObject();
        int factionIndex = mutation is "handled-without-watermark" or "unwitnessed-watermark" ? 1 : 0;
        JsonObject faction = simulation["factions"]![factionIndex]!.AsObject();
        JsonObject observation = faction["observation"]!.AsObject();
        JsonObject? active = observation["activeInvestigation"]?.AsObject();
        switch (mutation)
        {
            case "wrong-responder":
                active!["responderShipId"] = 2;
                break;
            case "player-responder":
                active!["responderShipId"] = 1;
                break;
            case "wrong-order":
                active!["orderId"] = 999;
                break;
            case "wrong-origin":
                active!["originLocationId"] = "gamma";
                break;
            case "wrong-destination":
                active!["destinationLocationId"] = "alpha";
                break;
            case "future-assignment":
                active!["assignedAtMilliseconds"] = 2_100;
                break;
            case "early-receipt":
                active!["sourceReceivedAtMilliseconds"] = 1_900;
                break;
            case "late-active-receipt":
                active!["sourceReceivedAtMilliseconds"] = 3_000;
                break;
            case "missing-wake":
                faction["pendingDecisionWake"] = null;
                break;
            case "wrong-wake-due":
                faction["pendingDecisionWake"]!["dueTimeMilliseconds"] = 69_900;
                break;
            case "handled-without-watermark":
                observation["completionWatermarks"] = new JsonArray();
                break;
            case "unwitnessed-watermark":
                observation["completionWatermarks"]![0]!["locationId"] = "alpha";
                break;
            case "watermark-after-assignment":
                observation["completionWatermarks"]![0]!["observedThroughMilliseconds"] = 3_000;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation.");
        }

        AssertRejectedWithoutMutation(root, live, before, $"invalid-response-{mutation}.json");
    }

    /// <summary>V8 reloads preserve pending delivery, active travel, completion, and suppression behavior.</summary>
    [Fact]
    public void ReloadedObservationContinuationsMatchUninterruptedSimulation()
    {
        AssertContinuation(CreatePendingDeliveryGame(), new SimulationTime(2_000));
        AssertContinuation(Restore(CreateLongActiveInvestigation()), new SimulationTime(72_000));

        GameSimulation completed = Advance(Restore(CreateLongActiveInvestigation()), new SimulationTime(72_000));
        AssertContinuation(completed, new SimulationTime(72_100));
    }

    private static GameSimulation CreateReceivedReportGame()
    {
        GameSimulation initial = FactionTestWorld
            .CreateBootstrap(
                [
                    new FactionStart(FactionTestWorld.FactionA, FactionTestWorld.DefinitionA),
                    new FactionStart(FactionTestWorld.FactionB, FactionTestWorld.DefinitionB),
                ],
                controlledShips: true
            )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
        initial.AdvanceFixedSteps(20);
        SimulationState state = initial.CaptureState();
        ShipState reportingShip = state.GetRequiredShip(new ShipInstanceId(4));
        state = state.ReplaceShip(
            reportingShip.InstanceId,
            reportingShip with
            {
                SensorKnowledge = new SensorKnowledge(38, []),
            }
        );
        var report = new ObservationReportSnapshot(
            new ObservationReportId(1),
            new ShipInstanceId(4),
            new SensorContactId(37),
            FactionTestWorld.Beta,
            new TacticalPosition(12.5, -4.25),
            new SimulationTime(0),
            SensorContactIdentification.Identified,
            "Observed Vessel",
            "Observed Design"
        );
        FactionState faction = state.GetRequiredFaction(FactionTestWorld.FactionB);
        var observation = new FactionObservationState(
            ObservationResponsePosture.Enabled,
            receivedReports:
            [
                new ReceivedObservationReport(report, new SimulationTime(2_000), ObservationReportHandling.Handled),
            ],
            completionWatermarks:
            [
                new ObservationLocationCompletionWatermark(FactionTestWorld.Beta, new SimulationTime(2_000)),
            ]
        );
        state = state.ReplaceFaction(FactionTestWorld.FactionB, faction with { Observation = observation }) with
        {
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(2),
        };
        return GameSimulation.RestoreState(state, FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
    }

    private static GameSimulation CreatePendingDeliveryGame()
    {
        GameSimulation game = CreateObservationGame(ObservationResponsePosture.Enabled);
        game.BootstrapHiddenCautiousContactObservation(new ShipInstanceId(2));
        return game;
    }

    private static GameSimulation CreateObservationGame(ObservationResponsePosture posture)
    {
        ShipStart[] ships =
        [
            FactionTestWorld.CreateShip(1, FactionTestWorld.Gamma, null),
            FactionTestWorld.CreateShip(2, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(3, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(4, FactionTestWorld.Beta, FactionTestWorld.FactionB),
        ];
        return new GameBootstrap(
            new SimulationTime(0),
            FactionTestWorld.CreateMap(),
            ships[0].InstanceId,
            ships,
            [
                new FactionStart(
                    FactionTestWorld.FactionA,
                    FactionTestWorld.DefinitionA,
                    ObservationResponsePosture: posture
                ),
                new FactionStart(FactionTestWorld.FactionB, FactionTestWorld.DefinitionB),
            ]
        ).CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);
    }

    private static SimulationState CreateLongActiveInvestigation()
    {
        StrategicMap map = CreateLongInvestigationMap();
        ShipStart[] ships =
        [
            FactionTestWorld.CreateShip(1, FactionTestWorld.Gamma, null),
            FactionTestWorld.CreateShip(2, FactionTestWorld.Beta, FactionTestWorld.FactionA),
            FactionTestWorld.CreateShip(3, FactionTestWorld.Alpha, FactionTestWorld.FactionA),
        ];
        SimulationState state = new GameBootstrap(
            new SimulationTime(0),
            map,
            ships[0].InstanceId,
            ships,
            [
                new FactionStart(
                    FactionTestWorld.FactionA,
                    FactionTestWorld.DefinitionA,
                    ObservationResponsePosture: ObservationResponsePosture.Enabled
                ),
            ]
        )
            .CreateSimulation(FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .CaptureState();
        ShipState observer = state.GetRequiredShip(new ShipInstanceId(2));
        state = state.ReplaceShip(
            observer.InstanceId,
            observer with
            {
                SensorKnowledge = new SensorKnowledge(2, observer.SensorKnowledge.Contacts),
            }
        );
        var report = new ObservationReportSnapshot(
            new ObservationReportId(1),
            observer.InstanceId,
            new SensorContactId(1),
            FactionTestWorld.Beta,
            default,
            new SimulationTime(0),
            SensorContactIdentification.Detected
        );
        FactionState faction = state.Factions[0];
        state = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    receivedReports: [new ReceivedObservationReport(report, new SimulationTime(2_000))]
                ),
            }
        ) with
        {
            Time = new SimulationTime(2_000),
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(2),
        };
        FactionInvestigationProposal proposal = GameSimulation
            .DecideFactionInvestigation(state, state.Factions[0])
            .Proposal!;
        return GameSimulation.ApplyFactionInvestigation(state, proposal, FactionTestWorld.ShipCatalog).CandidateState;
    }

    private static SimulationState CreateActiveWithHistoricalCompletion()
    {
        SimulationState state = CreateLongActiveInvestigation();
        FactionState faction = state.Factions[0];
        FactionObservationState observation = faction.Observation!;
        ShipState source = state.GetRequiredShip(new ShipInstanceId(2));
        state = state.ReplaceShip(
            source.InstanceId,
            source with
            {
                SensorKnowledge = new SensorKnowledge(3, source.SensorKnowledge.Contacts),
            }
        );
        var historical = new ObservationReportSnapshot(
            new ObservationReportId(2),
            source.InstanceId,
            new SensorContactId(2),
            FactionTestWorld.Gamma,
            default,
            new SimulationTime(0),
            SensorContactIdentification.Detected
        );
        state = state.ReplaceFaction(
            faction.Id,
            faction with
            {
                Observation = new FactionObservationState(
                    ObservationResponsePosture.Enabled,
                    receivedReports:
                    [
                        .. observation.ReceivedReports,
                        new ReceivedObservationReport(historical, state.Time, ObservationReportHandling.Handled),
                    ],
                    activeInvestigation: observation.ActiveInvestigation,
                    completionWatermarks:
                    [
                        new ObservationLocationCompletionWatermark(FactionTestWorld.Gamma, state.Time),
                    ]
                ),
            }
        ) with
        {
            ObservationReportIdAllocator = ObservationReportIdAllocator.Restore(3),
        };
        return GameSimulation
            .AdvanceTo(state, new SimulationTime(3_000), FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog)
            .State;
    }

    private static StrategicMap CreateLongInvestigationMap() =>
        new(
            [
                new StrategicLocation(FactionTestWorld.Alpha, "Alpha", default),
                new StrategicLocation(FactionTestWorld.Beta, "Beta", default),
                new StrategicLocation(FactionTestWorld.Gamma, "Gamma", default),
            ],
            [new StrategicRoute(FactionTestWorld.Alpha, FactionTestWorld.Beta, new SimulationDuration(70_000))]
        );

    private static void AssertContinuation(GameSimulation simulation, SimulationTime target)
    {
        GameSimulation uninterrupted = Advance(simulation, target);
        GameSimulation reloaded = Advance(Reload(simulation), target);
        Assert.Equal(GamePersistence.Serialize(uninterrupted, Metadata), GamePersistence.Serialize(reloaded, Metadata));
    }

    private static GameSimulation Advance(GameSimulation simulation, SimulationTime target) =>
        Restore(
            GameSimulation
                .AdvanceTo(
                    simulation.CaptureState(),
                    target,
                    FactionTestWorld.ShipCatalog,
                    FactionTestWorld.FactionCatalog
                )
                .State
        );

    private static GameSimulation Restore(SimulationState state) =>
        GameSimulation.RestoreState(state, FactionTestWorld.ShipCatalog, FactionTestWorld.FactionCatalog);

    private static GameSimulation Reload(GameSimulation simulation) =>
        GamePersistence
            .Deserialize(
                GamePersistence.Serialize(simulation, Metadata),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog,
                "continuation-v8.json"
            )
            .Simulation;

    private static JsonObject FindWork(JsonArray work, long id) =>
        work.Single(item => item!["id"]!.GetValue<long>() == id)!.AsObject();

    private static void AssertRejectedWithoutMutation(
        JsonObject invalid,
        GameSimulation live,
        byte[] before,
        string source
    )
    {
        Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(
                Encoding.UTF8.GetBytes(invalid.ToJsonString()),
                FactionTestWorld.ShipCatalog,
                FactionTestWorld.FactionCatalog,
                source
            )
        );
        Assert.Equal(before, GamePersistence.Serialize(live, Metadata));
    }

    private static JsonObject ToHistoricalV7(byte[] current)
    {
        JsonObject root = Parse(current);
        root["schemaVersion"] = 7;
        root["simulationRulesVersion"] = "faction-intent-autonomous-assignment-v1";
        JsonObject simulation = root["simulation"]!.AsObject();
        simulation.Remove("observationReportAllocatorNextId");
        foreach (JsonNode? faction in simulation["factions"]!.AsArray())
        {
            faction!.AsObject().Remove("observation");
        }
        return root;
    }

    private static void AssertEmptyDisabledObservation(JsonObject observation)
    {
        Assert.Equal("disabled", observation["posture"]!.GetValue<string>());
        Assert.Empty(observation["inFlightReports"]!.AsArray());
        Assert.Empty(observation["receivedReports"]!.AsArray());
        Assert.Null(observation["activeInvestigation"]);
        Assert.Empty(observation["completionWatermarks"]!.AsArray());
    }

    private static JsonObject Parse(byte[] json) => JsonNode.Parse(json)!.AsObject();
}
