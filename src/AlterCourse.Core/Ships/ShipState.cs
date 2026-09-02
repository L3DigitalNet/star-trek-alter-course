using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Orders;
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
        SensorIntegrity sensorIntegrity,
        SensorRepairState? sensorRepair,
        ShipStrategicState strategicState,
        ShipOrder? activeOrder = null
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

        ArgumentNullException.ThrowIfNull(strategicState);

        InstanceId = instanceId;
        DefinitionId = definitionId;
        VesselDisplayName = vesselDisplayName;
        TacticalPosition = tacticalPosition;
        TacticalMotion = tacticalMotion;
        SensorIntegrity = sensorIntegrity;
        SensorRepair = sensorRepair;
        StrategicState = strategicState;
        ActiveOrder = activeOrder;
    }

    internal ShipInstanceId InstanceId { get; }
    internal ShipDefinitionId DefinitionId { get; }
    internal string VesselDisplayName { get; }
    internal TacticalPosition TacticalPosition { get; init; }
    internal TacticalMotion TacticalMotion { get; init; }
    internal SensorIntegrity SensorIntegrity { get; init; }
    internal SensorRepairState? SensorRepair { get; init; }
    internal ShipStrategicState StrategicState { get; init; }
    internal ShipOrder? ActiveOrder { get; init; }
}
