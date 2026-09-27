---
schema_version: '1.1'
id: 'reference-n1jcy9-architecture'
title: 'Architecture'
description: 'Simulation authority, project boundaries, dependency policy, and architectural decision map.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-27'
tags:
  - 'architecture'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0014-use-an-extensible-bounded-ship-system-substrate.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/engineering-and-combat.md'
  - 'docs/wiki/ship-system-substrate.md'
  - 'Directory.Packages.props'
---

# Architecture

[Wiki home](README.md) · [Decision register](decision-register.md) · [Source catalog](sources.md)

## Current project boundaries

`AlterCourse.Core` owns the deterministic world, commands, rule evaluation, navigation, observations, Engineering, combat, orders, factions, scheduling, authored-definition interpretation, and snapshot mapping. It has no Godot dependency. `AlterCourse.Godot` references Core and owns scenes, input, rendering, selection, presentation timing, coordinate conversion, and UI state. Player-facing projections are deliberately narrower than world truth.

`AlterCourse.AssetCtl` is standalone development infrastructure. It references neither game project, and neither game project references it. It exchanges selected visual assets and provenance manifests through files, not runtime game authority.

A future `AlterCourse.Narrative` assembly is an ADR-defined integration direction, not a current project. If a qualifying branching feature admits a narrative runtime, Narrative may reference Core and Godot may reference both; Core must not depend on Narrative.

## Domain boundaries and transactions

Model responsibilities according to systems, not UI screens. Existing `ShipState` composes strategic, tactical, Engineering, order, sensor-knowledge, autonomous-contact, and combat state, plus an optional direct faction controller. `SimulationState` owns plural ships and factions, scheduler, map, allocators, and player identity. Commands validate and construct candidate state before committing consequential changes where required; failed loading never partially replaces the live game.

Avoid parallel mechanisms. New strategic decisions should consume established information projections and issue established domain commands where those fit. The political design does not authorize an entity framework, a universal faction/organization base class, or a generalized rules engine.

## Implemented faction slice

[Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) is implemented in Feature #86 / Final PR #87, merged into `dev` as `0217296`. Core has stable root faction identity and bounded consequential state, one optional asset-side direct faction controller with a derived roster, and a pure policy that issues existing ship-order commands only to idle controlled NPC ships. The player ship is excluded from faction autonomous control in the proof.

The policy input is a narrow own-asset administrative projection, not unrestricted world/ship state or a faction sensor network. The scheduler has closed Ship/Faction targets with exact typed validation and preserved ordering. Its persisted items are data only: stable identity, due time, same-time sequence, target, and known kind; executable callbacks do not cross the save boundary. Faction state, controller links, and initial work belong in typed bootstrap and complete candidate validation, not post-construction proof mutations.

Faction Intent and Autonomous Assignment introduced V7 with a non-inventive V6→V7 migration and zero-faction validity. Observation-Driven Faction Response advances the released format to V8 without inventing report or investigation history. v0.5.0 remains the historical V6 release. No organization/controller abstraction, hierarchy runtime, random policy, new dependency/framework, or political UI is part of these slices. These choices implement existing ADRs 0005-0007 and 0010; no ADR changes.

The Godot adapter privately loads faction content to construct and restore a valid Core aggregate. Current development uses V10 saves and retains the supported migration chain; V8 remains the released format. The adapter does not project hidden controller, faction, objective, report, investigation, or scheduled-work state; ordinary observed vessels remain available through player-safe contact projections. Malformed faction or response data fails without corrupting the live shell.

## Implemented first combat engagement

