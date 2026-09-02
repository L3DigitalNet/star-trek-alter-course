using AlterCourse.Core.Content;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Persistence;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Simulation;
using AlterCourse.Core.Strategic;
using Godot;
using GodotFile = Godot.FileAccess;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Owns one scene-lifetime simulation and projects it into the command shell.</summary>
public partial class GameScreen : Control
{
    private const string SchemaPath = "res://content/schemas/ship-definition-v1.schema.json";
    private const string ShipPath = "res://content/ships/pathfinder.json";
    private const string DefaultQuickSaveUserPath = "user://quick-save-v1.json";
    private const string QuickSaveId = "quick-save";
    private const string QuickSaveDisplayName = "Quick Save";

    private readonly SimulationRateController _rateController = new();
    private GameSimulation? _simulation;
    private ShipDefinitionCatalog? _shipCatalog;
    private PlayerProjection? _projection;
    private LocationId? _selectedDestination;
    private DateTimeOffset? _quickSaveCreatedAtUtc;
    private string _quickSavePath = null!;
    private StrategicMapView _strategicMap = null!;
    private TacticalMapView _tacticalMap = null!;
    private VBoxContainer _destinationButtons = null!;
    private Label _timeLabel = null!;
    private Label _shipLabel = null!;
    private Label _sensorLabel = null!;
    private Label _travelLabel = null!;
    private Label _courseLabel = null!;
    private Label _messageLabel = null!;
    private Button _travelButton = null!;
    private Button _courseButton = null!;
    private Button _strategicButton = null!;
    private Button _tacticalButton = null!;
    private Button _quickSaveButton = null!;
    private Button _quickLoadButton = null!;

    /// <summary>Gets whether canonical content produced a complete playable simulation.</summary>
    public bool IsGameplayReady => _simulation is not null;

    /// <summary>Gets or sets the Godot user-data path for the one quick-save slot.</summary>
    [Export]
    public string QuickSaveUserPath { get; set; } = DefaultQuickSaveUserPath;

    /// <summary>Gets the latest fresh player-known projection.</summary>
    public PlayerProjection? Projection => _projection;

    /// <inheritdoc />
    public override void _Ready()
    {
        BindScene();
        try
        {
            if (!QuickSaveUserPath.StartsWith("user://", StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Quick-save storage must use Godot's user:// boundary.");
            }

            string quickSavePath = ProjectSettings.GlobalizePath(QuickSaveUserPath);
            (ShipDefinitionCatalog catalog, GameSimulation simulation) = CreateSimulationFromCanonicalContent();
            _quickSavePath = quickSavePath;
            _shipCatalog = catalog;
            _simulation = simulation;
            SetMeta("quick_save_user_path", QuickSaveUserPath);
            RefreshProjection();
            BuildDestinationButtons();
            _strategicButton.GrabFocus();
        }
        catch (Exception exception)
        {
            // Loading is fail-closed: retaining a half-built aggregate would make the visible UI
            // appear playable while its definition and validation contract had not completed.
            _simulation = null;
            _shipCatalog = null;
            _projection = null;
            SetGameplayEnabled(false);
            _messageLabel.Text = $"Unable to load gameplay content: {exception.Message}";
            SetMeta("load_error", _messageLabel.Text);
        }
    }

    /// <inheritdoc />
    public override void _Process(double delta)
    {
        ProcessSyntheticDelta(delta);
    }

    /// <summary>Consumes controlled presentation elapsed time and returns submitted Core steps.</summary>
    public int ProcessSyntheticDelta(double elapsedSeconds)
    {
        int steps = _rateController.ConsumeElapsed(elapsedSeconds);
        if (_simulation is null || steps == 0)
        {
            return 0;
        }

        SimulationAdvanceResult result = _simulation.AdvanceFixedSteps(steps);
        RefreshProjection();
        if (result.ResolvedKinds.Contains(ScheduledWorkKind.TravelArrival))
        {
            BuildDestinationButtons();
        }

        return steps;
    }

    /// <summary>Selects one of the five supported presentation time rates.</summary>
    public void SetSimulationRate(double rate)
    {
        _rateController.SetRate(rate);
        SetMeta("simulation_rate", rate);
        _messageLabel.Text = rate == 0 ? "Simulation paused." : $"Simulation rate: {rate:0.0#}x";
    }

    /// <summary>Saves the current simulation to the one application-owned quick-save slot.</summary>
    public void QuickSave()
    {
        if (_simulation is null)
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
            ReportPersistenceFailure("Quick save", "save_failed", exception);
        }
    }

