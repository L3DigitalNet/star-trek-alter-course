---
schema_version: '1.1'
id: 'reference-11owgq-development-and-governance'
title: 'Development and Governance'
description: 'Repository toolchain, validation, contribution workflow, and durable documentation ownership.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'development'
  - 'validation'
aliases: []
related:
  - 'CONTRIBUTING.md'
  - 'docs/development-quality.md'
  - 'docs/development-agent-skills.md'
  - 'docs/adr/0013-use-dev-for-development-and-main-for-releases.md'
---

# Development and governance

[Wiki home](README.md) · [Architecture](architecture.md) · [Sources](sources.md)

## Repository toolchain

The reviewed repository pins Godot 4.7.2 .NET/C#, .NET SDK 10.0.111, C# 12, and Node 24. These are repository selections, not claims about the newest upstream releases. Core, Godot, and Core tests target .NET 8; AssetCtl and its tests target .NET 10. The supported source-development environment is Linux x86_64.

Exact tool and package versions belong in their existing configuration and lock files. Consult [global.json](../../global.json), [Directory.Build.props](../../Directory.Build.props), [Directory.Packages.props](../../Directory.Packages.props), and the [development runbook](../development-quality.md) rather than editing versions through wiki prose.

## Quality gate

`./scripts/verify.sh` is the canonical ordered, fail-fast, tracked-file-read-only gate used by contributors and CI. `./scripts/fix.sh` applies supported formatting. CSharpier owns C# whitespace; compiler/SDK analyzers, Meziantou, and banned-API rules enforce correctness and semantic style. Warnings fail builds.

The canonical path covers locked restore/build, Core and AssetCtl tests, offline asset validation, Godot integration/smoke, formatting, shell/workflow analysis, secret scanning, and repository policies. Managed Project Standards checks complement it with Markdown/frontmatter policy; they are not silently reimplemented in another gate.

Use ordinary xUnit tests for pure behavior and vendored GdUnit4 for current engine integration. ADR-selected additional testing tools remain demand-driven. `./scripts/test-mutation.sh` is a separate deep-validation path without an invented mutation-score threshold.

The launcher restores/builds before running Godot and handles source asset preparation. A stale Debug assembly or unimported asset is not evidence that the current Core rules are wrong; consult the [recorded gotchas](../handoff/bugs/INDEX.md).

## Branches, issues, and releases

ADRs and the repository's adopted workflow remain authoritative. Ordinary work starts from permanent protected `dev`, with significant work governed by an issue and a named topic branch. Open a draft PR with exactly one Final, Supporting, or qualifying Standalone declaration. Normal topic PRs squash into `dev`; `main` is release-only.

The installed `gh-workflow` tool owns typed issue fields and lifecycle validation, readiness, merge, and terminal-state synchronization. Follow [AGENTS.md](../../AGENTS.md), the installed skill, and [CONTRIBUTING](../../CONTRIBUTING.md). A connected-session capability gap must be disclosed in its work record; it does not create a standing alternative workflow or permission to bypass checks.

Release promotion uses a governed merge commit, a corresponding tag and immutable release, and synchronization back to `dev`. A source-only release does not imply a binary, installer, Godot export, or distribution pipeline. Narrow T0 and Handoff direct-admission exceptions do not apply to substantial design specifications or normative decisions.

## Documentation and agent ownership

This wiki organizes durable design knowledge. Existing ADRs, detailed designs, and specifications retain their authority. The [source catalog](sources.md) is the coverage index, the [decision register](decision-register.md) distinguishes decisions from implementation, and [open questions](open-questions.md) prevents proposals from becoming accidental commitments.

Operational facts still belong in STATUS, TODO, and the appropriate handoff files. Keep eager state small. Preserve owner-authored tasks and historical records. Do not edit standard-owned skills, hooks, or lock inventories merely to make documentation checks pass.

Managed Markdown uses the existing frontmatter schema, stable IDs, canonical quoted fields, Prettier, and markdownlint. A page being active does not mean its planned system is implemented. Future changes should update an owning design, implementation evidence, and wiki summary in the same governed work where practical.

## Licensing and external content

ST:AC is an unofficial noncommercial fan project. The repository's MIT grant applies within its stated boundaries to original software, not to Star Trek IP or third-party materials. Noncommercial status is not a blanket permission to copy reference text, art, audio, or scripts.

Follow [LICENSE](../../LICENSE.md), [LEGAL](../../LEGAL.md), contribution requirements, and asset provenance/rights rules. The wiki links existing references; it does not import a canon database or assert legal clearance for future content.

## Sources

[Development quality](../development-quality.md), [agent skills](../development-agent-skills.md), [quality-gate ADR](../adr/0002-use-one-canonical-quality-gate.md), [branch/release ADR](../adr/0013-use-dev-for-development-and-main-for-releases.md), and [branch governance discovery](../design/branch-release-governance.md).
