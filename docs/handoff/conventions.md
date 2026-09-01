# Project Conventions

## Quick reference

| ID    | Convention                                        |
| ----- | ------------------------------------------------- |
| C-001 | Run the canonical quality gate before completion. |
| C-002 | Keep simulation code independent from Godot.      |
| C-003 | Make diagnostic exceptions explicit and narrow.   |

## Numbered conventions

### C-001: Canonical quality gate

Run `./scripts/fix.sh` for deterministic formatting when appropriate, then run `./scripts/verify.sh` before declaring work complete. CI invokes the same verification script.

### C-002: Simulation boundary

Put pure gameplay and simulation behavior in `AlterCourse.Core`. Godot-facing code may reference Core, but Core must not reference Godot or ambient nondeterministic APIs.

### C-003: Diagnostic exceptions

Fix compiler and analyzer findings at their cause. Any necessary suppression must be narrow, justified, and entered in `config/diagnostic-suppressions.allowlist`; casual source or MSBuild suppression is rejected.
