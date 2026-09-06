using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Tests.Gameplay;

/// <summary>
/// Proves the whole Strategic Contact Reporting causal boundary headlessly: what the player observed stays
/// what the player observed, whatever the world does to the observed ship afterwards.
/// </summary>
public sealed class StrategicContactReportingScenarioTests
{
    private static readonly LocationId Dawn = new("dawn-anchor");
    private static readonly LocationId Vesper = new("vesper-reach");
    private static readonly LocationId Meridian = new("meridian-drift");

    // Every step is 100 ms. Detection lands at 3500 ms on the locked first-contact timeline and the active
    // scan completes 2000 ms later; the remaining count carries the run to the last current observation at
    // 24000 ms, from which staleness (24100 ms) and loss (29100 ms) follow.
    private const int StepsFromIdentificationToLastCurrent = 185;
    private const int StepsFromStaleToLoss = 50;
    private const int StepsPerTravelBetweenDawnAndVesper = 120;
    private const int StepsPerTravelBetweenVesperAndMeridian = 140;

    private readonly StrategicContactReportingProofFixture _fixture = new();

    /// <summary>Confirms one observation survives hidden movement, the observer's own travel, and a save.</summary>
    [Fact]
    public void ObservedReportSurvivesHiddenMovementObserverTravelAndPersistence()
    {
        GameSimulation game = _fixture.CreateDefault();
        Assert.Equal(
            PowerAllocationOutcome.Accepted,
            game.ApplyPowerAllocationPreset(PowerAllocationPreset.PrioritizeSensors).Outcome
        );

        SensorContactId contactId = DetectAndIdentifyKestrelAtDawnAnchor(game);
        StrategicContactReportProjection lost = LoseTheContactWithoutLosingTheReport(game, contactId);
        game = MoveKestrelAwayUnobserved(game, lost);
        TravelToVesperReach(game, contactId, lost);
        GameSimulation resumed = CrossTheSaveBoundary(game);
        ReacquireKestrelAtMeridianDrift(game, resumed, contactId, lost);
    }

    /// <summary>Confirms the target's own active order is hidden truth that no report can be read from.</summary>
    [Fact]
    public void KestrelsActiveOrderNeverReachesTheObserversReports()
    {
        // The two worlds differ in exactly one hidden fact: whether Kestrel carries a durable hold order, which
        // also puts one extra scheduled wake in the world. The hold outlives the whole comparison window, so
        // neither Kestrel moves strategically at any point compared here.
        GameSimulation orderless = _fixture.CreateWithKestrelOrder(null);
        GameSimulation holding = _fixture.CreateWithKestrelOrder(new HoldUntilOrderStart(new SimulationTime(60000)));
        Assert.Null(Milestone3ProofFixture.Kestrel(orderless).ActiveOrder);
        Assert.NotNull(Milestone3ProofFixture.Kestrel(holding).ActiveOrder);

        Assert.Equal(
            PowerAllocationOutcome.Accepted,
            orderless.ApplyPowerAllocationPreset(PowerAllocationPreset.PrioritizeSensors).Outcome
        );
        Assert.Equal(
            PowerAllocationOutcome.Accepted,
            holding.ApplyPowerAllocationPreset(PowerAllocationPreset.PrioritizeSensors).Outcome
        );

        // Detection, the last current observation, staleness and loss: the reports must agree at every stage,
        // not merely at the end, where two differences could cancel each other out.
        foreach (int steps in new[] { 35, 205, 1, 50 })
        {
            orderless.AdvanceFixedSteps(steps);
            holding.AdvanceFixedSteps(steps);
            Assert.Equal(Reports(orderless), Reports(holding));
        }

        Assert.Equal(SensorContactStatus.Lost, Assert.Single(Reports(orderless)).Status);
        Assert.Equal(new AtLocationState(Dawn), Milestone3ProofFixture.Kestrel(holding).StrategicState);
    }

