using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Identity;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Strategic;
using AlterCourse.Godot.Gameplay.Logging;
using Godot;
using Json.Schema;
using Serilog.Events;

namespace AlterCourse.Godot.Gameplay;

/// <summary>
/// Owns one scene-lifetime simulation and adapts its player-known projection to persistent command workspaces.
/// </summary>
public partial class GameScreen : Control
{
    private const string SystemSchemaPath = "res://content/schemas/system-definition-v1.schema.json";
    private const string SystemsPath = "res://content/systems/pathfinder-systems.json";
    private const string SchemaPath = "res://content/schemas/ship-definition-v6.schema.json";
    private const string ShipPath = "res://content/ships/pathfinder.json";
    private const string FactionSchemaPath = "res://content/schemas/faction-definition-v1.schema.json";
    private const string FactionAPath = "res://content/factions/faction-a.json";
    private const string FactionBPath = "res://content/factions/faction-b.json";
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
    private const int RecentActivityLimit = 64;

    // The committed system/ship/faction schemas are 5,014/1,525/544 bytes. 16 KiB admits more than three times
    // the largest current schema while bounding the engine read and strict text decode independently of definitions.
    internal const int MaximumSchemaBytes = 16 * 1024;
    internal const int ContentReadChunkBytes = 8192;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    private static readonly double[] RunningRates = [0.5, 1, 2, 4];

    private readonly SimulationRateController _rateController = new();
    private readonly List<CommandInterfacePresenter.ActivityEvent> _recentActivity = [];
    private GameSimulation? _simulation;
    private GameplayLogging? _logging;

    // Incremented on every assignment or clearing of _simulation (bootstrap, failed bootstrap, quick-load), never by
    // an ordinary refresh. Own-ship actions and deferred callbacks capture it so nothing presented or queued for one
    // simulation can act on its replacement; see OwnShipActionBinding.
    private long _simulationGeneration;
    private ShipDefinitionCatalog? _shipCatalog;
    private FactionDefinitionCatalog? _factionCatalog;
    private PlayerProjection? _projection;

    // Installed identities whose engineering_*_<id> test hooks are currently published, so a load that drops an
    // installation also drops its hooks instead of leaving the replaced world's values readable.
    private HashSet<long> _publishedInstallationMeta = [];
    private LocationId? _selectedDestination;
    private SensorContactId? _selectedContact;
    private DateTimeOffset? _quickSaveCreatedAtUtc;
    private string _quickSavePath = null!;
    private bool _quickSaveUsesDefaultPath;
    private bool _engineeringWorkspaceActive;
    private double _lastRunningRate = 1;
    private CommandInterfaceMode _commandMode = CommandInterfaceMode.Travel;
    private CommandInterfaceDataMode _dataMode = CommandInterfaceDataMode.Live;
    private CommandDeckWorkspace _commandDeck = null!;
    private EngineeringWorkspace _engineering = null!;
    private SpinBox _courseHeading = null!;
    private SpinBox _courseSpeed = null!;
    private Button _stopCourseButton = null!;
    private HBoxContainer _courseInputs = null!;
    private Label _courseCapability = null!;
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

    /// <summary>Gets or sets whether development diagnostics include actor-safe decision candidates and constraints.</summary>
    [Export]
    public bool EnableDecisionTraceDiagnostics { get; set; }

    /// <summary>Gets whether canonical content produced a complete playable simulation.</summary>
    public bool IsGameplayReady => _simulation is not null;

    /// <summary>Gets a process-local identity used to prove workspace switches retain the same simulation.</summary>
    public int SimulationIdentity => _simulation is null ? 0 : RuntimeHelpers.GetHashCode(_simulation);

    /// <summary>Gets or sets the Godot user-data path for the one quick-save slot.</summary>
    [Export]
    public string QuickSaveUserPath { get; set; } = DefaultQuickSaveUserPath;

    /// <summary>Gets or sets the canonical system-definition schema resource, loaded before any ship content.</summary>
    [Export]
    public string SystemDefinitionSchemaResourcePath { get; set; } = SystemSchemaPath;

    /// <summary>Gets or sets the canonical system-definition resource that ship loadouts reference.</summary>
    [Export]
    public string SystemDefinitionResourcePath { get; set; } = SystemsPath;

    /// <summary>Gets or sets the canonical ship schema resource used during bootstrap.</summary>
    [Export]
    public string ShipSchemaResourcePath { get; set; } = SchemaPath;

    /// <summary>Gets or sets the canonical player-ship definition resource used during bootstrap.</summary>
    [Export]
    public string ShipDefinitionResourcePath { get; set; } = ShipPath;

    internal Func<string, IContentFileAccess?> ContentFileOpener { get; set; } = GodotContentFileAccess.Open;

    /// <summary>Gets the latest fresh player-known projection.</summary>
    public PlayerProjection? Projection => _projection;

    /// <inheritdoc />
    public override void _Ready()
    {
        _logging = GameplayLogging.Create(
            () => ProjectSettings.GlobalizePath("user://logs"),
            () => GD.PrintErr("Gameplay diagnostics unavailable."),
            minimumLevel: EnableDecisionTraceDiagnostics ? LogEventLevel.Debug : LogEventLevel.Information
        );
        _logging.Diagnostics.Lifecycle(true, null, null);
        BindScene();
        try
        {
            string quickSavePath = ResolveQuickSavePath(QuickSaveUserPath);
            bool quickSaveUsesDefaultPath = string.Equals(
                QuickSaveUserPath,
                DefaultQuickSaveUserPath,
                StringComparison.Ordinal
            );
            (ShipDefinitionCatalog shipCatalog, FactionDefinitionCatalog factionCatalog, GameSimulation simulation) =
                CreateSimulationFromCanonicalContent();
            _quickSavePath = quickSavePath;
            _quickSaveUsesDefaultPath = quickSaveUsesDefaultPath;
            _shipCatalog = shipCatalog;
            _factionCatalog = factionCatalog;
            _simulation = simulation;
            _simulationGeneration++;
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
            _simulationGeneration++;
            _shipCatalog = null;
            _factionCatalog = null;
            _projection = null;
            _selectedDestination = null;
            _selectedContact = null;
            SetGameplayEnabled(false);
            _messageLabel.Text = "Gameplay content is unavailable. Check the local installation and restart.";
            SetMeta("load_error", _messageLabel.Text);
            LogDiagnostic(GameDiagnostics.FailureOperation.Content, exception);
        }
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        ProcessSyntheticDelta(delta);
    }

    /// <inheritdoc />
    public override void _Input(InputEvent @event)
    {
        if (GetViewport().GuiGetFocusOwner() is LineEdit)
            return;
        if (!@event.IsAction(ActionTogglePause))
        {
            return;
        }

        if (@event.IsActionPressed(ActionTogglePause))
        {
            TogglePause();
        }

        GetViewport().SetInputAsHandled();
    }

