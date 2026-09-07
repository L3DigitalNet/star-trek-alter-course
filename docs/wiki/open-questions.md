---
schema_version: '1.1'
id: 'plan-o0oaje-open-questions'
title: 'Open Design Questions'
description: 'Resolved and scoped decisions for the approved faction-assignment slice, with remaining questions deferred to concrete consumers.'
doc_type: 'plan'
status: 'active'
created: '2026-09-06'
updated: '2026-09-07'
tags:
  - 'design'
aliases: []
related:
  - 'docs/wiki/decision-register.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/factions-and-organizations.md'
  - 'ROADMAP.md'
---

# Open design questions

[Wiki home](README.md) · [Approved decisions](decision-register.md) · [Faction assignment](faction-intent-and-autonomous-assignment.md) · [Current implementation](implementation-status.md)

## How to use this register

Resolve only what blocks the next governed vertical slice. These questions are not a request to answer everything, a speculative implementation backlog, or permission for an agent to choose silently. The political principles in [Factions and organizations](factions-and-organizations.md) are settled and should not be repeatedly re-asked.

The owner approved [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) as the next bounded slice. Feature #86 / Final PR #87 implements it and merged into `dev` as `0217296`. Its six decisions resolve Q-05 and only the expressly scoped portions of Q-04, Q-06, Q-08, and Q-14. Q-02 and Q-03 remain open; this does not complete M3 or M5.

## Resolved sequencing and first-consumer decisions

**Q-01 — Scope and sequence — RESOLVED September 6, 2026.** [Strategic Contact Reporting](strategic-contact-reporting.md) was selected after v0.4.0 as a bounded bridge from local observer knowledge to durable, reference-frame-qualified strategic last-known contact information, before broader faction autonomy. It is implemented and released in v0.5.0. This historical sequencing decision remains valid.

Strategic Contact Reporting is not canonically named `M3B`. It does not itself complete M3 or begin M5. The earlier proposed `M3B→M5→M6` label/sequence was not approved wholesale. The next selected work is now the separate faction-assignment slice, not another attempt to implement Strategic Contact Reporting.

**Q-05 — Smallest political gameplay proof — RESOLVED for the first bounded slice, September 6, 2026.** Use two era-neutral root factions. A has an objective to establish ship presence at Vesper Reach and at least two directly controlled NPC candidates; B has a ship there. A's policy must select a different eligible ship when its preferred candidate is already committed. Existing order, travel, and sensor rules produce a legitimate offscreen NPC-NPC consequence without player interaction. This is a first M5 contribution, not the whole living-sector milestone. The canonical slice owns the complete proof and non-goals.

## Still open around knowledge and campaign context

**Q-02 — Durable known-vessel identity — OPEN.** Should an observer's remembered vessel identity eventually be distinct from a sensor track? How should a report about a never-locally-observed ship be represented? Strategic Contact Reporting reuses observer-local `SensorContactId`; neither it nor the faction-assignment slice approves `KnownShipId` or cross-observer correlation. Referring to a faction's own directly controlled `ShipInstanceId`s is not a new identity system for externally known vessels.

**Q-03 — Strategic knowledge and affiliation — OPEN.** Which facts can an observer learn, from which sources, and at which spatial scale? Scan still learns vessel/design names, not allegiance. Authoritative direct controller state does not automatically become observer-known affiliation, political intent, a transponder result, or UI data. Affiliation-learning sources, confidence, and broader intelligence remain deferred.

**Q-04 — Knowledge ownership and sharing — PARTIALLY RESOLVED for assignment only.** The faction policy may receive its objective, explicitly known proof-map topology, and assignment-relevant administrative facts of its own directly controlled assets: identity, current strategic state, and existing assignment/order status. It receives no unrestricted world/ship state, ship-local contacts/scans, external reports, or other factions' hidden facts.

What sensor information reaches a faction, how it is transmitted or delayed, how reports are correlated or summarized, what propagates through political hierarchy, and what reaches the player remain open. The approved administrative view is not an instantaneous sensor network. Strategic Contact Reporting remains ship/player-local; the first assignment policy does not ingest it.

**Q-06 — Era and region for initial content — SCOPED, broader question OPEN.** The approved first faction proof is era-neutral and can reuse existing fictional strategic locations. No campaign era/region or canonical polity assignment is selected. The archived assistant proposal of 2378 remains unapproved.

## When political presentation and government mechanics become consumers

