---
schema_version: '1.1'
id: 'index-5oz149-wiki'
title: 'Star Trek Alter Course Design Wiki'
description: 'Single source of truth for the design of the game: decisions, implemented systems, future design, and references.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'design'
  - 'architecture'
aliases: []
related:
  - 'ROADMAP.md'
  - 'docs/STATUS.md'
  - 'docs/wiki/decision-register.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/open-questions.md'
---

# Star Trek: Alter Course design wiki

## Purpose and baseline

This wiki is the single source of truth for the design of the game: what it is intended to become, what the code actually does, which decisions are settled, and which questions remain open. Every other design document in the repository is either supporting detail linked from a wiki page or historical evidence; none of them may contradict the wiki.

The initial implementation review is against `dev` commit `42481ca7fbc6c5c9da96985e02565f78a236cab7`, reviewed September 6, 2026. The current released gameplay baseline is source-only v0.4.0, First Contact & Engineering Backbone. Strategic Contact Reporting has since landed on `dev` (Feature #77, Final PR #78); it does not complete Milestone 3 and does not begin Milestone 5, and no new numbered release has been cut for it.

## Read by topic

- [Vision and scope](vision-and-scope.md): captain-level play, persistent consequences, simulation priorities, and non-goals.
- [Implementation status](implementation-status.md): implemented, preview-only, and absent systems; milestone and release boundaries.
- [Architecture](architecture.md): authority, project boundaries, dependencies, testing, and the ADR map.
- [World, navigation, and time](world-navigation-and-time.md): ship identity, bootstrap, strategic orders, tactical space, and scheduling.
- [Sensors, knowledge, and AI](sensors-knowledge-and-ai.md): actor-local observations, scan/hail, explainable decisions, and the current information boundary.
- [Strategic Contact Reporting](strategic-contact-reporting.md): durable, reference-frame-qualified actor-safe last-known contact information.
- [Engineering and combat](engineering-and-combat.md): the implemented power/condition/repair model and the planned combat integration.
- [Factions and organizations](factions-and-organizations.md): owner-approved political framework from the September 6 discussion; not yet implemented.
- [Diplomacy, economy, and campaigns](diplomacy-economy-and-campaigns.md): political consequences, history, trade, canon, and later campaign work.
- [Interface and player commands](interface-and-player-commands.md): Command Deck, Engineering, presentation authority, controls, and preview boundaries.
- [Content, assets, and persistence](content-assets-and-persistence.md): JSON definitions, V6 snapshots, migration, and the independent AssetCtl pipeline.
- [Development and governance](development-and-governance.md): toolchain, quality gate, branch/release workflow, agent guidance, and legal references.

For decisions rather than systems, use the [decision register](decision-register.md). For unfinished design, use [open questions](open-questions.md). The [source catalog](sources.md) indexes the original documents and implementation evidence.

## Status vocabulary

**Implemented** means the reviewed source has the behavior; supporting tests or release evidence are linked where available. **Approved design, not implemented** means the owner or an active ADR has selected a direction, not that a corresponding runtime type exists. **Planned** means the roadmap or a specification describes a future slice. **Proposed/open** means an option was discussed but not approved. **Historical** means a record explains earlier intent or an earlier implementation boundary.

A document frontmatter value of `status: active` describes the document, not feature completion. A mockup, selected package candidate, accepted political principle, approved next slice, or future milestone is not evidence of implemented gameplay.

## Authority and change discipline

This wiki governs design. When a wiki page and any other design document, roadmap passage, archived conversation, or agent instruction disagree about design, the wiki is correct and the other document is a defect to fix. Active ADRs remain the record of architectural decisions: an architectural boundary changes only through a new or amended ADR, and the wiki then reflects that decision. Implementation claims are checked against source and tests. The roadmap describes sequence and scope, not design, and defers to this wiki wherever it describes a system.

The Strategic Contact Reporting decision is owned by [Strategic Contact Reporting](strategic-contact-reporting.md). The political decisions are owned by [Factions and organizations](factions-and-organizations.md). Other wiki pages summarize or link them rather than redefining them. Assistant recommendations that the owner did not approve remain in the open-question register.

Record a design change on its wiki page in the same governed work that implements it. Then update any supporting document the page links so its detail matches, and update [implementation status](implementation-status.md). Change a decision explicitly rather than silently overwriting its meaning. Keep operational handoff state in its existing files and keep the wiki focused on durable knowledge. Use repository-relative links. Supporting documents under `docs/design/` and `docs/specs/` hold detailed rules, formulas, and contracts that a wiki page would only restate; they do not introduce design the wiki does not record.
