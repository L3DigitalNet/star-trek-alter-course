using AlterCourse.Core.Sensors;
using AlterCourse.Core.Strategic;
using Godot;

namespace AlterCourse.Godot.Gameplay;

/// <summary>
/// Presents travel or combat command data and raises typed UI intent without submitting simulation commands.
/// </summary>
public partial class CommandDeckWorkspace : Control
{
    /// <summary>Provides the strategic identity selected through the Command Deck map.</summary>
    public sealed class DestinationEventArgs(LocationId locationId) : EventArgs
    {
        /// <summary>Gets the selected strategic identity.</summary>
        public LocationId LocationId { get; } = locationId;
    }

    /// <summary>Provides the presentation action requested through the Command Deck inspector.</summary>
    public sealed class ActionEventArgs(CommandInterfaceAction action) : EventArgs
    {
        /// <summary>Gets the action whose submission was requested.</summary>
        public CommandInterfaceAction Action { get; } = action;
    }

    /// <summary>Provides the observer-local sensor contact selected through the tactical map.</summary>
    public sealed class ContactEventArgs(SensorContactId contactId) : EventArgs
    {
        /// <summary>Gets the selected observer-local identity.</summary>
        public SensorContactId ContactId { get; } = contactId;
    }

    private readonly Dictionary<string, Button> _actionButtons = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CommandInterfaceAction> _presentedActions = new(StringComparer.Ordinal);
    private VBoxContainer _systemRows = null!;
    private Label _systemsSummary = null!;
    private Label _mapTitle = null!;
    private Label _mapMode = null!;
    private Label _mapFooter = null!;
    private Label _inspectorHeading = null!;
    private VBoxContainer _inspectorContent = null!;
    private VBoxContainer _contextActions = null!;
    private StrategicMapView _strategicMap = null!;
    private TacticalMapView _tacticalMap = null!;

    /// <summary>Notifies the shell that the player selected a strategic destination.</summary>
    public event EventHandler<DestinationEventArgs>? DestinationSelected;

    /// <summary>Notifies the shell that a currently submittable presentation action was requested.</summary>
    public event EventHandler<ActionEventArgs>? PresentationActionRequested;

    /// <summary>Notifies the shell that the player selected an actor-safe live sensor contact.</summary>
    public event EventHandler<ContactEventArgs>? ContactSelected;

    /// <summary>Gets the currently displayed command hierarchy.</summary>
    public CommandInterfaceMode? CurrentMode { get; private set; }

    /// <summary>Gets whether the current display is live or an illustrative preview.</summary>
    public CommandInterfaceDataMode? CurrentDataMode { get; private set; }

    /// <summary>Gets the selected strategic identity most recently presented or selected.</summary>
    public LocationId? SelectedLocationId { get; private set; }

    /// <summary>Gets the selected observer-local sensor contact most recently presented.</summary>
    public SensorContactId? SelectedContactId { get; private set; }

    /// <summary>Gets the production strategic map control used by this workspace.</summary>
    public StrategicMapView StrategicMap => _strategicMap;

    /// <summary>Gets the production tactical map control used by this workspace.</summary>
    public TacticalMapView TacticalMap => _tacticalMap;

    /// <inheritdoc />
    public override void _Ready()
    {
        _systemRows = GetNode<VBoxContainer>("%SystemRows");
        _systemsSummary = GetNode<Label>("%SystemsSummary");
        _mapTitle = GetNode<Label>("%MapTitle");
        _mapMode = GetNode<Label>("%MapMode");
        _mapFooter = GetNode<Label>("%MapFooter");
        _inspectorHeading = GetNode<Label>("%InspectorHeading");
        _inspectorContent = GetNode<VBoxContainer>("%InspectorContent");
        _contextActions = GetNode<VBoxContainer>("%ContextActions");
        _strategicMap = GetNode<StrategicMapView>("%StrategicMap");
        _tacticalMap = GetNode<TacticalMapView>("%TacticalMap");
        _strategicMap.DestinationSelected = OnDestinationSelected;
        _tacticalMap.ContactSelected += OnContactSelected;
    }

    /// <summary>Displays one immutable presentation snapshot without retaining simulation authority.</summary>
    public void Present(CommandInterfacePresentation presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        EnsureReady();

        if (presentation.Mode == CommandInterfaceMode.Engineering)
        {
            throw new ArgumentException(
                "The Command Deck workspace supports only travel and combat modes.",
                nameof(presentation)
            );
        }

        CurrentMode = presentation.Mode;
        CurrentDataMode = presentation.DataMode;
        SelectedLocationId = presentation.SelectedLocationId;
        SelectedContactId = presentation.SelectedContactId;

        PresentSystems(presentation);
        PresentMap(presentation);
        PresentInspector(presentation);
        PresentActions(presentation);
    }

