using System.Collections.Immutable;
using Godot;

namespace AlterCourse.Godot.Gameplay;

/// <summary>
/// Projects one immutable engineering presentation into a reusable Godot workspace and emits typed UI intent.
/// </summary>
public partial class EngineeringWorkspace : Control
{
    private const string UnavailableText = "UNAVAILABLE — NOT PROVIDED BY LIVE SIMULATION";

    // Illustrative controls of the Engineering preview fixture. They never carry an Engineering operation, so they
    // can only ever render as preview-only; they are listed so the key grammar below admits them explicitly.
    private static readonly HashSet<string> ReservedPreviewActionIds = new(StringComparer.Ordinal)
    {
        "assign-repair",
        "isolate-eps",
        "prioritize-shields",
        "reduce-impulse",
        "reroute-eps",
    };

    /// <summary>Carries one authoritative command-interface intent requested by the Engineering workspace.</summary>
    public sealed class EngineeringCommandRequestedEventArgs(CommandInterfaceAction action) : EventArgs
    {
        /// <summary>Gets the current projected action that the application boundary must validate before submission.</summary>
        public CommandInterfaceAction Action { get; } = action;
    }

    private readonly Dictionary<string, Button> _actionButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<Button, ActionSlot> _actionSlots = [];
    private readonly Dictionary<string, Button> _hierarchyButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CommandInterfaceHierarchyRow> _presentedHierarchy = new(StringComparer.Ordinal);
    private CommandInterfacePresentation? _presentation;
    private OwnShipActionBinding? _presentedBinding;

    // The last non-null (live) binding and live presentation. Preview snapshots carry no binding, so a live -> preview
    // -> live round trip must not look like a load; only a change between two live bindings is a world boundary.
    private OwnShipActionBinding? _liveBinding;
    private CommandInterfacePresentation? _livePresentation;
    private VBoxContainer _actions = null!;
    private Label _actionsHeading = null!;
    private VBoxContainer _connectedLoads = null!;
    private HBoxContainer _engineeringTabs = null!;
    private VBoxContainer _hierarchy = null!;
    private VBoxContainer _inspector = null!;
    private VBoxContainer _powerAllocation = null!;
    private Button? _pendingFocusRestore;
    private EngineeringSchematicView _schematic = null!;
    private Label _schematicHeading = null!;

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

        Control? retainedFocus = GetViewport().GuiGetFocusOwner();
        bool restoreOwnedFocus =
            retainedFocus is Button focusedButton
            && (_actionButtons.ContainsValue(focusedButton) || _hierarchyButtons.ContainsValue(focusedButton));
        if (presentation.Binding is { } binding)
        {
            if (_liveBinding is not null && _liveBinding != binding)
            {
                CrossWorldBoundary(presentation);
            }

            _liveBinding = binding;
            _livePresentation = presentation;
        }

        _presentation = presentation;
        CurrentDataMode = presentation.DataMode;
        SetMeta("data_mode", presentation.DataMode.ToString());
        SetMeta("is_preview", IsPreviewMode);
        _engineeringTabs.Visible = IsPreviewMode;
        _schematicHeading.Text = IsPreviewMode ? "POWER / EPS DISTRIBUTION" : "ENGINEERING CAPABILITY";

        ReconcileHierarchy(presentation.Engineering);
        RebuildSchematic(presentation.Engineering);
        RebuildInspector();
        RebuildSection(_connectedLoads, "CONNECTED LOADS", FindTelemetry("connected-loads"));
        RebuildSection(_powerAllocation, "POWER ALLOCATION SUMMARY", FindTelemetry("power-allocation"));
        // A new owner or simulation generation means every installed identity may now name something else, so no
        // action button survives across it: a control captured before a load keeps its old binding and is refused
        // by the shell instead of being silently retargeted by key. An ordinary refresh keeps the binding, so
        // buttons, keys, and focus are reconciled in place as before.
        if (presentation.Binding != _presentedBinding)
        {
            RemoveAllActionButtons();
            _presentedBinding = presentation.Binding;
        }

