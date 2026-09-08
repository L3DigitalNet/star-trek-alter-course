---
schema_version: '1.1'
id: 'index-5oz149-wiki'
title: 'Star Trek Alter Course Design Wiki'
description: 'Single source of truth for the design of the game: decisions, implemented systems, future design, and references.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-08'
tags:
  - 'design'
  - 'architecture'
aliases: []
related:
  - 'ROADMAP.md'
  - 'docs/STATUS.md'
  - 'docs/wiki/decision-register.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/observation-driven-faction-response.md'
  - 'docs/wiki/open-questions.md'
---

# Star Trek: Alter Course design wiki

## Purpose and baseline

This wiki is the single source of truth for the design of the game: what it is intended to become, what the code actually does, which decisions are settled, and which questions remain open. Detailed game rules and milestone acceptance contracts live here. [Documents outside the wiki](../README.md) have distinct architectural, operational, onboarding, or legal roles; they do not introduce additional gameplay rules.

The initial implementation review is against `dev` commit `42481ca7fbc6c5c9da96985e02565f78a236cab7`, reviewed September 6, 2026. The current released gameplay baseline is source-only [v0.6.0](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.6.0), Faction Observation and Response, at `d00460e`; it includes both Feature #86 faction assignment and Feature #93 observation response. Strategic Contact Reporting remains the v0.5.0 historical V6 release; it does not complete Milestone 3 and does not itself begin Milestone 5.

[Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) is the first implemented contribution toward M5, delivered by Feature #86 / Final PR #87 and merged into `dev` as `0217296`. Its era-neutral proof implements the six selected boundary decisions and introduced V7 saves with faction content V1. The v0.6.0 release uses V8; v0.5.0 remains the historical V6 release and ship-definition content remains V4. This does not declare M3 or M5 complete.

[Observation-Driven Faction Response](observation-driven-faction-response.md) is implemented by Feature #93 / Final PR #94 and released in v0.6.0. V8 uses `observation-driven-faction-response-v1`; v0.5.0 remains the historical V6 release. It connects legitimate NPC sensor observations to delayed direct-faction reports and deterministic investigation assignments while preserving observer-local identity, information limits, existing orders, and bounded persistence. Its reviewed evidence includes Core scenario/horizon coverage, actor-safe Godot coverage, and the V8 persistence bound. M6 Tactical Combat Foundation is the next major development family; full M3 or M5 completion is not a prerequisite for beginning the first bounded combat engagement.

