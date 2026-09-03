using AlterCourse.Core.AI;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Ships;

internal sealed record ShipState
{
    internal const int MaximumVesselDisplayNameLength = 64;

    internal ShipState(
        ShipInstanceId instanceId,
        ShipDefinitionId definitionId,
        string vesselDisplayName,
        TacticalPosition tacticalPosition,
        TacticalMotion tacticalMotion,
        ShipEngineeringState engineering,
        ShipStrategicState strategicState,
        ShipOrder? activeOrder = null,
        SensorKnowledge? sensorKnowledge = null,
        ShipAutonomousState? autonomousState = null
    )
    {
        if (instanceId.Value <= 0)
        {
            throw new ArgumentException("Ship state requires an initialized instance identity.", nameof(instanceId));
        }

        if (string.IsNullOrWhiteSpace(definitionId.Value))
        {
            throw new ArgumentException(
                "Ship state requires an initialized definition identity.",
                nameof(definitionId)
            );
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(vesselDisplayName);
        if (vesselDisplayName.Length > MaximumVesselDisplayNameLength)
        {
            throw new ArgumentException(
                $"Vessel display name cannot exceed {MaximumVesselDisplayNameLength} characters.",
                nameof(vesselDisplayName)
            );
        }

        ArgumentNullException.ThrowIfNull(engineering);
        ArgumentNullException.ThrowIfNull(strategicState);

        InstanceId = instanceId;
        DefinitionId = definitionId;
        VesselDisplayName = vesselDisplayName;
        TacticalPosition = tacticalPosition;
        TacticalMotion = tacticalMotion;
        Engineering = engineering;
        StrategicState = strategicState;
        ActiveOrder = activeOrder;
        SensorKnowledge = sensorKnowledge ?? SensorKnowledge.Empty;
        AutonomousState = autonomousState ?? ShipAutonomousState.Empty;
    }

    // Save V4 restoration remains a sensor-shaped translation until the adjacent V5 persistence leg lands.
    internal ShipState(
        ShipInstanceId instanceId,
        ShipDefinitionId definitionId,
        string vesselDisplayName,
        TacticalPosition tacticalPosition,
        TacticalMotion tacticalMotion,
        SensorIntegrity sensorIntegrity,
        SensorRepairState? sensorRepair,
        ShipStrategicState strategicState,
        ShipOrder? activeOrder = null,
        SensorKnowledge? sensorKnowledge = null,
        ShipAutonomousState? autonomousState = null
    )
        : this(
            instanceId,
            definitionId,
            vesselDisplayName,
            tacticalPosition,
            tacticalMotion,
            new ShipEngineeringState(
                new SystemCondition(1),
                new SystemCondition(sensorIntegrity.Value),
                new SystemCondition(1),
                new PowerAllocation(new Quantities.PowerUnits(70), new Quantities.PowerUnits(50)),
                sensorRepair is null
                    ? null
                    : new SystemRepairState(
                        ShipSystemId.Sensors,
                        new SystemCondition(sensorRepair.StartingIntegrity.Value),
                        new SystemCondition(sensorRepair.TargetIntegrity.Value),
                        sensorRepair.StartedAt,
                        sensorRepair.ExpectedCompletion,
                        sensorRepair.ScheduledCompletionId
                    )
            ),
            strategicState,
            activeOrder,
            sensorKnowledge,
            autonomousState
        ) { }

    internal ShipInstanceId InstanceId { get; }
    internal ShipDefinitionId DefinitionId { get; }
    internal string VesselDisplayName { get; }
    internal TacticalPosition TacticalPosition { get; init; }
    internal TacticalMotion TacticalMotion { get; init; }
    internal ShipEngineeringState Engineering { get; init; }
    internal SensorIntegrity SensorIntegrity => new(Engineering.SensorCondition.Value);
    internal SensorRepairState? SensorRepair =>
        Engineering.ActiveRepair is not { TargetSystem: var target } repair || target != ShipSystemId.Sensors
            ? null
            : new SensorRepairState(
                new SensorIntegrity(repair.StartingCondition.Value),
                new SensorIntegrity(repair.TargetCondition.Value),
                repair.StartedAt,
                repair.ExpectedCompletion,
                repair.ScheduledCompletionId
            );
    internal ShipStrategicState StrategicState { get; init; }
    internal ShipOrder? ActiveOrder { get; init; }
    internal SensorKnowledge SensorKnowledge { get; init; }
    internal ShipAutonomousState AutonomousState { get; init; }
}
