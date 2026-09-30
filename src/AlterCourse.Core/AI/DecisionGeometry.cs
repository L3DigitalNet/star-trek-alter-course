using AlterCourse.Core.Quantities;
using AlterCourse.Core.Tactical;

namespace AlterCourse.Core.AI;

/// <summary>Preserves observed finite-coordinate directions for actor-safe movement decisions.</summary>
internal static class DecisionGeometry
{
    internal static HeadingDegrees HeadingBetween(TacticalPosition origin, TacticalPosition destination)
    {
        double deltaX = destination.XKilometers - origin.XKilometers;
        double deltaY = destination.YKilometers - origin.YKilometers;
        if (!double.IsFinite(deltaX) || !double.IsFinite(deltaY))
        {
            // Atan2 needs the component ratio, not the displacement magnitude. Scale both components only
            // on overflow: unconditional endpoint halving can erase a represented subnormal displacement.
            deltaX = Math.ScaleB(destination.XKilometers, -1) - Math.ScaleB(origin.XKilometers, -1);
            deltaY = Math.ScaleB(destination.YKilometers, -1) - Math.ScaleB(origin.YKilometers, -1);
        }

        return new HeadingDegrees(Math.Atan2(deltaX, deltaY) * 180 / Math.PI);
    }
}
