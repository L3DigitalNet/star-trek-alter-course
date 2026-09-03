namespace AlterCourse.Godot.Gameplay;

/// <summary>Represents a directed engineering schematic connection.</summary>
public sealed record CommandInterfaceEngineeringLink(string OriginId, string DestinationId, CommandInterfaceTone Tone);
