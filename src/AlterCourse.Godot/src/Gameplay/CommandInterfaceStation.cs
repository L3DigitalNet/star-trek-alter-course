namespace AlterCourse.Godot.Gameplay;

/// <summary>Represents one station-strip entry and its attention state.</summary>
public sealed record CommandInterfaceStation(
    string Id,
    string Label,
    bool IsSelected,
    int AttentionCount,
    CommandInterfaceTone Tone
);
