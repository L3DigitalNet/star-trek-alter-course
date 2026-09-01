using Godot;

namespace AlterCourse.Godot;

/// <summary>Exposes a minimal managed node for the GdUnit integration smoke test.</summary>
public partial class IntegrationProbe : Node
{
    /// <inheritdoc />
    public override void _EnterTree()
    {
        SetMeta("csharp_entered_tree", true);
    }
}
