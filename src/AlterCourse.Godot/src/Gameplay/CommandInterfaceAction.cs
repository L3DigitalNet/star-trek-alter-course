namespace AlterCourse.Godot.Gameplay;

using AlterCourse.Core.Sensors;

/// <summary>Describes one visible action and its safe submission classification.</summary>
public sealed record CommandInterfaceAction(
    string Id,
    string Label,
    CommandInterfaceTone Tone,
    CommandInterfaceActionAvailability Availability,
    CommandInterfaceIntent? Intent = null,
    SensorContactId? FocusedContactId = null,
    string? Tooltip = null
);
