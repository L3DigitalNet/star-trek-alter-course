# 004: Import Godot source assets before direct launch

## Cause

The tracked launcher restored and built the Debug assembly, then started the game without running Godot's asset import. A fresh checkout therefore had no `.godot` mappings for newly tracked fonts, so the theme and dependent scenes failed to load.

## Fix

`scripts/launch-game.sh` now runs Godot's headless `--import` mode after the build and before the requested launch. The behavior test verifies ordering, argument isolation, and fail-closed restore, build, and import paths.

## Lesson

A current managed assembly does not imply current imported presentation assets. The supported launch boundary must establish both before it opens a game scene.
