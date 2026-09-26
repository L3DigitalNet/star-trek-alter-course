using System.Runtime.InteropServices;
using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Ships;

/// <summary>Stores exact authoritative allocations for all four concrete power consumers.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PowerAllocation(
    PowerUnits Sensors,
    PowerUnits ImpulsePropulsion,
    PowerUnits Shields = default,
    PowerUnits DirectedEnergyWeapons = default
)
{
    /// <summary>Gets the exact sum without the individual-consumer quantity bound.</summary>
    public int Total => checked(Sensors.Value + ImpulsePropulsion.Value + Shields.Value + DirectedEnergyWeapons.Value);
}
