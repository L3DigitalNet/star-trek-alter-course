using AlterCourse.Core.Identity;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Gameplay;

/// <summary>
/// Declares one ship's initial state. Installed systems come from <see cref="Systems"/>, which distinguishes an
/// omitted loadout (use the design default) from an explicit one (including explicitly empty).
/// </summary>
public sealed record ShipStart(
    ShipInstanceId InstanceId,
    ShipDefinitionId DefinitionId,
    string VesselDisplayName,
    TacticalPosition TacticalPosition,
    TacticalMotion TacticalMotion,
    ShipStrategicStart Strategic,
    ShipSystemsStart Systems,
    SystemRepairStart? SystemRepair = null,
    ShipOrderStart? ActiveOrder = null,
    FactionId? DirectControllerFactionId = null
);
