---
schema_version: '1.1'
id: 'concept-zjt66i-world-navigation-and-time'
title: 'World Navigation and Time'
description: 'Persistent ship identity, map scales, orders, deterministic time, and scheduled consequences.'
doc_type: 'concept'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'simulation'
  - 'architecture'
aliases: []
related:
  - 'docs/adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'ROADMAP.md'
---

# World, navigation, and time

[Wiki home](README.md) · [Implementation status](implementation-status.md) · [Open questions](open-questions.md)

## Definition, instance, and starting state

A ship definition describes reusable design capability. A ship instance has stable identity, a name, runtime condition, location, motion, orders, and knowledge. Bootstrap declares initial circumstances, including activity already underway. These are separate responsibilities even where the current proof reuses one Pathfinder definition for all four vessels.

`PlayerShipId` selects an ordinary ship. It does not make NPCs encounter props or place player-only state at the root of the world model. The current 256-ship bound protects prototype input and work budgets; it is not a final galaxy population target.

The default map has Dawn Anchor, Vesper Reach, and Meridian Drift. Pathfinder starts at Dawn Anchor with constrained generation and a sensor repair. Wayfarer is at Vesper Reach, Horizon is traveling toward Meridian Drift, and Kestrel is at Dawn Anchor with cautious contact behavior. Proof route durations are short authored values, not a final warp-distance model.

## Strategic and tactical space

ADR 0004 permits regions/quadrants, sectors, systems, local spaces, and tactical space without requiring all scales now. A scale is not just camera zoom. Use stable identity and explicit containment/reference relationships while allowing each scale its own movement and visibility semantics.

Strategic space is a semantic graph with coordinates and metadata. Routes may eventually depend on distance, propulsion, hazards, access, treaties, and knowledge. Today the proof uses connected destinations and scheduled authored travel; full strategic pathfinding, free-space interception, and evolving border access are not implemented.

Tactical space uses continuous 2D position and motion. Domain coordinates are kilometers, positive Y toward tactical north; Godot converts to display coordinates. A displayed grid is not movement authority. Current ships carry tactical position/motion alongside strategic state, but traveling ships do not participate in local observations. Cross-scale knowledge and encounter lifecycle need later explicit design; this wiki does not invent a new positional schema.

## Orders versus physical activity

`TravelTo`, bounded cyclic `PatrolRoute`, and time-only `HoldUntil` are durable orders. Orders explain intent; physical strategic state records what is happening; correlated scheduled work controls the next consequence. Player travel and NPC progression reuse targetable Core travel behavior.

Canceling an order does not teleport or silently abort a physical voyage already underway. Cancellation removes only the correlated work it actually owns. Existing tests cover offscreen progression, cancellation, save/load, and insertion-order independence, including a 72-hour M2 scenario.

The approved [first faction-assignment slice](faction-intent-and-autonomous-assignment.md) will reuse these order/application paths. It may assign idle, directly controlled NPC ships only; existing order cancellation is not permission for faction AI to preempt an assignment or interrupt a voyage. The player ship remains outside faction autonomous control in the proof. This is approved design, not implemented behavior.

## One timeline, several update rates

Core time advances only through explicit operations. A pause submits no advancement; wall-clock time, rendering delays, and time spent with the process closed do not move the universe. Tactical/contact-sensitive work uses deterministic 100 ms boundaries. Strategic-only intervals can advance event-to-event, and repair state is analytically materialized at relevant boundaries.

The scheduler has finite known work kinds, stable work IDs, persisted same-time ordering, exact cancellation/correlation, and bounded processing. Current kinds are travel arrival, system repair completion, order wake, contact loss, scan completion, and ship contact decision wake. Current work targets a ship; faction-owned scheduling remains unimplemented.

D-11 now approves closed Ship/Faction targets for the first faction slice, not a generic actor registry or another scheduler. That implementation must preserve existing ship-work meaning, total same-time sequence, exact correlation, typed target validation, and budgets. Faction decision wakes use meaningful strategic boundaries rather than tactical-frequency faction polling. Typed bootstrap must create a complete valid initial aggregate, including faction state and initial work.

`AdvanceUntilNextPlayerRelevantEvent` processes hidden NPC work but does not report it merely because it occurred. Player-safe events include their actual simulation occurrence times rather than inheriting the final time of a batch. Future faction work must retain that boundary; it is not automatically a player-relevant event.

## Randomness and future scale

The present contact/order proofs do not need random decisions. D-13 explicitly keeps the first faction policy non-stochastic as well. ADR 0007 requires a versioned, restorable, injected random source and stable stream ownership when a real consumer appears; a seed alone is not a continuation contract. The eventual algorithm remains open.

Do not solve scale by simulating every offscreen actor at tactical frequency, inventing distributed services, or relaxing determinism. First identify the required behavior, choose the coarsest faithful update resolution, and measure representative scenarios.

## Sources

[Spatial ADR](../adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md), [time ADR](../adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md), [roadmap M1/M2](../../ROADMAP.md), [bootstrap](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [scheduled work](../../src/AlterCourse.Core/Simulation/ScheduledWork.cs), and [work kinds](../../src/AlterCourse.Core/Simulation/ScheduledWorkKind.cs).
