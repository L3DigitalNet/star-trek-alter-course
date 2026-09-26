using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Gameplay;

// TEMPORARY BRIDGE (removed by leg L5): kind-addressed entry points that let the unchanged Godot Engineering
// handlers and the legacy EngineeringProjection action enum drive the generic installed-system commands. Each maps
// a kind to the ship's sole installation of that kind through ShipSystemAdmission.SupportedSingle and then calls the
// generic command, so no rule lives here. L5 replaces the Godot handlers with installed-id actions and deletes this
// file together with PowerAllocationPreset.
public sealed partial class GameSimulation
{
    /// <summary>Temporary L5 bridge: applies a legacy preset through the generic Balanced or priority command.</summary>
    public PowerAllocationResult ApplyPowerAllocationPreset(PowerAllocationPreset preset)
    {
        if (preset == PowerAllocationPreset.Balanced)
        {
            return ApplyBalancedAllocation();
        }

        InstalledSystem? consumer = ShipSystemAdmission.SupportedSingle(
            _state.GetRequiredShip(_state.PlayerShipId).Engineering.Systems,
            PresetKind(preset)
        );
        return consumer is null
            ? new PowerAllocationResult(
                PowerAllocationOutcome.UnknownConsumer,
                new Player.ReadOnlyValueList<PlayerAdvanceEvent>([])
            )
            : ApplyPriorityAllocation(consumer.Id);
    }

    /// <summary>Temporary L5 bridge: repairs the player's sole installation of a kind.</summary>
    public SystemRepairResult BeginSystemRepair(ShipSystemKind targetKind, SystemCondition targetCondition)
    {
        InstalledSystem? target = ShipSystemAdmission.SupportedSingle(
            _state.GetRequiredShip(_state.PlayerShipId).Engineering.Systems,
            targetKind
        );
        return target is null
            ? new SystemRepairResult(SystemRepairOutcome.UnknownSystem)
            : BeginSystemRepair(target.Id, targetCondition);
    }

    internal static ShipSystemKind PresetKind(PowerAllocationPreset preset) =>
        preset switch
        {
            PowerAllocationPreset.PrioritizeSensors => ShipSystemKind.Sensors,
            PowerAllocationPreset.PrioritizePropulsion => ShipSystemKind.ImpulsePropulsion,
            PowerAllocationPreset.PrioritizeShields => ShipSystemKind.Shields,
            PowerAllocationPreset.PrioritizeDirectedEnergyWeapons => ShipSystemKind.DirectedEnergyWeapons,
            _ => throw new ArgumentOutOfRangeException(nameof(preset), preset, "Power allocation preset is unknown."),
        };
}
