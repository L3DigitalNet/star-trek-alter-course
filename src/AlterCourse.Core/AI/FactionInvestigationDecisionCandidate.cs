using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.AI;

/// <summary>Explains investigation eligibility and ranking facts for one directly controlled ship.</summary>
public sealed record FactionInvestigationDecisionCandidate
{
    /// <summary>Initializes complete evidence for one report-specific responder candidate.</summary>
    public FactionInvestigationDecisionCandidate(
        ObservationReportId reportId,
        ShipInstanceId shipId,
        LocationId? currentLocationId,
        FactionInvestigationCandidateReason reason,
        SimulationDuration? directRouteDuration
    )
    {
        if (reportId.Value <= 0)
        {
            throw new ArgumentException("A candidate requires an initialized report identity.", nameof(reportId));
        }

        if (shipId.Value <= 0)
        {
            throw new ArgumentException("A candidate requires an initialized ship identity.", nameof(shipId));
        }

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), "Investigation candidate reason is unsupported.");
        }

        if ((reason == FactionInvestigationCandidateReason.Eligible) != (directRouteDuration is not null))
        {
            throw new ArgumentException(
                "An eligible candidate requires a route duration, and a rejected candidate cannot have one.",
                nameof(directRouteDuration)
            );
        }

        ReportId = reportId;
        ShipId = shipId;
        CurrentLocationId = currentLocationId;
        Reason = reason;
        DirectRouteDuration = directRouteDuration;
    }

    /// <summary>Gets the report against which this candidate was evaluated.</summary>
    public ObservationReportId ReportId { get; }

    /// <summary>Gets the evaluated directly controlled ship.</summary>
    public ShipInstanceId ShipId { get; }

    /// <summary>Gets the ship's current location when one was available.</summary>
    public LocationId? CurrentLocationId { get; }

    /// <summary>Gets the first hard constraint result.</summary>
    public FactionInvestigationCandidateReason Reason { get; }

    /// <summary>Gets the direct-route duration used to rank an eligible responder.</summary>
    public SimulationDuration? DirectRouteDuration { get; }

    /// <summary>Gets whether every hard investigation constraint passed.</summary>
    public bool IsEligible => Reason == FactionInvestigationCandidateReason.Eligible;
}
