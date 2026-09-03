---
schema_version: '1.1'
id: 'reference-5kwoza-development-agent-skills'
title: 'Development Agent Skills'
description: 'Provenance, adaptation policy, harness installation, and update procedure for project-local development skills.'
doc_type: 'reference'
status: 'active'
created: '2026-09-01'
updated: '2026-09-02'
owner: 'project-maintainers'
consumer: 'agent'
tags:
  - 'development'
  - 'documentation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
source:
  - 'https://github.com/gamedev-skills/awesome-gamedev-agent-skills'
  - 'https://github.com/figma/mcp-server-guide'
  - 'https://developers.openai.com/codex/skills'
  - 'https://code.claude.com/docs/en/skills'
confidence: 'high'
visibility: 'public'
license: 'MIT, Apache-2.0, and Figma Developer Terms'
---

# Development agent skills

This repository carries five project-local Godot skills and 14 official Figma MCP skills for both Claude Code and Codex. Four Godot skills are pinned adaptations of upstream guidance; `stac-architecture` is project-owned and routes every implementation through the active architecture decisions.

## Installed skills

| Skill | Purpose | Origin |
| --- | --- | --- |
| `godot-csharp` | Godot 4.7.2/.NET 8 C#, generated bindings, builds, and interop | Adapted upstream |
| `godot-nodes-scenes` | Scene composition, instancing, lifecycle, ownership, and bounded autoload use | Adapted upstream |
| `godot-ui-control` | Responsive Control layout, themes, focus, mouse input, and accessible resizing | Adapted upstream |
| `godot-signals-groups` | Scene-local notification, groups, presentation, and adapter patterns | Adapted upstream |
| `stac-architecture` | Concise project boundary and ADR router | Project-owned |
| `figma-*` (14 skills) | Figma design, FigJam, Slides, diagrams, motion, code-connect, and design-to-code workflows | Figma official |

Each skill is present as a byte-identical pair under `.claude/skills/<name>/` and `.codex/skills/<name>/`. Harness metadata therefore does not diverge from substantive guidance.

The Figma MCP server is project-scoped in both `.mcp.json` and `.codex/config.toml` at `https://mcp.figma.com/mcp`. After opening this repository in Codex, authorize the configured server interactively with `codex mcp login figma`; credentials remain outside the repository.

## Figma MCP pin and inventory

