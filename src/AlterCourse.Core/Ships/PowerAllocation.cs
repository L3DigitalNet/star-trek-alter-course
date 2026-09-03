using System.Runtime.InteropServices;
using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Ships;

/// <summary>Stores exact authoritative allocations for both concrete power consumers.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PowerAllocation(PowerUnits Sensors, PowerUnits ImpulsePropulsion);