        ReconcileActions(presentation.Actions);
        bool focusStillRetained =
            retainedFocus is Button retainedButton
            && (_actionButtons.ContainsValue(retainedButton) || _hierarchyButtons.ContainsValue(retainedButton));
        _pendingFocusRestore = restoreOwnedFocus && focusStillRetained ? retainedFocus as Button : null;
        if (_pendingFocusRestore is not null)
        {
            OwnShipActionBinding? queuedFor = _presentedBinding;
            Callable.From(() => RestorePendingFocus(queuedFor)).CallDeferred();
        }
    }

    /// <summary>
    /// Handles a live presentation whose owner or simulation generation differs from the previous live one — a
    /// quick-load replaced the world. Installed identities are ship-local, so every hierarchy button and
    /// the <c>system:&lt;id&gt;</c> selection may now name another installation or another ship's installation.
    /// </summary>
    /// <remarks>
    /// All hierarchy buttons are discarded, so a row button captured (or focused) before the load is no longer
    /// registered: pressing it is ignored by <see cref="SelectCurrentComponent"/>, and no deferred focus restore can
    /// land on it. The selection survives only when the loaded world demonstrably presents the same component — same
    /// owner, same key, same authored component title — which keeps a same-ship reload of the running game stable;
    /// otherwise it is re-derived from the loaded presentation's defaults. Rejected: keeping selection by key alone
    /// (the defect: <c>system:4</c> silently retargets from shields to weapons, or onto another ship); resetting on
    /// every load (discards a still-valid same-ship selection for no safety gain).
    /// </remarks>
    private void CrossWorldBoundary(CommandInterfacePresentation loaded)
    {
        string? retained =
            SelectedComponentId is string selectedId
            && _liveBinding?.Owner == loaded.Binding?.Owner
            && ComponentTitle(_livePresentation, selectedId) is string before
            && string.Equals(before, ComponentTitle(loaded, selectedId), StringComparison.Ordinal)
                ? selectedId
                : null;
        RemoveAllHierarchyButtons();
        SelectedComponentId = retained;
        _pendingFocusRestore = null;
    }

    private static string? ComponentTitle(CommandInterfacePresentation? presentation, string componentId) =>
        presentation
            ?.Engineering?.Components.FirstOrDefault(component =>
                string.Equals(component.Id, componentId, StringComparison.Ordinal)
            )
            ?.Title;

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

    /// <summary>Gets the current visible Engineering controls for the shell-owned traversal ring.</summary>
    public IReadOnlyList<Control> GetVisibleFocusControls()
    {
        if (!IsInsideTree() || !IsVisibleInTree())
        {
            return [];
        }

        var controls = new List<Control>();
        controls.AddRange(_hierarchy.GetChildren().OfType<Control>().Where(IsFocusable));
        if (IsFocusable(_schematic))
        {
            controls.Add(_schematic);
        }

        controls.AddRange(_actions.GetChildren().OfType<Control>().Where(IsFocusable));
        return controls;
    }

    private void BindScene()
    {
        _hierarchy = GetNode<VBoxContainer>("%EngineeringHierarchy");
        _schematic = GetNode<EngineeringSchematicView>("%EngineeringSchematic");
        _inspector = GetNode<VBoxContainer>("%ComponentInspectorContent");
        _connectedLoads = GetNode<VBoxContainer>("%ConnectedLoadsContent");
        _engineeringTabs = GetNode<HBoxContainer>("%EngineeringTabs");
        _actions = GetNode<VBoxContainer>("%EngineeringActionsContent");
        _actionsHeading = new Label { Text = "ENGINEERING CONTROLS", ThemeTypeVariation = "PanelHeading" };
        _actions.AddChild(_actionsHeading);
        _powerAllocation = GetNode<VBoxContainer>("%PowerAllocationContent");
        _schematicHeading = GetNode<Label>("%SchematicHeading");
    }

    private void ReconcileHierarchy(CommandInterfaceEngineeringPresentation? engineering)
    {
        if (engineering is null || engineering.Hierarchy.IsDefaultOrEmpty)
        {
            RemoveAllHierarchyButtons();
            ClearChildren(_hierarchy);
            AddUnavailable(_hierarchy, "SYSTEM HIERARCHY");
            SelectedComponentId = null;
            SetMeta("selected_component_id", string.Empty);
            return;
        }

        RemoveNonButtonChildren(_hierarchy);

        var retainedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CommandInterfaceHierarchyRow row in engineering.Hierarchy)
        {
            if (!retainedIds.Add(row.Id))
            {
                throw new InvalidOperationException($"Duplicate Engineering hierarchy id '{row.Id}'.");
            }
        }

        bool selectionRetained =
            SelectedComponentId is string selectedId
            && retainedIds.Contains(selectedId)
            && engineering.Hierarchy.Any(row =>
                string.Equals(row.Id, selectedId, StringComparison.Ordinal)
                && row.Availability == CommandInterfaceAvailability.Available
            );
        if (!selectionRetained)
        {
            SelectedComponentId = engineering
                .Hierarchy.FirstOrDefault(row =>
                    row.IsSelected && row.Availability == CommandInterfaceAvailability.Available
                )
                ?.Id;
            SelectedComponentId ??= engineering
                .Hierarchy.FirstOrDefault(row => row.Availability == CommandInterfaceAvailability.Available)
                ?.Id;
        }

        for (int index = 0; index < engineering.Hierarchy.Length; index++)
        {
            ReconcileHierarchyButton(engineering.Hierarchy[index], index);
        }

        foreach (string removedId in _hierarchyButtons.Keys.Where(id => !retainedIds.Contains(id)).ToArray())
        {
            RemoveHierarchyButton(removedId);
        }

        SetMeta("selected_component_id", SelectedComponentId ?? string.Empty);
    }

    private void ReconcileHierarchyButton(CommandInterfaceHierarchyRow row, int index)
    {
        if (!_hierarchyButtons.TryGetValue(row.Id, out Button? button))
        {
            string rowId = row.Id;
            button = new Button
            {
                Name = $"Hierarchy_{SafeNodeName(row.Id)}",
                Alignment = HorizontalAlignment.Left,
                FocusMode = FocusModeEnum.All,
                ToggleMode = true,
            };
            Button pressed = button;
            button.Pressed += () => SelectCurrentComponent(pressed, rowId);
            _hierarchy.AddChild(button);
            _hierarchyButtons.Add(row.Id, button);
        }

        string indent = row.ParentId is null ? string.Empty : "    ";
        string attention = row.AttentionCount > 0 ? $"  [{row.AttentionCount} !]" : string.Empty;
        string unavailable =
            row.Availability == CommandInterfaceAvailability.Unavailable ? "  — UNAVAILABLE" : string.Empty;
        bool selected = string.Equals(row.Id, SelectedComponentId, StringComparison.Ordinal);
        button.Text = $"{indent}{row.Label}{attention}{unavailable}";
        button.ButtonPressed = selected;
        button.Disabled = row.Availability == CommandInterfaceAvailability.Unavailable;
        button.ThemeTypeVariation = VariationForButton(row.Tone, selected);
        button.TooltipText =
            row.Availability == CommandInterfaceAvailability.Available
                ? $"Inspect {row.Label}."
                : $"{row.Label} is not provided by the live simulation.";
        _presentedHierarchy[row.Id] = row;
        if (button.GetIndex() != index)
        {
            _hierarchy.MoveChild(button, index);
        }
    }

    // A button discarded at a world boundary still exists (QueueFree) and may still be signalled; only the button
    // currently registered for the key may select, so a pre-load row cannot select the loaded world's same key.
    private void SelectCurrentComponent(Button button, string componentId)
    {
        if (
            _hierarchyButtons.TryGetValue(componentId, out Button? current)
            && current == button
            && _presentedHierarchy.TryGetValue(componentId, out CommandInterfaceHierarchyRow? row)
            && row.Availability == CommandInterfaceAvailability.Available
        )
        {
            SelectComponent(componentId);
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

    private void ReconcileActions(ImmutableArray<CommandInterfaceAction> actions)
    {
        RemoveNonButtonChildren(_actions, _actionsHeading);
        var allIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (CommandInterfaceAction action in actions)
        {
            if (!allIds.Add(action.Id))
            {
                throw new InvalidOperationException($"Duplicate Engineering action id '{action.Id}'.");
            }
        }

        IEnumerable<CommandInterfaceAction> engineeringActions = actions.Where(action =>
            IsEngineeringActionKey(action.Id)
        );
        CommandInterfaceAction[] currentActions = [.. engineeringActions];
        var retainedIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < currentActions.Length; index++)
        {
            CommandInterfaceAction action = currentActions[index];
            retainedIds.Add(action.Id);
            ReconcileActionButton(action, index + 1);
        }

        foreach (string removedId in _actionButtons.Keys.Where(id => !retainedIds.Contains(id)).ToArray())
        {
            RemoveActionButton(removedId);
        }

        if (currentActions.Length == 0 && _actions.GetChildCount() == 1)
        {
            AddUnavailable(_actions, "ENGINEERING CONTROLS");
        }
    }

    private void ReconcileActionButton(CommandInterfaceAction action, int index)
    {
        bool enabled =
            CurrentDataMode == CommandInterfaceDataMode.Live
            && action.Availability == CommandInterfaceActionAvailability.Submittable
            && action.EngineeringOperation is not null;
        if (!_actionButtons.TryGetValue(action.Id, out Button? button))
        {
            var slot = new ActionSlot(action);
            button = new Button
            {
                Name = $"Action_{SafeNodeName(action.Id)}",
                Alignment = HorizontalAlignment.Left,
                FocusMode = FocusModeEnum.All,
            };
            Button pressed = button;
            button.Pressed += () => OnActionPressed(pressed, slot);
            _actions.AddChild(button);
            _actionButtons.Add(action.Id, button);
            _actionSlots.Add(button, slot);
        }

        string suffix = action.Availability switch
        {
            CommandInterfaceActionAvailability.PreviewOnly => "  [PREVIEW ONLY]",
            CommandInterfaceActionAvailability.Disabled => "  [UNAVAILABLE]",
            _ when CurrentDataMode != CommandInterfaceDataMode.Live => "  [NON-SUBMITTING]",
            _ => string.Empty,
        };
        string text = action.Label + suffix;
        bool disabled = !enabled;
        StringName variation = VariationForButton(action.Tone, false);
        string tooltip = ActionTooltip(action, enabled);
        if (!string.Equals(button.Text, text, StringComparison.Ordinal))
        {
            button.Text = text;
        }

        if (button.Disabled != disabled)
        {
            button.Disabled = disabled;
        }

        if (button.ThemeTypeVariation != variation)
        {
            button.ThemeTypeVariation = variation;
        }

        if (!string.Equals(button.TooltipText, tooltip, StringComparison.Ordinal))
        {
            button.TooltipText = tooltip;
        }

        _actionSlots[button].Action = action;
        if (button.GetIndex() != index)
        {
            _actions.MoveChild(button, index);
        }
    }

    private static string ActionTooltip(CommandInterfaceAction action, bool enabled) =>
        action.Tooltip
        ?? (
            enabled ? "Submit this intent to the application boundary for authoritative validation."
            : action.Availability == CommandInterfaceActionAvailability.PreviewOnly
                ? "Illustrative control; no simulation command will be submitted."
            : "The live simulation does not currently support this engineering command."
        );

    /// <summary>
    /// Emits the action this particular button was last presented with. A button removed by a binding change still
    /// holds its old binding, so the shell can recognise and refuse it; the payload is still re-validated against the
    /// current Core projection there before anything is submitted.
    /// </summary>
    private void OnActionPressed(Button button, ActionSlot slot)
    {
        if (
            CurrentDataMode == CommandInterfaceDataMode.Live
            && !button.Disabled
            && slot.Action.Availability == CommandInterfaceActionAvailability.Submittable
            && slot.Action.EngineeringOperation is not null
        )
        {
            EngineeringCommandRequested?.Invoke(this, new EngineeringCommandRequestedEventArgs(slot.Action));
        }
    }

    private void RemoveAllActionButtons()
    {
        foreach (string id in _actionButtons.Keys.ToArray())
        {
            RemoveActionButton(id);
        }
    }

    private void RemoveActionButton(string id)
    {
        Button button = _actionButtons[id];
        _actionButtons.Remove(id);
        _actionSlots.Remove(button);
        _actions.RemoveChild(button);
        button.QueueFree();
    }

    /// <summary>
    /// Admits the Engineering key grammar: <c>balance</c>, <c>return-command</c>, <c>prioritize:&lt;id&gt;</c>,
    /// <c>repair:&lt;id&gt;</c> with a positive decimal installed identity, plus the reserved preview ids.
    /// </summary>
    private static bool IsEngineeringActionKey(string id) =>
        id is "balance" or "return-command"
        || ReservedPreviewActionIds.Contains(id)
        || HasInstalledIdentity(id, "prioritize:")
        || HasInstalledIdentity(id, "repair:");

    private static bool HasInstalledIdentity(string id, string prefix) =>
        id.StartsWith(prefix, StringComparison.Ordinal)
        && long.TryParse(
            id.AsSpan(prefix.Length),
            System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture,
            out long value
        )
        && value > 0;

    // Deferred, so it captures the binding it was queued under: a restore queued before a quick-load flushes after
    // the load and must not move focus onto a control of the replaced world. A stale call returns without clearing
    // the field, which may already hold a restore queued by the loaded world's own presentation.
    private void RestorePendingFocus(OwnShipActionBinding? queuedFor)
    {
        if (queuedFor != _presentedBinding)
        {
            return;
        }

        Button? button = _pendingFocusRestore;
        _pendingFocusRestore = null;
        if (
            button is not null
            && GodotObject.IsInstanceValid(button)
            && button.IsInsideTree()
            && (_actionButtons.ContainsValue(button) || _hierarchyButtons.ContainsValue(button))
        )
        {
            button.GrabFocus();
        }
    }

    private void RemoveAllHierarchyButtons()
    {
        foreach (string id in _hierarchyButtons.Keys.ToArray())
        {
            RemoveHierarchyButton(id);
        }
    }

    private void RemoveHierarchyButton(string id)
    {
        Button button = _hierarchyButtons[id];
        _hierarchyButtons.Remove(id);
        _presentedHierarchy.Remove(id);
        _hierarchy.RemoveChild(button);
        button.QueueFree();
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

    private static bool IsFocusable(Control control) =>
        control.IsVisibleInTree()
        && control.FocusMode != FocusModeEnum.None
        && (control is not BaseButton button || !button.Disabled);

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static void RemoveNonButtonChildren(Node parent, Node? retained = null)
    {
        foreach (Node child in parent.GetChildren().Where(child => child is not Button && child != retained))
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

    // Godot rejects ':' in node names, and keys such as "repair:4" contain one; tests locate buttons by this name.
    private static string SafeNodeName(string id) => id.Replace('-', '_').Replace(':', '_');

    /// <summary>Holds the action a button currently represents; updated in place by an ordinary refresh.</summary>
    private sealed class ActionSlot(CommandInterfaceAction action)
    {
        public CommandInterfaceAction Action { get; set; } = action;
    }
}
