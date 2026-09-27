namespace AlterCourse.Godot.Gameplay;

using AlterCourse.Core.Player;
using AlterCourse.Core.Sensors;
using AlterCourse.Core.Ships;

/// <summary>Describes one visible action and its safe submission classification.</summary>
/// <remarks>
/// <see cref="Id"/> is the stable operation-plus-identity key (for Engineering: <c>balance</c>,
/// <c>prioritize:&lt;installed id&gt;</c>, <c>repair:&lt;installed id&gt;</c>, <c>return-command</c>), never a position
/// or label. <see cref="EngineeringOperation"/> and <see cref="EngineeringTarget"/> are typed intent only — Core
/// decides legality at submission. <see cref="AimKind"/> is the semantic subsystem chosen for remote fire, filled in
/// by the Command Deck at activation. <see cref="Binding"/> is null for previews.
/// </remarks>
public sealed record CommandInterfaceAction(
    string Id,
    string Label,
    CommandInterfaceTone Tone,
    CommandInterfaceActionAvailability Availability,
    CommandInterfaceIntent? Intent = null,
    SensorContactId? FocusedContactId = null,
    string? Tooltip = null,
    EngineeringOperation? EngineeringOperation = null,
    InstalledSystemId? EngineeringTarget = null,
    ShipSystemKind? AimKind = null,
    OwnShipActionBinding? Binding = null
);
