using AlterCourse.Core.AI;
using AlterCourse.Core.Factions;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.AI;

/// <summary>Verifies bounded information-limited faction investigation decisions.</summary>
public sealed class FactionInvestigationPolicyTests
{
    private static readonly FactionId Faction = new(3);
    private static readonly LocationId Origin = new("origin");
    private static readonly LocationId Destination = new("destination");

    /// <summary>Confirms report ordering is newest, earliest receipt, then lowest stable identity.</summary>
    [Fact]
    public void ChoosesReportByStablePriorityIndependentOfInputOrder()
    {
        ReceivedObservationReport oldest = Received(1, observer: 9, observedAt: 80_000, receivedAt: 82_000);
        ReceivedObservationReport laterReceipt = Received(2, observer: 9, observedAt: 90_000, receivedAt: 93_000);
        ReceivedObservationReport higherId = Received(4, observer: 9, observedAt: 90_000, receivedAt: 92_000);
        ReceivedObservationReport selected = Received(3, observer: 9, observedAt: 90_000, receivedAt: 92_000);

        FactionInvestigationDecisionExplanation forward = Evaluate(
            [oldest, laterReceipt, higherId, selected],
            [Asset(2)]
        );
        FactionInvestigationDecisionExplanation reverse = Evaluate(
            [selected, higherId, laterReceipt, oldest],
            [Asset(2)]
        );

        Assert.Equal(new ObservationReportId(3), forward.Proposal!.SourceReport.ReportId);
        Assert.Equal(forward.Proposal, reverse.Proposal);
        Assert.Equal(FactionInvestigationReportReason.Selected, Assert.Single(forward.Reports).Reason);
        Assert.False(forward.RandomnessUsed);
    }

    /// <summary>Confirms responder ranking prefers shortest direct travel and then lowest ship identity.</summary>
    [Fact]
    public void ChoosesResponderByRouteThenIdentityIndependentOfInputOrder()
    {
        FactionShipAssignmentSnapshot slow = Asset(2, "slow-origin");
        FactionShipAssignmentSnapshot tiedHigh = Asset(8);
        FactionShipAssignmentSnapshot tiedLow = Asset(4);
        FactionKnownRouteSnapshot slowRoute = Route("slow-origin", 5_000);
        FactionKnownRouteSnapshot fastRoute = Route("origin", 2_000);

        FactionInvestigationDecisionExplanation result = Evaluate(
            [Received(1, observer: 9, observedAt: 90_000, receivedAt: 92_000)],
            [tiedHigh, slow, tiedLow],
            [fastRoute, slowRoute]
        );

        Assert.Equal(new ShipInstanceId(4), result.Proposal!.ResponderShipId);
        Assert.Equal(Origin, result.Proposal.OriginLocationId);
        Assert.Equal(Destination, result.Proposal.DestinationLocationId);
        Assert.Equal(FactionInvestigationDecisionTieRule.ReportRecencyThenResponderRoute, result.TieRule);
    }

    /// <summary>Confirms observer, player, committed, traveling, and unreachable candidates are explained.</summary>
    [Fact]
    public void ExplainsEveryCandidateExclusionAndAcceptsAlreadyAtDestination()
    {
        ReceivedObservationReport report = Received(1, observer: 2, observedAt: 90_000, receivedAt: 92_000);
        FactionInvestigationDecisionExplanation result = Evaluate(
            [report],
            [
                Asset(2),
                Asset(3, player: true),
                Asset(4, committed: true),
                TravelingAsset(5),
                Asset(6, "unreachable"),
                Asset(7, "destination"),
            ]
        );

        Assert.Equal(new ShipInstanceId(7), result.Proposal!.ResponderShipId);
        Assert.Equal(
            0,
            result
                .Candidates.Single(candidate => candidate.ShipId == new ShipInstanceId(7))
                .DirectRouteDuration!.Value.Milliseconds
        );
        Assert.Equal(
            [
                FactionInvestigationCandidateReason.ReportingObserverRejected,
                FactionInvestigationCandidateReason.PlayerShipRejected,
                FactionInvestigationCandidateReason.AlreadyCommitted,
                FactionInvestigationCandidateReason.NotAtLocation,
                FactionInvestigationCandidateReason.DirectRouteUnavailable,
                FactionInvestigationCandidateReason.Eligible,
            ],
            result.Candidates.Select(candidate => candidate.Reason)
        );
    }

    /// <summary>Confirms freshness is half-open and future observations are rejected explicitly.</summary>
    [Fact]
    public void FreshnessAcceptsLastMillisecondAndRejectsBoundaryAndFuture()
    {
        ReceivedObservationReport fresh = Received(1, observer: 9, observedAt: 40_001, receivedAt: 42_000);
        ReceivedObservationReport expired = Received(2, observer: 9, observedAt: 40_000, receivedAt: 42_000);
        ReceivedObservationReport future = Received(3, observer: 9, observedAt: 100_001, receivedAt: 100_001);

        FactionInvestigationDecisionExplanation result = Evaluate([fresh], [Asset(2)]);
        FactionInvestigationDecisionExplanation rejected = Evaluate([expired, future], [Asset(2)]);

        Assert.Equal(new ObservationReportId(1), result.Proposal!.SourceReport.ReportId);
        Assert.Contains(
            rejected.Reports,
            item => item.ReceivedReport == future && item.Reason == FactionInvestigationReportReason.FutureObservation
        );
        Assert.Contains(
            rejected.Reports,
            item => item.ReceivedReport == expired && item.Reason == FactionInvestigationReportReason.Expired
        );
    }

