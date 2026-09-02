using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Strategic;
using Godot;
using GodotFile = Godot.FileAccess;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Owns one scene-lifetime simulation and projects player-known state into the command shell.</summary>
public partial class GameScreen : Control
{
    private const string SchemaPath = "res://content/schemas/ship-definition-v2.schema.json";
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
    private GameSimulation? _simulation;
    private ShipDefinitionCatalog? _shipCatalog;
    private PlayerProjection? _projection;
    private LocationId? _selectedDestination;
    private DateTimeOffset? _quickSaveCreatedAtUtc;
    private string _quickSavePath = null!;
    private bool _quickSaveUsesDefaultPath;
    private bool _strategicViewActive = true;
    private double _lastRunningRate = 1;
    private StrategicMapView _strategicMap = null!;
    private TacticalMapView _tacticalMap = null!;
    private VBoxContainer _strategicCommands = null!;
    private VBoxContainer _tacticalCommands = null!;
    private VBoxContainer _destinationButtons = null!;
    private Label _timeLabel = null!;
    private Label _vesselStatusLabel = null!;
    private Label _rateStatusLabel = null!;
    private Label _viewStatusLabel = null!;
    private Label _mapTitleLabel = null!;
    private Label _mapScaleLabel = null!;
    private Label _contextTitleLabel = null!;
    private Label _shipLabel = null!;
    private Label _sensorLabel = null!;
    private Label _travelLabel = null!;
    private Label _courseLabel = null!;
    private Label _messageLabel = null!;
    private Button _travelButton = null!;
    private Button _courseButton = null!;
    private Button _advanceUntilButton = null!;
    private Button _strategicButton = null!;
    private Button _tacticalButton = null!;
    private Button _quickSaveButton = null!;
    private Button _quickLoadButton = null!;
    private Button _pauseButton = null!;
    private Button _halfRateButton = null!;
    private Button _normalRateButton = null!;
    private Button _doubleRateButton = null!;
    private Button _quadRateButton = null!;

    /// <summary>Gets whether canonical content produced a complete playable simulation.</summary>
    public bool IsGameplayReady => _simulation is not null;

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
            RefreshProjection();
            BuildDestinationButtons();
            SetSimulationRate(1);
            _messageLabel.Text = "Command systems ready.";
            _strategicButton.CallDeferred(Control.MethodName.GrabFocus);
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
        int steps = _rateController.ConsumeElapsed(elapsedSeconds);
        if (_simulation is null || steps == 0)
        {
            return 0;
        }

