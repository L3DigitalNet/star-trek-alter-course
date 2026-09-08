using AlterCourse.Core.Factions;

namespace AlterCourse.Core.AI;

/// <summary>Explains why one received report was selected or excluded.</summary>
public sealed record FactionInvestigationReportEvaluation
{
    /// <summary>Initializes one report-specific policy result.</summary>
    public FactionInvestigationReportEvaluation(
        ReceivedObservationReport receivedReport,
        FactionInvestigationReportReason reason
    )
    {
        ArgumentNullException.ThrowIfNull(receivedReport);
        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason), "Investigation report reason is unsupported.");
        }

        ReceivedReport = receivedReport;
        Reason = reason;
    }

    /// <summary>Gets the immutable report and receipt facts evaluated by the policy.</summary>
    public ReceivedObservationReport ReceivedReport { get; }

    /// <summary>Gets the report's selection or exclusion reason.</summary>
    public FactionInvestigationReportReason Reason { get; }
}
