namespace AlterCourse.Core.AI;

/// <summary>Identifies one hard rule evaluated for a contact-response candidate.</summary>
public enum ShipContactDecisionConstraint
{
    /// <summary>The ship can always deliberately hold.</summary>
    HoldAvailable = 1,

    /// <summary>The ship must be in local tactical space to move.</summary>
    AtLocation = 2,

    /// <summary>Movement requires a current primary contact.</summary>
    CurrentPrimaryContact = 3,

    /// <summary>Movement requires a direction to the observed position.</summary>
    NonzeroObservedDisplacement = 4,

    /// <summary>Movement requires a positive speed no greater than the ship maximum.</summary>
    LegalMovementSpeed = 5,
}
