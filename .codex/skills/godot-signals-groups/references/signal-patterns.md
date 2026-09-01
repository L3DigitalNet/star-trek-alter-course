# Signal and group patterns reference

> Modified from `awesome-gamedev-agent-skills` at commit `7110607ab816ece9669274bc84937857a8819796`. Adapted to C# and ST:AC architecture. See the [Apache-2.0 license](../../../../LICENSES/Apache-2.0.txt) and [upstream NOTICE](../../../../LICENSES/awesome-gamedev-agent-skills-NOTICE.txt).

## Connection flags

`GodotObject.Connect` accepts `ConnectFlags`:

- `OneShot` disconnects after one emission.
- `Deferred` invokes at idle time, useful when a handler must mutate the scene tree.
- `Persist` records editor-created scene connections.
- `ReferenceCounted` counts duplicate equivalent connections.

```csharp
emitter.Connect(
    Emitter.SignalName.Finished,
    Callable.From(OnFinished),
    (uint)(GodotObject.ConnectFlags.OneShot | GodotObject.ConnectFlags.Deferred));
```

Flags control engine callback behavior only. They do not establish deterministic domain ordering.

`Callable.Bind` appends fixed arguments supplied at connection time, while `Unbind` drops trailing emitted arguments that a callback intentionally ignores. Prefer a small typed adapter method when binding would obscure which values come from the signal.

## Direct call or signal

- Use a direct typed call when an owner created a component and expects one result.
- Use a signal when a reusable scene-local emitter should not know its listeners or when multiple presentation listeners are natural.
- Use a typed application/Core operation when state changes, order matters, validation can fail, or the result persists.

Avoid replacing a straightforward bounded relationship with an event bus. Do not route cross-scene domain behavior through an autoload.

## Dynamic connections

At a necessary addon/GDScript boundary:

```csharp
Callable callback = Callable.From<string>(OnSelectionChanged);
StringName signal = "selection_changed";

if (!addon.IsConnected(signal, callback))
{
    addon.Connect(signal, callback);
}
```

Prefer generated `SignalName` constants for typed Godot C# objects. Validate dynamic argument types at the adapter boundary.

## Awaiting engine signals

```csharp
animation.Play("open");
await ToSignal(animation, AnimationPlayer.SignalName.AnimationFinished);
```

This is suitable for presentation sequencing. Never use `CreateTimer`, animation completion, or frame order to advance authoritative simulation; schedule that transition explicitly in Core.

## Groups

```csharp
node.AddToGroup("bridge_status_widgets");
node.RemoveFromGroup("bridge_status_widgets");
bool member = node.IsInGroup("bridge_status_widgets");

Godot.Collections.Array<Node> widgets =
    tree.GetNodesInGroup("bridge_status_widgets");
tree.CallGroup("bridge_status_widgets", "refresh_presentation");
```

`CallGroup` is string-based and can hide misspellings or missing methods. Iterate and type-check when the contract matters. Never use group membership as a save inventory, entity registry, faction/system membership model, or domain query.

## Persistence boundary

Signal connections and group membership are runtime engine structure. Versioned Core snapshot saves serialize explicit domain models and stable identifiers, not arbitrary Node graphs, listeners, or group membership.
