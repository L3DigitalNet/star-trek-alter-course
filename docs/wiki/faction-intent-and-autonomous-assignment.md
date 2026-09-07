---
schema_version: '1.1'
id: 'decision-egd3kj-faction-intent-and-autonomous-assignment'
title: 'Faction Intent and Autonomous Assignment'
description: 'Owner-approved first bounded M5 slice, control and knowledge boundaries, scheduler target extension, and planned V7 compatibility.'
doc_type: 'decision'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'design'
  - 'simulation'
  - 'ai'
aliases: []
related:
  - 'docs/wiki/factions-and-organizations.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/open-questions.md'
  - 'docs/wiki/decision-register.md'
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md'
  - 'ROADMAP.md'
---

# Faction Intent and Autonomous Assignment

[Wiki home](README.md) · [Political framework](factions-and-organizations.md) · [Decision register](decision-register.md) · [Open questions](open-questions.md)

## Approval, authority, and implementation status

**Approved design, not implemented.** On September 6, 2026, the owner approved the next-slice recommendation and all six preimplementation decisions: the concrete Q-05 proof, direct asset-control authority, narrow own-asset administrative knowledge, a closed Ship/Faction scheduler target, V7 migration semantics, and no randomness. The owner authorized documenting and merging those decisions before runtime implementation begins.

This page owns that bounded design. It refines the [political framework](factions-and-organizations.md) without replacing it and implements existing ADR boundaries rather than changing them. No new ADR, dependency, generic actor framework, or governance exception is introduced by this design.

The reviewed runtime remains v0.5.0 with ship-definition content V4 and save V6. This documentation does not create factions, change a schema, start an implementation feature, publish a release, or complete either M3 or M5. A subsequent governed implementation feature must use this approved contract; selecting the design is not evidence that implementation has started.

## Purpose and bounded architectural proof

The next implementation slice is **Faction Intent and Autonomous Assignment**, the first bounded M5 work after Strategic Contact Reporting. Its causal proof is:

> faction objective → explainable choice → ordinary ship assignment/order → offscreen execution → durable world or ship-local knowledge consequence

Existing M1/M2 world, orders, scheduler, M3A ship-local knowledge and cautious behavior, M4 Engineering boundaries, and v0.5.0 last-known reports remain the foundation. A faction chooses an assignment; execution must not become a parallel faction movement engine.

This is not an attempt to finish all intelligence questions before M5. Strategic Contact Reporting remains useful player/observer knowledge, but this first faction policy does not ingest those reports merely because the projection exists.

## Approved Q-05 proof scenario

Use two root-level, era-neutral factions in the existing Dawn Anchor / Vesper Reach / Meridian Drift proof region. These are development actors, not assertions about canonical governments, affiliations, or a campaign year. The player ship remains outside faction autonomous control.

Faction A has an objective to establish a ship presence at Vesper Reach and at least two directly controlled NPC candidate ships. Faction B has a ship at Vesper Reach. Use the existing travel/order machinery and ordinary local sensing/ship behavior for the offscreen encounter; do not add a combat, diplomacy, treaty, or economy subsystem to manufacture a consequence.

The main proof must show all of the following:

1. A faction decision wake builds an actor-safe snapshot of its objective, known topology, and the permitted administrative facts of its own assets.
2. The pure policy considers the eligible ships, rejects invalid candidates with reasons, selects deterministically, and returns a typed assignment through the existing ship-order command/application boundary.
3. The assigned ship travels offscreen. Its arrival produces a legitimate NPC-NPC sensor/contact consequence involving the other faction's ship even when the player never visits or interacts.
4. A controlled variant makes the otherwise preferred ship already committed; the policy selects the other eligible ship rather than replacing the commitment. A no-eligible-ship variant returns explained no-action without partial state changes.
5. Save/load preserves the assignment, relevant world and knowledge state, pending work, and deterministic continuation.

The second faction is a genuine distinct political actor with its own state and controlled asset, not an affiliation learned by the player. The bounded proof does not claim that one dispatch completes the broader M5 requirement for multiple autonomous political actors making consequential strategic choices; that remains a milestone-level exit condition beyond this initial slice.

The observed consequence must be authoritative world or ship-local knowledge state, not merely a log line. The faction policy need not receive the resulting contact report. Player-facing proof may use existing legitimate observations; diagnostic test access must not be exported to the normal UI.

Exact fixture names, IDs, tuning values, candidate ranking details, and class spellings are implementation choices within this contract. They must not turn the policy into a ship-name-specific script or silently expand the approved information or command boundaries.

## Direct control and command authority

A participating NPC ship may have zero or one direct controlling `FactionId`. Store the authoritative relationship on the ship/asset side; derive a faction's roster from canonical ship state rather than persisting a second independently mutable list of controlled ship IDs.

A null controller means that no faction-control assignment is represented in this slice. It does not invent an independent polity, an unknown enemy affiliation, or an implicit default faction. Controller identity is world truth, distinct from hierarchy, jurisdiction, ownership claims, and observer-known affiliation.

