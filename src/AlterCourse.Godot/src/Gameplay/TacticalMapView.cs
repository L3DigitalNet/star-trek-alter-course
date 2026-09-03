using AlterCourse.Core.Player;
using Godot;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Draws tactical Core motion or explicitly illustrative contacts without owning simulation state.</summary>
public partial class TacticalMapView : Control
{
    private const double PixelsPerKilometer = 18;
    private const float PreviewPadding = 64;

    private TacticalProjection? _projection;
    private IReadOnlyList<CommandInterfaceMapItem> _previewItems = [];
    private IReadOnlyList<CommandInterfaceMapLink> _previewLinks = [];

    /// <summary>Displays one fresh tactical projection.</summary>
    public void Present(TacticalProjection projection)
    {
        _projection = projection;
        _previewItems = [];
        _previewLinks = [];
        QueueRedraw();
    }

    /// <summary>Displays immutable illustrative contacts and vectors without creating tactical truth.</summary>
    public void PresentPreview(
        IReadOnlyList<CommandInterfaceMapItem> items,
        IReadOnlyList<CommandInterfaceMapLink> links
    )
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(links);
        _projection = null;
        _previewItems = items.ToArray();
        _previewLinks = links.ToArray();
        QueueRedraw();
    }

    /// <summary>Maps a Core position relative to the current ship-centered local plot.</summary>
    public Vector2 MapPosition(double xKilometers, double yKilometers)
    {
        double cameraXKilometers = _projection?.Position.XKilometers ?? 0;
        double cameraYKilometers = _projection?.Position.YKilometers ?? 0;
        return TacticalMapTransform.ToScreen(
            xKilometers - cameraXKilometers,
            yKilometers - cameraYKilometers,
            Size / 2,
            PixelsPerKilometer
        );
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        Vector2 center = Size / 2;
        DrawLine(new Vector2(0, center.Y), new Vector2(Size.X, center.Y), new Color("28424b"), 1);
        DrawLine(new Vector2(center.X, 0), new Vector2(center.X, Size.Y), new Color("28424b"), 1);
        if (_projection is null)
        {
            DrawPreview();
            return;
        }

        Vector2 ship = MapPosition(_projection.Position.XKilometers, _projection.Position.YKilometers);
        Vector2 direction = TacticalMapTransform.HeadingToScreenDirection(_projection.HeadingDegrees);
        DrawCircle(ship, 9, new Color("d6b75e"));
        DrawLine(
            ship,
            ship + direction * (float)(24 + _projection.SpeedKilometersPerSecond * 3),
            new Color("8fd8ee"),
            3
        );
    }

    private void DrawPreview()
    {
        CommandInterfaceMapItem? ship = _previewItems.FirstOrDefault(item =>
            item.Kind == CommandInterfaceMapItemKind.PlayerShip
        );
        if (ship is not null)
        {
            Vector2 shipPoint = ToPreviewScreen(ship);
            Color rangeColor = ToneColor(CommandInterfaceTone.Muted);
            DrawCircle(shipPoint, Math.Min(Size.X, Size.Y) * 0.18f, rangeColor, filled: false, width: 1);
            DrawCircle(shipPoint, Math.Min(Size.X, Size.Y) * 0.34f, rangeColor, filled: false, width: 1);
        }

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
                Vector2 start = ToPreviewScreen(origin);
                Vector2 end = ToPreviewScreen(destination);
                DrawLine(start, end, ToneColor(link.Tone), 2);
            }
        }

        foreach (CommandInterfaceMapItem item in _previewItems)
        {
            Vector2 point = ToPreviewScreen(item);
            Color color = ToneColor(item.Tone);
            Vector2[] marker =
                item.Kind == CommandInterfaceMapItemKind.PlayerShip
                    ? [point + new Vector2(-12, -9), point + new Vector2(12, 0), point + new Vector2(-12, 9)]
                    :
                    [
                        point + new Vector2(0, -11),
                        point + new Vector2(11, 0),
                        point + new Vector2(0, 11),
                        point + new Vector2(-11, 0),
                    ];
            DrawPolyline([.. marker, marker[0]], color, 2);
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

    private Vector2 ToPreviewScreen(CommandInterfaceMapItem item) =>
        new(
            PreviewPadding + (float)(item.X / 100) * Math.Max(1, Size.X - PreviewPadding * 2),
            PreviewPadding + (float)(item.Y / 100) * Math.Max(1, Size.Y - PreviewPadding * 2)
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
