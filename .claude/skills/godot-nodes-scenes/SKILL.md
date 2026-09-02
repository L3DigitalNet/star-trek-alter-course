---
name: godot-nodes-scenes
description: Compose Godot 4.7.2 C# scenes and nodes, instance PackedScenes, navigate lifecycles and ownership safely, and use narrowly justified autoloads without making the scene tree authoritative simulation state.
---

# Godot nodes and scenes

> Modified from `awesome-gamedev-agent-skills` at commit `7110607ab816ece9669274bc84937857a8819796`. Adapted to C# and ST:AC architecture. See the [Apache-2.0 license](../../../LICENSES/Apache-2.0.txt) and [upstream NOTICE](../../../LICENSES/awesome-gamedev-agent-skills-NOTICE.txt).

Use scenes for engine composition and presentation. `AlterCourse.Core`, not the scene tree, owns authoritative entities, semantic multi-scale space, schedules, and persistence. Godot transforms are render projections; Resources and `.tscn` files are not canonical domain records.

## Workflow

1. Compose small, single-purpose scenes from Nodes rather than deep scene inheritance.
2. Give reusable scene roots partial C# scripts and presentation-only exports.
3. Load a `PackedScene`, call `Instantiate<T>()`, configure the instance, and `AddChild` it.
4. Use `GetNode<T>` for required children, `GetNodeOrNull<T>` for optional children, and scene-unique names for stable deep access.
5. Respect `_EnterTree`, `_Ready`, `_ExitTree`, deferred mutation, and queued deletion.
6. Prefer direct bounded ownership. Add an autoload only for a demonstrated engine-wide presentation/service lifetime, never as a speculative manager, global bus, or world-state repository.

```csharp
namespace AlterCourse.Godot.Views;

/// <summary>Instances contact projections as presentation-only scene children.</summary>
public partial class TacticalView : Node2D
{
    private readonly PackedScene _contactMarker =
        GD.Load<PackedScene>("res://views/ContactMarker.tscn");

    /// <summary>Adds and presents one contact marker for the supplied Core projection.</summary>
    public ContactMarker AddProjectedContact(ContactProjection projection)
    {
        var marker = _contactMarker.Instantiate<ContactMarker>();
        AddChild(marker);
        marker.Present(projection);
        return marker;
    }
}
```

The `ContactProjection` comes from Core/application code. The marker does not become the contact record.

## Lifecycle and access

```csharp
namespace AlterCourse.Godot.Views;

/// <summary>Owns scene-lifetime access to the bridge status presentation.</summary>
public partial class BridgeView : Control
{
    private Label _status = null!;

    /// <inheritdoc />
    public override void _Ready()
    {
        _status = GetNode<Label>("%Status");
    }

    /// <inheritdoc />
    public override void _ExitTree()
    {
        // Detach only subscriptions whose publisher outlives this view.
    }
}
```

- A child's `_Ready()` runs after it enters the tree; do not depend on ready-initialized fields before `AddChild` completes.
- `QueueFree()` deletes at frame end. Retained Node references may become invalid; check `GodotObject.IsInstanceValid` when lifetime is uncertain.
- Use `CallDeferred` when a callback cannot safely mutate the tree immediately.
- `ChangeSceneToFile`/`ChangeSceneToPacked` are deferred; initialize the replacement in its own lifecycle.

## Autoload constraint

An autoload may be reasonable for an engine adapter with a truly process-wide lifetime, such as presentation-only audio routing. It must not hold authoritative game state, implement domain rules, schedule simulation, provide ambient RNG, or act as a global signal bus. Start with an explicit owner-to-owned reference or injected adapter and document the demonstrated need before adding global lifetime.

Read [`references/tree-and-instancing.md`](references/tree-and-instancing.md) for instancing, scene ownership, inherited scenes, paths, deferred operations, and saving editor-generated presentation scenes.
