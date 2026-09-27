using System.Runtime.InteropServices;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;

namespace AlterCourse.Core.Gameplay;

/// <summary>Initial condition and allocation for one design-default installation.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct InstalledSystemStateStart(
    InstalledSystemId Id,
    SystemCondition Condition,
    PowerUnits? Allocation
);
