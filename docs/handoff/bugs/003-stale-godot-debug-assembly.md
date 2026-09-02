# 003: Rebuild the Godot Debug assembly before direct launch

## Cause

The README's direct Godot command can reuse an ignored local Debug assembly after a branch change. The stale assembly still requested the removed V1 ship-definition schema and legacy quick-save path.

## Fix

Close Godot, resolve the repository SDK, run a locked restore, build `AlterCourse.Godot` in Debug with warnings-as-errors, then relaunch. A tracked launch and documentation remediation remains open.

## Lesson

Ignored Godot .NET build output is not guaranteed to match the checked-out source. Manual-launch instructions must establish a current assembly before starting Godot.
