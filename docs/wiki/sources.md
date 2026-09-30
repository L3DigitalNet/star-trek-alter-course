---
schema_version: '1.1'
id: 'index-5yghuo-sources'
title: 'Design Source Catalog'
description: 'Coverage index of architectural records, game contracts, implementation evidence, dated reviews, and historical provenance.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-30'
tags:
  - 'design'
  - 'architecture'
aliases: []
related:
  - 'docs/adr/README.md'
  - 'docs/wiki/README.md'
  - 'docs/wiki/decision-register.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/observation-driven-faction-response.md'
  - 'docs/wiki/implementation-status.md'
  - 'docs/wiki/ship-system-substrate.md'
  - 'docs/reviews/adr-expansion-documentation-review-2026-09-27.md'
---

# Design source catalog

[Wiki home](README.md) · [ADR catalog](../adr/README.md) · [Decision register](decision-register.md) · [Open questions](open-questions.md)

## Coverage and authority

The wiki owns gameplay design; ADRs own architectural decisions; source, schemas, and tests establish implementation; dated runs establish execution. Historical records explain origin. Future prose, package candidates, and archived recommendations are not implementation or approval evidence.

The [authority map](../README.md) distinguishes documents outside the wiki. This catalog is a navigation and coverage record, not an unbounded session log. Fixed revisions and governing PRs retain detailed history. No entry claims every external reference, live provider, or third-party tool was reverified.

## Review record

### Content admission and withdrawal geometry follow-up — 2026-09-30

