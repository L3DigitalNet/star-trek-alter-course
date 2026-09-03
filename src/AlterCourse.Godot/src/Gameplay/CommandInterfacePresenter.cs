using System.Collections.Immutable;
using System.Globalization;
using AlterCourse.Core.Gameplay;
using AlterCourse.Core.Player;
using AlterCourse.Core.Quantities;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Maps a fresh player-known Core projection into immutable command-interface display data.</summary>
public static class CommandInterfacePresenter
{
    /// <summary>Represents one player-visible activity retained by the command-interface log.</summary>
    public abstract record ActivityEvent(long SimulationTimeMilliseconds);

    /// <summary>Wraps one actor-safe event resolved by deterministic Core advancement.</summary>
    public sealed record ResolvedActivityEvent(long SimulationTimeMilliseconds, PlayerAdvanceEvent Event)
        : ActivityEvent(SimulationTimeMilliseconds);

    /// <summary>Captures one typed hail response using the observer-local contact context shown to the player.</summary>
    public sealed record HailActivityEvent(
        long SimulationTimeMilliseconds,
        SensorContactId ContactId,
        string ContactLabel,
        HailOutcome Outcome
    ) : ActivityEvent(SimulationTimeMilliseconds);

    /// <summary>Builds a live presentation without inspecting hidden scheduler, NPC, or aggregate state.</summary>
    public static CommandInterfacePresentation PresentLive(
        PlayerProjection projection,
        LocationId? selectedLocationId = null,
        SensorContactId? selectedContactId = null,
        IReadOnlyList<ActivityEvent>? recentEvents = null,
        CommandInterfaceMode mode = CommandInterfaceMode.Travel
    )
    {
        ArgumentNullException.ThrowIfNull(projection);
        recentEvents ??= [];

        StrategicLocationProjection? selectedLocation = selectedLocationId is LocationId selected
            ? projection.Strategic.Locations.SingleOrDefault(location => location.Id == selected)
            : null;
        SensorContactSnapshot? selectedContact = selectedContactId is SensorContactId contactId
            ? projection.Ship.Sensors.Contacts.SingleOrDefault(contact =>
                contact.Id == contactId && contact.Status != SensorContactStatus.Lost
            )
            : null;
        ImmutableArray<CommandInterfaceContact> contacts = BuildContacts(projection);
        return new CommandInterfacePresentation
        {
            DataMode = CommandInterfaceDataMode.Live,
            Mode = mode,
            Header = BuildHeader(projection),
            Systems = BuildSystems(projection),
            Telemetry = BuildTelemetry(projection, selectedLocation, selectedContact, mode),
            Actions = BuildActions(projection, selectedLocation, selectedContact, mode),
            Events = BuildEvents(projection, recentEvents),
            Stations = BuildStations(mode),
            MapItems = BuildMapItems(projection),
            MapLinks = BuildMapLinks(projection),
            SelectedLocationId = selectedLocation?.Id,
            Contacts = contacts,
            SelectedContactId = selectedContact?.Id,
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
        StrategicLocationProjection? selectedLocation,
        SensorContactSnapshot? selectedContact,
        CommandInterfaceMode mode
    )
    {
        if (mode == CommandInterfaceMode.Engineering)
        {
            return BuildEngineeringTelemetry(projection.Ship.Engineering);
        }

        if (mode == CommandInterfaceMode.Combat)
        {
            return BuildTacticalTelemetry(projection, selectedContact);
        }

        return BuildStrategicTelemetry(projection, selectedLocation);
    }

    private static ImmutableArray<CommandInterfaceTelemetrySection> BuildStrategicTelemetry(
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
        StrategicLocationProjection? selectedLocation,
        SensorContactSnapshot? selectedContact,
        CommandInterfaceMode mode
    )
    {
        if (mode == CommandInterfaceMode.Engineering)
        {
            return [.. projection.Ship.Engineering.Actions.Select(BuildEngineeringAction)];
        }

        bool activeScanAvailable = IsContactActionAvailable(
            projection,
            selectedContact,
            SensorContactAction.ActiveScan
        );
        bool hailAvailable = IsContactActionAvailable(projection, selectedContact, SensorContactAction.Hail);
        return
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
            LiveAction(
                "active-scan",
                selectedContact is null ? "Active scan" : $"Active scan {ContactLabel(selectedContact)}",
                CommandInterfaceTone.Caution,
                CommandInterfaceIntent.ActiveScan,
                activeScanAvailable,
                selectedContact?.Id,
                ContactActionTooltip(selectedContact, SensorContactAction.ActiveScan, activeScanAvailable)
            ),
            LiveAction(
                "hail",
                selectedContact is null ? "Hail" : $"Hail {ContactLabel(selectedContact)}",
                CommandInterfaceTone.Command,
                CommandInterfaceIntent.Hail,
                hailAvailable,
                selectedContact?.Id,
                ContactActionTooltip(selectedContact, SensorContactAction.Hail, hailAvailable)
            ),
            DisabledAction("fire-phasers", "Fire phasers", CommandInterfaceTone.Critical),
        ];
    }

