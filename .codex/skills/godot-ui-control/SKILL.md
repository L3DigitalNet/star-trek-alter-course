---
name: godot-ui-control
description: Build responsive, themed, keyboard- and mouse-accessible Godot 4.7.2 C# interfaces with Control nodes while projecting Core state and submitting explicit typed operations.
---

# Godot UI and Control nodes

> Modified from `awesome-gamedev-agent-skills` at commit `7110607ab816ece9669274bc84937857a8819796`. Adapted to C# and ST:AC architecture. See the [Apache-2.0 license](../../../LICENSES/Apache-2.0.txt) and [upstream NOTICE](../../../LICENSES/awesome-gamedev-agent-skills-NOTICE.txt).

UI projects read-only Core/application state and turns user actions into explicit typed intent. A callback may validate UI input, but domain legality and mutation belong behind the application/Core boundary. Do not put combat, diplomacy, mission, scheduling, or persistence rules in Controls.

## Layout workflow

1. Use `Control` derivatives for interface elements and a `CanvasLayer` for viewport-fixed HUD layers.
2. Use anchors and offsets for parent-relative layout. Start from editor layout presets.
3. Let `Container` nodes arrange children; child anchors and manual positions do not control container layout.
4. Use horizontal/vertical size flags and minimum sizes to express expansion and shrink behavior.
5. Apply a shared `Theme` to a subtree; reserve per-node overrides for genuine exceptions.
6. Establish initial focus, tab order or focus neighbors, mouse filtering, and layouts that remain usable when text or window size changes.

```csharp
namespace AlterCourse.Godot.UI;

/// <summary>Presents target orders and emits explicit engagement intent.</summary>
public partial class OrdersPanel : VBoxContainer
{
    private Button _engage = null!;

    /// <summary>Notifies the application boundary that engagement was requested.</summary>
    public event Action<EngageTargetIntent>? EngageRequested;

    /// <inheritdoc />
    public override void _Ready()
    {
        _engage = GetNode<Button>("%Engage");
        _engage.Pressed += OnEngagePressed;
        _engage.GrabFocus();
    }

    /// <summary>Displays the supplied target projection without changing domain state.</summary>
    public void Present(TargetProjection target)
    {
        _engage.Disabled = !target.CanRequestEngagement;
        _engage.Text = $"Engage {target.DisplayName}";
    }

    private void OnEngagePressed()
    {
        EngageRequested?.Invoke(new EngageTargetIntent());
    }
}
```

The UI describes available intent; the command handler decides whether the action is still legal.

## Responsive details

```csharp
panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
panel.MouseFilter = Control.MouseFilterEnum.Ignore;
spacer.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
firstButton.FocusNeighborBottom = secondButton.GetPath();
```

- Anchors are fractions of the parent rectangle; offsets are pixel deltas from anchored edges.
- Full Rect uses anchors `(0, 0, 1, 1)` and offsets as margins.
- Containers use each child's custom minimum size, theme minimums, and size flags.
- A full-rect decorative Control should usually ignore mouse input so it does not block interactive descendants or lower layers.

## Accessibility-relevant behavior

- Ensure keyboard/gamepad focus is visible and starts on a useful control.
- Define explicit neighbors when geometric navigation is ambiguous; preserve `ui_accept`, `ui_cancel`, and focus navigation actions.
- Exercise minimum supported window size, long/localized labels, scale changes, and dynamic lists. Avoid fixed pixel assumptions that clip text.
- Use semantic labels and tooltips where the visual alone is ambiguous. Do not require hover as the only way to discover or perform an action.
- Choose `MouseFilterEnum.Stop`, `Pass`, or `Ignore` deliberately for layered interfaces.

Read [`references/layout-and-theming.md`](references/layout-and-theming.md) for anchor math, containers, size flags, themes, StyleBoxes, focus, mouse interaction, and layering.
