namespace AlterCourse.Godot.Gameplay;

/// <summary>Represents one repair or engineering action queue row.</summary>
public sealed record CommandInterfaceQueueRow(
    int Priority,
    string Label,
    CommandInterfaceField Estimate,
    CommandInterfaceTone Tone
);
