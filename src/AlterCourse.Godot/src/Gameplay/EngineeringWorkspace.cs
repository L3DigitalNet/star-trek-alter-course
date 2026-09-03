using System.Collections.Immutable;
using Godot;

namespace AlterCourse.Godot.Gameplay;

/// <summary>
/// Projects one immutable engineering presentation into a reusable Godot workspace and emits typed UI intent.
/// </summary>
public partial class EngineeringWorkspace : Control
{
    private const string UnavailableText = "UNAVAILABLE — NOT PROVIDED BY LIVE SIMULATION";
    private static readonly HashSet<string> EngineeringActionIds = new(StringComparer.Ordinal)
    {
        "allocate-power",
        "assign-repair",
        "isolate-eps",
        "prioritize-shields",
        "reduce-impulse",
        "reroute-eps",
    };

    /// <summary>Carries one authoritative command-interface intent requested by the Engineering workspace.</summary>
    public sealed class EngineeringCommandRequestedEventArgs(CommandInterfaceIntent intent) : EventArgs
    {
        /// <summary>Gets the intent that the application boundary must validate before submission.</summary>
        public CommandInterfaceIntent Intent { get; } = intent;
    }

    private readonly Dictionary<string, Button> _actionButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Button> _hierarchyButtons = new(StringComparer.Ordinal);
    private CommandInterfacePresentation? _presentation;
    private VBoxContainer _actions = null!;
    private VBoxContainer _connectedLoads = null!;
    private VBoxContainer _hierarchy = null!;
    private VBoxContainer _inspector = null!;
    private VBoxContainer _powerAllocation = null!;
    private EngineeringSchematicView _schematic = null!;

    /// <summary>Notifies an application adapter of an authoritative intent selected in this view.</summary>
    public event EventHandler<EngineeringCommandRequestedEventArgs>? EngineeringCommandRequested;

    /// <summary>Gets the most recently presented data mode.</summary>
    public CommandInterfaceDataMode? CurrentDataMode { get; private set; }

    /// <summary>Gets whether the current snapshot is illustrative and non-authoritative.</summary>
    public bool IsPreviewMode => CurrentDataMode == CommandInterfaceDataMode.EngineeringPreview;

    /// <summary>Gets the stable identifier selected in the engineering hierarchy or schematic.</summary>
    public string? SelectedComponentId { get; private set; }

    /// <inheritdoc />
    public override void _Ready()
    {
        BindScene();
        _schematic.ComponentSelected = SelectComponent;
    }

    /// <summary>Replaces the complete view with one immutable engineering display snapshot.</summary>
    public void Present(CommandInterfacePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (presentation.Mode != CommandInterfaceMode.Engineering)
        {
            throw new ArgumentException(
                "EngineeringWorkspace requires an engineering-mode presentation.",
                nameof(presentation)
            );
        }

        _presentation = presentation;
        CurrentDataMode = presentation.DataMode;
        SelectedComponentId = presentation.Engineering?.Hierarchy.FirstOrDefault(row => row.IsSelected)?.Id;
        SetMeta("data_mode", presentation.DataMode.ToString());
        SetMeta("is_preview", IsPreviewMode);
        SetMeta("selected_component_id", SelectedComponentId ?? string.Empty);

        RebuildHierarchy(presentation.Engineering);
        RebuildSchematic(presentation.Engineering);
        RebuildInspector();
        RebuildSection(_connectedLoads, "CONNECTED LOADS", FindTelemetry("connected-loads"));
        RebuildSection(_powerAllocation, "POWER ALLOCATION SUMMARY", FindTelemetry("power-allocation"));
        RebuildActions(presentation.Actions);
        UpdateFocusTraversal();
    }

    /// <summary>Returns whether the named engineering action currently submits authoritative intent.</summary>
    public bool IsActionEnabled(string actionId) =>
        _actionButtons.TryGetValue(actionId, out Button? button) && !button.Disabled;

    /// <summary>Places keyboard focus on the first meaningful control in the visible Engineering workspace.</summary>
    public void GrabEntryFocus()
    {
        if (!IsInsideTree() || !IsVisibleInTree())
        {
            return;
        }

        Button? entry = _hierarchyButtons.Values.FirstOrDefault(button => button.ButtonPressed && !button.Disabled);
        entry ??= _hierarchyButtons.Values.FirstOrDefault(button => !button.Disabled);
        if (entry is not null)
        {
            entry.GrabFocus();
        }
        else
        {
            _schematic.GrabFocus();
        }
    }

