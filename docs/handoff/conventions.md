# Project Conventions

## Quick reference

| ID    | Convention                                            |
| ----- | ----------------------------------------------------- |
| C-001 | Run the canonical quality gate before completion.     |
| C-002 | Keep simulation code independent from Godot.          |
| C-003 | Make diagnostic exceptions explicit and narrow.       |
| C-004 | Follow the protected branch and release lifecycle.    |
| C-005 | Keep formatter and semantic-style ownership separate. |
| C-006 | Keep Godot project metadata editor-discoverable.      |
| C-007 | Keep Godot UI styling in the project-owned Theme.     |

## C-001: Canonical quality gate

Run `./scripts/fix.sh` when formatting is appropriate, then run `./scripts/verify.sh` before declaring work complete. CI invokes the same verification script.

## C-002: Simulation boundary

Keep pure gameplay and simulation in `AlterCourse.Core`. Godot may reference Core, but Core never references Godot or ambient nondeterministic APIs.

## C-003: Diagnostic exceptions

Fix findings at their cause. Any suppression must be narrow, justified, and listed in `config/diagnostic-suppressions.allowlist`.

## C-004: Protected branch and release lifecycle

Develop from `dev` on issue-named branches and merge significant work by PR. Squash into `dev`; release from `main`; return hotfixes to `dev`. Direct `dev` pushes are handoff paths or T0 prose; hooks and CI enforce this.

## C-005: Formatting and semantic style

CSharpier owns C# whitespace; bare `dotnet format` is noncanonical. EditorConfig and analyzers own semantic style, including `_camelCase` fields. Prettier owns Markdown/config; shfmt owns shell whitespace.

## C-006: Godot editor metadata

Set `config/features` so Godot Tools detects Godot 4 C#. Reload VS Code after metadata corrections.

## C-007: Godot command-interface presentation

Use the project-owned Godot Theme for semantic colors, typography, control states, and focus treatment. Keep Figma and PNGs as visual references; Core remains simulation truth.
