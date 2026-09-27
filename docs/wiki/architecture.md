---
schema_version: '1.1'
id: 'reference-n1jcy9-architecture'
title: 'Architecture'
description: 'Maps simulation authority, command commitment, information ownership, assets, session lifetime, and the governing ADRs.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-27'
tags:
  - 'architecture'
aliases: []
related:
  - 'docs/adr/README.md'
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0014-use-an-extensible-bounded-ship-system-substrate.md'
  - 'docs/adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md'
  - 'docs/adr/0016-own-actor-knowledge-and-share-immutable-observation-reports.md'
  - 'docs/adr/0017-generate-assets-outside-the-game-through-bounded-validated-publication.md'
  - 'docs/adr/0018-separate-simulation-session-lifetime-from-workspaces.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/engineering-and-combat.md'
  - 'docs/wiki/ship-system-substrate.md'
  - 'Directory.Packages.props'
---

# Architecture

[Wiki home](README.md) · [ADR catalog](../adr/README.md) · [Decision register](decision-register.md) · [Source catalog](sources.md)

## Current project boundaries

`AlterCourse.Core` owns the world, rules, commands, navigation, observations, Engineering, combat, orders, factions, scheduling, definition interpretation, and snapshot mapping. It has no Godot dependency. `AlterCourse.Godot` references Core and owns scenes, input, rendering, selection, presentation timing, coordinate conversion, and UI state. Player-facing projections are deliberately narrower than world truth.

`AlterCourse.AssetCtl` is independent development infrastructure: it references neither game assembly and neither references it. The game consumes selected asset files, not a running generation service. [ADR 0017](../adr/0017-generate-assets-outside-the-game-through-bounded-validated-publication.md) owns that boundary; the [tool specification](asset-pipeline-tool.md) owns its detailed contract.

A future `AlterCourse.Narrative` assembly is a conditional ADR-defined direction, not an installed project. If a qualifying branching feature admits it, Narrative may reference Core and Godot may reference both; Core must not depend on Narrative.

## Domain boundaries and transactions

Model responsibilities according to systems, not screens. `ShipState` composes strategic, tactical, Engineering, order, sensor-knowledge, autonomous-contact, and combat state, plus an optional direct faction controller. `SimulationState` owns plural ships/factions, scheduler, map, allocators, and player identity.

[ADR 0015](../adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md) owns the staged command boundary. Application revalidates current authority and capability, stages correlated consequences, and validates the complete candidate before commitment. Expected refusal of an atomic command preserves world state, time, IDs, and scheduled work. Internal helpers may compose an intermediate candidate; full aggregate validation belongs at the complete operation or batch boundary.

Command commitment and presentation are separate. A post-commit diagnostic or UI failure does not make a successful operation unapplied or justify automatically retrying it. ADR 0007 continues to govern advancement and permits a separately specified safely incremental operation; the new record does not silently remove that alternative. ADR 0006 separately governs durable snapshots and file replacement.

Avoid parallel mechanisms. AI proposes ordinary commands rather than mutating state while scoring candidates. Neither this transaction boundary nor the political design authorizes a command bus, entity framework, generalized rules engine, or universal faction/organization base class.

## Actor knowledge and reporting

[ADR 0016](../adr/0016-own-actor-knowledge-and-share-immutable-observation-reports.md) owns information production, provenance, retention, and transfer. World truth, actor-local knowledge, own-asset administrative facts, and received historical observations are distinct. ADRs 0004 and 0010 continue to own spatial and AI consumers.

A local contact ID is not global vessel identity. A report identifies its source and historical observation, not the hidden target's current state. Control does not automatically grant a live sensor union. Policy inputs, projections, target choices, disabled controls, and refusal reasons must all respect the admitted disclosure boundary.

[The sensor contract](sensors-knowledge-and-ai.md), [strategic reporting](strategic-contact-reporting.md), and [observation response](observation-driven-faction-response.md) retain their detailed lifecycle, delay, capacity, and investigation rules. Broader affiliation learning, cross-observer correlation, and sharing remain scoped or open in the question register.

## Implemented faction slice

[Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) supplies stable root-faction identity, bounded consequential state, one optional asset-side direct faction controller, and a derived roster. Its policy issues existing ship orders only to eligible idle controlled NPC ships; the proof excludes the player from faction autonomous control.

The policy receives narrow own-asset administrative facts, not unrestricted world state. The scheduler uses closed Ship/Faction targets and persisted data: stable work identity, due time, same-time sequence, target, and known kind. Faction state, controller links, and initial work enter through typed bootstrap and complete candidate validation.

[Observation-Driven Faction Response](observation-driven-faction-response.md) adds delayed immutable direct-faction reports and bounded investigation. These slices introduced V7 and V8 without inventing historical factions, reports, or investigations during migration. They introduced no hierarchy/organization runtime, political UI, random policy, or generic controller framework. Current development captures V10; [persistence](content-assets-and-persistence.md) owns the supported chain.

Godot privately loads the catalogs needed to construct or restore Core. It does not expose hidden faction, objective, controller, report, investigation, or scheduled-work state. Ordinary vessels remain visible only through legitimate player knowledge.

## Implemented first combat engagement

