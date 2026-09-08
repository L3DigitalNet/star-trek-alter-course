namespace AlterCourse.Core.Gameplay;

/// <summary>Describes whether an evaluated faction proposal passed authoritative application checks.</summary>
internal enum FactionAssignmentApplicationOutcome
{
    Accepted = 1,
    FactionMissing = 2,
    ObjectiveMismatch = 3,
    ShipMissing = 4,
    PlayerShipRejected = 5,
    ControllerMismatch = 6,
    ShipCommitted = 7,
    ShipTraveling = 8,
    RouteUnavailable = 9,
    DecisionWakePending = 10,
}