    /// <inheritdoc />
    public override void _UnhandledInput(InputEvent @event)
    {
        if (GetViewport().GuiGetFocusOwner() is LineEdit)
            return;
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
            PresentOperationResult(() =>
            {
                if (result.ResolvedEvents.Any(@event => @event.Kind == PlayerAdvanceEventKind.TravelArrived))
                {
                    ClearSelectedDestination();
                }

                PresentResolvedEvents(result.ResolvedEvents, announce: false);
                RefreshProjection();
            });
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
            SetMeta("quick_save_status", "saved");
            _logging?.Diagnostics.PersistenceCompleted(
                false,
                _projection?.SimulationTime.Milliseconds,
                _projection?.Ship.InstanceId.Value
            );
            SetMeta("quick_save_created_at_utc", createdAtUtc.ToString("O"));
            SetMeta("quick_save_saved_at_utc", savedAtUtc.ToString("O"));
            PresentOperationResult(() => _messageLabel.Text = "Quick save complete.");
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
            || _factionCatalog is null
            || _quickLoadButton.Disabled
            || _dataMode != CommandInterfaceDataMode.Live
        )
        {
            return;
        }

        try
        {
            string loadPath = ResolveQuickLoadPath();
            LoadedGameSave loaded = GamePersistence.Load(
                loadPath,
                _shipCatalog,
                _factionCatalog,
                _logging?.SimulationLogger
            );

            // Core constructs and validates the candidate in isolation. Assignment stays after that
            // boundary so an unreadable or invalid save cannot damage the playable aggregate.
            _simulation = loaded.Simulation;
            _simulationGeneration++;
            _quickSaveCreatedAtUtc = loaded.Metadata.CreatedAtUtc;
            SetMeta("quick_save_status", "loaded");
            // Rate is a current player preference, so it survives load. Fractional carry is dropped
            // because presentation time accumulated before the snapshot must not advance restored truth.
            _rateController.ResetAccumulatedTime();
            PlayerProjection restored = loaded.Simulation.GetPlayerProjection();
            _logging?.Diagnostics.PersistenceCompleted(
                true,
                restored.SimulationTime.Milliseconds,
                restored.Ship.InstanceId.Value
            );
            PresentOperationResult(() =>
            {
                _recentActivity.Clear();
                ClearSelectedDestination();
                ClearSelectedContact();

                ResetCourseDraft(restored);
                _messageLabel.Text = $"Quick load restored time {restored.SimulationTime.Milliseconds / 1000.0:0.0} s.";
                RefreshProjection();
                DeferFocus(CurrentWorkspaceButton());
            });
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

    /// <summary>Selects an observer-local contact through the GDScript integration boundary.</summary>
    public void SelectContact(long contactId)
    {
        if (_projection is null || _dataMode != CommandInterfaceDataMode.Live)
        {
            return;
        }

        var selected = new SensorContactId(contactId);
        if (
            _projection.Ship.Sensors.Contacts.All(contact =>
                contact.Id != selected || contact.Status == SensorContactStatus.Lost
            )
        )
        {
            return;
        }

        OnContactSelected(selected);
    }

    /// <summary>Requests an active scan of the selected observer-local contact.</summary>
    public void RequestSelectedActiveScan()
    {
        if (_selectedContact is SensorContactId contactId)
        {
            RequestActiveScan(contactId);
        }
    }

    /// <summary>Requests a bounded hail to the selected observer-local contact.</summary>
    public void RequestSelectedHail()
    {
        if (_selectedContact is SensorContactId contactId)
        {
            RequestHail(contactId);
        }
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
            PresentOperationResult(() =>
            {
                _messageLabel.Text = result.Outcome switch
                {
                    TravelOutcome.Accepted => $"Travel engaged for {FindLocationName(destination)}.",
                    TravelOutcome.AlreadyTraveling => "Travel unavailable: vessel is already underway.",
                    TravelOutcome.SameLocation => "Travel unavailable: vessel is already at that location.",
                    TravelOutcome.RouteUnavailable => "Travel unavailable: no direct route is known.",
                    _ => "Travel request was not accepted.",
                };
                PresentResolvedEvents(result.ResolvedEvents, announce: false);
                RefreshProjection();
            });
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
            SetMeta("advance_status", "advanced");
            PresentOperationResult(() =>
            {
                if (result.ResolvedEvents.Any(@event => @event.Kind == PlayerAdvanceEventKind.TravelArrived))
                {
                    ClearSelectedDestination();
                }

                PresentResolvedEvents(result.ResolvedEvents, announce: false);
                string resolved = DescribeAdvanceResult(result);
                SetMeta("last_advance_event", resolved);
                _messageLabel.Text = resolved;
                RefreshProjection();
            });
        }
        catch (Exception exception)
        {
            ReportAdvanceFailure(exception);
        }
    }

    /// <summary>Submits the visible course draft through the typed Core command.</summary>
    public void SetDemonstrationCourse()
    {
        SubmitCourse(_courseHeading.Value, _courseSpeed.Value);
    }

    // The persistent course inputs are the one shell draft that outlives a world replacement: the travel destination
    // and contact selection are cleared by QuickLoad, and every other shell button reads the current projection when
    // pressed. A heading and speed typed for the pre-load ship would otherwise be submitted, unreviewed, to whatever
    // ship the loaded save makes the player's, so the draft is re-derived from the loaded ship's current motion.
    // Pressing Set Course immediately afterwards therefore re-commands the loaded course and changes nothing. An
    // ordinary refresh never calls this, so a draft being edited survives the running simulation's refreshes.
    private void ResetCourseDraft(PlayerProjection loaded)
    {
        _courseHeading.Value = loaded.Ship.Tactical.HeadingDegrees.Value;
        _courseSpeed.Value = loaded.Ship.Tactical.SpeedKilometersPerSecond.Value;
    }

    private void StopCourse()
    {
        if (_projection is not null && !_stopCourseButton.Disabled)
            SubmitCourse(_projection.Ship.Tactical.HeadingDegrees.Value, 0);
    }

