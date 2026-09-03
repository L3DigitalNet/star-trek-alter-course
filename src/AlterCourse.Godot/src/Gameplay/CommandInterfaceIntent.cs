namespace AlterCourse.Godot.Gameplay;

/// <summary>Identifies one supported intent adapter without carrying executable behavior.</summary>
public enum CommandInterfaceIntent
{
    /// <summary>Submit strategic travel to the selected stable location identity.</summary>
    Travel = 1,

    /// <summary>Submit a tactical heading and speed.</summary>
    SetTacticalCourse = 2,

    /// <summary>Advance authoritative simulation time.</summary>
    AdvanceTime = 3,
}
