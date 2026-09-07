---
schema_version: '1.1'
id: 'concept-81vyg9-engineering-and-combat'
title: 'Engineering and Combat'
description: 'Implemented Engineering rules and the planned systems-driven combat foundation.'
doc_type: 'concept'
status: 'active'
created: '2026-09-06'
updated: '2026-09-07'
tags:
  - 'engineering'
  - 'simulation'
aliases: []
related:
  - 'docs/adr/0011-represent-physical-quantities-with-explicit-units.md'
  - 'docs/wiki/content-assets-and-persistence.md'
  - 'ROADMAP.md'
---

# Engineering and combat

[Wiki home](README.md) · [Sensors and AI](sensors-knowledge-and-ai.md) · [Interface and commands](interface-and-player-commands.md) · [Implementation status](implementation-status.md)

## Authority and implemented scope

Engineering is a deliberately small connected Core model: generated power constrains allocation; allocation and condition derive sensor and impulse capability; capability changes contacts, scans, tactical courses, and cautious AI; one repair changes one system over simulation time. Core owns values, legality decisions, correlations, and player projection; Godot displays that immutable projection and submits typed intent.

`ShipSystemId` is a closed semantic identity with explicit JSON names `power-generation`, `sensors`, and `impulse-propulsion`. Numeric enum ordinals never cross content, save, projection, or event boundaries; display labels cannot identify a system. Only sensors and impulse are allocatable and repairable.

`SystemCondition` is finite and bounded from zero through one; its Offline, Degraded, and Nominal labels are presentation states. `PowerUnits` is a non-negative abstract integer quantity bounded at 1,000,000. Construction rejects negative or out-of-range values, addition is checked, comparison is deterministic, and JSON uses an invariant integer. It is neither watts nor stored energy, fuel, heat, or a physical-precision claim.

Content defines immutable capability. Authored power values are positive and bounded; active-scan, sensor-repair, and impulse-repair durations are positive and aligned to the 100 ms simulation step. Runtime state holds three conditions, exact sensor/impulse allocation, and optionally one repair. Derived power, reserve, capability, range, speed, repair-progress labels, and UI state are not persisted. The current V7 save contract and migrations belong to [content, assets, and persistence](content-assets-and-persistence.md); former V4/V5 details were historical contracts, not current format guidance.

## Power and capability

```text
available = floor(nominal generation × generation condition)
```

Exact allocation must not exceed either authored demand or current available power. Reserve is `available - sensors - impulse`; unallocated power is legal. For each consumer, power satisfaction is allocated units divided by nominal demand, capped at one. Effective capability is condition multiplied by power satisfaction, also capped at one.

```text
effective passive range = authored passive range × sensor capability
effective maximum tactical speed = authored maximum tactical speed × impulse capability
```

Zero allocation or condition gives zero capability; full condition and nominal allocation reproduce authored capability. Impulse governs tactical propulsion only: strategic routes, travel duration, patrol, hold, and arrival scheduling retain their rules.

Core generates all presets and validates exact allocation through one path:

- **Balanced** floors proportional shares, then gives whole-unit remainders to Sensors before Impulse.
- **Prioritize Sensors** satisfies sensors first, then impulse.
- **Prioritize Propulsion** satisfies impulse first, then sensors.

Godot never repeats those calculations. A rejected allocation is atomic: it changes no Engineering, contact, scan, scheduler, motion, event, or simulation-time state. If the current tactical speed would exceed the resulting impulse limit, the allocation is rejected with a typed reason; the ship must slow first and is never silently decelerated.

Pathfinder is a proof configuration, not a permanent balance commitment: 120 nominal generation units, demand of 70 for sensors and 50 for impulse, 30 km passive range, and a 2,000 ms active scan. Its generator condition of 0.625 yields 75 units. The presets are Balanced 44/31, Sensors-first 70/5, and Propulsion-first 25/50. Sensor and impulse repair durations are 8,000 ms and 6,000 ms.

## Observation, movement, and repair

Passive observation uses effective sensor capability. A committed allocation reconciles ordered local observation immediately at current simulation time. An active scan keeps its authored fixed duration while capability is positive and its target is Current. If allocation or a repair boundary reduces sensor capability to zero, Core clears the scan, cancels its exact completion work, and emits the player-safe interruption; restored power never resumes it. [Sensors, knowledge, and AI](sensors-knowledge-and-ai.md) owns lifecycle and deterministic observation ordering.

Every player/autonomous tactical course is validated against the current effective impulse limit. The cautious policy receives actor-safe contact facts and its own limit, never world aggregate, target Engineering, hidden identity, or Godot state.

One ship can have one `SystemRepairState`, targeting only sensors or impulse. It records start/target condition, start/expected-completion time, and exact scheduled-work identity. Target must improve condition and completion must follow start. Condition/progress are linear analytical interpolation clamped to the repair interval. Completion materializes the exact target once and clears the repair atomically. Repair uses simulation time, continuing during travel and screen changes.

`SystemRepairCompletion` is a finite scheduler kind. The active repair matched by exact work ID supplies the target. Candidate/load validation rejects missing, duplicate, mismatched, or orphaned repair work. There are no repair queues, teams, spare parts, generator repair, arbitrary payloads, background loops, or zero-time recurrence.

## Presentation, combat, and evidence

Live Engineering shows only the player's nominal/available power, allocations, reserve, own conditions/statuses, effective capability/range/speed, active repair, and Core-supplied action reasons. The hierarchy is Overview, Power, Sensors, Propulsion, and Repairs. It does not expose NPC Engineering, scheduler entries, persistence DTOs, AI diagnostics, target IDs, affiliation, or intent. The [interface page](interface-and-player-commands.md) owns interaction and preview rules.

This is degraded operation and repair, not a general damage model. Shields, weapons, EPS topology, batteries, warp, life support, computers, structural systems, fuel, heat/coolant, crews, repair queues, inventory, generalized components, and dynamic strategic travel are absent or unavailable.

M6 should compose existing movement, knowledge, power, condition, AI, persistence, and Engineering rather than a parallel hit-point game. Targeting must use actor knowledge; shields/weapons must connect to power/condition; damage must affect capability; survivors retain identity. Directed energy is only a likely first weapon. Facings, geometry, damage distribution, disengagement, randomness, torpedoes, boarding, cloaking, fleets, electronic warfare, and balance remain open.

Rules are grounded in [Engineering state](../../src/AlterCourse.Core/Ships/ShipEngineeringState.cs), [repair correlation](../../src/AlterCourse.Core/Ships/SystemRepairState.cs), [Pathfinder content](../../src/AlterCourse.Godot/content/ships/pathfinder.json), [Engineering tests](../../tests/AlterCourse.Core.Tests/Ships/EngineeringBackboneTests.cs), and [scenario tests](../../tests/AlterCourse.Core.Tests/Gameplay/Milestone4EngineeringScenarioTests.cs). Combat is [roadmap M6](../../ROADMAP.md) intent, not playable-combat evidence.
