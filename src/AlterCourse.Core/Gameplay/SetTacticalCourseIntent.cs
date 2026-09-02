using System.Runtime.InteropServices;
using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Gameplay;

/// <summary>Requests a local tactical heading and speed.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct SetTacticalCourseIntent(HeadingDegrees Heading, SpeedKilometersPerSecond Speed);
