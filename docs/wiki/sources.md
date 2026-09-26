---
schema_version: '1.1'
id: 'index-5yghuo-sources'
title: 'Design Source Catalog'
description: 'Coverage index of architecture, game contracts, implementation evidence, documentation reviews, and historical provenance.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-26'
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

This catalog indexes maintained game contracts, implementation evidence, dated review coverage, and historical provenance. It is not another specification or a replacement for PR verification records. The wiki owns game design; active ADRs own architectural decisions; current source/tests establish actual implementation behavior; and historical records explain origin. Future prose, examples, package candidates, and archived assistant recommendations are not evidence of implementation or owner approval.

The [documentation authority map](../README.md) defines the distinct roles of retained documents outside the wiki. Historical review details remain in the linked PRs and fixed-revision history rather than being repeated here as an accumulating session log. No review in this catalog claims that every external reference, provider, or third-party tool was reverified.

## Review record

### Post-M6A documentation audit — 2026-09-26

- **Source:** gameplay and handoff baseline `dc3a7cebd6b765b7a16d573f76bf4414fabb00b3`, containing M6A merge `c2edae1f6357dbe8cd60aa5040646e48b9c23aee`; topic base `7efb1629e8228a2d52c7b9e5c96142a21278bdea` additionally includes the separately landed SDK correction. The SDK change does not alter the audited gameplay contracts.
- **Scope:** cross-page status, consistency, historical/current boundaries, completeness of M6A architecture and recovery descriptions, decision provenance, and source/test navigation. The audit compared the scheduler, combat transition, Engineering/repair command boundary, defensive response, production bootstrap, persistence claims, and PR #112/#113 admission evidence with their owning wiki pages and root summaries. AssetCtl was reviewed as a separately scoped tool reference, not as an exhaustive provider implementation audit.
- **Findings and disposition:** corrected the current scheduler ceiling from historical V8's 68,864 to V9's 69,120; added missing combat/faction state ownership to the architecture summary; qualified the first faction slice's V4/V7 history; corrected stale claims that direct faction reporting and Q-10 first-combat refinement were still wholly future work; dated D-19 separately from the September 7 sequence; consolidated duplicate Engineering formulas and proof values; clarified shield-condition recovery, NPC-only defensive stimuli, and defensive Hold's no-new-course behavior; and indexed final M6A verification without presenting inherited results as new execution.
- **Limits:** this is documentation correction, not a runtime patch, new gameplay approval, release, fresh manual playtest, or independent re-execution of the feature's local tests. [PR #117](https://github.com/L3DigitalNet/star-trek-alter-course/pull/117) owns the actual audit diff, connected-session capability disclosure, and check results. The separate SDK handoff and dirty shared-wiki checkout remain outside the audit.
- **Cadence:** targeted post-M6A reconciliation does not reset the full baseline review below. The next full review remains **2026-10-03**.

### Full baseline semantic reconciliation — 2026-09-26

