namespace AlterCourse.Godot.Gameplay;

/// <summary>Represents a presentation-space connection between two marker identities.</summary>
public sealed record CommandInterfaceMapLink(
    string OriginId,
    string DestinationId,
    CommandInterfaceTone Tone,
    string Label
);