The [recurring design-reconciliation procedure](development-and-governance.md#recurring-design-reconciliation) governs review of these claims. The [source catalog review record](sources.md#review-record) identifies the latest full and targeted reviews.

## Start here

- [Vision and scope](vision-and-scope.md): captain-level play, persistent consequences, simulation priorities, and non-goals.
- [Implementation status](implementation-status.md): implemented, preview-only, approved-not-implemented, and absent systems; milestone and release boundaries.
- [Milestone proofs](milestone-proofs.md): detailed acceptance criteria for completed and future milestones; [the roadmap](../../ROADMAP.md) owns sequence.

## Implemented systems and their contracts

- [World, navigation, and time](world-navigation-and-time.md): ship identity, bootstrap, strategic orders, tactical space, and scheduling.
- [Sensors, knowledge, and AI](sensors-knowledge-and-ai.md): actor-local observations, scan/hail, explainable decisions, and current versus approved information boundaries.
- [Strategic Contact Reporting](strategic-contact-reporting.md): implemented durable, reference-frame-qualified actor-safe last-known contact information.
- [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md): implemented bounded M5 contribution: direct NPC assignment, own-asset knowledge, typed scheduling, V7 migration, and no RNG.
- [Engineering and combat](engineering-and-combat.md): the implemented power/condition/repair model and the planned combat integration.
- [Interface and player commands](interface-and-player-commands.md): Command Deck, Engineering, presentation authority, controls, and preview boundaries.
- [Content, assets, and persistence](content-assets-and-persistence.md): JSON definitions, released V8 snapshots, historical adjacent migrations, and the independent AssetCtl pipeline.

## Implemented response and future design

- [Observation-Driven Faction Response](observation-driven-faction-response.md): implemented delayed direct ship-to-faction reporting and bounded investigation response with bounded V8 persistence proof.
- [Factions and organizations](factions-and-organizations.md): owner-approved political framework; the root-faction/direct-control subset is implemented while broader political runtime remains future work.
- [Diplomacy, economy, and campaigns](diplomacy-economy-and-campaigns.md): political consequences, history, trade, canon, and later campaign work.
- [Decision register](decision-register.md): settled decisions and their owning pages.
- [Open questions](open-questions.md): unresolved choices and the consumers that would justify resolving them.

## Architecture, development, and evidence

- [Architecture](architecture.md): authority, project boundaries, dependencies, testing, and the ADR map.
- [Development and governance](development-and-governance.md): toolchain, quality gate, branch/release workflow, agent guidance, and legal references.
- [Asset pipeline tool](asset-pipeline-tool.md): the full development-tool contract, separately labeled from gameplay and implementation status.
- [Source catalog](sources.md): implementation evidence, consolidation map, historical provenance, and dated review coverage.

Read the system page first and follow its specialized contract links when needed. Strategic Contact Reporting owns last-known-report rules; Faction Intent and Autonomous Assignment owns first-slice assignment rules; Observation-Driven Faction Response owns the implemented direct report-delivery/investigation extension. Their summary links elsewhere do not redefine those contracts.

## Status vocabulary

**Implemented** means the reviewed source has the behavior; supporting tests or release evidence are linked where available. **Approved design, not implemented** means the owner or an active ADR has selected a direction, not that a corresponding runtime type exists. **Planned** means the roadmap or a specification describes a future slice. **Proposed/open** means an option was discussed but not approved. **Historical** means a record explains earlier intent or an earlier implementation boundary.

A document frontmatter value of `status: active` describes the document, not feature completion. A mockup, selected package candidate, accepted political principle, approved next slice, or future milestone is not evidence of implemented gameplay.

## Authority and change discipline

This wiki governs design. When a wiki page and any other design document, roadmap passage, archived conversation, or agent instruction disagree about design, the wiki is correct and the other document is a defect to fix. Active ADRs remain the record of architectural decisions: an architectural boundary changes only through a new or amended ADR, and the wiki then reflects that decision. Implementation claims are checked against source and tests. The roadmap describes sequence and scope, not design, and defers to this wiki wherever it describes a system.

The released contact-reporting decision is owned by [Strategic Contact Reporting](strategic-contact-reporting.md). The implemented first M5 slice is owned by [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md). The implemented information-to-action slice is owned by [Observation-Driven Faction Response](observation-driven-faction-response.md). The broader political principles remain owned by [Factions and organizations](factions-and-organizations.md). Other pages summarize or link these records rather than redefining them. Assistant recommendations that the owner did not approve remain in the open-question register.

Record a design change on its wiki page in the same governed work that implements it. Keep detailed rules, formulas, state transitions, edge cases, and acceptance contracts inside the wiki, and update [implementation status](implementation-status.md) when behavior lands. Change a decision explicitly rather than silently overwriting its meaning. Keep operational handoff state in its existing files. Do not recreate a parallel design/specification tree; implementation plans and issues link the owning wiki contracts.

A useful system page explains purpose and current scope, state and invariants, legal actions and rejection behavior, time/order semantics, actor-knowledge and presentation boundaries, persistence implications, proof values where material, and source/test evidence. Cover only the headings that the system needs. Future sections explicitly distinguish approved intent from unanswered choices; they do not need invented implementation detail. Split a long specialized contract into another linked wiki page when it has a distinct consumer, preserving existing topic URLs where possible.
