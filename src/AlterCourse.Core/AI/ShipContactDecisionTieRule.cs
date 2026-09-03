namespace AlterCourse.Core.AI;

/// <summary>Identifies the deterministic order used when candidate scores tie.</summary>
public enum ShipContactDecisionTieRule
{
    /// <summary>Prefers Hold, then Approach, then Withdraw.</summary>
    HoldApproachWithdraw = 1,
}
