using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.Gameplay;

/// <summary>Contains only own tactical, capability, and timing facts supplied to combat decisions.</summary>
internal sealed record CombatOwnFacts(
    SimulationTime Time,
    bool AtLocation,
    TacticalPosition Position,
    TacticalMotion Motion,
    SpeedKilometersPerSecond MaximumSpeed,
    InstalledSystemId? WeaponId,
    DirectedEnergyWeaponDefinition? Weapon,
    SystemCondition WeaponCondition,
    double WeaponCapability,
    SimulationTime ReadyAt
);
