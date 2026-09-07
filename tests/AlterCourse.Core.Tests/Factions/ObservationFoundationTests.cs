using AlterCourse.Core.Factions;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Tests.Factions;

/// <summary>Verifies bounded actor-safe observation identity and state contracts.</summary>
public sealed class ObservationFoundationTests
{
    /// <summary>Confirms report identity allocation is positive, monotonic, restorable, and overflow-safe.</summary>
    [Fact]
    public void ReportIdentityAllocatorPreservesDeterministicContinuation()
    {
        (ObservationReportIdAllocator following, ObservationReportId first) = ObservationReportIdAllocator
            .Create()
            .Allocate();
        (ObservationReportIdAllocator restoredFollowing, ObservationReportId restored) = ObservationReportIdAllocator
            .Restore(19)
            .Allocate();

        Assert.Equal(1, first.Value);
        Assert.Equal(2, following.NextId);
        Assert.Equal(19, restored.Value);
        Assert.Equal(20, restoredFollowing.NextId);
        Assert.Throws<ArgumentOutOfRangeException>(() => ObservationReportIdAllocator.Restore(0));
        Assert.Throws<OverflowException>(() => ObservationReportIdAllocator.Restore(long.MaxValue).Allocate());
    }

    /// <summary>Confirms historical reports preserve legitimate learned facts without a hidden target identity.</summary>
    [Fact]
    public void ReportSnapshotPreservesOnlyObserverLocalFacts()
    {
        ObservationReportSnapshot report = Report(7, observer: 2, contact: 31, observedAt: 1_000, location: "vesper");

        Assert.Equal(new ObservationReportId(7), report.ReportId);
        Assert.Equal(new ShipInstanceId(2), report.ObserverShipId);
        Assert.Equal(new SensorContactId(31), report.ObserverContactId);
        Assert.Equal(new LocationId("vesper"), report.ObservedAtLocationId);
        Assert.Equal(new TacticalPosition(12, -4), report.ObservedPosition);
        Assert.Equal(SensorContactIdentification.Identified, report.Identification);
        Assert.Equal("Wayfarer", report.KnownVesselDisplayName);
        Assert.Equal("Pathfinder", report.KnownDesignDisplayName);
    }

    /// <summary>Confirms invalid identification combinations and uninitialized provenance fail at construction.</summary>
    [Fact]
    public void ReportSnapshotRejectsMalformedActorFacts()
    {
        Assert.Throws<ArgumentException>(() =>
            new ObservationReportSnapshot(
                new ObservationReportId(1),
                default,
                new SensorContactId(1),
                new LocationId("vesper"),
                default,
                default,
                SensorContactIdentification.Detected
            )
        );
        Assert.Throws<ArgumentException>(() =>
            new ObservationReportSnapshot(
                new ObservationReportId(1),
                new ShipInstanceId(2),
                new SensorContactId(1),
                new LocationId("vesper"),
                default,
                default,
                SensorContactIdentification.Detected,
                "Hidden name",
                null
            )
        );
    }

    /// <summary>Confirms default response state is disabled and every approved collection bound is enforced.</summary>
    [Fact]
    public void FactionObservationStateEnforcesApprovedBounds()
    {
        var disabled = new FactionObservationState();

        Assert.Equal(ObservationResponsePosture.Disabled, disabled.Posture);
        Assert.Empty(disabled.InFlightReports);
        Assert.Empty(disabled.ReceivedReports);
        Assert.Null(disabled.ActiveInvestigation);
        Assert.Empty(disabled.CompletionWatermarks);
        Assert.Equal(8, FactionObservationState.MaximumInFlightReports);
        Assert.Equal(16, FactionObservationState.MaximumReceivedReports);
        Assert.Equal(1, FactionObservationState.MaximumActiveInvestigations);
        Assert.Equal(24, FactionObservationState.MaximumCompletionWatermarks);
        Assert.Equal(60_000, FactionObservationState.ReportFreshnessMilliseconds);

        Assert.Throws<ArgumentException>(() => new FactionObservationState(inFlightReports: InFlightReports(9)));
        Assert.Throws<ArgumentException>(() => new FactionObservationState(receivedReports: Reports(17)));
        Assert.Throws<ArgumentException>(() =>
            new FactionObservationState(
                completionWatermarks: Enumerable
                    .Range(1, 25)
                    .Select(index => new ObservationLocationCompletionWatermark(
                        new LocationId($"location-{index}"),
                        default
                    ))
            )
        );
    }

