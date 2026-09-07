namespace AlterCourse.Core.AI;

/// <summary>Identifies why one received report was selected or excluded from investigation.</summary>
public enum FactionInvestigationReportReason
{
    /// <summary>The faction's response posture is disabled.</summary>
    PostureDisabled = 1,

    /// <summary>An existing active investigation consumes the faction's single response slot.</summary>
    ActiveInvestigationExists = 2,

    /// <summary>The report receipt occurs after the supplied decision time.</summary>
    ReceivedAfterDecision = 3,

    /// <summary>The report claims an observation after the supplied decision time.</summary>
    FutureObservation = 4,

    /// <summary>The report is at or beyond the half-open freshness boundary.</summary>
    Expired = 5,

    /// <summary>The report has already completed its response lifecycle.</summary>
    Handled = 6,

    /// <summary>An equal-or-later investigation already completed at the report location.</summary>
    CoveredByCompletionWatermark = 7,

    /// <summary>No directly controlled candidate can currently investigate this report.</summary>
    NoEligibleResponder = 8,

    /// <summary>The report is selected for the proposed investigation.</summary>
    Selected = 9,
}
