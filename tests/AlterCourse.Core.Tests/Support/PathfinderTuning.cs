namespace AlterCourse.Core.Tests.Support;

/// <summary>
/// Test tuning for the five pathfinder-style system definitions; defaults equal the production content.
/// </summary>
/// <remarks>
/// <see cref="Combat"/> = <see langword="false"/> omits the shield and weapon definitions from the design loadout. That
/// is the substrate expression of the pre-combat "three-consumer" fixtures, whose zero shield and weapon demand meant
/// the capability was absent.
/// </remarks>
internal sealed record PathfinderTuning(
    int Generation = 120,
    int SensorDemand = 70,
    int ImpulseDemand = 50,
    int ShieldDemand = 40,
    int WeaponDemand = 30,
    double MaximumTacticalSpeed = 10,
    double PassiveRange = 30,
    long ActiveScanMilliseconds = 2000,
    long SensorRepairMilliseconds = 8000,
    long ImpulseRepairMilliseconds = 6000,
    long ShieldRepairMilliseconds = 8000,
    long WeaponRepairMilliseconds = 6000,
    double WeaponRange = 20,
    double BaseDamage = 0.25,
    long CooldownMilliseconds = 2000,
    bool Combat = true
)
{
    /// <summary>Gets the production tuning.</summary>
    public static PathfinderTuning Production { get; } = new();
}