    private static ImmutableArray<CommandInterfaceEventRow> BuildEvents(
        PlayerProjection projection,
        IReadOnlyList<ActivityEvent> events
    ) =>
        [
            .. events.Select(activity =>
                activity switch
                {
                    ResolvedActivityEvent { Event.Kind: PlayerAdvanceEventKind.TravelArrived } =>
                        new CommandInterfaceEventRow(
                            FormatClock(activity.SimulationTimeMilliseconds),
                            "NAV",
                            "Strategic travel arrived at destination.",
                            CommandInterfaceTone.Navigation
                        ),
                    ResolvedActivityEvent { Event.Kind: PlayerAdvanceEventKind.SystemRepairCompleted } resolved =>
                        new CommandInterfaceEventRow(
                            FormatClock(activity.SimulationTimeMilliseconds),
                            "ENGINEER",
                            $"{SystemLabel(resolved.Event.ShipSystemId)} repair completed.",
                            CommandInterfaceTone.Nominal
                        ),
                    ResolvedActivityEvent { Event.Kind: PlayerAdvanceEventKind.SensorContactDetected } resolved =>
                        SensorEvent(projection, resolved, "Contact detected."),
                    ResolvedActivityEvent { Event.Kind: PlayerAdvanceEventKind.SensorContactStale } resolved =>
                        SensorEvent(projection, resolved, "Contact became stale."),
                    ResolvedActivityEvent { Event.Kind: PlayerAdvanceEventKind.SensorContactReacquired } resolved =>
                        SensorEvent(projection, resolved, "Contact reacquired."),
                    ResolvedActivityEvent { Event.Kind: PlayerAdvanceEventKind.SensorContactLost } resolved =>
                        SensorEvent(projection, resolved, "Contact lost."),
                    ResolvedActivityEvent { Event.Kind: PlayerAdvanceEventKind.ActiveSensorScanCompleted } resolved =>
                        SensorEvent(projection, resolved, "Active scan completed."),
                    ResolvedActivityEvent { Event.Kind: PlayerAdvanceEventKind.ActiveSensorScanInterrupted } resolved =>
                        SensorEvent(projection, resolved, "Active scan interrupted."),
                    HailActivityEvent { Outcome: HailOutcome.Acknowledged } hail => HailEvent(
                        hail,
                        "acknowledged the hail.",
                        CommandInterfaceTone.Nominal
                    ),
                    HailActivityEvent { Outcome: HailOutcome.NoResponse } hail => HailEvent(
                        hail,
                        "did not respond.",
                        CommandInterfaceTone.Caution
                    ),
                    _ => throw new ArgumentOutOfRangeException(nameof(events), activity, "Unknown player activity."),
                }
            ),
        ];

    private static CommandInterfaceEventRow HailEvent(
        HailActivityEvent hail,
        string message,
        CommandInterfaceTone tone
    ) => new(FormatClock(hail.SimulationTimeMilliseconds), "COMMS", $"{hail.ContactLabel} {message}", tone);

