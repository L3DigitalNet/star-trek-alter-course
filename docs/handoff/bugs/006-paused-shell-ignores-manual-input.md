# 006: Paused Godot shell ignores manual proof input until the next tick

## Cause

The Godot command shell only re-presents state when `GameSimulation` advances a tick. A manually paused simulation stops advancing, so button presses, hails, and other player actions during a manual proof appear to do nothing even though the click was received.

## Fix

No code change: this is a proof-procedure constraint, not a defect. Manual proofs must keep the simulation running (or single-step it) to observe the effect of an action instead of pausing and interacting.

## Lesson

When running a manual proof against the live shell, drive it while the simulation is running rather than paused; a paused shell will not re-present the outcome of an interaction until the next Core tick.