    /// <summary>Detects and scans Kestrel, so the report carries both its frame and the learned names.</summary>
    private static SensorContactId DetectAndIdentifyKestrelAtDawnAnchor(GameSimulation game)
    {
        AdvanceUntilResult detection = game.AdvanceUntilNextPlayerRelevantEvent();
        Assert.Equal(3500, detection.StoppedAt.Milliseconds);
        Assert.Equal(PlayerAdvanceEventKind.SensorContactDetected, Assert.Single(detection.ResolvedEvents).Kind);

        SensorContactId contactId = Assert.Single(detection.Projection.Ship.Sensors.Contacts).Id;
        Assert.Equal(ActiveSensorScanOutcome.Accepted, game.RequestActiveSensorScan(contactId).Outcome);
        SimulationAdvanceResult scan = game.AdvanceFixedSteps(20);
        Assert.Equal(5500, scan.FinalTime.Milliseconds);
        Assert.Equal(PlayerAdvanceEventKind.ActiveSensorScanCompleted, Assert.Single(scan.ResolvedEvents).Kind);

        StrategicContactReportProjection identified = Assert.Single(Reports(game));
        Assert.Equal(contactId, identified.ContactId);
        Assert.Equal(Dawn, identified.ObservedAtLocationId);
        Assert.Equal(new SimulationTime(5500), identified.LastObservedAt);
        Assert.Equal(SensorContactStatus.Current, identified.Status);
        Assert.Equal(SensorContactIdentification.Identified, identified.Identification);
        Assert.Equal("Survey Vessel Kestrel", identified.KnownVesselDisplayName);
        Assert.Equal("Pathfinder class", identified.KnownDesignDisplayName);
        Assert.Equal(contactId, Assert.Single(game.GetPlayerProjection().Ship.Sensors.Contacts).Id);
        return contactId;
    }

    /// <summary>Runs to the loss boundary, where staleness and loss may change status and nothing else.</summary>
    private static StrategicContactReportProjection LoseTheContactWithoutLosingTheReport(
        GameSimulation game,
        SensorContactId contactId
    )
    {
        SimulationAdvanceResult lastCurrent = game.AdvanceFixedSteps(StepsFromIdentificationToLastCurrent);
        Assert.Equal(24000, lastCurrent.FinalTime.Milliseconds);
        StrategicContactReportProjection current = Assert.Single(Reports(game));

        // Staleness is a lapsed observation rather than scheduled work, so it is reached by stepping the clock
        // across the boundary, not by seeking the next scheduled player event.
        SimulationAdvanceResult stale = game.AdvanceFixedSteps(1);
        Assert.Equal(24100, stale.FinalTime.Milliseconds);
        Assert.Equal(PlayerAdvanceEventKind.SensorContactStale, Assert.Single(stale.ResolvedEvents).Kind);
        StrategicContactReportProjection staleReport = Assert.Single(Reports(game));
        Assert.Equal(SensorContactStatus.Stale, staleReport.Status);
        AssertSameObservation(current, staleReport);

        SimulationAdvanceResult loss = game.AdvanceFixedSteps(StepsFromStaleToLoss);
        Assert.Equal(29100, loss.FinalTime.Milliseconds);
        Assert.Equal(PlayerAdvanceEventKind.SensorContactLost, Assert.Single(loss.ResolvedEvents).Kind);
        StrategicContactReportProjection lost = Assert.Single(Reports(game));
        Assert.Equal(contactId, lost.ContactId);
        Assert.Equal(SensorContactStatus.Lost, lost.Status);
        AssertSameObservation(current, lost);

        // The tactical surface drops a lost contact because it is no longer present; the strategic report
        // keeps it because the observation still happened.
        Assert.Empty(game.GetPlayerProjection().Ship.Sensors.Contacts);
        return lost;
    }

    /// <summary>Moves Kestrel out from under the player's stale knowledge under its own travel orders.</summary>
    private GameSimulation MoveKestrelAwayUnobserved(GameSimulation game, StrategicContactReportProjection lost)
    {
        GameSimulation ordered = _fixture.OrderKestrelTo(game, Vesper);
        ordered.AdvanceFixedSteps(StepsPerTravelBetweenDawnAndVesper);

        // The hidden-truth probe exists only to prove the world really moved; the assertion that matters is
        // that the projected report did not follow it.
        Assert.Equal(new AtLocationState(Vesper), Milestone3ProofFixture.Kestrel(ordered).StrategicState);
        Assert.Equal(41200, ordered.CaptureState().Time.Milliseconds);
        Assert.Equal(lost, Assert.Single(Reports(ordered)));

        // Kestrel travels on to Meridian Drift so that the player's own travel to Vesper Reach cannot be
        // confused with a legitimate reacquisition: the two ships must not meet when the player arrives.
        return _fixture.OrderKestrelTo(ordered, Meridian);
    }

    /// <summary>Travels the observer itself, which requalifies nothing it previously observed.</summary>
    private static void TravelToVesperReach(
        GameSimulation game,
        SensorContactId contactId,
        StrategicContactReportProjection lost
    )
    {
        Assert.Equal(TravelOutcome.Accepted, game.RequestTravel(new TravelIntent(Vesper)).Outcome);
        game.AdvanceFixedSteps(StepsPerTravelBetweenDawnAndVesper);

        Assert.Equal(53300, game.CaptureState().Time.Milliseconds);
        Assert.Equal(Vesper, game.GetPlayerProjection().Strategic.CurrentLocation!.Id);
        Assert.Equal(lost, KestrelReport(game, contactId));
    }

