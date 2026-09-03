namespace AlterCourse.Godot.Gameplay;

/// <summary>Represents one immutable label-value pair with explicit source availability and meaning.</summary>
public sealed record CommandInterfaceField(
    string Label,
    string Value,
    CommandInterfaceAvailability Availability,
    CommandInterfaceTone Tone = CommandInterfaceTone.Neutral
);
