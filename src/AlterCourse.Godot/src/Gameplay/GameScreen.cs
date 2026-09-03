using System.Runtime.CompilerServices;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Strategic;
using Godot;
using GodotFile = Godot.FileAccess;

namespace AlterCourse.Godot.Gameplay;

/// <summary>
/// Owns one scene-lifetime simulation and adapts its player-known projection to persistent command workspaces.
/// </summary>
public partial class GameScreen : Control
{
    private const string SchemaPath = "res://content/schemas/ship-definition-v3.schema.json";
    private const string ShipPath = "res://content/ships/pathfinder.json";
    private const string DefaultQuickSaveUserPath = "user://quick-save.json";
    private const string LegacyDefaultQuickSaveUserPath = "user://quick-save-v1.json";
    private const string QuickSaveId = "quick-save";
    private const string QuickSaveDisplayName = "Quick Save";
    private const string ActionViewStrategic = "view_strategic";
    private const string ActionViewTactical = "view_tactical";
    private const string ActionTogglePause = "toggle_pause";
    private const string ActionCycleRate = "cycle_time_rate";
    private const string ActionAdvanceUntil = "advance_until_event";
    private const string ActionQuickSave = "quick_save";
    private const string ActionQuickLoad = "quick_load";
    private const string ActionEngageTravel = "engage_selected_travel";
    private const string ActionSetCourse = "set_tactical_course";

    private static readonly double[] RunningRates = [0.5, 1, 2, 4];

    private readonly SimulationRateController _rateController = new();
    private readonly List<PlayerAdvanceEvent> _recentEvents = [];
    private GameSimulation? _simulation;
    private ShipDefinitionCatalog? _shipCatalog;
    private PlayerProjection? _projection;
    private LocationId? _selectedDestination;
    private DateTimeOffset? _quickSaveCreatedAtUtc;
    private string _quickSavePath = null!;
    private bool _quickSaveUsesDefaultPath;
    private bool _engineeringWorkspaceActive;
    private double _lastRunningRate = 1;
    private CommandInterfaceMode _commandMode = CommandInterfaceMode.Travel;
    private CommandInterfaceDataMode _dataMode = CommandInterfaceDataMode.Live;
    private CommandDeckWorkspace _commandDeck = null!;
    private EngineeringWorkspace _engineering = null!;
    private Label _eventLogHeading = null!;
    private VBoxContainer _eventLog = null!;
    private VBoxContainer _captainActions = null!;
    private VBoxContainer _engineeringBottomActions = null!;
    private VBoxContainer _engineeringQueue = null!;
    private VBoxContainer _engineeringQueueActions = null!;
    private Label _timeLabel = null!;
    private Label _vesselStatusLabel = null!;
    private Label _rateStatusLabel = null!;
    private Label _viewStatusLabel = null!;
    private Label _alertStatusLabel = null!;
    private Label _messageLabel = null!;
    private Button _travelButton = null!;
    private Button _courseButton = null!;
    private Button _advanceUntilButton = null!;
    private Button _strategicButton = null!;
    private Button _tacticalButton = null!;
    private Button _commandStationButton = null!;
    private Button _engineeringStationButton = null!;
    private Button _engineeringBottomReturnButton = null!;
    private Button _quickSaveButton = null!;
    private Button _quickLoadButton = null!;
    private Button _pauseButton = null!;
    private Button _halfRateButton = null!;
    private Button _normalRateButton = null!;
    private Button _doubleRateButton = null!;
    private Button _quadRateButton = null!;

    /// <summary>Gets whether canonical content produced a complete playable simulation.</summary>
    public bool IsGameplayReady => _simulation is not null;

    /// <summary>Gets a process-local identity used to prove workspace switches retain the same simulation.</summary>
    public int SimulationIdentity => _simulation is null ? 0 : RuntimeHelpers.GetHashCode(_simulation);

    /// <summary>Gets or sets the Godot user-data path for the one quick-save slot.</summary>
    [Export]
    public string QuickSaveUserPath { get; set; } = DefaultQuickSaveUserPath;

    /// <summary>Gets or sets the canonical ship schema resource used during bootstrap.</summary>
    [Export]
    public string ShipSchemaResourcePath { get; set; } = SchemaPath;

    /// <summary>Gets or sets the canonical player-ship definition resource used during bootstrap.</summary>
    [Export]
    public string ShipDefinitionResourcePath { get; set; } = ShipPath;

    /// <summary>Gets the latest fresh player-known projection.</summary>
    public PlayerProjection? Projection => _projection;

