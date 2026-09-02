using AlterCourse.Core.Identity;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Ships;

internal sealed record PlayerShipState(
    ShipInstanceId InstanceId,
    ShipDefinitionId DefinitionId,
    TacticalPosition TacticalPosition,
    TacticalMotion TacticalMotion,
    SensorIntegrity SensorIntegrity,
    SensorRepairState? SensorRepair
);
