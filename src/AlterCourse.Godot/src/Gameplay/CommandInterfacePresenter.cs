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
    // Bounds the retained-report section so an unbounded knowledge history can never grow the inspector
    // panel past the 1600x900 practical minimum the layout tests pin; the surplus is summarised in one row.
    private const int MaxKnownContactReportRows = 8;

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
        CommandInterfaceMode mode = CommandInterfaceMode.Travel,
        OwnShipActionBinding? binding = null
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
            // Every live action is an own-ship action, so each carries the owner and generation it was presented for.
            Actions =
            [
                .. BuildActions(projection, selectedLocation, selectedContact, mode)
                    .Select(action => action with { Binding = binding }),
            ],
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
            CombatTarget = FindCombatTarget(projection, selectedContact),
            Binding = binding,
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
            new("shields", "SHIELDS", CombatCondition(projection.Ship.Combat.Shields)),
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
            new("weapons", "WEAPONS", CombatCondition(projection.Ship.Combat.Weapon)),
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
            return
            [
                .. BuildTacticalTelemetry(projection, selectedContact),
                BuildCombatTelemetry(projection, selectedContact),
            ];
        }

        return BuildStrategicTelemetry(projection, selectedLocation);
    }

    private static ImmutableArray<CommandInterfaceTelemetrySection> BuildStrategicTelemetry(
        PlayerProjection projection,
        StrategicLocationProjection? selectedLocation
    )
    {
        return
        [
            new CommandInterfaceTelemetrySection(
                "destination",
                "DESTINATION",
                CommandInterfaceTone.Navigation,
                BuildDestinationFields(selectedLocation)
            ),
            new CommandInterfaceTelemetrySection(
                "strategic",
                "ROUTE",
                CommandInterfaceTone.Navigation,
                BuildRouteFields(projection.Strategic)
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
            new CommandInterfaceTelemetrySection(
                "last-known-contacts",
                "LAST KNOWN CONTACTS",
                CommandInterfaceTone.Muted,
                BuildKnownContactReportFields(projection.Strategic)
            ),
        ];
    }

    private static ImmutableArray<CommandInterfaceField> BuildDestinationFields(
        StrategicLocationProjection? selectedLocation
    ) =>
        selectedLocation is null
            ? [Unavailable("DESTINATION")]
            :
            [
                Available("DESTINATION", selectedLocation.DisplayName, CommandInterfaceTone.Navigation),
                Available("LOCATION ID", selectedLocation.Id.Value),
                Unavailable("CLASS"),
                Unavailable("POPULATION"),
            ];

    private static ImmutableArray<CommandInterfaceField> BuildRouteFields(StrategicProjection strategic) =>
        strategic.Travel is TravelProjection travel
            ?
            [
                Available("STATE", "UNDERWAY", CommandInterfaceTone.Navigation),
                Available("ORIGIN", FindLocationName(strategic, travel.Origin)),
                Available("DESTINATION", FindLocationName(strategic, travel.Destination)),
                Available("DEPARTURE", FormatSeconds(travel.Departure.Milliseconds)),
                Available("ARRIVAL", FormatSeconds(travel.ExpectedArrival.Milliseconds)),
                Available("ACTIVE", travel.IsActive ? "YES" : "NO"),
            ]
            :
            [
                Available("STATE", "AT LOCATION", CommandInterfaceTone.Nominal),
                Available("LOCATION", strategic.CurrentLocation?.DisplayName ?? "UNKNOWN"),
                Unavailable("ARRIVAL"),
            ];

    /// <summary>Builds the retained last-observed contact rows shown beside, and never as, live tactical truth.</summary>
    /// <remarks>
    /// Sourced only from <see cref="StrategicProjection.KnownContactReports"/>, so nothing here can drift toward
    /// hidden world truth: a report keeps its recorded location, time, and position after the contact goes Lost or
    /// the ship travels away. Lost reports are deliberately retained even though the tactical surface drops them —
    /// that divergence is the player-visible proof of the durable knowledge boundary. Every row states "Last seen"
    /// and carries the observation time so it can never be read as a current position.
    /// </remarks>
    private static ImmutableArray<CommandInterfaceField> BuildKnownContactReportFields(StrategicProjection strategic)
    {
        if (strategic.KnownContactReports.Count == 0)
        {
            return [Available("REPORTS", "No retained contact reports", CommandInterfaceTone.Muted)];
        }

        ImmutableArray<CommandInterfaceField>.Builder fields = ImmutableArray.CreateBuilder<CommandInterfaceField>();
        foreach (
            StrategicContactReportProjection report in strategic.KnownContactReports.Take(MaxKnownContactReportRows)
        )
        {
            fields.Add(
                new CommandInterfaceField(
                    FormatReportLabel(report),
                    FormatReportValue(strategic, report),
                    CommandInterfaceAvailability.Available,
                    ReportTone(report.Status)
                )
            );
        }

        int hidden = strategic.KnownContactReports.Count - fields.Count;
        if (hidden > 0)
        {
            fields.Add(
                Available(
                    "MORE",
                    string.Create(CultureInfo.InvariantCulture, $"{hidden} further retained report(s)"),
                    CommandInterfaceTone.Muted
                )
            );
        }

        return fields.ToImmutable();
    }

    private static string FormatReportLabel(StrategicContactReportProjection report) =>
        report.Identification == SensorContactIdentification.Identified
        && report.KnownVesselDisplayName is { Length: > 0 } vessel
            ? vessel
            // Matches the tactical contact naming exactly, so an unidentified report and its tactical marker
            // read as the same contact rather than two separate sightings.
            : string.Create(CultureInfo.InvariantCulture, $"Contact {report.ContactId.Value}");

    private static string FormatReportValue(StrategicProjection strategic, StrategicContactReportProjection report)
    {
        string value = string.Create(
            CultureInfo.InvariantCulture,
            $"Last seen at {FindReportLocationName(strategic, report.ObservedAtLocationId)} · t={FormatSeconds(report.LastObservedAt.Milliseconds)} · {FormatReportStatus(report.Status)}"
        );
        if (
            report.Identification == SensorContactIdentification.Identified
            && report.KnownDesignDisplayName is { Length: > 0 } design
        )
        {
            value += $" · {design}";
        }

        return value
            + $" · {FormatKilometers(report.LastObservedPosition.XKilometers)} / {FormatKilometers(report.LastObservedPosition.YKilometers)}";
    }

    private static string FormatReportStatus(SensorContactStatus status) => status.ToString().ToUpperInvariant();

    private static CommandInterfaceTone ReportTone(SensorContactStatus status) =>
        status switch
        {
            SensorContactStatus.Current => CommandInterfaceTone.Nominal,
            SensorContactStatus.Stale => CommandInterfaceTone.Caution,
            _ => CommandInterfaceTone.Muted,
        };

    private static ImmutableArray<CommandInterfaceAction> BuildActions(
        PlayerProjection projection,
        StrategicLocationProjection? selectedLocation,
        SensorContactSnapshot? selectedContact,
        CommandInterfaceMode mode
    )
    {
        if (mode == CommandInterfaceMode.Engineering)
        {
            EngineeringProjection engineering = projection.Ship.Engineering;
            return [.. engineering.Actions.Select(action => BuildEngineeringAction(engineering, action))];
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
                "Apply tactical course",
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
            BuildFireAction(projection, selectedContact),
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
                            $"{SystemLabel(resolved.Event.SystemKind)} repair completed.",
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
                    ResolvedActivityEvent resolved => new CommandInterfaceEventRow(
                        FormatClock(activity.SimulationTimeMilliseconds),
                        "TACTICAL",
                        CombatEventText(resolved.Event),
                        CommandInterfaceTone.Caution
                    ),
                    _ => throw new ArgumentOutOfRangeException(nameof(events), activity, "Unknown player activity."),
                }
            ),
        ];

    private static CombatTargetProjection? FindCombatTarget(
        PlayerProjection projection,
        SensorContactSnapshot? contact
    ) =>
        contact is null
            ? null
            : projection.Ship.Combat.Targets.SingleOrDefault(target => target.ContactId == contact.Id);

    private static CommandInterfaceAction BuildFireAction(PlayerProjection projection, SensorContactSnapshot? contact)
    {
        CombatTargetProjection? target = FindCombatTarget(projection, contact);
        bool available = target?.Outcome == FireDirectedEnergyOutcome.Accepted && target.AimKinds.Count > 0;
        return new(
            "fire-phasers",
            "Fire directed energy",
            CommandInterfaceTone.Critical,
            available ? CommandInterfaceActionAvailability.Submittable : CommandInterfaceActionAvailability.Disabled,
            CommandInterfaceIntent.FireDirectedEnergy,
            contact?.Id,
            FireReason(target?.Outcome ?? FireDirectedEnergyOutcome.ContactNotFound)
        );
    }

    private static CommandInterfaceTelemetrySection BuildCombatTelemetry(
        PlayerProjection projection,
        SensorContactSnapshot? contact
    )
    {
        CombatProjection combat = projection.Ship.Combat;
        CombatTargetProjection? target = FindCombatTarget(projection, contact);
        return new(
            "combat",
            "FIRE CONTROL / OWN SYSTEMS",
            CommandInterfaceTone.Critical,
            [
                Available("FIRE CONTROL", FireReason(target?.Outcome ?? FireDirectedEnergyOutcome.ContactNotFound)),
                Available("KNOWN RANGE", target?.Range is { } range ? FormatKilometers(range.Value) : "UNKNOWN"),
                Available(
                    "WEAPON RANGE",
                    combat.WeaponRange is { } weaponRange ? FormatKilometers(weaponRange.Value) : "UNAVAILABLE"
                ),
                Available("COOLDOWN REMAINING", FormatSeconds(combat.RemainingCooldown.Milliseconds)),
                .. CombatSystemFields("SHIELD", combat.Shields),
                .. CombatSystemFields("WEAPON", combat.Weapon),
            ]
        );
    }

    // An absent installation is stated as unavailable rather than rendered as a 0% placeholder; the three field
    // labels stay so the panel keeps its layout whether or not the capability is installed.
    private static ImmutableArray<CommandInterfaceField> CombatSystemFields(
        string prefix,
        CombatSystemStatusProjection? system
    ) =>
        system is null
            ?
            [
                Available($"{prefix} CONDITION", "UNAVAILABLE", CommandInterfaceTone.Muted),
                Available($"{prefix} POWER", "UNAVAILABLE", CommandInterfaceTone.Muted),
                Available($"{prefix} CAPABILITY", "UNAVAILABLE", CommandInterfaceTone.Muted),
            ]
            :
            [
                Available($"{prefix} CONDITION", FormatCondition(system.Condition)),
                Available($"{prefix} POWER", FormatPower(system.Allocation)),
                Available($"{prefix} CAPABILITY", FormatPercent(system.Capability)),
            ];

    private static CommandInterfaceField CombatCondition(CombatSystemStatusProjection? system) =>
        system is null
            ? Available("CONDITION", "UNAVAILABLE", CommandInterfaceTone.Muted)
            : Available("CONDITION", FormatCondition(system.Condition));

    /// <summary>Formats Core's typed fire outcome without recreating its legality checks.</summary>
    public static string FireReason(FireDirectedEnergyOutcome outcome) =>
        outcome switch
        {
            FireDirectedEnergyOutcome.Accepted => "Ready to fire at the selected subsystem.",
            FireDirectedEnergyOutcome.ContactNotFound => "Select a live contact to fire.",
            FireDirectedEnergyOutcome.ContactNotCurrent => "Contact is not current.",
            FireDirectedEnergyOutcome.ContactNotIdentified => "Contact is not identified.",
            FireDirectedEnergyOutcome.NotAtSameLocation => "No shared tactical location.",
            FireDirectedEnergyOutcome.OutOfRange => "Contact is out of weapon range.",
            FireDirectedEnergyOutcome.WeaponUnpowered => "Weapon is unpowered.",
            FireDirectedEnergyOutcome.WeaponOffline => "Weapon is offline or absent.",
            FireDirectedEnergyOutcome.CooldownActive => "Weapon cooldown is active.",
            FireDirectedEnergyOutcome.UnsupportedSystem => "Target subsystem is unavailable.",
            FireDirectedEnergyOutcome.TimeLimitExceeded => "Simulation time limit prevents firing.",
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unknown fire outcome."),
        };

    /// <summary>Describes only the qualitative or player-owned consequences admitted by Core.</summary>
    public static string CombatEventText(PlayerAdvanceEvent @event) =>
        @event.Kind switch
        {
            PlayerAdvanceEventKind.DirectedEnergyFired => "Directed-energy shot fired.",
            PlayerAdvanceEventKind.ShieldImpact => "Observed shield impact.",
            PlayerAdvanceEventKind.SubsystemPenetration => $"Observed penetration to {SystemLabel(@event.SystemKind)}.",
            PlayerAdvanceEventKind.OwnSystemDamaged => $"Own {SystemLabel(@event.SystemKind)} damaged.",
            PlayerAdvanceEventKind.SystemRepairInterrupted =>
                $"Own {SystemLabel(@event.SystemKind)} repair interrupted.",
            PlayerAdvanceEventKind.PowerBrownout => "Own power allocation reduced by brownout.",
            PlayerAdvanceEventKind.ForcedDeceleration => "Own speed reduced by propulsion capability.",
            _ => throw new ArgumentOutOfRangeException(nameof(@event), @event.Kind, "Unknown combat event."),
        };

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
        bool repairing = engineering.ActiveRepair is not null;

        // Rows are exactly Core's installations in Core's order, between the fixed overview and repairs rows; an
        // absent installation has no row. Keys carry the installed identity, never a position or label.
        return new CommandInterfaceEngineeringPresentation(
            [
                Hierarchy("overview", "OVERVIEW", selected: true, CommandInterfaceTone.Engineering),
                .. engineering.Systems.Select(row =>
                    Hierarchy(
                        SystemKey(row.Id),
                        EngineeringKindPresentation.HierarchyLabel(row),
                        false,
                        ConditionTone(row.Condition)
                    )
                ),
                Hierarchy(
                    "repairs",
                    "REPAIRS",
                    false,
                    repairing ? CommandInterfaceTone.Caution : CommandInterfaceTone.Neutral,
                    repairing ? 1 : 0
                ),
            ],
            [
                EngineeringOverview(engineering),
                .. engineering.Systems.Select(row => EngineeringSystemSection(engineering, row)),
                EngineeringRepair(engineering),
            ],
            [],
            []
        );
    }

    /// <summary>Gets the stable hierarchy and schematic key of one installation.</summary>
    public static string SystemKey(InstalledSystemId id) =>
        string.Create(CultureInfo.InvariantCulture, $"system:{id.Value}");

    /// <summary>Gets the stable action key of one Engineering operation and optional installation.</summary>
    public static string EngineeringActionKey(EngineeringOperation operation, InstalledSystemId? target) =>
        (operation, target) switch
        {
            (EngineeringOperation.Balance, null) => "balance",
            (EngineeringOperation.ReturnToCommand, null) => "return-command",
            (EngineeringOperation.Prioritize, { } id) => string.Create(
                CultureInfo.InvariantCulture,
                $"prioritize:{id.Value}"
            ),
            (EngineeringOperation.BeginRepair, { } id) => string.Create(
                CultureInfo.InvariantCulture,
                $"repair:{id.Value}"
            ),
            _ => throw new ArgumentOutOfRangeException(
                nameof(operation),
                operation,
                "Engineering operation and target do not form a known action."
            ),
        };

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

    /// <summary>
    /// Builds one installation's inspector section. The title is the authored component label; the field set comes
    /// from the presentation table's layout, and every value is a Core-projected row fact or ship-level total.
    /// </summary>
    private static CommandInterfaceTelemetrySection EngineeringSystemSection(
        EngineeringProjection engineering,
        InstalledSystemProjection row
    )
    {
        CommandInterfaceTone tone = ConditionTone(row.Condition);
        ImmutableArray<CommandInterfaceField> fields = EngineeringKindPresentation.Layout(row) switch
        {
            EngineeringKindPresentation.FieldLayout.Generation =>
            [
                Available("NOMINAL", FormatPower(engineering.NominalGeneration)),
                Available("AVAILABLE", FormatPower(engineering.AvailablePower), tone),
                Available("CONDITION", FormatCondition(row.Condition), tone),
                Available("RESERVE", FormatPower(engineering.Reserve)),
            ],
            EngineeringKindPresentation.FieldLayout.Sensors =>
            [
                Available("CONDITION", FormatCondition(row.Condition), tone),
                Available("ALLOCATION", FormatOptionalPower(row.Allocation)),
                Available("CAPABILITY", FormatOptionalPercent(row.Capability), tone),
                Available("PASSIVE RANGE", FormatKilometers(engineering.EffectivePassiveSensorRange.Value)),
            ],
            EngineeringKindPresentation.FieldLayout.ImpulsePropulsion =>
            [
                Available("CONDITION", FormatCondition(row.Condition), tone),
                Available("ALLOCATION", FormatOptionalPower(row.Allocation)),
                Available("CAPABILITY", FormatOptionalPercent(row.Capability), tone),
                Available("MAX TACTICAL SPEED", FormatSpeed(engineering.EffectiveMaximumTacticalSpeed.Value)),
            ],
            _ when row.NominalDemand is null => [Available("CONDITION", FormatCondition(row.Condition))],
            _ =>
            [
                Available("CONDITION", FormatCondition(row.Condition)),
                Available("ALLOCATION", FormatOptionalPower(row.Allocation)),
                Available("DEMAND", FormatOptionalPower(row.NominalDemand)),
                Available("CAPABILITY", FormatOptionalPercent(row.Capability)),
            ],
        };
        return new(SystemKey(row.Id), row.ComponentLabel.ToUpperInvariant(), tone, fields);
    }

    private static CommandInterfaceTelemetrySection EngineeringRepair(EngineeringProjection engineering) =>
        new(
            "repairs",
            "ACTIVE REPAIR",
            engineering.ActiveRepair is null ? CommandInterfaceTone.Neutral : CommandInterfaceTone.Caution,
            [
                Available(
                    "TARGET",
                    engineering.ActiveRepair is not { } repair
                        ? "NONE"
                        : EngineeringKindPresentation.TargetLabel(repair.TargetKind, repair.TargetLabel)
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
    )
    {
        InstalledSystemProjection[] consumers = [.. engineering.Systems.Where(row => row.Allocation is not null)];
        return
        [
            new CommandInterfaceTelemetrySection(
                "connected-loads",
                "CONNECTED LOADS",
                CommandInterfaceTone.Engineering,
                [
                    .. consumers.Select(row =>
                        Available(
                            EngineeringKindPresentation.ConnectedLoadLabel(row),
                            FormatOptionalPower(row.Allocation)
                        )
                    ),
                ]
            ),
            new CommandInterfaceTelemetrySection(
                "power-allocation",
                "POWER ALLOCATION SUMMARY",
                CommandInterfaceTone.Engineering,
                [
                    Available("NOMINAL GENERATION", FormatPower(engineering.NominalGeneration)),
                    Available("AVAILABLE POWER", FormatPower(engineering.AvailablePower)),
                    .. consumers.Select(row =>
                        Available(
                            EngineeringKindPresentation.AllocationSummaryLabel(row),
                            FormatOptionalPower(row.Allocation)
                        )
                    ),
                    Available("RESERVE", FormatPower(engineering.Reserve)),
                ]
            ),
        ];
    }

    /// <summary>
    /// Formats one Core action as a keyed button. The label is looked up from the target row's kind; availability,
    /// reason, order, and target all come from Core unchanged.
    /// </summary>
    private static CommandInterfaceAction BuildEngineeringAction(
        EngineeringProjection engineering,
        EngineeringActionProjection action
    )
    {
        InstalledSystemProjection? target = action.Target is { } id
            ? engineering.Systems.Single(row => row.Id == id)
            : null;
        string label = (action.Operation, target) switch
        {
            (EngineeringOperation.Balance, _) => "Balance power allocation",
            (EngineeringOperation.Prioritize, { } row) => EngineeringKindPresentation.PrioritizeLabel(row),
            (EngineeringOperation.BeginRepair, { } row) => EngineeringKindPresentation.RepairLabel(row),
            (EngineeringOperation.ReturnToCommand, _) => "Return to Command Deck",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "Unknown Engineering action."),
        };
        return new CommandInterfaceAction(
            EngineeringActionKey(action.Operation, action.Target),
            label,
            action.IsAvailable ? CommandInterfaceTone.Engineering : CommandInterfaceTone.Muted,
            action.IsAvailable
                ? CommandInterfaceActionAvailability.Submittable
                : CommandInterfaceActionAvailability.Disabled,
            Tooltip: EngineeringActionTooltip(action),
            EngineeringOperation: action.Operation,
            EngineeringTarget: action.Target
        );
    }

    private static string EngineeringActionTooltip(EngineeringActionProjection action) =>
        (action.IsAvailable, action.UnavailableReason) switch
        {
            (true, _) => "Apply this Engineering command.",
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

    private static string SystemLabel(ShipSystemKind? system) => EngineeringKindPresentation.TargetLabel(system);

    private static string FormatOptionalPower(PowerUnits? power) => power is { } value ? FormatPower(value) : "NONE";

    private static string FormatOptionalPercent(double? fraction) =>
        fraction is { } value ? FormatPercent(value) : "NONE";

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

    // Unlike FindLocationName, this never throws: a retained report can name a location the current
    // strategic projection no longer lists, and losing the whole command deck to one unresolvable
    // historical id would be a far worse failure than showing the raw id.
    private static string FindReportLocationName(StrategicProjection strategic, LocationId id) =>
        strategic.Locations.FirstOrDefault(location => location.Id == id)?.DisplayName ?? id.Value;

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
