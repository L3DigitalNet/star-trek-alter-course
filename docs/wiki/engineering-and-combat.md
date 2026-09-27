---
schema_version: '1.1'
id: 'concept-81vyg9-engineering-and-combat'
title: 'Engineering and Combat'
description: 'Implemented Engineering and first combat-engagement rules, including bounded tactical and persistence consequences.'
doc_type: 'concept'
status: 'active'
created: '2026-09-06'
updated: '2026-09-27'
tags:
  - 'engineering'
  - 'simulation'
aliases: []
related:
  - 'docs/adr/0011-represent-physical-quantities-with-explicit-units.md'
  - 'docs/adr/0014-use-an-extensible-bounded-ship-system-substrate.md'
  - 'docs/wiki/content-assets-and-persistence.md'
  - 'docs/wiki/observation-driven-faction-response.md'
  - 'docs/wiki/ship-system-substrate.md'
  - 'ROADMAP.md'
---

# Engineering and combat

[Wiki home](README.md) · [Sensors and AI](sensors-knowledge-and-ai.md) · [Interface and commands](interface-and-player-commands.md) · [Implementation status](implementation-status.md)

## Authority and implemented scope

Engineering is a deliberately small connected Core model: generated power constrains allocation; allocation and condition derive sensor, impulse, shield, and weapon capability; capability changes contacts, scans, tactical courses, combat, and AI; one repair changes one system over simulation time. Core owns values, legality decisions, correlations, and player projection. Godot displays that immutable projection and submits typed intent.