    private void BindScene()
    {
        _hierarchy = GetNode<VBoxContainer>("%EngineeringHierarchy");
        _schematic = GetNode<EngineeringSchematicView>("%EngineeringSchematic");
        _inspector = GetNode<VBoxContainer>("%ComponentInspectorContent");
        _connectedLoads = GetNode<VBoxContainer>("%ConnectedLoadsContent");
        _actions = GetNode<VBoxContainer>("%EngineeringActionsContent");
        _powerAllocation = GetNode<VBoxContainer>("%PowerAllocationContent");
    }

    private void RebuildHierarchy(CommandInterfaceEngineeringPresentation? engineering)
    {
        ClearChildren(_hierarchy);
        _hierarchyButtons.Clear();
        if (engineering is null || engineering.Hierarchy.IsDefaultOrEmpty)
        {
            AddUnavailable(_hierarchy, "SYSTEM HIERARCHY");
            return;
        }

        foreach (CommandInterfaceHierarchyRow row in engineering.Hierarchy)
        {
            string indent = row.ParentId is null ? string.Empty : "    ";
            string attention = row.AttentionCount > 0 ? $"  [{row.AttentionCount} !]" : string.Empty;
            string unavailable =
                row.Availability == CommandInterfaceAvailability.Unavailable ? "  — UNAVAILABLE" : string.Empty;
            var button = new Button
            {
                Name = $"Hierarchy_{SafeNodeName(row.Id)}",
                Text = $"{indent}{row.Label}{attention}{unavailable}",
                Alignment = HorizontalAlignment.Left,
                FocusMode = FocusModeEnum.All,
                ToggleMode = true,
                ButtonPressed = string.Equals(row.Id, SelectedComponentId, StringComparison.Ordinal),
                Disabled = row.Availability == CommandInterfaceAvailability.Unavailable,
                ThemeTypeVariation = VariationForButton(row.Tone, row.IsSelected),
                TooltipText =
                    row.Availability == CommandInterfaceAvailability.Available
                        ? $"Inspect {row.Label}."
                        : $"{row.Label} is not provided by the live simulation.",
            };
            string rowId = row.Id;
            button.Pressed += () => SelectComponent(rowId);
            _hierarchy.AddChild(button);
            _hierarchyButtons.Add(row.Id, button);
        }
    }

    private void RebuildSchematic(CommandInterfaceEngineeringPresentation? engineering)
    {
        _schematic.Present(engineering?.Components ?? [], engineering?.Links ?? [], SelectedComponentId, IsPreviewMode);
    }

    private void SelectComponent(string componentId)
    {
        SelectedComponentId = componentId;
        SetMeta("selected_component_id", componentId);
        foreach ((string id, Button button) in _hierarchyButtons)
        {
            button.ButtonPressed = string.Equals(id, componentId, StringComparison.Ordinal);
        }

        _schematic.SelectComponent(componentId);
        RebuildInspector();
    }

    private void RebuildInspector()
    {
        CommandInterfaceTelemetrySection? section = _presentation?.Engineering?.Components.FirstOrDefault(component =>
            string.Equals(component.Id, SelectedComponentId, StringComparison.Ordinal)
        );
        section ??= FindTelemetry("selected-component");
        RebuildSection(_inspector, "SELECTED COMPONENT", section);
    }

    private void RebuildActions(ImmutableArray<CommandInterfaceAction> actions)
    {
        ClearChildren(_actions);
        _actionButtons.Clear();
        AddHeading(_actions, "ENGINEERING CONTROLS");
        IEnumerable<CommandInterfaceAction> engineeringActions = actions.Where(action =>
            EngineeringActionIds.Contains(action.Id)
        );
        bool any = false;
        foreach (CommandInterfaceAction action in engineeringActions)
        {
            any = true;
            bool enabled =
                action.Availability == CommandInterfaceActionAvailability.Submittable && action.Intent is not null;
            string suffix = action.Availability switch
            {
                CommandInterfaceActionAvailability.PreviewOnly => "  [PREVIEW ONLY]",
                CommandInterfaceActionAvailability.Disabled => "  [UNAVAILABLE]",
                _ => string.Empty,
            };
            var button = new Button
            {
                Name = $"Action_{SafeNodeName(action.Id)}",
                Text = action.Label + suffix,
                Alignment = HorizontalAlignment.Left,
                FocusMode = FocusModeEnum.All,
                Disabled = !enabled,
                ThemeTypeVariation = VariationForButton(action.Tone, false),
                TooltipText =
                    enabled ? "Submit this intent to the application boundary for authoritative validation."
                    : action.Availability == CommandInterfaceActionAvailability.PreviewOnly
                        ? "Illustrative control; no simulation command will be submitted."
                    : "The live simulation does not currently support this engineering command.",
            };
            if (enabled)
            {
                CommandInterfaceIntent intent = action.Intent!.Value;
                button.Pressed += () =>
                    EngineeringCommandRequested?.Invoke(this, new EngineeringCommandRequestedEventArgs(intent));
            }

            _actions.AddChild(button);
            _actionButtons.Add(action.Id, button);
        }

        if (!any)
        {
            AddUnavailable(_actions, "ENGINEERING CONTROLS");
        }
    }

