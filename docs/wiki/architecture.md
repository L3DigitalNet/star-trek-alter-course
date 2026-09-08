---
schema_version: '1.1'
id: 'reference-n1jcy9-architecture'
title: 'Architecture'
description: 'Simulation authority, project boundaries, dependency policy, and architectural decision map.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-08'
tags:
  - 'architecture'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'Directory.Packages.props'
---

# Architecture

[Wiki home](README.md) · [Decision register](decision-register.md) · [Source catalog](sources.md)

## Current project boundaries

`AlterCourse.Core` owns the deterministic world, commands, rule evaluation, navigation, observations, engineering, orders, scheduling, authored-definition interpretation, and snapshot mapping. It has no Godot dependency. `AlterCourse.Godot` references Core and owns scenes, input, rendering, selection, presentation timing, coordinate conversion, and UI state. Player-facing projections are deliberately narrower than world truth.

`AlterCourse.AssetCtl` is standalone development infrastructure. It references neither game project, and neither game project references it. It exchanges selected visual assets and provenance manifests through files, not runtime game authority.

A future `AlterCourse.Narrative` assembly is an ADR-defined integration direction, not a current project. If a qualifying branching feature admits a narrative runtime, Narrative may reference Core and Godot may reference both; Core must not depend on Narrative.

## Domain boundaries and transactions

Model responsibilities according to systems, not UI screens. Existing `ShipState` composes strategic, tactical, engineering, order, sensor-knowledge, and autonomous state. `SimulationState` owns plural ships, scheduler, map, allocators, and player identity. Commands validate and construct candidate state before committing consequential changes where required; failed loading never partially replaces the live game.

Avoid parallel mechanisms. New strategic decisions should consume established information projections and issue established domain commands where those fit. The political design does not authorize an entity framework, a universal faction/organization base class, or a generalized rules engine.

## Implemented faction slice

[Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) is implemented in Feature #86 / Final PR #87, merged into `dev` as `0217296`. Core has stable root faction identity and bounded consequential state, one optional asset-side direct faction controller with a derived roster, and a pure policy that issues existing ship-order commands only to idle controlled NPC ships. The player ship is excluded from faction autonomous control in the proof.

The policy input is a narrow own-asset administrative projection, not unrestricted world/ship state or a faction sensor network. The scheduler has closed Ship/Faction targets with exact typed validation and preserved ordering. Its persisted items are data only: stable identity, due time, same-time sequence, target, and known kind; executable callbacks do not cross the save boundary. Faction state, controller links, and initial work belong in typed bootstrap and complete candidate validation, not post-construction proof mutations.

Faction Intent and Autonomous Assignment introduced V7 with a non-inventive V6→V7 migration and zero-faction validity. Observation-Driven Faction Response advances the released format to V8 without inventing report or investigation history. v0.5.0 remains the historical V6 release. No organization/controller abstraction, hierarchy runtime, random policy, new dependency/framework, or political UI is part of these slices. These choices implement existing ADRs 0005-0007 and 0010; no ADR changes.

The Godot adapter loads faction content only to build a valid Core aggregate and preserve V8 save compatibility. It does not project hidden controller, faction, objective, report, investigation, or scheduled-work state; ordinary observed vessels remain available through existing player-safe contact projections. Malformed faction or response data fails without corrupting the live shell.

## Dependency choices

Start with existing domain code and the .NET standard library for Core, and native Godot capabilities for presentation. Admit focused packages for demonstrated needs, with compatibility, licensing, transitive/native dependency, headless determinism, and replacement-boundary evidence. Central versions and lock files remain authoritative; the wiki is not a competing version catalog.

At this baseline, central package declarations include xUnit, JsonSchema.Net, Serilog and logging adapters, analyzers, and AssetCtl packages such as YamlDotNet, SkiaSharp, and Svg.Skia. Package presence does not mean every project consumes it. Inspect project references and the [dependency admission records](../dependency-admission/) before extending usage.

CsCheck, ArchUnitNET, GdUnit4Net, UnitsNet, Stateless, LogicBlocks, Ink, and the geometry/addon candidates named by ADRs are conditional selections or evaluation candidates, not blanket installed dependencies. Current Godot integration uses vendored GdUnit4; do not confuse that with installed GdUnit4Net.

## Cross-cutting decisions

The complete ADR catalog is indexed in [Sources](sources.md). Its governing boundaries are:

- ADRs 0001-0003: one-way Core/Godot separation, one canonical quality gate, and demand-driven dependency admission.
- ADRs 0004-0007: semantic multi-scale space, validated ordinary JSON content, explicit versioned JSON saves, and deterministic time/scheduling/randomness.
- ADRs 0008-0011: structured diagnostics, layered tests, information-limited explainable AI, and explicit quantities/units.
- ADRs 0012-0013: narrative subordinate to simulation and permanent `dev` with release-only `main`.

Logs are not state, events are not serialized delegates, and presentation clocks are not simulation clocks. Determinism means equivalent semantic outcomes for the same supported rules/content/snapshot/commands, not permanent bitwise replay compatibility across arbitrary versions.

## Testing and observability

Use the lowest layer that can prove a rule: ordinary Core tests for domain behavior, persistence, AI, and long-running scenarios; Godot-aware tests for engine lifecycle, input, focus, resource imports, and adapters. Project references and focused architecture tests protect authority boundaries. Mutation testing is deep validation, not a substitute for meaningful examples and negative cases.

ADR 0008 selects Serilog configured at a composition boundary through Microsoft logging abstractions. Pure rules return typed facts/explanations when callers or tests need them. Diagnostic logging, sink configuration, and telemetry must not determine outcomes or expose hidden state to ordinary player projections.

## Sources

[Core/Godot separation](../adr/0001-separate-simulation-from-godot.md), [dependency policy](../adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md), [testing](../adr/0009-use-layered-testing-and-architecture-conformance.md), [narrative](../adr/0012-keep-branching-narrative-subordinate-to-simulation.md), [package declarations](../../Directory.Packages.props), and [development quality](../development-quality.md).
