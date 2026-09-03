using AlterCourse.Core.Player;
using AlterCourse.Core.Sensors;
using Godot;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Draws tactical Core motion or explicitly illustrative contacts without owning simulation state.</summary>
public partial class TacticalMapView : Control
{
    private const double PixelsPerKilometer = 18;
    private const float PreviewPadding = 64;
    private const float ContactHitRadius = 16;

    /// <summary>Provides the observer-local identity selected on the tactical plot.</summary>
    public sealed class ContactEventArgs(SensorContactId contactId) : EventArgs
    {
        /// <summary>Gets the selected observer-local identity.</summary>
        public SensorContactId ContactId { get; } = contactId;
    }

    private TacticalProjection? _projection;
    private IReadOnlyList<CommandInterfaceContact> _contacts = [];
    private SensorContactId? _selectedContactId;
    private IReadOnlyList<CommandInterfaceMapItem> _previewItems = [];
    private IReadOnlyList<CommandInterfaceMapLink> _previewLinks = [];

    /// <summary>Notifies the workspace that a live actor-safe contact was selected.</summary>
    public event EventHandler<ContactEventArgs>? ContactSelected;

    /// <summary>Displays one fresh tactical projection.</summary>
    public void Present(TacticalProjection projection)
    {
        Present(projection, [], null);
    }

    /// <summary>Displays one fresh tactical projection and its actor-safe sensor contacts.</summary>
    public void Present(
        TacticalProjection projection,
        IReadOnlyList<CommandInterfaceContact> contacts,
        SensorContactId? selectedContactId
    )
    {
        ArgumentNullException.ThrowIfNull(projection);
        ArgumentNullException.ThrowIfNull(contacts);
        _projection = projection;
        _contacts = contacts.Where(contact => contact.Status != SensorContactStatus.Lost).ToArray();
        _selectedContactId = selectedContactId;
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
        _contacts = [];
        _selectedContactId = null;
        _previewItems = items.ToArray();
        _previewLinks = links.ToArray();
        QueueRedraw();
    }

    /// <inheritdoc />
    public override void _GuiInput(InputEvent @event)
    {
        if (
            _projection is not null
            && @event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true } click
            && FindContact(click.Position) is SensorContactId contactId
        )
        {
            AcceptEvent();
            ContactSelected?.Invoke(this, new ContactEventArgs(contactId));
        }
    }

    /// <summary>Selects a hit-tested live contact through the same typed path used by pointer input.</summary>
    public bool SelectContactAt(Vector2 screenPosition)
    {
        if (_projection is null || FindContact(screenPosition) is not SensorContactId contactId)
        {
            return false;
        }

        ContactSelected?.Invoke(this, new ContactEventArgs(contactId));
        return true;
    }

    /// <summary>Returns the hit contact value for the GDScript integration-test boundary, or zero.</summary>
    public long HitTestContactId(Vector2 screenPosition) => FindContact(screenPosition)?.Value ?? 0;

    /// <summary>Hit-tests explicit tactical candidates at the GDScript integration-test boundary.</summary>
    public long HitTestContactCandidates(Vector2 screenPosition, long[] contactIds, Vector2[] tacticalPositions)
    {
        ArgumentNullException.ThrowIfNull(contactIds);
        ArgumentNullException.ThrowIfNull(tacticalPositions);
        if (contactIds.Length != tacticalPositions.Length)
        {
            throw new ArgumentException(
                "Contact identities and tactical positions must have equal lengths.",
                nameof(tacticalPositions)
            );
        }

        return FindContact(
                screenPosition,
                contactIds.Select(
                    (id, index) =>
                        (
                            Id: new SensorContactId(id),
                            Position: MapPosition(tacticalPositions[index].X, tacticalPositions[index].Y)
                        )
                )
            )?.Value
            ?? 0;
    }

    /// <summary>Maps one presented contact's observed position for integration verification.</summary>
    public Vector2 MapContact(long contactId)
    {
        CommandInterfaceContact contact = _contacts.Single(contact => contact.Id == new SensorContactId(contactId));
        return MapPosition(contact.ObservedXKilometers, contact.ObservedYKilometers);
    }

    /// <summary>Returns one actor-safe presented contact label for integration verification.</summary>
    public string ContactLabel(long contactId) =>
        _contacts.Single(contact => contact.Id == new SensorContactId(contactId)).Label;

    /// <summary>Returns one actor-safe contact status for integration verification.</summary>
    public string ContactStatus(long contactId) =>
        _contacts.Single(contact => contact.Id == new SensorContactId(contactId)).Status.ToString();

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

        foreach (CommandInterfaceContact contact in _contacts)
        {
            DrawContact(contact);
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

    private void DrawContact(CommandInterfaceContact contact)
    {
        Vector2 point = MapPosition(contact.ObservedXKilometers, contact.ObservedYKilometers);
        Color color = ToneColor(
            contact.Status == SensorContactStatus.Current ? CommandInterfaceTone.Command : CommandInterfaceTone.Caution
        );
        Vector2[] marker =
        [
            point + new Vector2(0, -9),
            point + new Vector2(9, 0),
            point + new Vector2(0, 9),
            point + new Vector2(-9, 0),
            point + new Vector2(0, -9),
        ];
        DrawPolyline(marker, color, 2);
        if (_selectedContactId == contact.Id)
        {
            DrawCircle(point, 14, color, filled: false, width: 2);
        }

        DrawString(
            GetThemeFont("font", "TelemetryValue"),
            point + new Vector2(14, 5),
            contact.Label,
            HorizontalAlignment.Left,
            -1,
            GetThemeFontSize("font_size", "TelemetryValue"),
            color
        );
    }

    private SensorContactId? FindContact(Vector2 screenPosition) =>
        FindContact(
            screenPosition,
            _contacts.Select(contact =>
                (contact.Id, MapPosition(contact.ObservedXKilometers, contact.ObservedYKilometers))
            )
        );

    private static SensorContactId? FindContact(
        Vector2 screenPosition,
        IEnumerable<(SensorContactId Id, Vector2 Position)> contacts
    ) =>
        contacts
            .Select(contact => (contact.Id, Distance: contact.Position.DistanceTo(screenPosition)))
            .Where(candidate => candidate.Distance <= ContactHitRadius)
            .OrderBy(candidate => candidate.Distance)
            .ThenBy(candidate => candidate.Id.Value)
            .Select(candidate => (SensorContactId?)candidate.Id)
            .FirstOrDefault();

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
