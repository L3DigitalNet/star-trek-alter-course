using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Player;

/// <summary>Projects immutable player-owned Engineering state and derived capability.</summary>
public sealed record EngineeringProjection(
    PowerUnits NominalGeneration,
    PowerUnits AvailablePower,
    PowerUnits SensorAllocation,
    PowerUnits ImpulseAllocation,
    PowerUnits Reserve,
    SystemCondition GenerationCondition,
    SystemCondition SensorCondition,
    SystemCondition ImpulseCondition,
    double SensorCapability,
    double ImpulseCapability,
    DistanceKilometers EffectivePassiveSensorRange,
    SpeedKilometersPerSecond EffectiveMaximumTacticalSpeed,
    SystemRepairProjection? ActiveRepair,
    IReadOnlyList<EngineeringActionProjection> Actions
);
