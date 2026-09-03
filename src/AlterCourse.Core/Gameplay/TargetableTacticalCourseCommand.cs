using System.Runtime.InteropServices;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Quantities;

namespace AlterCourse.Core.Gameplay;

[StructLayout(LayoutKind.Auto)]
internal readonly record struct TargetableTacticalCourseCommand(
    ShipInstanceId TargetShipId,
    HeadingDegrees Heading,
    SpeedKilometersPerSecond Speed
);