[M6A](engineering-and-combat.md#implemented-m6a-first-engagement), implemented by [PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112), extends the existing ship aggregate rather than creating a second combat world. Following the substrate migration in PR #123, Engineering owns the installed-system collection, installation identity continuation, and one repair slot. Installations own condition, applicable power allocation, and typed specialized state, including directed-energy readiness on the installed weapon. Ship-owned combat state retains at most one correlated defensive stimulus. Tactical position, local sensor knowledge, system condition, and power are not duplicated in an encounter object.

The shared Core shot transition validates an observer-local contact, applies shield absorption and subsystem penetration, reconciles involuntary power/speed changes, cancels an affected repair by exact work identity, and reconciles observations before committing a valid candidate. Voluntary allocation/course rejection remains distinct from forced damage reconciliation. Godot never repairs an invalid Core aggregate.

The separate defensive policy receives only the acting ship's own facts and local contact knowledge. One delayed ship-targeted decision wake consumes a bounded stimulus and proposes Return Fire, Withdraw, or Hold through existing command paths. It does not introduce political hostility, autonomous strategic preemption, periodic weapon scheduling, or an external AI dependency. The Combat workspace is a presentation mode, not a Core encounter state.

The detailed rules belong to [Engineering and combat](engineering-and-combat.md); [content and persistence](content-assets-and-persistence.md) owns current V6 ship definitions, V1 system definitions, and V10 continuation, as well as the historical V5/V9 M6A boundary. M6A is unreleased development, M6 remains partial, and these implementation boundaries do not approve later combat refinements.

## Extensible ship-system boundary

[ADR 0014](../adr/0014-use-an-extensible-bounded-ship-system-substrate.md) governs the implemented extensible bounded ship-system substrate. `ShipSystemKind`, `SystemDefinitionId`, and `InstalledSystemId` distinguish semantic kind, reusable system definition, and installed system instance. A ship design owns an initial loadout; each live ship instance owns its actual installed systems. Same-class ships can retain different live loadouts across save/load independently of later class defaults; refit/replacement/removal/installation gameplay remains future work.

The substrate supports zero, one, or multiple installed instances of a kind when that kind's typed domain rules permit it. Common mechanics—condition, damage, repair, power allocation, persistence, generic projection, and Engineering presentation—must operate over the bounded installed-system set rather than repeated named fields/switches. System-specific effects remain explicit typed domain behavior; ADR 0014 does not admit an ECS, generic behavior engine, or arbitrary component framework.

[Issue #121](https://github.com/L3DigitalNet/star-trek-alter-course/issues/121) is complete through Final [PR #123](https://github.com/L3DigitalNet/star-trek-alter-course/pull/123), merged into `dev` as `17637dd` and unreleased, governed by the [ship-system substrate contract](ship-system-substrate.md). Runtime, content, persistence, projections, and Godot Engineering use the installed-system model. Historical DTOs and isolated migration adapters retain old field names; current capture writes V10 directly from live installations without flattening through a historical shape.

Common cardinality support is separate from current typed zero-or-one gameplay restrictions. Generic own-ship operations address installations; external targeting uses actor knowledge and semantic aim, not hidden installed inventory. The owning contract defines these distinctions and the evidence required before conformance can be claimed. It deliberately does not select recovery gameplay or refit costs, slots, or aggregation mechanics.

## Dependency choices

Start with existing domain code and the .NET standard library for Core, and native Godot capabilities for presentation. Admit focused packages for demonstrated needs, with compatibility, licensing, transitive/native dependency, headless determinism, and replacement-boundary evidence. Central versions and lock files remain authoritative; the wiki is not a competing version catalog.

At this baseline, central package declarations include xUnit, JsonSchema.Net, Serilog and logging adapters, analyzers, Core-test-only CsCheck and ArchUnitNET, and AssetCtl packages such as YamlDotNet, SkiaSharp, and Svg.Skia. Package presence does not mean every project consumes it. Inspect project references and the [dependency admission records](../dependency-admission/) before extending usage.

The reviewed ADR-conformance implementation admits CsCheck 4.9.1 and ArchUnitNET 0.13.4 only to `AlterCourse.Core.Tests`, with `PrivateAssets` boundaries and committed lockfile closure; it does not introduce either into Core, Godot, saves, or exports. GdUnit4Net remains conditional because there is no qualifying C# Godot integration test. ADR 0011 approves selective, bounded UnitsNet use for standard physical dimensions while retaining project-owned JSON, save, Godot-export, and fictional-quantity contracts. Stateless, LogicBlocks, Ink, and geometry/addon candidates remain conditional. Current Godot integration uses vendored GdUnit4; do not confuse that with installed GdUnit4Net.

## Cross-cutting decisions

The complete ADR catalog is indexed in [Sources](sources.md). Its governing boundaries are:

- ADRs 0001-0003: one-way Core/Godot separation, one canonical quality gate, and demand-driven dependency admission.
- ADRs 0004-0007: semantic multi-scale space, validated ordinary JSON content, explicit versioned JSON saves, and deterministic time/scheduling/randomness.
- ADRs 0008-0011: structured diagnostics, layered tests and Core-only architecture/property checks where their qualifying rules exist, information-limited explainable AI, and explicit quantities/units.
- ADRs 0012-0014: narrative subordinate to simulation, permanent `dev` with release-only `main`, and an extensible bounded ship-system substrate with typed system-specific behavior.

Gameplay composes Serilog at the Godot scene boundary through Microsoft logging abstractions. Core receives only an optional `ILogger<GameSimulation>` and emits allowlisted decision facts after a successful commit; logging failure cannot change the command or simulation outcome. The game composition owns the bounded file sink and fallback behavior; [gameplay logging admission](../dependency-admission/gameplay-logging.md) owns its concrete limits and removal boundary. Logs are not state, events are not serialized delegates, and presentation clocks are not simulation clocks. Determinism means equivalent semantic outcomes for the same supported rules/content/snapshot/commands, not permanent bitwise replay compatibility across arbitrary versions.

## Testing and observability

Use the lowest layer that can prove a rule: ordinary Core tests for domain behavior, persistence, AI, and long-running scenarios; Godot-aware tests for engine lifecycle, input, focus, resource imports, and adapters. Project references and focused architecture tests protect authority boundaries. Mutation testing is deep validation, not a substitute for meaningful examples and negative cases.

ADR 0008 selects Serilog configured at a composition boundary through Microsoft logging abstractions. Pure rules return typed facts/explanations when callers or tests need them. Diagnostic logging, sink configuration, and telemetry must not determine outcomes or expose hidden state to ordinary player projections.

## Sources

[Core/Godot separation](../adr/0001-separate-simulation-from-godot.md), [dependency policy](../adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md), [testing](../adr/0009-use-layered-testing-and-architecture-conformance.md), [ship-system ADR](../adr/0014-use-an-extensible-bounded-ship-system-substrate.md), [migration contract](ship-system-substrate.md), [narrative](../adr/0012-keep-branching-narrative-subordinate-to-simulation.md), [package declarations](../../Directory.Packages.props), [combat transition](../../src/AlterCourse.Core/Gameplay/GameSimulation.Combat.cs), [defensive policy](../../src/AlterCourse.Core/AI/DefensiveCombatDecisionPolicy.cs), and [development quality](../development-quality.md).