    /// <summary>Loads a new validated simulation from the quick-save slot.</summary>
    public void QuickLoad()
    {
        if (_simulation is null || _shipCatalog is null)
        {
            return;
        }

        try
        {
            LoadedGameSave loaded = GamePersistence.Load(_quickSavePath, _shipCatalog);

            // Core constructs and validates the candidate in isolation. Assignment stays after that
            // boundary so an unreadable or invalid save cannot damage the playable aggregate.
            _simulation = loaded.Simulation;
            _quickSaveCreatedAtUtc = loaded.Metadata.CreatedAtUtc;
            _selectedDestination = null;

            // Rate is a current player preference, so it survives load. Fractional carry is dropped
            // because presentation time accumulated before the snapshot must not advance restored truth.
            _rateController.ResetAccumulatedTime();
            RefreshProjection();
            BuildDestinationButtons();
            _messageLabel.Text =
                $"Quick load restored {loaded.Simulation.GetPlayerProjection().SimulationTime.Milliseconds / 1000.0:0.0} s.";
            SetMeta("quick_save_status", "loaded");
        }
        catch (Exception exception)
        {
            ReportPersistenceFailure("Quick load", "load_failed", exception);
        }
    }

    /// <summary>Selects a strategic destination by stable Core identifier.</summary>
    public void SelectDestination(string destinationId)
    {
        var destination = new LocationId(destinationId);
        OnDestinationSelected(destination);
    }

    /// <summary>Submits selected strategic travel through the typed Core command.</summary>
    public void RequestSelectedTravel()
    {
        if (_simulation is null || _selectedDestination is not LocationId destination)
        {
            return;
        }

        TravelRequestResult result = _simulation.RequestTravel(new TravelIntent(destination));
        _messageLabel.Text = $"Travel request: {result.Outcome}";
        RefreshProjection();
        BuildDestinationButtons();
    }

    /// <summary>Advances through Core to the earliest scheduled event boundary.</summary>
    public void AdvanceUntilNextEvent()
    {
        if (_simulation is null)
        {
            return;
        }

        AdvanceUntilResult result = _simulation.AdvanceUntilNextScheduledEvent();
        string resolved =
            result.ResolvedKinds.Count == 0 ? result.Outcome.ToString() : string.Join(", ", result.ResolvedKinds);
        _messageLabel.Text = $"Advanced to next event: {resolved}";
        SetMeta("last_advance_event", resolved);
        RefreshProjection();
        BuildDestinationButtons();
    }

    /// <summary>Submits a visible north-east tactical course through the typed Core command.</summary>
    public void SetDemonstrationCourse()
    {
        if (_simulation is null)
        {
            return;
        }

        SetTacticalCourseResult result = _simulation.SetTacticalCourse(
            new SetTacticalCourseIntent(new HeadingDegrees(45), new SpeedKilometersPerSecond(2))
        );
        _messageLabel.Text = $"Tactical course: {result.Outcome}";
        RefreshProjection();
    }

    /// <summary>Shows the distinct strategic projection.</summary>
    public void ShowStrategicView()
    {
        _strategicMap.Visible = true;
        _tacticalMap.Visible = false;
        _strategicButton.Disabled = true;
        _tacticalButton.Disabled = false;
        SetMeta("active_view", "strategic");
    }

    /// <summary>Shows the distinct tactical projection.</summary>
    public void ShowTacticalView()
    {
        _strategicMap.Visible = false;
        _tacticalMap.Visible = true;
        _strategicButton.Disabled = false;
        _tacticalButton.Disabled = true;
        SetMeta("active_view", "tactical");
    }

    /// <summary>Maps a continuous Core tactical position for integration verification.</summary>
    public Vector2 MapTacticalPosition(double xKilometers, double yKilometers) =>
        _tacticalMap.MapPosition(xKilometers, yKilometers);

    private void BindScene()
    {
        _strategicMap = GetNode<StrategicMapView>("%StrategicMap");
        _tacticalMap = GetNode<TacticalMapView>("%TacticalMap");
        _destinationButtons = GetNode<VBoxContainer>("%DestinationButtons");
        _timeLabel = GetNode<Label>("%SimulationTime");
        _shipLabel = GetNode<Label>("%ShipStatus");
        _sensorLabel = GetNode<Label>("%SensorStatus");
        _travelLabel = GetNode<Label>("%TravelStatus");
        _courseLabel = GetNode<Label>("%CourseStatus");
        _messageLabel = GetNode<Label>("%Message");
        _travelButton = GetNode<Button>("%TravelButton");
        _courseButton = GetNode<Button>("%CourseButton");
        _strategicButton = GetNode<Button>("%StrategicButton");
        _tacticalButton = GetNode<Button>("%TacticalButton");
        _quickSaveButton = GetNode<Button>("%QuickSaveButton");
        _quickLoadButton = GetNode<Button>("%QuickLoadButton");

        _strategicMap.DestinationSelected = OnDestinationSelected;
        _travelButton.Pressed += RequestSelectedTravel;
        _courseButton.Pressed += SetDemonstrationCourse;
        GetNode<Button>("%AdvanceUntilButton").Pressed += AdvanceUntilNextEvent;
        _strategicButton.Pressed += ShowStrategicView;
        _tacticalButton.Pressed += ShowTacticalView;
        _quickSaveButton.Pressed += QuickSave;
        _quickLoadButton.Pressed += QuickLoad;
        ConfigureRateButton("%PauseRate", 0);
        ConfigureRateButton("%HalfRate", 0.5);
        ConfigureRateButton("%NormalRate", 1);
        ConfigureRateButton("%DoubleRate", 2);
        ConfigureRateButton("%QuadRate", 4);
        ShowStrategicView();
    }

