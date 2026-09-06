---
schema_version: '1.1'
id: 'plan-o0oaje-open-questions'
title: 'Open Design Questions'
description: 'Deferred high-level choices organized by the next concrete consumer; Q-01 is resolved by Strategic Contact Reporting.'
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
  - 'docs/wiki/factions-and-organizations.md'
  - 'ROADMAP.md'
---

# Open design questions

[Wiki home](README.md) · [Approved decisions](decision-register.md) · [Strategic Contact Reporting](strategic-contact-reporting.md) · [Current implementation](implementation-status.md)

## How to use this register

The owner stopped refinement to avoid premature detail. These questions preserve unfinished choices; they are not a request to answer everything now, a new implementation backlog, or permission for an agent to choose silently. Resolve only what blocks the next governed vertical slice. The political principles in [Factions and organizations](factions-and-organizations.md) are settled and should not be repeatedly re-asked.

## Resolved sequencing decision

**Q-01 — Scope and sequence — RESOLVED September 6, 2026.** The next approved development slice is [Strategic Contact Reporting](strategic-contact-reporting.md): a bounded bridge from M3A's local observer knowledge to durable, reference-frame-qualified strategic last-known contact information. It is intentionally implemented before the broader M5 Living Sector/faction-autonomy proof so that later political AI does not need to invent its information seam at the same time as faction state and decision logic.

The slice is **not** canonically named `M3B`. It does not itself complete Milestone 3 or begin Milestone 5; historical milestone classification can be decided after implementation. The previously proposed `M3B→M5→M6` label/sequence is therefore superseded as a planning recommendation by this narrower approved decision, without approving any of the previously proposed identity or faction-sharing mechanisms.

## Still open around Strategic Contact Reporting and later political work

**Q-02 — Durable known-vessel identity.** Should an observer's remembered vessel identity eventually be distinct from a sensor track? How should a report about a never-locally-observed ship be represented? Strategic Contact Reporting deliberately reuses observer-local `SensorContactId` as far as it remains sufficient and does **not** approve `KnownShipId`. Cross-observer correlation and never-observed reports remain open for the first consumer that needs them.

**Q-03 — Strategic knowledge and affiliation.** What facts can an observer learn, from which sources, and at which spatial scale? Strategic Contact Reporting approves retention of legitimate last-known observation facts but does not change identification semantics: the current scan learns vessel/design names, not allegiance. Affiliation, controller, intent, transponder/communication intelligence, and confidence models remain open.

**Q-04 — Knowledge ownership and sharing.** What information does a faction receive from its ships or organizations, by what mechanism, with what delay/granularity, and what reaches the player? Strategic Contact Reporting creates an actor-safe report/projection seam for the observing ship/player knowledge boundary only. It does not approve automatic faction ingestion or an intelligence network.

**Q-05 — Smallest political gameplay proof.** Which actors, one objective/resource constraint, and one offscreen interaction best demonstrate actual choice in M5? The three-depth political design is a permitted structure, not a requirement that every depth/type be fully simulated in the first feature. Resolve this after Strategic Contact Reporting provides the information seam M5 can consume.

**Q-06 — Era and region for initial content.** Select a development reference epoch or explicitly use an era-neutral test scenario before writing political assumptions into content. No specific campaign year has been approved here; 2378 remains an archived assistant proposal. Strategic Contact Reporting should reuse existing proof content where practical and does not require resolving campaign era.

## When political presentation and government mechanics become consumers

**Q-07 — Atypical hierarchy labels.** How should the UI label a polity-wide internal movement directly under a root? Direct parentage, derived depth, three initial depths, and role/depth separation are settled. Exact display wording should not turn Polity/Constituent/Internal into restrictive actor types.

**Q-08 — Minimal authority and consequence rules.** Which small set of sanctioned/unsanctioned actions, inherited obligations, and discovery consequences is required by the first interaction? Leave permission matrices, treaty precedence, detailed constitutional exceptions, and espionage probability/resource systems deferred until justified.

**Q-09 — Institutional control and political transitions.** How will the first organization or ruling faction influence decisions without replacing the polity itself? Charter versus control, complex succession, mergers, dissolution, and contested jurisdiction need refinement only when a selected scenario requires them. Stable identity, mutable parentage, and government/state separation are already approved.

## Before combat and durable incidents

**Q-10 — Tactical scope.** Select the smallest useful shield/weapon/damage model and disengagement rules. Resolve interactions with power, observation, geometry, and existing orders before introducing a broad weapon catalog. M6's directed-energy suggestion is not a final weapon selection.

**Q-11 — Political memory.** Choose which incidents must persist, how responsibility becomes known/disputed, and how history remains bounded while still affecting later decisions. Do not implement an unbounded event log or replace diplomacy with one global reputation number by default.

## Before campaign generation or broader simulation

**Q-12 — Canon divergence and initialization.** Refine which facts are fixed at campaign start, which later canonical events are pressures rather than guarantees, and whether normal-simulation warm-up improves the start. The roadmap's preference for legitimate divergence is provisional at this level of detail.

**Q-13 — Resources, officers, and trade.** Define the first useful logistics or crew interaction before selecting economic resource catalogs, officer progression formulas, markets, repair staffing, or fuel models. Preserve captain-without-levels and the existing abstract Engineering model until a governed feature changes them.

**Q-14 — Compatibility and stochastic behavior.** Select and validate a versioned random algorithm when a genuine random consumer arrives. Decide supported development-save compatibility when new durable political state is introduced. Strategic Contact Reporting may require an adjacent save migration if new authoritative observation state cannot be reconstructed safely, but that is an implementation outcome rather than permission to invent historical knowledge. Do not invent allegiance, treaty history, reports, or cross-observer identity merely to satisfy a new schema.

## Implementation questions that are not product approvals

Strategic Contact Reporting requires reference-frame-qualified last-known observations but does not prescribe the exact runtime type. Efficient knowledge retention and input/work bounds must be resolved against the current M3A code and active ADRs. Faction-owned decision wakes may later require extending the current ship-only scheduler target; a closed typed Ship/Faction target remains only one proposal, not a mandatory framework. Safe reparenting and faction knowledge propagation also remain later questions.

Avoid speculative schemas or a generic actor/rules engine while these consumers are absent. If implementation discovery for Strategic Contact Reporting proves that one of Q-02 through Q-04 is actually required to meet that slice's approved exit condition, stop and return to governed design refinement rather than choosing silently.

## Sources

This register combines unresolved topics from the [roadmap](../../ROADMAP.md), the political discussion recorded under [issue #68](https://github.com/L3DigitalNet/star-trek-alter-course/issues/68), the approved next-slice decision recorded under [issue #74](https://github.com/L3DigitalNet/star-trek-alter-course/issues/74), and the boundary between implemented [local knowledge](sensors-knowledge-and-ai.md), approved [Strategic Contact Reporting](strategic-contact-reporting.md), and the approved [political design](factions-and-organizations.md).
