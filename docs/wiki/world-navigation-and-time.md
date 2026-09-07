---
schema_version: '1.1'
id: 'concept-zjt66i-world-navigation-and-time'
title: 'World Navigation and Time'
description: 'Persistent ship identity, map scales, orders, deterministic time, and scheduled consequences.'
doc_type: 'concept'
status: 'active'
created: '2026-09-06'
updated: '2026-09-07'
tags:
  - 'simulation'
  - 'architecture'
aliases: []
related:
  - 'docs/adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/milestone-proofs.md'
---

# World, navigation, and time

[Wiki home](README.md) · [Implementation status](implementation-status.md) · [Open questions](open-questions.md)

## Definition, instance, and starting state

A ship definition describes reusable design capability. A ship instance has stable identity, a name, runtime condition, location, motion, orders, and knowledge. Bootstrap declares initial circumstances, including activity already underway. These are separate responsibilities even where the production proof reuses one Pathfinder definition for all six vessels. The ship-catalog-only bootstrap overload retains the earlier four-ship, zero-faction proof.

`PlayerShipId` selects an ordinary ship. It does not make NPCs encounter props or place player-only state at the root of the world model. The current 256-ship bound protects prototype input and work budgets; it is not a final galaxy population target.

The default map has Dawn Anchor, Vesper Reach, and Meridian Drift. Pathfinder starts at Dawn Anchor with constrained generation and a sensor repair. Wayfarer is at Vesper Reach, Horizon is traveling toward Meridian Drift, and Kestrel is at Dawn Anchor with cautious contact behavior. The production faction proof adds Aurora and Resolute at Meridian Drift under Faction A's direct control, with Wayfarer controlled by Faction B. Proof route durations are short authored values, not a final warp-distance model.

## Strategic and tactical space

ADR 0004 permits regions/quadrants, sectors, systems, local spaces, and tactical space without requiring all scales now. A scale is not just camera zoom. Use stable identity and explicit containment/reference relationships while allowing each scale its own movement and visibility semantics.

Strategic space is a semantic graph with coordinates and metadata. Routes may eventually depend on distance, propulsion, hazards, access, treaties, and knowledge. Today the proof uses connected destinations and scheduled authored travel; full strategic pathfinding, free-space interception, and evolving border access are not implemented.

Tactical space uses continuous 2D position and motion. Domain coordinates are kilometers, positive Y toward tactical north; Godot converts to display coordinates. A displayed grid is not movement authority. Current ships carry tactical position/motion alongside strategic state, but traveling ships do not participate in local observations. Cross-scale knowledge and encounter lifecycle need later explicit design; this wiki does not invent a new positional schema.

## Orders versus physical activity

`TravelTo`, bounded cyclic `PatrolRoute`, and time-only `HoldUntil` are durable orders. Orders explain intent; physical strategic state records what is happening; correlated scheduled work controls the next consequence. Player travel and NPC progression reuse targetable Core travel behavior. Starting an order validates its target and current physical state before it creates the next work item; an order is therefore neither a permission to bypass a missing route nor a second travel state.

Canceling an order does not teleport or silently abort a physical voyage already underway. Cancellation removes only the correlated work it actually owns. Existing tests cover offscreen progression, cancellation, save/load, and insertion-order independence, including a 72-hour M2 scenario. Faction assignment has the narrower authority to start a valid ordinary order for an idle directly controlled NPC; it cannot use cancellation to preempt an order, a player command, or a voyage already in progress.

The implemented [faction-assignment slice](faction-intent-and-autonomous-assignment.md) reuses these order/application paths. It assigns only idle, directly controlled NPC ships; application revalidates that boundary before creating ordinary `TravelTo`. Existing order cancellation is not permission for faction AI to preempt an assignment or interrupt a voyage. The player ship remains outside faction autonomous control in the proof.

## One timeline, several update rates

Core time advances only through explicit operations. A pause submits no advancement; wall-clock time, rendering delays, and time spent with the process closed do not move the universe. Tactical/contact-sensitive work uses deterministic 100 ms boundaries. Strategic-only intervals can advance event-to-event, and repair state is analytically materialized at relevant boundaries.

The scheduler has finite known work kinds, stable work IDs, persisted same-time ordering, exact cancellation/correlation, and bounded processing. It has closed Ship/Faction targets: each item contains exactly one initialized identity matching its target kind, and each known work kind accepts only its intended target domain. Faction decision wakes require their exact faction identity and correlation, while existing work retains ship meaning. A malformed or mismatched target/kind fails validation rather than being reinterpreted.

The persisted scheduler capacity is `MaximumShips * (MaximumShips - 1 + 5) + MaximumFactions`: at the current 256 ship and 256 faction bounds, that is 66,816 outstanding items. The five per-ship allowance covers independently correlated travel, repair, order, scan, and decision work in addition to one possible contact-loss item for every other ship; each faction may retain one decision wake. This is an admission bound for a valid aggregate, not a promise that normal play creates that population.

D-11's implementation preserves existing ship-work meaning, total same-time sequence, exact correlation, typed target validation, and budgets. Faction decisions wake initially, at arrival, report delivery, or a future hold/travel release boundary; they do not poll at tactical frequency. A pending objective or report response is dormant only when it has no eligible candidate and no release boundary. Typed bootstrap creates a complete valid aggregate, including faction state and initial work. It schedules the ship's existing work before the faction's decision work, so a wake observes the already-established order/travel state instead of a partially initialized world. Observation-Driven Faction Response coalesces same-time report delivery with one deterministic faction evaluation and retains finite execution guards.

`AdvanceUntilNextPlayerRelevantEvent` processes hidden NPC work but does not report it merely because it occurred. Player-safe events include their actual simulation occurrence times rather than inheriting the final time of a batch. Implemented faction work follows that boundary and is not automatically player-relevant. A no-action faction wake either schedules one bounded future release boundary or leaves the objective dormant; it cannot spin at the same simulation time to manufacture progress.

## Randomness and future scale

The present contact/order proofs do not need random decisions. D-13 explicitly keeps the first faction policy non-stochastic as well. ADR 0007 requires a versioned, restorable, injected random source and stable stream ownership when a real consumer appears; a seed alone is not a continuation contract. The eventual algorithm remains open.

Do not solve scale by simulating every offscreen actor at tactical frequency, inventing distributed services, or relaxing determinism. First identify the required behavior, choose the coarsest faithful update resolution, and measure representative scenarios.

## Sources

[Spatial ADR](../adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md), [time ADR](../adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md), [M1/M2 proof record](milestone-proofs.md), [bootstrap](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [scheduled work](../../src/AlterCourse.Core/Simulation/ScheduledWork.cs), [scheduler](../../src/AlterCourse.Core/Simulation/SimulationScheduler.cs), [order execution tests](../../tests/AlterCourse.Core.Tests/Gameplay/OrderExecutionTests.cs), [bootstrap ordering tests](../../tests/AlterCourse.Core.Tests/Gameplay/GameBootstrapOrderTests.cs), and [faction wake tests](../../tests/AlterCourse.Core.Tests/Gameplay/FactionRuntimeWakeTests.cs).