    private static (ShipDefinitionCatalog Catalog, GameSimulation Simulation) CreateSimulationFromCanonicalContent()
    {
        string schema = ReadRequiredText(SchemaPath);
        string definitionJson = ReadRequiredText(ShipPath);
        var loader = new ShipDefinitionCatalogLoader(schema);
        ShipDefinitionCatalog catalog = loader.LoadCatalog([ShipDefinitionContent.FromText(ShipPath, definitionJson)]);
        ShipDefinition definition = catalog.Definitions.Single();
        return (catalog, FirstGameSetup.Create(definition));
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

    private void ConfigureRateButton(string path, double rate)
    {
        GetNode<Button>(path).Pressed += () => SetSimulationRate(rate);
    }

    private void OnDestinationSelected(LocationId destination)
    {
        _selectedDestination = destination;
        SetMeta("selected_destination", destination.Value);
        _strategicMap.Present(_projection!.Strategic, destination);
        _travelButton.Disabled = !IsConnectedDestination(destination);
        _messageLabel.Text = $"Selected destination: {FindLocationName(destination)}";
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
        _timeLabel.Text = $"SIMULATION TIME  {projection.SimulationTime.Milliseconds / 1000.0:0.0} s";
        _shipLabel.Text = $"{projection.Ship.DisplayName}\nID {projection.Ship.InstanceId.Value}";
        _sensorLabel.Text =
            $"SENSORS  {projection.Ship.Sensors.Integrity:P0}\nRepair {projection.Ship.Sensors.RepairProgress:P0}";
        _courseLabel.Text =
            $"TACTICAL\nHeading {projection.Ship.Tactical.HeadingDegrees:0.#}°\nSpeed {projection.Ship.Tactical.SpeedKilometersPerSecond:0.#} km/s";
        if (projection.Strategic.Travel is TravelProjection travel)
        {
            _travelLabel.Text =
                $"TRAVELING\n{FindLocationName(travel.Origin)} → {FindLocationName(travel.Destination)}\nETA {travel.ExpectedArrival.Milliseconds / 1000.0:0.0} s";
        }
        else
        {
            _travelLabel.Text = $"AT LOCATION\n{projection.Strategic.CurrentLocation!.DisplayName}";
        }

        _travelButton.Disabled = _selectedDestination is not LocationId selected || !IsConnectedDestination(selected);
        _courseButton.Disabled = !projection.AvailableActions.Contains(PlayerAction.SetTacticalCourse);
        SetProjectionMetadata(projection);
    }

    private void SetProjectionMetadata(PlayerProjection projection)
    {
        SetMeta("simulation_time_milliseconds", projection.SimulationTime.Milliseconds);
        SetMeta("ship_name", projection.Ship.DisplayName);
        SetMeta("sensor_integrity", projection.Ship.Sensors.Integrity);
        SetMeta("sensor_repair_progress", projection.Ship.Sensors.RepairProgress);
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
        foreach (StrategicLocationProjection location in _projection.Strategic.Locations)
        {
            var button = new Button
            {
                Text = location.DisplayName,
                Disabled = current is null || location.Id == current.Id || !IsConnectedDestination(location.Id),
                FocusMode = FocusModeEnum.All,
                TooltipText = $"Select {location.DisplayName} as strategic destination",
            };
            LocationId destination = location.Id;
            button.Pressed += () => OnDestinationSelected(destination);
            _destinationButtons.AddChild(button);
        }
    }

    private void SetGameplayEnabled(bool enabled)
    {
        _travelButton.Disabled = !enabled;
        _courseButton.Disabled = !enabled;
        GetNode<Button>("%AdvanceUntilButton").Disabled = !enabled;
        _quickSaveButton.Disabled = !enabled;
        _quickLoadButton.Disabled = !enabled;
        foreach (Button button in GetNode<HBoxContainer>("%RateControls").GetChildren().OfType<Button>())
        {
            button.Disabled = !enabled;
        }
    }

    private void ReportPersistenceFailure(string operation, string status, Exception exception)
    {
        _messageLabel.Text = $"{operation} failed: {exception.Message}";
        SetMeta("quick_save_status", status);
    }
}