- Repository: <https://github.com/figma/mcp-server-guide>
- Default branch: `main`
- Commit: `ae7e5e5f80da20f1dd7445e0c6ae5ac58a5b0bce`
- Commit date: `2026-09-01`
- Version: `figma_prod@2.2.107`
- Terms: [Figma Developer Terms](https://www.figma.com/legal/developer-terms/); Figma publishes these skills as beta resources.

The import is the complete `skills/` tree at that commit: `figma-code-connect`, `figma-create-new-file`, `figma-design-to-code`, `figma-generate-design`, `figma-generate-diagram`, `figma-generate-library`, `figma-generative-plugins`, `figma-implement-motion`, `figma-shaders`, `figma-swiftui`, `figma-use`, `figma-use-figjam`, `figma-use-motion`, and `figma-use-slides`. The files are byte-identical upstream copies, including references and JavaScript helper scripts. Markdown tooling excludes only these vendor trees, preserving official formatting.

## Upstream pin and inventory

- Repository: <https://github.com/gamedev-skills/awesome-gamedev-agent-skills>
- Default branch: `main`
- Commit: `7110607ab816ece9669274bc84937857a8819796`
- Commit date: `2026-08-24`
- License: Apache License 2.0; the pinned text is retained at [`LICENSES/Apache-2.0.txt`](../LICENSES/Apache-2.0.txt).
- NOTICE: the pinned notice is retained at [`LICENSES/awesome-gamedev-agent-skills-NOTICE.txt`](../LICENSES/awesome-gamedev-agent-skills-NOTICE.txt).

The import comparison is intentionally limited to these eight upstream files:

1. `skills/godot/godot-csharp/SKILL.md`
2. `skills/godot/godot-csharp/references/csharp-setup-and-interop.md`
3. `skills/godot/godot-nodes-scenes/SKILL.md`
4. `skills/godot/godot-nodes-scenes/references/tree-and-instancing.md`
5. `skills/godot/godot-ui-control/SKILL.md`
6. `skills/godot/godot-ui-control/references/layout-and-theming.md`
7. `skills/godot/godot-signals-groups/SKILL.md`
8. `skills/godot/godot-signals-groups/references/signal-patterns.md`

The pinned upstream `LICENSE` and `NOTICE` are also compared. No other upstream skill or support file is imported.

## Local adaptations

Every adapted file carries a modified-from-upstream notice and links to the retained license and NOTICE. The adaptation:

- makes C# the normal example language and retains dynamic GDScript only where addon or mixed-language interop requires it;
- targets the repository-controlled Godot 4.7.2, `net8.0`, SDK 10.0.111, and GdUnit4 6.2.0 environment;
- presents complete C# examples with file-scoped namespaces, documented public APIs, and the mechanically enforced `_camelCase` private-field convention;
- distinguishes the installed xUnit and GdUnit4 test baseline from the CsCheck, GdUnit4Net, and ArchUnitNET tools selected by ADR 0009 for admission when qualifying needs appear;
- routes authoritative simulation, domain state, space, time, scheduling, randomness, content, saves, AI, narrative consequences, and units to `AlterCourse.Core`;
- constrains Nodes, scenes, transforms, Resources, signals, groups, UI, and lifecycle to engine/presentation roles;
- removes generic advice for global game-state autoloads, global event buses, scene-tree persistence, ambient randomness, and GDScript-first implementation;
- omits links to upstream skills that are not installed locally and points agents at mechanically enforced conventions instead of creating competing style guidance.

Practical material retained includes partial Godot object classes, lifecycle signatures, exports, signals/events, typed node lookup, Variant and collection boundaries, build behavior, PackedScene instancing, ownership, layout, containers, size flags, themes, focus, mouse filtering, signal flags, groups, and necessary interop.

## Harness discovery and registration

[Anthropic's skill documentation](https://code.claude.com/docs/en/skills) defines project skills under `.claude/skills`, so Claude Code discovers that tree directly.

[Official OpenAI Codex skill documentation](https://developers.openai.com/codex/skills) documents repository discovery under `.agents/skills`. This project deliberately does not modify its standards-managed `.agents/skills` tree. It retains the required `.codex/skills` mirror and registers each `SKILL.md` with repository-relative `[[skills.config]]` entries in `.codex/config.toml`. The registration is a harness-specific wrapper; the paired skill bodies remain the parity authority.

## Semantic parity contract

`scripts/check-agent-skill-parity.sh` verifies all 19 skill directories and requires byte-identical Claude/Codex file inventories and contents. `scripts/verify.sh` runs that check as part of the canonical gate. A change to either harness copy must update its peer in the same commit.

## Update procedure

1. Fetch the upstream repository and check out the intended immutable commit.
2. For Godot skills, read only the eight listed files plus upstream `LICENSE` and `NOTICE`; record the new full SHA, date, and default branch here.
3. Compare each upstream file with its local adaptation. Preserve useful upstream corrections while reapplying the architecture, C#-first, and version constraints above.
4. Update the retained Apache-2.0 and NOTICE files byte-for-byte when upstream changes them.
5. Keep the modified-file notice in every adapted file and update its pin.
6. Copy the completed Claude tree to the Codex tree and run `scripts/check-agent-skill-parity.sh`.
7. For Figma skills, replace the complete paired trees from the pinned `skills/` source without reformatting them, update the Figma pin and inventory above, and run the parity check.
8. Search all installed Godot skill text for guidance that could violate the Core/Godot boundary, qualify every necessary engine-only mention, and run the canonical gate.

Do not modify imported Figma skill bodies, introduce a generator, or change global harness configuration as part of an update.
