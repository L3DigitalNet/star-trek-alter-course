using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Ships;

internal sealed record ShipState
{
    internal ShipState(
        ShipInstanceId instanceId,
        ShipDefinitionId definitionId,
        string vesselDisplayName,
        TacticalPosition tacticalPosition,
        TacticalMotion tacticalMotion,
        SensorIntegrity sensorIntegrity,
        SensorRepairState? sensorRepair,
        ShipStrategicState strategicState
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
        ArgumentNullException.ThrowIfNull(strategicState);

        InstanceId = instanceId;
        DefinitionId = definitionId;
        VesselDisplayName = vesselDisplayName;
        TacticalPosition = tacticalPosition;
        TacticalMotion = tacticalMotion;
        SensorIntegrity = sensorIntegrity;
        SensorRepair = sensorRepair;
        StrategicState = strategicState;
    }

    internal ShipInstanceId InstanceId { get; }
    internal ShipDefinitionId DefinitionId { get; }
    internal string VesselDisplayName { get; }
    internal TacticalPosition TacticalPosition { get; init; }
    internal TacticalMotion TacticalMotion { get; init; }
    internal SensorIntegrity SensorIntegrity { get; init; }
    internal SensorRepairState? SensorRepair { get; init; }
    internal ShipStrategicState StrategicState { get; init; }
}
