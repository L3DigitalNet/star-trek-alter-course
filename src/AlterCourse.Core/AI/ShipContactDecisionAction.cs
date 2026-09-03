namespace AlterCourse.Core.AI;

/// <summary>Identifies one stable cautious-contact candidate action.</summary>
public enum ShipContactDecisionAction
{
    /// <summary>Stops local tactical motion.</summary>
    Hold = 1,

    /// <summary>Moves toward the primary contact's observed position.</summary>
    Approach = 2,

    /// <summary>Moves away from the primary contact's observed position.</summary>
    Withdraw = 3,
}
