namespace AlterCourse.Godot.Gameplay;

/// <summary>Identifies the command-interface workspace whose information hierarchy is active.</summary>
public enum CommandInterfaceMode
{
    /// <summary>Strategic travel and navigation.</summary>
    Travel = 0,

    /// <summary>Tactical combat.</summary>
    Combat = 1,

    /// <summary>Ship engineering.</summary>
    Engineering = 2,
}
