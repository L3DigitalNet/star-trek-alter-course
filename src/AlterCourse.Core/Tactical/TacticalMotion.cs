
using System.Runtime.InteropServices;
using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Tactical;

/// <summary>Defines authoritative continuous tactical heading and speed.</summary>
[StructLayout(LayoutKind.Auto)]
public readonly record struct TacticalMotion(HeadingDegrees Heading, SpeedKilometersPerSecond Speed);
