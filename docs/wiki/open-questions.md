---
schema_version: '1.1'
id: 'plan-o0oaje-open-questions'
title: 'Open Design Questions'
description: 'Resolved and scoped decisions for the implemented faction-assignment slice and approved observation-driven response, with remaining questions deferred to concrete consumers.'
doc_type: 'plan'
status: 'active'
created: '2026-09-06'
updated: '2026-09-26'
tags:
  - 'design'
aliases: []
related:
  - 'docs/wiki/decision-register.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/observation-driven-faction-response.md'
  - 'docs/wiki/factions-and-organizations.md'
  - 'ROADMAP.md'
---

# Open design questions

[Wiki home](README.md) · [Approved decisions](decision-register.md) · [Faction assignment](faction-intent-and-autonomous-assignment.md) · [Observation response](observation-driven-faction-response.md) · [Current implementation](implementation-status.md)

## How to use this register

Resolve only what blocks the next governed vertical slice. These questions are not a request to answer everything, a speculative implementation backlog, or permission for an agent to choose silently. The political principles in [Factions and organizations](factions-and-organizations.md) are settled and should not be repeatedly re-asked.

[Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) is implemented in Feature #86 / Final PR #87 as `0217296`. The owner approved [Observation-Driven Faction Response](observation-driven-faction-response.md) as the next bounded slice, and Feature #93 / Final PR #94 implements its direct reporting/provenance/compatibility contract. This does not complete M3/M5 or silently answer broader intelligence questions.

## Resolved sequencing and first-consumer decisions

**Q-01 — Scope and sequence — RESOLVED September 6, 2026; subsequently refined.** [Strategic Contact Reporting](strategic-contact-reporting.md) was selected after v0.4.0 as a bounded bridge from local observer knowledge to durable, reference-frame-qualified strategic last-known contact information, before broader faction autonomy. It is implemented and released in v0.5.0. This historical sequencing decision remains valid.

Strategic Contact Reporting is not canonically named `M3B`. It does not itself complete M3 or begin M5. The earlier proposed `M3B→M5→M6` label/sequence was not approved wholesale. The implemented faction-assignment slice followed it. Observation-Driven Faction Response is implemented; M6 Tactical Combat Foundation is the next major development family without requiring M3 or M5 to be declared complete first.

**Q-05 — Smallest political gameplay proof — RESOLVED for the first bounded slice, September 6, 2026.** Use two era-neutral root factions. A has an objective to establish ship presence at Vesper Reach and at least two directly controlled NPC candidates; B has a ship there. A's policy must select a different eligible ship when its preferred candidate is already committed. Existing order, travel, and sensor rules produce a legitimate offscreen NPC-NPC consequence without player interaction. This is a first M5 contribution, not the whole living-sector milestone. The canonical slice owns the complete proof and non-goals.

## Knowledge and campaign context

**Q-02 — Durable known-vessel identity — OPEN; source provenance IMPLEMENTED in v0.6.0.** The released observation-response slice preserves the reporting observer's ordinary `ShipInstanceId`, that observer's local `SensorContactId`, observation reference frame, position/time, and legitimately learned vessel/design identification as source provenance. This does **not** identify the hidden target globally. `KnownShipId`, cross-observer correlation, matching-by-name, and a durable remembered vessel identity distinct from local tracks remain open.

How a future faction refers to a vessel it has never locally observed also remains open beyond the approved historical report snapshot. A faction can store “observer X reported its Contact Y at location Z” without claiming that Y is globally the same vessel as another observer's contact.

**Q-03 — Strategic knowledge and affiliation — OPEN.** Which facts can an observer learn, from which sources, and at which spatial scale? Scan still learns vessel/design names, not allegiance. Authoritative direct controller state does not automatically become observer-known affiliation, political intent, a transponder result, or UI data. The approved response investigates reported **activity at a location**, not an enemy or known faction vessel. Affiliation-learning sources, confidence, and broader intelligence remain deferred.

**Q-04 — Knowledge ownership and sharing — PARTIALLY RESOLVED for assignment and one direct report channel.** The implemented assignment policy may receive its objective, explicitly known proof-map topology, and assignment-relevant administrative facts of its own directly controlled assets: identity, current strategic state, and existing assignment/order status.

The implemented response slice adds exactly one sensor-information path: **directly controlled NPC ship → its direct controlling faction**. A new local observation episode may produce an immutable historical snapshot delivered after 2,000 ms of simulation time. The recipient stores explicit bounded received reports; it does not read a live union of ship sensor stores. The report preserves observer-local provenance and never gains hidden target identity/controller, affiliation, intent, or later target movement. The player is not part of this reporting path.

What propagates through political hierarchy, organizations, allies, treaty partners, other factions, or to the player remains open. Communications range/relays/jamming, intelligence fusion, confidence, cross-observer correlation, and broader report summarization remain open. This direct path is not an instantaneous sensor network or a general message bus.

**Q-06 — Era and region for initial content — SCOPED, broader question OPEN.** The approved faction proofs remain era-neutral and may reuse existing fictional strategic locations. No campaign era/region or canonical polity assignment is selected. The archived assistant proposal of 2378 remains unapproved.

## When political presentation and government mechanics become consumers

**Q-07 — Atypical hierarchy labels — OPEN.** Direct parentage, derived depth, three initial supported depths for the eventual hierarchy, and role/depth separation are settled. Exact UI wording for atypical branches must not turn Polity/Constituent/Internal into restrictive actor types. The current implemented faction slices need roots only and add no political UI.

