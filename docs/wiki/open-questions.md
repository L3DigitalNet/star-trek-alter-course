---
schema_version: '1.1'
id: 'plan-o0oaje-open-questions'
title: 'Open Design Questions'
description: 'Resolved and deferred choices by concrete consumer, including the approved first faction-assignment slice and its scoped refinements.'
doc_type: 'plan'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
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

[Wiki home](README.md) · [Approved decisions](decision-register.md) · [Current implementation](implementation-status.md)

## How to use this register

Resolve only what blocks the next governed vertical slice. Unfinished choices are not permission for an agent to choose silently or a request to design every future system now. The political principles in [Factions and organizations](factions-and-organizations.md) are settled and should not be repeatedly re-asked.

After v0.5.0, the owner approved [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) and its six preimplementation decisions on September 6, 2026. That page owns the bounded contract. This register distinguishes complete decisions for the slice from broader questions that remain open. Approval is design authority, not an implementation-completion claim.

## Resolved sequencing and proof decisions

**Q-01 — Scope and sequence — RESOLVED September 6, 2026.** [Strategic Contact Reporting](strategic-contact-reporting.md) was selected after v0.4.0 as the bounded bridge from local observer knowledge to durable, reference-frame-qualified last-known information before political autonomy. It is implemented and released in v0.5.0. Its information seam does not itself authorize faction ingestion of reports.

The slice is not canonically named `M3B`, does not complete Milestone 3, and did not itself begin M5. The former speculative `M3B→M5→M6` package of identity/sharing choices was not approved by Q-01. The next selected design after v0.5.0 is the bounded faction-assignment slice below, not an instruction to finish all M3 questions first.

**Q-05 — Smallest political gameplay proof — RESOLVED for the first bounded slice, September 6, 2026.** Two era-neutral root factions participate in the existing three-location proof region. Faction A chooses among at least two directly controlled NPC ships to establish presence at Vesper Reach, where Faction B has a ship. A committed-preferred-ship variant must select the other eligible ship; a no-eligible-ship variant returns explained no-action. Existing orders/travel and ship-local sensing produce an offscreen NPC-NPC consequence without player intervention. See [the approved scenario and acceptance contract](faction-intent-and-autonomous-assignment.md).

This selects the first M5 implementation slice, not the full milestone exit contract. Broader consequential choices by multiple autonomous political actors remain M5 work; the three-depth political design is permitted structure, not a requirement to implement every level now. Runtime implementation remains a subsequent governed feature.

## Still open or partially resolved for political work

**Q-02 — Durable known-vessel identity — OPEN.** Should an observer's remembered vessel identity eventually be distinct from a sensor track? How should a report about a never-locally-observed ship be represented? Strategic Contact Reporting reuses observer-local `SensorContactId` and does not approve `KnownShipId`. The approved faction slice may reference its own controlled ships by real `ShipInstanceId`; that is administrative identity, not external vessel correlation. Cross-observer identity remains open for a consumer that needs it.

**Q-03 — Strategic knowledge and affiliation — OPEN.** What facts can an observer learn, from which sources, and at which spatial scale? Current scans learn vessel/design names, not allegiance. True direct control in the approved faction slice does not expose a controller through sensors, AI inputs about external ships, or player projections. Affiliation, intent, transponder/communication intelligence, and confidence models remain open.

**Q-04 — Knowledge ownership and sharing — PARTIALLY RESOLVED.** For the first faction-assignment slice only, a faction receives current administrative facts needed to assign its own directly controlled ships: identity, strategic state, and assignment/order status. This is an explicit decision-time simplification, not a communications or intelligence network. No ship-local contacts, scans, external reports, enemy state, or other faction objectives enter through this permission. The [owning decision](faction-intent-and-autonomous-assignment.md) defines the complete narrow boundary.

Sensor-report propagation, granularity/delay, never-locally-observed reports, cross-observer correlation, superior/subordinate or allied sharing, and what reaches the player remain open. Do not mark all of Q-04 resolved or use direct control to imply automatic intelligence ingestion.

**Q-06 — Era and region for initial content — SCOPED CHOICE MADE; CAMPAIGN QUESTION OPEN.** The first faction-assignment proof uses era-neutral development actors and existing fictional locations. It does not select a canonical campaign year or region. In particular, 2378 remains an archived assistant proposal. Resolve campaign era only when actual campaign content needs it.

