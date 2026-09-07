using System.Text;
using System.Text.Json.Nodes;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tests.Gameplay;

namespace AlterCourse.Core.Tests.Persistence;

/// <summary>Verifies the V6 observed-location frame, its V5 migration, and its load-time admission bounds.</summary>
/// <remarks>
/// The V5 fixtures are produced by downgrading a live current document rather than by hand-writing a whole
/// V5 world: that keeps every unrelated member exactly as the V5 contract admitted it, so a failure
/// here is always about the frame and never about a stale hand-copied envelope.
/// </remarks>
public sealed class GamePersistenceV6ContactReportTests
{
    private static readonly LocationId Dawn = new("dawn-anchor");

    // Step counts on the locked first-contact timeline shared with StrategicContactReportTests:
    // detection at 3500 ms, the last current observation at 24000 ms, stale at 24100 ms, lost at 29100 ms.
    private const int StepsToAcquisition = 35;
    private const int StepsToStale = 241;
    private const int StepsToLoss = 291;

    private readonly Milestone3ProofFixture _fixture = new();

    /// <summary>Confirms a current round trip preserves the V6 frame and the whole report for every status.</summary>
    [Theory]
    [InlineData(StepsToAcquisition, SensorContactStatus.Current)]
    [InlineData(StepsToStale, SensorContactStatus.Stale)]
    [InlineData(StepsToLoss, SensorContactStatus.Lost)]
    public void CurrentRoundTripPreservesObservedFrameAndReports(int steps, SensorContactStatus expected)
    {
        GameSimulation game = CreateObservedWorld(steps);
        IReadOnlyList<StrategicContactReportProjection> before = Reports(game);
        Assert.Equal(expected, Assert.Single(before).Status);

        byte[] saved = GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata);
        JsonObject contact = FirstPlayerContact(Parse(saved));
        LoadedGameSave loaded = GamePersistence.Deserialize(saved, _fixture.Catalog, "native-v7.json");

