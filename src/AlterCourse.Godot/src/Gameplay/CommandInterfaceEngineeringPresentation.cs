using System.Collections.Immutable;

namespace AlterCourse.Godot.Gameplay;

/// <summary>Collects the engineering-only hierarchy, metrics, topology, and queue projections.</summary>
public sealed record CommandInterfaceEngineeringPresentation(
    ImmutableArray<CommandInterfaceHierarchyRow> Hierarchy,
    ImmutableArray<CommandInterfaceTelemetrySection> Components,
    ImmutableArray<CommandInterfaceEngineeringLink> Links,
    ImmutableArray<CommandInterfaceQueueRow> Queue
);
