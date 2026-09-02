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
}