    /// <summary>Displays one approved deterministic preview through the production workspace.</summary>
    public void PresentPreview(CommandInterfaceDataMode dataMode)
    {
        if (dataMode is not (CommandInterfaceDataMode.TravelPreview or CommandInterfaceDataMode.CombatPreview))
        {
            throw new ArgumentOutOfRangeException(nameof(dataMode), dataMode, "Select a Command Deck preview mode.");
        }

        Present(CommandInterfacePreviewFixtures.Create(dataMode));
    }

    /// <summary>Reports whether an action is currently enabled for UI submission.</summary>
    public bool IsActionEnabled(string actionId) =>
        _actionButtons.TryGetValue(actionId, out Button? button) && !button.Disabled;

    /// <summary>Gets visible enabled context actions for shell-owned focus traversal.</summary>
    public IEnumerable<Control> GetVisibleFocusControls() =>
        _actionButtons.Values.Where(button => button.IsVisibleInTree() && !button.Disabled);

    private void PresentSystems(CommandInterfacePresentation presentation)
    {
        ClearChildren(_systemRows);
        foreach (CommandInterfaceSystemRow system in presentation.Systems)
        {
            var row = new HBoxContainer { Name = $"System_{system.Id}" };
            var label = new Label
            {
                Text = system.Label,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ThemeTypeVariation = "TelemetryLabel",
            };
            var status = new Label
            {
                Text = FormatField(system.Status),
                ThemeTypeVariation = ToneVariation(system.Status.Tone),
                TooltipText = $"{system.Label}: {FormatField(system.Status)}",
            };
            row.AddChild(label);
            row.AddChild(status);
            _systemRows.AddChild(row);
        }

        _systemsSummary.Text =
            presentation.DataMode == CommandInterfaceDataMode.Live ? "LIVE / PLAYER-KNOWN" : "ILLUSTRATIVE PREVIEW";
    }

    private void PresentMap(CommandInterfacePresentation presentation)
    {
        bool isTravel = presentation.Mode == CommandInterfaceMode.Travel;
        _strategicMap.Visible = isTravel;
        _strategicMap.MouseFilter = isTravel ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
        _tacticalMap.Visible = !isTravel;
        _tacticalMap.MouseFilter = isTravel ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;

        _mapTitle.Text = isTravel ? "STRATEGIC NAVIGATION" : "TACTICAL SPACE";
        _mapTitle.ThemeTypeVariation = isTravel ? "PanelHeading" : "StatusCritical";
        _mapMode.Text =
            presentation.DataMode == CommandInterfaceDataMode.Live
                ? "LIVE CORE PROJECTION"
                : "DETERMINISTIC PREVIEW / NON-AUTHORITATIVE";

        if (isTravel)
        {
            if (presentation.Strategic is not null)
            {
                _strategicMap.Present(presentation.Strategic, presentation.SelectedLocationId);
            }
            else
            {
                _strategicMap.PresentPreview(
                    presentation.MapItems,
                    presentation.MapLinks,
                    presentation.SelectedLocationId
                );
            }

            _mapFooter.Text = presentation.SelectedLocationId is LocationId selected
                ? $"SELECTED {selected.Value.ToUpperInvariant()} / DESTINATION INTENT AVAILABLE"
                : "SELECT A DESTINATION TO REQUEST TRAVEL";
            return;
        }

        if (presentation.Tactical is not null)
        {
            _tacticalMap.Present(presentation.Tactical, presentation.Contacts, presentation.SelectedContactId);
        }
        else
        {
            _tacticalMap.PresentPreview(presentation.MapItems, presentation.MapLinks);
        }

        _mapFooter.Text =
            presentation.DataMode == CommandInterfaceDataMode.Live
                ? "TACTICAL MOTION / PLAYER-KNOWN CORE PROJECTION"
                : "HOSTILE CONTACTS / RELATIVE VECTORS / PREVIEW ONLY";
    }

    private void PresentInspector(CommandInterfacePresentation presentation)
    {
        ClearChildren(_inspectorContent);
        _inspectorHeading.Text =
            presentation.Mode == CommandInterfaceMode.Combat
                ? "SELECTED CONTACT / TACTICAL SUMMARY"
                : "DESTINATION / ROUTE";
        _inspectorHeading.ThemeTypeVariation =
            presentation.Mode == CommandInterfaceMode.Combat ? "StatusCritical" : "PanelHeading";

        foreach (CommandInterfaceTelemetrySection section in presentation.Telemetry)
        {
            var panel = new PanelContainer { Name = $"Telemetry_{section.Id}", ThemeTypeVariation = "InspectorPanel" };
            var content = new VBoxContainer();
            var heading = new Label { Text = section.Title, ThemeTypeVariation = ToneVariation(section.Tone) };
            content.AddChild(heading);
            foreach (CommandInterfaceField field in section.Fields)
            {
                var fieldRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
                fieldRow.AddChild(
                    new Label
                    {
                        Text = field.Label,
                        CustomMinimumSize = new Vector2(112, 0),
                        ThemeTypeVariation = "TelemetryLabel",
                    }
                );
                fieldRow.AddChild(
                    new Label
                    {
                        Text = FormatField(field),
                        SizeFlagsHorizontal = SizeFlags.ExpandFill,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        ThemeTypeVariation = ToneVariation(field.Tone),
                        AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    }
                );
                content.AddChild(fieldRow);
            }

            panel.AddChild(content);
            _inspectorContent.AddChild(panel);
        }
    }

