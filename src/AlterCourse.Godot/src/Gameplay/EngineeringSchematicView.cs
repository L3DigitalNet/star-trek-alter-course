using System.Collections.Immutable;
using Godot;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Draws an immutable engineering component and link projection as a local technical schematic.</summary>
public partial class EngineeringSchematicView : Control
{
    private const float OuterPadding = 12;
    private const float CellGap = 10;
    private const float CellHeight = 62;
    private const float MinimumCellWidth = 170;
    private const float MaximumCellWidth = 220;
    private const float TitleBaseline = 22;
    private const float TelemetryBaseline = 45;
    private ImmutableArray<CommandInterfaceTelemetrySection> _components = [];
    private ImmutableArray<CommandInterfaceEngineeringLink> _links = [];
    private readonly Dictionary<string, Rect2> _componentBounds = new(StringComparer.Ordinal);
    private string? _selectedComponentId;

    /// <summary>Gets or sets the callback that receives a presentation-only component selection.</summary>
    public Action<string>? ComponentSelected { get; set; }

    /// <inheritdoc />
    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        Resized += OnResized;
    }

    /// <summary>Displays one immutable component topology without deriving engineering state.</summary>
    public void Present(
        IReadOnlyList<CommandInterfaceTelemetrySection> components,
        IReadOnlyList<CommandInterfaceEngineeringLink> links,
        string? selectedComponentId,
        bool isPreview
    )
    {
        ArgumentNullException.ThrowIfNull(components);
        ArgumentNullException.ThrowIfNull(links);
        _components = [.. components];
        _links = [.. links];
        _selectedComponentId = selectedComponentId;
        SetMeta("component_count", _components.Length);
        SetMeta("link_count", _links.Length);
        SetMeta("is_preview", isPreview);
        SetMeta("selected_component_id", selectedComponentId ?? string.Empty);
        SetMeta("maximum_component_width", MaximumCellWidth);
        SetMeta("topology_available", !_links.IsDefaultOrEmpty);
        OnResized();
        QueueRedraw();
    }

    /// <summary>Updates the selected outline without changing the supplied topology.</summary>
    public void SelectComponent(string componentId)
    {
        _selectedComponentId = componentId;
        SetMeta("selected_component_id", componentId);
        QueueRedraw();
    }

    /// <inheritdoc />
    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click)
        {
            return;
        }

        KeyValuePair<string, Rect2>? selected = _componentBounds.FirstOrDefault(entry =>
            entry.Value.HasPoint(click.Position)
        );
        if (selected is not KeyValuePair<string, Rect2> component || string.IsNullOrEmpty(component.Key))
        {
            return;
        }

        GrabFocus();
        SelectComponent(component.Key);
        ComponentSelected?.Invoke(component.Key);
        AcceptEvent();
    }

    /// <inheritdoc />
    public override void _Draw()
    {
        _componentBounds.Clear();
        if (_components.IsDefaultOrEmpty)
        {
            DrawUnavailable("TECHNICAL SCHEMATIC UNAVAILABLE", new Vector2(OuterPadding, 28), HorizontalAlignment.Left);
            DrawFocusOutline();
            return;
        }

        BuildComponentBounds();
        DrawLinks();
        foreach (CommandInterfaceTelemetrySection component in _components)
        {
            DrawComponent(component, _componentBounds[component.Id]);
        }

        if (_links.IsDefaultOrEmpty)
        {
            DrawUnavailable(
                "DISTRIBUTION TOPOLOGY UNAVAILABLE",
                new Vector2(OuterPadding, Size.Y - 8),
                HorizontalAlignment.Center
            );
        }

        DrawFocusOutline();
    }

    private void BuildComponentBounds()
    {
        int columns = ColumnCount();
        float availableWidth = Math.Max(MinimumCellWidth, Size.X - OuterPadding * 2);
        float cellWidth = Math.Clamp(
            (availableWidth - CellGap * (columns - 1)) / columns,
            MinimumCellWidth,
            MaximumCellWidth
        );
        float columnStep = columns == 1 ? 0 : (availableWidth - cellWidth) / (columns - 1);
        for (int index = 0; index < _components.Length; index++)
        {
            int column = index % columns;
            int row = index / columns;
            _componentBounds[_components[index].Id] = new Rect2(
                OuterPadding + column * columnStep,
                OuterPadding + row * (CellHeight + CellGap),
                cellWidth,
                CellHeight
            );
        }
    }

    private void DrawLinks()
    {
        foreach (CommandInterfaceEngineeringLink link in _links)
        {
            if (
                _componentBounds.TryGetValue(link.OriginId, out Rect2 origin)
                && _componentBounds.TryGetValue(link.DestinationId, out Rect2 destination)
            )
            {
                DrawLine(origin.GetCenter(), destination.GetCenter(), ToneColor(link.Tone), 1);
            }
        }
    }

    private void DrawUnavailable(string text, Vector2 position, HorizontalAlignment alignment) =>
        DrawString(
            GetThemeFont("font", "MutedTelemetry"),
            position,
            text,
            alignment,
            Size.X - OuterPadding * 2,
            GetThemeFontSize("font_size", "MutedTelemetry"),
            GetThemeColor("font_color", "MutedTelemetry")
        );

    private void DrawFocusOutline()
    {
        if (HasFocus())
        {
            DrawRect(new Rect2(Vector2.One, Size - Vector2.One * 2), new Color("67c6d4"), filled: false, width: 2);
        }
    }

    private void DrawComponent(CommandInterfaceTelemetrySection component, Rect2 bounds)
    {
        bool selected = string.Equals(component.Id, _selectedComponentId, StringComparison.Ordinal);
        Color tone = ToneColor(component.Tone);
        DrawRect(bounds, new Color("0b1d25e6"));
        DrawRect(bounds, tone, filled: false, width: selected ? 2 : 1);
        DrawString(
            GetThemeFont("font", "PanelHeading"),
            bounds.Position + new Vector2(8, TitleBaseline),
            component.Title,
            HorizontalAlignment.Left,
            bounds.Size.X - 16,
            GetThemeFontSize("font_size", "PanelHeading"),
            tone
        );
        string telemetry = string.Join(
            "  ·  ",
            component.Fields.Take(2).Select(field => $"{field.Label} {DisplayValue(field)}")
        );
        DrawString(
            GetThemeFont("font", "TelemetryLabel"),
            bounds.Position + new Vector2(8, TelemetryBaseline),
            telemetry,
            HorizontalAlignment.Left,
            bounds.Size.X - 16,
            GetThemeFontSize("font_size", "TelemetryLabel"),
            GetThemeColor("font_color", "TelemetryLabel")
        );
    }

    private int ColumnCount()
    {
        int responsiveColumns =
            Size.X >= 620 ? 3
            : Size.X >= 390 ? 2
            : 1;
        return Math.Max(1, Math.Min(responsiveColumns, _components.Length));
    }

    private void OnResized()
    {
        Vector2 minimum = new(360, RequiredHeight(ColumnCount()));
        if (!CustomMinimumSize.IsEqualApprox(minimum))
        {
            CustomMinimumSize = minimum;
        }

        QueueRedraw();
    }

    private float RequiredHeight(int columns) =>
        OuterPadding * 2
        + Math.Max(1, (int)Math.Ceiling((double)Math.Max(1, _components.Length) / columns)) * CellHeight
        + Math.Max(0, (int)Math.Ceiling((double)Math.Max(1, _components.Length) / columns) - 1) * CellGap;

    private Color ToneColor(CommandInterfaceTone tone) =>
        tone switch
        {
            CommandInterfaceTone.Critical => GetThemeColor("font_color", "StatusCritical"),
            CommandInterfaceTone.Caution or CommandInterfaceTone.Engineering => GetThemeColor(
                "font_color",
                "StatusCaution"
            ),
            CommandInterfaceTone.Nominal => GetThemeColor("font_color", "StatusNominal"),
            CommandInterfaceTone.Muted => GetThemeColor("font_color", "MutedTelemetry"),
            _ => GetThemeColor("font_color", "TelemetryValue"),
        };

    private static string DisplayValue(CommandInterfaceField field) =>
        field.Availability == CommandInterfaceAvailability.Available ? field.Value : "UNAVAILABLE";
}
