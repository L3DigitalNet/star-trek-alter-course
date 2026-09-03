namespace AlterCourse.Godot.Gameplay;

/// <summary>Describes whether a displayed datum is known to the current presentation source.</summary>
public enum CommandInterfaceAvailability
{
    /// <summary>The source does not currently provide this datum.</summary>
    Unavailable = 0,

    /// <summary>The source provides this datum.</summary>
    Available = 1,
}
