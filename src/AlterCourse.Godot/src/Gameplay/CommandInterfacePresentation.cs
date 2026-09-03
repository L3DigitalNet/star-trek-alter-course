using System.Collections.Immutable;
using AlterCourse.Core.Player;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Identifies whether command-interface data is authoritative live data or an illustrative preview.</summary>
public enum CommandInterfaceDataMode
{
    /// <summary>Use only data projected from the running simulation.</summary>
    Live = 0,

    /// <summary>Show the approved travel composition with deterministic illustrative values.</summary>
    TravelPreview = 1,

    /// <summary>Show the approved combat composition with deterministic illustrative values.</summary>
    CombatPreview = 2,

    /// <summary>Show the approved engineering composition with deterministic illustrative values.</summary>
    EngineeringPreview = 3,
}

/// <summary>Identifies the command-interface workspace whose information hierarchy is active.</summary>
public enum CommandInterfaceMode
{
    /// <summary>Strategic travel and navigation.</summary>
    Travel = 0,

    /// <summary>Tactical combat.</summary>
    Combat = 1,

    /// <summary>Ship engineering.</summary>
    Engineering = 2,
}

/// <summary>Describes whether a displayed datum is known to the current presentation source.</summary>
public enum CommandInterfaceAvailability
{
    /// <summary>The source does not currently provide this datum.</summary>
    Unavailable = 0,

    /// <summary>The source provides this datum.</summary>
    Available = 1,
}

/// <summary>Provides semantic color intent without coupling presentation data to a Godot theme.</summary>
public enum CommandInterfaceTone
{
    /// <summary>Ordinary structural content.</summary>
    Neutral = 0,

    /// <summary>De-emphasized or unavailable content.</summary>
    Muted = 1,

    /// <summary>Command-deck identity or interaction.</summary>
    Command = 2,

    /// <summary>Navigation content.</summary>
    Navigation = 3,

    /// <summary>Engineering content.</summary>
    Engineering = 4,

    /// <summary>Nominal state.</summary>
    Nominal = 5,

    /// <summary>State requiring attention.</summary>
    Caution = 6,

    /// <summary>Critical or hostile state.</summary>
    Critical = 7,
}

/// <summary>Distinguishes disabled, illustrative, and authoritative command-interface actions.</summary>
public enum CommandInterfaceActionAvailability
{
    /// <summary>The action is visible but cannot be used.</summary>
    Disabled = 0,

    /// <summary>The action is illustrative and must never submit an intent.</summary>
    PreviewOnly = 1,

    /// <summary>The action maps to a currently suggested Core intent.</summary>
    Submittable = 2,
}

/// <summary>Identifies one supported intent adapter without carrying executable behavior.</summary>
public enum CommandInterfaceIntent
{
    /// <summary>Submit strategic travel to the selected stable location identity.</summary>
    Travel = 1,

    /// <summary>Submit a tactical heading and speed.</summary>
    SetTacticalCourse = 2,

    /// <summary>Advance authoritative simulation time.</summary>
    AdvanceTime = 3,
}

/// <summary>Identifies a marker's role in a strategic or tactical display.</summary>
public enum CommandInterfaceMapItemKind
{
    /// <summary>A strategic location.</summary>
    Location = 0,

    /// <summary>The player vessel.</summary>
    PlayerShip = 1,

    /// <summary>An observed or illustrative contact.</summary>
    Contact = 2,
}

/// <summary>Represents one immutable label-value pair with explicit source availability and meaning.</summary>
public sealed record CommandInterfaceField(
    string Label,
    string Value,
    CommandInterfaceAvailability Availability,
    CommandInterfaceTone Tone = CommandInterfaceTone.Neutral
);

/// <summary>Represents one system-spine status row.</summary>
public sealed record CommandInterfaceSystemRow(string Id, string Label, CommandInterfaceField Status);

/// <summary>Groups related telemetry for an inspector or summary panel.</summary>
public sealed record CommandInterfaceTelemetrySection(
    string Id,
    string Title,
    CommandInterfaceTone Tone,
    ImmutableArray<CommandInterfaceField> Fields
);

/// <summary>Describes one visible action and its safe submission classification.</summary>
public sealed record CommandInterfaceAction(
    string Id,
    string Label,
    CommandInterfaceTone Tone,
    CommandInterfaceActionAvailability Availability,
    CommandInterfaceIntent? Intent = null
);

