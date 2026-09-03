using AlterCourse.Core.Strategic;

namespace AlterCourse.Godot.Gameplay;

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