**Q-07 — Atypical hierarchy labels — OPEN.** Direct parentage, derived depth, three initial supported depths for the eventual hierarchy, and role/depth separation are settled. Exact UI wording for atypical branches must not turn Polity/Constituent/Internal into restrictive actor types. The first faction-assignment slice needs roots only and adds no political UI.

**Q-08 — Minimal authority and consequence rules — PARTIALLY RESOLVED for direct assignment only.** A faction may assign only an idle, directly controlled NPC ship satisfying the existing order/travel prerequisites. No order preemption, cancellation/replacement, voyage interruption, controller transfer, or player-command override is granted. The proof excludes the player ship from faction autonomous control.

Sanctioned versus unsanctioned political actions, inherited obligations, discovery consequences, delegation, captain versus strategic command, permission matrices, treaty precedence, constitutional exceptions, and espionage remain open until a concrete interaction needs them.

**Q-09 — Institutional control and political transitions — OPEN.** How will an organization or ruling faction influence decisions without replacing the polity itself? Charter versus control, succession, mergers, dissolution, and contested jurisdiction require later refinement. Stable identity, mutable parentage, and government/state separation remain approved principles. First-slice faction-only direct control does not redefine organizations as factions or approve a generic actor model.

## Before combat and durable incidents

**Q-10 — Tactical scope — OPEN.** Select the smallest useful shield/weapon/damage model and disengagement rules. Resolve interactions with power, observation, geometry, and existing orders before introducing a broad weapon catalog. M6's directed-energy suggestion is not a final weapon selection.

**Q-11 — Political memory — OPEN.** Choose which incidents must persist, how responsibility becomes known/disputed, and how history remains bounded while affecting later decisions. Do not implement an unbounded event log or replace diplomacy with one global reputation number. A faction decision explanation is not automatically political memory.

## Before campaign generation or broader simulation

**Q-12 — Canon divergence and initialization — OPEN.** Refine which facts are fixed at campaign start, which later canonical events are pressures rather than guarantees, and whether normal-simulation warm-up improves the start. The roadmap's preference for legitimate divergence remains provisional at this level of detail. Typed initialization of the era-neutral faction proof does not resolve campaign generation.

**Q-13 — Resources, officers, and trade — OPEN.** Define the first useful logistics or crew interaction before selecting economic catalogs, officer progression, markets, repair staffing, or fuel models. Preserve captain-without-levels and the existing abstract Engineering model. The first faction proof uses existing ship commitments, not a new political resource economy.

**Q-14 — Compatibility and stochastic behavior — PARTIALLY RESOLVED for the next slice.** The implementation is approved to introduce V7 and preserve the adjacent V1→V2→V3→V4→V5→V6→V7 migration chain. V6→V7 creates no factions, controller assignments, faction decision state, or faction wakes; it preserves historical ship/world state and scheduler semantics under explicit ship targets. Zero-faction worlds remain valid. New-game initialization must not inject politics into a migrated save.

The first faction policy is fully deterministic and consumes no randomness. The eventual fixed/versioned RNG algorithm, supported random consumers, and later development-save compatibility promises remain open. Current `dev` saves are V7; v0.5.0 remains V6. The preceding V5→V6 migration's refusal to invent observation history remains the precedent, not permission to invent political facts.

## Implementation choices versus approvals

The closed typed **Ship | Faction** scheduler target is now approved for the first faction slice; it is no longer merely one unapproved proposal. Stable ordering, exact correlation, target validation, budgets, and typed bootstrap remain governed by the existing ADRs. No generic actor/entity/target registry is approved.

Exact type spelling, identity representation, focused candidate scoring/tie-breaking, meaningful wake cadence, JSON family layout, and concrete bounded state models are implementation choices within the approved behavior. They must be explicit and tested; they are not permission to add unapproved knowledge, preemption, organizations, hierarchy, or RNG.

If implementation discovery proves that a deferred product decision is necessary, return to governed design refinement rather than silently extending the slice. Do not reopen the six approved decisions simply because the general questions retain unresolved portions.

## Sources

The register combines the [roadmap](../../ROADMAP.md), the political discussion under [issue #68](https://github.com/L3DigitalNet/star-trek-alter-course/issues/68), the Strategic Contact Reporting decision under [issue #74](https://github.com/L3DigitalNet/star-trek-alter-course/issues/74), and the owner's later approval of [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md), recorded through [documentation PR #84](https://github.com/L3DigitalNet/star-trek-alter-course/pull/84). The [decision register](decision-register.md) preserves the stable decision IDs.