[Issue #135](https://github.com/L3DigitalNet/star-trek-alter-course/issues/135) and [Final draft PR #138](https://github.com/L3DigitalNet/star-trek-alter-course/pull/138) own the bounded R1–R3 follow-up. The [dated report](../reviews/adr-remediation-2026-09-30.md) preserves the September 27 register and records exact reproduction revisions, executor commands, outcomes, the September 30 compatibility decision, and remaining verification gaps. Documentation adoption through [PR #137](https://github.com/L3DigitalNet/star-trek-alter-course/pull/137) merged as `7e2df746688f63df9936988d20978710e0aaf2d4`, with source/tests/scripts unchanged from `efd4ac93894fc28ea0c3ea40ca1be008dd4d6631`; it did not fix these defects or certify the four additional ADRs.

The targeted review compared content/persistence, Engineering/combat, world/navigation/time, sensors/knowledge/AI, and implementation status with `GameScreen`, the native content adapter, all three `DefinitionContent.FromText` factories, both contact policies, and the reproduction/focused tests. R1/R2 were reproduced and fixed: corrected content worker `b6755f74d441002c69e7674283fc193d6786fcfe` passed 184 Core content cases and 103 Godot shell cases. R3's corrected worker `7bd42bd7d3154d01028e3228be04a537f732562d` passed 147 numerical and command/save-load cases. Native security and geometry reviews confirmed their scoped claims without independently executing tests. Combined canonical verification at runtime-source candidate `e1c89f9de42f827cb8132207daa707c354fe88b1` passed 1,376 Core, 324 AssetCtl, and Godot one integration/two asset-import/103 gameplay cases plus smoke. Continuous domain coordinates, actor-local nonzero-displacement constraints, ordinary course application, and same-build continuation already have exact owning contracts; the spatial/policy pages are preserved. Content and implementation-status prose now describe the corrected boundaries and distinguish source-candidate proof from final admission.

The ADR 0006/0007 compatibility amendments are integrated in documentation baseline `27a393d36b02da8dfcaa46ab07d0fdebc9ce5639`: cross-version obligations start at v1.0.0; strict admission and same-build continuation still apply. Documentation gates, mutation results, cross-agent review, and final hosted admission remain pending in the report; PR #138's execution receipt owns the final PR head. Exported-PCK execution and nonseekable-input coverage are not claimed. This is a targeted review, not a full semantic sweep, manual playtest, release, or broad ADR certification. The next full sweep remains **2026-10-03**; UnitsNet's bounded exception and future-trigger deferrals remain unchanged.

### Additional ADRs and documentation polish — 2026-09-27

[PR #134](https://github.com/L3DigitalNet/star-trek-alter-course/pull/134) records the owner-selected ADRs 0015–0018 and documentation-only reconciliation from `dev` at `efd4ac93894fc28ea0c3ea40ca1be008dd4d6631`. The [review record](../reviews/adr-expansion-documentation-review-2026-09-27.md) identifies edited surfaces, preservation checks, source entry points, and capability limits. It does not certify new runtime behavior or silently expand the original 191-obligation audit.

The pass clarifies command commitment, knowledge/report ownership, AssetCtl publication, and session/action lifetime; centralizes ADR navigation; reduces duplicated release history; corrects completed-substrate tense; and aligns project-owned architecture guidance. Detailed gameplay contracts, historical approvals, and released V8 versus development V10 remain intact. The PR owns actual final-head checks and admission status.

This is a repository documentation/editorial pass with targeted architecture traces, not a new full gameplay-semantic sweep or manual playtest. The next full sweep remains **2026-10-03**.

### ADR reconciliation and implementation conformance — 2026-09-27

The [191-obligation register](../reviews/adr-conformance-2026-09-27.md) records the full review of ADRs 0001–0014 from baseline `2ef6c77aa4587719aac0b59cfda3d6ee7c560e5a`, scoped implementation traces, independent reviews, amendments, applicability, and execution receipts. [Initiative #127](https://github.com/L3DigitalNet/star-trek-alter-course/issues/127), [PR #132](https://github.com/L3DigitalNet/star-trek-alter-course/pull/132), and [closeout PR #133](https://github.com/L3DigitalNet/star-trek-alter-course/pull/133) govern that completed work.

The [integration addendum](../reviews/adr-conformance-2026-09-27.md#integration-addendum--2026-09-27) distinguishes candidate and actual post-merge verification. It retains the approved bounded UnitsNet exception and conditional no-RNG treatment. That audit predates ADRs 0015–0018 and is not their conformance certificate. Its [original catalog entry](https://github.com/L3DigitalNet/star-trek-alter-course/blob/efd4ac93894fc28ea0c3ea40ca1be008dd4d6631/docs/wiki/sources.md#adr-reconciliation-and-implementation-conformance--2026-09-27) preserves the complete contemporary summary. It did not reset the full-sweep date.

### Post-merge substrate documentation correction — 2026-09-27

Reviewed `dev` at `a090c33` against runtime merge `17637dd`, installed-system state, V10 mapping, Godot loading, and continuation/compatibility tests. Corrected stale next-implementation and pre-merge language. Issue #121 and PRs #123/#124 were complete; ship V6/system V1/save V10 were current development formats. The [fixed original entry](https://github.com/L3DigitalNet/star-trek-alter-course/blob/efd4ac93894fc28ea0c3ea40ca1be008dd4d6631/docs/wiki/sources.md#post-merge-substrate-documentation-correction--2026-09-27) retains scope and limits. This was targeted inspection, not a fresh gameplay run or full sweep.

### Ship-system substrate implementation review — 2026-09-27

The migration review progressed from `ac9d5ac9bf173375aa1aeb2e4d65cb1b820b4063` through integrated proof at `0403377994e47387ce7fdddf84273974219f0108`. Early pending-test statements describe the earlier leg, not the completed migration. Final PR #123 merged as `17637dd` with an identical tested tree.

[The full leg-by-leg record](https://github.com/L3DigitalNet/star-trek-alter-course/blob/efd4ac93894fc28ea0c3ea40ca1be008dd4d6631/docs/wiki/sources.md#ship-system-substrate-implementation-review--2026-09-27) preserves reviewed pages, source paths, action/identity changes, actual proof names, limitations, and chronology. The [substrate contract](ship-system-substrate.md) owns current behavior; no later recovery gameplay was approved by this review.

### Ship-system substrate admission review — 2026-09-26

The admission review at `e16e1a83e064e35c1fd7bb7a43f9e595b4d36d81` followed ADR 0014 and preceded implementation. [PR #122](https://github.com/L3DigitalNet/star-trek-alter-course/pull/122) clarified distinct identities, live loadouts, complete vertical migration, historical mapping, remote-target information safety, and required proof. [The original record](https://github.com/L3DigitalNet/star-trek-alter-course/blob/efd4ac93894fc28ea0c3ea40ca1be008dd4d6631/docs/wiki/sources.md#ship-system-substrate-admission-review--2026-09-26) retains its no-runtime/no-execution limits. Its pre-implementation wording is historical.

### Latest full semantic reconciliation — 2026-09-26

The latest full semantic sweep preceded M6A work at `42e760b02be12591e43416bc8c02bbe585ca6632`, after source-only v0.6.2. It covered every then-indexed wiki topic, root summaries, STATUS/TODO, development prose, and ADRs 0001–0013. It inspected source/tests; it was not itself a gameplay test execution.

It corrected then-current V8 bounds, release/synchronization language, response tense, V7 evidence, the unimplemented live AssetCtl probe, and caller-owned OpenBao resolution. Its [complete topic matrix](https://github.com/L3DigitalNet/star-trek-alter-course/blob/e16e1a83e064e35c1fd7bb7a43f9e595b4d36d81/docs/wiki/sources.md#full-baseline-semantic-reconciliation--2026-09-26) and [PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112) retain evidence. The next full sweep is **2026-10-03**; later targeted/editorial work does not reset it.

### M6A implementation and post-implementation review — 2026-09-26

[PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112) is the implementation admission. Runtime head `5403a3d670808a6c105b53299e7d90fa560314e7` and documentation head `42fba73206de4f87e0a32e9b1c292c198296c5dc` have their own recorded verification. M6A merged as `c2edae1`; [post-merge run 36250769003](https://github.com/L3DigitalNet/star-trek-alter-course/actions/runs/36250769003) passed, and [PR #113](https://github.com/L3DigitalNet/star-trek-alter-course/pull/113) closed handoff.

The Current-contact correction, exact counts, independent-review limits, and later [PR #117](https://github.com/L3DigitalNet/star-trek-alter-course/pull/117) documentation audit remain in the [fixed detailed record](https://github.com/L3DigitalNet/star-trek-alter-course/blob/efd4ac93894fc28ea0c3ea40ca1be008dd4d6631/docs/wiki/sources.md#m6a-implementation-and-post-implementation-review--2026-09-26). The audit did not approve recovery gameplay; its nonfatal focus diagnostic was separately addressed during the substrate work.

### Earlier targeted and consolidation reviews

The initial wiki implementation review used `42481ca7fbc6c5c9da96985e02565f78a236cab7` on September 6. Later records remain in their PRs and the [fixed prior catalog](https://github.com/L3DigitalNet/star-trek-alter-course/blob/e16e1a83e064e35c1fd7bb7a43f9e595b4d36d81/docs/wiki/sources.md#review-record):

- [PR #99](https://github.com/L3DigitalNet/star-trek-alter-course/pull/99): September 8 release/V8 reconciliation after `d00460e` and sync #98.
- [PR #89](https://github.com/L3DigitalNet/star-trek-alter-course/pull/89): September 7 bounded faction/knowledge review and wiki consolidation.
- [Task #91 / PR #92](https://github.com/L3DigitalNet/star-trek-alter-course/pull/92): September 7 observation-response admission at `ad73862`; its checks verified the unchanged baseline, not the later feature. Invalid staging #90 closed unmerged.
- [Feature #93 / PR #94](https://github.com/L3DigitalNet/star-trek-alter-course/pull/94): bounded V8 response implementation, later released in v0.6.0; not approval of detailed combat mechanics.

## All active ADRs

The [ADR catalog](../adr/README.md#active-catalog) is the complete maintained index of records 0001–0018. Its [scope map](../adr/README.md#scope-and-overlap) distinguishes overlapping subjects and its maintenance guidance covers amendments and supersession. This anchor remains stable for existing links without duplicating another title inventory.

## Canonical wiki decisions and future design

[Strategic Contact Reporting](strategic-contact-reporting.md) owns ship/player historical information. [Faction assignment](faction-intent-and-autonomous-assignment.md) owns D-08–D-13; [Observation response](observation-driven-faction-response.md) owns D-14–D-17; [Engineering and combat](engineering-and-combat.md) owns D-19's mechanics. [Ship-system substrate](ship-system-substrate.md) owns the completed #121 migration, not future recovery. [Milestone proofs](milestone-proofs.md) and the roadmap distinguish scope and sequence.

[Factions and organizations](factions-and-organizations.md) owns approved political principles, not proof of implemented hierarchy/governments/treaties. [Open questions](open-questions.md) retains unresolved intelligence, political, campaign, recovery, and compatibility choices. New cross-cutting ADRs formalize boundaries without answering those choices.

## Consolidation map

- Former `docs/design/command-deck-ui.md` → [Interface](interface-and-player-commands.md): composition, Theme, preview, focus, and visual provenance.
- Former `docs/design/engineering-backbone.md` → [Engineering](engineering-and-combat.md): formulas, quantities, allocation, repair, capability, and proof; persistence owns migration.
- Former `docs/design/first-observed-contact.md` → [Sensors](sensors-knowledge-and-ai.md): geometry, lifecycle, scan/hail, policy, and timing.
- Former branch-governance discovery → [Governance](development-and-governance.md) and ADR 0013, with fixed provenance below.
- Former brainstorming/ChatGPT summary → vision, decisions, and open questions; unapproved proposals remain historical.
- Former `docs/specs/asset-pipeline-tool.md` → [Asset pipeline tool](asset-pipeline-tool.md), preserving its full contract and identity; ADR 0017 owns its architectural boundaries.
- Root README gameplay detail and roadmap proofs → owning system pages and [Milestone proofs](milestone-proofs.md); onboarding and sequence remain separate.

## Dependency and development references

[AssetCtl admission](../dependency-admission/assetctl.md) · [JsonSchema.Net admission](../dependency-admission/jsonschema-net-core.md) · [Gameplay logging admission](../dependency-admission/gameplay-logging.md) · [Architecture testing admission](../dependency-admission/architecture-testing.md) · [UnitsNet evaluation](../dependency-admission/unitsnet-evaluation.md) · [Development quality](../development-quality.md) · [Agent setup](../development-agent-skills.md) · [Contributing](../../CONTRIBUTING.md)

Exact selections belong in [global.json](../../global.json), [Directory.Build.props](../../Directory.Build.props), [Directory.Packages.props](../../Directory.Packages.props), lock files, and the [canonical verifier](../../scripts/verify.sh). [Asset configuration](../../config/assets/) and [AssetCtl](../../tools/AlterCourse.AssetCtl/) are development-tool sources. [License](../../LICENSE.md) and [legal notice](../../LEGAL.md) retain their rights boundaries.

## Implementation evidence

- **World/bootstrap:** [FirstGameSetup](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [GameBootstrap](../../src/AlterCourse.Core/Gameplay/GameBootstrap.cs), [ShipState](../../src/AlterCourse.Core/Ships/ShipState.cs), [SimulationState](../../src/AlterCourse.Core/Gameplay/SimulationState.cs), [order tests](../../tests/AlterCourse.Core.Tests/Gameplay/OrderExecutionTests.cs).
- **Commands/time:** [GameSimulation](../../src/AlterCourse.Core/Gameplay/GameSimulation.cs), [ScheduledWork](../../src/AlterCourse.Core/Simulation/ScheduledWork.cs), [work kinds](../../src/AlterCourse.Core/Simulation/ScheduledWorkKind.cs), [scheduler](../../src/AlterCourse.Core/Simulation/SimulationScheduler.cs), [scheduler tests](../../tests/AlterCourse.Core.Tests/Simulation/SimulationSchedulerTests.cs).
- **Knowledge:** [SensorKnowledge](../../src/AlterCourse.Core/Sensors/SensorKnowledge.cs), [cautious policy](../../src/AlterCourse.Core/AI/CautiousContactDecisionPolicy.cs), [hail tests](../../tests/AlterCourse.Core.Tests/Gameplay/HailAndContactDecisionTests.cs), [validation tests](../../tests/AlterCourse.Core.Tests/Gameplay/SensorKnowledgeValidationTests.cs), [strategic reports](../../tests/AlterCourse.Core.Tests/Gameplay/StrategicContactReportTests.cs).
- **Engineering:** [Engineering state](../../src/AlterCourse.Core/Ships/ShipEngineeringState.cs), [installed collection](../../src/AlterCourse.Core/Ships/InstalledSystemCollection.cs), [repair state](../../src/AlterCourse.Core/Ships/SystemRepairState.cs), [backbone tests](../../tests/AlterCourse.Core.Tests/Ships/EngineeringBackboneTests.cs), [identity tests](../../tests/AlterCourse.Core.Tests/Ships/InstalledSystemIdentityTests.cs), [projection tests](../../tests/AlterCourse.Core.Tests/Player/EngineeringProjectionTests.cs).
- **Faction assignment:** [policy](../../src/AlterCourse.Core/AI/FactionAssignmentPolicy.cs), [runtime](../../src/AlterCourse.Core/Gameplay/GameSimulation.Factions.cs), [policy tests](../../tests/AlterCourse.Core.Tests/AI/FactionAssignmentPolicyTests.cs), [typed targets](../../tests/AlterCourse.Core.Tests/Simulation/FactionSchedulerTargetTests.cs), [wakes](../../tests/AlterCourse.Core.Tests/Gameplay/FactionRuntimeWakeTests.cs), [scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/FactionAssignmentScenarioTests.cs), [knowledge tests](../../tests/AlterCourse.Core.Tests/Gameplay/FactionKnowledgeBoundaryTests.cs), [horizons](../../tests/AlterCourse.Core.Tests/Gameplay/FactionLongHorizonTests.cs), [V7 tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV7FactionTests.cs).
- **Observation response:** [runtime](../../src/AlterCourse.Core/Gameplay/GameSimulation.Observation.cs), [state](../../src/AlterCourse.Core/Factions/FactionObservationState.cs), [scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/ObservationResponseScenarioTests.cs), [fixture](../../tests/AlterCourse.Core.Tests/Gameplay/ObservationResponseProofFixture.cs), [horizons](../../tests/AlterCourse.Core.Tests/Gameplay/ObservationResponseLongHorizonTests.cs), [V8 tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV8ObservationTests.cs).
- **Combat:** [transition](../../src/AlterCourse.Core/Gameplay/GameSimulation.Combat.cs), [defensive policy](../../src/AlterCourse.Core/AI/DefensiveCombatDecisionPolicy.cs), [runtime tests](../../tests/AlterCourse.Core.Tests/Gameplay/CombatRuntimeTests.cs), [policy tests](../../tests/AlterCourse.Core.Tests/AI/DefensiveCombatDecisionPolicyTests.cs), [scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/M6CombatScenarioTests.cs), [horizons](../../tests/AlterCourse.Core.Tests/Gameplay/M6CombatLongHorizonTests.cs), [V9 tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV9CombatTests.cs).
- **Content/saves:** [mapping](../../src/AlterCourse.Core/Persistence/GamePersistence.cs), [admission bounds](../../src/AlterCourse.Core/Persistence/GamePersistence.Bounds.cs), [file operations](../../src/AlterCourse.Core/Persistence/SaveFileOperations.cs), [admission tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceAdmissionTests.cs), [write failures](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceWriteFailureTests.cs), [V10 mapping](../../src/AlterCourse.Core/Persistence/GamePersistence.V10.cs), [ship V6](../../src/AlterCourse.Godot/content/ships/pathfinder.json), [ship schema](../../src/AlterCourse.Godot/content/schemas/ship-definition-v6.schema.json), [system catalog](../../src/AlterCourse.Godot/content/systems/pathfinder-systems.json), [system schema](../../src/AlterCourse.Godot/content/schemas/system-definition-v1.schema.json), [faction loader](../../src/AlterCourse.Core/Content/FactionDefinitionCatalogLoader.cs), [maximum shapes](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV4SensorTests.cs), [V10 compatibility](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV10CompatibilityTests.cs).
- **Diagnostics/conformance:** [Core diagnostics](../../src/AlterCourse.Core/Gameplay/GameSimulation.Diagnostics.cs), [Godot composition](../../src/AlterCourse.Godot/src/Gameplay/Logging/GameplayLogging.cs), [allowlisted events](../../src/AlterCourse.Godot/src/Gameplay/Logging/GameDiagnostics.cs), [dependency rules](../../tests/AlterCourse.Core.Tests/DependencyArchitectureTests.cs), [generated invariants](../../tests/AlterCourse.Core.Tests/GeneratedSimulationInvariantTests.cs).
- **Godot/session:** [GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs), [OwnShipActionBinding](../../src/AlterCourse.Godot/src/Gameplay/OwnShipActionBinding.cs), [EngineeringWorkspace](../../src/AlterCourse.Godot/src/Gameplay/EngineeringWorkspace.cs), [shell tests](../../src/AlterCourse.Godot/tests/GameplayShellTest.gd).
- **Asset tooling:** [composition](../../tools/AlterCourse.AssetCtl/Program.cs), [generation](../../tools/AlterCourse.AssetCtl/Generation/GenerationOrchestrator.cs), [publication/recovery](../../tools/AlterCourse.AssetCtl/Publishing/PublishingTypes.cs), [tests](../../tests/AlterCourse.AssetCtl.Tests/), and [offline gate](../../scripts/verify.sh).

[Content/persistence](content-assets-and-persistence.md) owns version-qualified proof values and capacity bounds; they are not ordinary save sizes or future-schema guarantees. [Implementation status](implementation-status.md#verification-evidence-not-a-fresh-execution-claim) distinguishes inherited admission from later runs. This index identifies where to inspect evidence, not a fresh execution of every linked test.

## Latest design-approval evidence

[PR #84](https://github.com/L3DigitalNet/star-trek-alter-course/pull/84) records September 6 faction-slice approval; [PR #92](https://github.com/L3DigitalNet/star-trek-alter-course/pull/92) records September 7 response admission and later M6 sequence; [PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112) records September 26 M6A refinement. None approves later recovery gameplay.

[PR #120](https://github.com/L3DigitalNet/star-trek-alter-course/pull/120) adopts ADR 0014. [PR #122](https://github.com/L3DigitalNet/star-trek-alter-course/pull/122) reconciles Issue #121 admission; [PR #123](https://github.com/L3DigitalNet/star-trek-alter-course/pull/123) completes implementation and integrated proof; [PR #124](https://github.com/L3DigitalNet/star-trek-alter-course/pull/124) records handoff. The migration remains unreleased. [PR #134](https://github.com/L3DigitalNet/star-trek-alter-course/pull/134) records owner-selected additional ADR documentation, not new gameplay or release.

## Visual references

[Travel](../ui/reference/command-deck-travel.png), [Combat](../ui/reference/command-deck-combat.png), and [Engineering](../ui/reference/engineering-workspace.png) are presentation references. [Interface](interface-and-player-commands.md) owns Figma provenance; the [runtime Theme](../../src/AlterCourse.Godot/assets/ui/command_theme.tres) owns styling. Images do not prove depicted systems exist.

## Operational knowledge and history

[STATUS](../STATUS.md), [TODO](../TODO.md), [handoff state](../handoff/state.md), [architecture](../handoff/architecture.md), [conventions](../handoff/conventions.md), [spec/plan pointers](../handoff/specs-plans.md), and [deployment](../handoff/deployed.md) retain operational facts. [Credentials](../handoff/credentials.md) are reference-only; never copy values. [Bugs](../handoff/bugs/INDEX.md) and [sessions](../handoff/sessions/) preserve historical evidence.

## Historical provenance

Deleted originals remain at fixed revision `685a60577a671e4f39828608ae148263697a9013`; they are provenance, not current authority.

- [Owner brief/transcript](https://github.com/L3DigitalNet/star-trek-alter-course/blob/685a60577a671e4f39828608ae148263697a9013/docs/design/initial-brainstorm-session.md): consolidated owner intent; the assistant's 2378 suggestion was not approval.
- [Branch/release discovery](https://github.com/L3DigitalNet/star-trek-alter-course/blob/685a60577a671e4f39828608ae148263697a9013/docs/design/branch-release-governance.md): rationale adopted by ADR 0013.
- [Original design directory](https://github.com/L3DigitalNet/star-trek-alter-course/tree/685a60577a671e4f39828608ae148263697a9013/docs/design): former UI, Engineering, contact, and save contracts.
- [Former ChatGPT summary](https://github.com/L3DigitalNet/star-trek-alter-course/blob/685a60577a671e4f39828608ae148263697a9013/docs/llm-resources/project-instructions-chatgpt.md): duplicated guidance retained only for history.

## Maintaining coverage

Keep design on its owning page, architecture in ADRs, implementation claims grounded in source/tests, and actual execution/admission in the governing PR. Preserve important review detail through fixed revisions. Targeted review, formatting, or link checks do not reset the full-sweep date or prove semantic conformance by themselves.
