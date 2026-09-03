namespace AlterCourse.Godot.Gameplay;

/// <summary>Provides semantic color intent without coupling presentation data to a Godot theme.</summary>
public enum CommandInterfaceTone
{
    /// <summary>Ordinary structural content.</summary>
    Neutral = 0,

    /// <summary>De-emphasized or unavailable content.</summary>
    Muted = 1,

    /// <summary>Command-deck identity or interaction.</summary>
    Command = 2,

    /// <summary>Navigation content.</summary>
    Navigation = 3,

    /// <summary>Engineering content.</summary>
    Engineering = 4,

    /// <summary>Nominal state.</summary>
    Nominal = 5,

    /// <summary>State requiring attention.</summary>
    Caution = 6,

    /// <summary>Critical or hostile state.</summary>
    Critical = 7,
}
