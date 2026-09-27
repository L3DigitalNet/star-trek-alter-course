---
schema_version: '1.1'
id: 'concept-zjt66i-world-navigation-and-time'
title: 'World Navigation and Time'
description: 'Persistent ship identity, map scales, orders, deterministic time, and scheduled consequences.'
doc_type: 'concept'
status: 'active'
created: '2026-09-06'
updated: '2026-09-27'
tags:
  - 'simulation'
  - 'architecture'
aliases: []
related:
  - 'docs/adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/milestone-proofs.md'
---

# World, navigation, and time

[Wiki home](README.md) · [Implementation status](implementation-status.md) · [Open questions](open-questions.md)

## Definition, instance, and starting state

A ship definition describes reusable design capability. A ship instance has stable identity, a name, runtime condition, location, motion, orders, and knowledge. Bootstrap declares initial circumstances, including activity already underway. These responsibilities remain separate even where the production proof reuses one Pathfinder definition for all six vessels. The ship-catalog-only bootstrap overload retains the earlier four-ship, zero-faction proof.

`PlayerShipId` selects an ordinary ship. It does not make NPCs encounter props or place player-only state at the root of the world model. The current 256-ship bound protects prototype input and work budgets; it is not a final galaxy population target.

The default map has Dawn Anchor, Vesper Reach, and Meridian Drift. Pathfinder starts at Dawn Anchor with constrained generation and a sensor repair. Wayfarer is at Vesper Reach, Horizon travels toward Meridian Drift, and Kestrel is at Dawn Anchor with cautious contact behavior. The production faction proof adds Aurora and Resolute at Meridian Drift under Faction A; Wayfarer and Horizon are directly controlled by Faction B. Short authored route durations are proof values, not a final warp-distance model. [Engineering and combat](engineering-and-combat.md#power-and-capability) owns initial combat conditions and allocations.

## Strategic and tactical space

ADR 0004 permits regions/quadrants, sectors, systems, local spaces, and tactical space without requiring every scale now. A scale is not merely camera zoom. Stable identities and explicit containment/reference relationships allow different movement and visibility semantics at each scale.

Strategic space is a semantic graph with coordinates and metadata. Routes may eventually depend on distance, propulsion, hazards, access, treaties, and knowledge. The current proof uses connected destinations and scheduled authored travel; full strategic pathfinding, free-space interception, and evolving border access are not implemented.

Tactical space uses continuous 2D position and motion. Domain coordinates are kilometers, with positive Y toward tactical north; Godot converts to display coordinates. A displayed grid is not movement authority. Ships carry tactical position/motion alongside strategic state, but traveling ships do not participate in local observations. Broader cross-scale knowledge and encounter lifecycle require later explicit design.

## Orders versus physical activity

`TravelTo`, bounded cyclic `PatrolRoute`, and time-only `HoldUntil` are durable orders. Orders explain intent; physical strategic state records activity; correlated work controls the next consequence. Player travel and NPC progression reuse targetable Core travel behavior. Starting an order validates its target and physical state before creating work; an order neither bypasses a missing route nor creates a second travel state.

Canceling an order does not teleport or silently abort a voyage already underway. Cancellation removes only the correlated work it owns. Tests cover offscreen progression, cancellation, save/load, and insertion-order independence, including the 72-hour M2 scenario. This existing cancellation mechanism does not grant faction AI preemption authority.

The [faction-assignment slice](faction-intent-and-autonomous-assignment.md) reuses ordinary order/application paths for idle, directly controlled NPC ships. Application revalidates authority and prerequisites before creating `TravelTo`. It cannot override a player command, an order, or a voyage in progress; the player remains outside faction autonomous control in this proof.

## One timeline, several update rates

Core time advances only through explicit operations. Pause submits no advancement; wall-clock time, rendering delays, and time with the process closed do not move the universe. Tactical/contact-sensitive work uses deterministic 100 ms boundaries. Strategic-only intervals can advance event-to-event, and repair is analytically materialized at relevant boundaries.

[ADR 0015](../adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md) owns staged command/advancement commitment. The current public advancement paths install a validated candidate only after successful processing; a candidate budget failure does not commit intermediate time, IDs, work, or events. Stopping successfully at a player-relevant boundary is not a failure. ADR 0007 still permits a separately specified safely incremental operation with explicit partial-progress semantics; this page does not introduce one.

Post-commit diagnostics may record allowlisted facts with simulation time and stable IDs. They neither schedule work nor become snapshot data, and logger failure does not change a committed result.

The scheduler uses finite known work kinds, stable IDs, persisted same-time ordering, exact cancellation/correlation, and bounded processing. Its closed Ship/Faction targets contain exactly one initialized identity matching the target kind; each work kind accepts only its intended domain. Faction wakes require exact faction identity and correlation. A malformed target/kind fails validation rather than being reinterpreted.

Current V10 capacity is `MaximumShips * (MaximumShips - 1 + 6) + MaximumFactions * (1 + MaximumInFlightReports)`: at 256 ships and 256 factions, **69,120 outstanding items**, unchanged from V9. The six per-ship allowances cover travel, repair, order, scan, contact-decision, and combat-decision work, plus one possible contact-loss item per other ship. Repair and scan remain at most one per ship, so the installed-system migration adds no work kind. Each faction permits one decision wake and up to eight in-flight deliveries.

That capacity is a conservative admission bound, not a promise that every slot can coexist or normal play creates that population. The V8 ceiling was 68,864 before combat-decision allowance. [Persistence](content-assets-and-persistence.md) distinguishes historical bounds from current proof measurements.

D-11 preserves ship-work meaning, total same-time sequence, exact correlations, typed validation, and budgets. Faction decisions wake initially, at arrival, at report delivery, or at a future hold/travel release boundary; they do not poll at tactical frequency. A pending objective or report response is dormant only when no eligible candidate or justified release boundary exists.

Typed bootstrap constructs a complete valid aggregate, including initial work. It schedules existing ship work before faction decisions so a wake observes established order/travel state, not partial initialization. Observation-Driven Faction Response coalesces same-time delivery with one deterministic faction evaluation and finite execution guards.

`AdvanceUntilNextPlayerRelevantEvent` processes hidden NPC work without reporting it merely because it occurred. Player-safe events carry actual occurrence times, not the final batch time. Faction work is not automatically player-relevant. A no-action wake schedules a justified finite release boundary or leaves the objective dormant; it cannot spin at the same instant to manufacture progress.

## Randomness and future scale

The current contact/order proofs, both faction policies, and M6A use no authoritative randomness. ADR 0007 requires a versioned, restorable, injected source and stable stream ownership when a real consumer appears. A seed alone is not a continuation contract; the eventual algorithm remains open.

M6A permits at most one delayed `ShipCombatDecisionWake` per eligible NPC. Its stimulus is due 100 ms after a legitimate accepted shot and retains exact correlation; the first pending stimulus wins. A cooldown wait creates no additional wake. Readiness is an absolute persisted simulation time, not periodic work. Player-relevant advancement returns actual player-owned hit/reconciliation events without exposing pending NPC intent.

Do not solve scale by updating every offscreen actor at tactical frequency, distributing simulation into services, or relaxing determinism. Identify the required behavior, choose the coarsest faithful resolution, and measure representative scenarios.

## Sources

[Spatial ADR](../adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md), [time ADR](../adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md), [command ADR](../adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md), [M1/M2 proofs](milestone-proofs.md), [bootstrap](../../src/AlterCourse.Core/Gameplay/FirstGameSetup.cs), [scheduled work](../../src/AlterCourse.Core/Simulation/ScheduledWork.cs), [scheduler](../../src/AlterCourse.Core/Simulation/SimulationScheduler.cs), [order tests](../../tests/AlterCourse.Core.Tests/Gameplay/OrderExecutionTests.cs), [bootstrap ordering tests](../../tests/AlterCourse.Core.Tests/Gameplay/GameBootstrapOrderTests.cs), and [faction wake tests](../../tests/AlterCourse.Core.Tests/Gameplay/FactionRuntimeWakeTests.cs).
