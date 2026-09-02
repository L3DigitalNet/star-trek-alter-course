---
name: godot-csharp
description: Use Godot 4.7.2 and .NET 8 C# for engine-facing code while preserving AlterCourse.Core as the pure deterministic simulation authority. Covers partial Node classes, lifecycle overrides, exports, signals, typed node access, Variant interop, and builds.
---

# Godot C# / .NET

> Modified from `awesome-gamedev-agent-skills` at commit `7110607ab816ece9669274bc84937857a8819796`. Adapted for Star Trek: Alter Course, C#-first examples, and its simulation boundary. Apache-2.0 license and upstream NOTICE: [`LICENSES/Apache-2.0.txt`](../../../LICENSES/Apache-2.0.txt) and [`LICENSES/awesome-gamedev-agent-skills-NOTICE.txt`](../../../LICENSES/awesome-gamedev-agent-skills-NOTICE.txt).

Use this skill for C# scripts, `.csproj` integration, Godot/.NET builds, exports, signals, typed node access, and necessary C#/GDScript interop. The repository targets Godot.NET.Sdk 4.7.2 and `net8.0` with SDK 10.0.111.

## Route ownership first

Before writing a Node, decide whether the behavior is simulation truth or engine presentation.

- Put deterministic domain state and rules in `AlterCourse.Core`: ships, factions, systems, sectors, actors, diplomacy, damage, missions, schedules, semantic space, typed quantities, explicit time, seeded randomness, AI decisions, content models, and persistence models.
- Put scenes, nodes, input, rendering, UI, animation, audio, and thin engine adapters in `AlterCourse.Godot`.
- Core must not reference Godot types. Godot may project Core state and submit typed intent; it must not become a second simulation authority.
- `_Process`, `_PhysicsProcess`, scene lifecycle, timers, wall clock, signal order, and `GD.Rand*` are never authoritative clocks or randomness. Use them only for presentation/input and invoke explicit Core operations.

## Core workflow

1. Use the Godot .NET editor build and the repository-resolved .NET SDK.
2. Declare every Godot object script as a `partial` class; generated exports, signals, and name constants depend on it.
3. Override lifecycle methods with C# names and signatures: `_Ready()`, `_Process(double delta)`, and `_PhysicsProcess(double delta)`.
4. Expose presentation configuration with `[Export]`. Do not export authoritative domain state.
5. Declare `[Signal]` delegates ending in `EventHandler`; use generated `SignalName` constants and C# events.
6. Prefer `GetNode<T>` and generated API names. Use dynamic `Call`/`Get`/`Set` only at a necessary interop boundary.

## Node adapter example

```csharp
using AlterCourse.Core.Navigation;
using Godot;

public partial class CourseMarker : Node2D
{
    [Export]
    public NodePath LabelPath { get; set; } = new("Label");

    [Signal]
    public delegate void DestinationRequestedEventHandler(string destinationId);

    private Label _label = null!;

    public override void _Ready()
    {
        _label = GetNode<Label>(LabelPath);
    }

    public void Present(RouteProjection route)
    {
        _label.Text = route.DisplayName;
        Position = new Vector2(route.ScreenX, route.ScreenY);
    }

    public void RequestDestination(string destinationId)
    {
        EmitSignal(SignalName.DestinationRequested, destinationId);
    }
}
```

The node displays a projection and raises UI intent. It does not calculate routes or mutate a ship.

## Signals, loading, and values

```csharp
public partial class Overlay : Control
{
    private readonly PackedScene _contactScene =
        GD.Load<PackedScene>("res://ui/ContactMarker.tscn");

    public void AddContact(Vector2 screenPosition)
    {
        var marker = _contactScene.Instantiate<Node2D>();
        AddChild(marker);
        marker.Position = screenPosition;
    }
}
```

- Godot vectors, colors, and transforms are value types. Mutate a local copy, then assign it back.
- Prefer `QueueFree()` to immediate `Free()` and guard retained references with `GodotObject.IsInstanceValid`.
- Use `GD.Print`/`GD.PrintErr` for editor-visible diagnostics; use the repository logging architecture for application observability.

## Build and generator pitfalls

- Missing `partial`, wrong lifecycle casing, or a `[Signal]` delegate without the `EventHandler` suffix breaks generated bindings or silently prevents callbacks.
- Godot collections and `Variant` are for engine boundaries. Prefer ordinary .NET collections inside pure managed code.
- The non-.NET editor cannot load C# assemblies. Build failures should be reproduced through repository commands, not addressed by weakening analyzers or central settings.
- Avoid adding NuGet packages opportunistically. ADR 0003 requires native-first, demand-driven admission evidence.

Read [`references/csharp-setup-and-interop.md`](references/csharp-setup-and-interop.md) for exports, collections, source-generator constraints, builds, and interop.