## When political presentation and government mechanics become consumers

**Q-07 — Atypical hierarchy labels — OPEN.** How should the UI label a polity-wide internal movement directly under a root? Direct parentage, derived depth, three initial depths, and role/depth separation are settled. Exact display wording must not turn Polity/Constituent/Internal into restrictive actor types. The first faction slice adds no political/affiliation UI or hierarchy runtime.

**Q-08 — Minimal authority and consequence rules — BROADER QUESTION OPEN.** The first faction slice permits only direct-controller assignment of idle NPC assets, excludes the player ship, and does not preempt existing orders or physical travel. This narrow command rule does not answer sanctioned/unsanctioned political action, inherited obligations, treaty precedence, detailed constitutional exceptions, or discovery consequences. Refine those only when an interaction requires them.

**Q-09 — Institutional control and political transitions — OPEN.** How will the first organization or ruling faction influence decisions without replacing the polity itself? Charter versus control, complex succession, mergers, dissolution, and contested jurisdiction require consumers. Stable identity, mutable parentage, and government/state separation are already approved. A faction-only direct-controller field for the first slice does not revoke future organization-controlled assets.

## Before combat and durable incidents

**Q-10 — Tactical scope — OPEN.** Select the smallest useful shield/weapon/damage model and disengagement rules. Resolve interactions with power, observation, geometry, and existing orders before introducing a broad weapon catalog. M6's directed-energy suggestion is not a final weapon selection.

**Q-11 — Political memory — OPEN.** Choose which incidents must persist, how responsibility becomes known/disputed, and how history remains bounded while affecting later decisions. A faction decision explanation is not automatically durable political memory. Do not implement an unbounded event log or replace diplomacy with one global reputation number.

## Before campaign generation or broader simulation

**Q-12 — Canon divergence and initialization — OPEN.** Refine which facts are fixed at campaign start, which later canonical events are pressures rather than guarantees, and whether normal-simulation warm-up improves the start. Era-neutral faction bootstrap does not select these campaign policies.

**Q-13 — Resources, officers, and trade — OPEN.** Define the first useful logistics or crew interaction before selecting economic resource catalogs, officer progression formulas, markets, repair staffing, or fuel models. Existing ship commitment supplies the first faction proof's constraint without introducing an economy. Preserve captain-without-levels and the current abstract Engineering model until governed work changes them.

**Q-14 — Compatibility and stochastic behavior — PARTIALLY RESOLVED.** The approved faction implementation plans save V7 with the adjacent V1→V7 migration chain. V6→V7 adds zero factions, null ship controllers, no faction decision state, and no faction wakes; existing ship work retains equivalent typed Ship targets, identities, timing, and ordering. Zero-faction worlds remain valid. New-game bootstrap does not run during migration. The first faction policy consumes no randomness.

V6 remains the current runtime save schema until implementation. Broader development-save support and selection of a versioned random algorithm remain open. Choose and validate an algorithm only when a genuine random consumer arrives. No migration may invent allegiance, treaty history, reports, control, or cross-observer identity. The V5→V6 migration's refusal to derive historical observation locations remains the preceding example of that rule.

## Approved implementation boundaries and remaining discretion

The first faction consumer now approves a closed typed `Ship | Faction` scheduler target under ADR 0007; it is no longer merely an unapproved proposal. Faction starts/control links/wakes use typed bootstrap and complete candidate validation. Root-only scope, no generic actor/controller framework, no faction intelligence sharing, and no new faction/affiliation UI are explicit guardrails.

Exact runtime class spelling, faction definition layout, deterministic ranking details, wake/reassessment cadence, and numeric input/work bounds must be selected against current code and proven by the implementation tests. These are not permission to change the approved behavior. Safe reparenting, broader controller types, and faction knowledge propagation remain later questions.

If implementation requires crossing a deferred boundary to satisfy the approved exit condition, return to governed design refinement rather than choosing silently.

## Sources

[Roadmap](../../ROADMAP.md), [political discussion issue #68](https://github.com/L3DigitalNet/star-trek-alter-course/issues/68), [Strategic Contact Reporting decision issue #74](https://github.com/L3DigitalNet/star-trek-alter-course/issues/74), [local knowledge](sensors-knowledge-and-ai.md), [Strategic Contact Reporting](strategic-contact-reporting.md), [political framework](factions-and-organizations.md), and the owner's September 6, 2026 approval recorded in [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md).
