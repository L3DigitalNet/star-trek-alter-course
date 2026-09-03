using System.Collections.Immutable;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Creates deterministic, display-only fixtures for the three approved command-interface references.</summary>
public static class CommandInterfacePreviewFixtures
{
    private static readonly LocationId BetazedId = new("betazed");
    private static readonly LocationId CeltrisId = new("celtris-iii");
    private static readonly LocationId Hd38291Id = new("hd-38291");

    /// <summary>Creates one explicitly selected preview; live data must come from <see cref="CommandInterfacePresenter"/>.</summary>
    public static CommandInterfacePresentation Create(CommandInterfaceDataMode dataMode) =>
        dataMode switch
        {
            CommandInterfaceDataMode.TravelPreview => CreateTravel(),
            CommandInterfaceDataMode.CombatPreview => CreateCombat(),
            CommandInterfaceDataMode.EngineeringPreview => CreateEngineering(),
            CommandInterfaceDataMode.Live => throw new ArgumentException(
                "Live presentation requires a fresh Core PlayerProjection.",
                nameof(dataMode)
            ),
            _ => throw new ArgumentOutOfRangeException(
                nameof(dataMode),
                dataMode,
                "Unknown command-interface data mode."
            ),
        };

    private static CommandInterfacePresentation CreateTravel() =>
        new()
        {
            DataMode = CommandInterfaceDataMode.TravelPreview,
            Mode = CommandInterfaceMode.Travel,
            Header =
            [
                Field("STATION", "COMMAND DECK / TRAVEL", CommandInterfaceTone.Command),
                Field("VESSEL", "USS ENTERPRISE NCC-1701-D"),
                Field("STARDATE", "47214.3", CommandInterfaceTone.Muted),
                Field("ALERT", "NORMAL", CommandInterfaceTone.Nominal),
                Field("VELOCITY", "WARP 6.2", CommandInterfaceTone.Navigation),
                Field("SIMULATION", "1×", CommandInterfaceTone.Muted),
                Field("CLOCK", "14:32:08", CommandInterfaceTone.Muted),
            ],
            Systems = TravelSystems(),
            Telemetry =
            [
                Section(
                    "destination",
                    "DESTINATION",
                    CommandInterfaceTone.Navigation,
                    Field("NAME", "BETAZED", CommandInterfaceTone.Navigation),
                    Field("AFFILIATION", "Federation member world"),
                    Field("CLASS", "M"),
                    Field("POPULATION", "5.6B"),
                    Field("DISTANCE", "4.7 ly"),
                    Field("ETA", "00:17:42"),
                    Field("ARRIVAL", "14:49:50")
                ),
                Section(
                    "route",
                    "ROUTE",
                    CommandInterfaceTone.Navigation,
                    Field("PROFILE", "DIRECT / WARP 6.2", CommandInterfaceTone.Navigation),
                    Field("HAZARD", "1 minor gravimetric hazard", CommandInterfaceTone.Caution),
                    Field("CROSSINGS", "No restricted space crossings"),
                    Field("FUEL RESERVE", "82%")
                ),
            ],
            Actions =
            [
                PreviewAction("adjust-course", "Adjust course", CommandInterfaceTone.Navigation),
                PreviewAction("change-speed", "Change speed", CommandInterfaceTone.Command),
                PreviewAction("open-navigation", "Open Navigation station", CommandInterfaceTone.Navigation),
                PreviewAction("scan-contact", "Scan unidentified contact", CommandInterfaceTone.Caution),
                PreviewAction("set-course", "Set course…", CommandInterfaceTone.Navigation),
                PreviewAction("hail-contact", "Hail contact", CommandInterfaceTone.Command),
                PreviewAction("yellow-alert", "Yellow alert", CommandInterfaceTone.Caution),
            ],
            Events =
            [
                Event("14:31:02", "NAV", "Course to Betazed laid in. Warp 6.2."),
                Event(
                    "14:31:27",
                    "SENSORS",
                    "Unidentified contact detected at 12.8 light-minutes.",
                    CommandInterfaceTone.Caution
                ),
                Event("14:31:44", "OPS", "Estimated arrival 14:49:50."),
                Event("14:32:03", "ENGINEER", "Power margin stable at +18%.", CommandInterfaceTone.Caution),
                Event("14:32:08", "COMMAND", "Continuing current course.", CommandInterfaceTone.Command),
            ],
            Stations = Stations("command"),
            MapItems =
            [
                MapLocation(BetazedId, "BETAZED", 72, 34, CommandInterfaceTone.Nominal),
                MapLocation(Hd38291Id, "HD 38291", 38, 30, CommandInterfaceTone.Muted),
                new CommandInterfaceMapItem(
                    "enterprise",
                    "ENTERPRISE / WARP 6.2",
                    CommandInterfaceMapItemKind.PlayerShip,
                    22,
                    63,
                    CommandInterfaceTone.Command
                ),
                new CommandInterfaceMapItem(
                    "contact-01",
                    "CONTACT 01 / UNIDENTIFIED / 12.8 lm",
                    CommandInterfaceMapItemKind.Contact,
                    50,
                    58,
                    CommandInterfaceTone.Caution
                ),
            ],
            MapLinks = [new("enterprise", BetazedId.Value, CommandInterfaceTone.Navigation, "WARP 6.2")],
            SelectedLocationId = BetazedId,
        };

