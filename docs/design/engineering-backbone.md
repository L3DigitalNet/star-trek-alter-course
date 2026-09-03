---
schema_version: '1.1'
id: 'concept-34m8xl-engineering-backbone'
title: 'Engineering Backbone'
description: 'Defines the authoritative power, condition, repair, capability, persistence, and live Engineering boundary for Milestone 4.'
doc_type: 'concept'
status: 'active'
created: '2026-09-03'
updated: '2026-09-03'
tags:
  - 'architecture'
  - 'engineering'
  - 'simulation'
aliases: []
related:
  - 'ROADMAP.md'
  - 'docs/design/first-observed-contact.md'
  - 'docs/design/command-deck-ui.md'
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md'
---

# Engineering Backbone

Milestone 4 establishes one concrete Engineering chain: generated power constrains allocation; allocation and condition derive sensor and impulse capability; those capabilities alter contacts, scans, tactical courses, and cautious AI; one analytical repair changes a selected system over simulation time. The ordinary Kestrel encounter is the player-visible proof. This is an unreleased `dev` feature governed by Feature #62.

## Authority and scope

`AlterCourse.Core` owns every Engineering value, transition, schedule correlation, legality decision, player projection, and autonomous capability fact. `AlterCourse.Godot` displays the player ship's immutable projection and submits typed Engineering intent. Content JSON authors immutable design capability, while save JSON captures consequential runtime state. Derived capability, range, speed, reserve, progress labels, and UI selection are never persisted.

The implementation is deliberately concrete. It recognizes power generation, sensors, and impulse propulsion; only sensors and impulse are allocatable and repairable. It does not introduce a component registry, common system base class, arbitrary telemetry, or a runtime service layer.

## Stable identities and bounded values

`ShipSystemId` is a closed semantic identity with explicit JSON names `power-generation`, `sensors`, and `impulse-propulsion`. Numeric enum ordinals never cross content, save, projection, or event boundaries. Display labels remain presentation data and cannot identify a system.

`SystemCondition` is a finite bounded scalar from zero through one. Zero supplies no effective capability; one supplies full capability relative to authored design. `Offline`, `Degraded`, and `Nominal` are derived presentation statuses. The same condition type serves generator, sensor, and impulse state, while each system's capability rule stays explicit.

`PowerUnits` is a non-negative integral abstract quantity bounded at 1,000,000 units. Construction rejects negative or out-of-range values, addition is checked, comparison is deterministic, and JSON uses an invariant integer. The unit makes no watt, energy, fuel, thermal, or physical-precision claim.

## Authored Engineering definition

Ship-definition content V4 retains base passive sensor range, fixed active-scan duration, and base maximum tactical speed. Its concrete `engineering` object adds nominal generation, nominal sensor demand, nominal impulse demand, sensor repair duration, and impulse repair duration. All power values are positive and bounded; durations are positive and align to the 100 ms simulation step.

The Pathfinder proof definition uses 120 nominal generation units, 70 sensor-demand units, and 50 impulse-demand units. A nominal generator can therefore satisfy both systems exactly. Sensor repair retains the established authored duration; impulse repair has its own authored duration. Bootstrap, not reusable design content, selects damaged or power-constrained runtime condition.

## Available power and allocation

Available power is derived and never persisted:

```text
available = floor(nominal generation × generation condition)
```

The calculation uses decimal-safe bounded arithmetic, never exceeds nominal generation, and rejects invalid authored values before a ship exists. Authoritative allocation stores exact sensor and impulse units. Each value must be at most its corresponding nominal demand and their checked sum must be at most current available power. Reserve is `available - sensors - impulse`; unallocated reserve is legal.

An exact allocation command and three Core-generated choices share one validator:

- Balanced floors each proportional share `available × demand / total demand`, then assigns remaining whole units in stable Sensors-before-Impulse order while respecting demand.
- Prioritize Sensors satisfies sensor demand first and gives the remaining available units to impulse.
- Prioritize Propulsion satisfies impulse demand first and gives the remaining available units to sensors.

