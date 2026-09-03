namespace AlterCourse.Godot.Gameplay;

/// <summary>Represents one chronological event or order-log row.</summary>
public sealed record CommandInterfaceEventRow(string Time, string Source, string Message, CommandInterfaceTone Tone);
