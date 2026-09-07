using AlterCourse.Core.Factions;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.AI;

/// <summary>Defines the complete bounded actor-specific input to one investigation decision.</summary>
public sealed record FactionInvestigationDecisionInput
{
    private readonly ReadOnlyDecisionList<ReceivedObservationReport> _receivedReports;
    private readonly ReadOnlyDecisionList<ObservationLocationCompletionWatermark> _completionWatermarks;
    private readonly ReadOnlyDecisionList<FactionShipAssignmentSnapshot> _assets;
    private readonly ReadOnlyDecisionList<FactionKnownRouteSnapshot> _knownRoutes;

    /// <summary>Initializes an immutable faction investigation decision request.</summary>
    public FactionInvestigationDecisionInput(
        FactionId factionId,
        SimulationTime decisionTime,
        ObservationResponsePosture posture,
        IEnumerable<ReceivedObservationReport>? receivedReports,
        IEnumerable<ObservationLocationCompletionWatermark>? completionWatermarks,
        bool hasActiveInvestigation,
        IEnumerable<FactionShipAssignmentSnapshot>? assets,
        IEnumerable<FactionKnownRouteSnapshot>? knownRoutes
    )
    {
        if (factionId.Value <= 0)
        {
            throw new ArgumentException(
                "A faction decision requires an initialized faction identity.",
                nameof(factionId)
            );
        }

        if (!Enum.IsDefined(posture))
        {
            throw new ArgumentOutOfRangeException(nameof(posture), "Observation-response posture is unsupported.");
        }

        ReceivedObservationReport[] materializedReports = MaterializeReports(receivedReports);
        ObservationLocationCompletionWatermark[] materializedWatermarks = MaterializeWatermarks(completionWatermarks);
        FactionShipAssignmentSnapshot[] materializedAssets = MaterializeAssets(assets);
        FactionKnownRouteSnapshot[] materializedRoutes = MaterializeRoutes(knownRoutes);

        FactionId = factionId;
        DecisionTime = decisionTime;
        Posture = posture;
        HasActiveInvestigation = hasActiveInvestigation;
        _receivedReports = new ReadOnlyDecisionList<ReceivedObservationReport>(
            materializedReports
                .OrderByDescending(report => report.Report.ObservedAt.Milliseconds)
                .ThenBy(report => report.ReceivedAt.Milliseconds)
                .ThenBy(report => report.Report.ReportId.Value)
        );
        _completionWatermarks = new ReadOnlyDecisionList<ObservationLocationCompletionWatermark>(
            materializedWatermarks.OrderBy(watermark => watermark.LocationId.Value, StringComparer.Ordinal)
        );
        _assets = new ReadOnlyDecisionList<FactionShipAssignmentSnapshot>(
            materializedAssets.OrderBy(asset => asset.ShipId.Value)
        );
        _knownRoutes = new ReadOnlyDecisionList<FactionKnownRouteSnapshot>(
            materializedRoutes
                .OrderBy(route => route.A.Value, StringComparer.Ordinal)
                .ThenBy(route => route.B.Value, StringComparer.Ordinal)
        );
    }

    /// <summary>Gets the recipient faction that owns the decision.</summary>
    public FactionId FactionId { get; }

    /// <summary>Gets the authoritative simulation time supplied to the policy.</summary>
    public SimulationTime DecisionTime { get; }

    /// <summary>Gets whether this faction participates in direct observation response.</summary>
    public ObservationResponsePosture Posture { get; }

    /// <summary>Gets whether the faction's single investigation slot is already occupied.</summary>
    public bool HasActiveInvestigation { get; }

    /// <summary>Gets retained received reports in deterministic policy order.</summary>
    public IReadOnlyList<ReceivedObservationReport> ReceivedReports => _receivedReports;

    /// <summary>Gets sparse completion watermarks in stable location order.</summary>
    public IReadOnlyList<ObservationLocationCompletionWatermark> CompletionWatermarks => _completionWatermarks;

    /// <summary>Gets directly controlled own assets in stable identity order.</summary>
    public IReadOnlyList<FactionShipAssignmentSnapshot> Assets => _assets;

    /// <summary>Gets actor-known direct routes in stable endpoint order.</summary>
    public IReadOnlyList<FactionKnownRouteSnapshot> KnownRoutes => _knownRoutes;

    private static ReceivedObservationReport[] MaterializeReports(IEnumerable<ReceivedObservationReport>? values)
    {
        ReceivedObservationReport[] materialized = (values ?? [])
            .Take(FactionObservationState.MaximumReceivedReports + 1)
            .ToArray();
        if (
            materialized.Length > FactionObservationState.MaximumReceivedReports
            || materialized.Any(value => value is null)
        )
        {
            throw new ArgumentException(
                $"An investigation decision supports at most {FactionObservationState.MaximumReceivedReports} nonnull reports.",
                nameof(values)
            );
        }

        if (materialized.Select(value => value.Report.ReportId).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException("Investigation reports require unique identities.", nameof(values));
        }

        return materialized;
    }

    private static ObservationLocationCompletionWatermark[] MaterializeWatermarks(
        IEnumerable<ObservationLocationCompletionWatermark>? values
    )
    {
        ObservationLocationCompletionWatermark[] materialized = (values ?? [])
            .Take(FactionObservationState.MaximumCompletionWatermarks + 1)
            .ToArray();
        if (
            materialized.Length > FactionObservationState.MaximumCompletionWatermarks
            || materialized.Any(value => value is null)
        )
        {
            throw new ArgumentException(
                $"An investigation decision supports at most {FactionObservationState.MaximumCompletionWatermarks} nonnull watermarks.",
                nameof(values)
            );
        }

        if (materialized.Select(value => value.LocationId).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException("Completion watermarks require unique locations.", nameof(values));
        }

        return materialized;
    }

    private static FactionShipAssignmentSnapshot[] MaterializeAssets(IEnumerable<FactionShipAssignmentSnapshot>? values)
    {
        FactionShipAssignmentSnapshot[] materialized = (values ?? []).Take(SimulationState.MaximumShips + 1).ToArray();
        if (materialized.Length > SimulationState.MaximumShips || materialized.Any(value => value is null))
        {
            throw new ArgumentException(
                $"An investigation decision supports at most {SimulationState.MaximumShips} nonnull own assets.",
                nameof(values)
            );
        }

        if (materialized.Select(value => value.ShipId).Distinct().Count() != materialized.Length)
        {
            throw new ArgumentException("Investigation assets require unique ship identities.", nameof(values));
        }

        return materialized;
    }

    private static FactionKnownRouteSnapshot[] MaterializeRoutes(IEnumerable<FactionKnownRouteSnapshot>? values)
    {
        FactionKnownRouteSnapshot[] materialized = (values ?? []).Take(StrategicMap.MaximumRoutes + 1).ToArray();
        if (materialized.Length > StrategicMap.MaximumRoutes || materialized.Any(value => value is null))
        {
            throw new ArgumentException(
                $"An investigation decision supports at most {StrategicMap.MaximumRoutes} nonnull known routes.",
                nameof(values)
            );
        }

        if (materialized.DistinctBy(value => (value.A, value.B)).Count() != materialized.Length)
        {
            throw new ArgumentException("A known direct connection may be supplied only once.", nameof(values));
        }

        return materialized;
    }
}
