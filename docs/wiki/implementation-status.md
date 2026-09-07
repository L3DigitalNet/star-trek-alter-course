---
schema_version: '1.1'
id: 'reference-g40auc-implementation-status'
title: 'Implementation Status'
description: 'Reviewed gameplay baseline and explicit boundaries between runtime, previews, and future work.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
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
---

# Implementation status

[Wiki home](README.md) · [Sources](sources.md) · [Open questions](open-questions.md)

## Reviewed baseline

v0.5.0 is the immutable source-only release and uses V6 saves. Feature #86 / Final PR #87 implements the bounded faction slice with V7 on its review branch; it is not yet represented as merged `dev` truth or a new release. For the operational snapshot, consult [STATUS](../STATUS.md).

## Implemented gameplay

Core owns multiple ordinary persistent ships and an explicit `PlayerShipId`; the default world contains four ships across three strategic locations. The player ship is not a separate world-root entity type. Typed bootstrap distinguishes reusable design definitions from vessel names and starting condition.

Strategic travel has scheduled arrival. Durable `TravelTo`, `PatrolRoute`, and `HoldUntil` orders exist, with offscreen progression and long-horizon tests. Not every ship in the default contact proof has an active order; order machinery and the currently authored proof scenario are different things.

Local tactical space has continuous 2D positions and course/speed commands. Observer-local contacts support Current, Stale, Lost, reacquisition, active identification, and a typed hail acknowledgement. A bounded cautious-contact policy acts from its own knowledge rather than hidden target state.

Engineering owns generation condition, constrained allocation, sensor and impulse condition, derived capability, and one sensor or impulse repair per ship. Those values affect actual detection and tactical course limits. The live Engineering UI is not merely a preview.

The review-branch implementation also has root faction definitions and mutable presence objectives, ship-side optional direct `FactionId` control with a derived roster, pure own-asset assignment, typed Ship/Faction scheduler targets, and strict V1 faction JSON. It selects by route duration then ship ID, issues ordinary `TravelTo`, and keeps offscreen contact knowledge ship-local. Save V7 persists the consequential state and migrates V6 with zero factions, null controllers, and explicitly ship-targeted work. Ship-definition content V4 remains current.

The Godot shell privately loads both strict catalogs and adapts V7 saves without adding faction UI. Its focused 65-test compatibility suite verifies a production ship reaches Vesper after a midflight quick-load, root controllers retain a null player controller, malformed faction/controller data leaves the live shell usable, and ordinary UI hides faction names, vessels, and work.

Implementation evidence: [FirstGameSetup](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [ShipState](../../src/AlterCourse.Core/Ships/ShipState.cs), [SimulationState](../../src/AlterCourse.Core/Gameplay/SimulationState.cs), [GameSimulation](../../src/AlterCourse.Core/Gameplay/GameSimulation.cs), [Core gameplay tests](../../tests/AlterCourse.Core.Tests/Gameplay/), and the [Engineering design](../design/engineering-backbone.md).

## Milestones and release boundaries

Milestone 1 world/bootstrap and Milestone 2 active-world orders are implemented. Milestone 3A first observed contact and [Strategic Contact Reporting](strategic-contact-reporting.md) are implemented, but the roadmap explicitly does not declare all of Milestone 3 complete. Milestone 4 Engineering Backbone is implemented. M3A and M4 are included in v0.4.0; Strategic Contact Reporting was delivered in Feature #77 / Final PR #78, merged into `dev` as `80c3084`, and included in v0.5.0.

Feature #86 is the first implemented M5 contribution under review, not evidence that M5 or M3 is complete. The broader living-sector milestone and M6-M9 tactical combat, diplomacy/incidents, canon-anchored bootstrap, and regional campaign integration remain future work. No canonical M3B milestone is admitted.

## Implemented review-branch slice, pending landing

[Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) is implemented in Feature #86 / Final PR #87. It provides the approved era-neutral root-faction proof primitives: deterministic candidate choice under commitment, idle directly controlled NPC-only application, narrow own-asset facts, typed bootstrap and scheduling, strict faction content V1, and V6→V7 migration without political invention.

Q-05 is resolved for that slice. Q-04, Q-06, Q-08, and Q-14 have only the documented scoped decisions; Q-02, Q-03, and broader intelligence/political questions remain open. Production and long-horizon scenarios cover the bounded slice; the Final PR records verification evidence. This review-branch status is not a release or merge claim.

## Preview-only or absent

Combat mockups do not establish authoritative shields, weapons, fire-control solutions, or damage resolution. Detailed EPS networks, batteries, warp Engineering, life support, transporters, repair teams/queues, crew systems, fuel, inventory, and economy are absent. The actual repair model has one active repair, not the multi-team queue illustrated by earlier UI references.

Strategic long-range sensor simulation, affiliation/intent knowledge, organizations, governments, political hierarchies, treaties, espionage, and layered jurisdiction are not implemented; durable last-known strategic contact reporting is (see [Strategic Contact Reporting](strategic-contact-reporting.md)). The review-branch faction runtime does not change these future boundaries.

`KnownShipId`, organization controllers, narrative runtime, a database, ECS, and a public mod API remain absent and are not admitted by the slice.

## Verification evidence, not a fresh execution claim

The v0.5.0 release candidate gate reports 406 Core tests, 324 AssetCtl tests, 63 gameplay/UI tests, one Godot integration test, and two generated-asset import tests, with warning-free builds; the v0.4.0 release and M4 admission record reported 376, 324, 60, one, and two. These are baseline release results, not tests rerun merely by writing this page. Documentation PR #84 records its own actual checks separately and supplies no runtime faction-acceptance evidence.

[v0.5.0 release](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.5.0) · [v0.4.0 release](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.4.0) · [M4 PR #63](https://github.com/L3DigitalNet/star-trek-alter-course/pull/63) · [Roadmap outcomes](../../ROADMAP.md)
