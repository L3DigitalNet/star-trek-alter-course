using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Gameplay;

/// <summary>Requests a shot at a concrete system of one observer-local contact.</summary>
public readonly record struct FireDirectedEnergyIntent(SensorContactId ContactId, ShipSystemId TargetSystem);
