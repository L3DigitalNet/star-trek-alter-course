using System.Collections.Immutable;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Groups related telemetry for an inspector or summary panel.</summary>
public sealed record CommandInterfaceTelemetrySection(
    string Id,
    string Title,
    CommandInterfaceTone Tone,
    ImmutableArray<CommandInterfaceField> Fields
);
