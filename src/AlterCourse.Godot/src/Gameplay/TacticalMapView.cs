using AlterCourse.Core.Player;
using Godot;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Draws continuous tactical position and motion without owning simulation state.</summary>
public partial class TacticalMapView : Control
{
    private const double PixelsPerKilometer = 18;
    private TacticalProjection? _projection;

    /// <summary>Displays one fresh tactical projection.</summary>
    public void Present(TacticalProjection projection)
    {
        _projection = projection;
        QueueRedraw();
    }

    /// <summary>Maps a Core position through the same centralized transform used for drawing.</summary>
    public Vector2 MapPosition(double xKilometers, double yKilometers) =>
        TacticalMapTransform.ToScreen(xKilometers, yKilometers, Size / 2, PixelsPerKilometer);

    /// <inheritdoc />
    public override void _Draw()
    {
        Vector2 center = Size / 2;
        DrawLine(new Vector2(0, center.Y), new Vector2(Size.X, center.Y), new Color("28424b"), 1);
        DrawLine(new Vector2(center.X, 0), new Vector2(center.X, Size.Y), new Color("28424b"), 1);
        if (_projection is null)
        {
            return;
        }

        Vector2 ship = MapPosition(_projection.Position.XKilometers, _projection.Position.YKilometers);
        double headingRadians = (_projection.HeadingDegrees - 90) * Math.PI / 180;
        var direction = new Vector2((float)Math.Cos(headingRadians), (float)Math.Sin(headingRadians));
        DrawCircle(ship, 9, new Color("d6b75e"));
        DrawLine(
            ship,
            ship + direction * (float)(24 + _projection.SpeedKilometersPerSecond * 3),
            new Color("8fd8ee"),
            3
        );
    }
}