        Assert.Equal(7, Parse(saved)["schemaVersion"]!.GetValue<int>());
        Assert.Equal("dawn-anchor", contact["observedAtLocationId"]!.GetValue<string>());
        Assert.Equal(Dawn, Assert.Single(Player(loaded.Simulation).SensorKnowledge.Contacts).ObservedAtLocationId);
        Assert.Equal(before, Reports(loaded.Simulation));
        Assert.Equal(saved, GamePersistence.Serialize(loaded.Simulation, loaded.Metadata));
    }

    /// <summary>Confirms the V5 migration leaves the frame unqualified while preserving every other contact fact.</summary>
    /// <remarks>
    /// Runs on a current contact, where the observer is at Dawn Anchor and the target sits at the very
    /// same location: the frame the migration declines to invent is one it could have guessed correctly,
    /// which is exactly the guess ADR 0006 forbids on untrusted input.
    /// </remarks>
    [Fact]
    public void MigratedV5ContactsCarryNoFrameEvenWhenBothShipsShareALocation()
    {
        GameSimulation game = CreateObservedWorld(StepsToAcquisition);
        SensorContactTrack original = Assert.Single(Player(game).SensorKnowledge.Contacts);
        Assert.Equal(Dawn, original.ObservedAtLocationId);
        Assert.Equal(new AtLocationState(Dawn), Player(game).StrategicState);
        Assert.Equal(new AtLocationState(Dawn), Milestone3ProofFixture.Kestrel(game).StrategicState);

        LoadedGameSave loaded = LoadLegacyV5(game);
        SensorContactTrack migrated = Assert.Single(Player(loaded.Simulation).SensorKnowledge.Contacts);

        Assert.Null(migrated.ObservedAtLocationId);
        Assert.Equal(original with { ObservedAtLocationId = null }, migrated);
        Assert.Equal(
            7,
            Parse(GamePersistence.Serialize(loaded.Simulation, loaded.Metadata))["schemaVersion"]!.GetValue<int>()
        );
        Assert.Null(
            FirstPlayerContact(Parse(GamePersistence.Serialize(loaded.Simulation, loaded.Metadata)))[
                "observedAtLocationId"
            ]
        );
    }

    /// <summary>Confirms an unqualified legacy contact stays on the tactical surface but out of the reports.</summary>
    [Theory]
    [InlineData(StepsToAcquisition, SensorContactStatus.Current)]
    [InlineData(StepsToStale, SensorContactStatus.Stale)]
    public void UnqualifiedLegacyContactsAreTacticallyPresentButUnreported(int steps, SensorContactStatus expected)
    {
        GameSimulation restored = LoadLegacyV5(CreateObservedWorld(steps)).Simulation;

        Assert.Equal(expected, Assert.Single(restored.GetPlayerProjection().Ship.Sensors.Contacts).Status);
        Assert.Empty(Reports(restored));
    }

    /// <summary>Confirms a fresh observation after migration qualifies the legacy contact and reports it.</summary>
    [Fact]
    public void ObservationAfterMigrationQualifiesTheLegacyContact()
    {
        GameSimulation restored = LoadLegacyV5(CreateObservedWorld(StepsToAcquisition)).Simulation;
        SensorContactTrack legacy = Assert.Single(Player(restored).SensorKnowledge.Contacts);
        Assert.Empty(Reports(restored));

        restored.AdvanceFixedSteps(1);

        SensorContactTrack refreshed = Assert.Single(Player(restored).SensorKnowledge.Contacts);
        StrategicContactReportProjection report = Assert.Single(Reports(restored));
        Assert.Equal(legacy.Id, refreshed.Id);
        Assert.Equal(Dawn, refreshed.ObservedAtLocationId);
        Assert.Equal(legacy.Id, report.ContactId);
        Assert.Equal(Dawn, report.ObservedAtLocationId);
        Assert.Equal(SensorContactStatus.Current, report.Status);
    }

    /// <summary>Confirms a mid-scenario save and load reports exactly what uninterrupted progression reports.</summary>
    [Fact]
    public void InterruptedProgressionReportsMatchUninterruptedProgression()
    {
        GameSimulation uninterrupted = CreateObservedWorld(StepsToAcquisition);
        GameSimulation resumed = GamePersistence
            .Deserialize(
                GamePersistence.Serialize(CreateObservedWorld(StepsToAcquisition), Milestone3ProofFixture.Metadata),
                _fixture.Catalog,
                "mid-scenario-v7.json"
            )
            .Simulation;

        for (int stage = 0; stage < 3; stage++)
        {
            uninterrupted.AdvanceFixedSteps(100);
            resumed.AdvanceFixedSteps(100);
            Assert.Equal(Reports(uninterrupted), Reports(resumed));
        }

        Assert.Equal(
            GamePersistence.Serialize(uninterrupted, Milestone3ProofFixture.Metadata),
            GamePersistence.Serialize(resumed, Milestone3ProofFixture.Metadata)
        );
    }

    /// <summary>Confirms every unadmitted observed-location form fails closed before any state is replaced.</summary>
    [Theory]
    [InlineData("unknown-location")]
    [InlineData("whitespace")]
    [InlineData("empty")]
    [InlineData("oversized")]
    public void RejectsUnadmittedObservedLocations(string mutation)
    {
        byte[] valid = GamePersistence.Serialize(
            CreateObservedWorld(StepsToAcquisition),
            Milestone3ProofFixture.Metadata
        );
        byte[] invalid = Mutate(
            valid,
            root =>
                FirstPlayerContact(root)["observedAtLocationId"] = mutation switch
                {
                    "unknown-location" => "no-such-anchor",
                    "whitespace" => "   ",
                    "empty" => string.Empty,
                    "oversized" => new string('l', LocationId.MaximumLength + 1),
                    _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, "Unknown mutation."),
                }
        );

        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(invalid, _fixture.Catalog, $"{mutation}.json")
        );
        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
        Assert.Equal(
            valid,
            GamePersistence.Serialize(CreateObservedWorld(StepsToAcquisition), Milestone3ProofFixture.Metadata)
        );
    }

    /// <summary>Confirms a V5 document that already carries a frame is incompatible with the V5 contract.</summary>
    /// <remarks>
    /// The V5 shape is closed by <c>JsonUnmappedMemberHandling.Disallow</c>, so a forward-dated member
    /// cannot smuggle a frame past the historical mapper into the migration.
    /// </remarks>
    [Fact]
    public void RejectsV5DocumentCarryingAnObservedLocation()
    {
        byte[] invalid = Mutate(
            ToLegacyV5Document(
                GamePersistence.Serialize(CreateObservedWorld(StepsToAcquisition), Milestone3ProofFixture.Metadata)
            ),
            root => FirstPlayerContact(root)["observedAtLocationId"] = "dawn-anchor"
        );

        GamePersistenceException failure = Assert.Throws<GamePersistenceException>(() =>
            GamePersistence.Deserialize(invalid, _fixture.Catalog, "forward-dated-v5.json")
        );
        Assert.Equal(GamePersistenceFailure.InvalidData, failure.Failure);
    }

    private GameSimulation CreateObservedWorld(int steps)
    {
        GameSimulation game = _fixture.CreateDefault();
        Assert.Equal(
            PowerAllocationOutcome.Accepted,
            game.ApplyPowerAllocationPreset(PowerAllocationPreset.PrioritizeSensors).Outcome
        );
        game.AdvanceFixedSteps(steps);
        return game;
    }

    private LoadedGameSave LoadLegacyV5(GameSimulation game) =>
        GamePersistence.Deserialize(
            ToLegacyV5Document(GamePersistence.Serialize(game, Milestone3ProofFixture.Metadata)),
            _fixture.Catalog,
            "legacy-v5.json"
        );

    /// <summary>Rewrites a live V7 document into the V5 document the same world would have produced.</summary>
    private static byte[] ToLegacyV5Document(byte[] current) =>
        Mutate(
            current,
            root =>
            {
                root["schemaVersion"] = 5;
                root["simulationRulesVersion"] = "engineering-backbone-v1";
                root["simulation"]!.AsObject().Remove("factions");
                foreach (JsonNode? ship in root["simulation"]!["ships"]!.AsArray())
                {
                    ship!.AsObject().Remove("directControllerFactionId");
                    foreach (JsonNode? contact in ship!["sensorKnowledge"]!["contacts"]!.AsArray())
                    {
                        contact!.AsObject().Remove("observedAtLocationId");
                    }
                }
                foreach (JsonNode? work in root["simulation"]!["scheduler"]!["outstandingWork"]!.AsArray())
                {
                    work!.AsObject().Remove("targetKind");
                    work.AsObject().Remove("targetFactionId");
                }
            }
        );

    private static JsonObject FirstPlayerContact(JsonObject root) =>
        root["simulation"]!["ships"]![0]!["sensorKnowledge"]!["contacts"]![0]!.AsObject();

    private static IReadOnlyList<StrategicContactReportProjection> Reports(GameSimulation game) =>
        game.GetPlayerProjection().Strategic.KnownContactReports;

    private static ShipState Player(GameSimulation game) => Milestone3ProofFixture.Player(game);

    private static byte[] Mutate(byte[] source, Action<JsonObject> mutation)
    {
        JsonObject root = Parse(source);
        mutation(root);
        return Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static JsonObject Parse(byte[] json) => JsonNode.Parse(json)!.AsObject();
}