    /// <inheritdoc />
    public override void _Ready()
    {
        BindScene();
        try
        {
            string quickSavePath = ResolveQuickSavePath(QuickSaveUserPath);
            bool quickSaveUsesDefaultPath = string.Equals(
                QuickSaveUserPath,
                DefaultQuickSaveUserPath,
                StringComparison.Ordinal
            );
            (ShipDefinitionCatalog catalog, GameSimulation simulation) = CreateSimulationFromCanonicalContent();
            _quickSavePath = quickSavePath;
            _quickSaveUsesDefaultPath = quickSaveUsesDefaultPath;
            _shipCatalog = catalog;
            _simulation = simulation;
            SetMeta("quick_save_user_path", QuickSaveUserPath);
            SetSimulationRate(1);
            ShowStrategicView();
            _messageLabel.Text = "Command systems ready.";
        }
        catch (Exception exception)
        {
            // Loading is fail-closed: retaining a partial aggregate would present commands whose
            // definition and validation contracts never completed.
            _simulation = null;
            _shipCatalog = null;
            _projection = null;
            _selectedDestination = null;
            SetGameplayEnabled(false);
            _messageLabel.Text = "Gameplay content is unavailable. Check the local installation and restart.";
            SetMeta("load_error", _messageLabel.Text);
            LogDiagnostic("Gameplay bootstrap failed", exception);
        }
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        ProcessSyntheticDelta(delta);
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event.IsActionPressed(ActionViewStrategic))
        {
            ShowStrategicView();
        }
        else if (@event.IsActionPressed(ActionViewTactical))
        {
            ShowTacticalView();
        }
        else if (@event.IsActionPressed(ActionTogglePause))
        {
            TogglePause();
        }
        else if (@event.IsActionPressed(ActionCycleRate))
        {
            CycleSimulationRate();
        }
        else if (@event.IsActionPressed(ActionAdvanceUntil))
        {
            AdvanceUntilNextPlayerRelevantEvent();
        }
        else if (@event.IsActionPressed(ActionQuickSave))
        {
            QuickSave();
        }
        else if (@event.IsActionPressed(ActionQuickLoad))
        {
            QuickLoad();
        }
        else if (@event.IsActionPressed(ActionEngageTravel))
        {
            RequestSelectedTravel();
        }
        else if (@event.IsActionPressed(ActionSetCourse))
        {
            SetDemonstrationCourse();
        }
        else
        {
            return;
        }

