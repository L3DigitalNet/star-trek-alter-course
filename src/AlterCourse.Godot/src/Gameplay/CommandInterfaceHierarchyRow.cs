namespace AlterCourse.Godot.Gameplay;

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
