# Control layout and theming reference

> Modified from `awesome-gamedev-agent-skills` at commit `7110607ab816ece9669274bc84937857a8819796`. Adapted to C# and ST:AC architecture. See the [Apache-2.0 license](../../../../LICENSES/Apache-2.0.txt) and [upstream NOTICE](../../../../LICENSES/awesome-gamedev-agent-skills-NOTICE.txt).

## Anchors and offsets

A `Control` has four anchors in the `0.0`–`1.0` range and four pixel offsets.

- `(0,0,0,0)` tracks the parent's top-left; offsets determine fixed position and size.
- `(0,0,1,1)` tracks all parent edges; offsets become margins and the child resizes.
- centered anchors use `0.5`; offsets describe the rectangle around the center.

Use `SetAnchorsAndOffsetsPreset` or editor Layout presets so anchors and offsets change together.

## Containers

- `VBoxContainer` / `HBoxContainer`: vertical or horizontal stacks.
- `GridContainer`: column-based grid.
- `MarginContainer`: theme-controlled outer margins.
- `CenterContainer`: centers children at minimum size.
- `PanelContainer`: StyleBox-backed single-content panel.
- `ScrollContainer`: scrolls oversized content.
- `TabContainer`: tabbed child pages.
- `AspectRatioContainer`: preserves aspect ratio.
- `HSplitContainer` / `VSplitContainer`: resizable two-pane layout.
- `HFlowContainer` / `VFlowContainer`: wrapping flow layout.

Containers own child position and size. Express flow using `SizeFlags.Fill`, `Expand`, `ExpandFill`, and shrink flags plus `SizeFlagsStretchRatio`.

## Theme and StyleBox

```csharp
var theme = new Theme();
theme.SetColor("font_color", "Button", Colors.White);
theme.SetFontSize("font_size", "Button", 18);

var box = new StyleBoxFlat { BgColor = new Color("101824") };
box.SetCornerRadiusAll(8);
box.SetContentMarginAll(12);
theme.SetStylebox("normal", "Button", box);
root.Theme = theme;
```

`Theme` values cover colors, constants, fonts, font sizes, icons, and StyleBoxes. `StyleBoxFlat`, `StyleBoxTexture`, and `StyleBoxEmpty` cover common styling. Theme Resources are presentation assets and may be `.tres`; that permission does not extend to canonical domain content.

Per-node overrides win over inherited Theme values:

```csharp
title.AddThemeFontSizeOverride("font_size", 24);
title.AddThemeColorOverride("font_color", Colors.Gold);
column.AddThemeConstantOverride("separation", 16);
```

Prefer a shared Theme when multiple nodes need the same value.

## Focus and keyboard navigation

```csharp
control.FocusMode = Control.FocusModeEnum.All;
control.GrabFocus();
control.FocusNeighborBottom = next.GetPath();
control.FocusNext = tabTarget.GetPath();
```

Automatic geometric navigation is useful for simple layouts; explicit neighbors prevent surprising movement in complex or changing layouts. When rebuilding a list, restore focus to a surviving meaningful control.

## Mouse interaction

- `Stop` handles the event and blocks propagation.
- `Pass` handles locally and permits propagation to ancestors.
- `Ignore` does not receive mouse events and allows lower Controls to receive them.
- Use `GuiInput(InputEvent)` for custom pointer/drag handling and ordinary control signals for buttons, toggles, text submission, and value changes.

## Layering and resizing

Place HUD Controls beneath `CanvasLayer` to remain independent of the world camera. Test minimum window dimensions, changed aspect ratios, content scaling, keyboard-only input, pointer input, long strings, and dynamic visibility. A container-driven layout should reflow without embedding domain decisions in visibility callbacks.