Godot submits a stable choice or exact Core intent; it never reproduces these calculations. A rejected allocation changes no Engineering, contact, scan, scheduler, motion, event, or time state. If the ship's current tactical speed would exceed the proposed effective maximum, the whole allocation is rejected with a typed reason. The player or AI must reduce speed before removing required propulsion power; allocation never silently decelerates a ship.

## Effective capability

For each consumer, power satisfaction is the allocated units divided by nominal demand, capped at one. Effective capability is condition multiplied by power satisfaction and remains in `[0, 1]`.

```text
effective passive range = authored passive range × sensor capability
effective maximum tactical speed = authored maximum tactical speed × impulse capability
```

Zero allocation or zero condition produces zero capability. Full nominal allocation at full condition reproduces authored capability. These values are calculated in Core from one authoritative Engineering state. Strategic routes, travel duration, patrol, hold, and arrival scheduling remain unchanged; impulse is local tactical propulsion only.

## Observation and active scans

Passive observation replaces the former direct sensor-integrity multiplier with effective sensor capability. After a valid allocation commits, Core immediately invokes the existing ordered observation reconciliation at the current simulation time. Contacts may become Current, Stale, or reacquired at that same time; observer-local IDs, learned identity, target correlation secrecy, player-safe events, and exact event occurrence times remain unchanged.

Active scan duration remains the authored fixed duration and is never rescheduled for partial capability. A scan may continue while capability is positive and its target is Current. If a committed allocation or repair boundary makes sensor capability zero, Core clears the scan, cancels its exact scheduled completion, and emits the player-safe interruption event. Restoring power does not resurrect the scan. Existing target-staleness interruption remains unchanged.

Changing sensor condition during an active repair is contact-sensitive. The aggregate advances only ships whose motion or sensor repair can change local observations, on the existing deterministic 100 ms boundaries. There is no new global polling sweep.

## Tactical propulsion and cautious AI

Every player and autonomous `SetTacticalCourse` application validates against the acting ship's current effective maximum tactical speed. Typed results distinguish strategic travel, propulsion offline, requested speed above current capability, and existing input failures. No command path may validate only against the immutable authored maximum.

The cautious-contact policy receives actor-safe contact snapshots plus the acting ship's own effective tactical-speed limit. It receives no `SimulationState`, other ship runtime state, hidden target identity, target Engineering values, or Godot state. Approach and Withdraw remain deterministically bounded and pass through the same targetable tactical-course application boundary. Explanations may record the acting capability constraint but remain internal diagnostic records rather than public world truth.

## System repair and scheduling

`SystemRepairState` replaces sensor-specific repair state. It records repairable target identity, starting and target condition, start and expected completion times, and one exact scheduled-work identity. Only sensors or impulse propulsion are accepted in this slice; generator repair is rejected as unsupported. One ship may have at most one active repair, its target condition must increase, and the authored target-specific duration must place completion strictly after start.

Condition and progress at time `T` are analytical linear interpolation, clamped to the repair interval. Completion materializes the exact target once and clears the repair atomically. Repair continues during travel and while another workspace is shown because it uses simulation time rather than Godot time.

The scheduler replaces `SensorRepairCompletion` with the finite known `SystemRepairCompletion` kind. Scheduled work still carries only due time, ship identity, kind, stable ID, and sequence; target system comes from the validated active repair matched by exact work ID. Load validation rejects absent, duplicate, mismatched, or orphaned repair work. It introduces no delegates, arbitrary payload, queue, cancellation, background loop, or zero-time recurrence.

## Bootstrap and proof world

Typed `ShipStart` supplies generator, sensor, and impulse condition; exact initial allocation; and optional active system repair. Obsolete sensor-only bootstrap fields are removed. `GameBootstrap` validates current tactical speed against effective capability and validates repair progress/correlation before constructing the candidate aggregate.

The four ordinary ships and Survey Vessel Kestrel's cautious posture remain. USS Pathfinder begins with generator condition `0.625`, yielding 75 available units from the 120-unit definition. Its deterministic Balanced allocation is 44 sensors and 31 impulse, so both cannot reach full capability. Prioritize Sensors becomes 70/5 and materially extends passive range; Prioritize Propulsion becomes 25/50 and materially increases tactical speed. Kestrel and unrelated proof ships start with nominal generator/system condition and full compatible allocations. Pathfinder begins one observable sensor repair so repair, contact, scan/hail, allocation, course, and save/load participate in one production scenario.

