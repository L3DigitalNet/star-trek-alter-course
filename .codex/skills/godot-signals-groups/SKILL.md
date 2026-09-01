---
name: godot-signals-groups
description: Use Godot 4.7.2 C# signals and groups for scene-local notification, UI, lifecycle, presentation, and adapters without replacing typed Core operations or deterministic transitions.
---

# Godot signals and groups

> Modified from `awesome-gamedev-agent-skills` at commit `7110607ab816ece9669274bc84937857a8819796`. Adapted to C# and ST:AC architecture. See the [Apache-2.0 license](../../../LICENSES/Apache-2.0.txt) and [upstream NOTICE](../../../LICENSES/awesome-gamedev-agent-skills-NOTICE.txt).

Use signals for Godot notification when an emitter should not know its scene-local listeners. Use groups for presentation categories or bounded engine broadcasts. Prefer a direct call for a clear owner-to-owned relationship.

Signals and groups must not replace Core commands, typed domain operations/events, deterministic transition order, scheduling, persistence, or stable entity registries. Never create a global autoload event bus.

## C# signals

```csharp
public partial class ContactMarker : Area2D
{
    [Signal]
    public delegate void SelectedEventHandler(string contactId);

    public string ContactId { get; private set; } = string.Empty;

    public void Select()
    {
        EmitSignal(SignalName.Selected, ContactId);
    }
}

public partial class TacticalView : Node2D
{
    public void Attach(ContactMarker marker)
    {
        marker.Selected += OnMarkerSelected;
    }

    private void OnMarkerSelected(string contactId)
    {
        // Submit typed selection intent through the application boundary.
    }
}
```

The signal communicates a presentation interaction. It does not decide a domain outcome.

## Direction and lifetime

1. A reusable child emits upward; its owner connects and coordinates.
2. Use generated C# events for C# signals and `Connect`/`Callable` for dynamic addon boundaries.
3. Connect once, avoid duplicate callbacks, and detach when a longer-lived publisher retains a shorter-lived non-Godot listener.
4. Use one-shot or deferred connection flags only for engine lifecycle requirements, never to define authoritative ordering.
5. Await animation or UI completion when presentation needs it. Do not await timers/signals as simulation scheduling.

## Groups

```csharp
AddToGroup("tactical_contact_markers");

foreach (Node node in GetTree().GetNodesInGroup("tactical_contact_markers"))
{
    if (node is ContactMarker marker)
    {
        marker.Visible = showContacts;
    }
}
```

Groups are global to a `SceneTree`, and string-based `CallGroup` silently skips incompatible members. Use narrow presentation names and typed checks. Core collections keyed by stable identifiers own domain membership.

Read [`references/signal-patterns.md`](references/signal-patterns.md) for flags, direct-call trade-offs, dynamic connections, groups, and adapter patterns.
