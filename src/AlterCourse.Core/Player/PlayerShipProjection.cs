
using AlterCourse.Core.Identity;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Player;

/// <summary>Projects player ship identity and gameplay-relevant runtime state.</summary>
public sealed record PlayerShipProjection
{
    internal PlayerShipProjection(
        ShipInstanceId instanceId,
        ShipDefinitionId definitionId,
        string displayName,
        TacticalProjection tactical,
        SensorProjection sensors
    ) =>
        (InstanceId, DefinitionId, DisplayName, Tactical, Sensors) =
            (instanceId, definitionId, displayName, tactical, sensors);

    /// <summary>Gets deterministic runtime identity.</summary>
    public ShipInstanceId InstanceId { get; }

    /// <summary>Gets stable definition identity.</summary>
    public ShipDefinitionId DefinitionId { get; }

    /// <summary>Gets player-facing ship name.</summary>
    public string DisplayName { get; }

    /// <summary>Gets local tactical state.</summary>
    public TacticalProjection Tactical { get; }

    /// <summary>Gets sensor state.</summary>
    public SensorProjection Sensors { get; }
}
