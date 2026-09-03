---
schema_version: '1.1'
id: 'concept-rkv6hr-first-observed-contact'
title: 'First Observed Contact'
description: 'Defines the implemented actor-specific sensor knowledge boundary for the first noncombat contact slice.'
doc_type: 'concept'
status: 'active'
created: '2026-09-03'
updated: '2026-09-03'
tags:
  - 'architecture'
  - 'sensors'
  - 'simulation'
aliases: []
related:
  - 'ROADMAP.md'
  - 'docs/adr/0001-core-domain-and-godot-presentation-boundary.md'
  - 'docs/adr/0006-versioned-save-snapshots-and-validated-migration.md'
  - 'docs/adr/0007-deterministic-time-and-event-scheduling.md'
  - 'docs/adr/0010-explainable-ai-from-actor-appropriate-information.md'
---

# First Observed Contact

Milestone 3A establishes the first durable, noncombat boundary between world truth and what a ship knows. It is deliberately a small vertical slice: a damaged player ship and a full-integrity cautious vessel can have different knowledge of the same local encounter, scan and hail through Core commands, and retain their consequential state through save/load.

## Authority and scope

`AlterCourse.Core` owns ships, tactical positions, sensor condition, contacts, scanning, hailing, autonomous decisions, scheduling, and persistence. `AlterCourse.Godot` receives player-known projections, renders and hit-tests them, retains only selected-contact presentation state, and translates typed user intent. It neither discovers ships from aggregate state nor calculates detection.

This slice has local tactical scope only. A passive observation is possible only when distinct observer and target ships are both at the same strategic `LocationId`. Traveling ships neither observe nor appear as local contacts. The rule uses an authored passive range multiplied by the observer's current sensor integrity; it compares that effective range with the Euclidean tactical distance in kilometers. It uses no randomness.

## Contact knowledge and lifecycle

Each `ShipState` owns bounded, canonically ordered sensor knowledge and a monotonically allocated `SensorContactId`. The ID belongs to the observer, is not derived from a `ShipInstanceId`, and remains stable through stale, lost, and reacquired states. Core retains a correlated target ship ID only to resolve authoritative rules and commands. That correlation never appears in player projections, Godot presentation, player-safe events, or AI decision inputs.

A contact exposes only its observer-known facts: local contact ID, last observed tactical position and time, Current/Stale/Lost status, Detected/Identified state, and vessel/design display names only after identification. A new detectable contact is Current and Detected. Continued detection refreshes its observed position and time. When it is no longer detectable, it becomes Stale, keeps its last observation, and receives exactly one loss work item due five seconds later. Resolution rechecks detectability: a still-undetectable Stale contact becomes Lost; reacquisition cancels its exact loss work, restores Current, and preserves its local ID and prior identification. Lost contacts remain bounded correlation memory but are absent from live tactical projection.

## Active scan and hail

One observer can own at most one active scan. A scan targets a current detected local contact and schedules completion using the ship definition's active-scan duration. Completion revalidates that the contact remains Current, then identifies it and records the target's vessel and design display names. If the contact becomes stale or lost first, Core cancels the exact completion work, clears the operation, and emits an interrupted result.

Hail is an immediate typed Core request for an identified current player contact. The player presents its vessel and design identity to the internally correlated target. A target without the focused posture or a valid reciprocal current contact returns no response. The cautious proof vessel accepts a valid hail, updates its own actor-local contact knowledge, and changes course through the ordinary targetable tactical-course command. Hail adds neither dialogue state nor a narrative runtime.

## Autonomous cautious response

Only the proof vessel has persisted `CautiousContact` posture; migrated and existing ships do not acquire it automatically. Its decision input contains own-ship facts and actor-safe contact snapshots, never `SimulationState`, target `ShipState`, a target definition, or hidden identity/position.

The pure policy evaluates Hold, Approach, and Withdraw with explicit constraints, scores, and the stable Hold/Approach/Withdraw tie rule. It selects an incoming valid hail source first; otherwise it chooses the nearest Current observed contact and breaks ties by local contact ID. An unidentified current contact selects Withdraw at a bounded 0.5 km/s (clamped by maximum speed). A valid identified hail selects Hold; no Current contact also selects Hold. Each evaluation returns a typed explanation of its facts, candidates, rejected constraints, selected action, issued course, tie rule, and `RandomnessUsed = false`. The explanation is diagnostic and testable, not durable history.

## Deterministic boundaries and player events

Contact-sensitive local simulation uses the existing 100 ms tactical grid only when motion or changing sensor integrity can change local knowledge. At each such boundary, Core captures one world-truth snapshot, evaluates observer/target pairs in observer ship-ID then target ship-ID order, applies contact changes in that order, and schedules same-time decision wakes in observer order. The existing scheduler then resolves due work with finite budgets. Contact-loss, scan-completion, and decision work use exact saved correlations and revalidate prerequisites rather than trusting an old assumption.

Inactive strategic work remains event-to-event; there is no global recurring sensor sweep or future range-crossing solver. `AdvanceUntilNextPlayerRelevantEvent` processes hidden NPC observation and decision work without reporting it. It may return early for player-safe contact lifecycle or scan events, each carrying only an optional local contact ID.

## Content and persistence

Ship-definition schema V3 adds `passiveSensorRangeKilometers` and `activeScanDurationMilliseconds`. The canonical Pathfinder definition uses 30 km and 2,000 ms respectively. Save schema V4 uses simulation-rules version `sensor-knowledge-first-contact-v1` and explicitly maps per-ship contact allocators, tracks, observation facts, identification, active scan state, autonomous posture, pending decision wake, and exact new scheduler correlations.

V3-to-V4 migration preserves existing state but initializes empty sensor knowledge, next contact ID 1, no active scan, no contact posture, and no pending decision wake. The V1-to-V2-to-V3-to-V4 chain remains adjacent and candidate-before-commit validation rejects invalid contact and scheduler cross-references before replacing live simulation state.

## Deliberate deferrals

This slice does not implement affiliation or intent knowledge, confidence values or measurement error, estimated stale positions, strategic contacts, cloaking, emissions, electronic warfare, false contacts, NPC scans, additional doctrines, faction or mission AI, dialogue trees, sensor power allocation, EPS, damage, shields, weapons, combat, generalized encounter generation, or Science/Communications workspaces. Combat and Engineering preview fixtures remain illustrative, non-authoritative, and non-submitting.
