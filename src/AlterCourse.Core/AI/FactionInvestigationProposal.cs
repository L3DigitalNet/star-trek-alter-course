using AlterCourse.Core.Factions;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.AI;

/// <summary>Requests validated ordinary travel to investigate one exact historical report.</summary>
public sealed record FactionInvestigationProposal
{
    /// <summary>Initializes a proposal with every actor-safe fact required for application revalidation.</summary>
    public FactionInvestigationProposal(
        FactionId factionId,
        ObservationReportSnapshot sourceReport,
        SimulationTime receivedAt,
        ShipInstanceId responderShipId,
        LocationId originLocationId,
        LocationId destinationLocationId,
        SimulationTime decisionTime
    )
    {
        if (factionId.Value <= 0)
        {
            throw new ArgumentException(
                "An investigation proposal requires an initialized faction.",
                nameof(factionId)
            );
        }

        ArgumentNullException.ThrowIfNull(sourceReport);
        if (
            receivedAt.Milliseconds < sourceReport.ObservedAt.Milliseconds
            || decisionTime.Milliseconds < receivedAt.Milliseconds
        )
        {
            throw new ArgumentException(
                "Proposal times must preserve observation, receipt, and decision order.",
                nameof(receivedAt)
            );
        }

        if (responderShipId.Value <= 0 || responderShipId == sourceReport.ObserverShipId)
        {
            throw new ArgumentException(
                "A proposal requires a distinct initialized responder.",
                nameof(responderShipId)
            );
        }

        if (string.IsNullOrWhiteSpace(originLocationId.Value))
        {
            throw new ArgumentException("A proposal requires an initialized current origin.", nameof(originLocationId));
        }

        if (destinationLocationId != sourceReport.ObservedAtLocationId)
        {
            throw new ArgumentException(
                "A proposal destination must equal the source report's historical location.",
                nameof(destinationLocationId)
            );
        }

        FactionId = factionId;
        SourceReport = sourceReport;
        ReceivedAt = receivedAt;
        ResponderShipId = responderShipId;
        OriginLocationId = originLocationId;
        DestinationLocationId = destinationLocationId;
        DecisionTime = decisionTime;
    }

    /// <summary>Gets the recipient faction requesting application.</summary>
    public FactionId FactionId { get; }

    /// <summary>Gets the exact historical report snapshot selected by the policy.</summary>
    public ObservationReportSnapshot SourceReport { get; }

    /// <summary>Gets the selected report's actual receipt time.</summary>
    public SimulationTime ReceivedAt { get; }

    /// <summary>Gets the selected directly controlled responder.</summary>
    public ShipInstanceId ResponderShipId { get; }

    /// <summary>Gets the responder's current origin at evaluation time.</summary>
    public LocationId OriginLocationId { get; }

    /// <summary>Gets the fixed historical report location.</summary>
    public LocationId DestinationLocationId { get; }

    /// <summary>Gets the authoritative evaluation time.</summary>
    public SimulationTime DecisionTime { get; }
}