    private static void RebuildSection(
        VBoxContainer target,
        string fallbackTitle,
        CommandInterfaceTelemetrySection? section
    )
    {
        ClearChildren(target);
        AddHeading(target, section?.Title ?? fallbackTitle);
        if (section is null || section.Fields.IsDefaultOrEmpty)
        {
            AddUnavailable(target, fallbackTitle);
            return;
        }

        foreach (CommandInterfaceField field in section.Fields)
        {
            var row = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            row.AddChild(
                new Label
                {
                    Text = field.Label,
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    ThemeTypeVariation = "TelemetryLabel",
                }
            );
            row.AddChild(
                new Label
                {
                    Text = DisplayValue(field),
                    ThemeTypeVariation = VariationForLabel(field.Tone, field.Availability),
                }
            );
            target.AddChild(row);
        }
    }

    private CommandInterfaceTelemetrySection? FindTelemetry(string id) =>
        _presentation?.Telemetry.FirstOrDefault(section => string.Equals(section.Id, id, StringComparison.Ordinal));

    private void UpdateFocusTraversal()
    {
        if (!IsInsideTree())
        {
            return;
        }

        var controls = new List<Control>();
        controls.AddRange(_hierarchyButtons.Values.Where(button => !button.Disabled));
        controls.Add(_schematic);
        controls.AddRange(_actionButtons.Values.Where(button => !button.Disabled));
        for (int index = 0; index < controls.Count; index++)
        {
            Control previous = controls[(index - 1 + controls.Count) % controls.Count];
            Control next = controls[(index + 1) % controls.Count];
            controls[index].FocusPrevious = previous.GetPath();
            controls[index].FocusNeighborTop = previous.GetPath();
            controls[index].FocusNext = next.GetPath();
            controls[index].FocusNeighborBottom = next.GetPath();
        }
    }

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static void AddHeading(Node parent, string text)
    {
        parent.AddChild(new Label { Text = text, ThemeTypeVariation = "PanelHeading" });
    }

    private static void AddUnavailable(Node parent, string subject)
    {
        parent.AddChild(
            new Label
            {
                Text = $"{subject}: {UnavailableText}",
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                ThemeTypeVariation = "MutedTelemetry",
            }
        );
    }

    private static string DisplayValue(CommandInterfaceField field) =>
        field.Availability == CommandInterfaceAvailability.Available ? field.Value : "UNAVAILABLE";

    private static StringName VariationForButton(CommandInterfaceTone tone, bool selected) =>
        tone switch
        {
            CommandInterfaceTone.Critical => "DangerButton",
            CommandInterfaceTone.Caution or CommandInterfaceTone.Engineering => "WarningButton",
            CommandInterfaceTone.Command => "CommandButton",
            _ when selected => "WarningButton",
            _ => string.Empty,
        };

    private static StringName VariationForLabel(CommandInterfaceTone tone, CommandInterfaceAvailability availability) =>
        availability == CommandInterfaceAvailability.Unavailable || tone == CommandInterfaceTone.Muted
            ? "MutedTelemetry"
            : tone switch
            {
                CommandInterfaceTone.Critical => "StatusCritical",
                CommandInterfaceTone.Caution or CommandInterfaceTone.Engineering => "StatusCaution",
                CommandInterfaceTone.Nominal => "StatusNominal",
                _ => "TelemetryValue",
            };

    private static string SafeNodeName(string id) => id.Replace('-', '_');
}
