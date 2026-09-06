---
schema_version: '1.1'
id: 'index-5oz149-wiki'
title: 'Star Trek Alter Course Design Wiki'
description: 'Navigable synthesis of project decisions, implemented systems, future design, and authoritative references.'
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
  - 'docs/wiki/open-questions.md'
---

# Star Trek: Alter Course design wiki

## Purpose and baseline

This wiki is the central reading surface for the game: what it is intended to become, what the code actually does, which decisions are settled, and which questions remain open. It consolidates the existing source material without replacing its detailed contracts.

The initial implementation review is against `dev` commit `42481ca7fbc6c5c9da96985e02565f78a236cab7`, reviewed September 6, 2026. The current released gameplay baseline is source-only v0.4.0, First Contact & Engineering Backbone. This wiki change implements no gameplay and creates no new release.

## Read by topic

- [Vision and scope](vision-and-scope.md): captain-level play, persistent consequences, simulation priorities, and non-goals.
- [Implementation status](implementation-status.md): implemented, preview-only, and absent systems; milestone and release boundaries.
- [Architecture](architecture.md): authority, project boundaries, dependencies, testing, and the ADR map.
- [World, navigation, and time](world-navigation-and-time.md): ship identity, bootstrap, strategic orders, tactical space, and scheduling.
- [Sensors, knowledge, and AI](sensors-knowledge-and-ai.md): actor-local observations, scan/hail, explainable decisions, and unresolved strategic knowledge.
- [Engineering and combat](engineering-and-combat.md): the implemented power/condition/repair model and the planned combat integration.
- [Factions and organizations](factions-and-organizations.md): owner-approved political framework from the September 6 discussion; not yet implemented.
- [Diplomacy, economy, and campaigns](diplomacy-economy-and-campaigns.md): political consequences, history, trade, canon, and later campaign work.
- [Interface and player commands](interface-and-player-commands.md): Command Deck, Engineering, presentation authority, controls, and preview boundaries.
- [Content, assets, and persistence](content-assets-and-persistence.md): JSON definitions, V5 snapshots, migration, and the independent AssetCtl pipeline.
- [Development and governance](development-and-governance.md): toolchain, quality gate, branch/release workflow, agent guidance, and legal references.

For decisions rather than systems, use the [decision register](decision-register.md). For unfinished design, use [open questions](open-questions.md). The [source catalog](sources.md) indexes the original documents and implementation evidence.

## Status vocabulary

**Implemented** means the reviewed source has the behavior; supporting tests or release evidence are linked where available. **Approved design, not implemented** means the owner or an active ADR has selected a direction, not that a corresponding runtime type exists. **Planned** means the roadmap or a specification describes a future slice. **Proposed/open** means an option was discussed but not approved. **Historical** means a record explains earlier intent or an earlier implementation boundary.

A document frontmatter value of `status: active` describes the document, not feature completion. A mockup, selected package candidate, accepted political principle, or future milestone is not evidence of implemented gameplay.

## Authority and change discipline

Active ADRs govern architecture. Detailed system designs and specifications govern their stated scope. Implementation claims must be checked against source and tests; documentation disagreement is a defect to reconcile, not permission to silently change an ADR. The roadmap describes sequence and scope, not final acceptance criteria for every system.

The new political decisions are owned by [Factions and organizations](factions-and-organizations.md). Other wiki pages summarize or link them rather than redefining them. Assistant recommendations that the owner did not approve remain in the open-question register. Existing archived conversations remain historical evidence, not a blanket approval of every assistant suggestion.

When a system changes, update its owning design and implementation evidence, then the relevant wiki summary and [implementation status](implementation-status.md). Change a decision explicitly rather than silently overwriting its meaning. Keep operational handoff state in its existing files and keep the wiki focused on durable knowledge. Use repository-relative links; do not copy entire specifications into parallel wiki versions.