    /// <summary>Confirms report collections own caller input and use their canonical persistence order.</summary>
    [Fact]
    public void ObservationCollectionsAreImmutableAndCanonicallyOrdered()
    {
        var reports = new List<ReceivedObservationReport>
        {
            new(Report(3, 2, 3, 500, "third"), new SimulationTime(2_500)),
            new(Report(2, 2, 2, 500, "second"), new SimulationTime(2_500)),
            new(Report(1, 2, 1, 400, "first"), new SimulationTime(2_400)),
        };
        var state = new FactionObservationState(receivedReports: reports);
        reports.Clear();

        Assert.Equal(
            [new ObservationReportId(2), new ObservationReportId(3), new ObservationReportId(1)],
            state.ReceivedReports.Select(report => report.Report.ReportId)
        );
        Assert.Throws<ArgumentException>(() =>
            new FactionObservationState(receivedReports: [Reports(1).Single(), Reports(1).Single()])
        );
    }

    /// <summary>Confirms active state retains enough source provenance to survive received-report eviction.</summary>
    [Fact]
    public void ActiveInvestigationOwnsItsSourceCorrelation()
    {
        ObservationReportSnapshot source = Report(9, observer: 2, contact: 4, observedAt: 500, location: "meridian");
        var active = new ActiveFactionInvestigation(
            source,
            new ShipInstanceId(3),
            new LocationId("vesper"),
            new LocationId("meridian"),
            new SimulationTime(2_000),
            new SimulationTime(2_500),
            new ShipOrderId(44)
        );
        var state = new FactionObservationState(activeInvestigation: active, receivedReports: []);

        Assert.Empty(state.ReceivedReports);
        Assert.Same(source, state.ActiveInvestigation!.SourceReport);
        Assert.Equal(new SensorContactId(4), state.ActiveInvestigation.SourceReport.ObserverContactId);
        Assert.Equal(new LocationId("vesper"), state.ActiveInvestigation.OriginLocationId);
        Assert.Equal(new LocationId("meridian"), state.ActiveInvestigation.DestinationLocationId);
        Assert.Equal(new SimulationTime(2_000), state.ActiveInvestigation.SourceReceivedAt);
        Assert.Equal(new ShipOrderId(44), state.ActiveInvestigation.OrderId);
    }

    private static IEnumerable<ReceivedObservationReport> Reports(int count) =>
        Enumerable
            .Range(1, count)
            .Select(index => new ReceivedObservationReport(
                Report(index, 2, index, index, $"location-{index}"),
                new SimulationTime(index + 2_000)
            ));

    private static IEnumerable<ObservationReportInFlight> InFlightReports(int count) =>
        Enumerable
            .Range(1, count)
            .Select(index => new ObservationReportInFlight(
                Report(index, 2, index, index, $"location-{index}"),
                new ScheduledWorkId(index),
                new SimulationTime(index + 2_000)
            ));

    private static ObservationReportSnapshot Report(
        long id,
        long observer,
        long contact,
        long observedAt,
        string location
    ) =>
        new(
            new ObservationReportId(id),
            new ShipInstanceId(observer),
            new SensorContactId(contact),
            new LocationId(location),
            new TacticalPosition(12, -4),
            new SimulationTime(observedAt),
            SensorContactIdentification.Identified,
            "Wayfarer",
            "Pathfinder"
        );
}
