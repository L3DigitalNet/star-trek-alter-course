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

The gameplay baseline was reviewed September 6, 2026 at `80c308483fafb3b6d42de8f3a3382bec1ad7158b`; the subsequent v0.5.0 release and documentation closeout are on `dev` at `d4072d632c8906fc4071abbf70e77681bbd5f339`. v0.5.0 is an immutable source-only release, not a packaged game; v0.4.0 is its predecessor. The faction-assignment approval records design only and changes no gameplay, content schema, save schema, or dependencies. For the operational snapshot, consult [STATUS](../STATUS.md).

## Implemented gameplay

Core owns multiple ordinary persistent ships and an explicit `PlayerShipId`; the default world contains four ships across three strategic locations. The player ship is not a separate world-root entity type. Typed bootstrap distinguishes reusable design definitions from vessel names and starting condition.

Strategic travel has scheduled arrival. Durable `TravelTo`, `PatrolRoute`, and `HoldUntil` orders exist, with offscreen progression and long-horizon tests. Not every ship in the default contact proof has an active order; order machinery and the currently authored proof scenario are different things.

Local tactical space has continuous 2D positions and course/speed commands. Observer-local contacts support Current, Stale, Lost, reacquisition, active identification, and a typed hail acknowledgement. A bounded cautious-contact policy acts from its own knowledge rather than hidden target state.

Engineering owns generation condition, constrained allocation, sensor and impulse condition, derived capability, and one sensor or impulse repair per ship. Those values affect actual detection and tactical course limits. The live Engineering UI is not merely a preview.

The shell supports map/workspace switching, explicit pause and rates, player-relevant event advancement, and a quick-save/load slot. Save V6 retains the world, exact scheduler correlations, and reference-frame-qualified contact knowledge under rules identity `strategic-contact-reporting-v1`. `KnownContactReports` is derived from retained knowledge rather than separately serialized projection state. Adjacent migration supports V1 through V6. Ship-definition content V4 is current.

Implementation evidence: [FirstGameSetup](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [ShipState](../../src/AlterCourse.Core/Ships/ShipState.cs), [SimulationState](../../src/AlterCourse.Core/Gameplay/SimulationState.cs), [GameSimulation](../../src/AlterCourse.Core/Gameplay/GameSimulation.cs), [Core gameplay tests](../../tests/AlterCourse.Core.Tests/Gameplay/), and the [Engineering design](../design/engineering-backbone.md).

## Milestones and release boundaries

Milestone 1 world/bootstrap and Milestone 2 active-world orders are implemented. Milestone 3A first observed contact and [Strategic Contact Reporting](strategic-contact-reporting.md) are implemented, but the roadmap explicitly does not declare all of Milestone 3 complete. Milestone 4 Engineering Backbone is implemented. M3A and M4 are included in v0.4.0; Strategic Contact Reporting was delivered in Feature #77 / Final PR #78, merged into `dev` as `80c3084`, and included in v0.5.0.

Milestone 5 has a selected first bounded slice, described below, but no runtime implementation has started. The broader living-sector milestone and M6-M9 tactical combat, diplomacy/incidents, canon-anchored bootstrap, and regional campaign integration remain future work. No canonical M3B milestone is admitted.

## Approved next slice, not implemented

[Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) is owner-approved through [documentation PR #84](https://github.com/L3DigitalNet/star-trek-alter-course/pull/84). It specifies an era-neutral two-root-faction proof, real candidate choice under an existing-commitment constraint, direct control of idle NPC ships, narrow own-asset administrative knowledge, closed Ship/Faction scheduled targets, typed bootstrap, a planned non-inventive V7 migration, and no randomness or political UI.

Q-05 is resolved for that slice. Q-04, Q-06, Q-08, and Q-14 have only the documented scoped decisions; Q-02, Q-03, and broader intelligence/political questions remain open. None of these approval labels is a claim that a faction or V7 save currently exists. The later implementation requires its own governed feature, tests, and admission evidence.

## Preview-only or absent

Combat mockups do not establish authoritative shields, weapons, fire-control solutions, or damage resolution. Detailed EPS networks, batteries, warp Engineering, life support, transporters, repair teams/queues, crew systems, fuel, inventory, and economy are absent. The actual repair model has one active repair, not the multi-team queue illustrated by earlier UI references.

Strategic long-range sensor simulation, affiliation/intent knowledge, faction runtime state, organizations, governments, political hierarchies, treaties, espionage, and layered jurisdiction are not implemented; durable last-known strategic contact reporting is (see [Strategic Contact Reporting](strategic-contact-reporting.md)). The [approved political model](factions-and-organizations.md) describes intended future behavior only.

The assignment documentation creates no runtime `FactionId`, controller link, faction-owned scheduler target, or V7 model. Current scheduled work remains ship-targeted. `KnownShipId`, organization controllers, narrative runtime, a database, ECS, and a public mod API remain absent and are not admitted by the slice.

## Verification evidence, not a fresh execution claim

The v0.5.0 release candidate gate reports 406 Core tests, 324 AssetCtl tests, 63 gameplay/UI tests, one Godot integration test, and two generated-asset import tests, with warning-free builds; the v0.4.0 release and M4 admission record reported 376, 324, 60, one, and two. These are baseline release results, not tests rerun merely by writing this page. Documentation PR #84 records its own actual checks separately and supplies no runtime faction-acceptance evidence.

[v0.5.0 release](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.5.0) · [v0.4.0 release](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.4.0) · [M4 PR #63](https://github.com/L3DigitalNet/star-trek-alter-course/pull/63) · [Roadmap outcomes](../../ROADMAP.md)