        GetViewport().SetInputAsHandled();
        SetMeta("last_input_action", @event.AsText());
    }

    /// <summary>Consumes controlled presentation elapsed time and returns submitted Core steps.</summary>
    public int ProcessSyntheticDelta(double elapsedSeconds)
    {
        // Preview is a frozen presentation fixture. It must neither consume fractional live time nor
        // let the regular frame loop overwrite the fixture with an authoritative projection.
        if (_dataMode != CommandInterfaceDataMode.Live)
        {
            return 0;
        }

        int steps = _rateController.ConsumeElapsed(elapsedSeconds);
        if (_simulation is null || steps == 0)
        {
            return 0;
        }

        try
        {
            SimulationAdvanceResult result = _simulation.AdvanceFixedSteps(steps);
            SetMeta("advance_status", "advanced");
            if (result.ResolvedEvents.Contains(PlayerAdvanceEvent.TravelArrived))
            {
                ClearSelectedDestination();
            }

            PresentResolvedEvents(result.ResolvedEvents, announce: false);
            RefreshProjection();
            return steps;
        }
        catch (Exception exception)
        {
            ReportAdvanceFailure(exception);
            return 0;
        }
    }

    /// <summary>Selects one of the five supported presentation time rates.</summary>
    public void SetSimulationRate(double rate)
    {
        if (_simulation is null || _dataMode != CommandInterfaceDataMode.Live)
        {
            return;
        }

        _rateController.SetRate(rate);
        if (rate > 0)
        {
            _lastRunningRate = rate;
        }

        SetMeta("simulation_rate", rate);
        _rateStatusLabel.Text = rate == 0 ? "RATE PAUSED" : $"RATE {rate:0.0#}x";
        _messageLabel.Text = rate == 0 ? "Simulation paused." : $"Simulation running at {rate:0.0#}x.";
        UpdateRateButtonStates();
    }

    /// <summary>Pauses or resumes the previously selected running rate.</summary>
    public void TogglePause()
    {
        if (_pauseButton.Disabled)
        {
            return;
        }

        SetSimulationRate(_rateController.Rate == 0 ? _lastRunningRate : 0);
    }

    /// <summary>Cycles through the supported running rates.</summary>
    public void CycleSimulationRate()
    {
        if (_pauseButton.Disabled)
        {
            return;
        }

        int current = Array.IndexOf(RunningRates, _rateController.Rate);
        SetSimulationRate(RunningRates[(current + 1 + RunningRates.Length) % RunningRates.Length]);
    }

    /// <summary>Saves the current simulation to the one application-owned quick-save slot.</summary>
    public void QuickSave()
    {
        if (_simulation is null || _quickSaveButton.Disabled || _dataMode != CommandInterfaceDataMode.Live)
        {
            return;
        }

        DateTimeOffset savedAtUtc = DateTimeOffset.UtcNow;
        DateTimeOffset createdAtUtc = _quickSaveCreatedAtUtc ?? savedAtUtc;
        var metadata = new GameSaveMetadata(QuickSaveId, QuickSaveDisplayName, createdAtUtc, savedAtUtc);

        try
        {
            GamePersistence.Save(_quickSavePath, _simulation, metadata);
            _quickSaveCreatedAtUtc = createdAtUtc;
            _messageLabel.Text = "Quick save complete.";
            SetMeta("quick_save_status", "saved");
            SetMeta("quick_save_created_at_utc", createdAtUtc.ToString("O"));
            SetMeta("quick_save_saved_at_utc", savedAtUtc.ToString("O"));
        }
        catch (Exception exception)
        {
            ReportPersistenceFailure("Quick save", "save_failed", "storage is unavailable", exception);
        }
    }

    /// <summary>Loads a new validated simulation from the quick-save slot.</summary>
    public void QuickLoad()
    {
        if (
            _simulation is null
            || _shipCatalog is null
            || _quickLoadButton.Disabled
            || _dataMode != CommandInterfaceDataMode.Live
        )
        {
            return;
        }

        try
        {
            string loadPath = ResolveQuickLoadPath();
            LoadedGameSave loaded = GamePersistence.Load(loadPath, _shipCatalog);

            // Core constructs and validates the candidate in isolation. Assignment stays after that
            // boundary so an unreadable or invalid save cannot damage the playable aggregate.
            _simulation = loaded.Simulation;
            _quickSaveCreatedAtUtc = loaded.Metadata.CreatedAtUtc;
            _recentEvents.Clear();
            ClearSelectedDestination();

            // Rate is a current player preference, so it survives load. Fractional carry is dropped
            // because presentation time accumulated before the snapshot must not advance restored truth.
            _rateController.ResetAccumulatedTime();
            RefreshProjection();
            _messageLabel.Text =
                $"Quick load restored time {loaded.Simulation.GetPlayerProjection().SimulationTime.Milliseconds / 1000.0:0.0} s.";
            SetMeta("quick_save_status", "loaded");
            CurrentWorkspaceButton().CallDeferred(Control.MethodName.GrabFocus);
        }
        catch (Exception exception)
        {
            ReportPersistenceFailure("Quick load", "load_failed", "save data is unavailable or invalid", exception);
        }
    }

    /// <summary>Selects a strategic destination by stable Core identifier.</summary>
    public void SelectDestination(string destinationId)
    {
        if (_simulation is null || _projection is null || _dataMode != CommandInterfaceDataMode.Live)
        {
            return;
        }

        var destination = new LocationId(destinationId);
        if (_projection.Strategic.Locations.All(location => location.Id != destination))
        {
            return;
        }

        OnDestinationSelected(destination);
    }

    /// <summary>Submits selected strategic travel through the typed Core command.</summary>
    public void RequestSelectedTravel()
    {
        if (
            _simulation is null
            || _dataMode != CommandInterfaceDataMode.Live
            || _travelButton.Disabled
            || _selectedDestination is not LocationId destination
        )
        {
            return;
        }

        try
        {
            TravelRequestResult result = _simulation.RequestTravel(new TravelIntent(destination));
            _messageLabel.Text = result.Outcome switch
            {
                TravelOutcome.Accepted => $"Travel engaged for {FindLocationName(destination)}.",
                TravelOutcome.AlreadyTraveling => "Travel unavailable: vessel is already underway.",
                TravelOutcome.SameLocation => "Travel unavailable: vessel is already at that location.",
                TravelOutcome.RouteUnavailable => "Travel unavailable: no direct route is known.",
                _ => "Travel request was not accepted.",
            };
            RefreshProjection();
        }
        catch (Exception exception)
        {
            ReportCommandFailure("Travel command failed safely.", exception);
        }
    }

    /// <summary>Advances through Core to the next player-relevant event boundary.</summary>
    public void AdvanceUntilNextPlayerRelevantEvent()
    {
        if (_simulation is null || _advanceUntilButton.Disabled || _dataMode != CommandInterfaceDataMode.Live)
        {
            return;
        }

        try
        {
            AdvanceUntilResult result = _simulation.AdvanceUntilNextPlayerRelevantEvent();
            if (result.ResolvedEvents.Contains(PlayerAdvanceEvent.TravelArrived))
            {
                ClearSelectedDestination();
            }

            PresentResolvedEvents(result.ResolvedEvents, announce: false);
            RefreshProjection();
            string resolved = DescribeAdvanceResult(result);
            _messageLabel.Text = resolved;
            SetMeta("advance_status", "advanced");
            SetMeta("last_advance_event", resolved);
        }
        catch (Exception exception)
        {
            ReportAdvanceFailure(exception);
        }
    }

    /// <summary>Submits a visible north-east tactical course through the typed Core command.</summary>
    public void SetDemonstrationCourse()
    {
        if (_simulation is null || _courseButton.Disabled || _dataMode != CommandInterfaceDataMode.Live)
        {
            return;
        }

        try
        {
            SetTacticalCourseResult result = _simulation.SetTacticalCourse(
                new SetTacticalCourseIntent(new HeadingDegrees(45), new SpeedKilometersPerSecond(2))
            );
            _messageLabel.Text = result.Outcome switch
            {
                SetTacticalCourseOutcome.Accepted => "Tactical course set: heading 045°, speed 2 km/s.",
                SetTacticalCourseOutcome.UnavailableWhileTraveling =>
                    "Course unavailable while strategic travel is active.",
                SetTacticalCourseOutcome.SpeedExceedsMaximum => "Course unavailable: requested speed exceeds limits.",
                _ => "Tactical course was not accepted.",
            };
            RefreshProjection();
        }
        catch (Exception exception)
        {
            ReportCommandFailure("Tactical command failed safely.", exception);
        }
    }

    /// <summary>Shows the live strategic route projection in the persistent Command Deck.</summary>
    public void ShowStrategicView()
    {
        _commandMode = CommandInterfaceMode.Travel;
        ActivateLiveWorkspace(engineering: false);
    }

    /// <summary>Shows the live tactical motion projection in the persistent Command Deck.</summary>
    public void ShowTacticalView()
    {
        _commandMode = CommandInterfaceMode.Combat;
        ActivateLiveWorkspace(engineering: false);
    }

    /// <summary>Shows the live engineering projection without replacing the running simulation.</summary>
    public void ShowEngineeringWorkspace()
    {
        ActivateLiveWorkspace(engineering: true);
    }

    /// <summary>Returns to the last live Command Deck context without replacing the running simulation.</summary>
    public void ShowCommandWorkspace()
    {
        ActivateLiveWorkspace(engineering: false);
    }

    /// <summary>Displays an approved deterministic preview through the instantiated production workspace.</summary>
    public void ShowPreview(CommandInterfaceDataMode dataMode)
    {
        if (dataMode == CommandInterfaceDataMode.Live)
        {
            throw new ArgumentException(
                "Use RestoreLiveMode to return to authoritative presentation.",
                nameof(dataMode)
            );
        }

        CommandInterfacePresentation presentation = CommandInterfacePreviewFixtures.Create(dataMode);
        _dataMode = dataMode;
        _engineeringWorkspaceActive = presentation.Mode == CommandInterfaceMode.Engineering;
        if (!_engineeringWorkspaceActive)
        {
            _commandMode = presentation.Mode;
        }

        SetWorkspaceVisibility();
        PresentWorkspace(presentation);
        PresentShell(presentation);
        _messageLabel.Text = "Illustrative preview — no command can change the running simulation.";
        FocusCurrentWorkspace();
    }

    /// <summary>Restores the authoritative projection for the currently selected workspace.</summary>
    public void RestoreLiveMode()
    {
        ActivateLiveWorkspace(_engineeringWorkspaceActive);
    }

    /// <summary>Maps a continuous Core tactical position for integration verification.</summary>
    public Vector2 MapTacticalPosition(double xKilometers, double yKilometers) =>
        _commandDeck.TacticalMap.MapPosition(xKilometers, yKilometers);

    private void BindScene()
    {
        _commandDeck = GetNode<CommandDeckWorkspace>("%CommandDeckWorkspace");
        _engineering = GetNode<EngineeringWorkspace>("%EngineeringWorkspace");
        _eventLogHeading = GetNode<Label>("%EventLogHeading");
        _eventLog = GetNode<VBoxContainer>("%EventLogContent");
        _captainActions = GetNode<VBoxContainer>("%CaptainActions");
        _engineeringBottomActions = GetNode<VBoxContainer>("%EngineeringBottomActions");
        _engineeringQueue = GetNode<VBoxContainer>("%EngineeringQueueContent");
        _engineeringQueueActions = GetNode<VBoxContainer>("%EngineeringQueueActions");
        _timeLabel = GetNode<Label>("%SimulationTime");
        _vesselStatusLabel = GetNode<Label>("%VesselStatus");
        _rateStatusLabel = GetNode<Label>("%RateStatus");
        _viewStatusLabel = GetNode<Label>("%ViewStatus");
        _alertStatusLabel = GetNode<Label>("%AlertStatus");
        _messageLabel = GetNode<Label>("%Message");
        BindButtons();

        _commandDeck.DestinationSelected += OnWorkspaceDestinationSelected;
        _commandDeck.PresentationActionRequested += OnPresentationActionRequested;
        _engineering.EngineeringCommandRequested += OnEngineeringCommandRequested;
        _travelButton.Pressed += RequestSelectedTravel;
        _courseButton.Pressed += SetDemonstrationCourse;
        _advanceUntilButton.Pressed += AdvanceUntilNextPlayerRelevantEvent;
        _strategicButton.Pressed += ShowStrategicView;
        _tacticalButton.Pressed += ShowTacticalView;
        _commandStationButton.Pressed += ShowCommandWorkspace;
        _engineeringStationButton.Pressed += ShowEngineeringWorkspace;
        _engineeringBottomReturnButton.Pressed += ShowCommandWorkspace;
        _quickSaveButton.Pressed += QuickSave;
        _quickLoadButton.Pressed += QuickLoad;
        _pauseButton.Pressed += TogglePause;
        ConfigureRateButton(_halfRateButton, 0.5);
        ConfigureRateButton(_normalRateButton, 1);
        ConfigureRateButton(_doubleRateButton, 2);
        ConfigureRateButton(_quadRateButton, 4);
        SetWorkspaceVisibility();
    }

    private void BindButtons()
    {
        _travelButton = GetNode<Button>("%TravelButton");
        _courseButton = GetNode<Button>("%CourseButton");
        _advanceUntilButton = GetNode<Button>("%AdvanceUntilButton");
        _strategicButton = GetNode<Button>("%StrategicButton");
        _tacticalButton = GetNode<Button>("%TacticalButton");
        _commandStationButton = GetNode<Button>("%CommandStationButton");
        _engineeringStationButton = GetNode<Button>("%EngineeringStationButton");
        _engineeringBottomReturnButton = GetNode<Button>("%EngineeringBottomReturnButton");
        _quickSaveButton = GetNode<Button>("%QuickSaveButton");
        _quickLoadButton = GetNode<Button>("%QuickLoadButton");
        _pauseButton = GetNode<Button>("%PauseRate");
        _halfRateButton = GetNode<Button>("%HalfRate");
        _normalRateButton = GetNode<Button>("%NormalRate");
        _doubleRateButton = GetNode<Button>("%DoubleRate");
        _quadRateButton = GetNode<Button>("%QuadRate");
    }

    private (ShipDefinitionCatalog Catalog, GameSimulation Simulation) CreateSimulationFromCanonicalContent()
    {
        string schema = ReadRequiredText(ShipSchemaResourcePath);
        string definitionJson = ReadRequiredText(ShipDefinitionResourcePath);
        var loader = new ShipDefinitionCatalogLoader(schema);
        ShipDefinitionCatalog catalog = loader.LoadCatalog([
            ShipDefinitionContent.FromText(ShipDefinitionResourcePath, definitionJson),
        ]);
        return (catalog, FirstGameSetup.Create(catalog));
    }

    private static string ReadRequiredText(string path)
    {
        using var file = GodotFile.Open(path, GodotFile.ModeFlags.Read);
        if (file is null)
        {
            throw new InvalidOperationException($"Godot could not open required file '{path}'.");
        }

        string text = file.GetAsText();
        if (file.GetError() != Error.Ok)
        {
            throw new InvalidOperationException($"Godot could not read required file '{path}'.");
        }

        return text;
    }

    private static string ResolveQuickSavePath(string userPath)
    {
        if (!userPath.StartsWith("user://", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Quick-save storage must use Godot's user:// boundary.");
        }

        string userRoot = Path.GetFullPath(ProjectSettings.GlobalizePath("user://"));
        string resolvedPath = Path.GetFullPath(ProjectSettings.GlobalizePath(userPath));
        string relativePath = Path.GetRelativePath(userRoot, resolvedPath);
        if (
            Path.IsPathRooted(relativePath)
            || relativePath.Equals("..", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException("Quick-save storage must remain inside Godot's user:// boundary.");
        }

        return resolvedPath;
    }

    private string ResolveQuickLoadPath()
    {
        if (!_quickSaveUsesDefaultPath || File.Exists(_quickSavePath))
        {
            return _quickSavePath;
        }

        return ResolveQuickSavePath(LegacyDefaultQuickSaveUserPath);
    }

    private void ConfigureRateButton(Button button, double rate)
    {
        button.Pressed += () => SetSimulationRate(rate);
    }

    private void ActivateLiveWorkspace(bool engineering)
    {
        _dataMode = CommandInterfaceDataMode.Live;
        _engineeringWorkspaceActive = engineering;
        SetWorkspaceVisibility();
        if (_simulation is not null)
        {
            RefreshProjection();
        }

        FocusCurrentWorkspace();
    }

    private void SetWorkspaceVisibility()
    {
        _commandDeck.Visible = !_engineeringWorkspaceActive;
        _engineering.Visible = _engineeringWorkspaceActive;
    }

    private void RefreshProjection()
    {
        _projection = _simulation!.GetPlayerProjection();
        CommandInterfaceMode mode = _engineeringWorkspaceActive ? CommandInterfaceMode.Engineering : _commandMode;
        CommandInterfacePresentation presentation = CommandInterfacePresenter.PresentLive(
            _projection,
            _selectedDestination,
            _recentEvents,
            mode
        );
        PresentWorkspace(presentation);
        PresentShell(presentation);
        SetProjectionMetadata(_projection);
    }

    private void PresentWorkspace(CommandInterfacePresentation presentation)
    {
        if (presentation.Mode == CommandInterfaceMode.Engineering)
        {
            _engineering.Present(presentation);
        }
        else
        {
            _commandDeck.Present(presentation);
        }
    }

    private void PresentShell(CommandInterfacePresentation presentation)
    {
        bool live = presentation.DataMode == CommandInterfaceDataMode.Live && _simulation is not null;
        string activeView = presentation.Mode switch
        {
            CommandInterfaceMode.Travel => "strategic",
            CommandInterfaceMode.Combat => "tactical",
            CommandInterfaceMode.Engineering => "engineering",
            _ => "unavailable",
        };
        SetMeta("data_mode", presentation.DataMode.ToString());
        SetMeta("active_workspace", presentation.Mode == CommandInterfaceMode.Engineering ? "engineering" : "command");
        SetMeta("active_view", activeView);
        SetMeta("simulation_identity", SimulationIdentity);

        if (live)
        {
            _vesselStatusLabel.Text = $"VESSEL {_projection!.Ship.DisplayName}";
            _timeLabel.Text = $"TIME {_projection.SimulationTime.Milliseconds / 1000.0:0.0} s";
            _rateStatusLabel.Text = _rateController.Rate == 0 ? "RATE PAUSED" : $"RATE {_rateController.Rate:0.0#}x";
            _alertStatusLabel.Text = "ALERT UNAVAILABLE";
        }
        else
        {
            _vesselStatusLabel.Text = $"PREVIEW / {HeaderValue(presentation, "VESSEL")}";
            _timeLabel.Text = $"PREVIEW / {HeaderValue(presentation, "CLOCK", "TIME UNAVAILABLE")}";
            _rateStatusLabel.Text = "RATE FROZEN / PREVIEW";
            _alertStatusLabel.Text = HeaderValue(presentation, "ALERT", "ALERT UNAVAILABLE");
        }

        _viewStatusLabel.Text = presentation.Mode switch
        {
            CommandInterfaceMode.Travel => "COMMAND DECK / TRAVEL",
            CommandInterfaceMode.Combat => "COMMAND DECK / COMBAT",
            CommandInterfaceMode.Engineering => "ENGINEERING WORKSPACE",
            _ => "WORKSPACE UNAVAILABLE",
        };
        RenderBottomArea(presentation);
        UpdateStationButtons(presentation);
        UpdateContextControls(presentation, live);
        UpdateFocusTraversal();
    }

    private void RenderEventLog(IReadOnlyList<CommandInterfaceEventRow> events)
    {
        ClearChildren(_eventLog);
        if (events.Count == 0)
        {
            _eventLog.AddChild(new Label { Text = "NO PLAYER-RESOLVED EVENTS", ThemeTypeVariation = "MutedTelemetry" });
            return;
        }

        foreach (CommandInterfaceEventRow row in events)
        {
            _eventLog.AddChild(
                new Label
                {
                    Text = $"{row.Time}  {row.Source}  {row.Message}",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    ThemeTypeVariation = EventVariation(row.Tone),
                }
            );
        }
    }

    private void RenderBottomArea(CommandInterfacePresentation presentation)
    {
        bool engineering = presentation.Mode == CommandInterfaceMode.Engineering;
        _captainActions.Visible = !engineering;
        _engineeringBottomActions.Visible = engineering;
        _eventLogHeading.Text = engineering ? "ENGINEERING EVENT LOG" : "EVENT / ORDER LOG";
        SetMeta("bottom_area_mode", engineering ? "engineering" : "command");
        RenderEventLog(presentation.Events);

        if (!engineering)
        {
            ClearChildren(_engineeringQueue);
            ClearChildren(_engineeringQueueActions);
            return;
        }

        RenderEngineeringQueue(presentation.Engineering?.Queue ?? []);
        RenderEngineeringQueueActions(presentation.Actions);
    }

    private void RenderEngineeringQueue(IReadOnlyList<CommandInterfaceQueueRow> queue)
    {
        ClearChildren(_engineeringQueue);
        if (queue.Count == 0)
        {
            _engineeringQueue.AddChild(
                new Label { Text = "REPAIR QUEUE UNAVAILABLE", ThemeTypeVariation = "MutedTelemetry" }
            );
            return;
        }

        foreach (CommandInterfaceQueueRow row in queue)
        {
            _engineeringQueue.AddChild(
                new Label
                {
                    Text = $"{row.Priority}  {row.Label}  {DisplayQueueEstimate(row.Estimate)}",
                    AutowrapMode = TextServer.AutowrapMode.WordSmart,
                    ThemeTypeVariation = EventVariation(row.Tone),
                }
            );
        }
    }

    private void RenderEngineeringQueueActions(IReadOnlyList<CommandInterfaceAction> actions)
    {
        ClearChildren(_engineeringQueueActions);
        foreach (
            CommandInterfaceAction action in actions.Where(action =>
                string.Equals(action.Id, "reorder-repairs", StringComparison.Ordinal)
            )
        )
        {
            _engineeringQueueActions.AddChild(
                new Button
                {
                    Name = $"BottomAction_{action.Id.Replace('-', '_')}",
                    Text = $"{action.Label} [PREVIEW ONLY]",
                    Disabled = true,
                    FocusMode = FocusModeEnum.All,
                    ThemeTypeVariation = "CommandButton",
                    TooltipText = "Illustrative queue control; no simulation command will be submitted.",
                }
            );
        }
    }

    private void UpdateStationButtons(CommandInterfacePresentation presentation)
    {
        CommandInterfaceStation command = presentation.Stations.Single(station =>
            string.Equals(station.Id, "command", StringComparison.Ordinal)
        );
        CommandInterfaceStation engineering = presentation.Stations.Single(station =>
            string.Equals(station.Id, "engineering", StringComparison.Ordinal)
        );
        _commandStationButton.Text = StationText(command);
        _engineeringStationButton.Text = StationText(engineering);
        _commandStationButton.ButtonPressed = presentation.Mode != CommandInterfaceMode.Engineering;
        _engineeringStationButton.ButtonPressed = presentation.Mode == CommandInterfaceMode.Engineering;
        _commandStationButton.ThemeTypeVariation = _commandStationButton.ButtonPressed
            ? "StationTabActive"
            : "StationTab";
        _engineeringStationButton.ThemeTypeVariation = _engineeringStationButton.ButtonPressed
            ? "StationTabActive"
            : "StationTab";
        bool workspaceNavigationAvailable = _simulation is not null;
        _commandStationButton.Disabled = !workspaceNavigationAvailable;
        _engineeringStationButton.Disabled = !workspaceNavigationAvailable;
    }

    private void UpdateContextControls(CommandInterfacePresentation presentation, bool live)
    {
        bool travel = presentation.Mode == CommandInterfaceMode.Travel;
        bool combat = presentation.Mode == CommandInterfaceMode.Combat;
        _strategicButton.ButtonPressed = travel;
        _tacticalButton.ButtonPressed = combat;
        bool workspaceNavigationAvailable = _simulation is not null;
        _strategicButton.Disabled = !workspaceNavigationAvailable;
        _tacticalButton.Disabled = !workspaceNavigationAvailable;
        _travelButton.Visible = travel;
        _courseButton.Visible = combat;
        _travelButton.Disabled = !live || !IsSubmittable(presentation, "travel");
        _courseButton.Disabled = !live || !IsSubmittable(presentation, "set-tactical-course");
        _advanceUntilButton.Disabled = !live || !IsSubmittable(presentation, "advance-time");
        _quickSaveButton.Disabled = !live;
        _quickLoadButton.Disabled = !live;
        foreach (Button button in GetNode<HBoxContainer>("%RateControls").GetChildren().OfType<Button>())
        {
            button.Disabled = !live;
        }

        _travelButton.TooltipText = _travelButton.Disabled
            ? "Travel is unavailable until a live destination is selected."
            : $"Submit travel intent to {FindLocationName(_selectedDestination!.Value)}. Shortcut: E.";
        _courseButton.TooltipText = _courseButton.Disabled
            ? "Course changes are unavailable during strategic travel or preview."
            : "Submit heading 045° and speed 2 km/s. Shortcut: C.";
    }

    private void OnWorkspaceDestinationSelected(object? sender, CommandDeckWorkspace.DestinationEventArgs args)
    {
        if (_dataMode == CommandInterfaceDataMode.Live)
        {
            OnDestinationSelected(args.LocationId);
        }
    }

    private void OnDestinationSelected(LocationId destination)
    {
        if (_projection is null || _dataMode != CommandInterfaceDataMode.Live)
        {
            return;
        }

        _selectedDestination = destination;
        SetMeta("selected_destination", destination.Value);
        RefreshProjection();
        _messageLabel.Text = $"Selected destination: {FindLocationName(destination)}.";
    }

    private void OnPresentationActionRequested(object? sender, CommandDeckWorkspace.ActionEventArgs args)
    {
        if (_dataMode != CommandInterfaceDataMode.Live || args.Action.Intent is not CommandInterfaceIntent intent)
        {
            return;
        }

        SubmitIntent(intent);
    }

    private void OnEngineeringCommandRequested(
        object? sender,
        EngineeringWorkspace.EngineeringCommandRequestedEventArgs args
    )
    {
        if (_dataMode == CommandInterfaceDataMode.Live)
        {
            SubmitIntent(args.Intent);
        }
    }

    private void SubmitIntent(CommandInterfaceIntent intent)
    {
        switch (intent)
        {
            case CommandInterfaceIntent.Travel:
                RequestSelectedTravel();
                break;
            case CommandInterfaceIntent.SetTacticalCourse:
                SetDemonstrationCourse();
                break;
            case CommandInterfaceIntent.AdvanceTime:
                AdvanceUntilNextPlayerRelevantEvent();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown command-interface intent.");
        }
    }

    private string FindLocationName(LocationId id) =>
        _projection!.Strategic.Locations.Single(location => location.Id == id).DisplayName;

    private void SetProjectionMetadata(PlayerProjection projection)
    {
        SetMeta("simulation_time_milliseconds", projection.SimulationTime.Milliseconds);
        SetMeta("ship_name", projection.Ship.DisplayName);
        SetMeta("sensor_integrity", projection.Ship.Sensors.Integrity);
        SetMeta("sensor_repair_progress", projection.Ship.Sensors.RepairProgress);
        SetMeta("sensor_repairing", projection.Ship.Sensors.IsRepairing);
        SetMeta("map_location_count", projection.Strategic.Locations.Count);
        SetMeta("map_route_count", projection.Strategic.Routes.Count);
        SetMeta("travel_active", projection.Strategic.Travel is not null);
        SetMeta("travel_origin", projection.Strategic.Travel?.Origin.Value ?? string.Empty);
        SetMeta("travel_destination", projection.Strategic.Travel?.Destination.Value ?? string.Empty);
        SetMeta("travel_eta_milliseconds", projection.Strategic.Travel?.ExpectedArrival.Milliseconds ?? -1);
        SetMeta("tactical_x", projection.Ship.Tactical.Position.XKilometers);
        SetMeta("tactical_y", projection.Ship.Tactical.Position.YKilometers);
        SetMeta("tactical_heading", projection.Ship.Tactical.HeadingDegrees);
        SetMeta("tactical_speed", projection.Ship.Tactical.SpeedKilometersPerSecond);
    }

    private void ClearSelectedDestination()
    {
        _selectedDestination = null;
        SetMeta("selected_destination", string.Empty);
    }

    private void SetGameplayEnabled(bool enabled)
    {
        foreach (
            Button button in new[]
            {
                _travelButton,
                _courseButton,
                _advanceUntilButton,
                _quickSaveButton,
                _quickLoadButton,
                _strategicButton,
                _tacticalButton,
                _commandStationButton,
                _engineeringStationButton,
                _engineeringBottomReturnButton,
                _pauseButton,
                _halfRateButton,
                _normalRateButton,
                _doubleRateButton,
                _quadRateButton,
            }
        )
        {
            button.Disabled = !enabled;
            button.ButtonPressed = false;
        }

        UpdateFocusTraversal();
    }

    private void UpdateRateButtonStates()
    {
        _pauseButton.ButtonPressed = _rateController.Rate == 0;
        _halfRateButton.ButtonPressed = _rateController.Rate == 0.5;
        _normalRateButton.ButtonPressed = _rateController.Rate == 1;
        _doubleRateButton.ButtonPressed = _rateController.Rate == 2;
        _quadRateButton.ButtonPressed = _rateController.Rate == 4;
    }

    private void UpdateFocusTraversal()
    {
        if (!IsInsideTree())
        {
            return;
        }

        var controls = new List<Control>();
        if (_engineeringWorkspaceActive)
        {
            controls.Add(_commandStationButton);
            controls.Add(_engineeringStationButton);
            controls.AddRange(_engineering.GetVisibleFocusControls());
            controls.Add(_engineeringBottomReturnButton);
        }
        else
        {
            controls.AddRange(
                new Button[]
                {
                    _commandStationButton,
                    _engineeringStationButton,
                    _strategicButton,
                    _tacticalButton,
                    _travelButton,
                    _courseButton,
                    _pauseButton,
                    _halfRateButton,
                    _normalRateButton,
                    _doubleRateButton,
                    _quadRateButton,
                    _advanceUntilButton,
                    _quickSaveButton,
                    _quickLoadButton,
                }
            );
        }

        controls = controls.Where(IsFocusable).ToList();
        if (controls.Count == 0)
        {
            return;
        }

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

    private static bool IsFocusable(Control control) =>
        control.IsVisibleInTree()
        && control.FocusMode != FocusModeEnum.None
        && (control is not BaseButton button || !button.Disabled);

    private Button CurrentWorkspaceButton() =>
        _engineeringWorkspaceActive ? _engineeringStationButton : _commandStationButton;

    private void FocusCurrentWorkspace()
    {
        if (_engineeringWorkspaceActive)
        {
            _engineering.GrabEntryFocus();
        }
        else
        {
            _commandStationButton.CallDeferred(Control.MethodName.GrabFocus);
        }
    }

    private static string DisplayQueueEstimate(CommandInterfaceField estimate) =>
        estimate.Availability == CommandInterfaceAvailability.Available ? estimate.Value : "UNAVAILABLE";

    private static bool IsSubmittable(CommandInterfacePresentation presentation, string actionId) =>
        presentation.Actions.Any(action =>
            string.Equals(action.Id, actionId, StringComparison.Ordinal)
            && action.Availability == CommandInterfaceActionAvailability.Submittable
            && action.Intent is not null
        );

    private static string HeaderValue(
        CommandInterfacePresentation presentation,
        string label,
        string fallback = "UNAVAILABLE"
    ) =>
        presentation.Header.FirstOrDefault(field => string.Equals(field.Label, label, StringComparison.Ordinal))
            is CommandInterfaceField field
            ? field.Value
            : fallback;

    private static string StationText(CommandInterfaceStation station) =>
        station.AttentionCount > 0 ? $"{station.Label} [{station.AttentionCount} !]" : station.Label;

    private static StringName EventVariation(CommandInterfaceTone tone) =>
        tone switch
        {
            CommandInterfaceTone.Critical => "StatusCritical",
            CommandInterfaceTone.Caution or CommandInterfaceTone.Engineering => "StatusCaution",
            CommandInterfaceTone.Nominal => "StatusNominal",
            CommandInterfaceTone.Muted => "MutedTelemetry",
            _ => "TelemetryValue",
        };

    private static void ClearChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren())
        {
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static string DescribeAdvanceResult(AdvanceUntilResult result)
    {
        if (result.ResolvedEvents.Count == 0)
        {
            return "No pending player event to advance to.";
        }

        return $"Advanced to: {string.Join(", ", result.ResolvedEvents.Select(DescribePlayerEvent))}.";
    }

    private void PresentResolvedEvents(IReadOnlyList<PlayerAdvanceEvent> events, bool announce)
    {
        if (events.Count == 0)
        {
            return;
        }

        _recentEvents.AddRange(events);
        string description = string.Join(", ", events.Select(DescribePlayerEvent));
        SetMeta("last_advance_event", description);
        if (announce)
        {
            _messageLabel.Text = description;
        }
    }

    private static string DescribePlayerEvent(PlayerAdvanceEvent @event) =>
        @event switch
        {
            PlayerAdvanceEvent.TravelArrived => "arrival complete",
            PlayerAdvanceEvent.SensorRepairCompleted => "sensor repair complete",
            _ => "player event complete",
        };

    private void ReportPersistenceFailure(string operation, string status, string category, Exception exception)
    {
        _messageLabel.Text = $"{operation} failed: {category}.";
        SetMeta("quick_save_status", status);
        LogDiagnostic($"{operation} failed", exception);
    }

    private void ReportCommandFailure(string playerMessage, Exception exception)
    {
        _messageLabel.Text = playerMessage;
        LogDiagnostic(playerMessage, exception);
    }

    private void ReportAdvanceFailure(Exception exception)
    {
        _rateController.SetRate(0);
        SetMeta("simulation_rate", 0);
        UpdateRateButtonStates();
        _rateStatusLabel.Text = "RATE PAUSED";
        _messageLabel.Text = "Time advancement failed safely; simulation is paused.";
        SetMeta("advance_status", "failed");
        LogDiagnostic("Simulation advancement failed", exception);
    }

    private static void LogDiagnostic(string operation, Exception exception)
    {
        GD.PrintErr($"{operation}: {exception.GetType().Name}: {exception.Message}");
    }
}
