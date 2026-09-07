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
  - 'docs/wiki/observation-driven-faction-response.md'
  - 'docs/wiki/implementation-status.md'
---

# Design source catalog

[Wiki home](README.md) · [Decision register](decision-register.md) · [Open questions](open-questions.md)

## Coverage and authority

This catalog inventories the wiki's game contracts, implementation evidence, and historical provenance. The former external game-design documents have been consolidated into their owning wiki pages. The wiki does not claim to have externally reverified every historical citation or every development-tool provider.

The wiki is the single source of truth for game design, including detailed rules and milestone proofs. Active ADRs record architectural decisions. Historical records explain origin, and current code/tests establish actual implementation behavior. A source's future tense, example, package candidate, or archived assistant recommendation is not evidence of an implemented feature or owner approval. [The documentation authority map](../README.md) defines the distinct roles of documents retained outside the wiki.

## Review record

Targeted review, 2026-09-07:

- **Source:** `685a605`; merged Feature #86 / Final PR #87 commit `0217296`.
- **Scope:** faction landing claims and summaries in the root [README](../../README.md), [roadmap](../../ROADMAP.md), and this wiki's implementation, architecture, faction, knowledge, navigation, persistence, decision, and campaign pages.
- **Evidence:** static inspection of the [faction policy](../../src/AlterCourse.Core/AI/FactionAssignmentPolicy.cs), [runtime wakes](../../src/AlterCourse.Core/Gameplay/GameSimulation.Factions.cs), [V7 persistence](../../src/AlterCourse.Core/Persistence/GamePersistence.cs), and [targeted tests](../../tests/AlterCourse.Core.Tests/Gameplay/FactionAssignmentScenarioTests.cs). [FirstGameSetup](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs) and [bootstrap tests](../../tests/AlterCourse.Core.Tests/Gameplay/FactionBootstrapTests.cs) distinguish the six-ship production and four-ship legacy worlds. The audit used inherited PR #87 test evidence; [PR #89](https://github.com/L3DigitalNet/star-trek-alter-course/pull/89) records the verification reruns.
- **Findings and disposition:** corrected stale pending-landing/review-branch claims, four-ship production summaries, and README V6/current-rules claims. Current `dev` is V7 under `faction-intent-autonomous-assignment-v1`; v0.5.0 remains the V6 release. M3 and M5 remain incomplete.
- **Full-review cadence:** the initial 2026-09-06 full-corpus review remains the [Wiki home](README.md) provenance; the next full review is due 2026-09-13. This targeted review does not reset that date; follow the [recurring design-reconciliation procedure](development-and-governance.md#recurring-design-reconciliation).

### Design-admission reconciliation — 2026-09-07

The staging review compared the selected next-slice design against `dev` `89c6b498f08058b2ffdf971ecd3175a5220a00c2`, the active ADR set, and the September 7 source-backed wiki baseline above. That review was a **design reconciliation, not a new runtime audit**: no implementation source changed and it claimed no fresh runtime test execution.

The owner approved [Observation-Driven Faction Response](observation-driven-faction-response.md) as the next bounded slice and M6 Tactical Combat Foundation as the next major development family after it. The reconciliation records D-14 through D-18, scopes only the required portions of Q-02/Q-04/Q-08/Q-10/Q-14, and leaves Q-03 plus exact Q-10 combat mechanics open. It explicitly distinguishes approved design from current V7 implementation and does not reset the full semantic-sweep deadline of 2026-09-13.

[Task #91](https://github.com/L3DigitalNet/star-trek-alter-course/issues/91) and [Final PR #92](https://github.com/L3DigitalNet/star-trek-alter-course/pull/92) regularize the staging work. Fresh reconciliation against `89c6b49` reviewed all active ADRs, the nine changed pages, related knowledge/faction/navigation/persistence/interface contracts, and their source/tests. Native and adversarial review clarified freshness, Q-10 scope, shared faction coordination, suppression lifetime, rejection behavior, and proof bounds while preserving the information boundary. Fresh canonical verification at `e9fe5f0` passed 505 Core tests, 324 AssetCtl tests, and 68 Godot integration/import/gameplay cases; this verifies the unchanged runtime baseline, not the future response behavior. PR #92 owns subsequent review and final-head check evidence.

### Structure, depth, and consolidation review — 2026-09-07

Reviewed the documentation at `0aeb5264b32ae7e420267b66f54ef2060942fe02`, the unchanged implementation at `685a605`, and the integrated consolidation in PR #89. The topic structure covered the implemented game families, but important details were split between short wiki summaries, old design documents, and root files. The resulting structure keeps stable topic pages, groups navigation by reader need, moves milestone proofs and the full AssetCtl reference inside the wiki, and leaves root documents as entry points.

The implementation audit identified depth gaps rather than a wholly absent implemented game system. Corrections include Engineering allocation ties and atomic rejection, exact repair/loss work correlation, contact geometry and cautious-policy outcomes, UI focus/disabled behavior, scheduler bounds and bootstrap order, and historical migration defaults. The following coverage records the inspected source families; it is not a claim that every line of code or development-tool provider was re-audited.

| Reviewed family | Owning contract | Implementation and behavioral evidence |
| --- | --- | --- |
| Bootstrap, orders, time and space | [World](world-navigation-and-time.md) | [Bootstrap](../../src/AlterCourse.Core/Gameplay/GameBootstrap.cs), [first world](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [scheduler](../../src/AlterCourse.Core/Simulation/SimulationScheduler.cs), [orders](../../tests/AlterCourse.Core.Tests/Gameplay/OrderExecutionTests.cs) |
| Local contacts, scan, hail and cautious AI | [Sensors](sensors-knowledge-and-ai.md) | [Knowledge](../../src/AlterCourse.Core/Sensors/SensorKnowledge.cs), [policy](../../src/AlterCourse.Core/AI/CautiousContactDecisionPolicy.cs), [hail/decision tests](../../tests/AlterCourse.Core.Tests/Gameplay/HailAndContactDecisionTests.cs) |
| Durable last-known reports | [Reporting](strategic-contact-reporting.md) | [Simulation/projection](../../src/AlterCourse.Core/Gameplay/GameSimulation.cs), [report tests](../../tests/AlterCourse.Core.Tests/Gameplay/) indexed by the owning page |
| Power, condition and repair | [Engineering](engineering-and-combat.md) | [Engineering state](../../src/AlterCourse.Core/Ships/ShipEngineeringState.cs), [repair](../../src/AlterCourse.Core/Ships/SystemRepairState.cs), [Engineering tests](../../tests/AlterCourse.Core.Tests/Ships/EngineeringBackboneTests.cs) |
| Faction assignment and offscreen consequences | [Faction assignment](faction-intent-and-autonomous-assignment.md) | [Policy](../../src/AlterCourse.Core/AI/FactionAssignmentPolicy.cs), [runtime](../../src/AlterCourse.Core/Gameplay/GameSimulation.Factions.cs), [scenario](../../tests/AlterCourse.Core.Tests/Gameplay/FactionAssignmentScenarioTests.cs), [long horizon](../../tests/AlterCourse.Core.Tests/Gameplay/FactionLongHorizonTests.cs) |
| Content, saves and migration | [Content/persistence](content-assets-and-persistence.md) | [Content loaders](../../src/AlterCourse.Core/Content/), [persistence](../../src/AlterCourse.Core/Persistence/GamePersistence.cs), [V7 validation tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV7FactionTests.cs) |
| Player interface and focus | [Interface](interface-and-player-commands.md) | [GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs), [Engineering workspace](../../src/AlterCourse.Godot/src/Gameplay/EngineeringWorkspace.cs), [shell tests](../../src/AlterCourse.Godot/tests/GameplayShellTest.gd) |
| Tool reference and future design | [AssetCtl](asset-pipeline-tool.md), [milestone proofs](milestone-proofs.md), [open questions](open-questions.md) | Full tool contract preserved; milestone/owner intent compared with former sources. Future acceptance and proposals were not treated as implemented gameplay. |

The [consolidation map](#consolidation-map) records content destinations before deletion. PR #89 records checks actually executed and review findings. This structural and targeted implementation review does not reset the full semantic-sweep deadline above.

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
- [Observation-Driven Faction Response](observation-driven-faction-response.md): owner-approved next bounded M5 slice, not implemented; D-14 through D-17 cover direct delayed reporting, bounded deterministic investigation, persistence, and information limits.
- [Engineering and combat](engineering-and-combat.md) and [Milestone proofs](milestone-proofs.md): D-18 records M6 first combat engagement as the next major family after the response slice, with combat-driven Engineering depth and Q-10 still gating exact combat rules.
- [Factions and organizations](factions-and-organizations.md): owner-approved conceptual political framework; its hierarchy, organization, government, and relationship runtime remains future work, while the first assignment consumer implements only the root-faction/direct-control subset.
- [Open questions](open-questions.md): Q-01 and first-slice Q-05 resolved; Q-02/Q-04/Q-06/Q-08/Q-14 scoped in part; Q-03 and remaining intelligence, political, exact combat, campaign, and compatibility questions deferred.

## Consolidation map

| Former source | Canonical destination and disposition |
| --- | --- |
| `docs/design/command-deck-ui.md` | [Interface](interface-and-player-commands.md): composition, Theme, preview boundary, focus/disabled behavior, Figma and image provenance; original deleted. |
| `docs/design/engineering-backbone.md` | [Engineering](engineering-and-combat.md): formulas, units, presets, repair/correlation, capability effects and proof values; [persistence](content-assets-and-persistence.md) owns historical migrations; original deleted. |
| `docs/design/first-observed-contact.md` | [Sensors](sensors-knowledge-and-ai.md): geometry, lifecycle, scan/hail, deterministic cautious policy and timing; [persistence](content-assets-and-persistence.md) retains migration boundaries; original deleted. |
| `docs/design/branch-release-governance.md` | [Governance](development-and-governance.md) retains decision summary; ADR 0013 already preserves the adopted decisions, alternatives and risk. Discovery original deleted, history pinned below. |
| `docs/design/initial-brainstorm-session.md` | [Vision](vision-and-scope.md), [future systems](diplomacy-economy-and-campaigns.md), [decisions](decision-register.md), and [open questions](open-questions.md) preserve owner intent and distinguish unapproved proposals; transcript deleted, history pinned below. |
| `docs/llm-resources/project-instructions-chatgpt.md` | Duplicated wiki vision/priorities and ADR routing; original deleted. Agent entry points now route directly to wiki contracts and ADRs. |
| `docs/specs/asset-pipeline-tool.md` | [Asset pipeline tool](asset-pipeline-tool.md): full contract and document identity retained inside the wiki; former path removed. Requirements and planned phases remain distinct from implemented capability. |
| Root README gameplay/save detail | [Interface](interface-and-player-commands.md), [Engineering](engineering-and-combat.md), [world](world-navigation-and-time.md), and [persistence](content-assets-and-persistence.md); README retains onboarding. |
| Root roadmap design/proof detail | [Milestone proofs](milestone-proofs.md); [roadmap](../../ROADMAP.md) retains sequence/status and links. |

This consolidation preserves substantive contracts rather than treating a shorter summary as equivalent. Historical save versions remain labeled historical. The AssetCtl reference is a development-tool contract, not another game-design authority or proof that all listed future phases have shipped.

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

[Documentation PR #92](https://github.com/L3DigitalNet/star-trek-alter-course/pull/92), governed by [Task #91](https://github.com/L3DigitalNet/star-trek-alter-course/issues/91), records the owner's September 7 approval of Observation-Driven Faction Response and the subsequent M6/combat-driven Engineering sequence. It supersedes [staging PR #90](https://github.com/L3DigitalNet/star-trek-alter-course/pull/90), which was closed unmerged without valid admission. This is documentation evidence only; it is not runtime implementation, V8 evidence, a combat feature, or a release.

## Visual references

[Travel reference](../ui/reference/command-deck-travel.png), [Combat reference](../ui/reference/command-deck-combat.png), and [Engineering reference](../ui/reference/engineering-workspace.png) remain presentation references. Their owning [interface page](interface-and-player-commands.md) records the Figma source. The [runtime Theme](../../src/AlterCourse.Godot/assets/ui/command_theme.tres) is the implementation's styling authority. Images are not assertions that depicted combat or advanced Engineering systems exist.

## Operational knowledge and history

- [STATUS](../STATUS.md) and [TODO](../TODO.md): current operational snapshot and future work, not replacement design specifications.
- [Handoff state](../handoff/state.md), [architecture](../handoff/architecture.md), [conventions](../handoff/conventions.md), [spec/plan pointers](../handoff/specs-plans.md), and [deployment](../handoff/deployed.md): compact repository operations knowledge.
- [Credential reference location](../handoff/credentials.md): reference-only operational record; do not copy secret values into the wiki.
- [Bug/gotcha index](../handoff/bugs/INDEX.md) and [session records](../handoff/sessions/): durable operational lessons and history.

## Historical provenance

The deleted originals remain available at fixed revision `685a60577a671e4f39828608ae148263697a9013`. These links preserve provenance only; the maintained wiki and active ADRs govern current design.

- [Original owner brief and brainstorming transcript](https://github.com/L3DigitalNet/star-trek-alter-course/blob/685a60577a671e4f39828608ae148263697a9013/docs/design/initial-brainstorm-session.md): owner intent is consolidated in the vision/future-system pages. Assistant alternatives, including a 2378 epoch, were not approvals.
- [Branch/release discovery](https://github.com/L3DigitalNet/star-trek-alter-course/blob/685a60577a671e4f39828608ae148263697a9013/docs/design/branch-release-governance.md): rationale adopted by ADR 0013, not a second current workflow contract.
- [Original gameplay design directory](https://github.com/L3DigitalNet/star-trek-alter-course/tree/685a60577a671e4f39828608ae148263697a9013/docs/design): earlier UI, Engineering and contact contracts, including their historical save-version scope.
- [Former ChatGPT instruction summary](https://github.com/L3DigitalNet/star-trek-alter-course/blob/685a60577a671e4f39828608ae148263697a9013/docs/llm-resources/project-instructions-chatgpt.md): duplicated guidance, preserved for provenance rather than maintained as an alternate brief.

## Maintaining coverage

When a new ADR or system family is admitted, record game design on its owning wiki page and link the evidence here. Keep game formulas, transitions, limits and acceptance contracts in the wiki; link executable schemas/tests instead of copying their full implementation. Operational procedures retain their distinct document owners. Preserve history with fixed-revision provenance rather than maintaining superseded design files in parallel.
