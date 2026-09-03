using System.Collections.Immutable;
using AlterCourse.Core.Player;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Strategic;

namespace AlterCourse.Godot.Gameplay;

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

    /// <summary>Gets actor-safe live contacts, or an empty collection for illustrative previews.</summary>
    public ImmutableArray<CommandInterfaceContact> Contacts { get; init; } = [];

    /// <summary>Gets the selected observer-local contact identity when it remains actionable context.</summary>
    public SensorContactId? SelectedContactId { get; init; }

    /// <summary>Gets the original Core strategic projection for the existing strategic map adapter.</summary>
    public StrategicProjection? Strategic { get; init; }

    /// <summary>Gets the original Core tactical projection for the existing tactical map adapter.</summary>
    public TacticalProjection? Tactical { get; init; }

    /// <summary>Gets engineering-only content, or null outside the engineering hierarchy.</summary>
    public CommandInterfaceEngineeringPresentation? Engineering { get; init; }
}
