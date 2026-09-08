namespace AlterCourse.Core.AI;

/// <summary>Describes the complete result of one faction investigation evaluation.</summary>
public enum FactionInvestigationDecisionOutcome
{
    /// <summary>An eligible responder was proposed for ordinary investigation travel.</summary>
    InvestigationProposed = 1,

    /// <summary>The response posture is disabled.</summary>
    PostureDisabled = 2,

    /// <summary>An existing investigation prevents another assignment.</summary>
    ActiveInvestigationExists = 3,

    /// <summary>No retained report is currently actionable.</summary>
    NoActionableReport = 4,

    /// <summary>Actionable reports exist, but none has an eligible responder.</summary>
    NoEligibleResponder = 5,
}
