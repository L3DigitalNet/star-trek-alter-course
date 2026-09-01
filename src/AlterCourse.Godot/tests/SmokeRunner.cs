using Godot;

namespace AlterCourse.Godot.Tests;

/// <summary>Runs the headless managed-runtime smoke check and exits.</summary>
public partial class SmokeRunner : Node
{
    /// <inheritdoc />
    public override void _Ready()
    {
        GD.Print("ALTER_COURSE_GODOT_SMOKE_OK");
        GetTree().Quit();
    }
}
