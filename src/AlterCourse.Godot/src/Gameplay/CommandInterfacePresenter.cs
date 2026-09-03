using System.Collections.Immutable;
using System.Globalization;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Player;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Maps a fresh player-known Core projection into immutable command-interface display data.</summary>
public static class CommandInterfacePresenter
{
    /// <summary>Builds a live presentation without inspecting hidden scheduler, NPC, or aggregate state.</summary>
    public static CommandInterfacePresentation PresentLive(
        PlayerProjection projection,
        LocationId? selectedLocationId = null,
        IReadOnlyList<PlayerAdvanceEvent>? recentEvents = null,
        CommandInterfaceMode mode = CommandInterfaceMode.Travel
    )
    {
        ArgumentNullException.ThrowIfNull(projection);
        recentEvents ??= [];

        StrategicLocationProjection? selectedLocation = selectedLocationId is LocationId selected
            ? projection.Strategic.Locations.SingleOrDefault(location => location.Id == selected)
            : null;
        return new CommandInterfacePresentation
        {
            DataMode = CommandInterfaceDataMode.Live,
            Mode = mode,
            Header = BuildHeader(projection),
            Systems = BuildSystems(projection),
            Telemetry = BuildTelemetry(projection, selectedLocation),
            Actions = BuildActions(projection, selectedLocation),
            Events = BuildEvents(projection, recentEvents),
            Stations = BuildStations(mode),
            MapItems = BuildMapItems(projection),
            MapLinks = BuildMapLinks(projection),
            SelectedLocationId = selectedLocation?.Id,
            Strategic = projection.Strategic,
            Tactical = projection.Ship.Tactical,
            Engineering = BuildEngineering(projection),
        };
    }

    private static ImmutableArray<CommandInterfaceField> BuildHeader(PlayerProjection projection) =>
        [
            Available("VESSEL", projection.Ship.DisplayName, CommandInterfaceTone.Command),
            Available("REGISTRY", projection.Ship.InstanceId.Value.ToString(CultureInfo.InvariantCulture)),
            Available("VESSEL CLASS ID", projection.Ship.DefinitionId.Value),
            Unavailable("STARDATE"),
            Available("SIMULATION", FormatSeconds(projection.SimulationTime.Milliseconds)),
            Unavailable("ALERT"),
        ];

    private static ImmutableArray<CommandInterfaceSystemRow> BuildSystems(PlayerProjection projection)
    {
        CommandInterfaceTone sensorTone =
            projection.Ship.Sensors.Integrity >= 0.8 ? CommandInterfaceTone.Nominal : CommandInterfaceTone.Caution;
        return
        [
            SystemUnavailable("hull", "HULL"),
            SystemUnavailable("shields", "SHIELDS"),
            SystemUnavailable("power", "POWER"),
            new CommandInterfaceSystemRow(
                "propulsion",
                "PROP",
                Available(
                    "SPEED",
                    FormatSpeed(projection.Ship.Tactical.SpeedKilometersPerSecond),
                    CommandInterfaceTone.Nominal
                )
            ),
            new CommandInterfaceSystemRow(
                "sensors",
                "SENSORS",
                Available("INTEGRITY", FormatPercent(projection.Ship.Sensors.Integrity), sensorTone)
            ),
            SystemUnavailable("weapons", "WEAPONS"),
            SystemUnavailable("computer", "COMPUTER"),
            SystemUnavailable("life-support", "LIFE SUP"),
        ];
    }