    /// <summary>Round-trips the world, proving the reports and the continuation both survive a save.</summary>
    private GameSimulation CrossTheSaveBoundary(GameSimulation game)
    {
        GameSimulation resumed = _fixture.RoundTrip(game, "strategic-contact-reporting-at-vesper.json");
        Assert.Equal(Reports(game), Reports(resumed));

        SimulationAdvanceResult live = game.AdvanceFixedSteps(5);
        SimulationAdvanceResult restored = resumed.AdvanceFixedSteps(5);
        Assert.Equal(live, restored);
        AssertSameSave(game, resumed);
        return resumed;
    }

    /// <summary>
    /// Reacquires Kestrel where it actually went, which requalifies the same observer-local contact instead of
    /// opening a second one, and drives the uninterrupted and resumed runs identically to the same save.
    /// </summary>
    private static void ReacquireKestrelAtMeridianDrift(
        GameSimulation game,
        GameSimulation resumed,
        SensorContactId contactId,
        StrategicContactReportProjection lost
    )
    {
        Assert.Equal(TravelOutcome.Accepted, game.RequestTravel(new TravelIntent(Meridian)).Outcome);
        Assert.Equal(TravelOutcome.Accepted, resumed.RequestTravel(new TravelIntent(Meridian)).Outcome);
        SimulationAdvanceResult live = game.AdvanceFixedSteps(StepsPerTravelBetweenVesperAndMeridian);
        SimulationAdvanceResult restored = resumed.AdvanceFixedSteps(StepsPerTravelBetweenVesperAndMeridian);
        Assert.Equal(67800, live.FinalTime.Milliseconds);
        Assert.Equal(live, restored);
        AssertSameSave(game, resumed);

        Assert.Contains(restored.ResolvedEvents, @event => @event.Kind == PlayerAdvanceEventKind.TravelArrived);
        Assert.Contains(
            restored.ResolvedEvents,
            @event => @event.Kind == PlayerAdvanceEventKind.SensorContactReacquired
        );
        StrategicContactReportProjection reacquired = KestrelReport(resumed, contactId);
        Assert.Equal(Meridian, reacquired.ObservedAtLocationId);
        Assert.Equal(new SimulationTime(67800), reacquired.LastObservedAt);
        Assert.Equal(Milestone3ProofFixture.Kestrel(resumed).TacticalPosition, reacquired.LastObservedPosition);
        Assert.NotEqual(lost.LastObservedPosition, reacquired.LastObservedPosition);
        Assert.Equal(SensorContactStatus.Current, reacquired.Status);
        Assert.Equal(SensorContactIdentification.Identified, reacquired.Identification);
        Assert.Equal("Survey Vessel Kestrel", reacquired.KnownVesselDisplayName);
        Assert.Equal("Pathfinder class", reacquired.KnownDesignDisplayName);
    }

    private static void AssertSameObservation(
        StrategicContactReportProjection expected,
        StrategicContactReportProjection actual
    )
    {
        Assert.Equal(expected.ContactId, actual.ContactId);
        Assert.Equal(expected.ObservedAtLocationId, actual.ObservedAtLocationId);
        Assert.Equal(expected.LastObservedPosition, actual.LastObservedPosition);
        Assert.Equal(expected.LastObservedAt, actual.LastObservedAt);
        Assert.Equal(expected.Identification, actual.Identification);
        Assert.Equal(expected.KnownVesselDisplayName, actual.KnownVesselDisplayName);
        Assert.Equal(expected.KnownDesignDisplayName, actual.KnownDesignDisplayName);
    }

    private static void AssertSameSave(GameSimulation expected, GameSimulation actual) =>
        Assert.Equal(
            GamePersistence.Serialize(expected, Milestone3ProofFixture.Metadata),
            GamePersistence.Serialize(actual, Milestone3ProofFixture.Metadata)
        );

    private static IReadOnlyList<StrategicContactReportProjection> Reports(GameSimulation game) =>
        game.GetPlayerProjection().Strategic.KnownContactReports;

    /// <summary>Selects Kestrel's report by observer-local identity, past reports of other ships.</summary>
    private static StrategicContactReportProjection KestrelReport(GameSimulation game, SensorContactId contactId) =>
        Assert.Single(Reports(game), report => report.ContactId == contactId);
}
