namespace AlterCourse.Core.AI;

/// <summary>Identifies the first hard constraint that determines one assignment candidate's eligibility.</summary>
public enum FactionAssignmentCandidateReason
{
    /// <summary>The player-controlled ship is outside autonomous assignment authority.</summary>
    PlayerShipRejected = 1,

    /// <summary>An existing order already commits the ship.</summary>
    AlreadyCommitted = 2,

    /// <summary>The ship is not currently at a strategic location.</summary>
    NotAtLocation = 3,

    /// <summary>The actor-known topology contains no direct route to the objective.</summary>
    DirectRouteUnavailable = 4,

    /// <summary>The ship satisfies every assignment constraint.</summary>
    Eligible = 5,
}
