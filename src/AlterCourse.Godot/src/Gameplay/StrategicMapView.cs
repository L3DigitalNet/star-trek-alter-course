using AlterCourse.Core.Player;
using AlterCourse.Core.Strategic;
using Godot;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Draws strategic Core projections or explicitly illustrative map previews.</summary>
public partial class StrategicMapView : Control
{
    private const float MapPadding = 64;

    private StrategicProjection? _projection;
    private LocationId? _selectedDestination;
    private IReadOnlyList<CommandInterfaceMapItem> _previewItems = [];
    private IReadOnlyList<CommandInterfaceMapLink> _previewLinks = [];

    /// <summary>Gets or sets the owning screen's direct map-selection callback.</summary>
    public Action<LocationId>? DestinationSelected { get; set; }

    /// <summary>Displays one fresh strategic projection.</summary>
    public void Present(StrategicProjection projection, LocationId? selectedDestination)
    {
        _projection = projection;
        _selectedDestination = selectedDestination;
        _previewItems = [];
        _previewLinks = [];
        QueueRedraw();
    }

    /// <summary>Displays immutable illustrative map data without promoting it to a Core projection.</summary>
    public void PresentPreview(
        IReadOnlyList<CommandInterfaceMapItem> items,
        IReadOnlyList<CommandInterfaceMapLink> links,
        LocationId? selectedDestination
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(links);
        _projection = null;
        _selectedDestination = selectedDestination;
        _previewItems = items.ToArray();
        _previewLinks = links.ToArray();
        QueueRedraw();
    }

    /// <inheritdoc />
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        LocationId? selected = _projection is null
            ? FindPreviewDestination(click.Position)
            : _projection
                .Locations.Select(location =>
                    (Location: location, Distance: ToScreen(location.Position).DistanceTo(click.Position))
                )
                .Where(candidate => candidate.Distance <= 24)
                .OrderBy(candidate => candidate.Distance)
                .Select(candidate => (LocationId?)candidate.Location.Id)
                .FirstOrDefault();
        if (selected is LocationId locationId)
        {
            _selectedDestination = locationId;
            QueueRedraw();
            DestinationSelected?.Invoke(locationId);
            AcceptEvent();
        }
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        if (_projection is null)
        {
            DrawPreview();
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
        double minX = _projection!.Locations.Min(location => location.Position.X);
        double maxX = _projection.Locations.Max(location => location.Position.X);
        double minY = _projection.Locations.Min(location => location.Position.Y);
        double maxY = _projection.Locations.Max(location => location.Position.Y);
        double width = Math.Max(1, Size.X - MapPadding * 2);
        double height = Math.Max(1, Size.Y - MapPadding * 2);
        double normalizedX = (position.X - minX) / Math.Max(1, maxX - minX);
        double normalizedY = (position.Y - minY) / Math.Max(1, maxY - minY);
        return new Vector2((float)(MapPadding + normalizedX * width), (float)(MapPadding + (1 - normalizedY) * height));
    }

    private void DrawPreview()
    {
        foreach (CommandInterfaceMapLink link in _previewLinks)
        {
            CommandInterfaceMapItem? origin = _previewItems.FirstOrDefault(item =>
                string.Equals(item.Id, link.OriginId, StringComparison.Ordinal)
            );
            CommandInterfaceMapItem? destination = _previewItems.FirstOrDefault(item =>
                string.Equals(item.Id, link.DestinationId, StringComparison.Ordinal)
            );
            if (origin is not null && destination is not null)
            {
                DrawLine(ToPreviewScreen(origin), ToPreviewScreen(destination), ToneColor(link.Tone), 2);
            }
        }

        foreach (CommandInterfaceMapItem item in _previewItems)
        {
            Vector2 point = ToPreviewScreen(item);
            Color color = ToneColor(item.Tone);
            bool selected = item.StrategicLocationId == _selectedDestination;
            if (item.Kind == CommandInterfaceMapItemKind.PlayerShip)
            {
                Vector2[] ship =
                [
                    point + new Vector2(-12, -9),
                    point + new Vector2(12, 0),
                    point + new Vector2(-12, 9),
                ];
                DrawColoredPolygon(ship, color);
            }
            else
            {
                DrawCircle(point, selected ? 12 : 8, color, filled: !selected, width: 2);
            }

            DrawString(
                GetThemeFont("font", "TelemetryValue"),
                point + new Vector2(16, 5),
                item.Label,
                HorizontalAlignment.Left,
                -1,
                GetThemeFontSize("font_size", "TelemetryValue"),
                color
            );
        }
    }

    private LocationId? FindPreviewDestination(Vector2 clickPosition) =>
        _previewItems
            .Where(item => item.StrategicLocationId is not null)
            .Select(item => (Item: item, Distance: ToPreviewScreen(item).DistanceTo(clickPosition)))
            .Where(candidate => candidate.Distance <= 24)
            .OrderBy(candidate => candidate.Distance)
            .Select(candidate => candidate.Item.StrategicLocationId)
            .FirstOrDefault();

    private Vector2 ToPreviewScreen(CommandInterfaceMapItem item) =>
        new(
            MapPadding + (float)(item.X / 100) * Math.Max(1, Size.X - MapPadding * 2),
            MapPadding + (float)(item.Y / 100) * Math.Max(1, Size.Y - MapPadding * 2)
        );

    private Color ToneColor(CommandInterfaceTone tone) => GetThemeColor("font_color", ToneVariation(tone));

    private static StringName ToneVariation(CommandInterfaceTone tone) =>
        tone switch
        {
            CommandInterfaceTone.Muted => "MutedTelemetry",
            CommandInterfaceTone.Nominal => "StatusNominal",
            CommandInterfaceTone.Caution => "StatusCaution",
            CommandInterfaceTone.Critical => "StatusCritical",
            CommandInterfaceTone.Command or CommandInterfaceTone.Navigation => "StationTabActive",
            CommandInterfaceTone.Engineering => "StatusCaution",
            _ => "TelemetryValue",
        };
}
