namespace AlterCourse.Core.Ships;

// TEMPORARY BRIDGE (removed by leg L5): per-kind presets survive only so the unchanged Godot Engineering handlers
// keep compiling. Core power commands are generic (Balance, Prioritize installed consumer); the adapter in
// GameSimulation.EngineeringAdapter.cs maps each preset onto them. Nothing in Core mechanics reads this enum.

/// <summary>Identifies a deterministic Core-owned power allocation choice.</summary>
public enum PowerAllocationPreset
{
    /// <summary>Distributes available power proportionally with stable remainder assignment.</summary>
    Balanced = 1,

    /// <summary>Satisfies sensors before impulse propulsion.</summary>
    PrioritizeSensors = 2,

    /// <summary>Satisfies impulse propulsion before sensors.</summary>
    PrioritizePropulsion = 3,

    /// <summary>Satisfies shields before remaining consumers in semantic order.</summary>
    PrioritizeShields = 4,

    /// <summary>Satisfies directed-energy weapons before remaining consumers in semantic order.</summary>
    PrioritizeDirectedEnergyWeapons = 5,
}
