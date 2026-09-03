# 005: Preserve Command Deck context actions across refreshes

## Cause

Each live projection refresh cleared and rebuilt the Context Actions container. The simulation refreshes about every 100 ms at normal rate, so pointer presses could span two button instances and keyboard focus was repeatedly lost.

## Fix

`CommandDeckWorkspace` now reconciles action buttons by stable presentation ID. Existing nodes receive current text, availability, tooltip, tone, and ordering; only added or removed actions change the scene tree. A Godot-aware regression test proves identity, focus, and command submission survive a live refresh.

## Lesson

Frequently refreshed Godot presentation should update stable interactive controls in place. Replacing focused or pressed controls makes input unreliable even when the action handler is correct.