**Q-08 — Minimal authority and consequence rules — PARTIALLY RESOLVED for direct assignment and investigation only.** A faction may assign only an idle, directly controlled NPC ship satisfying existing order/travel prerequisites. The observation-response slice may issue one bounded investigation assignment under the same authority, excluding the reporting observer for its own report and never preempting existing work. Existing presence intent is processed first; the player ship remains excluded.

Sanctioned versus unsanctioned political actions, inherited obligations, discovery consequences, delegation, captain versus strategic command, permission matrices, treaty precedence, constitutional exceptions, and espionage remain open until a concrete interaction needs them.

**Q-09 — Institutional control and political transitions — OPEN.** How will an organization or ruling faction influence decisions without replacing the polity itself? Charter versus control, succession, mergers, dissolution, and contested jurisdiction require later refinement. Stable identity, mutable parentage, and government/state separation remain approved principles. Faction-only direct control and direct-faction reporting do not redefine organizations as factions or approve a generic actor model.

## Before combat and durable incidents

**Q-10 — Tactical scope — RESOLVED for M6A First Combat Engagement; implementation in progress.** M6A composes one directed-energy family, non-facing shields, tactical motion, local sensor knowledge, Engineering allocation/condition, deterministic defensive response, persistence, and withdrawal without a separate combat-state graph. It uses no RNG, hull pool, shield recharge, facings, or new weapon family. Firing requires a same-location Current and Identified local contact in inclusive range, positive weapon capability, and readiness. ReturnFire, Withdraw, and Hold are the complete defensive candidates; no policy uses faction/controller/affiliation facts or interrupts travel/orders.

Damage first consumes bounded shield capacity, then applies penetration directly to a selected concrete subsystem. Brownout, speed clamp, active-scan interruption, and repair cancellation are forced deterministic reconciliation, not voluntary-command rejection. Positive damage to a system cancels its active repair, including shield absorption against Shields. M6A persists readiness, four-consumer Engineering state, bounded defensive stimulus, and exact wake correlation through planned V9; V8→V9 creates no historical combat capability, damage, aggression, or hidden knowledge. Shield facings/geometry, recharge, hull, broad combat rules, and later stochastic consumers remain open.

Ship-system depth should grow through combat consumers. Detailed EPS networks, batteries, heat/coolant, advanced warp Engineering, life support, crew/repair teams, magazines, boarding, cloaking, and electronic warfare remain deferred until a concrete tactical decision needs them.

**Q-11 — Political memory — OPEN.** Choose which incidents must persist, how responsibility becomes known/disputed, and how history remains bounded while affecting later decisions. Do not implement an unbounded event log or replace diplomacy with one global reputation number. A faction decision explanation or received sensor report is not automatically political memory.

## Before campaign generation or broader simulation

**Q-12 — Canon divergence and initialization — OPEN.** Refine which facts are fixed at campaign start, which later canonical events are pressures rather than guarantees, and whether normal-simulation warm-up improves the start. The roadmap's preference for legitimate divergence remains provisional at this level of detail. Typed initialization of the era-neutral faction proofs does not resolve campaign generation.

**Q-13 — Resources, officers, and trade — OPEN.** Define the first useful logistics or crew interaction before selecting economic catalogs, officer progression, markets, repair staffing, or fuel models. Preserve captain-without-levels and the existing concrete Engineering model. Current faction proofs use existing ship commitments, not a new political resource economy.

**Q-14 — Compatibility and stochastic behavior — PARTIALLY RESOLVED through V8.** Released v0.6.0 preserves the adjacent V1→V2→V3→V4→V5→V6→V7→V8 chain. The implemented V6→V7 migration creates no factions, controller assignments, faction decision state, or faction wakes and preserves historical ship/world state and scheduler semantics under explicit ship targets.

Observation-Driven Faction Response introduces V8 under rules identity `observation-driven-faction-response-v1`. Migration creates no reports, report-delivery work, investigation state, or location-response history and disables the new reporting/response posture for migrated factions. Existing contacts must not be mined to invent unsent history. Zero-faction worlds remain valid. New-game bootstrap enables the posture explicitly for the proof.

Both implemented faction assignment and the approved observation-response policy consume no randomness. The eventual fixed/versioned RNG algorithm, supported random consumers, and later development-save compatibility promises remain open. M6 must revisit the RNG question only if its first combat rules actually need it.

## Implementation choices versus approvals

The closed typed **Ship | Faction** scheduler target remains approved. Released observation response adds a finite report-delivery work kind targeted to Faction with exact report correlation; this does not approve a generic actor/message/event target registry. Stable ordering, exact correlation, target validation, budgets, and typed bootstrap remain governed by existing ADRs.

Exact internal type spelling, identity representation for report IDs, focused immutable policy-input types, JSON DTO layout, and measured scheduler/save maximum constants are implementation choices within the approved behavior. They must be explicit and tested; they are not permission to add unapproved identity correlation, affiliation knowledge, preemption, organizations, hierarchy, communications simulation, or RNG.

If implementation discovery proves that a deferred product decision is necessary, return to governed design refinement rather than silently extending the slice. Do not reopen prior decisions simply because the general questions retain unresolved portions.

## Sources

The register combines the [roadmap](../../ROADMAP.md), the political discussion under [issue #68](https://github.com/L3DigitalNet/star-trek-alter-course/issues/68), the Strategic Contact Reporting decision under [issue #74](https://github.com/L3DigitalNet/star-trek-alter-course/issues/74), the owner's approval of [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md), and the September 7 owner approval of [Observation-Driven Faction Response](observation-driven-faction-response.md) and subsequent M6 sequencing. The [decision register](decision-register.md) preserves the stable decision IDs.
