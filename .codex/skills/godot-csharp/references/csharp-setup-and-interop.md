# Godot C# setup and interop reference

> Modified from `awesome-gamedev-agent-skills` at commit `7110607ab816ece9669274bc84937857a8819796`. Adapted for Godot 4.7.2, .NET 8, C#-first use, and ST:AC architecture. See the [Apache-2.0 license](../../../../LICENSES/Apache-2.0.txt) and [upstream NOTICE](../../../../LICENSES/awesome-gamedev-agent-skills-NOTICE.txt).

## Project and build behavior

- The project uses `Godot.NET.Sdk/4.7.2` and targets `net8.0`; `global.json` selects SDK 10.0.111.
- The Godot .NET editor builds managed assemblies before play. `dotnet build` validates managed code without starting the editor; the canonical gate also runs headless Godot integration.
- Godot source generators inspect `partial` Godot object classes and generate signal events plus nested name classes such as `SignalName` and `MethodName`.
- Keep generated `bin/`, `obj/`, and `.godot/` output untracked.

## Export attributes

Use exports for editor-authored presentation references and tunables, not authoritative world state.

```csharp
[Export]
public NodePath TargetPath { get; set; } = new();

[Export(PropertyHint.Range, "0,1,0.01")]
public float Opacity { get; set; } = 1.0f;

[ExportGroup("Presentation")]
[Export]
public Texture2D? Icon { get; set; }

[Export]
public Godot.Collections.Array<PackedScene> MarkerScenes { get; set; } = new();
```

`[ExportGroup]`, `[ExportSubgroup]`, and `[ExportCategory]` organize the Inspector. A field/property type must be Variant-compatible to export.

## Collections and Variant

- `Godot.Collections.Array<T>` and `Dictionary<TKey,TValue>` are Variant-backed and suitable for exported properties or engine calls.
- `System.Collections.Generic` collections are the normal choice for Core and pure managed implementation.
- Convert at an adapter boundary rather than leaking Godot collections into Core.
- Signal arguments and dynamic calls must use Variant-compatible types. Prefer stable identifiers and explicit projection DTOs over passing Nodes or Resources across layers.

## Signals and events

```csharp
public partial class StatusPanel : Control
{
    [Signal]
    public delegate void AcknowledgeRequestedEventHandler(string notificationId);

    public override void _Ready()
    {
        AcknowledgeRequested += OnAcknowledgeRequested;
    }

    private void OnAcknowledgeRequested(string notificationId)
    {
        // Forward to an application boundary that validates the typed operation.
    }
}
```

Use generated C# events for C# emitters. For a named dynamic signal, use `Connect(StringName, Callable)` with generated name constants where available. Disconnect explicit long-lived relationships when their lifetimes differ.

## Necessary GDScript interop

Normal project code is C#. At a required GDScript addon boundary:

```csharp
Node addon = GetNode("AddonNode");
addon.Call("refresh_view", new Variant[] { projectionId });
Variant value = addon.Get("selected_id");
addon.Connect("selection_changed", Callable.From<string>(OnSelectionChanged));
```

Keep dynamic names at one adapter, validate returned Variants, and translate to typed project contracts immediately. Public C# members on a Node or `[GlobalClass]` are callable from GDScript.

## Resources

`Resource` and `.tres` are appropriate for presentation assets such as themes, textures, style boxes, and editor-authored visual configuration. They are not canonical domain content or saves. Canonical domain content is UTF-8 JSON with versioned schema and semantic validation; saves are versioned Core snapshot models serialized as JSON.

## API equivalents

- `preload` / `load` → `GD.Load<T>("res://...")`
- `instantiate()` → `packed.Instantiate<T>()`
- `$Path` / `%Unique` → `GetNode<T>("Path")` / `GetNode<T>("%Unique")`
- `queue_free()` → `QueueFree()`
- `emit_signal(...)` → `EmitSignal(SignalName.X, ...)`
- `is_instance_valid(o)` → `GodotObject.IsInstanceValid(o)`

Do not translate `randf()`/`randi()` into authoritative gameplay logic. Core owns seeded, versioned randomness. Do not await Godot timers for simulation scheduling; Core owns explicit time and schedules.
