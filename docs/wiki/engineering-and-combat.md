---
schema_version: '1.1'
id: 'concept-81vyg9-engineering-and-combat'
title: 'Engineering and Combat'
description: 'Implemented Engineering rules and the approved sequencing principles for the systems-driven combat foundation.'
doc_type: 'concept'
status: 'active'
created: '2026-09-06'
updated: '2026-09-26'
tags:
  - 'engineering'
  - 'simulation'
aliases: []
related:
  - 'docs/adr/0011-represent-physical-quantities-with-explicit-units.md'
  - 'docs/wiki/content-assets-and-persistence.md'
  - 'docs/wiki/observation-driven-faction-response.md'
  - 'ROADMAP.md'
---

# Engineering and combat

[Wiki home](README.md) · [Sensors and AI](sensors-knowledge-and-ai.md) · [Interface and commands](interface-and-player-commands.md) · [Implementation status](implementation-status.md)

## Authority and implemented scope

Engineering is a deliberately small connected Core model: generated power constrains allocation; allocation and condition derive sensor and impulse capability; capability changes contacts, scans, tactical courses, and cautious AI; one repair changes one system over simulation time. Core owns values, legality decisions, correlations, and player projection; Godot displays that immutable projection and submits typed intent.

`ShipSystemId` is a closed semantic identity with explicit JSON names `power-generation`, `sensors`, and `impulse-propulsion`. Numeric enum ordinals never cross content, save, projection, or event boundaries; display labels cannot identify a system. Only sensors and impulse are allocatable and repairable.

`SystemCondition` is finite and bounded from zero through one; its Offline, Degraded, and Nominal labels are presentation states. `PowerUnits` is a non-negative abstract integer quantity bounded at 1,000,000. Construction rejects negative or out-of-range values, addition is checked, comparison is deterministic, and JSON uses an invariant integer. It is neither watts nor stored energy, fuel, heat, or a physical-precision claim.

Content defines immutable capability. Authored power values are positive and bounded; active-scan, sensor-repair, and impulse-repair durations are positive and aligned to the 100 ms simulation step. Runtime state holds three conditions, exact sensor/impulse allocation, and optionally one repair. Derived power, reserve, capability, range, speed, repair-progress labels, and UI state are not persisted. The current released V8 save contract and migrations belong to [content, assets, and persistence](content-assets-and-persistence.md); former V4/V5 details were historical contracts, not current format guidance.

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

## Presentation and current combat boundary

Live Engineering shows only the player's nominal/available power, allocations, reserve, own conditions/statuses, effective capability/range/speed, active repair, and Core-supplied action reasons. The hierarchy is Overview, Power, Sensors, Propulsion, and Repairs. It does not expose NPC Engineering, scheduler entries, persistence DTOs, AI diagnostics, target IDs, affiliation, or intent. The [interface page](interface-and-player-commands.md) owns interaction and preview rules.

This is degraded operation and repair, not a general damage model. Shields, weapons, EPS topology, batteries, warp, life support, computers, structural systems, fuel, heat/coolant, crews, repair queues, inventory, generalized components, and dynamic strategic travel are absent or unavailable.

Combat remains unimplemented. [Observation-Driven Faction Response](observation-driven-faction-response.md) is implemented. **M6A First Combat Engagement is owner-approved and implementation is in progress.** Full M3 or M5 completion is not a prerequisite; this approval is not evidence that any M6A behavior has landed.

## First combat engagement direction

M6 should compose existing movement, knowledge, power, condition, AI, persistence, and Engineering rather than a parallel hit-point game. The first bounded engagement should begin with:

- one directed-energy weapon family;
- a bounded shield model;
- targeting/fire control constrained by actor knowledge;
- meaningful power competition with sensing and propulsion;
- tactical maneuver/range;
- damage that degrades concrete systems/capability;
- deterministic/explainable combat AI;
- persistence of combat consequences; and
- withdrawal, disengagement, and non-engagement as valid outcomes.

The M6A contract below settles the bounded first engagement. Facings, a hull pool, recharge, additional weapon families, and broader combat remain deferred.

## Approved M6A first engagement

Pathfinder V5 preserves its existing generation 120, sensor demand 70, impulse demand 50, passive range 30 km, maximum tactical speed 10 km/s, scan 2,000 ms, sensor repair 8,000 ms, and impulse repair 6,000 ms. It adds shield demand 40, directed-energy demand 30, shield repair 8,000 ms, weapon repair 6,000 ms, directed-energy range 20 km, base shot damage 0.25, and cooldown 2,000 ms. These Pathfinder values are proof/balance fixtures, not permanent balance commitments. Authored combat values are positive, damage is at most one, times align to the fixed step, and strict content validation admits no fallback proof values.

The closed subsystem identities are `power-generation`, `sensors`, `impulse-propulsion`, `shields`, and `directed-energy-weapons`. All five are legal damage targets because each has an operational effect. Generation remains unrepairable; shields and directed-energy weapons share the existing one-repair slot with sensors and impulse.

For every powered consumer, capability is `condition × min(1, allocation / positive demand)`, bounded to `[0, 1]`. A shot's output is `baseDamage × weaponCapability`. M6A resolves one all-aspect shield system: shield effective capacity is `shieldCondition × shieldPowerSatisfaction`; absorption is `min(output, capacity)`, shield condition becomes `clamp(oldCondition - absorption, 0, 1)`, and penetration is `output - absorption`. Penetration directly reduces the selected subsystem condition. A shot targeted at Shields applies its penetration after absorption to that already reduced shield condition. Shield capacity ceases at zero condition; offline or unpowered shields also absorb zero. Nominal fully powered shields initially absorb a full bounded shot. Broader shield geometry and facings are deferred. There are no recharge, hull-pool, or random outcomes.

