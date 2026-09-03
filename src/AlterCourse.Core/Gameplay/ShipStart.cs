using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Gameplay;

/// <summary>Declares stable ship identity and domain state without runtime scheduler details.</summary>
public sealed record ShipStart
{
    /// <summary>Initializes a ship start from one complete engineering declaration.</summary>
    public ShipStart(
        ShipInstanceId instanceId,
        ShipDefinitionId definitionId,
        string vesselDisplayName,
        TacticalPosition tacticalPosition,
        TacticalMotion tacticalMotion,
        SystemCondition generationCondition,
        SystemCondition sensorCondition,
        SystemCondition impulseCondition,
        PowerAllocation allocation,
        ShipStrategicStart strategic,
        SystemRepairStart? systemRepair = null,
        ShipOrderStart? activeOrder = null
    ) =>
        (
            InstanceId,
            DefinitionId,
            VesselDisplayName,
            TacticalPosition,
            TacticalMotion,
            GenerationCondition,
            SensorCondition,
            ImpulseCondition,
            Allocation,
            Strategic,
            SystemRepair,
            this.ActiveOrder
        ) = (
            instanceId,
            definitionId,
            vesselDisplayName,
            tacticalPosition,
            tacticalMotion,
            generationCondition,
            sensorCondition,
            impulseCondition,
            allocation,
            strategic,
            systemRepair,
            activeOrder
        );

    internal ShipStart(
        ShipInstanceId instanceId,
        ShipDefinitionId definitionId,
        string vesselDisplayName,
        TacticalPosition tacticalPosition,
        TacticalMotion tacticalMotion,
        SensorIntegrity sensorIntegrity,
        ShipStrategicStart strategic,
        SensorRepairStart? sensorRepair = null,
        ShipOrderStart? ActiveOrder = null
    )
        : this(
            instanceId,
            definitionId,
            vesselDisplayName,
            tacticalPosition,
            tacticalMotion,
            new SystemCondition(1),
            new SystemCondition(sensorIntegrity.Value),
            new SystemCondition(1),
            new PowerAllocation(new PowerUnits(70), new PowerUnits(50)),
            strategic,
            sensorRepair is null
                ? null
                : new SystemRepairStart(
                    ShipSystemId.Sensors,
                    new SystemCondition(sensorRepair.StartingIntegrity.Value),
                    new SystemCondition(sensorRepair.TargetIntegrity.Value),
                    sensorRepair.StartedAt
                ),
            ActiveOrder
        ) { }

    /// <summary>Gets deterministic runtime identity.</summary>
    public ShipInstanceId InstanceId { get; init; }

    /// <summary>Gets immutable definition identity.</summary>
    public ShipDefinitionId DefinitionId { get; init; }

    /// <summary>Gets vessel display name.</summary>
    public string VesselDisplayName { get; init; }

    /// <summary>Gets initial local position.</summary>
    public TacticalPosition TacticalPosition { get; init; }

    /// <summary>Gets initial local motion.</summary>
    public TacticalMotion TacticalMotion { get; init; }

    /// <summary>Gets initial generation condition.</summary>
    public SystemCondition GenerationCondition { get; init; }

    /// <summary>Gets initial sensor condition.</summary>
    public SystemCondition SensorCondition { get; init; }

    /// <summary>Gets initial impulse condition.</summary>
    public SystemCondition ImpulseCondition { get; init; }

    /// <summary>Gets exact initial allocation.</summary>
    public PowerAllocation Allocation { get; init; }

    /// <summary>Gets initial strategic state.</summary>
    public ShipStrategicStart Strategic { get; init; }

    /// <summary>Gets the optional active system repair.</summary>
    public SystemRepairStart? SystemRepair { get; init; }

    /// <summary>Gets the optional autonomous order.</summary>
    public ShipOrderStart? ActiveOrder { get; init; }
}
