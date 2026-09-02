using Godot;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Maps east/north Core kilometers into right/down Godot screen space.</summary>
public static class TacticalMapTransform
{
    /// <summary>Converts one continuous Core position using presentation-only center and zoom.</summary>
    public static Vector2 ToScreen(
        double xKilometers,
        double yKilometers,
        Vector2 viewportCenter,
        double pixelsPerKilometer
    )
    {
        if (!double.IsFinite(pixelsPerKilometer) || pixelsPerKilometer <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pixelsPerKilometer),
                pixelsPerKilometer,
                "Tactical map scale must be finite and positive."
            );
        }

        return viewportCenter
            + new Vector2((float)(xKilometers * pixelsPerKilometer), (float)(-yKilometers * pixelsPerKilometer));
    }

    /// <summary>Converts a clockwise-from-north Core heading into a Godot screen direction.</summary>
    public static Vector2 HeadingToScreenDirection(double headingDegrees)
    {
        if (!double.IsFinite(headingDegrees))
        {
            throw new ArgumentOutOfRangeException(
                nameof(headingDegrees),
                headingDegrees,
                "Tactical heading must be finite."
            );
        }

        double headingRadians = headingDegrees * Math.PI / 180;
        return new Vector2((float)Math.Sin(headingRadians), (float)-Math.Cos(headingRadians));
    }
}