    /// <summary>Confirms handled and watermark-covered reports cannot trigger a repeated response.</summary>
    [Fact]
    public void HandlingAndLocationWatermarkSuppressRepeatedResponse()
    {
        ReceivedObservationReport handled = new(
            Report(1, observer: 9, observedAt: 70_000),
            new SimulationTime(72_000),
            ObservationReportHandling.Handled
        );
        ReceivedObservationReport covered = Received(2, observer: 9, observedAt: 80_000, receivedAt: 82_000);
        var watermark = new ObservationLocationCompletionWatermark(Destination, new SimulationTime(80_000));

        FactionInvestigationDecisionExplanation result = Evaluate(
            [covered, handled],
            [Asset(2)],
            watermarks: [watermark]
        );

        Assert.Equal(FactionInvestigationDecisionOutcome.NoActionableReport, result.Outcome);
        Assert.Equal(
            [FactionInvestigationReportReason.CoveredByCompletionWatermark, FactionInvestigationReportReason.Handled],
            result.Reports.Select(item => item.Reason)
        );
    }

    /// <summary>Confirms disabled posture and an occupied response slot make no candidate decision.</summary>
    [Theory]
    [InlineData(ObservationResponsePosture.Disabled, false, FactionInvestigationDecisionOutcome.PostureDisabled)]
    [InlineData(
        ObservationResponsePosture.Enabled,
        true,
        FactionInvestigationDecisionOutcome.ActiveInvestigationExists
    )]
    public void PostureAndActiveInvestigationBoundResponse(
        ObservationResponsePosture posture,
        bool active,
        FactionInvestigationDecisionOutcome expected
    )
    {
        FactionInvestigationDecisionExplanation result = Evaluate(
            [Received(1, observer: 9, observedAt: 90_000, receivedAt: 92_000)],
            [Asset(2)],
            posture: posture,
            active: active
        );

        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Proposal);
        Assert.Empty(result.Candidates);
    }

    /// <summary>Confirms input collections are copied, canonically ordered, bounded, and null-safe.</summary>
    [Fact]
    public void InputOwnsBoundedImmutableCollections()
    {
        var reports = new List<ReceivedObservationReport> { Received(2, 9, 10_000, 12_000) };
        var input = new FactionInvestigationDecisionInput(
            Faction,
            new SimulationTime(100_000),
            ObservationResponsePosture.Enabled,
            reports,
            null,
            false,
            null,
            null
        );
        reports.Add(Received(1, 9, 20_000, 22_000));

        Assert.Single(input.ReceivedReports);
        Assert.Empty(input.Assets);
        Assert.Empty(input.KnownRoutes);
        Assert.Empty(input.CompletionWatermarks);
        Assert.Throws<ArgumentException>(() =>
            new FactionInvestigationDecisionInput(
                Faction,
                default,
                ObservationResponsePosture.Enabled,
                Enumerable.Range(1, 17).Select(index => Received(index, 9, index, index + 2_000)),
                [],
                false,
                [],
                []
            )
        );
    }

    private static FactionInvestigationDecisionExplanation Evaluate(
        IEnumerable<ReceivedObservationReport> reports,
        IEnumerable<FactionShipAssignmentSnapshot> assets,
        IEnumerable<FactionKnownRouteSnapshot>? routes = null,
        IEnumerable<ObservationLocationCompletionWatermark>? watermarks = null,
        ObservationResponsePosture posture = ObservationResponsePosture.Enabled,
        bool active = false
    ) =>
        FactionInvestigationPolicy.Evaluate(
            new FactionInvestigationDecisionInput(
                Faction,
                new SimulationTime(100_000),
                posture,
                reports,
                watermarks,
                active,
                assets,
                routes ?? [Route("origin", 2_000)]
            )
        );

    private static FactionShipAssignmentSnapshot Asset(
        long id,
        string location = "origin",
        bool player = false,
        bool committed = false
    ) =>
        new(new ShipInstanceId(id), player, FactionShipStrategicStatus.AtLocation, new LocationId(location), committed);

    private static FactionShipAssignmentSnapshot TravelingAsset(long id) =>
        new(new ShipInstanceId(id), false, FactionShipStrategicStatus.Traveling, null, false);

    private static FactionKnownRouteSnapshot Route(string origin, long duration) =>
        new(new LocationId(origin), Destination, new SimulationDuration(duration));

    private static ReceivedObservationReport Received(long id, long observer, long observedAt, long receivedAt) =>
        new(Report(id, observer, observedAt), new SimulationTime(receivedAt));

    private static ObservationReportSnapshot Report(long id, long observer, long observedAt) =>
        new(
            new ObservationReportId(id),
            new ShipInstanceId(observer),
            new SensorContactId(id),
            Destination,
            new TacticalPosition(id, -id),
            new SimulationTime(observedAt),
            SensorContactIdentification.Detected
        );
}
