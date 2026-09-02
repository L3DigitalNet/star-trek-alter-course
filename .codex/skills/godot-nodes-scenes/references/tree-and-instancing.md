# Scene tree and instancing reference

> Modified from `awesome-gamedev-agent-skills` at commit `7110607ab816ece9669274bc84937857a8819796`. Adapted to C# and ST:AC architecture. See the [Apache-2.0 license](../../../../LICENSES/Apache-2.0.txt) and [upstream NOTICE](../../../../LICENSES/awesome-gamedev-agent-skills-NOTICE.txt).

## Node paths

- `GetNode<T>("Child/Grandchild")` resolves a required relative path and reports an error if absent.
- `GetNodeOrNull<T>("Optional")` expresses optional membership.
- `GetNode<T>("%UniqueName")` uses a scene-unique node marked in the editor and survives local reparenting better than a deep path.
- `GetTree().Root` is the root `Window`; `GetTree().CurrentScene` is the active scene root.

Avoid using groups or scene traversal as an untyped registry for domain objects. Core collections and stable domain identifiers own that relationship.

## Loading and instancing

```csharp
private readonly PackedScene _scene =
    GD.Load<PackedScene>("res://views/SystemMarker.tscn");

public Node2D CreateMarker(Node parent, Vector2 screenPosition)
{
    var marker = _scene.Instantiate<Node2D>();
    parent.AddChild(marker);
    marker.Position = screenPosition;
    return marker;
}
```

`PackedScene.GenEditState` values are for editor tooling; ordinary runtime instancing uses the default. A loaded scene and its Nodes are engine representations, not a persistence format.

## Ownership when saving scenes

When an editor tool constructs a presentation scene in code, every child intended for serialization needs its `Owner` set to the scene root before `PackedScene.Pack` and `ResourceSaver.Save`.

```csharp
var root = new Node2D();
var child = new Sprite2D();
root.AddChild(child);
child.Owner = root;

var packed = new PackedScene();
packed.Pack(root);
ResourceSaver.Save(packed, "res://generated/presentation.tscn");
```

This is suitable for editor-generated presentation content only. Never serialize the runtime scene graph as a save or canonical world model.

## Composition and inherited scenes

Scene inheritance can share a stable visual base while variants override presentation properties. Prefer composition when behavior can be an owned child component; deep inheritance makes lifecycle and editor overrides harder to reason about.

## Tree mutation and validity

```csharp
if (GodotObject.IsInstanceValid(node))
{
    node.QueueFree();
}

instance.CallDeferred(Node.MethodName.Reparent, newParent);
```

Use deferred calls for tree mutations from physics callbacks or signal handlers when Godot reports a flushing-query restriction. `Reparent` keeps a `Node2D`/`Control` global transform by default where supported.

## Spatial boundary

Godot `Vector2`, transforms, navigation objects, tile maps, and Nodes serve rendering and local engine interaction. Core owns stable semantic locations, strategic routes, tactical quantities, scale transitions, and deterministic routefinding. Convert through a named adapter rather than storing Godot coordinates as domain truth.