    private static CommandInterfacePresentation CreateCombat() =>
        new()
        {
            DataMode = CommandInterfaceDataMode.CombatPreview,
            Mode = CommandInterfaceMode.Combat,
            Header =
            [
                Field("STATION", "COMMAND DECK / COMBAT", CommandInterfaceTone.Critical),
                Field("VESSEL", "USS ENTERPRISE NCC-1701-D"),
                Field("STARDATE", "47214.3", CommandInterfaceTone.Muted),
                Field("ALERT", "RED ALERT", CommandInterfaceTone.Critical),
                Field("VELOCITY", "IMPULSE 0.68c", CommandInterfaceTone.Command),
                Field("SIMULATION", "0.5×", CommandInterfaceTone.Caution),
                Field("CLOCK", "18:07:43", CommandInterfaceTone.Muted),
            ],
            Systems = CombatSystems(),
            Telemetry =
            [
                Section(
                    "selected-contact",
                    "SELECTED CONTACT",
                    CommandInterfaceTone.Critical,
                    Field("IDENTITY", "GALOR-CLASS CRUISER", CommandInterfaceTone.Critical),
                    Field("AFFILIATION", "Cardassian Union"),
                    Field("RANGE", "7.3 Mm"),
                    Field("BEARING", "037°"),
                    Field("SHIELDS", "61%", CommandInterfaceTone.Caution),
                    Field("WEAPONS", "CHARGED", CommandInterfaceTone.Critical),
                    Field("INTENT", "HOSTILE", CommandInterfaceTone.Critical)
                ),
                Section(
                    "tactical-summary",
                    "TACTICAL SUMMARY",
                    CommandInterfaceTone.Caution,
                    Field("FIRE SOLUTION", "93%", CommandInterfaceTone.Caution),
                    Field("ARC", "Forward shield strongest"),
                    Field("PHASERS", "Banks in range"),
                    Field("PHOTON TORPEDOES", "42"),
                    Field("COMMS", "Channel open")
                ),
            ],
            Actions =
            [
                PreviewAction("hail-target", "Hail target", CommandInterfaceTone.Command),
                PreviewAction("fire-phasers", "Fire phasers", CommandInterfaceTone.Critical),
                PreviewAction("fire-torpedo", "Fire photon torpedo", CommandInterfaceTone.Critical),
                PreviewAction("target-subsystem", "Target subsystem…", CommandInterfaceTone.Caution),
                PreviewAction("open-tactical", "Open Tactical station", CommandInterfaceTone.Command),
                PreviewAction("evasive-maneuver", "Evasive maneuver…", CommandInterfaceTone.Caution),
                PreviewAction("reinforce-shields", "Reinforce forward shields", CommandInterfaceTone.Caution),
                PreviewAction("withdraw", "Disengage / withdraw", CommandInterfaceTone.Command),
            ],
            Events =
            [
                Event(
                    "18:06:51",
                    "SENSORS",
                    "Cardassian contacts changed course to intercept.",
                    CommandInterfaceTone.Caution
                ),
                Event(
                    "18:07:03",
                    "TACTICAL",
                    "Galor-01 raised shields and charged weapons.",
                    CommandInterfaceTone.Critical
                ),
                Event("18:07:15", "COMMAND", "Red alert. Shields raised.", CommandInterfaceTone.Command),
                Event(
                    "18:07:29",
                    "DAMAGE",
                    "Port impulse manifold hit. Propulsion degraded.",
                    CommandInterfaceTone.Critical
                ),
                Event(
                    "18:07:36",
                    "ENGINEER",
                    "EPS Bus A load at 91%. Forward shields affected.",
                    CommandInterfaceTone.Caution
                ),
                Event("18:07:43", "TACTICAL", "Phaser solution available on Galor-01.", CommandInterfaceTone.Critical),
            ],
            Stations = Stations("command", tacticalAttention: 2, engineeringAttention: 1),
            MapItems =
            [
                new(
                    "enterprise",
                    "ENTERPRISE / IMPULSE 0.68c",
                    CommandInterfaceMapItemKind.PlayerShip,
                    31,
                    49,
                    CommandInterfaceTone.Command
                ),
                new(
                    "galor-01",
                    "GALOR-01 / 7.3 Mm / HOSTILE",
                    CommandInterfaceMapItemKind.Contact,
                    61,
                    45,
                    CommandInterfaceTone.Critical
                ),
                new(
                    "galor-02",
                    "GALOR-02 / 11.8 Mm / HOSTILE",
                    CommandInterfaceMapItemKind.Contact,
                    72,
                    67,
                    CommandInterfaceTone.Critical
                ),
                MapLocation(CeltrisId, "CELTRIS III", 84, 23, CommandInterfaceTone.Muted),
            ],
            MapLinks =
            [
                new("enterprise", "galor-01", CommandInterfaceTone.Critical, "TARGET VECTOR"),
                new("galor-01", "enterprise", CommandInterfaceTone.Critical, "INTERCEPT"),
                new("galor-02", "enterprise", CommandInterfaceTone.Caution, "CLOSURE"),
            ],
        };