    private void PresentActions(CommandInterfacePresentation presentation)
    {
        var retainedActionIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < presentation.Actions.Length; index++)
        {
            CommandInterfaceAction action = presentation.Actions[index];
            if (!retainedActionIds.Add(action.Id))
            {
                throw new InvalidOperationException($"Duplicate command-interface action id '{action.Id}'.");
            }

            ReconcileActionButton(action, presentation.DataMode, index);
        }

        foreach (string removedActionId in _actionButtons.Keys.Where(id => !retainedActionIds.Contains(id)).ToArray())
        {
            Button button = _actionButtons[removedActionId];
            _actionButtons.Remove(removedActionId);
            _presentedActions.Remove(removedActionId);
            _contextActions.RemoveChild(button);
            button.QueueFree();
        }
    }

    private void ReconcileActionButton(CommandInterfaceAction action, CommandInterfaceDataMode dataMode, int index)
    {
        bool canSubmit =
            dataMode == CommandInterfaceDataMode.Live
            && action.Availability == CommandInterfaceActionAvailability.Submittable;
        if (!_actionButtons.TryGetValue(action.Id, out Button? button))
        {
            string actionId = action.Id;
            button = new Button { Name = $"Action_{actionId}", FocusMode = FocusModeEnum.All };
            button.Pressed += () => OnActionPressed(actionId);
            _contextActions.AddChild(button);
            _actionButtons.Add(actionId, button);
        }

        string text = action.Label + ActionSuffix(action.Availability, canSubmit);
        bool disabled = !canSubmit;
        string tooltip =
            action.Tooltip
            ?? (
                canSubmit
                    ? "Requests presentation intent; the shell and Core remain authoritative."
                    : "Displayed for context only; this control cannot submit an intent."
            );
        StringName variation = ActionVariation(action.Tone);
        if (!string.Equals(button.Text, text, StringComparison.Ordinal))
        {
            button.Text = text;
        }

        if (button.Disabled != disabled)
        {
            button.Disabled = disabled;
        }

        if (!string.Equals(button.TooltipText, tooltip, StringComparison.Ordinal))
        {
            button.TooltipText = tooltip;
        }

        if (button.ThemeTypeVariation != variation)
        {
            button.ThemeTypeVariation = variation;
        }

        _presentedActions[action.Id] = action;
        if (button.GetIndex() != index)
        {
            _contextActions.MoveChild(button, index);
        }
    }

    private static string ActionSuffix(CommandInterfaceActionAvailability availability, bool canSubmit) =>
        availability switch
        {
            CommandInterfaceActionAvailability.PreviewOnly => " [PREVIEW]",
            CommandInterfaceActionAvailability.Disabled => " [UNAVAILABLE]",
            CommandInterfaceActionAvailability.Submittable when !canSubmit => " [NON-SUBMITTING]",
            _ => string.Empty,
        };

    private void OnDestinationSelected(LocationId locationId)
    {
        SelectedLocationId = locationId;
        DestinationSelected?.Invoke(this, new DestinationEventArgs(locationId));
    }

    private void OnContactSelected(object? sender, TacticalMapView.ContactEventArgs args)
    {
        SelectedContactId = args.ContactId;
        ContactSelected?.Invoke(this, new ContactEventArgs(args.ContactId));
    }

    private void OnActionPressed(string actionId)
    {
        if (
            CurrentDataMode == CommandInterfaceDataMode.Live
            && IsActionEnabled(actionId)
            && _presentedActions.TryGetValue(actionId, out CommandInterfaceAction? action)
        )
        {
            PresentationActionRequested?.Invoke(this, new ActionEventArgs(action));
        }
    }

    private void EnsureReady()
    {
        if (!IsNodeReady())
        {
            throw new InvalidOperationException("CommandDeckWorkspace must enter the scene tree before presentation.");
        }
    }

    private static string FormatField(CommandInterfaceField field) =>
        field.Availability == CommandInterfaceAvailability.Available ? field.Value : "UNAVAILABLE";

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

    private static StringName ActionVariation(CommandInterfaceTone tone) =>
        tone switch
        {
            CommandInterfaceTone.Critical => "DangerButton",
            CommandInterfaceTone.Caution or CommandInterfaceTone.Engineering => "WarningButton",
            _ => "CommandButton",
        };

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }
}