[M6A First Combat Engagement](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112) is implemented on `dev`, but remains unreleased and does not complete M6. M6A was admitted using V5 ship definitions and V9 saves; [Issue #121's installed-system substrate migration](ship-system-substrate.md) has since superseded that runtime representation on the development branch (Final PR #123, unreleased) with ship content V6, system-definition content V1, and V10 saves. The latest source release remains v0.6.2, whose gameplay baseline remains v0.6.0 with V4 ship definitions and V8 saves — unaffected by either development-branch change. [Content, assets, and persistence](content-assets-and-persistence.md) owns compatibility and migration details.

`ShipSystemKind` (renamed from `ShipSystemId`; the five strings are unchanged) carries the semantic kind names `power-generation`, `sensors`, `impulse-propulsion`, `shields`, and `directed-energy-weapons`. All five are legal M6A damage targets. The four consumers—sensors, impulse, shields, and directed-energy weapons—are allocatable and repairable; generation is not repairable in M6A. This is no longer a fixed one-field-per-kind structure: each kind is now a reusable system definition that a ship installs as a distinct instance with its own identity, condition, and allocation (below and [Ship-system substrate](ship-system-substrate.md)). Numeric enum ordinals never cross content, save, projection, or event boundaries; display labels cannot identify a system.

[ADR 0014](../adr/0014-use-an-extensible-bounded-ship-system-substrate.md) required distinguishing kind, reusable definition, and persistent installed instance. [Issue #121's substrate contract](ship-system-substrate.md) implements that complete migration on the development branch, including heterogeneity, absence, identity, compatibility, actor-safe targeting, and generic Godot Engineering presentation. The M6A formulas below remain the numerical baseline; they describe the current installed-system representation, not a fixed-field or universal five-system assumption.

`SystemCondition` is finite and bounded from zero through one; its Offline, Degraded, and Nominal labels are presentation states. `PowerUnits` is a non-negative abstract integer quantity bounded at 1,000,000. Construction rejects negative or out-of-range values, addition is checked, comparison is deterministic, and JSON uses an invariant integer. It is neither watts nor stored energy, fuel, heat, or a physical-precision claim.

Content defines immutable capability. Authored power and combat values are positive and bounded; base shot damage is at most one; scan, repair, and weapon-cooldown durations are positive and aligned to the 100 ms simulation step. Strict content validation admits no fallback proof values. Runtime state holds one installed-system collection (condition and, for consumers, allocation per installation), one optional repair keyed to an installed identity, a per-ship readiness time per installed weapon, and at most one pending defensive stimulus. With the production Pathfinder loadout this reproduces five conditions and four consumer allocations, but that count is a content fact, not a fixed-field limit. Derived power, reserve, capability, range, speed, repair-progress labels, and UI state are not persisted.

## Power and capability

```text
available = floor(nominal generation × generation condition)
reserve = available - sensors - impulse - shields - directed energy
power satisfaction = min(1, allocation / positive nominal demand)
effective capability = condition × power satisfaction
```

Exact allocation must not exceed either authored consumer demand or current available power. Unallocated power is legal. Capability is bounded to `[0, 1]`; zero allocation or condition gives zero capability, while nominal condition and allocation reproduce authored capability.

```text
effective passive range = authored passive range × sensor capability
effective maximum tactical speed = authored maximum tactical speed × impulse capability
```

Impulse governs tactical propulsion only: strategic routes, travel duration, patrol, hold, and arrival scheduling retain their rules.

Core generates all presets and validates exact allocation through one path. **Balanced** floors proportional demand shares and distributes remaining whole units in one semantic pass, at most one per eligible consumer, in authored common order (Sensors, Impulse, Shields, Directed Energy for the production loadout), with installed-system ID as the tie-break after that order. Priority takes an installed consumer ID, satisfies it first, then the remaining consumers in that same order. Allocations remain capped by demand; surplus generation remains reserve. Godot never repeats these calculations.

A rejected voluntary allocation is atomic: it changes no Engineering, contact, scan, scheduler, motion, event, or simulation-time state. If the current tactical speed would exceed the resulting impulse limit, the allocation is rejected with a typed reason. The ship must slow first; a voluntary allocation command never silently decelerates it. Damage uses the separate forced reconciliation below.

### Pathfinder proof values and starting state

Pathfinder V5 is a proof configuration, not a permanent balance commitment:

| Authored quantity                                   | Value                         |
| --------------------------------------------------- | ----------------------------- |
| Nominal generation                                  | 120 power units               |
| Sensor / impulse / shield / directed-energy demands | 70 / 50 / 40 / 30 power units |
| Passive range / maximum tactical speed              | 30 km / 10 km/s               |
| Active scan duration / weapon cooldown              | 2,000 ms / 2,000 ms           |
| Directed-energy range / base shot damage            | 20 km / 0.25                  |
| Sensor / shield repair duration                     | 8,000 ms each                 |
| Impulse / directed-energy repair duration           | 6,000 ms each                 |

The player's generator condition of 0.625 yields 75 units. In consumer order, Balanced is 28/20/16/11, Sensors-first is 70/5/0/0, and Propulsion-first is 25/50/0/0. The new-game player retains the historical travel allocation 44/31/0/0 rather than applying the expanded Balanced preset automatically.

New-game bootstrap initializes authored shield and directed-energy systems as nominal, but the player's allocations to both are zero. The V5 production Kestrel uses 70/5/15/30 from 120 available power: this preserves 30 km sensing and its 0.5 km/s cautious course while enabling defense. Other production ships retain zero shield/weapon allocation. This starting state does not assign hostile allegiance. The separate V8→V9 migration initializes the new conditions and allocations to zero rather than injecting new-game combat capability into old saves.

## Observation, movement, and repair

Passive observation uses effective sensor capability. A committed allocation reconciles ordered local observation immediately at current simulation time. An active scan keeps its authored fixed duration while capability is positive and its target is Current. Zero sensor capability or loss of the required contact interrupts the scan, clears it, and cancels its exact completion work; restored power never resumes it. [Sensors, knowledge, and AI](sensors-knowledge-and-ai.md) owns lifecycle and deterministic observation ordering.

Every player/autonomous tactical course is validated against the current effective impulse limit. The cautious-contact policy receives actor-safe contact facts and its own limit, never world aggregate, target Engineering, hidden identity, or Godot state.

One ship can have one `SystemRepairState`, targeting an installed system ID (resolving support and duration from that installation's definition rather than a per-kind switch; generation cannot be targeted because its definition carries no repair capability). It records start/target condition, start/expected-completion time, and exact scheduled-work identity. Target must improve condition and completion must follow start. Condition/progress are linear analytical interpolation clamped to the repair interval. Completion materializes the exact target once and clears the repair atomically. Repair uses simulation time, continuing during travel and screen changes.

Any positive shield absorption cancels a shield repair, regardless of the selected target. Positive penetration cancels repair of the penetrated subsystem. Damage to a different system leaves the active repair running. Each interruption preserves the already materialized condition after damage and removes the exact completion work; an obsolete completion must not restore the old target condition.

`SystemRepairCompletion` is a finite scheduler kind. The active repair matched by exact work ID supplies the target. Candidate/load validation rejects missing, duplicate, mismatched, or orphaned repair work. There are no repair queues, teams, spare parts, generator repair, arbitrary payloads, background loops, or zero-time recurrence. The defensive combat policy does not start repairs or reallocate power.

## Presentation and first combat engagement

Live Engineering shows only the player's nominal/available power, allocations, reserve, own conditions/statuses, effective capability/range/speed, active repair, and Core-supplied action reasons. The M6A hierarchy is Overview, Power, Sensors, Propulsion, Shields, Weapons, and Repairs. It does not expose NPC Engineering, scheduler entries, persistence DTOs, AI diagnostics, hidden target IDs, affiliation, or intent. The [interface page](interface-and-player-commands.md) owns interaction and preview rules.

This is a bounded operational-damage model, not a general one. EPS topology, batteries, warp, life support, computers, structural systems, fuel, heat/coolant, crews, repair queues, inventory, generalized components, dynamic strategic travel, a hull pool, ship destruction, shield facings, automatic recharge, additional weapon families, and random outcomes are absent or deferred.

## Implemented M6A first engagement

### Targeting and firing cadence

An M6A fire intent carries a local `SensorContactId` and a semantic target `ShipSystemId`. It requires the same strategic location, a Current and Identified local contact, inclusive range under the existing spatial tolerance, positive weapon capability, and `now >= nextReady`. Core resolves hidden target correlation internally; it does not expose the true target identity through the command or actor-safe projection.

Every rejection is atomic: it changes no cooldown, scheduler, contacts, or events. A successful shot sets persisted per-ship absolute `nextReady` to checked `now + cooldown`; readiness defaults to zero and is nonnegative, fixed-step aligned, and bounded. There is no periodic weapon scheduler. When the addition is not representable, Core returns a typed time-limit rejection without partial mutation. Unrepresentable finite-position separation rejects as out of range without a projection failure.

### Shields and subsystem damage

```text
output = base shot damage × weapon capability
shield capacity = shield condition × shield power satisfaction
absorption = min(output, shield capacity)
remaining shield condition = clamp(old shield condition - absorption, 0, 1)
penetration = output - absorption
```

Penetration directly reduces the selected subsystem condition, bounded to `[0, 1]`. A shot targeted at Shields applies penetration after absorption to that already reduced shield condition. Offline or unpowered shields absorb zero. Nominal fully powered shields initially absorb a full bounded shot.

M6A uses one all-aspect shield system. Its condition is both the damaged system state and the basis of remaining protection; there is no separate shield-energy reservoir or automatic recharge. Allocating more power increases protection available from the remaining condition but does not restore that condition. Recovery uses the existing shield repair. Broader geometry, facings, recharge, hull damage, and random outcomes remain deferred.

### Atomic damage transition

Damage constructs one complete candidate in this order: derive shot output; apply shields; apply selected-system penetration; cancel an active repair whose system receives positive damage, including shield absorption against Shields, with its exact completion work; reconcile allocation and speed; reuse observation and invalid-scan interruption; admit defensive stimulus; validate; then commit. Due repair work already dequeued in the same batch must tolerate this exact invalidation without weakening ordinary orphan validation.

### Defensive response

A nonplayer ship receives at most one pending stimulus when its local contact for the observed attacker is Current at attack time. Identification is never invented; the player does not receive an autonomous defensive stimulus. The stimulus stores only local contact ID, observed time, due time `observed + 100 ms`, and exact work ID. The first eligible stimulus wins in accepted-shot order; later shots neither grow nor postpone it.

One `ShipCombatDecisionWake` consumes the stimulus before evaluating once. From own tactical, capability, readiness, time, and local-contact facts only, the separate deterministic policy explains all three candidates and selects in fixed Return Fire → Withdraw → Hold order:

- Return Fire targets directed-energy weapons when legal.
- Otherwise Withdraw issues an ordinary legal course when a known-current displacement and legal speed exist.
- Otherwise Hold issues no new course. It does not automatically stop existing motion.

The policy does not wait or reschedule for cooldown, start repair, change power allocation, alter travel/orders, use faction/controller/affiliation facts, or replace the cautious-contact policy. A return shot may schedule a later stimulus but never recurses inline. Mutual identification in the production defensive proof comes from the existing scan and hail path, not from being attacked.

### Disengagement and information boundaries

There is no global encounter object, combat lock, or separate combat clock. Ordinary motion, range, contact lifecycle, and strategic location determine whether another shot is legal. Withdrawal and non-engagement remain valid choices; the Combat workspace does not create an authoritative engagement.

M6A attacker events report a fired shot, shield interaction, or penetration directed at the selected kind, without target identity, controller, faction, condition, capability, or percentage. A player victim may see its own damage, brownout, forced speed, and repair interruption. These events do not establish political hostility, faction knowledge, or durable diplomatic incidents.

The [heterogeneous-target contract](ship-system-substrate.md#remote-targeting-must-not-become-an-inventory-probe) refines feedback for Issue #121 so an absent hidden installation cannot be inferred from a pre-shot choice, free refusal, or confirmed-damage message. It defines normal discharge, shield resolution, no fallback receiver, and unconfirmed subsystem damage for that newly representable case, implemented on the development branch. It does not change the canonical M6A damage formulas above.

### Involuntary degradation is a required design decision

This decision is **resolved and implemented for M6A**. The existing allocation/course rules govern voluntary commands and may reject a requested state that would exceed current capability. Physical damage cannot be rejected merely because it invalidates a previously legal allocation, speed, scan, or repair.

When generation damage leaves current allocation within available power, allocation is preserved. Otherwise each consumer becomes `floor(oldAllocation × available / oldTotal)`, then in one semantic pass each eligible consumer receives at most one remaining unit in Sensors, Impulse, Shields, Directed Energy order. No allocation increases above its prior value. This brownout scales committed allocations, unlike Balanced, which distributes according to authored demand.

If effective impulse capability falls below current speed, forced reconciliation clamps speed to the new maximum and preserves heading. Sensor loss or a generation brownout may interrupt a scan through the existing observation rules. Damage interrupts only the repair described above. Core owns all of these transitions and their actor-safe consequences; Godot does not correct state after the fact.

## Combat-driven Engineering depth

After the first combat proof, add ship systems because a concrete tactical or command decision requires them. A new system should create a meaningful choice, failure mode, or interaction rather than exist only for fidelity bookkeeping. [Issue #121's substrate migration](ship-system-substrate.md), required by ADR 0014 before additional systems or recovery gameplay, is implemented on the development branch; further ship-system depth remains future work.

M6A's recovery limits are intentional scope boundaries, not claims of a complete damage-control loop: generation can be damaged but not repaired, shield protection has no automatic recharge, and the reactive combat policy does not manage Engineering recovery. Repair meaning, rates, reprioritization, and autonomous recovery require later refinement; #121 deliberately preserves existing repair behavior rather than deciding them.

Detailed EPS topology, batteries, heat/coolant, advanced warp Engineering, life support, crews, repair teams/queues, magazines, boarding, cloaking, electronic warfare, torpedoes, tractor beams, and other specialized systems remain deferred until a real consumer demonstrates need. Do not build an exhaustive subsystem catalog before a concrete gameplay consumer exists.

## Sources

[Engineering state](../../src/AlterCourse.Core/Ships/ShipEngineeringState.cs), [repair correlation](../../src/AlterCourse.Core/Ships/SystemRepairState.cs), [combat transition](../../src/AlterCourse.Core/Gameplay/GameSimulation.Combat.cs), [defensive policy](../../src/AlterCourse.Core/AI/DefensiveCombatDecisionPolicy.cs), [Pathfinder content](../../src/AlterCourse.Godot/content/ships/pathfinder.json), [Engineering tests](../../tests/AlterCourse.Core.Tests/Ships/EngineeringBackboneTests.cs), [M4 scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/Milestone4EngineeringScenarioTests.cs), [combat scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/M6CombatScenarioTests.cs), [combat horizon](../../tests/AlterCourse.Core.Tests/Gameplay/M6CombatLongHorizonTests.cs), and [V9 persistence tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV9CombatTests.cs). Final admission and verification evidence is indexed in [Implementation status](implementation-status.md#verification-evidence-not-a-fresh-execution-claim). [Ship-system substrate](ship-system-substrate.md) separately defines the migration's implemented runtime, persistence, and generic Godot Engineering presentation; recovery and further tactical presentation are future design work.
