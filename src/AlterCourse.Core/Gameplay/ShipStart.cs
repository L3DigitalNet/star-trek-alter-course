using AlterCourse.Core.Identity;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Gameplay;

/// <summary>Declares stable ship identity and domain state without runtime scheduler details.</summary>
public sealed record ShipStart(
    ShipInstanceId InstanceId,
    ShipDefinitionId DefinitionId,
    string VesselDisplayName,
    TacticalPosition TacticalPosition,
    TacticalMotion TacticalMotion,
    SensorIntegrity SensorIntegrity,
    ShipStrategicStart Strategic,
    SensorRepairStart? SensorRepair = null,
    ShipOrderStart? ActiveOrder = null
);
