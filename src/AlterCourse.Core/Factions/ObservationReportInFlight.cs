using AlterCourse.Core.Identity;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Factions;

/// <summary>Correlates one report payload with its sole authoritative scheduled delivery.</summary>
internal sealed record ObservationReportInFlight
{
    internal ObservationReportInFlight(
        ObservationReportSnapshot report,
        ScheduledWorkId deliveryWorkId,
        SimulationTime dueTime
    )
    {
        ArgumentNullException.ThrowIfNull(report);
        if (deliveryWorkId.Value <= 0)
        {
            throw new ArgumentException(
                "In-flight delivery requires initialized scheduled work.",
                nameof(deliveryWorkId)
            );
        }

        if (dueTime.Milliseconds < report.ObservedAt.Milliseconds)
        {
            throw new ArgumentException("Report delivery cannot precede its source observation.", nameof(dueTime));
        }

        Report = report;
        DeliveryWorkId = deliveryWorkId;
        DueTime = dueTime;
    }

    internal ObservationReportSnapshot Report { get; }
    internal ScheduledWorkId DeliveryWorkId { get; }
    internal SimulationTime DueTime { get; }
}
