---
schema_version: '1.1'
id: 'index-5yghuo-sources'
title: 'Design Source Catalog'
description: 'Coverage index of original architecture, design, specification, workflow, implementation, and historical sources.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-07'
tags:
  - 'design'
  - 'architecture'
aliases: []
related:
  - 'docs/wiki/README.md'
  - 'docs/wiki/decision-register.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/implementation-status.md'
---

# Design source catalog

[Wiki home](README.md) · [Decision register](decision-register.md) · [Open questions](open-questions.md)

## Coverage and authority

This catalog inventories the project-owned design/specification corpus present in `dev`, originally reviewed September 6, 2026, and links later same-day approved design decisions. Original supporting files remain in place as detail and evidence. The wiki does not claim to have freshly executed every test or externally reverified every historical citation.

The wiki is the single source of truth for design; this catalog lists the documents that supply supporting detail, architectural decisions, implementation evidence, and history. Active ADRs record architectural decisions. Supporting design and specification documents supply detail within the scope their owning wiki page assigns them and may not contradict it. Historical records explain origin, and current code/tests resolve implementation claims. A source's future tense, example, package candidate, or archived assistant recommendation is not evidence of an implemented feature or owner approval.

## Review record

Targeted review, 2026-09-07:

- **Source:** `685a605`; merged Feature #86 / Final PR #87 commit `0217296`.
- **Scope:** faction landing claims and summaries in the root [README](../../README.md), [roadmap](../../ROADMAP.md), and this wiki's implementation, architecture, faction, knowledge, navigation, persistence, decision, and campaign pages.
- **Evidence:** static inspection of the [faction policy](../../src/AlterCourse.Core/AI/FactionAssignmentPolicy.cs), [runtime wakes](../../src/AlterCourse.Core/Gameplay/GameSimulation.Factions.cs), [V7 persistence](../../src/AlterCourse.Core/Persistence/GamePersistence.cs), and [targeted tests](../../tests/AlterCourse.Core.Tests/Gameplay/FactionAssignmentScenarioTests.cs). The audit used inherited PR #87 test evidence; this PR's verification records any reruns.
- **Findings and disposition:** corrected stale pending-landing/review-branch claims and README V6/current-rules claims. Current `dev` is V7 under `faction-intent-autonomous-assignment-v1`; v0.5.0 remains the V6 release. M3 and M5 remain incomplete.
- **Full-review cadence:** the initial 2026-09-06 full-corpus review remains the [Wiki home](README.md) provenance; the next full review is due 2026-09-13. This targeted review does not reset that date; follow the [recurring design-reconciliation procedure](development-and-governance.md#recurring-design-reconciliation).

## All active ADRs

- [0001 — Separate simulation from Godot](../adr/0001-separate-simulation-from-godot.md): one-way assembly dependency and pure Core testing.
- [0002 — One canonical quality gate](../adr/0002-use-one-canonical-quality-gate.md): pinned, fail-fast, read-only verification shared with CI.
- [0003 — Native capabilities and demand-driven dependencies](../adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md): ownership order, admission evidence, and excluded speculative frameworks.
- [0004 — Semantic spatial model](../adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md): scale relationships, graph/continuous space, and actor-safe map projections.
- [0005 — JSON and schema validation](../adr/0005-use-json-and-schema-validation-for-domain-content.md): ordinary domain content, stable IDs, semantic validation, and narrow specialized-format exceptions.
- [0006 — Versioned JSON snapshot saves](../adr/0006-use-versioned-json-snapshot-saves.md): persistence mapping, migration, validation, and atomic recovery boundaries.
- [0007 — Deterministic time, scheduling, and randomness](../adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md): explicit clocks, serializable consequences, stable order, and future random-stream contracts.
- [0008 — Structured observability with Serilog](../adr/0008-use-structured-observability-with-serilog.md): logging boundaries, typed diagnostics, sink behavior, and nonauthoritative logs.
- [0009 — Layered testing and architecture conformance](../adr/0009-use-layered-testing-and-architecture-conformance.md): unit/scenario/property/engine testing and conditional package admission.
- [0010 — Explainable domain AI](../adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md): actor knowledge, constraints, deterministic choice, typed commands, and demand-driven state machines.
- [0011 — Explicit physical quantities](../adr/0011-represent-physical-quantities-with-explicit-units.md): canonical units, bounded/fictional values, and conditional UnitsNet evaluation.
- [0012 — Narrative subordinate to simulation](../adr/0012-keep-branching-narrative-subordinate-to-simulation.md): future Narrative boundary, typed consequences, and Ink prototype trigger.
- [0013 — Dev development and main releases](../adr/0013-use-dev-for-development-and-main-for-releases.md): branch/merge policy, governed admission, tags, and immutable releases.

## Canonical wiki decisions and future design

- [Strategic Contact Reporting](strategic-contact-reporting.md): implemented after v0.4.0 and released in v0.5.0 (Feature #77, Final PR #78); durable reference-frame-qualified actor-safe last-known contact reporting with its original non-goals preserved.
- [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md): Feature #86 / Final PR #87 implementation of the bounded M5 slice, merged into `dev` as `0217296`; D-08 through D-13 cover its proof, control, knowledge, scheduler/bootstrap, V7 migration, and no-RNG decisions.
- [Factions and organizations](factions-and-organizations.md): owner-approved conceptual political framework; its hierarchy, organization, government, and relationship runtime remains future work, while the first assignment consumer implements only the root-faction/direct-control subset.
- [Open questions](open-questions.md): Q-01 and first-slice Q-05 resolved; Q-04/Q-06/Q-08/Q-14 scoped in part; Q-02/Q-03 and remaining intelligence, political, combat, campaign, and compatibility questions deferred.

## Supporting design and specification documents

- [Root overview and controls](../../README.md): present gameplay and source launch instructions, with the next design clearly separated.
- [Development roadmap](../../ROADMAP.md): implemented M1/M2/M4, partial M3/contact-reporting outcomes, the selected first M5 slice, and remaining M5-M9 boundaries.
- [Command Deck UI](../design/command-deck-ui.md): approved shell, Engineering workspace, visual language, runtime Theme, and preview policy.
- [First observed contact](../design/first-observed-contact.md): detailed M3A knowledge, scan/hail, cautious behavior, and V4-era contract; M4 supersedes its sensor-only Engineering description.
- [Engineering Backbone](../design/engineering-backbone.md): current M4 rules, proof values, content V4, save V5 (superseded by V6), repair/scan correlations, and live UI.
- [Branch/release governance discovery](../design/branch-release-governance.md): decision rationale; ADR 0013 governs the adopted outcome.
- [Asset pipeline tool specification](../specs/asset-pipeline-tool.md): full provider/configuration, validation, lifecycle, provenance, cost, rights, and publishing contract.

## Dependency and development references

- [AssetCtl dependency admission](../dependency-admission/assetctl.md) and [JsonSchema.Net Core admission](../dependency-admission/jsonschema-net-core.md).
- [Development quality](../development-quality.md) and [development agent skills](../development-agent-skills.md).
- [Contribution workflow](../../CONTRIBUTING.md) and [agent instructions](../../AGENTS.md).
- [SDK selection](../../global.json), [build policy](../../Directory.Build.props), [central packages](../../Directory.Packages.props), and [canonical verifier](../../scripts/verify.sh).
- [Asset configuration/catalog](../../config/assets/) and [AssetCtl tool](../../tools/AlterCourse.AssetCtl/).
- [License policy](../../LICENSE.md) and [legal notice](../../LEGAL.md): reference boundaries, not legal clearance for proposed external content.

## Implementation evidence

- [FirstGameSetup](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs) and [GameBootstrap](../../src/AlterCourse.Core/Gameplay/GameBootstrap.cs): current world and typed initialization.
- [ShipState](../../src/AlterCourse.Core/Ships/ShipState.cs), [SimulationState](../../src/AlterCourse.Core/Gameplay/SimulationState.cs), and [GameSimulation](../../src/AlterCourse.Core/Gameplay/GameSimulation.cs): current aggregate and commands.
- [ScheduledWork](../../src/AlterCourse.Core/Simulation/ScheduledWork.cs), [work kinds](../../src/AlterCourse.Core/Simulation/ScheduledWorkKind.cs), and [scheduler](../../src/AlterCourse.Core/Simulation/SimulationScheduler.cs).
- [AI](../../src/AlterCourse.Core/AI/), [sensors](../../src/AlterCourse.Core/Sensors/), and [ships/Engineering](../../src/AlterCourse.Core/Ships/).
- [Persistence mapping](../../src/AlterCourse.Core/Persistence/GamePersistence.cs), [Pathfinder definition](../../src/AlterCourse.Godot/content/ships/pathfinder.json), and [ship-definition V4 schema](../../src/AlterCourse.Godot/content/schemas/ship-definition-v4.schema.json).
- [Core gameplay tests](../../tests/AlterCourse.Core.Tests/Gameplay/), including milestone and negative regression scenarios.
- [v0.4.0 release](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.4.0), [M3A feature #58](https://github.com/L3DigitalNet/star-trek-alter-course/issues/58), and [M4 feature #62](https://github.com/L3DigitalNet/star-trek-alter-course/issues/62).
- [M3A PR #61](https://github.com/L3DigitalNet/star-trek-alter-course/pull/61) and [M4 PR #63](https://github.com/L3DigitalNet/star-trek-alter-course/pull/63): admission/test/manual evidence, not a fresh rerun in this wiki review.
- [Strategic Contact Reporting documentation task #74](https://github.com/L3DigitalNet/star-trek-alter-course/issues/74): approval/provenance for the slice. [Strategic Contact Reporting feature #77](https://github.com/L3DigitalNet/star-trek-alter-course/issues/77) and [Final PR #78](https://github.com/L3DigitalNet/star-trek-alter-course/pull/78): implementation evidence.
- [Faction policy](../../src/AlterCourse.Core/AI/FactionAssignmentPolicy.cs), [runtime wakes](../../src/AlterCourse.Core/Gameplay/GameSimulation.Factions.cs), [strict faction content](../../src/AlterCourse.Core/Content/FactionDefinitionCatalogLoader.cs), and [V7 persistence](../../src/AlterCourse.Core/Persistence/GamePersistence.cs): Feature #86 / Final PR #87 source evidence, merged into `dev` as `0217296`.
- [Faction policy tests](../../tests/AlterCourse.Core.Tests/AI/FactionAssignmentPolicyTests.cs), [scheduler target tests](../../tests/AlterCourse.Core.Tests/Simulation/FactionSchedulerTargetTests.cs), [runtime tests](../../tests/AlterCourse.Core.Tests/Gameplay/FactionRuntimeWakeTests.cs), and [V7 persistence tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV7FactionTests.cs): inherited executed Core evidence from PR #87; not rerun in this review.
- [Assignment scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/FactionAssignmentScenarioTests.cs), [knowledge boundary](../../tests/AlterCourse.Core.Tests/Gameplay/FactionKnowledgeBoundaryTests.cs), and [long horizon](../../tests/AlterCourse.Core.Tests/Gameplay/FactionLongHorizonTests.cs): targeted production, continuation, forbidden-knowledge, dormancy, and finite-hold evidence at `407393d`.
- Godot compatibility at `eeec0ce`: focused `GameplayShellTests` 65/65, warning-free build, private catalog loading, V7 quick-load continuation, malformed faction-data rejection, and player-safe faction hiding.

## Latest design-approval evidence

[Documentation PR #84](https://github.com/L3DigitalNet/star-trek-alter-course/pull/84) records the owner's September 6 approval of the first faction slice and all six pre-implementation recommendations, along with documentation consistency and hosted-check evidence. It is not a gameplay implementation PR, a new release, a V7 runtime change, or proof of M5 completion.

## Visual references

[Travel reference](../ui/reference/command-deck-travel.png), [Combat reference](../ui/reference/command-deck-combat.png), and [Engineering reference](../ui/reference/engineering-workspace.png) remain presentation references. Their owning [UI decision](../design/command-deck-ui.md) records the Figma source. The [runtime Theme](../../src/AlterCourse.Godot/assets/ui/command_theme.tres) is the implementation's styling authority. Images are not assertions that depicted combat or advanced Engineering systems exist.

## Operational knowledge and history

- [STATUS](../STATUS.md) and [TODO](../TODO.md): current operational snapshot and future work, not replacement design specifications.
- [Handoff state](../handoff/state.md), [architecture](../handoff/architecture.md), [conventions](../handoff/conventions.md), [spec/plan pointers](../handoff/specs-plans.md), and [deployment](../handoff/deployed.md): compact repository operations knowledge.
- [Credential reference location](../handoff/credentials.md): reference-only operational record; do not copy secret values into the wiki.
- [Bug/gotcha index](../handoff/bugs/INDEX.md) and [session records](../handoff/sessions/): durable operational lessons and history.
- [Project instruction/reference material](../llm-resources/): agent-facing context, subordinate to active architectural decisions.
- [Archived initial brainstorming transcript](../design/initial-brainstorm-session.md): original owner brief and exploratory alternatives. Early Python/TUI direction and an assistant-proposed 2378 epoch must not be treated as current engine/campaign decisions.

## Maintaining coverage

When a new ADR, supporting specification, or system family is admitted, record the design on its wiki topic page first, then link the supporting source here. Keep detailed formulas, schemas, and operating procedures in their supporting documents. Preserve historical records with clear scope rather than silently rewriting them as present-day implementation truth.
