namespace AlterCourse.Core.AI;

/// <summary>Identifies the stable report and responder ordering used by investigation policy.</summary>
public enum FactionInvestigationDecisionTieRule
{
    /// <summary>Prefer newest reports, then earliest receipt and ID; prefer shortest routes, then ship ID.</summary>
    ReportRecencyThenResponderRoute = 1,
}
