namespace AlterCourse.Godot.Gameplay;

/// <summary>Distinguishes disabled, illustrative, and authoritative command-interface actions.</summary>
public enum CommandInterfaceActionAvailability
{
    /// <summary>The action is visible but cannot be used.</summary>
    Disabled = 0,

    /// <summary>The action is illustrative and must never submit an intent.</summary>
    PreviewOnly = 1,

    /// <summary>The action maps to a currently suggested Core intent.</summary>
    Submittable = 2,
}