    private static CommandInterfaceEventRow SensorEvent(
        PlayerProjection projection,
        ResolvedActivityEvent resolved,
        string message
    ) =>
        new(
            FormatClock(resolved.SimulationTimeMilliseconds),
            "SENSOR",
            $"{DescribeContact(projection, resolved.Event)}: {message}",
            CommandInterfaceTone.Caution
        );

    private static string DescribeContact(PlayerProjection projection, PlayerAdvanceEvent @event)
    {
        if (@event.SensorContactId is not { } contactId)
        {
            return "Contact";
        }

        SensorContactSnapshot? contact = projection.Ship.Sensors.Contacts.SingleOrDefault(candidate =>
            candidate.Id == contactId
        );
        return contact is null ? $"Contact {contactId.Value}" : ContactLabel(contact);
    }

    private static ImmutableArray<CommandInterfaceContact> BuildContacts(PlayerProjection projection) =>
        [
            .. projection
                .Ship.Sensors.Contacts.Where(contact => contact.Status != SensorContactStatus.Lost)
                .OrderBy(contact => contact.Id.Value)
                .Select(contact => new CommandInterfaceContact(
                    contact.Id,
                    ContactLabel(contact),
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

    private static ImmutableArray<CommandInterfaceTelemetrySection> BuildTacticalTelemetry(
        PlayerProjection projection,
        SensorContactSnapshot? selectedContact
    )
    {
        ImmutableArray<CommandInterfaceTelemetrySection>.Builder sections =
            ImmutableArray.CreateBuilder<CommandInterfaceTelemetrySection>();
        if (selectedContact is null)
        {
            sections.Add(
                new CommandInterfaceTelemetrySection(
                    "contact",
                    "CONTACT",
                    CommandInterfaceTone.Muted,
                    [Unavailable("SELECTION")]
                )
            );
        }
        else
        {
            sections.Add(
                new CommandInterfaceTelemetrySection(
                    "contact",
                    ContactLabel(selectedContact).ToUpperInvariant(),
                    selectedContact.Status == SensorContactStatus.Current
                        ? CommandInterfaceTone.Command
                        : CommandInterfaceTone.Caution,
                    BuildContactFields(projection, selectedContact)
                )
            );
        }

        sections.Add(
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
            )
        );
        return sections.ToImmutable();
    }

    private static ImmutableArray<CommandInterfaceField> BuildContactFields(
        PlayerProjection projection,
        SensorContactSnapshot selectedContact
    )
    {
        ImmutableArray<CommandInterfaceField>.Builder fields = ImmutableArray.CreateBuilder<CommandInterfaceField>();
        fields.Add(Available("LOCAL CONTACT ID", selectedContact.Id.Value.ToString(CultureInfo.InvariantCulture)));
        fields.Add(
            Available(
                "STATUS",
                selectedContact.Status.ToString().ToUpperInvariant(),
                selectedContact.Status == SensorContactStatus.Current
                    ? CommandInterfaceTone.Command
                    : CommandInterfaceTone.Caution
            )
        );
        fields.Add(Available("IDENTIFICATION", selectedContact.Identification.ToString().ToUpperInvariant()));
        fields.Add(Available("OBSERVED X", FormatKilometers(selectedContact.LastObservedPosition.XKilometers)));
        fields.Add(Available("OBSERVED Y", FormatKilometers(selectedContact.LastObservedPosition.YKilometers)));
        fields.Add(Available("OBSERVED AT", FormatSeconds(selectedContact.LastObservedAt.Milliseconds)));
        fields.Add(
            Available(
                "OBSERVATION AGE",
                FormatSeconds(projection.SimulationTime.Milliseconds - selectedContact.LastObservedAt.Milliseconds),
                selectedContact.Status == SensorContactStatus.Stale
                    ? CommandInterfaceTone.Caution
                    : CommandInterfaceTone.Neutral
            )
        );
        if (selectedContact.Identification == SensorContactIdentification.Identified)
        {
            fields.Add(Available("VESSEL", selectedContact.KnownVesselDisplayName ?? "UNKNOWN"));
            fields.Add(Available("DESIGN", selectedContact.KnownDesignDisplayName ?? "UNKNOWN"));
        }

        if (projection.Ship.Sensors.ActiveScanContactId == selectedContact.Id)
        {
            fields.Add(
                Available(
                    "ACTIVE SCAN",
                    projection.Ship.Sensors.ActiveScanProgress is double progress
                        ? FormatPercent(progress)
                        : "IN PROGRESS",
                    CommandInterfaceTone.Caution
                )
            );
        }

        return fields.ToImmutable();
    }

    private static string ContactLabel(SensorContactSnapshot contact) =>
        contact.Identification == SensorContactIdentification.Identified
            ? contact.KnownVesselDisplayName ?? contact.KnownDesignDisplayName ?? $"Contact {contact.Id.Value}"
            : $"Contact {contact.Id.Value}";

    private static bool IsContactActionAvailable(
        PlayerProjection projection,
        SensorContactSnapshot? contact,
        SensorContactAction action
    ) =>
        contact is not null
        && projection.Ship.Sensors.ContactActions.Any(candidate =>
            candidate.ContactId == contact.Id && candidate.AvailableActions.Contains(action)
        );

    private static string ContactActionTooltip(
        SensorContactSnapshot? contact,
        SensorContactAction action,
        bool isAvailable
    ) =>
        (contact, action, isAvailable) switch
        {
            (null, SensorContactAction.ActiveScan, _) => "Select a live sensor contact to request an active scan.",
            (null, SensorContactAction.Hail, _) => "Select an identified live sensor contact to hail.",
            (not null, SensorContactAction.ActiveScan, true) => "Request active identification of this contact.",
            (not null, SensorContactAction.Hail, true) => "Request a bounded hail to this identified contact.",
            (not null, SensorContactAction.ActiveScan, false) =>
                "Active scan is unavailable for this contact in the current Core projection.",
            (not null, SensorContactAction.Hail, false) =>
                "Hail is unavailable for this contact in the current Core projection.",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown contact action."),
        };

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
        EngineeringProjection engineering = projection.Ship.Engineering;
        CommandInterfaceTone powerTone = ConditionTone(engineering.GenerationCondition);
        CommandInterfaceTone sensorTone = ConditionTone(engineering.SensorCondition);
        CommandInterfaceTone impulseTone = ConditionTone(engineering.ImpulseCondition);
        bool repairing = engineering.ActiveRepair is not null;
        return new CommandInterfaceEngineeringPresentation(
            [
                Hierarchy("overview", "OVERVIEW", selected: true, CommandInterfaceTone.Engineering),
                Hierarchy("power", "POWER", false, powerTone),
                Hierarchy("sensors", "SENSORS", false, sensorTone),
                Hierarchy("propulsion", "PROPULSION", false, impulseTone),
                Hierarchy(
                    "repairs",
                    "REPAIRS",
                    false,
                    repairing ? CommandInterfaceTone.Caution : CommandInterfaceTone.Neutral,
                    repairing ? 1 : 0
                ),
            ],
            BuildEngineeringComponents(engineering, powerTone, sensorTone, impulseTone),
            [],
            []
        );
    }

    private static ImmutableArray<CommandInterfaceTelemetrySection> BuildEngineeringComponents(
        EngineeringProjection engineering,
        CommandInterfaceTone powerTone,
        CommandInterfaceTone sensorTone,
        CommandInterfaceTone impulseTone
    ) =>
        [
            EngineeringOverview(engineering),
            EngineeringPower(engineering, powerTone),
            EngineeringSensors(engineering, sensorTone),
            EngineeringPropulsion(engineering, impulseTone),
            EngineeringRepair(engineering),
        ];

    private static CommandInterfaceTelemetrySection EngineeringOverview(EngineeringProjection engineering) =>
        new(
            "overview",
            "ENGINEERING OVERVIEW",
            CommandInterfaceTone.Engineering,
            [
                Available("AVAILABLE POWER", FormatPower(engineering.AvailablePower)),
                Available("RESERVE", FormatPower(engineering.Reserve)),
                Available("SENSOR RANGE", FormatKilometers(engineering.EffectivePassiveSensorRange.Value)),
                Available("MAX TACTICAL SPEED", FormatSpeed(engineering.EffectiveMaximumTacticalSpeed.Value)),
            ]
        );

    private static CommandInterfaceTelemetrySection EngineeringPower(
        EngineeringProjection engineering,
        CommandInterfaceTone tone
    ) =>
        new(
            "power",
            "POWER GENERATION",
            tone,
            [
                Available("NOMINAL", FormatPower(engineering.NominalGeneration)),
                Available("AVAILABLE", FormatPower(engineering.AvailablePower), tone),
                Available("CONDITION", FormatCondition(engineering.GenerationCondition), tone),
                Available("RESERVE", FormatPower(engineering.Reserve)),
            ]
        );

    private static CommandInterfaceTelemetrySection EngineeringSensors(
        EngineeringProjection engineering,
        CommandInterfaceTone tone
    ) =>
        new(
            "sensors",
            "SENSORS",
            tone,
            [
                Available("CONDITION", FormatCondition(engineering.SensorCondition), tone),
                Available("ALLOCATION", FormatPower(engineering.SensorAllocation)),
                Available("CAPABILITY", FormatPercent(engineering.SensorCapability), tone),
                Available("PASSIVE RANGE", FormatKilometers(engineering.EffectivePassiveSensorRange.Value)),
            ]
        );

    private static CommandInterfaceTelemetrySection EngineeringPropulsion(
        EngineeringProjection engineering,
        CommandInterfaceTone tone
    ) =>
        new(
            "propulsion",
            "IMPULSE PROPULSION",
            tone,
            [
                Available("CONDITION", FormatCondition(engineering.ImpulseCondition), tone),
                Available("ALLOCATION", FormatPower(engineering.ImpulseAllocation)),
                Available("CAPABILITY", FormatPercent(engineering.ImpulseCapability), tone),
                Available("MAX TACTICAL SPEED", FormatSpeed(engineering.EffectiveMaximumTacticalSpeed.Value)),
            ]
        );

    private static CommandInterfaceTelemetrySection EngineeringRepair(EngineeringProjection engineering) =>
        new(
            "repairs",
            "ACTIVE REPAIR",
            engineering.ActiveRepair is null ? CommandInterfaceTone.Neutral : CommandInterfaceTone.Caution,
            [
                Available(
                    "TARGET",
                    engineering.ActiveRepair is null ? "NONE" : SystemLabel(engineering.ActiveRepair.TargetSystem)
                ),
                Available(
                    "PROGRESS",
                    engineering.ActiveRepair is null ? "INACTIVE" : FormatPercent(engineering.ActiveRepair.Progress)
                ),
                Available(
                    "COMPLETION",
                    engineering.ActiveRepair is null
                        ? "NOT SCHEDULED"
                        : FormatSeconds(engineering.ActiveRepair.ExpectedCompletion.Milliseconds)
                ),
            ]
        );

    private static ImmutableArray<CommandInterfaceTelemetrySection> BuildEngineeringTelemetry(
        EngineeringProjection engineering
    ) =>
        [
            new CommandInterfaceTelemetrySection(
                "connected-loads",
                "CONNECTED LOADS",
                CommandInterfaceTone.Engineering,
                [
                    Available("SENSORS", FormatPower(engineering.SensorAllocation)),
                    Available("IMPULSE PROPULSION", FormatPower(engineering.ImpulseAllocation)),
                ]
            ),
            new CommandInterfaceTelemetrySection(
                "power-allocation",
                "POWER ALLOCATION SUMMARY",
                CommandInterfaceTone.Engineering,
                [
                    Available("NOMINAL GENERATION", FormatPower(engineering.NominalGeneration)),
                    Available("AVAILABLE POWER", FormatPower(engineering.AvailablePower)),
                    Available("SENSORS", FormatPower(engineering.SensorAllocation)),
                    Available("PROPULSION", FormatPower(engineering.ImpulseAllocation)),
                    Available("RESERVE", FormatPower(engineering.Reserve)),
                ]
            ),
        ];

    private static CommandInterfaceAction BuildEngineeringAction(EngineeringActionProjection action)
    {
        (string id, string label) = action.Action switch
        {
            EngineeringAction.Balanced => ("allocate-balanced", "Balance power allocation"),
            EngineeringAction.PrioritizeSensors => ("prioritize-sensors", "Prioritize sensors"),
            EngineeringAction.PrioritizePropulsion => ("prioritize-propulsion", "Prioritize propulsion"),
            EngineeringAction.BeginSensorRepair => ("repair-sensors", "Begin sensor repair"),
            EngineeringAction.BeginImpulseRepair => ("repair-propulsion", "Begin impulse repair"),
            EngineeringAction.ReturnToCommand => ("return-command", "Return to Command Deck"),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action.Action, "Unknown Engineering action."),
        };
        return new CommandInterfaceAction(
            id,
            label,
            action.IsAvailable ? CommandInterfaceTone.Engineering : CommandInterfaceTone.Muted,
            action.IsAvailable
                ? CommandInterfaceActionAvailability.Submittable
                : CommandInterfaceActionAvailability.Disabled,
            Tooltip: EngineeringActionTooltip(action),
            EngineeringCommand: action.Action
        );
    }