    private static ImmutableArray<CommandInterfaceTelemetrySection> BuildTelemetry(
        PlayerProjection projection,
        StrategicLocationProjection? selectedLocation
    )
    {
        ImmutableArray<CommandInterfaceField> destinationFields = selectedLocation is null
            ? [Unavailable("DESTINATION")]
            :
            [
                Available("DESTINATION", selectedLocation.DisplayName, CommandInterfaceTone.Navigation),
                Available("LOCATION ID", selectedLocation.Id.Value),
                Unavailable("CLASS"),
                Unavailable("POPULATION"),
            ];
        ImmutableArray<CommandInterfaceField> strategicFields = projection.Strategic.Travel is TravelProjection travel
            ?
            [
                Available("STATE", "UNDERWAY", CommandInterfaceTone.Navigation),
                Available("ORIGIN", FindLocationName(projection.Strategic, travel.Origin)),
                Available("DESTINATION", FindLocationName(projection.Strategic, travel.Destination)),
                Available("DEPARTURE", FormatSeconds(travel.Departure.Milliseconds)),
                Available("ARRIVAL", FormatSeconds(travel.ExpectedArrival.Milliseconds)),
                Available("ACTIVE", travel.IsActive ? "YES" : "NO"),
            ]
            :
            [
                Available("STATE", "AT LOCATION", CommandInterfaceTone.Nominal),
                Available("LOCATION", projection.Strategic.CurrentLocation?.DisplayName ?? "UNKNOWN"),
                Unavailable("ARRIVAL"),
            ];
        return
        [
            new CommandInterfaceTelemetrySection(
                "destination",
                "DESTINATION",
                CommandInterfaceTone.Navigation,
                destinationFields
            ),
            new CommandInterfaceTelemetrySection(
                "strategic",
                "ROUTE",
                CommandInterfaceTone.Navigation,
                strategicFields
            ),
            new CommandInterfaceTelemetrySection(
                "tactical",
                "TACTICAL MOTION",
                CommandInterfaceTone.Command,
                [
                    Available("POSITION X", FormatKilometers(projection.Ship.Tactical.Position.XKilometers)),
                    Available("POSITION Y", FormatKilometers(projection.Ship.Tactical.Position.YKilometers)),
                    Available("HEADING", FormatHeading(projection.Ship.Tactical.HeadingDegrees)),
                    Available("SPEED", FormatSpeed(projection.Ship.Tactical.SpeedKilometersPerSecond)),
                ]
            ),
            new CommandInterfaceTelemetrySection(
                "combat",
                "TACTICAL SUMMARY",
                CommandInterfaceTone.Critical,
                [Unavailable("CONTACTS"), Unavailable("FIRE SOLUTION"), Unavailable("SHIELDS"), Unavailable("WEAPONS")]
            ),
        ];
    }

    private static ImmutableArray<CommandInterfaceAction> BuildActions(
        PlayerProjection projection,
        StrategicLocationProjection? selectedLocation
    ) =>
        [
            LiveAction(
                "travel",
                selectedLocation is null ? "Set course…" : $"Set course to {selectedLocation.DisplayName}",
                CommandInterfaceTone.Navigation,
                CommandInterfaceIntent.Travel,
                selectedLocation is not null && projection.AvailableActions.Contains(PlayerAction.Travel)
            ),
            LiveAction(
                "set-tactical-course",
                "Adjust tactical course…",
                CommandInterfaceTone.Command,
                CommandInterfaceIntent.SetTacticalCourse,
                projection.AvailableActions.Contains(PlayerAction.SetTacticalCourse)
            ),
            LiveAction(
                "advance-time",
                "Advance to next event",
                CommandInterfaceTone.Command,
                CommandInterfaceIntent.AdvanceTime,
                projection.AvailableActions.Contains(PlayerAction.AdvanceTime)
            ),
            DisabledAction("fire-phasers", "Fire phasers", CommandInterfaceTone.Critical),
            DisabledAction("allocate-power", "Prioritize power", CommandInterfaceTone.Engineering),
            DisabledAction("assign-repair", "Assign repair team…", CommandInterfaceTone.Engineering),
        ];

