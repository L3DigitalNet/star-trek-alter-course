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

The approved [faction-assignment slice](faction-intent-and-autonomous-assignment.md) will reuse these commands and execution paths. A faction may assign only an eligible idle NPC it directly controls; it cannot preempt an order, interrupt existing physical travel, or autonomously assign the player ship. Existing cancellation support does not authorize faction order preemption in this slice.

## One timeline, several update rates

Core time advances only through explicit operations. A pause submits no advancement; wall-clock time, rendering delays, and time spent with the process closed do not move the universe. Tactical/contact-sensitive work uses deterministic 100 ms boundaries. Strategic-only intervals can advance event-to-event, and repair state is analytically materialized at relevant boundaries.

The scheduler has finite known work kinds, stable work IDs, persisted same-time ordering, exact cancellation/correlation, and bounded processing. Current kinds are travel arrival, system repair completion, order wake, contact loss, scan completion, and ship contact decision wake. Current work targets a ship; faction-owned scheduling remains unimplemented.

`AdvanceUntilNextPlayerRelevantEvent` processes hidden NPC work but does not report it merely because it occurred. Player-safe events include their actual simulation occurrence times rather than inheriting the final time of a batch.

## Approved faction scheduling and bootstrap extension

The first faction-assignment consumer approves a closed typed `Ship | Faction` scheduled-work target under ADR 0007. The target representation must distinguish the identity domains even when underlying numeric IDs match, preserve existing work order/correlation, and reject wrong-kind or dangling targets. It does not require an Actor/Entity abstraction or another scheduler.

Faction decisions wake at meaningful bounded simulation boundaries, not every tactical tick. The implementation must define initial and follow-on wakes, no-action behavior, deduplication, and bounded reassessment so it cannot loop at the same time or repeatedly replace commitments. Exact cadence and budgets remain implementation choices within the owning decision's tests.

New faction starts, asset control links, and initial wakes enter through typed bootstrap and complete aggregate validation, not postconstruction political mutations. A zero-faction world stays valid, including migrated V6 saves. These are approved next-slice requirements only; current bootstrap and save V6 remain unchanged by documentation.

## Randomness and future scale

The present contact/order proofs do not need random decisions. The approved first faction policy also consumes no randomness. ADR 0007 requires a versioned, restorable, injected random source and stable stream ownership when a real consumer appears; a seed alone is not a continuation contract. This slice does not select an algorithm or add random streams to saves.

Do not solve scale by simulating every offscreen actor at tactical frequency, inventing distributed services, or relaxing determinism. First identify the required behavior, choose the coarsest faithful update resolution, and measure representative scenarios.

## Sources

[Spatial ADR](../adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md), [time ADR](../adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md), [faction assignment decision](faction-intent-and-autonomous-assignment.md), [roadmap M1/M2](../../ROADMAP.md), [bootstrap](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [scheduled work](../../src/AlterCourse.Core/Simulation/ScheduledWork.cs), and [work kinds](../../src/AlterCourse.Core/Simulation/ScheduledWorkKind.cs).
