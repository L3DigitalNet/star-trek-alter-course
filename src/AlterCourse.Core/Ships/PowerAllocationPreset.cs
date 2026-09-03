namespace AlterCourse.Core.Ships;

/// <summary>Identifies a deterministic Core-owned power allocation choice.</summary>
public enum PowerAllocationPreset
{
    /// <summary>Distributes available power proportionally with stable remainder assignment.</summary>
    Balanced = 1,

    /// <summary>Satisfies sensors before impulse propulsion.</summary>
    PrioritizeSensors = 2,

    /// <summary>Satisfies impulse propulsion before sensors.</summary>
    PrioritizePropulsion = 3,
}
