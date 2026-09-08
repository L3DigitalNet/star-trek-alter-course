namespace AlterCourse.Core.AI;

/// <summary>Identifies the first hard constraint that determines one investigation candidate's eligibility.</summary>
public enum FactionInvestigationCandidateReason
{
    /// <summary>The reporting observer cannot respond to its own report.</summary>
    ReportingObserverRejected = 1,

    /// <summary>The player-controlled ship is outside autonomous faction authority.</summary>
    PlayerShipRejected = 2,

    /// <summary>An existing ordinary order already commits the ship.</summary>
    AlreadyCommitted = 3,

    /// <summary>The ship lacks a valid current strategic location.</summary>
    NotAtLocation = 4,

    /// <summary>The actor-known topology contains no legal direct route to the report location.</summary>
    DirectRouteUnavailable = 5,

    /// <summary>The ship satisfies every investigation constraint.</summary>
    Eligible = 6,
}
