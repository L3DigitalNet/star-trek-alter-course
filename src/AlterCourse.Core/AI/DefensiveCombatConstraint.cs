namespace AlterCourse.Core.AI;

/// <summary>Records one deterministic actor-safe defensive decision fact.</summary>
internal enum DefensiveCombatConstraint
{
    LegitimateStimulus,
    CurrentContact,
    IdentifiedContact,
    LocalContext,
    WeaponPresent,
    WeaponOperational,
    InRange,
    FutureBoundaryAvailable,
    WeaponPowered,
    WeaponReady,
    FireLegal,
    KnownDisplacement,
    PropulsionAvailable,
}
