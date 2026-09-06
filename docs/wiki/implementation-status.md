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
---

# Implementation status

[Wiki home](README.md) · [Sources](sources.md) · [Open questions](open-questions.md)

## Reviewed baseline

Reviewed September 6, 2026 against `dev` at `42481ca7fbc6c5c9da96985e02565f78a236cab7`. v0.4.0 is an immutable source-only release, not a packaged game. This documentation PR changes no gameplay, content schema, save schema, or dependencies. For the current operational snapshot after this review, consult [STATUS](../STATUS.md).

## Implemented gameplay

Core owns multiple ordinary persistent ships and an explicit `PlayerShipId`; the default world contains four ships across three strategic locations. The player ship is not a separate world-root entity type. Typed bootstrap distinguishes reusable design definitions from vessel names and starting condition.

Strategic travel has scheduled arrival. Durable `TravelTo`, `PatrolRoute`, and `HoldUntil` orders exist, with offscreen progression and long-horizon tests. Not every ship in the default contact proof has an active order; order machinery and the currently authored proof scenario are different things.

Local tactical space has continuous 2D positions and course/speed commands. Observer-local contacts support Current, Stale, Lost, reacquisition, active identification, and a typed hail acknowledgement. A bounded cautious-contact policy acts from its own knowledge rather than hidden target state.

Engineering owns generation condition, constrained allocation, sensor and impulse condition, derived capability, and one sensor or impulse repair per ship. Those values affect actual detection and tactical course limits. The live Engineering UI is not merely a preview.

The shell supports map/workspace switching, explicit pause and rates, player-relevant event advancement, and a quick-save/load slot. Save V5 retains the world and exact scheduler correlations; adjacent migration supports V1 through V5. Ship-definition content V4 is current.

Implementation evidence: [FirstGameSetup](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [ShipState](../../src/AlterCourse.Core/Ships/ShipState.cs), [SimulationState](../../src/AlterCourse.Core/Gameplay/SimulationState.cs), [GameSimulation](../../src/AlterCourse.Core/Gameplay/GameSimulation.cs), [Core gameplay tests](../../tests/AlterCourse.Core.Tests/Gameplay/), and the [Engineering design](../design/engineering-backbone.md).

## Milestones and release boundaries

Milestone 1 world/bootstrap and Milestone 2 active-world orders are implemented. Milestone 3A first observed contact is implemented, but the roadmap explicitly does not declare all of Milestone 3 complete. Milestone 4 Engineering Backbone is implemented. M3A and M4 are included in v0.4.0.

Milestones 5 through 9 remain future slices: living sector/faction autonomy, tactical combat, diplomacy/incidents, canon-anchored bootstrap, and regional campaign integration. The proposed M3B knowledge/faction-identity prerequisite is a discussion proposal, not an admitted milestone or implementation plan.

## Preview-only or absent

Combat mockups do not establish authoritative shields, weapons, fire-control solutions, or damage resolution. Detailed EPS networks, batteries, warp Engineering, life support, transporters, repair teams/queues, crew systems, fuel, inventory, and economy are absent. The actual repair model has one active repair, not the multi-team queue illustrated by earlier UI references.

Strategic sensor contacts, affiliation/intent knowledge, faction runtime state, organizations, governments, political hierarchies, treaties, espionage, and layered jurisdiction are not implemented. The [approved political model](factions-and-organizations.md) describes intended future behavior only.

No new `FactionId`, `KnownShipId`, organization controller, faction-owned scheduler target, narrative runtime, database, ECS, or public mod API is created by this wiki. Current scheduled work remains ship-targeted.

## Verification evidence, not a fresh execution claim

The v0.4.0 release and M4 admission record report 376 Core tests, 324 AssetCtl tests, 60 gameplay/UI tests, one Godot integration test, and two generated-asset import tests, with warning-free builds. These are baseline release results, not tests rerun merely by writing this page. The documentation PR records its own actual checks separately.

[Release evidence](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.4.0) · [M4 PR #63](https://github.com/L3DigitalNet/star-trek-alter-course/pull/63) · [Roadmap outcomes](../../ROADMAP.md)
