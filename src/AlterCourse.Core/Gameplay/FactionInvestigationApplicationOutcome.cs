namespace AlterCourse.Core.Gameplay;

internal enum FactionInvestigationApplicationOutcome
{
    Accepted = 1,
    CompletedAtCurrentLocation = 2,
    FactionMissing = 3,
    PostureDisabled = 4,
    ActiveInvestigationExists = 5,
    SourceReportUnavailable = 6,
    SourceReportIneligible = 7,
    DecisionStale = 8,
    ShipMissing = 9,
    PlayerShipRejected = 10,
    ControllerMismatch = 11,
    ShipCommitted = 12,
    ShipTraveling = 13,
    OriginMismatch = 14,
    RouteUnavailable = 15,
}
