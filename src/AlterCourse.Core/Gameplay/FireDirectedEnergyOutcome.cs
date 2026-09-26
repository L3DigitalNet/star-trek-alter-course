namespace AlterCourse.Core.Gameplay;

/// <summary>Identifies actor-safe fire legality without target runtime identities or health.</summary>
public enum FireDirectedEnergyOutcome
{
    /// <summary>The shot committed atomically.</summary>
    Accepted = 1,

    /// <summary>The local contact is absent.</summary>
    ContactNotFound,

    /// <summary>The local contact is stale or lost.</summary>
    ContactNotCurrent,

    /// <summary>Identity has not been learned.</summary>
    ContactNotIdentified,

    /// <summary>The actor lacks a shared local strategic context.</summary>
    NotAtSameLocation,

    /// <summary>Observed separation exceeds authored inclusive range.</summary>
    OutOfRange,

    /// <summary>The weapon has no effective power.</summary>
    WeaponUnpowered,

    /// <summary>The weapon is absent or has zero condition.</summary>
    WeaponOffline,

    /// <summary>Own weapon readiness has not expired.</summary>
    CooldownActive,

    /// <summary>The requested subsystem identity is unsupported.</summary>
    UnsupportedSystem,

    /// <summary>A required future boundary cannot be represented.</summary>
    TimeLimitExceeded,
}
