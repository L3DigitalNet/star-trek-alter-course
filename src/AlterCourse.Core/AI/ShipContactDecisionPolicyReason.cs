namespace AlterCourse.Core.AI;

/// <summary>Identifies the policy rule that produced a candidate score.</summary>
public enum ShipContactDecisionPolicyReason
{
    /// <summary>A hard constraint prevented the candidate from being scored.</summary>
    HardConstraintRejected = 1,

    /// <summary>No current contact exists, so holding avoids blind pursuit.</summary>
    NoCurrentContactHold = 2,

    /// <summary>An identified current contact hailed the deciding ship.</summary>
    IdentifiedHailHold = 3,

    /// <summary>An identified current contact does not require evasive movement.</summary>
    IdentifiedContactHold = 4,

    /// <summary>Holding remains a legal but weak response to an unidentified contact.</summary>
    UnidentifiedContactHoldAlternative = 5,

    /// <summary>Approach remains plausible but cautious policy ranks it below withdrawal.</summary>
    CautiousApproachAlternative = 6,

    /// <summary>Cautious policy favors separation from an unidentified contact.</summary>
    UnidentifiedContactWithdraw = 7,

    /// <summary>Approach is a low-priority alternative after identification.</summary>
    IdentifiedContactApproachAlternative = 8,

    /// <summary>Withdrawal is a low-priority alternative after identification.</summary>
    IdentifiedContactWithdrawAlternative = 9,
}