/// <summary>Represents one chronological event or order-log row.</summary>
public sealed record CommandInterfaceEventRow(string Time, string Source, string Message, CommandInterfaceTone Tone);

/// <summary>Represents one station-strip entry and its attention state.</summary>
public sealed record CommandInterfaceStation(
    string Id,
    string Label,
    bool IsSelected,
    int AttentionCount,
    CommandInterfaceTone Tone
);

/// <summary>Represents a presentation-space marker while retaining stable navigation identity when one exists.</summary>
public sealed record CommandInterfaceMapItem(
    string Id,
    string Label,
    CommandInterfaceMapItemKind Kind,
    double X,
    double Y,
    CommandInterfaceTone Tone,
    LocationId? StrategicLocationId = null
);

/// <summary>Represents a presentation-space connection between two marker identities.</summary>
public sealed record CommandInterfaceMapLink(
    string OriginId,
    string DestinationId,
    CommandInterfaceTone Tone,
    string Label
);

/// <summary>Represents one engineering hierarchy row.</summary>
public sealed record CommandInterfaceHierarchyRow(
    string Id,
    string? ParentId,
    string Label,
    bool IsSelected,
    int AttentionCount,
    CommandInterfaceAvailability Availability,
    CommandInterfaceTone Tone
);

/// <summary>Represents a directed engineering schematic connection.</summary>
public sealed record CommandInterfaceEngineeringLink(
    string OriginId,
    string DestinationId,
    CommandInterfaceTone Tone
);

/// <summary>Represents one repair or engineering action queue row.</summary>
public sealed record CommandInterfaceQueueRow(
    int Priority,
    string Label,
    CommandInterfaceField Estimate,
    CommandInterfaceTone Tone
);

/// <summary>Collects the engineering-only hierarchy, metrics, topology, and queue projections.</summary>
public sealed record CommandInterfaceEngineeringPresentation(
    ImmutableArray<CommandInterfaceHierarchyRow> Hierarchy,
    ImmutableArray<CommandInterfaceTelemetrySection> Components,
    ImmutableArray<CommandInterfaceEngineeringLink> Links,
    ImmutableArray<CommandInterfaceQueueRow> Queue
);

/// <summary>
/// Supplies one immutable display snapshot for the command shell without exposing simulation mutation or Godot types.
/// </summary>
public sealed record CommandInterfacePresentation
{
    /// <summary>Gets whether this snapshot is live or illustrative.</summary>
    public required CommandInterfaceDataMode DataMode { get; init; }

    /// <summary>Gets the active information hierarchy.</summary>
    public required CommandInterfaceMode Mode { get; init; }

    /// <summary>Gets header identity and status fields.</summary>
    public required ImmutableArray<CommandInterfaceField> Header { get; init; }

    /// <summary>Gets ship-system status rows.</summary>
    public required ImmutableArray<CommandInterfaceSystemRow> Systems { get; init; }

    /// <summary>Gets inspector and telemetry sections.</summary>
    public required ImmutableArray<CommandInterfaceTelemetrySection> Telemetry { get; init; }

    /// <summary>Gets visible actions with explicit submission classification.</summary>
    public required ImmutableArray<CommandInterfaceAction> Actions { get; init; }

    /// <summary>Gets chronological event rows.</summary>
    public required ImmutableArray<CommandInterfaceEventRow> Events { get; init; }

    /// <summary>Gets station navigation and attention state.</summary>
    public required ImmutableArray<CommandInterfaceStation> Stations { get; init; }

    /// <summary>Gets strategic or tactical display markers.</summary>
    public required ImmutableArray<CommandInterfaceMapItem> MapItems { get; init; }

    /// <summary>Gets strategic routes or illustrative tactical vectors.</summary>
    public required ImmutableArray<CommandInterfaceMapLink> MapLinks { get; init; }

    /// <summary>Gets the selected strategic identity when the user has made a live or preview selection.</summary>
    public LocationId? SelectedLocationId { get; init; }

    /// <summary>Gets the original Core strategic projection for the existing strategic map adapter.</summary>
    public StrategicProjection? Strategic { get; init; }

    /// <summary>Gets the original Core tactical projection for the existing tactical map adapter.</summary>
    public TacticalProjection? Tactical { get; init; }

    /// <summary>Gets engineering-only content, or null outside the engineering hierarchy.</summary>
    public CommandInterfaceEngineeringPresentation? Engineering { get; init; }
}
