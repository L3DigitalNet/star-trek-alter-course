namespace AlterCourse.Core.Player;

/// <summary>Identifies one stable player Engineering action.</summary>
public enum EngineeringAction
{
    /// <summary>Applies the balanced allocation preset.</summary>
    Balanced = 1,

    /// <summary>Applies the sensor-priority allocation preset.</summary>
    PrioritizeSensors = 2,

    /// <summary>Applies the propulsion-priority allocation preset.</summary>
    PrioritizePropulsion = 3,

    /// <summary>Begins a complete sensor repair.</summary>
    BeginSensorRepair = 4,

    /// <summary>Begins a complete impulse-propulsion repair.</summary>
    BeginImpulseRepair = 5,

    /// <summary>Returns presentation focus to command.</summary>
    ReturnToCommand = 6,

    /// <summary>Applies the shield-priority allocation preset.</summary>
    PrioritizeShields = 7,

    /// <summary>Applies the weapon-priority allocation preset.</summary>
    PrioritizeDirectedEnergyWeapons = 8,

    /// <summary>Begins a complete shield repair.</summary>
    BeginShieldRepair = 9,

    /// <summary>Begins a complete weapon repair.</summary>
    BeginDirectedEnergyRepair = 10,
}
