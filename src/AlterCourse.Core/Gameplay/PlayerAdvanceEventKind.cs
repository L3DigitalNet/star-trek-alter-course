namespace AlterCourse.Core.Gameplay;

/// <summary>Identifies a player-visible consequence without exposing simulation truth identities.</summary>
public enum PlayerAdvanceEventKind
{
    /// <summary>The player ship completed strategic travel.</summary>
    TravelArrived = 1,

    /// <summary>The player ship completed one system repair.</summary>
    SystemRepairCompleted = 2,

    /// <summary>A new observer-local contact was admitted.</summary>
    SensorContactDetected = 3,

    /// <summary>A current observer-local contact became temporarily unobserved.</summary>
    SensorContactStale = 4,

    /// <summary>A stale or lost observer-local contact became current again.</summary>
    SensorContactReacquired = 5,

    /// <summary>A stale observer-local contact reached its exact loss boundary.</summary>
    SensorContactLost = 6,

    /// <summary>An active scan identified its observer-local contact.</summary>
    ActiveSensorScanCompleted = 7,

    /// <summary>An active scan was interrupted because its contact ceased to be current.</summary>
    ActiveSensorScanInterrupted = 8,

    /// <summary>The player fired an accepted directed-energy shot.</summary>
    DirectedEnergyFired = 9,

    /// <summary>An observed shot impacted shields.</summary>
    ShieldImpact = 10,

    /// <summary>An observed shot penetrated to its selected system.</summary>
    SubsystemPenetration = 11,

    /// <summary>The player suffered positive system damage.</summary>
    OwnSystemDamaged = 12,

    /// <summary>Damage interrupted the player exactly correlated repair.</summary>
    SystemRepairInterrupted = 13,

    /// <summary>Reduced generation forced the player allocation down.</summary>
    PowerBrownout = 14,

    /// <summary>Reduced capability forced slower motion while preserving heading.</summary>
    ForcedDeceleration = 15,
}