[M6A](engineering-and-combat.md#implemented-m6a-first-engagement), delivered by [PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112), extends the ship aggregate rather than creating a second combat world. After the substrate migration, installations own condition, applicable allocation, and typed specialized state such as weapon readiness. Engineering owns the installed collection and one repair slot; ship combat state retains a bounded correlated defensive stimulus.

The shared shot transition validates observer-local targeting, applies shield absorption and subsystem penetration, reconciles forced power/speed changes, cancels affected repair work, and reconciles observations before commitment. Voluntary rejection is not forced-damage reconciliation; Godot never repairs an invalid Core aggregate.

The defensive policy sees own facts and local knowledge and proposes Return Fire, Withdraw, or Hold through existing command paths. It does not create political hostility, strategic preemption, periodic weapon work, or an external AI dependency. Combat is a presentation workspace, not a second Core encounter authority.

The [Engineering contract](engineering-and-combat.md) owns mechanics; [persistence](content-assets-and-persistence.md) owns historical V5/V9 admission and current ship V6/system V1/save V10. M6A is unreleased and M6 remains partial.

## Extensible ship-system boundary

[ADR 0014](../adr/0014-use-an-extensible-bounded-ship-system-substrate.md) separates `ShipSystemKind`, `SystemDefinitionId`, and `InstalledSystemId`. A ship design owns an initial loadout; each live vessel owns its actual installations. Same-class vessels can retain different loadouts independently of later class defaults.

Common condition, damage, repair, power, persistence, projection, and Engineering presentation operate over the bounded installed set. Typed rules decide specialized effects and allowed cardinality; common storage does not approve multi-generator, multi-sensor, shield, or weapon aggregation. No ECS or arbitrary behavior framework is admitted.

[Issue #121](https://github.com/L3DigitalNet/star-trek-alter-course/issues/121) completed this migration through [PR #123](https://github.com/L3DigitalNet/star-trek-alter-course/pull/123), merged as `17637dd` and unreleased. Historical wire models retain their old shapes; current V10 capture reads live installations directly. The [substrate contract](ship-system-substrate.md) owns compatibility, information safety, and evidence.

Own-ship operations address installations. Remote targeting uses semantic aim and actor knowledge, not hidden inventory. Refit costs, slots, removal/replacement gameplay, and recovery semantics remain separate decisions.

## Session and workspace lifetime

[ADR 0018](../adr/0018-separate-simulation-session-lifetime-from-workspaces.md) keeps session lifetime separate from workspace lifetime. `GameScreen` currently owns the live simulation and coordination; changing workspaces does not recreate it. That responsibility may be extracted for a concrete consumer without introducing a global manager.

A successful simulation replacement invalidates old actionable context, even when numeric IDs match. Own-ship Engineering actions use owner/generation binding and current payload resolution. Ordinary refresh preserves still-valid selection/focus; load rebuilds or clears context according to the interface contract. Deferred callbacks recheck target lifetime.

Preview fixtures are explicit development/test presentation and cannot substitute for unavailable live data or mutate/save a real world. Core snapshots, session interaction state, and application preferences remain distinct. [Interface and player commands](interface-and-player-commands.md) owns the concrete controls and replacement behavior.

## Dependency choices

Prefer existing domain code and .NET capabilities in Core, and native Godot presentation. Admit focused packages for demonstrated needs with compatibility, licensing, transitive/native dependency, deterministic/headless behavior, and removal-boundary evidence. Project references, central versions, and lock files are authoritative; this page is not a version catalog.

Current declarations include xUnit, JsonSchema.Net, Serilog/logging adapters, analyzers, Core-test-only CsCheck and ArchUnitNET, and AssetCtl packages such as YamlDotNet, SkiaSharp, and Svg.Skia. Presence in central declarations does not mean every project consumes a package. Consult [dependency admissions](../dependency-admission/) before expanding use.

CsCheck and ArchUnitNET remain test-only. Vendored GdUnit4 supports current Godot integration; GdUnit4Net remains conditional on a qualifying C# engine test. ADR 0011 and its [UnitsNet evaluation](../dependency-admission/unitsnet-evaluation.md) retain the approved bounded existing-quantity exception. Stateless, LogicBlocks, Ink, and geometry/addon candidates remain conditional rather than presumed installed.

## Cross-cutting decisions

The [ADR catalog](../adr/README.md) indexes all eighteen active records and maps their overlap. ADRs 0015–0018 supplement existing ownership rather than replacing scheduler, persistence, AI, content, or testing contracts.

Gameplay composes Serilog through Microsoft logging abstractions at the Godot boundary. Core receives an optional logger and emits allowlisted decision facts after successful commitment; logging failure cannot change the result. [Gameplay logging admission](../dependency-admission/gameplay-logging.md) owns concrete sink limits and removal boundaries.

Logs are not state, scheduled work is not a serialized delegate, and presentation clocks are not simulation clocks. Determinism means equivalent semantic outcomes for supported rules/content/snapshots/commands, not permanent bitwise replay across arbitrary versions.

## Testing and observability

Use the lowest layer that can prove a rule: ordinary Core tests for domain transitions, persistence, AI, and long horizons; Godot-aware tests for lifecycle, input, focus, imports, and adapters. Project references and focused architecture tests protect authority. Mutation testing supplements meaningful examples and negative cases.

The [191-obligation conformance register](../reviews/adr-conformance-2026-09-27.md) remains dated evidence for ADRs 0001–0014. It is not retroactive execution proof for later records. Each affected change must identify source/tests and actual verification without confusing inspected tests, inherited results, or documentation adoption with a fresh run.

## Sources

[ADR catalog](../adr/README.md), [source/test catalog](sources.md#implementation-evidence), [substrate contract](ship-system-substrate.md), [package declarations](../../Directory.Packages.props), [combat transition](../../src/AlterCourse.Core/Gameplay/GameSimulation.Combat.cs), [defensive policy](../../src/AlterCourse.Core/AI/DefensiveCombatDecisionPolicy.cs), and [development quality](../development-quality.md).