    private void SubmitCourse(double heading, double speed)
    {
        if (_simulation is null || _courseButton.Disabled || _dataMode != CommandInterfaceDataMode.Live)
        {
            return;
        }

        try
        {
            SetTacticalCourseResult result = _simulation.SetTacticalCourse(
                new SetTacticalCourseIntent(new HeadingDegrees(heading), new SpeedKilometersPerSecond(speed))
            );
            PresentOperationResult(() =>
            {
                _messageLabel.Text = result.Outcome switch
                {
                    SetTacticalCourseOutcome.Accepted =>
                        $"Tactical course set: heading {heading:0.0}°, speed {speed:0.0} km/s.",
                    SetTacticalCourseOutcome.UnavailableWhileTraveling =>
                        "Course unavailable while strategic travel is active.",
                    SetTacticalCourseOutcome.PropulsionOffline => "Course unavailable: impulse propulsion is offline.",
                    SetTacticalCourseOutcome.SpeedExceedsCurrentCapability =>
                        "Course unavailable: requested speed exceeds current propulsion capability.",
                    _ => "Tactical course was not accepted.",
                };
                RefreshProjection();
            });
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
        _commandDeck.ContactSelected += OnWorkspaceContactSelected;
        _commandDeck.PresentationActionRequested += OnPresentationActionRequested;
        _engineering.EngineeringCommandRequested += OnEngineeringCommandRequested;
        _travelButton.Pressed += RequestSelectedTravel;
        _courseButton.Pressed += SetDemonstrationCourse;
        _stopCourseButton.Pressed += StopCourse;
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
        _courseHeading = GetNode<SpinBox>("%CourseHeading");
        _courseSpeed = GetNode<SpinBox>("%CourseSpeed");
        _stopCourseButton = GetNode<Button>("%StopCourseButton");
        _courseInputs = GetNode<HBoxContainer>("%CourseInputs");
        _courseCapability = GetNode<Label>("%CourseCapability");
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

    private (
        ShipDefinitionCatalog ShipCatalog,
        FactionDefinitionCatalog FactionCatalog,
        GameSimulation Simulation
    ) CreateSimulationFromCanonicalContent()
    {
        // Explicit paths, no directory discovery. System definitions load first because ship loadouts are resolved
        // against the complete system catalog.
        SystemDefinitionCatalogLoader systemLoader = CreateSystemDefinitionLoader();
        SystemDefinitionCatalog systemCatalog = systemLoader.LoadCatalog([
            SystemDefinitionContent.FromUtf8(
                ContentSource(SystemDefinitionResourcePath),
                ReadRequiredBytes(
                    SystemDefinitionResourcePath,
                    SystemDefinitionContent.MaximumDocumentBytes,
                    "content.too-large"
                )
            ),
        ]);
        ShipDefinitionCatalogLoader shipLoader = CreateShipDefinitionLoader(systemCatalog);
        ShipDefinitionCatalog shipCatalog = shipLoader.LoadCatalog([
            ShipDefinitionContent.FromUtf8(
                ContentSource(ShipDefinitionResourcePath),
                ReadRequiredBytes(
                    ShipDefinitionResourcePath,
                    ShipDefinitionContent.MaximumDocumentBytes,
                    "content.size-limit"
                )
            ),
        ]);
        FactionDefinitionCatalogLoader factionLoader = CreateFactionDefinitionLoader();
        FactionDefinitionCatalog factionCatalog = factionLoader.LoadCatalog([
            FactionDefinitionContent.FromUtf8(
                ContentSource(FactionAPath),
                ReadRequiredBytes(
                    FactionAPath,
                    FactionDefinitionContent.MaximumDocumentBytes,
                    "content.size-limit",
                    faction: true
                )
            ),
            FactionDefinitionContent.FromUtf8(
                ContentSource(FactionBPath),
                ReadRequiredBytes(
                    FactionBPath,
                    FactionDefinitionContent.MaximumDocumentBytes,
                    "content.size-limit",
                    faction: true
                )
            ),
        ]);
        return (
            shipCatalog,
            factionCatalog,
            FirstGameSetup.Create(shipCatalog, factionCatalog, _logging?.SimulationLogger)
        );
    }

    private string ReadRequiredSchema(string path, bool faction = false)
    {
        byte[] bytes = ReadRequiredBytes(path, MaximumSchemaBytes, "content.size-limit", faction);
        try
        {
            // Native GetAsText recognized this encoding metadata. Strip one preamble only after raw byte bounds.
            ReadOnlySpan<byte> json = bytes;
            if (json.StartsWith("\uFEFF"u8))
            {
                json = json[3..];
            }

            return StrictUtf8.GetString(json);
        }
        catch (DecoderFallbackException)
        {
            throw ContentFailure(path, "json.invalid", "Required schema contains invalid UTF-8.", faction);
        }
    }

    private SystemDefinitionCatalogLoader CreateSystemDefinitionLoader()
    {
        string text = ReadRequiredSchema(SystemDefinitionSchemaResourcePath);
        try
        {
            return new SystemDefinitionCatalogLoader(text);
        }
        // JsonSchema.Net rejects non-object/non-boolean roots with an exact, parameterless ArgumentException.
        // Constructor arguments are already valid; derived argument exceptions remain programming failures.
        catch (Exception exception)
            when (exception is JsonException or JsonSchemaException
                || (
                    exception.GetType() == typeof(ArgumentException)
                    && exception is ArgumentException { ParamName: null }
                )
            )
        {
            throw ContentFailure(
                SystemDefinitionSchemaResourcePath,
                "schema.invalid",
                "Required schema is invalid.",
                faction: false
            );
        }
    }

    private ShipDefinitionCatalogLoader CreateShipDefinitionLoader(SystemDefinitionCatalog systems)
    {
        string text = ReadRequiredSchema(ShipSchemaResourcePath);
        try
        {
            return new ShipDefinitionCatalogLoader(text, systems);
        }
        // JsonSchema.Net rejects non-object/non-boolean roots with an exact, parameterless ArgumentException.
        // Constructor arguments are already valid; derived argument exceptions remain programming failures.
        catch (Exception exception)
            when (exception is JsonException or JsonSchemaException
                || (
                    exception.GetType() == typeof(ArgumentException)
                    && exception is ArgumentException { ParamName: null }
                )
            )
        {
            throw ContentFailure(
                ShipSchemaResourcePath,
                "schema.invalid",
                "Required schema is invalid.",
                faction: false
            );
        }
    }

    private FactionDefinitionCatalogLoader CreateFactionDefinitionLoader()
    {
        string text = ReadRequiredSchema(FactionSchemaPath, faction: true);
        try
        {
            return new FactionDefinitionCatalogLoader(text);
        }
        // JsonSchema.Net rejects non-object/non-boolean roots with an exact, parameterless ArgumentException.
        // Constructor arguments are already valid; derived argument exceptions remain programming failures.
        catch (Exception exception)
            when (exception is JsonException or JsonSchemaException
                || (
                    exception.GetType() == typeof(ArgumentException)
                    && exception is ArgumentException { ParamName: null }
                )
            )
        {
            throw ContentFailure(FactionSchemaPath, "schema.invalid", "Required schema is invalid.", faction: true);
        }
    }

    private byte[] ReadRequiredBytes(string path, int maximumBytes, string sizeCode, bool faction = false)
    {
        using IContentFileAccess? file = ContentFileOpener(path);
        if (file is null)
        {
            throw ContentReadFailure(path);
        }

        ulong declaredLength = file.GetLength();
        if (file.GetError() != Error.Ok)
        {
            throw ContentReadFailure(path);
        }

        if (declaredLength > (ulong)maximumBytes)
        {
            throw ContentFailure(path, sizeCode, "Required content exceeds its byte limit.", faction);
        }

        using var buffer = new MemoryStream();
        while (true)
        {
            // Length is only an early-rejection hint. Read to actual exhaustion with one bounded sentinel byte
            // so growth or a misleading length cannot admit a valid prefix while ignoring an unread suffix.
            int request = Math.Min(ContentReadChunkBytes, maximumBytes + 1 - checked((int)buffer.Length));
            byte[] chunk = file.GetBuffer(request);
            Error error = file.GetError();
            if (chunk.Length > request || (error != Error.Ok && error != Error.FileEof))
            {
                throw ContentReadFailure(path);
            }

            if (chunk.Length == 0)
            {
                if (error != Error.FileEof)
                {
                    throw ContentReadFailure(path);
                }

                break;
            }

            buffer.Write(chunk);
            if (buffer.Length > maximumBytes)
            {
                throw ContentFailure(path, sizeCode, "Required content exceeds its byte limit.", faction);
            }
        }

        // A partial final buffer with EOF is normal. An exhausted stream shorter or longer than its declared
        // length is not: even an otherwise-valid JSON prefix cannot prove that the complete resource was read.
        if ((ulong)buffer.Length != declaredLength)
        {
            throw ContentReadFailure(path);
        }

        return buffer.ToArray();
    }

    private static IOException ContentReadFailure(string path) =>
        new($"Godot could not read required content resource '{ContentSource(path)}'.");

    private static Exception ContentFailure(string path, string code, string message, bool faction) =>
        faction
            ? new FactionContentValidationException([
                new FactionContentDiagnostic(code, ContentSource(path), "#", string.Empty, message),
            ])
            : new ShipContentValidationException([
                new ShipContentDiagnostic(code, ContentSource(path), "#", string.Empty, message),
            ]);

    private static string ContentSource(string path) =>
        path.Length <= 512
        && (path.StartsWith("res://", StringComparison.Ordinal) || path.StartsWith("user://", StringComparison.Ordinal))
            ? path
            : "content-resource";

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
        Control? priorFocus = GetViewport().GuiGetFocusOwner();
        _projection = _simulation!.GetPlayerProjection();
        RevalidateSelectedContact();
        CommandInterfaceMode mode = _engineeringWorkspaceActive ? CommandInterfaceMode.Engineering : _commandMode;
        CommandInterfacePresentation presentation = CommandInterfacePresenter.PresentLive(
            _projection,
            _selectedDestination,
            _selectedContact,
            _recentActivity,
            mode,
            new OwnShipActionBinding(_projection.Ship.InstanceId, _simulationGeneration)
        );
        SetProjectionMetadata(_projection);
        PresentWorkspace(presentation);
        PresentShell(presentation);
        if (priorFocus is not null && !IsFocusable(priorFocus))
        {
            Control fallback = !_engineeringWorkspaceActive
                ? _commandDeck.GetVisibleFocusControls().FirstOrDefault() ?? _commandStationButton
                : _engineeringStationButton;
            DeferFocus(fallback);
        }
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
        SetMeta("simulation_generation", _simulationGeneration);

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
        _courseInputs.Visible = combat;
        _courseCapability.Visible = combat;
        _travelButton.Disabled = !live || !IsSubmittable(presentation, "travel");
        _courseButton.Disabled = !live || !IsSubmittable(presentation, "set-tactical-course");
        _stopCourseButton.Disabled = _courseButton.Disabled;
        if (_courseHeading.Editable != !_courseButton.Disabled)
            _courseHeading.Editable = !_courseButton.Disabled;
        if (_courseSpeed.Editable != !_courseButton.Disabled)
            _courseSpeed.Editable = !_courseButton.Disabled;
        _courseHeading.GetLineEdit().FocusMode = _courseButton.Disabled ? FocusModeEnum.None : FocusModeEnum.All;
        _courseSpeed.GetLineEdit().FocusMode = _courseButton.Disabled ? FocusModeEnum.None : FocusModeEnum.All;
        _courseCapability.Text = live
            ? $"CURRENT MAXIMUM: {_projection!.Ship.Engineering.EffectiveMaximumTacticalSpeed.Value:0.0} km/s"
            : "ILLUSTRATIVE PREVIEW / COURSE UNAVAILABLE";
        bool canAdvance =
            live
            && (
                _engineeringWorkspaceActive
                    ? _projection!.AvailableActions.Contains(PlayerAction.AdvanceTime)
                    : IsSubmittable(presentation, "advance-time")
            );
        _advanceUntilButton.Disabled = !canAdvance;
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
            : "Apply the entered heading and speed. Shortcut: C.";
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

    private void OnWorkspaceContactSelected(object? sender, CommandDeckWorkspace.ContactEventArgs args)
    {
        if (_dataMode == CommandInterfaceDataMode.Live)
        {
            OnContactSelected(args.ContactId);
        }
    }

    private void OnContactSelected(SensorContactId contactId)
    {
        if (
            _projection is null
            || _projection.Ship.Sensors.Contacts.All(contact =>
                contact.Id != contactId || contact.Status == SensorContactStatus.Lost
            )
        )
        {
            return;
        }

        _selectedContact = contactId;
        SetMeta("selected_contact", contactId.Value);
        RefreshProjection();
        _messageLabel.Text = $"Selected {DescribeContact(contactId)}.";
    }

    private void OnPresentationActionRequested(object? sender, CommandDeckWorkspace.ActionEventArgs args)
    {
        if (_dataMode != CommandInterfaceDataMode.Live || args.Action.Intent is not CommandInterfaceIntent intent)
        {
            return;
        }

        SubmitAction(args.Action, intent);
    }

    private void OnEngineeringCommandRequested(
        object? sender,
        EngineeringWorkspace.EngineeringCommandRequestedEventArgs args
    )
    {
        if (_dataMode == CommandInterfaceDataMode.Live && args.Action.EngineeringOperation is not null)
        {
            SubmitEngineeringAction(args.Action);
        }
    }

    /// <summary>
    /// Submits one Engineering control only if it still means what it meant when presented: its binding must match
    /// the current simulation generation and player ship, and its key must re-resolve to an available action in a
    /// freshly read Core projection. Otherwise nothing is submitted and the player is told the control lapsed.
    /// </summary>
    /// <remarks>
    /// Re-resolving by key alone is not enough: installed identities are ship-local, so after a load that changes
    /// the player ship or reuses id 4 for another definition, "repair:4" would silently retarget. The switch below
    /// is over the four generic operations only; it never branches on a system kind.
    /// </remarks>
    private void SubmitEngineeringAction(CommandInterfaceAction action)
    {
        if (_simulation is null || _dataMode != CommandInterfaceDataMode.Live)
        {
            return;
        }

        try
        {
            PlayerProjection current = _simulation.GetPlayerProjection();
            if (
                action.EngineeringOperation is not { } operation
                || !IsCurrentBinding(action.Binding, current)
                || !current.Ship.Engineering.Actions.Any(candidate =>
                    candidate.Operation == operation
                    && candidate.Target == action.EngineeringTarget
                    && candidate.IsAvailable
                )
            )
            {
                PresentOperationResult(() => RefuseLapsedControl(action));
                return;
            }

            InstalledSystemProjection? target = action.EngineeringTarget is { } id
                ? current.Ship.Engineering.Systems.Single(row => row.Id == id)
                : null;
            switch (operation)
            {
                case EngineeringOperation.Balance:
                    ApplyAllocation(_simulation.ApplyBalancedAllocation(), "Balanced allocation", action, current);
                    break;
                case EngineeringOperation.Prioritize when target is not null:
                    ApplyAllocation(
                        _simulation.ApplyPriorityAllocation(target.Id),
                        EngineeringKindPresentation.PriorityMessageLabel(target),
                        action,
                        current
                    );
                    break;
                case EngineeringOperation.BeginRepair when target is not null:
                    BeginSystemRepair(target, action);
                    break;
                case EngineeringOperation.ReturnToCommand:
                    PresentOperationResult(() =>
                    {
                        ShowCommandWorkspace();
                        _messageLabel.Text = "Returned to Command Deck.";
                    });
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(action), operation, "Unknown Engineering action.");
            }
        }
        catch (Exception exception)
        {
            ReportCommandFailure("Engineering command failed safely.", exception);
        }
    }

    /// <summary>
    /// Returns the refusal text shown when an allocation exceeds the demand of the player's installation
    /// <paramref name="installedId"/>, resolved through the current projection exactly as <see cref="ApplyAllocation"/>
    /// resolves the offending consumer. Balance and priority allocations never exceed demand, so the label-pinning
    /// gdUnit test reads the production strings through this instead of through a button.
    /// </summary>
    public string DescribeDemandRefusal(long installedId) =>
        _projection is null ? string.Empty : DemandRefusal(_projection, new InstalledSystemId(installedId));

    private static string DemandRefusal(PlayerProjection projection, InstalledSystemId? consumer) =>
        EngineeringKindPresentation.DemandExceededMessage(
            projection.Ship.Engineering.Systems.SingleOrDefault(row => row.Id == consumer)?.Kind
        );

    /// <summary>
    /// Test hook: reports whether a binding for player ship <paramref name="ownerShipId"/> presented under
    /// <paramref name="simulationGeneration"/> would be accepted now, through the same comparison every own-ship
    /// submission uses. Every quick-load bumps the generation, so no load fixture can hold the generation while
    /// changing the owner; this is the only way to prove the owner half of the check on its own.
    /// </summary>
    public bool IsBindingCurrent(long ownerShipId, long simulationGeneration) =>
        _simulation is not null
        && IsCurrentBinding(
            new OwnShipActionBinding(new ShipInstanceId(ownerShipId), simulationGeneration),
            _simulation.GetPlayerProjection()
        );

    private bool IsCurrentBinding(OwnShipActionBinding? binding, PlayerProjection current) =>
        binding is not null
        && binding.SimulationGeneration == _simulationGeneration
        && binding.Owner == current.Ship.InstanceId;

    private void RefuseLapsedControl(CommandInterfaceAction action)
    {
        RefreshProjection();
        _messageLabel.Text = "Command unavailable: that control is no longer available.";
        SetMeta("last_refused_action", action.Id);
    }

    private void ApplyAllocation(
        PowerAllocationResult result,
        string label,
        CommandInterfaceAction action,
        PlayerProjection before
    )
    {
        SetMeta("last_engineering_command", EngineeringCommandMeta(action, result.Outcome.ToString()));
        PresentOperationResult(() =>
        {
            PresentResolvedEvents(result.ResolvedEvents, announce: false);
            _messageLabel.Text = result.Outcome switch
            {
                PowerAllocationOutcome.Accepted => $"{label} applied.",
                PowerAllocationOutcome.CurrentSpeedExceedsResultingMaximum =>
                    "Allocation unavailable: reduce current speed before lowering propulsion power.",
                PowerAllocationOutcome.ConsumerDemandExceeded => DemandRefusal(before, result.Consumer),
                PowerAllocationOutcome.AvailablePowerExceeded =>
                    "Allocation unavailable: requested load exceeds available power.",
                _ => "Power allocation was not accepted.",
            };
            RefreshProjection();
        });
    }

    private void BeginSystemRepair(InstalledSystemProjection target, CommandInterfaceAction action)
    {
        // Each projected repair action means a complete repair; Core still validates the nominal target against
        // current condition and the one-repair constraint at submission time.
        SystemRepairResult result = _simulation!.BeginSystemRepair(target.Id, new SystemCondition(1));
        SetMeta("last_engineering_command", EngineeringCommandMeta(action, result.Outcome.ToString()));
        PresentOperationResult(() =>
        {
            _messageLabel.Text = result.Outcome switch
            {
                SystemRepairOutcome.Accepted =>
                    $"{EngineeringKindPresentation.MessageNoun(target.Kind, target.ComponentLabel)} repair started.",
                SystemRepairOutcome.RepairAlreadyActive => "Repair unavailable: another system repair is active.",
                SystemRepairOutcome.NotRepairable => "Repair unavailable: that system is unsupported.",
                SystemRepairOutcome.TargetDoesNotImproveCondition =>
                    "Repair unavailable: the selected system is already nominal.",
                _ => "System repair was not accepted.",
            };
            RefreshProjection();
        });
    }

    // Test hook "<operation>:<installed id or ->:<outcome>", e.g. "Prioritize:2:Accepted" or "Balance:-:Accepted".
    private static string EngineeringCommandMeta(CommandInterfaceAction action, string outcome) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"{action.EngineeringOperation}:{(action.EngineeringTarget is { } id ? id.Value.ToString(CultureInfo.InvariantCulture) : "-")}:{outcome}"
        );

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
            case CommandInterfaceIntent.ActiveScan:
            case CommandInterfaceIntent.Hail:
                return;
            default:
                throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unknown command-interface intent.");
        }
    }

    private void SubmitAction(CommandInterfaceAction action, CommandInterfaceIntent intent)
    {
        // Command Deck actions are bound like Engineering ones: contact identities are observer-local, so a control
        // presented for a replaced simulation must not act on whatever now carries the same local contact id.
        if (_simulation is null)
        {
            return;
        }

        if (!IsCurrentBinding(action.Binding, _simulation.GetPlayerProjection()))
        {
            RefuseLapsedControl(action);
            return;
        }

        if (intent == CommandInterfaceIntent.FireDirectedEnergy)
        {
            if (action.FocusedContactId is { } contact && action.AimKind is { } system)
                RequestDirectedEnergy(contact, system);
            return;
        }
        if (intent is CommandInterfaceIntent.ActiveScan or CommandInterfaceIntent.Hail)
        {
            if (action.FocusedContactId is not SensorContactId contactId)
            {
                return;
            }

            if (intent == CommandInterfaceIntent.ActiveScan)
            {
                RequestActiveScan(contactId);
            }
            else
            {
                RequestHail(contactId);
            }

            return;
        }

        SubmitIntent(intent);
    }

    private void RequestActiveScan(SensorContactId contactId)
    {
        if (_simulation is null || _dataMode != CommandInterfaceDataMode.Live)
        {
            return;
        }

        try
        {
            ActiveSensorScanResult result = _simulation.RequestActiveSensorScan(contactId);
            PresentOperationResult(() =>
            {
                _messageLabel.Text = result.Outcome switch
                {
                    ActiveSensorScanOutcome.Accepted => $"Active scan started on {DescribeContact(contactId)}.",
                    ActiveSensorScanOutcome.ContactNotFound => "Active scan unavailable: contact is no longer present.",
                    ActiveSensorScanOutcome.ContactNotCurrent => "Active scan unavailable: contact is not current.",
                    ActiveSensorScanOutcome.AlreadyIdentified =>
                        "Active scan unavailable: contact is already identified.",
                    ActiveSensorScanOutcome.SensorsUnavailable => "Active scan unavailable: sensors are offline.",
                    ActiveSensorScanOutcome.ScanAlreadyActive => "Active scan unavailable: another scan is active.",
                    _ => "Active scan request was not accepted.",
                };
                SetMeta("last_contact_command", $"active-scan:{result.Outcome}");
                RefreshProjection();
            });
        }
        catch (Exception exception)
        {
            ReportCommandFailure("Active scan command failed safely.", exception);
        }
    }

    private void RequestDirectedEnergy(SensorContactId contactId, ShipSystemKind system)
    {
        if (_simulation is null || _dataMode != CommandInterfaceDataMode.Live)
            return;
        try
        {
            FireDirectedEnergyResult result = _simulation.FireDirectedEnergy(
                new FireDirectedEnergyIntent(contactId, system)
            );
            PresentOperationResult(() =>
            {
                SetMeta("last_fire_outcome", result.Outcome.ToString());
                PresentResolvedEvents(result.ResolvedEvents, false);
                _messageLabel.Text =
                    result.Outcome == FireDirectedEnergyOutcome.Accepted
                        ? "Directed-energy shot fired."
                        : CommandInterfacePresenter.FireReason(result.Outcome);
                RefreshProjection();
            });
        }
        catch (Exception exception)
        {
            ReportCommandFailure("Fire command failed safely.", exception);
        }
    }

    private void RequestHail(SensorContactId contactId)
    {
        if (_simulation is null || _dataMode != CommandInterfaceDataMode.Live)
        {
            return;
        }

        try
        {
            string contactLabel = DescribeContact(contactId);
            HailResult result = _simulation.RequestHail(contactId);
            PresentOperationResult(() =>
            {
                _messageLabel.Text = result.Outcome switch
                {
                    HailOutcome.Acknowledged => $"{contactLabel} acknowledged the hail.",
                    HailOutcome.NoResponse => $"{contactLabel} did not respond.",
                    HailOutcome.ContactNotFound => "Hail unavailable: contact is no longer present.",
                    HailOutcome.ContactNotCurrent => "Hail unavailable: contact is not current.",
                    HailOutcome.ContactNotIdentified => "Hail unavailable: identify the contact first.",
                    _ => "Hail request was not accepted.",
                };
                PresentHailOutcome(contactId, contactLabel, result.Outcome);
                SetMeta("last_contact_command", $"hail:{result.Outcome}");
                RefreshProjection();
            });
        }
        catch (Exception exception)
        {
            ReportCommandFailure("Hail command failed safely.", exception);
        }
    }

    private string FindLocationName(LocationId id) =>
        _projection!.Strategic.Locations.Single(location => location.Id == id).DisplayName;

    private void SetProjectionMetadata(PlayerProjection projection)
    {
        SetMeta("simulation_time_milliseconds", projection.SimulationTime.Milliseconds);
        SetMeta("ship_name", projection.Ship.DisplayName);
        SetMeta("player_ship_id", projection.Ship.InstanceId.Value);
        SetMeta("sensor_integrity", projection.Ship.Sensors.Integrity);
        SetMeta("sensor_repair_progress", projection.Ship.Sensors.RepairProgress);
        SetMeta("sensor_repairing", projection.Ship.Sensors.IsRepairing);
        SetEngineeringMetadata(projection.Ship.Engineering);
        SetMeta("combat_remaining_cooldown", projection.Ship.Combat.RemainingCooldown.Milliseconds);
        SetMeta("combat_next_ready_at", projection.Ship.Combat.NextDirectedEnergyReadyAt?.Milliseconds ?? -1);
        CombatTargetProjection? combatTarget = projection.Ship.Combat.Targets.SingleOrDefault(target =>
            target.ContactId == _selectedContact
        );
        SetMeta("combat_target_outcome", combatTarget?.Outcome.ToString() ?? string.Empty);
        SetMeta("combat_known_range", combatTarget?.Range?.Value ?? -1);
        SetMeta("map_location_count", projection.Strategic.Locations.Count);
        SetMeta("map_route_count", projection.Strategic.Routes.Count);
        SetMeta("travel_active", projection.Strategic.Travel is not null);
        SetMeta("travel_origin", projection.Strategic.Travel?.Origin.Value ?? string.Empty);
        SetMeta("travel_destination", projection.Strategic.Travel?.Destination.Value ?? string.Empty);
        SetMeta("travel_eta_milliseconds", projection.Strategic.Travel?.ExpectedArrival.Milliseconds ?? -1);
        SetMeta("tactical_x", projection.Ship.Tactical.Position.XKilometers);
        SetMeta("tactical_y", projection.Ship.Tactical.Position.YKilometers);
        SetMeta("tactical_heading", projection.Ship.Tactical.HeadingDegrees.Value);
        SetMeta("tactical_speed", projection.Ship.Tactical.SpeedKilometersPerSecond.Value);
        SetContactMetadata(projection);
    }

    /// <summary>
    /// Publishes engineering test hooks. Per-installation values are keyed by installed identity —
    /// <c>engineering_condition_&lt;id&gt;</c>, <c>engineering_allocation_&lt;id&gt;</c> (consumers only) and
    /// <c>engineering_capability_&lt;id&gt;</c> (where projected) — for every row Core projects, so no hook presumes
    /// one installation per kind. Observation only; nothing reads them back.
    /// </summary>
    /// <remarks>
    /// Rejected: kind-named hooks such as <c>engineering_sensor_allocation</c>. They must pick "the" row of a kind
    /// (a <c>SingleOrDefault</c> over kind), which is exactly the singleton selection the substrate contract forbids
    /// outside typed admission. A canonical-loadout kind-to-id mapping, where a test wants one, lives in test code.
    /// </remarks>
    private void SetEngineeringMetadata(EngineeringProjection engineering)
    {
        SetMeta("engineering_nominal_power", engineering.NominalGeneration.Value);
        SetMeta("engineering_available_power", engineering.AvailablePower.Value);
        SetMeta("engineering_reserve", engineering.Reserve.Value);
        SetMeta("engineering_repair_target", engineering.ActiveRepair?.TargetKind.Value ?? string.Empty);
        SetMeta(
            "engineering_repair_target_id",
            engineering.ActiveRepair?.Target.Value.ToString(CultureInfo.InvariantCulture) ?? string.Empty
        );
        SetMeta(
            "engineering_system_ids",
            string.Join(',', engineering.Systems.Select(row => row.Id.Value.ToString(CultureInfo.InvariantCulture)))
        );

        var published = new HashSet<long>();
        foreach (InstalledSystemProjection row in engineering.Systems)
        {
            long id = row.Id.Value;
            published.Add(id);
            SetMeta(InstallationMetaName("condition", id), row.Condition.Value);
            if (row.Allocation is { } allocation)
            {
                SetMeta(InstallationMetaName("allocation", id), allocation.Value);
            }
            else
            {
                RemoveMeta(InstallationMetaName("allocation", id));
            }

            if (row.Capability is { } capability)
            {
                SetMeta(InstallationMetaName("capability", id), capability);
            }
            else
            {
                RemoveMeta(InstallationMetaName("capability", id));
            }
        }

        foreach (long removed in _publishedInstallationMeta.Where(id => !published.Contains(id)))
        {
            RemoveMeta(InstallationMetaName("condition", removed));
            RemoveMeta(InstallationMetaName("allocation", removed));
            RemoveMeta(InstallationMetaName("capability", removed));
        }

        _publishedInstallationMeta = published;
    }

    private static StringName InstallationMetaName(string field, long installedId) =>
        string.Create(CultureInfo.InvariantCulture, $"engineering_{field}_{installedId}");

    private void SetContactMetadata(PlayerProjection projection)
    {
        CommandInterfaceContact[] visibleContacts =
        [
            .. projection
                .Ship.Sensors.Contacts.Where(contact => contact.Status != SensorContactStatus.Lost)
                .OrderBy(contact => contact.Id.Value)
                .Select(contact => new CommandInterfaceContact(
                    contact.Id,
                    contact.Identification == SensorContactIdentification.Identified
                        ? contact.KnownVesselDisplayName
                            ?? contact.KnownDesignDisplayName
                            ?? $"Contact {contact.Id.Value}"
                        : $"Contact {contact.Id.Value}",
                    contact.Status,
                    contact.Identification,
                    contact.LastObservedPosition.XKilometers,
                    contact.LastObservedPosition.YKilometers,
                    contact.LastObservedAt.Milliseconds,
                    projection.SimulationTime.Milliseconds - contact.LastObservedAt.Milliseconds,
                    contact.Identification == SensorContactIdentification.Identified
                        ? contact.KnownVesselDisplayName
                        : null,
                    contact.Identification == SensorContactIdentification.Identified
                        ? contact.KnownDesignDisplayName
                        : null,
                    projection.Ship.Sensors.ActiveScanContactId == contact.Id
                )),
        ];
        SetMeta("sensor_contact_count", visibleContacts.Length);
        SetMeta("active_scan_contact", projection.Ship.Sensors.ActiveScanContactId?.Value ?? 0);
        SetMeta("selected_contact", _selectedContact?.Value ?? 0);
        SetMeta("first_contact_id", visibleContacts.FirstOrDefault()?.Id.Value ?? 0);
        SetMeta("first_contact_label", visibleContacts.FirstOrDefault()?.Label ?? string.Empty);
        SetMeta("first_contact_status", visibleContacts.FirstOrDefault()?.Status.ToString() ?? string.Empty);
        SetMeta(
            "first_contact_identification",
            visibleContacts.FirstOrDefault()?.Identification.ToString() ?? string.Empty
        );
    }

    private void ClearSelectedDestination()
    {
        _selectedDestination = null;
        SetMeta("selected_destination", string.Empty);
    }

    private void ClearSelectedContact()
    {
        _selectedContact = null;
        SetMeta("selected_contact", 0);
    }

    private void RevalidateSelectedContact()
    {
        if (
            _selectedContact is SensorContactId selected
            && _projection!.Ship.Sensors.Contacts.All(contact =>
                contact.Id != selected || contact.Status == SensorContactStatus.Lost
            )
        )
        {
            ClearSelectedContact();
        }
    }

    private void SetGameplayEnabled(bool enabled)
    {
        foreach (
            Button button in new[]
            {
                _travelButton,
                _courseButton,
                _stopCourseButton,
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
            if (_commandDeck.TacticalMap.IsVisibleInTree())
            {
                controls.Add(_commandDeck.TacticalMap);
            }

            controls.AddRange(_commandDeck.GetVisibleFocusControls());
            controls.AddRange(GetCourseFocusControls());
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

    private IEnumerable<Control> GetCourseFocusControls() =>
        _courseInputs.IsVisibleInTree()
            ? [_courseHeading.GetLineEdit(), _courseSpeed.GetLineEdit(), _stopCourseButton]
            : [];

    private Button CurrentWorkspaceButton() =>
        _engineeringWorkspaceActive ? _engineeringStationButton : _commandStationButton;

    // Focus requests are deferred so they land after the whole synchronous presentation pass, yet a later
    // call in that same frame can reconcile the captured target away: ShowPreview removes live action
    // buttons that a preceding RefreshProjection chose as its fallback. Removed buttons are only
    // QueueFree'd, so they are still valid objects outside the tree when the deferred call runs, and an
    // unguarded GrabFocus then fails with the engine's "!is_inside_tree()" error. The target is therefore
    // revalidated at flush time and a stale request is dropped; any later request queued in the same
    // frame (FocusCurrentWorkspace after a view switch) still applies, matching
    // EngineeringWorkspace.RestorePendingFocus.
    //
    // The request also captures the simulation generation: a focus request queued for one simulation is dropped if
    // a quick-load replaced it before the flush, since the load queues its own focus target.
    private void DeferFocus(Control target)
    {
        long generation = _simulationGeneration;
        Callable
            .From(() =>
            {
                if (
                    generation == _simulationGeneration
                    && GodotObject.IsInstanceValid(target)
                    && target.IsInsideTree()
                    && target.IsVisibleInTree()
                )
                {
                    target.GrabFocus();
                }
            })
            .CallDeferred();
    }

    private void FocusCurrentWorkspace()
    {
        if (_engineeringWorkspaceActive)
        {
            _engineering.GrabEntryFocus();
        }
        else
        {
            DeferFocus(_commandStationButton);
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

    private string DescribeAdvanceResult(AdvanceUntilResult result)
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

        foreach (PlayerAdvanceEvent @event in events)
        {
            _logging?.Diagnostics.Consequence(@event, _projection?.Ship.InstanceId.Value);
            AppendRecentActivity(
                new CommandInterfacePresenter.ResolvedActivityEvent(@event.OccurredAt.Milliseconds, @event)
            );
        }

        string description = string.Join(", ", events.Select(DescribePlayerEvent));
        SetMeta("last_advance_event", description);
        if (announce)
        {
            _messageLabel.Text = description;
        }
    }

    private void PresentHailOutcome(SensorContactId contactId, string contactLabel, HailOutcome outcome)
    {
        if (outcome is HailOutcome.Acknowledged or HailOutcome.NoResponse)
        {
            AppendRecentActivity(
                new CommandInterfacePresenter.HailActivityEvent(
                    _projection!.SimulationTime.Milliseconds,
                    contactId,
                    contactLabel,
                    outcome
                )
            );
        }
    }

    private void AppendRecentActivity(CommandInterfacePresenter.ActivityEvent activity)
    {
        _recentActivity.Add(activity);
        if (_recentActivity.Count > RecentActivityLimit)
        {
            _recentActivity.RemoveRange(0, _recentActivity.Count - RecentActivityLimit);
        }
    }

    private string DescribePlayerEvent(PlayerAdvanceEvent @event) =>
        @event.Kind switch
        {
            PlayerAdvanceEventKind.TravelArrived => "arrival complete",
            PlayerAdvanceEventKind.SystemRepairCompleted =>
                $"{EngineeringKindPresentation.MessageNoun(@event.SystemKind).ToLowerInvariant()} repair complete",
            PlayerAdvanceEventKind.SensorContactDetected => $"{DescribeContact(@event)} detected",
            PlayerAdvanceEventKind.SensorContactStale => $"{DescribeContact(@event)} stale",
            PlayerAdvanceEventKind.SensorContactReacquired => $"{DescribeContact(@event)} reacquired",
            PlayerAdvanceEventKind.SensorContactLost => $"{DescribeContact(@event)} lost",
            PlayerAdvanceEventKind.ActiveSensorScanCompleted => $"{DescribeContact(@event)} scan complete",
            PlayerAdvanceEventKind.ActiveSensorScanInterrupted => $"{DescribeContact(@event)} scan interrupted",
            PlayerAdvanceEventKind.DirectedEnergyFired
            or PlayerAdvanceEventKind.ShieldImpact
            or PlayerAdvanceEventKind.SubsystemPenetration
            or PlayerAdvanceEventKind.OwnSystemDamaged
            or PlayerAdvanceEventKind.SystemRepairInterrupted
            or PlayerAdvanceEventKind.PowerBrownout
            or PlayerAdvanceEventKind.ForcedDeceleration => CommandInterfacePresenter.CombatEventText(@event),
            _ => "player event complete",
        };

    private string DescribeContact(PlayerAdvanceEvent @event) =>
        @event.SensorContactId is { } contactId ? DescribeContact(contactId) : "Contact";

    private string DescribeContact(SensorContactId contactId)
    {
        SensorContactSnapshot? contact = _projection?.Ship.Sensors.Contacts.SingleOrDefault(candidate =>
            candidate.Id == contactId
        );
        return contact?.Identification == SensorContactIdentification.Identified
            ? contact.KnownVesselDisplayName ?? contact.KnownDesignDisplayName ?? $"Contact {contactId.Value}"
            : $"Contact {contactId.Value}";
    }

    // Core and persistence have already returned. A projection fault cannot turn their committed result into
    // a failed command or a failed save/load; the scene may be stale, but authoritative state remains intact.
    private void PresentOperationResult(Action present)
    {
        try
        {
            present();
        }
        catch (Exception exception)
        {
            SetMeta("presentation_status", "failed");
            if (GodotObject.IsInstanceValid(_messageLabel))
            {
                _messageLabel.Text += " Presentation refresh unavailable; simulation state is retained.";
            }
            LogDiagnostic(GameDiagnostics.FailureOperation.Presentation, exception);
        }
    }

    private void ReportPersistenceFailure(string operation, string status, string category, Exception exception)
    {
        _messageLabel.Text = $"{operation} failed: {category}.";
        SetMeta("quick_save_status", status);
        LogDiagnostic(
            string.Equals(status, "save_failed", StringComparison.Ordinal)
                ? GameDiagnostics.FailureOperation.Save
                : GameDiagnostics.FailureOperation.Load,
            exception
        );
    }

    private void ReportCommandFailure(string playerMessage, Exception exception)
    {
        _messageLabel.Text = playerMessage;
        LogDiagnostic(GameDiagnostics.FailureOperation.Command, exception);
    }

    private void ReportAdvanceFailure(Exception exception)
    {
        _rateController.SetRate(0);
        SetMeta("simulation_rate", 0);
        UpdateRateButtonStates();
        _rateStatusLabel.Text = "RATE PAUSED";
        _messageLabel.Text = "Time advancement failed safely; simulation is paused.";
        SetMeta("advance_status", "failed");
        LogDiagnostic(GameDiagnostics.FailureOperation.Simulation, exception);
    }

    private void LogDiagnostic(GameDiagnostics.FailureOperation operation, Exception exception) =>
        _logging?.Diagnostics.Failure(
            operation,
            exception,
            _projection?.SimulationTime.Milliseconds,
            _projection?.Ship.InstanceId.Value
        );

    /// <inheritdoc />
    public override void _ExitTree()
    {
        _logging?.Diagnostics.Lifecycle(
            false,
            _projection?.SimulationTime.Milliseconds,
            _projection?.Ship.InstanceId.Value
        );
        _logging?.Dispose();
        _logging = null;
    }
}