## Persistence V5

Save V5 uses a new explicit model and simulation-rules version. Each ship stores three conditions, exact sensor/impulse allocation, and optional system repair target/conditions/times/exact work identity. It does not store derived power, reserve, capability, range, speed, labels, progress, presentation, preview, or cached projections.

The adjacent V4-to-V5 migration maps `SensorIntegrity` to sensor condition and an active V4 sensor repair to a V5 repair targeting `sensors`, preserving all times and exact scheduled-work identity while changing the known work kind. It initializes generator and impulse condition to one and allocates both consumers at full nominal demand. Because migrated V4 content has nominal generation equal to total demand, historical passive range and tactical speed are preserved. The migration validates the complete V5 candidate against the resolved definition before constructing live state; impossible allocations or content mismatch fail clearly.

The V1-to-V2-to-V3-to-V4-to-V5 chain remains adjacent. Simulation time, contact knowledge and local IDs, active scans, autonomous posture/wakes, orders, scheduler ordering, and allocator continuation are preserved. Maximum-shape V5 serialization measures 95,677,740 bytes under the 134,217,728-byte (128 MiB) envelope, and failed load never replaces live state.

## Player projection and live Engineering

The immutable player Engineering projection exposes nominal and available power, exact allocations, reserve, own-system conditions/statuses, effective capabilities, effective sensor range and tactical maximum speed, active repair target/progress/completion, and canonical Engineering actions with current availability reasons. It exposes no mutable aggregate, scheduled work, persistence DTO, AI diagnostic, target ship ID, hidden affiliation/intent, or NPC Engineering truth.

Normal gameplay presents only live Core values in the existing Engineering workspace. The hierarchy is `OVERVIEW`, `POWER`, `SENSORS`, `PROPULSION`, and `REPAIRS`. Preview fixtures remain usable only in explicit preview mode; shields, weapons, EPS, batteries, warp, life support, computers, and structural systems stay absent or `Unavailable` according to the existing convention.

Balanced, Prioritize Sensors, Prioritize Propulsion, Begin Sensor Repair, Begin Impulse Repair, and Return to Command are stable typed presentation actions. Disabled actions carry Core-supplied reasons. After intent translation, `GameScreen` consumes the typed Core result, records concise feedback, and presents a fresh projection; it never optimistically mutates UI state.

Engineering hierarchy, component selection, and action controls reconcile by stable presentation ID. Existing controls are updated in place, signals connect once, removed actions become non-activatable, and activation resolves the current payload by ID. Focus, hover, pointer press/release, selection, and keyboard traversal therefore survive frequent live refresh. The existing pause/rate/save/load and focused-button Space protections remain in force.

## Verification and deliberate deferrals

Lowest-layer tests cover bounded quantities, independent allocation/capability models, commands and atomicity, contact/scan consequences, tactical/AI limits, analytical repair/correlation, strict V4 content, V5 migrations/envelope/continuation, the full headless Kestrel sequence, public API/architecture boundaries, and Godot live refresh/intent/isolation. The combined scenario acquires Kestrel at 3,500 ms, saves during the active scan at 4,500 ms, completes the scan and later reacquires the same local contact ID at 5,500 ms, completes the sensor repair at 8,000 ms, and proves USS Horizon's strategic arrival at 14,000 ms. Focused Core and GameplayShell suites are green; canonical verification, manual gameplay review, and final review remain pending.

This milestone does not start strategic contacts, affiliations or intent knowledge, additional postures, factions, diplomacy, dialogue, shields, weapons, combat, life support, computers, transporters, structural damage, warp Engineering, dynamic travel recalculation, fuel, heat/coolant, EPS topology, batteries, crew, repair teams/queues, parts/inventory, economy, arbitrary damage, generalized component definitions, a universal system interface, ECS, event bus, behavior tree, state-machine framework, dependency-injection container, database, service architecture, LLM authority, release, packaging, or `main` work.
