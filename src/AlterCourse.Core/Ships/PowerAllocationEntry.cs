using System.Runtime.InteropServices;
using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Ships;

/// <summary>One installed consumer's exact allocation inside a complete <see cref="PowerAllocation"/>.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct PowerAllocationEntry(InstalledSystemId Consumer, PowerUnits Allocation);