This full review preceded M6A behavior work. Its source was `42e760b02be12591e43416bc8c02bbe585ca6632`, after source-only v0.6.2 at `255eaedc8e27b483b0fd4e2fe0bccf050486b3bf`. The then-current gameplay baseline was v0.6.0/V8. The review inspected source and tests but did not itself execute gameplay or persistence tests. Its complete scope and admission are preserved in [Feature #111 / PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112).

It covered every indexed wiki topic, root README/roadmap, current STATUS/TODO, development operational prose including #109/#110 language-server records, and all active ADRs. It corrected the then-current V8 scheduler bound, release/synchronization language, implemented-response tense, historical V7 persistence evidence, AssetCtl's unimplemented live provider probe, and the caller-owned OpenBao resolution boundary. It found no active-ADR conflict. Its V8 values are historical baseline evidence, not current V9 limits.

| Indexed topic | Baseline source/test evidence and disposition |
| --- | --- |
| [Wiki home](README.md) | Release/tag history and linked topic contracts; source release distinguished from gameplay-changing release. |
| [Architecture](architecture.md) | Core/Godot references and ADRs 0001–0013; no authority-boundary contradiction. |
| [Asset pipeline tool](asset-pipeline-tool.md) | CLI dispatch and CLI tests; live `doctor --probe` remained unimplemented and rejected with exit 2. |
| [Content and persistence](content-assets-and-persistence.md) | Persistence mapping and V8 bounds tests; current V8 versus historical V7 measurements distinguished. |
| [Decision register](decision-register.md) | Faction state, response scenarios, and admission history; no decision changed. |
| [Development and governance](development-and-governance.md) | Review procedure, ADR catalog, and quality gate; overdue sweep and next due date recorded. |
| [Diplomacy, economy, and campaigns](diplomacy-economy-and-campaigns.md) | Faction/response sources; implemented direct reporting distinguished from future political systems. |
| [Engineering and combat](engineering-and-combat.md) | Engineering tests; at this pre-M6A baseline combat was still future work. |
| [Faction assignment](faction-intent-and-autonomous-assignment.md) | Assignment scenarios and persistence; historical V7 proof distinguished from V8 continuation. |
| [Factions and organizations](factions-and-organizations.md) | Faction state and both bounded scenarios; two implemented consumers, broader political model future. |
| [Implementation status](implementation-status.md) | Release history, response scenarios, and bounds; second bounded M5 contribution marked implemented. |
| [Interface](interface-and-player-commands.md) | GameScreen and shell tests; no discrepancy at the pre-M6A baseline. |
| [Milestone proofs](milestone-proofs.md) | Release and response evidence; M3/M5 partial, M6 then future. |
| [Observation response](observation-driven-faction-response.md) | Response scenarios, faction state, and V8 bounds; no discrepancy. |
| [Open questions](open-questions.md) | Response implementation and decisions; implemented response tense corrected. |
| [Sensors and AI](sensors-knowledge-and-ai.md) | Sensor knowledge and observation tests; no discrepancy. |
| [Strategic reporting](strategic-contact-reporting.md) | Persistence chain and report tests; historical sequence distinguished from later consumers. |
| [Vision](vision-and-scope.md) | Implementation status and decisions; no discrepancy. |
| [World and time](world-navigation-and-time.md) | Scheduler and scheduler tests; V8 ceiling corrected from 66,816 to 68,864. |
| [Source catalog](sources.md) | Indexed topic review and ADR catalog; replaced the overdue full-sweep deadline. |

The next full review is **2026-10-03**. Later targeted work does not move that date.

### M6A targeted behavior reconciliation — 2026-09-26

[PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112) is the final admission record, superseding intermediate feature checkpoints as the current evidence summary. Final source `5403a3d670808a6c105b53299e7d90fa560314e7` passed canonical verification with **789 Core, 324 AssetCtl, and 76 total Godot tests (73 gameplay, one integration, two asset import)**, smoke, and warning-free Release/Debug builds. Candidate `42fba73206de4f87e0a32e9b1c292c198296c5dc` added only validated documentation and passed all five hosted checks, including [run 36250509274](https://github.com/L3DigitalNet/star-trek-alter-course/actions/runs/36250509274). The feature merged as `c2edae1`; [post-merge run 36250769003](https://github.com/L3DigitalNet/star-trek-alter-course/actions/runs/36250769003) passed. [PR #113](https://github.com/L3DigitalNet/star-trek-alter-course/pull/113) records the handoff closeout.

The RQ023M6A correction requires a Current contact's observer and target to be at the same location, with any present observation frame matching it. Null legacy frames remain null. Stale/Lost historical frames have no present-location constraint. Six rejection cases failed before the correction; the focused regression filter passed 124 tests and final Core passed 789. Peer reviews were static inspections, not test execution; the exhausted initial peer attempt is not counted. Manual device testing is not claimed.

M6A has V5 content, V9 saves, bounded correlated defensive continuation, and a 512-attack horizon with 512 accepted return fires. The final supported-shape bound is 113,292,140 bytes within 128 MiB. This evidence establishes the implemented bounded slice, not full M6 or M3/M5 completion. Detailed tests are indexed below; intermediate 779-test checkpoints remain historical in the PR rather than competing with final-head evidence.

### Historical targeted release reconciliation — 2026-09-08

[PR #99](https://github.com/L3DigitalNet/star-trek-alter-course/pull/99) reconciled published v0.6.0/V8 after the signed release merge `d00460ea8b472c44ea2a8343d43e676efb96000b` and synchronization PR #98. It preserved historical V6/V7 evidence, M3/M5 incompleteness, and the then-future M6 sequence. The release gate reported 598 Core, 324 AssetCtl, and 67 gameplay + one integration + two import tests. This was a targeted review, not a full sweep.

### Historical targeted review — 2026-09-07

[PR #89](https://github.com/L3DigitalNet/star-trek-alter-course/pull/89) reviewed `685a605` and Feature #86 merge `0217296`, including six-ship production versus four-ship compatibility bootstrap, assignment, player knowledge, and persistence. At that time `dev` used V7; v0.5.0 was the V6 release. It corrected landing summaries without declaring M3/M5 complete.

### Design-admission reconciliation — 2026-09-07

[Task #91 / PR #92](https://github.com/L3DigitalNet/star-trek-alter-course/pull/92) regularized the observation-response design admission against `89c6b49`. Invalid staging PR #90 was closed unmerged. The accepted design clarified freshness, suppression, shared faction coordination, rejection, proof bounds, and the future M6 direction. Its 505 Core / 324 AssetCtl / 68 total Godot gate verified the unchanged baseline, not future observation response or combat behavior.

### Implementation admission and boundary review — 2026-09-07

[Feature #93 / PR #94](https://github.com/L3DigitalNet/star-trek-alter-course/pull/94) implemented the admitted observation-response contract after design merge `ad73862`. Its scenario, player-safety, and bounded persistence evidence belongs to that feature and the subsequent v0.6.0 release. It did not resolve exact first-combat mechanics; D-19 and Feature #111 later did so.

### Structure, depth, and consolidation review — 2026-09-07

[PR #89](https://github.com/L3DigitalNet/star-trek-alter-course/pull/89) consolidated the documents reviewed at `0aeb526` and implementation `685a605`. It retained detailed Engineering, observation, repair-correlation, AI, persistence, focus, and milestone contracts inside the wiki instead of replacing them with short summaries. The consolidation map below preserves destinations; fixed-revision originals remain linked for provenance. This structural/targeted review did not reset the then-current full-sweep deadline.

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
- [0014 — Extensible bounded ship-system substrate](../adr/0014-use-an-extensible-bounded-ship-system-substrate.md): separates system kind, reusable system definition, and installed system instance; requires heterogeneous runtime loadouts and shared condition/damage/repair/power/persistence mechanics without a universal component framework.

## Canonical wiki decisions and future design

[Strategic Contact Reporting](strategic-contact-reporting.md) owns the original ship/player last-known information contract. [Faction assignment](faction-intent-and-autonomous-assignment.md) owns D-08–D-13's own-asset assignment. [Observation response](observation-driven-faction-response.md) owns D-14–D-17's delayed direct reporting and investigation. [Engineering and combat](engineering-and-combat.md) owns D-19's implemented M6A mechanics. [Milestone proofs](milestone-proofs.md) and the root roadmap distinguish partial milestones and future sequence.

[Factions and organizations](factions-and-organizations.md) owns the broader approved political principles, not a claim that hierarchy, organizations, governments, or relationships are implemented. [Open questions](open-questions.md) keeps Q-02/Q-03 and remaining intelligence, political, campaign, and compatibility decisions distinct from resolved Q-01, Q-05, and bounded M6A Q-10. Combat-driven Engineering is a direction, not approval of an unspecified next feature.

## Consolidation map

| Former source | Canonical destination and disposition |
| --- | --- |
| `docs/design/command-deck-ui.md` | [Interface](interface-and-player-commands.md): composition, Theme, previews, focus, and visual provenance; original deleted. |
| `docs/design/engineering-backbone.md` | [Engineering](engineering-and-combat.md): formulas, units, presets, repair/correlation, capability and proof values; [persistence](content-assets-and-persistence.md) owns migrations; original deleted. |
| `docs/design/first-observed-contact.md` | [Sensors](sensors-knowledge-and-ai.md): geometry, lifecycle, scan/hail, cautious policy and timing; persistence retains historical migration; original deleted. |
| `docs/design/branch-release-governance.md` | [Governance](development-and-governance.md) and ADR 0013 retain decisions, alternatives, and risk; original deleted, fixed history below. |
| `docs/design/initial-brainstorm-session.md` | Vision, future systems, decisions, and open questions preserve owner intent separately from unapproved proposals; transcript retained in fixed history only. |
| `docs/llm-resources/project-instructions-chatgpt.md` | Duplicate removed; agent entry points route directly to wiki/ADRs. |
| `docs/specs/asset-pipeline-tool.md` | [Asset pipeline tool](asset-pipeline-tool.md): full contract and document identity retained inside wiki; former path removed. |
| Root README gameplay/save detail | Owning interface, Engineering, world, and persistence pages; README retains onboarding. |
| Root roadmap design/proof detail | [Milestone proofs](milestone-proofs.md); roadmap retains sequence/status. |

The consolidation preserves substantive contracts. Historical save versions remain labeled historical; AssetCtl's reference requirements are not proof that every provider or future phase has shipped.

## Dependency and development references

[AssetCtl dependency admission](../dependency-admission/assetctl.md) · [JsonSchema.Net Core admission](../dependency-admission/jsonschema-net-core.md) · [Development quality](../development-quality.md) · [Agent setup](../development-agent-skills.md) · [Contributing](../../CONTRIBUTING.md) · [AGENTS](../../AGENTS.md)

Exact selections belong in [global.json](../../global.json), [Directory.Build.props](../../Directory.Build.props), [Directory.Packages.props](../../Directory.Packages.props), and the [canonical verifier](../../scripts/verify.sh). [Asset configuration/catalog](../../config/assets/) and [AssetCtl](../../tools/AlterCourse.AssetCtl/) are separate tooling sources. [License policy](../../LICENSE.md) and [legal notice](../../LEGAL.md) define repository boundaries; this catalog asserts no new legal clearance.

## Implementation evidence

| Family | Source and test evidence |
| --- | --- |
| World and bootstrap | [FirstGameSetup](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [GameBootstrap](../../src/AlterCourse.Core/Gameplay/GameBootstrap.cs), [ShipState](../../src/AlterCourse.Core/Ships/ShipState.cs), [SimulationState](../../src/AlterCourse.Core/Gameplay/SimulationState.cs), [orders](../../tests/AlterCourse.Core.Tests/Gameplay/OrderExecutionTests.cs). |
| Time and work | [ScheduledWork](../../src/AlterCourse.Core/Simulation/ScheduledWork.cs), [work kinds](../../src/AlterCourse.Core/Simulation/ScheduledWorkKind.cs), [scheduler](../../src/AlterCourse.Core/Simulation/SimulationScheduler.cs), [scheduler tests](../../tests/AlterCourse.Core.Tests/Simulation/SimulationSchedulerTests.cs). |
| Local observation and knowledge | [SensorKnowledge](../../src/AlterCourse.Core/Sensors/SensorKnowledge.cs), [cautious policy](../../src/AlterCourse.Core/AI/CautiousContactDecisionPolicy.cs), [hail tests](../../tests/AlterCourse.Core.Tests/Gameplay/HailAndContactDecisionTests.cs), [contact validation](../../tests/AlterCourse.Core.Tests/Gameplay/SensorKnowledgeValidationTests.cs), [strategic reports](../../tests/AlterCourse.Core.Tests/Gameplay/StrategicContactReportTests.cs). |
| Engineering and repair | [Engineering state](../../src/AlterCourse.Core/Ships/ShipEngineeringState.cs), [repair state](../../src/AlterCourse.Core/Ships/SystemRepairState.cs), [commands](../../src/AlterCourse.Core/Gameplay/GameSimulation.cs), [Engineering tests](../../tests/AlterCourse.Core.Tests/Ships/EngineeringBackboneTests.cs). |
| Faction assignment | [Policy](../../src/AlterCourse.Core/AI/FactionAssignmentPolicy.cs), [runtime](../../src/AlterCourse.Core/Gameplay/GameSimulation.Factions.cs), [policy tests](../../tests/AlterCourse.Core.Tests/AI/FactionAssignmentPolicyTests.cs), [typed targets](../../tests/AlterCourse.Core.Tests/Simulation/FactionSchedulerTargetTests.cs), [wake tests](../../tests/AlterCourse.Core.Tests/Gameplay/FactionRuntimeWakeTests.cs). |
| Faction proof and knowledge limits | [Assignment scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/FactionAssignmentScenarioTests.cs), [knowledge boundaries](../../tests/AlterCourse.Core.Tests/Gameplay/FactionKnowledgeBoundaryTests.cs), [long horizon](../../tests/AlterCourse.Core.Tests/Gameplay/FactionLongHorizonTests.cs), [V7 tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV7FactionTests.cs). |
| Observation response | [Runtime](../../src/AlterCourse.Core/Gameplay/GameSimulation.Observation.cs), [bounded state](../../src/AlterCourse.Core/Factions/FactionObservationState.cs), [production scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/ObservationResponseScenarioTests.cs), [proof fixture](../../tests/AlterCourse.Core.Tests/Gameplay/ObservationResponseProofFixture.cs), [active horizon](../../tests/AlterCourse.Core.Tests/Gameplay/ObservationResponseLongHorizonTests.cs), [V8 tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV8ObservationTests.cs). |
| M6A combat and defense | [Combat transition](../../src/AlterCourse.Core/Gameplay/GameSimulation.Combat.cs), [defensive policy](../../src/AlterCourse.Core/AI/DefensiveCombatDecisionPolicy.cs), [runtime tests](../../tests/AlterCourse.Core.Tests/Gameplay/CombatRuntimeTests.cs), [policy tests](../../tests/AlterCourse.Core.Tests/AI/DefensiveCombatDecisionPolicyTests.cs). |
| M6A integrated continuation | [Combat scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/M6CombatScenarioTests.cs), [combat long horizon](../../tests/AlterCourse.Core.Tests/Gameplay/M6CombatLongHorizonTests.cs), [V9 persistence](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV9CombatTests.cs). |
| Content and saves | [Persistence mapping](../../src/AlterCourse.Core/Persistence/GamePersistence.cs), [Pathfinder V5](../../src/AlterCourse.Godot/content/ships/pathfinder.json), [V5 schema](../../src/AlterCourse.Godot/content/schemas/ship-definition-v5.schema.json), [faction loader](../../src/AlterCourse.Core/Content/FactionDefinitionCatalogLoader.cs), [maximum-shape proof](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV4SensorTests.cs). |
| Godot presentation | [GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs), [Engineering workspace](../../src/AlterCourse.Godot/src/Gameplay/EngineeringWorkspace.cs), [shell tests](../../src/AlterCourse.Godot/tests/GameplayShellTest.gd); input/focus, actor-safe Combat, V9 continuation, and hidden faction-state boundaries. |

Current V9 proof values are 88,137,170 bytes for the faction fixture, 108,935,516 bytes for the empty-combat report vertex, and 113,292,140 bytes for the conservative supported-shape bound within 128 MiB. Populated-stimulus tests separately cover 255 simultaneous correlated NPC stimuli. A measured fixture is not by itself a universal maximum, and these figures are not promises for future schemas. [Content and persistence](content-assets-and-persistence.md) retains historical V6–V8 measurements and migration details.

[Implementation status](implementation-status.md#verification-evidence-not-a-fresh-execution-claim) distinguishes inherited final feature/release results from later documentation checks. Tests indexed here are evidence sources, not a claim that every one was executed by this audit.

## Latest design-approval evidence

[PR #84](https://github.com/L3DigitalNet/star-trek-alter-course/pull/84) records the September 6 first faction-slice approval. [Task #91 / PR #92](https://github.com/L3DigitalNet/star-trek-alter-course/pull/92) records the September 7 observation-response approval and subsequent M6/Engineering sequence, superseding invalid staging PR #90. These are design-admission records, not their later runtime implementations.

[Feature #111](https://github.com/L3DigitalNet/star-trek-alter-course/issues/111) and [Final PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112) record the September 26 M6A refinement and implementation represented by D-19. The post-M6A audit corrects documentation; it does not approve a detailed next Engineering feature or declare any incomplete milestone finished.

## Visual references

[Travel reference](../ui/reference/command-deck-travel.png), [Combat reference](../ui/reference/command-deck-combat.png), and [Engineering reference](../ui/reference/engineering-workspace.png) remain presentation references. [Interface](interface-and-player-commands.md) owns their Figma provenance. The [runtime Theme](../../src/AlterCourse.Godot/assets/ui/command_theme.tres) owns implementation styling. Images do not establish that every depicted combat or advanced Engineering feature exists.

## Operational knowledge and history

[STATUS](../STATUS.md) and [TODO](../TODO.md) own operational work, not replacement design. [Handoff state](../handoff/state.md), [architecture](../handoff/architecture.md), [conventions](../handoff/conventions.md), [spec/plan pointers](../handoff/specs-plans.md), and [deployment](../handoff/deployed.md) retain compact operational context. [Credential references](../handoff/credentials.md) remain reference-only; do not copy secret values. [Bugs](../handoff/bugs/INDEX.md) and [sessions](../handoff/sessions/) retain operational lessons and history.

## Historical provenance

Deleted originals remain at fixed revision `685a60577a671e4f39828608ae148263697a9013`. They preserve provenance only; maintained wiki contracts and active ADRs govern current design.

- [Owner brief and brainstorming transcript](https://github.com/L3DigitalNet/star-trek-alter-course/blob/685a60577a671e4f39828608ae148263697a9013/docs/design/initial-brainstorm-session.md): owner intent is consolidated; assistant suggestions, including a 2378 epoch, were not approvals.
- [Branch/release discovery](https://github.com/L3DigitalNet/star-trek-alter-course/blob/685a60577a671e4f39828608ae148263697a9013/docs/design/branch-release-governance.md): rationale adopted by ADR 0013, not another current workflow.
- [Original design directory](https://github.com/L3DigitalNet/star-trek-alter-course/tree/685a60577a671e4f39828608ae148263697a9013/docs/design): earlier UI, Engineering, contact, and historical save-version contracts.
- [Former ChatGPT summary](https://github.com/L3DigitalNet/star-trek-alter-course/blob/685a60577a671e4f39828608ae148263697a9013/docs/llm-resources/project-instructions-chatgpt.md): duplicated guidance retained only for provenance.

## Maintaining coverage

Record design on its owning wiki page, architecture through ADRs, implementation assertions against source/tests, and actual execution in the governing PR. Link new system families here without duplicating their formulas or adding an unbounded review log. Preserve important history through fixed revisions and PR records. A targeted audit or format/link check does not reset the full-sweep date or prove semantic correctness by itself.
