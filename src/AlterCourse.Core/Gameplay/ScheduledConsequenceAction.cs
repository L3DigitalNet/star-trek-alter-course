namespace AlterCourse.Core.Gameplay;

internal enum ScheduledConsequenceAction
{
    CompleteSystemRepair = 1,
    FinishTravel = 2,
    CompleteTravelTo = 3,
    ContinuePatrol = 4,
    CompleteHold = 5,
    LoseSensorContact = 6,
    CompleteActiveSensorScan = 7,
    WakeShipContactDecision = 8,
    IgnoreInvalidatedWork = 9,
    WakeFactionDecision = 10,
    DeliverObservationReport = 11,
    IgnoreInvalidatedObservationReportDelivery = 12,
}
