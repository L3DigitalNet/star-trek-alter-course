---
schema_version: '1.1'
id: 'reference-g40auc-implementation-status'
title: 'Implementation Status'
description: 'Reviewed gameplay baseline and explicit boundaries between runtime, approved future work, previews, and absent systems.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-26'
tags:
  - 'simulation'
  - 'design'
aliases: []
related:
  - 'docs/STATUS.md'
  - 'README.md'
  - 'ROADMAP.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/observation-driven-faction-response.md'
  - 'docs/wiki/engineering-and-combat.md'
  - 'docs/wiki/ship-system-substrate.md'
---

# Implementation status

[Wiki home](README.md) · [Sources](sources.md) · [Open questions](open-questions.md)

## Reviewed baseline

[v0.6.2](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.6.2) is the current immutable source-only release. Its latest gameplay-changing release is v0.6.0, which uses V4 ship content and V8 saves under `observation-driven-faction-response-v1` and contains Feature #86 / Final PR #87's bounded faction slice and Feature #93 / Final PR #94's observation response.

Current `dev` additionally has **unreleased M6A First Combat Engagement**, implemented by [Feature #111 / Final PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112) and merged as `c2edae1f6357dbe8cd60aa5040646e48b9c23aee`. It uses V5 ship content and V9 saves under `first-combat-engagement-v1`. [PR #113](https://github.com/L3DigitalNet/star-trek-alter-course/pull/113) records its handoff closeout at `dc3a7cebd6b765b7a16d573f76bf4414fabb00b3`. Later tooling or documentation commits do not by themselves publish this gameplay. v0.5.0 remains the historical V6 release. Consult [STATUS](../STATUS.md) for current operational work and [Sources](sources.md#review-record) for subsequent review coverage.

## Selected next work, not implemented

[ADR 0014](../adr/0014-use-an-extensible-bounded-ship-system-substrate.md) was adopted through [PR #120](https://github.com/L3DigitalNet/star-trek-alter-course/pull/120) at `e16e1a8`. The current fixed-field implementation only partially conforms. The owner selected [Issue #121](https://github.com/L3DigitalNet/star-trek-alter-course/issues/121) to migrate all five existing systems through the [installed-system substrate contract](ship-system-substrate.md) before further systems or Damage Control gameplay.

The migration must establish independently owned heterogeneous live loadouts and common installed-ID mechanics, not a facade over named fields. Expected ship V6/system-definition V1/save V10 formats and new conformance tests are admission targets, not present runtime evidence. Neither the ADR nor this documentation completes #121, M3, M5, or M6. Recovery mechanics and player refits remain outside the selected implementation.

## Implemented gameplay

Core owns multiple ordinary persistent ships and an explicit `PlayerShipId`; the production default world contains six ships across three strategic locations. The ship-catalog-only bootstrap overload retains the earlier four-ship, zero-faction proof. The player ship is not a separate world-root entity type. Typed bootstrap distinguishes reusable design definitions from vessel names and starting condition.

Strategic travel has scheduled arrival. Durable `TravelTo`, `PatrolRoute`, and `HoldUntil` orders exist, with offscreen progression and long-horizon tests. Not every ship in the default contact proof has an active order; order machinery and the currently authored proof scenario are different things.

Local tactical space has continuous 2D positions and course/speed commands. Observer-local contacts support Current, Stale, Lost, reacquisition, active identification, and a typed hail acknowledgement. A bounded cautious-contact policy acts from its own knowledge rather than hidden target state.

Engineering owns generation condition, constrained four-consumer allocation, five system conditions, derived capability, and one repair per ship for every system except generation. Those values affect detection, tactical course limits, shields, directed-energy fire, and deterministic forced reconciliation after damage. The live Engineering and Combat UI is not merely a preview.

Feature #86 added root faction definitions and mutable presence objectives, ship-side optional direct `FactionId` control with a derived roster, pure own-asset assignment, typed Ship/Faction scheduler targets, and strict V1 faction JSON. It selects by route duration then ship ID, issues ordinary `TravelTo`, and keeps offscreen contact knowledge ship-local. Its V7 save persists the consequential state and migrates V6 with zero factions, null controllers, and explicitly ship-targeted work. V4 ship content remains the released baseline; M6A uses V5 content.

The Godot shell privately loads both strict catalogs and adapts V9 saves without adding faction UI. It exposes the minimal live Combat workspace, four-consumer Engineering, and heading/speed Apply and Stop through existing Core commands while retaining actor-safe projections: no target health, controller, affiliation, faction, or pending intent telemetry.

Implementation evidence: [FirstGameSetup](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [ShipState](../../src/AlterCourse.Core/Ships/ShipState.cs), [SimulationState](../../src/AlterCourse.Core/Gameplay/SimulationState.cs), [GameSimulation](../../src/AlterCourse.Core/Gameplay/GameSimulation.cs), [Core gameplay tests](../../tests/AlterCourse.Core.Tests/Gameplay/), and the [Engineering contract](engineering-and-combat.md).

## Milestones and release boundaries

Milestone 1 world/bootstrap and Milestone 2 active-world orders are implemented. Milestone 3A first observed contact and [Strategic Contact Reporting](strategic-contact-reporting.md) are implemented, but the roadmap explicitly does not declare all of Milestone 3 complete. Milestone 4 Engineering Backbone is implemented. M3A and M4 are included in v0.4.0; Strategic Contact Reporting was delivered in Feature #77 / Final PR #78, merged into `dev` as `80c3084`, and included in v0.5.0.

Feature #86 is the first implemented M5 contribution, not evidence that M5 or M3 is complete. Observation-Driven Faction Response is the implemented second bounded M5 contribution. M6A first combat engagement is an unreleased, implemented contribution toward partial M6; M3 and M5 do not need to be declared complete first. Later M6 refinement and M7-M9 runtime remain future work. No canonical M3B milestone is admitted. Combat-driven Engineering remains the development direction, now preceded by the selected ADR 0014 conformance migration; detailed recovery gameplay is not approved by that selection.

## Implemented bounded faction slice

[Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) is implemented in Feature #86 / Final PR #87. It provides the approved era-neutral root-faction proof primitives: deterministic candidate choice under commitment, idle directly controlled NPC-only application, narrow own-asset facts, typed bootstrap and scheduling, strict faction content V1, and V6→V7 migration without political invention.

Q-05 is resolved for that slice. Q-04, Q-06, Q-08, and Q-14 have only the documented scoped decisions; Q-02, Q-03, and broader intelligence/political questions remain open. Production and long-horizon scenarios cover the bounded slice; the Final PR records inherited verification evidence. V7 is not a release claim.

## Implemented observation response

[Observation-Driven Faction Response](observation-driven-faction-response.md) implements one direct NPC ship-to-direct-faction report channel and one bounded investigation response. It selects a 2,000 ms deterministic report delay, observer-local historical provenance, eight in-flight and sixteen retained reports per faction, one active investigation, a 60,000 ms freshness window, no RNG, no preemption, and non-inventive adjacent persistence.

The implementation provides faction received-report state, report-delivery scheduler work, observation-response posture, investigation commitments, and the V8 save extension. Three production and three long-horizon Core proofs establish the two-faction path and bounded continuation. Its historical gameplay/UI suite passed 67/67 and kept the player projection actor-safe. The V8 high-width graph fixture was 108,890,984 bytes compact; its source-derived conservative ceiling was 113,024,376 bytes, 21,193,352 bytes below the unchanged 128 MiB envelope. Current V9 bounds are separately documented in [content and persistence](content-assets-and-persistence.md#implemented-v9-combat-persistence); historical measurements are not current-format maxima.

## Implemented M6A combat

M6A composes local Current and Identified contacts, same-location/range legality, one generic directed-energy weapon, all-aspect shields, five damage targets, four competing power consumers, one repair, tactical movement, deterministic defensive response, and V9 continuation. Fire has a per-ship persisted absolute readiness time; rejection is atomic. An accepted shot uses `baseDamage × weaponCapability`, shields absorb `min(output, shieldCondition × shieldPowerSatisfaction)`, and any remaining damage reduces the selected system. Damage may force allocation, speed, scan, and repair reconciliation without inventing a hull pool, recharge, global encounter, random outcome, affiliation, or political hostility.

An eligible **nonplayer victim** can retain one future wake at `observed + 100 ms` when it has a Current local contact for the attacker. The player does not receive an autonomous defensive wake. The wake consumes that stimulus and explains Return Fire, Withdraw, and Hold from local tactical facts, capabilities, readiness, and time only; it chooses that fixed order and does not wait for cooldown. Events reveal only actor-safe qualitative facts.

The detailed formulas, forced transitions, initial allocations, and current recovery limits belong to [Engineering and combat](engineering-and-combat.md). Combat readiness and defensive state extend the existing ship aggregate; they are not a separate encounter simulation.

## Preview-only or absent

Detailed EPS networks, batteries, warp Engineering, life support, transporters, repair teams/queues, crew systems, fuel, inventory, and economy are absent. The actual repair model has one active repair, not the multi-team queue illustrated by earlier UI references. Generation is a damage target but is not repairable in M6A. Shield facings, automatic recharge, a hull system, ship destruction, other weapon families, global encounters, and random combat outcomes are absent.

Strategic long-range sensor simulation, affiliation/intent knowledge, organizations, governments, political hierarchies, treaties, espionage, layered jurisdiction, and general faction intelligence sharing are not implemented. Durable last-known strategic contact reporting is implemented ship/player-side; Observation-Driven Faction Response adds only its direct ship-to-faction reporting extension.

`KnownShipId`, cross-observer vessel correlation, organization controllers, narrative runtime, a database, ECS, and a public mod API remain absent. Completed bounded slices do not admit these deferred systems.

## Verification evidence, not a fresh execution claim

### M6A admission

[Final PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112) records canonical verification on final source `5403a3d`: **789 Core tests, 324 AssetCtl tests, and 76 Godot tests in total—73 gameplay/UI, one integration, and two asset-import tests—plus smoke**, with zero Release/Debug build warnings or errors. Its documentation-only final candidate `42fba73` passed all five hosted checks, including [Canonical verification run 36250509274](https://github.com/L3DigitalNet/star-trek-alter-course/actions/runs/36250509274). [Post-merge run 36250769003](https://github.com/L3DigitalNet/star-trek-alter-course/actions/runs/36250769003) passed on `c2edae1`; PR #113 records the subsequent handoff checks.

The admission evidence includes the corrected Current-contact context validation, 255 simultaneous correlated NPC stimuli, populated V9 continuation, and a 512-attack long-horizon proof with 512 accepted defensive return fires. The [scenario](../../tests/AlterCourse.Core.Tests/Gameplay/M6CombatScenarioTests.cs), [horizon](../../tests/AlterCourse.Core.Tests/Gameplay/M6CombatLongHorizonTests.cs), and [V9](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV9CombatTests.cs) sources define the actual assertions. Native and cross-agent reviews recorded by the PR were static inspections, not additional test runs. Headless Godot checks establish input and focus behavior; manual device testing is not claimed.

These are inherited admission results. A later documentation audit must record its own scope and checks rather than present these numbers as newly executed tests. [PR #117](https://github.com/L3DigitalNet/star-trek-alter-course/pull/117) records the subsequent nonfatal `grab_focus` diagnostic; a green suite does not establish an error-free engine log.

### Historical release and consolidation evidence

The v0.6.0 release gate reports 598 Core tests, 324 AssetCtl tests, 67 gameplay/UI tests, one Godot integration test, and two generated-asset import tests, with warning-free builds. The historical v0.5.0 release candidate gate reported 406, 324, 63, one, and two; v0.4.0 and M4 reported 376, 324, 60, one, and two. Documentation PR #84 records its own actual checks separately and supplies no runtime faction-acceptance evidence.

The wiki-consolidation PR later verified the then-current `dev` state with 505 Core tests, 324 AssetCtl tests, 65 gameplay/UI tests, one Godot integration test, and two generated-asset import tests. That is inherited evidence for its implemented baseline, not execution evidence for Observation-Driven Faction Response or M6A.

[v0.6.0 release](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.6.0) · [v0.5.0 release](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.5.0) · [v0.4.0 release](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.4.0) · [M4 PR #63](https://github.com/L3DigitalNet/star-trek-alter-course/pull/63) · [Roadmap outcomes](../../ROADMAP.md)