    private static string EngineeringActionTooltip(EngineeringActionProjection action) =>
        (action.IsAvailable, action.UnavailableReason) switch
        {
            (true, _) => "Submit this Engineering intent for authoritative Core validation.",
            (false, EngineeringActionUnavailableReason.CurrentSpeedTooHigh) =>
                "Unavailable: current speed exceeds the resulting propulsion margin.",
            (false, EngineeringActionUnavailableReason.RepairAlreadyActive) =>
                "Unavailable: another system repair is active.",
            (false, EngineeringActionUnavailableReason.SystemAlreadyNominal) =>
                "Unavailable: this system is already nominal.",
            _ => "Unavailable: Core does not currently support this Engineering action.",
        };

    private static CommandInterfaceHierarchyRow Hierarchy(
        string id,
        string label,
        bool selected,
        CommandInterfaceTone tone,
        int attention = 0
    ) => new(id, null, label, selected, attention, CommandInterfaceAvailability.Available, tone);

    private static CommandInterfaceTone ConditionTone(SystemCondition condition) =>
        condition.Status == SystemConditionStatus.Nominal ? CommandInterfaceTone.Nominal : CommandInterfaceTone.Caution;

    private static string FormatCondition(SystemCondition condition) =>
        $"{condition.Status.ToString().ToUpperInvariant()} / {FormatPercent(condition.Value)}";

    private static string FormatPower(PowerUnits power) =>
        $"{power.Value.ToString(CultureInfo.InvariantCulture)} units";

    private static string SystemLabel(ShipSystemId? system) =>
        system switch
        {
            ShipSystemId id when id == ShipSystemId.PowerGeneration => "Power generation",
            ShipSystemId id when id == ShipSystemId.Sensors => "Sensors",
            ShipSystemId id when id == ShipSystemId.ImpulsePropulsion => "Impulse propulsion",
            _ => "System",
        };

    private static CommandInterfaceAction LiveAction(
        string id,
        string label,
        CommandInterfaceTone tone,
        CommandInterfaceIntent intent,
        bool isAvailable,
        SensorContactId? focusedContactId = null,
        string? tooltip = null
    ) =>
        new(
            id,
            label,
            isAvailable ? tone : CommandInterfaceTone.Muted,
            isAvailable ? CommandInterfaceActionAvailability.Submittable : CommandInterfaceActionAvailability.Disabled,
            intent,
            focusedContactId,
            tooltip
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