    private static CommandInterfacePresentation CreateEngineering() =>
        new()
        {
            DataMode = CommandInterfaceDataMode.EngineeringPreview,
            Mode = CommandInterfaceMode.Engineering,
            Header =
            [
                Field("STATION", "ENGINEERING WORKSPACE", CommandInterfaceTone.Engineering),
                Field("VESSEL", "USS ENTERPRISE NCC-1701-D"),
                Field("STARDATE", "47214.3", CommandInterfaceTone.Muted),
                Field("POWER MARGIN", "+4%", CommandInterfaceTone.Caution),
                Field("ALERT", "RED ALERT", CommandInterfaceTone.Critical),
                Field("INCIDENT", "EPS A OVERLOAD", CommandInterfaceTone.Caution),
            ],
            Systems = CombatSystems(),
            Telemetry =
            [
                Section(
                    "selected-component",
                    "SELECTED COMPONENT / EPS BUS A-4",
                    CommandInterfaceTone.Engineering,
                    Field("LOAD", "91%", CommandInterfaceTone.Caution),
                    Field("TEMPERATURE", "412 K", CommandInterfaceTone.Caution),
                    Field("DAMAGE", "14%", CommandInterfaceTone.Critical),
                    Field("EFFICIENCY", "83%"),
                    Field("TRIP THRESHOLD", "95%", CommandInterfaceTone.Caution),
                    Field("FEED STABILITY", "UNSTABLE", CommandInterfaceTone.Critical)
                ),
                Section(
                    "connected-loads",
                    "CONNECTED LOADS",
                    CommandInterfaceTone.Engineering,
                    Field("FORWARD SHIELDS", "18.2 GW"),
                    Field("PHASER BANK 3", "9.4 GW"),
                    Field("PORT IMPULSE", "14.8 GW"),
                    Field("SENSOR ARRAY", "6.1 GW")
                ),
                Section(
                    "power-allocation",
                    "POWER ALLOCATION",
                    CommandInterfaceTone.Engineering,
                    Field("SHIELDS", "31%", CommandInterfaceTone.Caution),
                    Field("PROPULSION", "24%", CommandInterfaceTone.Caution),
                    Field("WEAPONS", "23%", CommandInterfaceTone.Critical),
                    Field("OTHER", "16%"),
                    Field("RESERVE", "4%", CommandInterfaceTone.Caution)
                ),
            ],
            Actions =
            [
                PreviewAction("prioritize-shields", "Prioritize forward shields", CommandInterfaceTone.Caution),
                PreviewAction("reroute-eps", "Reroute through EPS Bus B", CommandInterfaceTone.Command),
                PreviewAction("reduce-impulse", "Reduce port impulse draw", CommandInterfaceTone.Neutral),
                PreviewAction("isolate-eps", "Isolate EPS Bus A-4", CommandInterfaceTone.Critical),
                PreviewAction("assign-repair", "Assign repair team…", CommandInterfaceTone.Command),
                PreviewAction("reorder-repairs", "Reorder repair priorities…", CommandInterfaceTone.Command),
                PreviewAction("return-command", "Return to Command Deck", CommandInterfaceTone.Command),
            ],
            Events =
            [
                Event(
                    "18:07:29",
                    "DAMAGE",
                    "Port impulse manifold struck. EPS load redistributed.",
                    CommandInterfaceTone.Critical
                ),
                Event(
                    "18:07:32",
                    "POWER",
                    "EPS Bus A exceeded 88% continuous-load threshold.",
                    CommandInterfaceTone.Caution
                ),
                Event(
                    "18:07:36",
                    "SHIELDS",
                    "Forward shield feed voltage fluctuating ±11%.",
                    CommandInterfaceTone.Critical
                ),
                Event("18:07:40", "AUTO", "Auxiliary batteries placed in standby.", CommandInterfaceTone.Nominal),
                Event(
                    "18:07:43",
                    "COMMAND",
                    "Engineering incident opened from Command Deck.",
                    CommandInterfaceTone.Command
                ),
            ],
            Stations = Stations("engineering"),
            MapItems = [],
            MapLinks = [],
            Engineering = EngineeringFixture(),
        };