    private static ImmutableArray<CommandInterfaceEventRow> BuildEvents(
        PlayerProjection projection,
        IReadOnlyList<PlayerAdvanceEvent> events
    ) =>
        [
            .. events.Select(@event =>
                @event.Kind switch
                {
                    PlayerAdvanceEventKind.TravelArrived => new CommandInterfaceEventRow(
                        FormatClock(projection.SimulationTime.Milliseconds),
                        "NAV",
                        "Strategic travel arrived at destination.",
                        CommandInterfaceTone.Navigation
                    ),
                    PlayerAdvanceEventKind.SensorRepairCompleted => new CommandInterfaceEventRow(
                        FormatClock(projection.SimulationTime.Milliseconds),
                        "ENGINEER",
                        "Sensor repair completed.",
                        CommandInterfaceTone.Nominal
                    ),
                    PlayerAdvanceEventKind.SensorContactDetected => SensorEvent(
                        projection,
                        @event,
                        "Contact detected."
                    ),
                    PlayerAdvanceEventKind.SensorContactStale => SensorEvent(
                        projection,
                        @event,
                        "Contact became stale."
                    ),
                    PlayerAdvanceEventKind.SensorContactReacquired => SensorEvent(
                        projection,
                        @event,
                        "Contact reacquired."
                    ),
                    PlayerAdvanceEventKind.SensorContactLost => SensorEvent(projection, @event, "Contact lost."),
                    PlayerAdvanceEventKind.ActiveSensorScanCompleted => SensorEvent(
                        projection,
                        @event,
                        "Active scan completed."
                    ),
                    PlayerAdvanceEventKind.ActiveSensorScanInterrupted => SensorEvent(
                        projection,
                        @event,
                        "Active scan interrupted."
                    ),
                    _ => throw new ArgumentOutOfRangeException(nameof(events), @event, "Unknown player event."),
                }
            ),
        ];

    private static CommandInterfaceEventRow SensorEvent(
        PlayerProjection projection,
        PlayerAdvanceEvent @event,
        string message
    ) =>
        new(
            FormatClock(projection.SimulationTime.Milliseconds),
            "SENSOR",
            $"{DescribeContact(@event)}: {message}",
            CommandInterfaceTone.Caution
        );

    private static string DescribeContact(PlayerAdvanceEvent @event) =>
        @event.SensorContactId is { } contactId ? $"Contact {contactId.Value}" : "Contact";

    private static ImmutableArray<CommandInterfaceStation> BuildStations(CommandInterfaceMode mode)
    {
        string selected = mode switch
        {
            CommandInterfaceMode.Travel => "command",
            CommandInterfaceMode.Combat => "command",
            CommandInterfaceMode.Engineering => "engineering",
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown command-interface mode."),
        };
        return
        [
            new(
                "command",
                "COMMAND",
                string.Equals(selected, "command", StringComparison.Ordinal),
                0,
                CommandInterfaceTone.Command
            ),
            new("tactical", "TACTICAL", false, 0, CommandInterfaceTone.Muted),
            new("navigation", "NAVIGATION", false, 0, CommandInterfaceTone.Navigation),
            new(
                "engineering",
                "ENGINEERING",
                string.Equals(selected, "engineering", StringComparison.Ordinal),
                0,
                CommandInterfaceTone.Engineering
            ),
            new("science", "SCIENCE", false, 0, CommandInterfaceTone.Muted),
            new("comms", "COMMS", false, 0, CommandInterfaceTone.Muted),
            new("operations", "OPERATIONS", false, 0, CommandInterfaceTone.Muted),
        ];
    }

    private static ImmutableArray<CommandInterfaceMapItem> BuildMapItems(PlayerProjection projection) =>
        [
            .. projection.Strategic.Locations.Select(location => new CommandInterfaceMapItem(
                location.Id.Value,
                location.DisplayName,
                CommandInterfaceMapItemKind.Location,
                location.Position.X,
                location.Position.Y,
                CommandInterfaceTone.Navigation,
                location.Id
            )),
        ];

    private static ImmutableArray<CommandInterfaceMapLink> BuildMapLinks(PlayerProjection projection) =>
        [
            .. projection.Strategic.Routes.Select(route => new CommandInterfaceMapLink(
                route.Origin.Value,
                route.Destination.Value,
                CommandInterfaceTone.Navigation,
                FormatSeconds(route.Duration.Milliseconds)
            )),
        ];