        try
        {
            SimulationAdvanceResult result = _simulation.AdvanceFixedSteps(steps);
            SetMeta("advance_status", "advanced");
            RefreshProjection();
            PresentResolvedEvents(result.ResolvedEvents, announce: false);
            if (result.ResolvedEvents.Contains(PlayerAdvanceEvent.TravelArrived))
            {
                _selectedDestination = null;
                SetMeta("selected_destination", string.Empty);
                BuildDestinationButtons();
            }

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
        if (_simulation is null)
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
        if (_simulation is null || _quickSaveButton.Disabled)
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
        if (_simulation is null || _shipCatalog is null || _quickLoadButton.Disabled)
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
            _selectedDestination = null;
            SetMeta("selected_destination", string.Empty);

            // Rate is a current player preference, so it survives load. Fractional carry is dropped
            // because presentation time accumulated before the snapshot must not advance restored truth.
            _rateController.ResetAccumulatedTime();
            RefreshProjection();
            BuildDestinationButtons();
            _messageLabel.Text =
                $"Quick load restored time {loaded.Simulation.GetPlayerProjection().SimulationTime.Milliseconds / 1000.0:0.0} s.";
            SetMeta("quick_save_status", "loaded");
            CurrentViewButton().CallDeferred(Control.MethodName.GrabFocus);
        }
        catch (Exception exception)
        {
            ReportPersistenceFailure("Quick load", "load_failed", "save data is unavailable or invalid", exception);
        }
    }

    /// <summary>Selects a strategic destination by stable Core identifier.</summary>
    public void SelectDestination(string destinationId)
    {
        if (_simulation is null || _projection is null)
        {
            return;
        }

        OnDestinationSelected(new LocationId(destinationId));
    }

    /// <summary>Submits selected strategic travel through the typed Core command.</summary>
    public void RequestSelectedTravel()
    {
        if (_simulation is null || _travelButton.Disabled || _selectedDestination is not LocationId destination)
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
            BuildDestinationButtons();
        }
        catch (Exception exception)
        {
            ReportCommandFailure("Travel command failed safely.", exception);
        }
    }

    /// <summary>Advances through Core to the next player-relevant event boundary.</summary>
    public void AdvanceUntilNextPlayerRelevantEvent()
    {
        if (_simulation is null || _advanceUntilButton.Disabled)
        {
            return;
        }

        try
        {
            AdvanceUntilResult result = _simulation.AdvanceUntilNextPlayerRelevantEvent();
            string resolved = DescribeAdvanceResult(result);
            _messageLabel.Text = resolved;
            SetMeta("advance_status", "advanced");
            SetMeta("last_advance_event", resolved);
            RefreshProjection();
            if (result.ResolvedEvents.Contains(PlayerAdvanceEvent.TravelArrived))
            {
                _selectedDestination = null;
                SetMeta("selected_destination", string.Empty);
                BuildDestinationButtons();
            }
        }
        catch (Exception exception)
        {
            ReportAdvanceFailure(exception);
        }
    }

    /// <summary>Submits a visible north-east tactical course through the typed Core command.</summary>
    public void SetDemonstrationCourse()
    {
        if (_simulation is null || _courseButton.Disabled)
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

    /// <summary>Shows the strategic route projection and its matching command context.</summary>
    public void ShowStrategicView()
    {
        _strategicViewActive = true;
        _strategicMap.Visible = true;
        _tacticalMap.Visible = false;
        _strategicCommands.Visible = true;
        _tacticalCommands.Visible = false;
        _strategicButton.ButtonPressed = true;
        _tacticalButton.ButtonPressed = false;
        _viewStatusLabel.Text = "VIEW STRATEGIC";
        _mapTitleLabel.Text = "STRATEGIC SENSOR PLOT";
        _mapScaleLabel.Text = "SEMANTIC ROUTES";
        _contextTitleLabel.Text = "STRATEGIC COMMAND";
        SetMeta("active_view", "strategic");
        UpdateFocusTraversal();
    }

    /// <summary>Shows the tactical motion projection and its matching command context.</summary>
    public void ShowTacticalView()
    {
        _strategicViewActive = false;
        _strategicMap.Visible = false;
        _tacticalMap.Visible = true;
        _strategicCommands.Visible = false;
        _tacticalCommands.Visible = true;
        _strategicButton.ButtonPressed = false;
        _tacticalButton.ButtonPressed = true;
        _viewStatusLabel.Text = "VIEW TACTICAL";
        _mapTitleLabel.Text = "TACTICAL MOTION PLOT";
        _mapScaleLabel.Text = "LOCAL KILOMETERS";
        _contextTitleLabel.Text = "TACTICAL COMMAND";
        SetMeta("active_view", "tactical");
        UpdateFocusTraversal();
    }

    /// <summary>Maps a continuous Core tactical position for integration verification.</summary>
    public Vector2 MapTacticalPosition(double xKilometers, double yKilometers) =>
        _tacticalMap.MapPosition(xKilometers, yKilometers);

    private void BindScene()
    {
        _strategicMap = GetNode<StrategicMapView>("%StrategicMap");
        _tacticalMap = GetNode<TacticalMapView>("%TacticalMap");
        _strategicCommands = GetNode<VBoxContainer>("%StrategicCommands");
        _tacticalCommands = GetNode<VBoxContainer>("%TacticalCommands");
        _destinationButtons = GetNode<VBoxContainer>("%DestinationButtons");
        BindLabels();
        BindButtons();

        _strategicMap.DestinationSelected = OnDestinationSelected;
        _travelButton.Pressed += RequestSelectedTravel;
        _courseButton.Pressed += SetDemonstrationCourse;
        _advanceUntilButton.Pressed += AdvanceUntilNextPlayerRelevantEvent;
        _strategicButton.Pressed += ShowStrategicView;
        _tacticalButton.Pressed += ShowTacticalView;
        _quickSaveButton.Pressed += QuickSave;
        _quickLoadButton.Pressed += QuickLoad;
        _pauseButton.Pressed += TogglePause;
        ConfigureRateButton(_halfRateButton, 0.5);
        ConfigureRateButton(_normalRateButton, 1);
        ConfigureRateButton(_doubleRateButton, 2);
        ConfigureRateButton(_quadRateButton, 4);
        ShowStrategicView();
    }

    private void BindLabels()
    {
        _timeLabel = GetNode<Label>("%SimulationTime");
        _vesselStatusLabel = GetNode<Label>("%VesselStatus");
        _rateStatusLabel = GetNode<Label>("%RateStatus");
        _viewStatusLabel = GetNode<Label>("%ViewStatus");
        _mapTitleLabel = GetNode<Label>("%MapTitle");
        _mapScaleLabel = GetNode<Label>("%MapScale");
        _contextTitleLabel = GetNode<Label>("%ContextTitle");
        _shipLabel = GetNode<Label>("%ShipStatus");
        _sensorLabel = GetNode<Label>("%SensorStatus");
        _travelLabel = GetNode<Label>("%TravelStatus");
        _courseLabel = GetNode<Label>("%CourseStatus");
        _messageLabel = GetNode<Label>("%Message");
    }

    private void BindButtons()
    {
        _travelButton = GetNode<Button>("%TravelButton");
        _courseButton = GetNode<Button>("%CourseButton");
        _advanceUntilButton = GetNode<Button>("%AdvanceUntilButton");
        _strategicButton = GetNode<Button>("%StrategicButton");
        _tacticalButton = GetNode<Button>("%TacticalButton");
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

    private void OnDestinationSelected(LocationId destination)
    {
        if (_projection is null)
        {
            return;
        }

        _selectedDestination = destination;
        SetMeta("selected_destination", destination.Value);
        _strategicMap.Present(_projection.Strategic, destination);
        bool available =
            IsConnectedDestination(destination) && _projection.AvailableActions.Contains(PlayerAction.Travel);
        _travelButton.Disabled = !available;
        _travelButton.TooltipText = available
            ? $"Engage direct travel to {FindLocationName(destination)}. Shortcut: E."
            : "Travel is unavailable for this location or current vessel state.";
        _messageLabel.Text = $"Selected destination: {FindLocationName(destination)}.";
        UpdateFocusTraversal();
    }

    private bool IsConnectedDestination(LocationId destination)
    {
        StrategicLocationProjection? current = _projection?.Strategic.CurrentLocation;
        return current is not null
            && _projection!.Strategic.Routes.Any(route =>
                (route.Origin == current.Id && route.Destination == destination)
                || (route.Destination == current.Id && route.Origin == destination)
            );
    }

    private string FindLocationName(LocationId id) =>
        _projection!.Strategic.Locations.Single(location => location.Id == id).DisplayName;

    private void RefreshProjection()
    {
        _projection = _simulation!.GetPlayerProjection();
        PlayerProjection projection = _projection;
        _strategicMap.Present(projection.Strategic, _selectedDestination);
        _tacticalMap.Present(projection.Ship.Tactical);
        _timeLabel.Text = $"TIME {projection.SimulationTime.Milliseconds / 1000.0:0.0} s";
        _vesselStatusLabel.Text = $"VESSEL {projection.Ship.DisplayName}";
        _shipLabel.Text = $"VESSEL\n{projection.Ship.DisplayName}\nRegistry {projection.Ship.InstanceId.Value}";
        string repairState =
            projection.Ship.Sensors.IsRepairing ? $"REPAIRING {projection.Ship.Sensors.RepairProgress:P0}"
            : projection.Ship.Sensors.Integrity >= 1 ? "REPAIR COMPLETE"
            : "REPAIR INACTIVE";
        _sensorLabel.Text = $"SENSORS\nIntegrity {projection.Ship.Sensors.Integrity:P0}\n{repairState}";
        _courseLabel.Text =
            $"TACTICAL\nPosition {projection.Ship.Tactical.Position.XKilometers:0.0}, {projection.Ship.Tactical.Position.YKilometers:0.0} km\nHeading {projection.Ship.Tactical.HeadingDegrees:000.#}°\nSpeed {projection.Ship.Tactical.SpeedKilometersPerSecond:0.#} km/s";
        if (projection.Strategic.Travel is TravelProjection travel)
        {
            _travelLabel.Text =
                $"STRATEGIC — UNDERWAY\n{FindLocationName(travel.Origin)} → {FindLocationName(travel.Destination)}\nETA {travel.ExpectedArrival.Milliseconds / 1000.0:0.0} s";
        }
        else
        {
            _travelLabel.Text = $"STRATEGIC — AT LOCATION\n{projection.Strategic.CurrentLocation!.DisplayName}";
        }

        bool travelAvailable =
            _selectedDestination is LocationId selected
            && IsConnectedDestination(selected)
            && projection.AvailableActions.Contains(PlayerAction.Travel);
        _travelButton.Disabled = !travelAvailable;
        _travelButton.TooltipText =
            travelAvailable ? $"Engage direct travel to {FindLocationName(_selectedDestination!.Value)}. Shortcut: E."
            : projection.Strategic.Travel is not null ? "Travel is unavailable while the vessel is already underway."
            : "Select a directly connected destination before engaging travel.";
        _courseButton.Disabled = !projection.AvailableActions.Contains(PlayerAction.SetTacticalCourse);
        _courseButton.TooltipText = _courseButton.Disabled
            ? "Course changes are unavailable during strategic travel."
            : "Submit heading 045° and speed 2 km/s. Shortcut: C.";
        _advanceUntilButton.Disabled = !projection.AvailableActions.Contains(PlayerAction.AdvanceTime);
        SetProjectionMetadata(projection);
        UpdateFocusTraversal();
    }

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

    private void BuildDestinationButtons()
    {
        foreach (Node child in _destinationButtons.GetChildren())
        {
            _destinationButtons.RemoveChild(child);
            child.QueueFree();
        }

        StrategicLocationProjection? current = _projection!.Strategic.CurrentLocation;
        bool travelActionAvailable = _projection.AvailableActions.Contains(PlayerAction.Travel);
        foreach (StrategicLocationProjection location in _projection.Strategic.Locations)
        {
            bool connected = current is not null && IsConnectedDestination(location.Id);
            var button = new Button
            {
                Text = location.DisplayName,
                Disabled = !travelActionAvailable || location.Id == current?.Id || !connected,
                FocusMode = FocusModeEnum.All,
                ToggleMode = true,
                ButtonPressed = location.Id == _selectedDestination,
                TooltipText = connected
                    ? $"Select {location.DisplayName} by stable navigation identity."
                    : $"{location.DisplayName} is not directly reachable from the current location.",
            };
            LocationId destination = location.Id;
            button.Pressed += () => OnDestinationSelected(destination);
            _destinationButtons.AddChild(button);
        }

        UpdateFocusTraversal();
    }

    private void SetGameplayEnabled(bool enabled)
    {
        _travelButton.Disabled = !enabled;
        _courseButton.Disabled = !enabled;
        _advanceUntilButton.Disabled = !enabled;
        _quickSaveButton.Disabled = !enabled;
        _quickLoadButton.Disabled = !enabled;
        foreach (Button button in GetNode<HBoxContainer>("%RateControls").GetChildren().OfType<Button>())
        {
            button.Disabled = !enabled;
        }

        foreach (Button button in _destinationButtons.GetChildren().OfType<Button>())
        {
            button.Disabled = !enabled;
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

        var controls = new List<Control> { _strategicButton, _tacticalButton };
        if (_strategicViewActive)
        {
            controls.AddRange(
                _destinationButtons.GetChildren().OfType<Button>().Where(button => button.Visible && !button.Disabled)
            );
            if (!_travelButton.Disabled)
            {
                controls.Add(_travelButton);
            }
        }
        else if (!_courseButton.Disabled)
        {
            controls.Add(_courseButton);
        }

        controls.AddRange(
            new Button[]
            {
                _pauseButton,
                _halfRateButton,
                _normalRateButton,
                _doubleRateButton,
                _quadRateButton,
                _advanceUntilButton,
                _quickSaveButton,
                _quickLoadButton,
            }.Where(button => !button.Disabled)
        );

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

    private Button CurrentViewButton() => _strategicViewActive ? _strategicButton : _tacticalButton;

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