    private static CommandInterfaceEngineeringPresentation EngineeringFixture() =>
        new(
            [
                Hierarchy("overview", null, "OVERVIEW"),
                Hierarchy("power", null, "POWER", attention: 2, tone: CommandInterfaceTone.Caution),
                Hierarchy("warp-core", "power", "Warp core"),
                Hierarchy("fusion-reactors", "power", "Fusion reactors"),
                Hierarchy("eps-network", "power", "EPS network", true, 2, CommandInterfaceTone.Caution),
                Hierarchy("batteries", "power", "Batteries"),
                Hierarchy("propulsion", null, "PROPULSION", attention: 1, tone: CommandInterfaceTone.Caution),
                Hierarchy("shields", null, "SHIELDS", attention: 1, tone: CommandInterfaceTone.Caution),
                Hierarchy("weapons", null, "WEAPONS"),
                Hierarchy("sensors", null, "SENSORS"),
                Hierarchy("computer", null, "COMPUTER"),
                Hierarchy("life-support", null, "LIFE SUPPORT"),
                Hierarchy("structural", null, "STRUCTURAL"),
            ],
            [
                Section("warp-core", "WARP CORE", CommandInterfaceTone.Nominal, Field("OUTPUT", "88.4 GW")),
                Section(
                    "eps-bus-a",
                    "EPS BUS A",
                    CommandInterfaceTone.Caution,
                    Field("LOAD", "91%"),
                    Field("STATE", "DEG")
                ),
                Section(
                    "forward-shields",
                    "FORWARD SHIELDS",
                    CommandInterfaceTone.Caution,
                    Field("OUTPUT", "41%"),
                    Field("PRIORITY", "2")
                ),
                Section(
                    "phaser-banks",
                    "PHASER BANKS",
                    CommandInterfaceTone.Neutral,
                    Field("STATE", "READY"),
                    Field("OUTPUT", "18 GW")
                ),
                Section(
                    "aux-batteries",
                    "AUX BATTERIES",
                    CommandInterfaceTone.Neutral,
                    Field("CHARGE", "74%"),
                    Field("STATE", "STANDBY")
                ),
                Section(
                    "eps-bus-b",
                    "EPS BUS B",
                    CommandInterfaceTone.Nominal,
                    Field("LOAD", "54%"),
                    Field("STATE", "NOM")
                ),
                Section(
                    "impulse-drive",
                    "IMPULSE DRIVE",
                    CommandInterfaceTone.Caution,
                    Field("STATE", "PORT DEGRADED")
                ),
                Section(
                    "fusion-reactors",
                    "FUSION REACTORS",
                    CommandInterfaceTone.Nominal,
                    Field("OUTPUT", "92%"),
                    Field("STATE", "NOMINAL")
                ),
                Section(
                    "sensors",
                    "SENSORS",
                    CommandInterfaceTone.Muted,
                    Field("STATE", "NOMINAL"),
                    Field("OUTPUT", "8 GW")
                ),
            ],
            [
                new("warp-core", "eps-bus-a", CommandInterfaceTone.Nominal),
                new("warp-core", "eps-bus-b", CommandInterfaceTone.Caution),
                new("eps-bus-a", "forward-shields", CommandInterfaceTone.Caution),
                new("eps-bus-a", "phaser-banks", CommandInterfaceTone.Nominal),
                new("eps-bus-a", "aux-batteries", CommandInterfaceTone.Neutral),
                new("eps-bus-b", "impulse-drive", CommandInterfaceTone.Caution),
                new("eps-bus-b", "fusion-reactors", CommandInterfaceTone.Nominal),
            ],
            [
                Queue(1, "Port impulse manifold", "ETA 04:18", CommandInterfaceTone.Caution),
                Queue(2, "EPS Bus A-4 inspection", "ETA 02:40", CommandInterfaceTone.Caution),
                Queue(3, "Forward shield coupler", "ETA 06:05", CommandInterfaceTone.Muted),
            ]
        );

