namespace AlterCourse.Core.Gameplay;

internal enum ScheduledConsequenceRule
{
    SystemRepairCompletion = 1,
    OrderlessTravelArrival = 2,
    TravelToArrival = 3,
    PatrolWaypointArrival = 4,
    HoldUntilWake = 5,
    SensorContactLoss = 6,
    ActiveSensorScanCompletion = 7,
    ShipContactDecisionWake = 8,
}
