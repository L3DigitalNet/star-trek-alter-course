# 003: Rebuild the Godot Debug assembly before direct launch

## Cause

The README's direct Godot command could reuse an ignored local Debug assembly after a branch change. The stale assembly still requested the removed V1 ship-definition schema and legacy quick-save path.

## Fix

`scripts/launch-game.sh` now resolves the repository SDK, performs a locked restore, and builds `AlterCourse.Godot` in Debug with warnings-as-errors before launch. Its behavior test is part of canonical verification, and the README points direct launch to the script.

## Lesson

Ignored Godot .NET build output is not guaranteed to match the checked-out source. The tracked launch boundary must establish a current assembly before starting Godot.
