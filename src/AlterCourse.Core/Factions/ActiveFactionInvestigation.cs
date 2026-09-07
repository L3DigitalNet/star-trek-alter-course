using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Core.Factions;

/// <summary>Retains the complete fixed correlation for one committed faction investigation.</summary>
internal sealed record ActiveFactionInvestigation
{
    internal ActiveFactionInvestigation(
        ObservationReportSnapshot sourceReport,
        ShipInstanceId responderShipId,
        LocationId originLocationId,
        LocationId destinationLocationId,
        SimulationTime sourceReceivedAt,
        SimulationTime assignedAt,
        ShipOrderId orderId
    )
    {
        ArgumentNullException.ThrowIfNull(sourceReport);
        if (responderShipId.Value <= 0)
        {
            throw new ArgumentException("An investigation requires an initialized responder.", nameof(responderShipId));
        }

        if (responderShipId == sourceReport.ObserverShipId)
        {
            throw new ArgumentException(
                "The reporting observer cannot investigate its own report.",
                nameof(responderShipId)
            );
        }

        if (string.IsNullOrWhiteSpace(originLocationId.Value))
        {
            throw new ArgumentException("An investigation requires an initialized origin.", nameof(originLocationId));
        }

        if (destinationLocationId != sourceReport.ObservedAtLocationId)
        {
            throw new ArgumentException(
                "An investigation destination must remain the source report's historical location.",
                nameof(destinationLocationId)
            );
        }

        if (
            sourceReceivedAt.Milliseconds < sourceReport.ObservedAt.Milliseconds
            || assignedAt.Milliseconds < sourceReceivedAt.Milliseconds
        )
        {
            throw new ArgumentException(
                "Investigation times must preserve source observation, receipt, and assignment order.",
                nameof(assignedAt)
            );
        }

        if (orderId.Value <= 0)
        {
            throw new ArgumentException("An investigation requires an initialized ordinary order.", nameof(orderId));
        }

        SourceReport = sourceReport;
        ResponderShipId = responderShipId;
        OriginLocationId = originLocationId;
        DestinationLocationId = destinationLocationId;
        SourceReceivedAt = sourceReceivedAt;
        AssignedAt = assignedAt;
        OrderId = orderId;
    }

    // The full actor-safe source snapshot is retained because received-report eviction must not destroy the
    // provenance needed to validate a committed investigation after save/load.
    internal ObservationReportSnapshot SourceReport { get; }
    internal ShipInstanceId ResponderShipId { get; }
    internal LocationId OriginLocationId { get; }
    internal LocationId DestinationLocationId { get; }
    internal SimulationTime SourceReceivedAt { get; }
    internal SimulationTime AssignedAt { get; }
    internal ShipOrderId OrderId { get; }
}