    private static ImmutableArray<CommandInterfaceSystemRow> TravelSystems() =>
        [
            System("hull", "HULL", "96%", CommandInterfaceTone.Nominal),
            System("shields", "SHIELDS", "100%", CommandInterfaceTone.Nominal),
            System("power", "POWER", "+18", CommandInterfaceTone.Nominal),
            System("propulsion", "PROP", "NOM", CommandInterfaceTone.Nominal),
            System("sensors", "SENSORS", "NOM", CommandInterfaceTone.Nominal),
            System("weapons", "WEAPONS", "SAFE", CommandInterfaceTone.Muted),
            System("computer", "COMPUTER", "NOM", CommandInterfaceTone.Nominal),
            System("life-support", "LIFE SUP", "NOM", CommandInterfaceTone.Nominal),
        ];

    private static ImmutableArray<CommandInterfaceSystemRow> CombatSystems() =>
        [
            System("hull", "HULL", "84%", CommandInterfaceTone.Caution),
            System("shields", "SHIELDS", "62%", CommandInterfaceTone.Caution),
            System("power", "POWER", "+4", CommandInterfaceTone.Caution),
            System("propulsion", "PROP", "DEG", CommandInterfaceTone.Caution),
            System("sensors", "SENSORS", "NOM", CommandInterfaceTone.Nominal),
            System("weapons", "WEAPONS", "HOT", CommandInterfaceTone.Critical),
            System("computer", "COMPUTER", "NOM", CommandInterfaceTone.Nominal),
            System("life-support", "LIFE SUP", "NOM", CommandInterfaceTone.Nominal),
        ];

    private static ImmutableArray<CommandInterfaceStation> Stations(
        string selected,
        int tacticalAttention = 0,
        int engineeringAttention = 0
    ) =>
        [
            Station("command", "COMMAND", selected, 0, CommandInterfaceTone.Command),
            Station("tactical", "TACTICAL", selected, tacticalAttention, CommandInterfaceTone.Critical),
            Station("navigation", "NAVIGATION", selected, 0, CommandInterfaceTone.Navigation),
            Station("engineering", "ENGINEERING", selected, engineeringAttention, CommandInterfaceTone.Engineering),
            Station("science", "SCIENCE", selected, 0, CommandInterfaceTone.Muted),
            Station("comms", "COMMS", selected, 0, CommandInterfaceTone.Muted),
            Station("operations", "OPERATIONS", selected, 0, CommandInterfaceTone.Muted),
        ];

    private static CommandInterfaceField Field(
        string label,
        string value,
        CommandInterfaceTone tone = CommandInterfaceTone.Neutral
    ) => new(label, value, CommandInterfaceAvailability.Available, tone);

    private static CommandInterfaceTelemetrySection Section(
        string id,
        string title,
        CommandInterfaceTone tone,
        params CommandInterfaceField[] fields
    ) => new(id, title, tone, [.. fields]);

    private static CommandInterfaceSystemRow System(string id, string label, string value, CommandInterfaceTone tone) =>
        new(id, label, Field("STATUS", value, tone));

    private static CommandInterfaceAction PreviewAction(string id, string label, CommandInterfaceTone tone) =>
        new(id, label, tone, CommandInterfaceActionAvailability.PreviewOnly);

    private static CommandInterfaceEventRow Event(
        string time,
        string source,
        string message,
        CommandInterfaceTone tone = CommandInterfaceTone.Neutral
    ) => new(time, source, message, tone);

    private static CommandInterfaceStation Station(
        string id,
        string label,
        string selected,
        int attention,
        CommandInterfaceTone tone
    ) => new(id, label, string.Equals(id, selected, StringComparison.Ordinal), attention, tone);

    private static CommandInterfaceMapItem MapLocation(
        LocationId id,
        string label,
        double x,
        double y,
        CommandInterfaceTone tone
    ) => new(id.Value, label, CommandInterfaceMapItemKind.Location, x, y, tone, id);

    private static CommandInterfaceHierarchyRow Hierarchy(
        string id,
        string? parentId,
        string label,
        bool selected = false,
        int attention = 0,
        CommandInterfaceTone tone = CommandInterfaceTone.Neutral
    ) => new(id, parentId, label, selected, attention, CommandInterfaceAvailability.Available, tone);

    private static CommandInterfaceQueueRow Queue(
        int priority,
        string label,
        string estimate,
        CommandInterfaceTone tone
    ) => new(priority, label, Field("ESTIMATE", estimate, tone), tone);
}
