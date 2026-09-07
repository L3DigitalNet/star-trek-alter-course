using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Factions;

/// <summary>Stores one bounded faction-held report with its actual receipt and handling state.</summary>
public sealed record ReceivedObservationReport
{
    /// <summary>Initializes a received historical report.</summary>
    public ReceivedObservationReport(
        ObservationReportSnapshot report,
        SimulationTime receivedAt,
        ObservationReportHandling handling = ObservationReportHandling.Unhandled
    )
    {
        ArgumentNullException.ThrowIfNull(report);
        if (receivedAt.Milliseconds < report.ObservedAt.Milliseconds)
        {
            throw new ArgumentException("A report cannot be received before it was observed.", nameof(receivedAt));
        }

        if (!Enum.IsDefined(handling))
        {
            throw new ArgumentOutOfRangeException(nameof(handling), "Observation report handling is unsupported.");
        }

        Report = report;
        ReceivedAt = receivedAt;
        Handling = handling;
    }

    /// <summary>Gets the immutable historical source snapshot.</summary>
    public ObservationReportSnapshot Report { get; }

    /// <summary>Gets the actual simulation time at which the recipient accepted delivery.</summary>
    public SimulationTime ReceivedAt { get; }

    /// <summary>Gets whether the report remains available for response.</summary>
    public ObservationReportHandling Handling { get; }
}
