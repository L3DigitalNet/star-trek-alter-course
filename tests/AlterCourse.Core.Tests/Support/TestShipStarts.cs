using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;

namespace AlterCourse.Core.Tests.Support;

/// <summary>Builds design-default <see cref="ShipSystemsStart"/> declarations for the pathfinder-style loadout.</summary>
internal static class TestShipStarts
{
    /// <summary>
    /// Declares state for the five default installations. Shield and weapon condition and power default to 0 —
    /// installed but offline — which is what a pre-substrate <c>ShipStart</c> produced when it did not set them.
    /// </summary>
    internal static ShipSystemsStart Pathfinder(
        double generation = 1,
        double sensors = 1,
        double impulse = 1,
        int sensorPower = 70,
        int impulsePower = 50,
        int shieldPower = 0,
        int weaponPower = 0,
        double shields = 0,
        double weapons = 0
    ) =>
        ShipSystemsStart.FromDesignDefaults([
            new(TestShipContent.Generator, new SystemCondition(generation), null),
            new(TestShipContent.Sensors, new SystemCondition(sensors), new PowerUnits(sensorPower)),
            new(TestShipContent.Impulse, new SystemCondition(impulse), new PowerUnits(impulsePower)),
            new(TestShipContent.Shields, new SystemCondition(shields), new PowerUnits(shieldPower)),
            new(TestShipContent.Weapons, new SystemCondition(weapons), new PowerUnits(weaponPower)),
        ]);

    /// <summary>Declares state for a three-installation loadout (generation, sensors, impulse; no combat systems).</summary>
    internal static ShipSystemsStart WithoutCombat(
        double generation = 1,
        double sensors = 1,
        double impulse = 1,
        int sensorPower = 70,
        int impulsePower = 50
    ) =>
        ShipSystemsStart.FromDesignDefaults([
            new(TestShipContent.Generator, new SystemCondition(generation), null),
            new(TestShipContent.Sensors, new SystemCondition(sensors), new PowerUnits(sensorPower)),
            new(TestShipContent.Impulse, new SystemCondition(impulse), new PowerUnits(impulsePower)),
        ]);

    /// <summary>Declares a repair of one of the default installations.</summary>
    internal static SystemRepairStart Repair(InstalledSystemId target, double starting, double goal, long startedAt) =>
        new(target, new SystemCondition(starting), new SystemCondition(goal), new SimulationTime(startedAt));
}
