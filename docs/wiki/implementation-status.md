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

Reviewed September 6, 2026 against `dev` at `d4072d632c8906fc4071abbf70e77681bbd5f339`; gameplay is unchanged from Strategic Contact Reporting merge `80c308483fafb3b6d42de8f3a3382bec1ad7158b`. v0.5.0 is an immutable source-only release, not a packaged game; v0.4.0 is its predecessor. The next-slice documentation changes no gameplay, content schema, save schema, or dependencies. Consult [STATUS](../STATUS.md) for the operational snapshot.

## Implemented gameplay

Core owns multiple ordinary persistent ships and an explicit `PlayerShipId`; the default world contains four ships across three strategic locations. The player ship is not a separate world-root entity type. Typed bootstrap distinguishes reusable design definitions from vessel names and starting condition.

Strategic travel has scheduled arrival. Durable `TravelTo`, `PatrolRoute`, and `HoldUntil` orders exist, with offscreen progression and long-horizon tests. Not every ship in the default contact proof has an active order; order machinery and the currently authored proof scenario are different things.

Local tactical space has continuous 2D positions and course/speed commands. Observer-local contacts support Current, Stale, Lost, reacquisition, active identification, and a typed hail acknowledgement. A bounded cautious-contact policy acts from its own knowledge rather than hidden target state.

Engineering owns generation condition, constrained allocation, sensor and impulse condition, derived capability, and one sensor or impulse repair per ship. Those values affect actual detection and tactical course limits. The live Engineering UI is not merely a preview.

The shell supports map/workspace switching, explicit pause and rates, player-relevant event advancement, and a quick-save/load slot. Save V6 retains the world and exact scheduler correlations, including sensor knowledge and observation frames from which `KnownContactReports` is derived, under rules identity `strategic-contact-reporting-v1`. Adjacent migration supports V1 through V6. Ship-definition content V4 is current; neither a V7 loader nor faction content exists yet.

Implementation evidence: [FirstGameSetup](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [ShipState](../../src/AlterCourse.Core/Ships/ShipState.cs), [SimulationState](../../src/AlterCourse.Core/Gameplay/SimulationState.cs), [GameSimulation](../../src/AlterCourse.Core/Gameplay/GameSimulation.cs), [Core gameplay tests](../../tests/AlterCourse.Core.Tests/Gameplay/), and the [Engineering design](../design/engineering-backbone.md).

## Milestones and release boundaries

Milestone 1 world/bootstrap and Milestone 2 active-world orders are implemented. Milestone 3A first observed contact and [Strategic Contact Reporting](strategic-contact-reporting.md) are implemented, but the roadmap explicitly does not declare all of Milestone 3 complete. Milestone 4 Engineering Backbone is implemented. M3A and M4 are included in v0.4.0; Strategic Contact Reporting is delivered in Feature #77 / Final PR #78, merged into `dev` as `80c3084`, and included in v0.5.0.

Milestones 5 through 9 remain unimplemented: living sector/faction autonomy, tactical combat, diplomacy/incidents, canon-anchored bootstrap, and regional campaign integration. The owner has selected the first bounded M5 design below; that does not start or complete its runtime implementation. The former proposed M3B global-identity/faction-sharing prerequisite remains unapproved.

## Approved next slice, not implemented

[Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) is the next owner-approved design after v0.5.0. It selects an era-neutral two-root-faction proof: explainable choice among directly controlled idle NPC ships, ordinary offscreen assignment/travel, and an NPC-NPC sensor/contact consequence. It does not claim to complete all of M5.

The approved boundaries are an asset-side optional direct controlling `FactionId`, no order/travel preemption or player autonomous assignment, narrow current own-asset administrative knowledge rather than sensor sharing, a closed Ship/Faction scheduler target, typed political bootstrap, planned V7 migration that invents no factions/control/history, and no randomness or faction/affiliation UI.

Q-05 is resolved for this bounded proof. Q-04 and Q-14 have scoped answers; the campaign era remains open despite an era-neutral fixture choice. Q-02/Q-03 and broader intelligence/political systems remain deferred. No runtime type, test count, schema, or release status is advanced by this approval.

## Preview-only or absent

Combat mockups do not establish authoritative shields, weapons, fire-control solutions, or damage resolution. Detailed EPS networks, batteries, warp Engineering, life support, transporters, repair teams/queues, crew systems, fuel, inventory, and economy are absent. The actual repair model has one active repair, not the multi-team queue illustrated by earlier UI references.

Strategic long-range sensor simulation, affiliation/intent knowledge, faction runtime state, organizations, governments, political hierarchies, treaties, espionage, and layered jurisdiction are not implemented; durable last-known strategic contact reporting is (see [Strategic Contact Reporting](strategic-contact-reporting.md)). The [political framework](factions-and-organizations.md) and its first selected consumer describe approved future behavior only.

No new `FactionId`, `KnownShipId`, controller field, faction-owned scheduler target, narrative runtime, database, ECS, or public mod API is created by this documentation. Current scheduled work remains ship-targeted.

## Verification evidence, not a fresh execution claim

The v0.5.0 release candidate gate reports 406 Core tests, 324 AssetCtl tests, 63 gameplay/UI tests, one Godot integration test, and two generated-asset import tests, with warning-free builds; the v0.4.0 release and M4 admission record reported 376, 324, 60, one, and two. These are baseline release results, not tests rerun merely by writing this page. The documentation PR records its own actual checks separately.

[v0.5.0 release](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.5.0) · [v0.4.0 release](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.4.0) · [M4 PR #63](https://github.com/L3DigitalNet/star-trek-alter-course/pull/63) · [Roadmap outcomes](../../ROADMAP.md)
