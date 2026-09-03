namespace AlterCourse.Godot.Gameplay;

/// <summary>Identifies a marker's role in a strategic or tactical display.</summary>
public enum CommandInterfaceMapItemKind
{
    /// <summary>A strategic location.</summary>
    Location = 0,

    /// <summary>The player vessel.</summary>
    PlayerShip = 1,

    /// <summary>An observed or illustrative contact.</summary>
    Contact = 2,
}
