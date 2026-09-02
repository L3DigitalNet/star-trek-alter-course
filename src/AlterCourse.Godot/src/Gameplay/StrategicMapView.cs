using AlterCourse.Core.Player;
using AlterCourse.Core.Strategic;
using Godot;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Draws continuous strategic locations and routes from a fresh Core projection.</summary>
public partial class StrategicMapView : Control
{
    private StrategicProjection? _projection;
    private LocationId? _selectedDestination;

    /// <summary>Gets or sets the owning screen's direct map-selection callback.</summary>
    public Action<LocationId>? DestinationSelected { get; set; }

    /// <summary>Displays one fresh strategic projection.</summary>
    public void Present(StrategicProjection projection, LocationId? selectedDestination)
    {
        _projection = projection;
        _selectedDestination = selectedDestination;
        QueueRedraw();
    }

    /// <inheritdoc />
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        StrategicLocationProjection? selected = _projection
            ?.Locations.Select(location =>
                (Location: location, Distance: ToScreen(location.Position).DistanceTo(click.Position))
            )
            .Where(candidate => candidate.Distance <= 24)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Location)
            .FirstOrDefault();
        if (selected is not null)
        {
            DestinationSelected?.Invoke(selected.Id);
            AcceptEvent();
        }
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        if (_projection is null)
        {
            return;
        }

        foreach (StrategicRouteProjection route in _projection.Routes)
        {
            StrategicLocationProjection origin = FindLocation(route.Origin);
            StrategicLocationProjection destination = FindLocation(route.Destination);
            DrawLine(ToScreen(origin.Position), ToScreen(destination.Position), new Color("3f6875"), 2);
        }

        foreach (StrategicLocationProjection location in _projection.Locations)
        {
            Vector2 point = ToScreen(location.Position);
            bool isCurrent = _projection.CurrentLocation?.Id == location.Id;
            bool isSelected = _selectedDestination == location.Id;
            Color color =
                isCurrent ? new Color("d6b75e")
                : isSelected ? new Color("8fd8ee")
                : new Color("adc7ce");
            DrawCircle(point, isCurrent || isSelected ? 10 : 7, color);
            DrawString(
                ThemeDB.FallbackFont,
                point + new Vector2(14, 5),
                location.DisplayName,
                HorizontalAlignment.Left,
                -1,
                16,
                color
            );
        }
    }

    private StrategicLocationProjection FindLocation(LocationId id) =>
        _projection!.Locations.Single(location => location.Id == id);

    private Vector2 ToScreen(StrategicMapPosition position)
    {
        const float Padding = 64;
        double minX = _projection!.Locations.Min(location => location.Position.X);
        double maxX = _projection.Locations.Max(location => location.Position.X);
        double minY = _projection.Locations.Min(location => location.Position.Y);
        double maxY = _projection.Locations.Max(location => location.Position.Y);
        double width = Math.Max(1, Size.X - Padding * 2);
        double height = Math.Max(1, Size.Y - Padding * 2);
        double normalizedX = (position.X - minX) / Math.Max(1, maxX - minX);
        double normalizedY = (position.Y - minY) / Math.Max(1, maxY - minY);
        return new Vector2((float)(Padding + normalizedX * width), (float)(Padding + (1 - normalizedY) * height));
    }
}
