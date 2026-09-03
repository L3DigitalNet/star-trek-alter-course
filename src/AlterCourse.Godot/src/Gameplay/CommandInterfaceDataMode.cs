namespace AlterCourse.Godot.Gameplay;

/// <summary>Identifies whether command-interface data is authoritative live data or an illustrative preview.</summary>
public enum CommandInterfaceDataMode
{
    /// <summary>Use only data projected from the running simulation.</summary>
    Live = 0,

    /// <summary>Show the approved travel composition with deterministic illustrative values.</summary>
    TravelPreview = 1,

    /// <summary>Show the approved combat composition with deterministic illustrative values.</summary>
    CombatPreview = 2,

    /// <summary>Show the approved engineering composition with deterministic illustrative values.</summary>
    EngineeringPreview = 3,
}
