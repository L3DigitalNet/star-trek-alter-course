---
schema_version: '1.1'
id: 'index-5yghuo-sources'
title: 'Design Source Catalog'
description: 'Coverage index of architecture, game contracts, implementation evidence, documentation reviews, and historical provenance.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-27'
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
  - 'docs/wiki/ship-system-substrate.md'
---

# Design source catalog

[Wiki home](README.md) · [Decision register](decision-register.md) · [Open questions](open-questions.md)

## Coverage and authority

This catalog indexes maintained game contracts, implementation evidence, dated review coverage, and historical provenance. The wiki owns game design; active ADRs own architectural decisions; source/tests establish implementation; historical records explain origin. Future prose, package candidates, and archived assistant recommendations are not implementation or approval evidence.

The [documentation authority map](../README.md) defines distinct roles outside the wiki. PRs and fixed-revision history retain detailed review records rather than creating an unbounded session log here. No review below claims every external reference, provider, or third-party tool was reverified.

## Review record

### ADR reconciliation and implementation conformance — 2026-09-27

Reviewed all fourteen active ADRs in full from `origin/dev` baseline `2ef6c77aa4587719aac0b59cfda3d6ee7c560e5a`, with scoped implementation traces and independent native/Claude reviews. The [191-obligation register](../reviews/adr-conformance-2026-09-27.md) retains baseline mismatches, amendments, per-obligation applicability, named tests, exact candidate receipts and unresolved limits. [Initiative #127](https://github.com/L3DigitalNet/star-trek-alter-course/issues/127) and [Supporting PR #132](https://github.com/L3DigitalNet/star-trek-alter-course/pull/132) govern the implementation.

Reconciled architecture, development/governance, content/persistence, engineering/combat, world/time, dependency admissions and project-owned architecture guidance. Corrections cover structured game diagnostics, Standalone admission, selected architecture/property tooling, typed physical and bounded-ratio boundaries, conditional no-RNG obligations, strict admission and failure reporting. The owner approved the bounded existing-quantity exception in ADR 0011; no historical UnitsNet rejection is invented. Ship V6/system V1/save V10 remain current and unreleased. This is targeted architectural reconciliation, not a new full gameplay-wiki sweep; the full-sweep due date remains **2026-10-03**. Source/test evidence and actual review limitations belong to the register; no fresh manual device playtest is claimed here.

The final documentation reconciliation reviewed head `5a2712c` and actual PR #132 squash `9b4f6a7d6182c0c4dd281dc1eba15cff5f10a610`; their trees match. The [integration addendum](../reviews/adr-conformance-2026-09-27.md#integration-addendum--2026-09-27) records fresh candidate and actual post-merge verification separately. Wiki home, architecture, implementation status, content/persistence, README and roadmap retain accurate development/release boundaries. This landing check adds no gameplay decision and does not reset the full sweep.

### Post-merge substrate documentation correction — 2026-09-27

Reviewed `dev` at `a090c33` (runtime merge `17637dd`) against `ShipEngineeringState`, installed-system identities, `GamePersistence`/V10 mapping, Godot catalog loading, and faction persistence, V10 compatibility, and heterogeneous-continuation tests. Corrected stale next-implementation and pre-merge claims in the roadmap, wiki home, architecture, faction-assignment, observation-response, and strategic-reporting pages, this catalog, and STATUS. Current development uses ship content V6, system-definition V1, and saves V10; Issue #121 and PRs #123/#124 are complete. Historical admission reviews below remain historical. This targeted source review does not claim a fresh gameplay test execution or reset the next full sweep due **2026-10-03**.

### Ship-system substrate implementation review — 2026-09-27

- **Baseline:** `ac9d5ac9bf173375aa1aeb2e4d65cb1b820b4063` on `feature/121-extensible-ship-system-substrate` (branched from the PR #122 admission baseline `a86206d`; supersedes this review's earlier pass at `c9c7ed6`), containing implemented runtime, content, persistence, and Godot generic Engineering presentation for the installed-system substrate. Integrated proof tests (architecture conformance, extension demo, heterogeneous continuation, long horizon) are being written by a later leg (L6) and are not yet part of this baseline.
- **Scope:** compared [Ship-system substrate](ship-system-substrate.md), [Engineering and combat](engineering-and-combat.md), [Content, assets, and persistence](content-assets-and-persistence.md), [Interface and player commands](interface-and-player-commands.md), [Sensors, knowledge, and AI](sensors-knowledge-and-ai.md), [World, navigation, and time](world-navigation-and-time.md), [Implementation status](implementation-status.md), [Milestone proofs](milestone-proofs.md), [Open questions](open-questions.md), this catalog, both wiki/root summaries, and both project-owned architecture-router copies against `src/AlterCourse.Core/Ships/`, `src/AlterCourse.Core/Player/`, `src/AlterCourse.Core/Persistence/GamePersistence.V10.cs` and `SaveModelsV10.cs`, `src/AlterCourse.Godot/content/{schemas,systems,ships}/`, `src/AlterCourse.Godot/src/Gameplay/{EngineeringWorkspace,CommandInterfaceAction,OwnShipActionBinding,EngineeringKindPresentation,GameScreen}.cs`, and the named test files below.
- **Corrections:** flipped CURRENT-DEV claims naming V5 ship content/V9 saves, fixed four-consumer/five-condition Engineering state, and array-position Balanced/priority ordering to the implemented installed-system representation (ship content V6, system-definition content V1, save schema V10 under `installed-ship-system-substrate-v1`, common-order-then-installed-ID tie-break); replaced SELECTED-WORK framing on the contract page and its cross-references with implemented status now that runtime, persistence, and Godot presentation evidence all exist; converted "in progress"/adapter-bridge language for the generic Engineering rows, `balance`/`prioritize:<id>`/`repair:<id>`/`return-command`/`system:<id>` action keys, `OwnShipActionBinding` owner/generation refusal, `EngineeringKindPresentation` base-label preservation, and absent-installation "UNAVAILABLE" panels to implemented facts, verified against the merged L5 code; confirmed removal of `EngineeringAction`, `PowerAllocationPreset`, and `GameSimulation.EngineeringAdapter.cs`.
- **Routing:** confirmed the two project-owned architecture-router copies are `.claude/skills/stac-architecture/SKILL.md` and `.codex/skills/stac-architecture/SKILL.md` (there is no `.agents/` copy) and aligned both to implemented-status framing. Preserved recovery/Damage Control, refit gameplay, aggregation, and additional systems as deferred/selected-later work per [open questions](open-questions.md).
- **Evidence and limits:** Final PR #123 owns the actual diff and final check results for the full leg sequence; this is documentation/design reconciliation against the runtime, persistence, and Godot presentation legs (L1–L5), verified against `ac9d5ac9bf173375aa1aeb2e4d65cb1b820b4063`, not a fresh manual playtest or an independent peer review. Integrated conformance tests (architecture, extension demo, heterogeneous continuation, long horizon) are still being written by leg L6 and are referred to here only generically, pending their names from the orchestrator.
- **Final-head reconciliation:** at `0403377994e47387ce7fdddf84273974219f0108` the L6 proof tests (`SubstrateConformanceTests`, `SubstrateExtensionTests`, `HeterogeneousLoadoutContinuationTests`, `M6CombatLongHorizonTests`) are integrated; pending-test wording in [Implementation status](implementation-status.md) and [Ship-system substrate](ship-system-substrate.md), a mistargeted catalog link in [Content, assets, and persistence](content-assets-and-persistence.md), and the handoff summaries were corrected against them. The baseline and limits bullets above describe the earlier `ac9d5ac` pass. PR #123 then merged into `dev` as `17637dd` with an identical tree.
- **Cadence:** targeted review only. The next full semantic sweep remains **2026-10-03**.

### Ship-system substrate admission review — 2026-09-26

- **Baseline:** `e16e1a83e064e35c1fd7bb7a43f9e595b4d36d81`, containing ADR 0014 from PR #120 but no runtime substrate migration. [Issue #121](https://github.com/L3DigitalNet/star-trek-alter-course/issues/121) is selected implementation work.
- **Scope:** ADR 0014 and its related architecture/content/save/time/testing constraints; current Engineering, allocation, repair, bootstrap, scan/weapon associations, V9 mapping, and common player actions; wiki/roadmap/work-state routing and the previous agent prompt. The [owning migration contract](ship-system-substrate.md) records the resulting boundaries and required evidence.
- **Corrections:** distinguish kind/definition/installation and class defaults/live loadouts; require complete vertical migration rather than named-field wrappers; qualify absent and unsupported multiple-instance behavior; prevent hidden inventory disclosure through target availability/refusal; require direct current-format capture and explicit historical component mapping; distinguish frozen historical wire models from adaptable runtime translators; require characterization, heterogeneity, information-boundary, continuation, bounds, and conformance tests in #121.
- **Routing:** reconcile current work pointers and both project-owned architecture-router copies. Historical M6A formulas, V5/V9 implementation claims, and released V4/V8 behavior remain historical/current facts, not reasons to perpetuate fixed-field architecture.
- **Evidence and limits:** [PR #122](https://github.com/L3DigitalNet/star-trek-alter-course/pull/122) owns the actual diff, named-page review, capability disclosure, and final check results. This is documentation/design reconciliation, not runtime implementation, a fresh manual playtest, or independent peer review. New conformance tests are required future evidence, not claimed present tests.
- **Cadence:** targeted review only. The next full semantic sweep remains **2026-10-03**.

### Latest full semantic reconciliation — 2026-09-26

The full review preceded M6A behavior work against `42e760b02be12591e43416bc8c02bbe585ca6632`, after source-only v0.6.2 at `255eaedc8e27b483b0fd4e2fe0bccf050486b3bf`. It covered every then-indexed wiki topic, root summaries, STATUS/TODO, development operational prose including #109/#110, and ADRs 0001–0013. It inspected source/tests but was not itself a gameplay test execution.

It corrected then-current V8 scheduler bounds, release/synchronization language, implemented-response tense, historical V7 evidence, the unimplemented AssetCtl live provider probe, and caller-owned OpenBao resolution. Those V8 facts remain historical; M6A subsequently introduced V9.

The complete named-topic review matrix is retained in the [fixed pre-substrate source catalog](https://github.com/L3DigitalNet/star-trek-alter-course/blob/e16e1a83e064e35c1fd7bb7a43f9e595b4d36d81/docs/wiki/sources.md#full-baseline-semantic-reconciliation--2026-09-26) and [Feature #111 / PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112). Its next full review is **2026-10-03**; neither subsequent targeted audit resets it.

### M6A implementation and post-implementation review — 2026-09-26

[PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112) is M6A's final admission record. Source `5403a3d670808a6c105b53299e7d90fa560314e7` passed canonical verification with 789 Core, 324 AssetCtl, and 76 total Godot tests (73 gameplay, one integration, two imports), smoke, and warning-free C# builds. Documentation candidate `42fba73206de4f87e0a32e9b1c292c198296c5dc` passed all five hosted checks, including [run 36250509274](https://github.com/L3DigitalNet/star-trek-alter-course/actions/runs/36250509274). M6A merged as `c2edae1`; [post-merge run 36250769003](https://github.com/L3DigitalNet/star-trek-alter-course/actions/runs/36250769003) passed. [PR #113](https://github.com/L3DigitalNet/star-trek-alter-course/pull/113) records handoff closeout.

The Current-contact correction requires observer/target co-location and any present observation frame to match. Null legacy frames remain null; Stale/Lost frames remain historical. Six rejection cases failed before the correction; the focused filter passed 124 tests and final Core passed 789. Peer reviews were static; an exhausted attempt was not approval. Manual device testing was not claimed.

[PR #117](https://github.com/L3DigitalNet/star-trek-alter-course/pull/117) audited post-M6A documentation from `dc3a7ce`, incorporating separate SDK work through `3eec384`. It corrected the current 69,120 scheduler ceiling, architecture ownership, obsolete reporting/combat blockers, D-19's date, Engineering explanations, and final evidence navigation. It also recorded an unexpected nonfatal Godot `grab_focus` diagnostic in a passing scan/hail test. It did not fix runtime behavior or approve recovery gameplay. Its hosted verification and local-capability limits belong to that PR.

### Earlier targeted and consolidation reviews

Detailed records remain in their owning PRs and the [fixed prior source catalog](https://github.com/L3DigitalNet/star-trek-alter-course/blob/e16e1a83e064e35c1fd7bb7a43f9e595b4d36d81/docs/wiki/sources.md#review-record):

- [PR #99](https://github.com/L3DigitalNet/star-trek-alter-course/pull/99), September 8: published v0.6.0/V8 reconciliation after release `d00460e` and sync #98; preserved historical versions and partial milestones.
- [PR #89](https://github.com/L3DigitalNet/star-trek-alter-course/pull/89), September 7: bounded faction/knowledge review and wiki consolidation; preserved substantive contracts, authority distinctions, and historical sources.
- [Task #91 / PR #92](https://github.com/L3DigitalNet/star-trek-alter-course/pull/92), September 7: observation-response design admission at `ad73862`; invalid staging #90 closed unmerged. Its 505/324/68 gate verified the unchanged baseline, not the later feature.
- [Feature #93 / PR #94](https://github.com/L3DigitalNet/star-trek-alter-course/pull/94): implementation admission for bounded V8 observation response, later released in v0.6.0; not approval of exact combat mechanics.

## All active ADRs

- [0001 — Separate simulation from Godot](../adr/0001-separate-simulation-from-godot.md): one-way assembly dependency and pure Core testing.
- [0002 — One canonical quality gate](../adr/0002-use-one-canonical-quality-gate.md): pinned, fail-fast, read-only verification shared with CI.
- [0003 — Native capabilities and demand-driven dependencies](../adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md): ownership order, admission evidence, and excluded speculative frameworks.
- [0004 — Semantic spatial model](../adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md): scale relationships, graph/continuous space, and actor-safe projections.
- [0005 — JSON and schema validation](../adr/0005-use-json-and-schema-validation-for-domain-content.md): ordinary domain content, stable IDs, semantic validation, and specialized-format exceptions.
- [0006 — Versioned JSON snapshot saves](../adr/0006-use-versioned-json-snapshot-saves.md): mapping, migration, validation, and atomic recovery.
- [0007 — Deterministic time, scheduling, and randomness](../adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md): explicit clocks, serializable consequences, stable ordering, and future random streams.
- [0008 — Structured observability with Serilog](../adr/0008-use-structured-observability-with-serilog.md): typed diagnostics and nonauthoritative logging.
- [0009 — Layered testing and architecture conformance](../adr/0009-use-layered-testing-and-architecture-conformance.md): unit/scenario/property/engine testing and conditional package admission.
- [0010 — Explainable domain AI](../adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md): actor knowledge, constraints, deterministic decisions, and typed commands.
- [0011 — Explicit physical quantities](../adr/0011-represent-physical-quantities-with-explicit-units.md): canonical units and bounded fictional values.
- [0012 — Narrative subordinate to simulation](../adr/0012-keep-branching-narrative-subordinate-to-simulation.md): future Narrative boundary and conditional Ink admission.
- [0013 — Dev development and main releases](../adr/0013-use-dev-for-development-and-main-for-releases.md): branch/merge policy, governed admission, tags, and releases.
- [0014 — Extensible bounded ship-system substrate](../adr/0014-use-an-extensible-bounded-ship-system-substrate.md): distinct kind/definition/installation, heterogeneous runtime-owned loadouts, common mechanics, and typed specialized behavior.

## Canonical wiki decisions and future design

[Strategic Contact Reporting](strategic-contact-reporting.md) owns ship/player last-known information. [Faction assignment](faction-intent-and-autonomous-assignment.md) owns D-08–D-13. [Observation response](observation-driven-faction-response.md) owns D-14–D-17. [Engineering and combat](engineering-and-combat.md) owns D-19's implemented M6A mechanics. [Ship-system substrate](ship-system-substrate.md) owns the implemented Issue #121 migration, not future recovery gameplay. [Milestone proofs](milestone-proofs.md) and the roadmap distinguish partial milestones and sequence.

[Factions and organizations](factions-and-organizations.md) owns broader approved political principles, not implementation of hierarchy, organizations, governments, or treaties. [Open questions](open-questions.md) preserves unresolved intelligence, political, campaign, recovery, and compatibility decisions. ADR 0014 and Issue #121 supersede the earlier nonspecific next-step direction only for the required substrate migration.

## Consolidation map

- Former `docs/design/command-deck-ui.md` → [Interface](interface-and-player-commands.md): composition, Theme, preview, focus, and visual provenance.
- Former `docs/design/engineering-backbone.md` → [Engineering](engineering-and-combat.md): formulas, units, presets, repair/correlation, capability, proof values; [persistence](content-assets-and-persistence.md) owns migrations.
- Former `docs/design/first-observed-contact.md` → [Sensors](sensors-knowledge-and-ai.md): geometry, lifecycle, scan/hail, cautious policy, timing; persistence owns historical conversion.
- Former branch-governance discovery → [Governance](development-and-governance.md) and ADR 0013, with fixed historical provenance below.
- Former brainstorming and ChatGPT summary → owner intent in vision/decisions/open questions; unapproved proposals and deleted originals remain historical only.
- Former `docs/specs/asset-pipeline-tool.md` → [Asset pipeline tool](asset-pipeline-tool.md), preserving its full contract and identity.
- Root README gameplay details → owning system pages; root roadmap proofs → [Milestone proofs](milestone-proofs.md). Onboarding and sequence retain their separate roles.

## Dependency and development references

[AssetCtl admission](../dependency-admission/assetctl.md) · [JsonSchema.Net admission](../dependency-admission/jsonschema-net-core.md) · [Gameplay logging admission](../dependency-admission/gameplay-logging.md) · [ADR conformance review](../reviews/adr-conformance-2026-09-27.md) · [Development quality](../development-quality.md) · [Agent setup](../development-agent-skills.md) · [Contributing](../../CONTRIBUTING.md) · [AGENTS](../../AGENTS.md)

Exact selections belong in [global.json](../../global.json), [Directory.Build.props](../../Directory.Build.props), [Directory.Packages.props](../../Directory.Packages.props), and the [canonical verifier](../../scripts/verify.sh). [Asset configuration](../../config/assets/) and [AssetCtl](../../tools/AlterCourse.AssetCtl/) are separate tooling sources. [License](../../LICENSE.md) and [legal notice](../../LEGAL.md) govern rights; this catalog asserts no new clearance.

## Implementation evidence

- **World/bootstrap:** [FirstGameSetup](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [GameBootstrap](../../src/AlterCourse.Core/Gameplay/GameBootstrap.cs), [ShipState](../../src/AlterCourse.Core/Ships/ShipState.cs), [SimulationState](../../src/AlterCourse.Core/Gameplay/SimulationState.cs), [order tests](../../tests/AlterCourse.Core.Tests/Gameplay/OrderExecutionTests.cs).
- **Time/work:** [ScheduledWork](../../src/AlterCourse.Core/Simulation/ScheduledWork.cs), [work kinds](../../src/AlterCourse.Core/Simulation/ScheduledWorkKind.cs), [scheduler](../../src/AlterCourse.Core/Simulation/SimulationScheduler.cs), [tests](../../tests/AlterCourse.Core.Tests/Simulation/SimulationSchedulerTests.cs).
- **Observation/knowledge:** [SensorKnowledge](../../src/AlterCourse.Core/Sensors/SensorKnowledge.cs), [cautious policy](../../src/AlterCourse.Core/AI/CautiousContactDecisionPolicy.cs), [hail tests](../../tests/AlterCourse.Core.Tests/Gameplay/HailAndContactDecisionTests.cs), [knowledge validation](../../tests/AlterCourse.Core.Tests/Gameplay/SensorKnowledgeValidationTests.cs), [strategic reports](../../tests/AlterCourse.Core.Tests/Gameplay/StrategicContactReportTests.cs).
- **Engineering/repair:** [Engineering state](../../src/AlterCourse.Core/Ships/ShipEngineeringState.cs), [installed-system collection](../../src/AlterCourse.Core/Ships/InstalledSystemCollection.cs), [repair state](../../src/AlterCourse.Core/Ships/SystemRepairState.cs), [commands](../../src/AlterCourse.Core/Gameplay/GameSimulation.cs), [tests](../../tests/AlterCourse.Core.Tests/Ships/EngineeringBackboneTests.cs), [installed-system identity tests](../../tests/AlterCourse.Core.Tests/Ships/InstalledSystemIdentityTests.cs). The fixed-field M6A baseline has been replaced by the implemented #121 installed-system substrate on the development branch, including Godot's generic Engineering presentation ([projection tests](../../tests/AlterCourse.Core.Tests/Player/EngineeringProjectionTests.cs), [Engineering workspace](../../src/AlterCourse.Godot/src/Gameplay/EngineeringWorkspace.cs), [shell tests](../../src/AlterCourse.Godot/tests/GameplayShellTest.gd)).
- **Faction assignment:** [policy](../../src/AlterCourse.Core/AI/FactionAssignmentPolicy.cs), [runtime](../../src/AlterCourse.Core/Gameplay/GameSimulation.Factions.cs), [policy tests](../../tests/AlterCourse.Core.Tests/AI/FactionAssignmentPolicyTests.cs), [typed-target tests](../../tests/AlterCourse.Core.Tests/Simulation/FactionSchedulerTargetTests.cs), [wake tests](../../tests/AlterCourse.Core.Tests/Gameplay/FactionRuntimeWakeTests.cs).
- **Faction proof:** [scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/FactionAssignmentScenarioTests.cs), [knowledge boundaries](../../tests/AlterCourse.Core.Tests/Gameplay/FactionKnowledgeBoundaryTests.cs), [horizon](../../tests/AlterCourse.Core.Tests/Gameplay/FactionLongHorizonTests.cs), [V7 tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV7FactionTests.cs).
- **Observation response:** [runtime](../../src/AlterCourse.Core/Gameplay/GameSimulation.Observation.cs), [state](../../src/AlterCourse.Core/Factions/FactionObservationState.cs), [scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/ObservationResponseScenarioTests.cs), [fixture](../../tests/AlterCourse.Core.Tests/Gameplay/ObservationResponseProofFixture.cs), [horizon](../../tests/AlterCourse.Core.Tests/Gameplay/ObservationResponseLongHorizonTests.cs), [V8 tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV8ObservationTests.cs).
- **M6A:** [combat](../../src/AlterCourse.Core/Gameplay/GameSimulation.Combat.cs), [defensive policy](../../src/AlterCourse.Core/AI/DefensiveCombatDecisionPolicy.cs), [runtime tests](../../tests/AlterCourse.Core.Tests/Gameplay/CombatRuntimeTests.cs), [policy tests](../../tests/AlterCourse.Core.Tests/AI/DefensiveCombatDecisionPolicyTests.cs), [scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/M6CombatScenarioTests.cs), [horizon](../../tests/AlterCourse.Core.Tests/Gameplay/M6CombatLongHorizonTests.cs), [V9 tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV9CombatTests.cs).
- **Content/saves:** [mapping](../../src/AlterCourse.Core/Persistence/GamePersistence.cs), [admission prescan](../../src/AlterCourse.Core/Persistence/GamePersistence.Bounds.cs), [file-operation seam](../../src/AlterCourse.Core/Persistence/SaveFileOperations.cs), [admission tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceAdmissionTests.cs), [write-failure tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceWriteFailureTests.cs), [V10 capture/migration](../../src/AlterCourse.Core/Persistence/GamePersistence.V10.cs), [Pathfinder V6](../../src/AlterCourse.Godot/content/ships/pathfinder.json), [ship-definition V6 schema](../../src/AlterCourse.Godot/content/schemas/ship-definition-v6.schema.json), [system-definition V1 catalog](../../src/AlterCourse.Godot/content/systems/pathfinder-systems.json) and [schema](../../src/AlterCourse.Godot/content/schemas/system-definition-v1.schema.json), [faction loader](../../src/AlterCourse.Core/Content/FactionDefinitionCatalogLoader.cs), [maximum-shape tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV4SensorTests.cs), [V10 compatibility tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV10CompatibilityTests.cs).
- **Diagnostics and ADR conformance:** [post-commit Core decision diagnostics](../../src/AlterCourse.Core/Gameplay/GameSimulation.Diagnostics.cs), [Godot scene composition](../../src/AlterCourse.Godot/src/Gameplay/Logging/GameplayLogging.cs), and [allowlisted events](../../src/AlterCourse.Godot/src/Gameplay/Logging/GameDiagnostics.cs). [Dependency rules](../../tests/AlterCourse.Core.Tests/DependencyArchitectureTests.cs) and [generated invariants](../../tests/AlterCourse.Core.Tests/GeneratedSimulationInvariantTests.cs) supply focused architecture and behavioral proof; exact execution receipts belong to the conformance register.
- **Godot:** [GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs), [Engineering workspace](../../src/AlterCourse.Godot/src/Gameplay/EngineeringWorkspace.cs), [shell tests](../../src/AlterCourse.Godot/tests/GameplayShellTest.gd).

V9 proof values were 88,137,170 bytes for the faction fixture, 108,935,516 for the empty-combat report vertex, and 113,292,140 for the conservative supported-shape bound within 128 MiB. The implemented V10 substrate migration measures 109,030,603 bytes for its maximum-width save, with a conservative supported-shape bound of 114,536,452 bytes within the unchanged 128 MiB envelope. Populated continuation separately covers 255 simultaneous NPC stimuli. These are not ordinary save sizes or future-schema guarantees. [Content/persistence](content-assets-and-persistence.md) retains historical values. [Implementation status](implementation-status.md#verification-evidence-not-a-fresh-execution-claim) distinguishes inherited admission from later checks.

## Latest design-approval evidence

[PR #84](https://github.com/L3DigitalNet/star-trek-alter-course/pull/84) records the September 6 faction-slice approval. [Task #91 / PR #92](https://github.com/L3DigitalNet/star-trek-alter-course/pull/92) records September 7 observation-response admission and subsequent M6/Engineering sequence. [Feature #111 / PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112) records September 26 M6A refinement and implementation. Those records are not approval of later recovery gameplay.

[PR #120](https://github.com/L3DigitalNet/star-trek-alter-course/pull/120) adopts ADR 0014. The owner then selected [Issue #121](https://github.com/L3DigitalNet/star-trek-alter-course/issues/121) for implementation before further systems. [PR #122](https://github.com/L3DigitalNet/star-trek-alter-course/pull/122) reconciled that migration's admission documentation and handoff before implementation began. Final [PR #123](https://github.com/L3DigitalNet/star-trek-alter-course/pull/123) completed runtime, content, persistence, Godot presentation, and integrated proof, merging into `dev` as `17637dd`. Issue #121 is closed Done; [PR #124](https://github.com/L3DigitalNet/star-trek-alter-course/pull/124) records its operational handoff. The migration remains unreleased.

## Visual references

[Travel](../ui/reference/command-deck-travel.png), [Combat](../ui/reference/command-deck-combat.png), and [Engineering](../ui/reference/engineering-workspace.png) remain presentation references. [Interface](interface-and-player-commands.md) owns Figma provenance; the [runtime Theme](../../src/AlterCourse.Godot/assets/ui/command_theme.tres) owns styling. Images do not establish depicted systems as implemented.

## Operational knowledge and history

[STATUS](../STATUS.md), [TODO](../TODO.md), [handoff state](../handoff/state.md), [architecture](../handoff/architecture.md), [conventions](../handoff/conventions.md), [spec/plan pointers](../handoff/specs-plans.md), and [deployment](../handoff/deployed.md) retain operational facts rather than replacement design. [Credential references](../handoff/credentials.md) are reference-only; never copy values. [Bugs](../handoff/bugs/INDEX.md) and [sessions](../handoff/sessions/) preserve operational history.

## Historical provenance

Deleted originals remain at fixed revision `685a60577a671e4f39828608ae148263697a9013`; they are provenance, not current authority.

- [Owner brief/transcript](https://github.com/L3DigitalNet/star-trek-alter-course/blob/685a60577a671e4f39828608ae148263697a9013/docs/design/initial-brainstorm-session.md): owner intent is consolidated; the assistant's 2378 suggestion was not approval.
- [Branch/release discovery](https://github.com/L3DigitalNet/star-trek-alter-course/blob/685a60577a671e4f39828608ae148263697a9013/docs/design/branch-release-governance.md): rationale adopted by ADR 0013.
- [Original design directory](https://github.com/L3DigitalNet/star-trek-alter-course/tree/685a60577a671e4f39828608ae148263697a9013/docs/design): former UI, Engineering, contact, and save contracts.
- [Former ChatGPT summary](https://github.com/L3DigitalNet/star-trek-alter-course/blob/685a60577a671e4f39828608ae148263697a9013/docs/llm-resources/project-instructions-chatgpt.md): duplicated guidance retained only for history.

## Maintaining coverage

Record design on its owning page, architecture through ADRs, implementation claims against source/tests, and execution in the governing PR. Preserve important review detail through fixed revisions and PRs. A targeted review, format check, or link check does not reset the full-sweep date or prove semantic conformance by itself.