A faction may assign only an idle, directly controlled NPC ship through validated Core commands. Existing active orders and physical travel are not preempted, canceled, replaced, reprioritized, or stolen by this feature. Validate control and eligibility again at application, not only during candidate evaluation; rejected commands leave both faction and ship state unchanged.

An eligible idle ship must satisfy the existing order/travel preconditions; a ship already physically traveling does not become available merely because its order is absent. The implementation must state the exact predicate using current domain state and cover it with negative tests.

The player ship cannot be autonomously assigned by a faction in this proof. Future captain-versus-strategic-command authority remains open. No controller transfer or political reparenting feature is admitted here.

Only factions are implemented as direct controllers now. Organizations remain a distinct future domain; do not introduce `ActorId`, a universal controller base type, a faction/organization union, or placeholder organization state before an actual organization consumer requires it. This narrow representation does not revoke the broader political design's eventual support for organization-controlled assets.

## Scoped administrative knowledge, not intelligence sharing

The faction policy receives a purpose-built immutable input, never `SimulationState`, unrestricted `ShipState`, or the player's projection. Besides its own objective and explicitly known strategic topology, its own-asset administrative facts are limited to what assignment needs: controlled ship identity, current strategic state, and existing assignment/order status.

This slice models those administrative facts as current at the decision boundary. It does not implement transmission delay or a communications network for administrative status, and this simplification grants no access to external observations. The authoritative control relationship determines which assets qualify for that administrative view.

The input must not contain ship-local contacts, active scans, external last-known contact reports, hidden enemy positions, enemy orders or Engineering state, the other faction's objectives, or inferred affiliation/intent. Having a direct controller does not automatically upload a ship's sensor knowledge. Additional administrative fields require an explicit concrete consumer and design refinement rather than a broad permission to inspect owned objects.

Own-asset `ShipInstanceId` is legitimate administrative identity; it does not create a cross-observer `KnownShipId`. External ships continue to be represented to observing ships by observer-local `SensorContactId`. Neither controller truth nor faction decision explanations are automatically revealed to the player.

This is a **scoped partial answer to Q-04** only. External intelligence propagation, report granularity/delay, correlation, superior/subordinate sharing, and player receipt remain open. Q-02 known-vessel identity and Q-03 affiliation-learning semantics remain open.

## Minimal faction state, definitions, and bootstrap

Introduce stable `FactionId`, bounded consequential faction state, only the objective/decision state this proof needs, and a focused pure decision policy with typed inputs, candidates, command/no-action output, and structured explanations. Persist state that affects future behavior; do not persist a duplicate roster or a UI projection as a second authority.

Reusable production faction definitions follow ADR 0005: strict JSON, stable definition references, schema and semantic validation, and immutable Core-consumable definitions. Definition data, faction runtime identity/state, and initial scenario circumstances remain distinct. Do not hard-code mutable campaign objectives, control links, or future hierarchy into immutable definitions merely for convenience.

Faction starts, control links, initial state, and initial decision wakes enter through typed bootstrap and complete candidate validation. Do not add postconstruction `BootstrapHiddenFaction...` mutations. Existing proof-specific initialization is not a precedent for political bootstrap backdoors; removing unrelated legacy helpers is not required by this documentation decision.

Support zero factions as a valid world. Reject duplicate/uninitialized identities, broken controller or objective references, invalid bounds, and inconsistent pending-work correlations before admitting a candidate. The first scenario uses roots only; parent/child runtime, governments, organizations, and all three political depths are not required to implement this slice.

## Closed scheduler target extension

Faction decision wakes are the first concrete non-ship scheduler consumer. Extend the existing scheduler with a closed typed target that distinguishes `Ship(ShipInstanceId)` from `Faction(FactionId)`. The exact C# spelling is not prescribed. Do not introduce `Actor`, `Entity`, `ISchedulable`, an untyped object target, or a generic target registry.

Keep known data-only work kinds, stable work IDs, exact ownership/cancellation, persisted same-time ordering, and deterministic serialized application under ADR 0007. Persist target kind plus its correctly typed identity and validate both target existence and kind/target compatibility. A matching numeric value in another identity domain is never a valid substitute.

Faction decisions run at meaningful simulation boundaries, not on every tactical tick and not on a second clock. Define the initial wake, reassessment/termination behavior, deduplication, and any no-action fallback explicitly during implementation; bound candidates, outstanding work, and repeated decisions. The policy must not oscillate, repeatedly assign an already committed ship, or create an unbounded same-time wake chain.

The exact cadence and numeric budgets are implementation choices to justify with the selected scenario and long-horizon tests, not authorization for wall-clock behavior, a workflow engine, or parallel mutation. Ship-targeted work retains its existing semantics and same-time ordering.

## Planned persistence and V6-to-V7 migration