    private static CommandInterfaceEngineeringPresentation BuildEngineering(PlayerProjection projection)
    {
        CommandInterfaceTone sensorTone =
            projection.Ship.Sensors.Integrity >= 0.8 ? CommandInterfaceTone.Nominal : CommandInterfaceTone.Caution;
        return new CommandInterfaceEngineeringPresentation(
            [
                HierarchyUnavailable("power", null, "POWER"),
                HierarchyUnavailable("propulsion", null, "PROPULSION"),
                HierarchyUnavailable("shields", null, "SHIELDS"),
                HierarchyUnavailable("weapons", null, "WEAPONS"),
                new CommandInterfaceHierarchyRow(
                    "sensors",
                    null,
                    "SENSORS",
                    true,
                    projection.Ship.Sensors.IsRepairing ? 1 : 0,
                    CommandInterfaceAvailability.Available,
                    sensorTone
                ),
                HierarchyUnavailable("computer", null, "COMPUTER"),
                HierarchyUnavailable("life-support", null, "LIFE SUPPORT"),
                HierarchyUnavailable("structural", null, "STRUCTURAL"),
            ],
            [
                new CommandInterfaceTelemetrySection(
                    "sensors",
                    "SENSORS",
                    sensorTone,
                    [
                        Available("INTEGRITY", FormatPercent(projection.Ship.Sensors.Integrity), sensorTone),
                        Available(
                            "REPAIR STATE",
                            projection.Ship.Sensors.IsRepairing ? "REPAIRING" : "INACTIVE",
                            projection.Ship.Sensors.IsRepairing
                                ? CommandInterfaceTone.Caution
                                : CommandInterfaceTone.Muted
                        ),
                        Available("REPAIR PROGRESS", FormatPercent(projection.Ship.Sensors.RepairProgress), sensorTone),
                    ]
                ),
                new CommandInterfaceTelemetrySection(
                    "engineering-unsupported",
                    "ENGINEERING",
                    CommandInterfaceTone.Engineering,
                    [Unavailable("POWER"), Unavailable("EPS"), Unavailable("DAMAGE"), Unavailable("GENERAL REPAIR")]
                ),
            ],
            [],
            []
        );
    }

    private static CommandInterfaceAction LiveAction(
        string id,
        string label,
        CommandInterfaceTone tone,
        CommandInterfaceIntent intent,
        bool isAvailable
    ) =>
        new(
            id,
            label,
            isAvailable ? tone : CommandInterfaceTone.Muted,
            isAvailable ? CommandInterfaceActionAvailability.Submittable : CommandInterfaceActionAvailability.Disabled,
            isAvailable ? intent : null
        );

    private static CommandInterfaceAction DisabledAction(string id, string label, CommandInterfaceTone tone) =>
        new(id, label, tone, CommandInterfaceActionAvailability.Disabled);

    private static CommandInterfaceSystemRow SystemUnavailable(string id, string label) =>
        new(id, label, Unavailable("STATUS"));

    private static CommandInterfaceHierarchyRow HierarchyUnavailable(string id, string? parentId, string label) =>
        new(id, parentId, label, false, 0, CommandInterfaceAvailability.Unavailable, CommandInterfaceTone.Muted);

    private static CommandInterfaceField Available(
        string label,
        string value,
        CommandInterfaceTone tone = CommandInterfaceTone.Neutral
    ) => new(label, value, CommandInterfaceAvailability.Available, tone);

    private static CommandInterfaceField Unavailable(string label) =>
        new(label, string.Empty, CommandInterfaceAvailability.Unavailable, CommandInterfaceTone.Muted);

    private static string FindLocationName(StrategicProjection strategic, LocationId id) =>
        strategic.Locations.Single(location => location.Id == id).DisplayName;

    private static string FormatClock(long milliseconds) =>
        TimeSpan.FromMilliseconds(milliseconds).ToString(@"hh\:mm\:ss", CultureInfo.InvariantCulture);

    private static string FormatSeconds(long milliseconds) =>
        string.Create(CultureInfo.InvariantCulture, $"{milliseconds / 1000.0:0.0} s");

    private static string FormatPercent(double fraction) => fraction.ToString("P0", CultureInfo.InvariantCulture);

    private static string FormatKilometers(double kilometers) =>
        string.Create(CultureInfo.InvariantCulture, $"{kilometers:0.0} km");

    private static string FormatHeading(double degrees) =>
        string.Create(CultureInfo.InvariantCulture, $"{degrees:000.#}°");

    private static string FormatSpeed(double kilometersPerSecond) =>
        string.Create(CultureInfo.InvariantCulture, $"{kilometersPerSecond:0.#} km/s");
}