Allocation validates all four consumers against their authored demands and available power `floor(nominalGeneration × generationCondition)`. Brownout first preserves allocations if their total fits. Otherwise each allocation becomes `floor(oldAllocation × available / oldTotal)`, then in one semantic pass each eligible consumer receives at most one remaining unit in fixed order Sensors, Impulse, Shields, DirectedEnergy. No allocation increases above its prior value. Balanced allocation uses proportional demands and the same one-unit remainder order; priority allocation satisfies its selected consumer then the remaining consumers in that order, except propulsion priority keeps Sensors next. The 75-unit Balanced result of 28/20/16/11 and the historical 44/31 travel allocation are proof/balance fixtures, not permanent balance commitments.

New-game bootstrap initializes shield and directed-energy conditions as nominal. The player starts with both allocations at zero so the existing travel/repair demonstration remains valid. Kestrel may explicitly use 70 Sensors, 5 Impulse, 15 Shields, and 30 DirectedEnergy from 120 available power, preserving 30 km sensing and its 0.5 km/s cautious course while enabling defense. Other ordinary NPC content may remain unpowered unless a proof requires otherwise; no hostile allegiance is added. Hail legitimately identifies reciprocal contacts before defensive proof. Damage can disable concrete systems but M6A has no hull system or ship destruction.

A fire intent carries a local `SensorContactId` and a target `ShipSystemId`. It requires the same strategic location, a Current and Identified local contact, inclusive range under the existing spatial tolerance, positive weapon capability, and `now >= nextReady`. Every rejection is atomic: it changes no cooldown, scheduler, contacts, or events. A successful shot sets the persisted per-ship absolute `nextReady` to checked `now + cooldown`; readiness defaults to zero and is nonnegative, fixed-step aligned, and bounded. There is no periodic weapon scheduler. At a time where that addition is not representable, Core returns a typed time-limit rejection without partial mutation.

Damage commits one complete candidate in this order: derive shot damage; apply shields; apply selected-system penetration; cancel an active repair whose system receives any positive damage, including shield absorption against Shields, with its exact completion work; reconcile allocation and speed; reuse observation and invalid-scan interruption; admit defensive stimulus; validate; then commit. The forced transition preserves heading on a speed clamp. A generator brownout may also interrupt a scan. Due work already dequeued in the same batch must tolerate this exact invalidation without weakening ordinary orphan validation.

Defensive response is a separate deterministic policy. A nonplayer ship receives at most one pending stimulus when its local contact for the observed attacker is Current at attack time; identification is never invented. The stimulus stores only local contact ID, observed time, due time `observed + 100 ms`, and exact work ID. The first eligible stimulus wins in accepted-shot order; later shots neither grow nor postpone it. One `ShipCombatDecisionWake` consumes the stimulus before evaluating once. From own tactical, capability, readiness, time, and local-contact facts only, it explains all three candidates with hard constraints and rejection reasons, then selects contact/system in fixed ReturnFire → Withdraw → Hold order: ReturnFire at directed-energy weapons when legal, otherwise Withdraw when a known-current displacement and legal speed exist, otherwise Hold. Hold creates no course; Withdraw issues an ordinary legal course. It does not wait or reschedule for cooldown, alter travel/orders, use faction/controller/affiliation facts, or replace the cautious-contact policy. A return shot may schedule a later stimulus but never recurses inline.

Results and events remain actor-safe. An attacker may learn only that a shot fired, shield hit, or penetration reached the selected system; it receives no target identity, controller, faction, condition, capability, or percentage. A player victim may see its own damage, brownout, forced speed, and repair interruption. Unrepresentable finite-position separation rejects as out of range without a projection failure.

### Involuntary degradation is a required design decision

The existing allocation/course rules govern **voluntary commands** and may reject a requested state that would exceed current capability. Combat damage is different: the simulation cannot reject physical damage merely because the resulting generator or impulse capability makes the ship's existing allocation, speed, scan, or repair state illegal under voluntary-command rules.

M6A defines deterministic reconciliation for:

- generation falling below already committed sensor, impulse, shield, or directed-energy allocations;
- impulse capability falling below current tactical speed;
- sensor capability becoming insufficient for an active scan; and
- damage/condition changes interacting with an active repair.

Do not quietly reuse voluntary allocation rejection as the damage rule and do not create hidden Godot-side correction. Core must own the forced transition and expose actor-safe consequences.

## Combat-driven Engineering depth

After the first combat proof, add ship systems because a concrete tactical or command decision requires them. A new system should create a meaningful choice, failure mode, or interaction rather than exist only for fidelity bookkeeping.

Detailed EPS topology, batteries, heat/coolant, advanced warp Engineering, life support, crews, repair teams/queues, magazines, boarding, cloaking, electronic warfare, torpedoes, tractor beams, and other specialized systems remain deferred until a real consumer demonstrates need. This does not make them undesirable; it prevents an exhaustive subsystem catalog from delaying the first interconnected combat proof.

Rules are grounded in [Engineering state](../../src/AlterCourse.Core/Ships/ShipEngineeringState.cs), [repair correlation](../../src/AlterCourse.Core/Ships/SystemRepairState.cs), [Pathfinder content](../../src/AlterCourse.Godot/content/ships/pathfinder.json), [Engineering tests](../../tests/AlterCourse.Core.Tests/Ships/EngineeringBackboneTests.cs), and [scenario tests](../../tests/AlterCourse.Core.Tests/Gameplay/Milestone4EngineeringScenarioTests.cs). Combat remains [roadmap M6](../../ROADMAP.md) intent until implemented evidence exists.