The implementation is planned to introduce **save V7** because faction state, direct-controller links, and non-ship scheduled work are consequential state. **V6 remains current until that implementation lands.** Preserve the adjacent V1→V2→V3→V4→V5→V6→V7 chain under ADR 0006; do not change ship-definition content V4 solely because a distinct faction content family is added.

The approved V6→V7 migration creates an empty faction collection, null direct-controller links on all historical ships, no faction decision state, and no faction-targeted scheduled work. It preserves existing ship state, sensor knowledge, orders, counters, and scheduled-work identities/order; existing work acquires the equivalent typed Ship target without being rescheduled.

Do not infer political actors, control, allegiance, objectives, reports, or history from vessel names, definition names, strategic location, or current new-game content. New-game bootstrap may instantiate the new proof; a migrated game continues validly with zero factions. Loading is not new-game initialization.

Explicitly persist every new consequential reference, objective/assignment state, identity allocator if one is required, and exact faction wake correlation. Continue bounded input validation, unsupported-version rejection, complete candidate validation, and atomic replacement. Measure the expanded maximum shape against the existing save envelope; do not silently raise limits or weaken validation to make the new shape fit.

This resolves the compatibility decision for this slice only. It is not a perpetual pre-1.0 support promise or selection of a future random algorithm.

## Determinism and explanations

The first faction policy consumes **no randomness**. Use stable candidate ordering, explicit constraints and ranking, deterministic tie-breaking, and a typed explanation of selection or no-action. No random algorithm, stream, seed, or random persistence surface is introduced merely to choose between ships.

The explanation must identify the objective, permitted facts used, candidates, rejected constraints, ranking/tie rule, and resulting command or no-action. It is available to tests/diagnostics, not automatically to players or as durable political memory. Facts needed for future decisions belong in bounded authoritative state rather than only in a diagnostic trace.

Changing hidden external truth while holding the permitted actor input fixed must not change the pure policy result. Resolution may reject a proposal whose real command preconditions no longer hold. Q-14's broader stochastic choices remain deferred until a genuine consumer needs them.

## Acceptance and verification contract for implementation

The later implementation feature must prove the complete causal scenario, committed-preferred-ship alternative, and explained no-action case. Acceptance also requires:

- Negative authority and knowledge tests: foreign/uncontrolled/player/busy/traveling targets cannot be assigned; unauthorized facts never reach the policy or player projection; hidden-world changes cannot alter a fixed-input decision.
- Scheduler and persistence tests: typed target separation including equal underlying IDs, wrong kind/target rejection, dangling references, duplicate wakes, stable same-time order, V6 migration without invention, zero-faction continuation, and uninterrupted versus save/load continuation.
- Deterministic and bounded execution: insertion-order independence, bounded candidate/wake growth, no zero-time loops or repeated assignment oscillation, and long-horizon offscreen behavior while the player does unrelated work.
- Typed bootstrap and content validation: malformed/unknown/oversized definitions and snapshots fail closed; no political postconstruction mutation path is needed.
- Existing Core/Godot knowledge and command boundaries remain intact. Headless scenario proof is sufficient for this new slice; no faction-management UI or affiliation display is required.
- Canonical repository verification and the governed review/PR workflow run on the implementation change. This design page is not evidence that those future tests already exist or pass.

## Explicit non-goals and remaining questions

Do not add global vessel identity/correlation, affiliation or intent learning, faction sensor-report ingestion, an intelligence network, a new faction/affiliation UI, organizations, hierarchy runtime, governments, treaties, attitudes, political memory, territory ownership, economy, combat, broad mission/narrative systems, generic actors, new dependency frameworks, or RNG infrastructure in this slice.

Q-05 is resolved for this bounded initial proof, not for all M5 scope. Q-04 is partially resolved only for own-asset administrative knowledge. Q-06 uses an era-neutral fixture while the campaign era remains open. Q-14 is resolved only for this V7 migration/no-randomness boundary. Q-02, Q-03, Q-07 through Q-13, the rest of Q-04/Q-06/Q-14, and the final M3/M5 completion contracts remain deferred to real consumers.

If implementation cannot satisfy the approved exit condition without crossing one of those boundaries, return to governed design refinement instead of silently broadening the feature.

## Sources

The owner's September 6, 2026 approval of the repository review and six preimplementation recommendations is the product authority recorded by this documentation change. Existing constraints and implementation seams are [Factions and organizations](factions-and-organizations.md), [Strategic Contact Reporting](strategic-contact-reporting.md), [open questions](open-questions.md), [GameBootstrap](../../src/AlterCourse.Core/Gameplay/GameBootstrap.cs), [ScheduledWork](../../src/AlterCourse.Core/Simulation/ScheduledWork.cs), [SimulationState](../../src/AlterCourse.Core/Gameplay/SimulationState.cs), and [CautiousContactDecisionPolicy](../../src/AlterCourse.Core/AI/CautiousContactDecisionPolicy.cs). ADRs 0005, 0006, 0007, and 0010 govern content, persistence, scheduling, and AI respectively.
