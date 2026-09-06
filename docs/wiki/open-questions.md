---
schema_version: '1.1'
id: 'plan-o0oaje-open-questions'
title: 'Open Design Questions'
description: 'Deferred high-level choices and unapproved proposals organized by the next concrete consumer.'
doc_type: 'plan'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'design'
aliases: []
related:
  - 'docs/wiki/decision-register.md'
  - 'docs/wiki/factions-and-organizations.md'
  - 'ROADMAP.md'
---

# Open design questions

[Wiki home](README.md) · [Approved decisions](decision-register.md) · [Current implementation](implementation-status.md)

## How to use this register

The owner stopped refinement to avoid premature detail. These questions preserve unfinished choices; they are not a request to answer everything now, a new implementation backlog, or permission for an agent to choose silently. Resolve only what blocks the next governed vertical slice. The political principles in [Factions and organizations](factions-and-organizations.md) are settled and should not be repeatedly re-asked.

## Before admitting the next knowledge or political slice

**Q-01 — Scope and sequence.** Should the next feature be a bounded strategic-knowledge/faction-identity prerequisite, a minimal living-sector feature, or a different division? The proposed name M3B and M3B→M5→M6 sequence remain recommendations. M3A is implemented, M3 as a whole remains incomplete, and the roadmap's next major listed milestone is M5.

**Q-02 — Durable known-vessel identity.** Should an observer's remembered vessel identity be distinct from a sensor track? How should a report about a never-locally-observed ship be represented? A separate `KnownShipId` was proposed, not approved; the current implementation uses observer-local `SensorContactId` and hidden Core target correlation.

**Q-03 — Strategic knowledge and affiliation.** What facts can the first observer learn, from which sources, at which spatial scale? Does scanning establish affiliation, do communications supply it, or is it initially reported/inferred? The current scan learns names, not allegiance. Distinguish true control/affiliation from what an observer knows, without selecting a confidence model prematurely.

**Q-04 — Knowledge ownership and sharing.** What information does a faction receive from its ships or organizations, and what reaches the player? Actor-appropriate information is already an ADR requirement; the reporting/distribution mechanism, delay, and granularity are not selected. The explicit-report proposal must not become an unreviewed intelligence network.

**Q-05 — Smallest political gameplay proof.** Which actors, one objective/resource constraint, and one offscreen interaction best demonstrate actual choice? The three-depth political design is a permitted structure, not a requirement that every depth/type be fully simulated in the first feature.

**Q-06 — Era and region for initial content.** Select a development reference epoch or explicitly use an era-neutral test scenario before writing political assumptions into content. No specific campaign year has been approved here; 2378 remains an archived assistant proposal.

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

**Q-14 — Compatibility and stochastic behavior.** Select and validate a versioned random algorithm when a genuine random consumer arrives. Decide supported development-save compatibility when new durable political state is introduced. Do not invent allegiance, treaty history, or knowledge during migration merely to satisfy a new schema.

## Implementation questions that are not product approvals

Faction-owned decision wakes may require extending the current ship-only scheduler target; a closed typed Ship/Faction target is one proposal, not a mandatory framework. Reference-frame-qualified strategic observations, efficient knowledge retention, safe reparenting, and input/work bounds must be resolved against concrete code and the active ADRs. Avoid speculative schemas or a generic actor/rules engine while these consumers are absent.

## Sources

This register combines unresolved topics from the [roadmap](../../ROADMAP.md), the current conversation recorded under [issue #68](https://github.com/L3DigitalNet/star-trek-alter-course/issues/68), and the boundary between implemented [local knowledge](sensors-knowledge-and-ai.md) and the approved [political design](factions-and-organizations.md).
