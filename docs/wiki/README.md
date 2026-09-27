---
schema_version: '1.1'
id: 'index-5oz149-wiki'
title: 'Star Trek Alter Course Design Wiki'
description: 'Single source of truth for game design, with current implementation, architectural decisions, and unresolved choices clearly separated.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-27'
tags:
  - 'design'
  - 'architecture'
aliases: []
related:
  - 'ROADMAP.md'
  - 'docs/STATUS.md'
  - 'docs/adr/README.md'
  - 'docs/wiki/decision-register.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/observation-driven-faction-response.md'
  - 'docs/wiki/open-questions.md'
  - 'docs/wiki/ship-system-substrate.md'
---

# Star Trek: Alter Course design wiki

## Purpose and baseline

This wiki is the single source of truth for game design: intended behavior, reviewed implementation, approved decisions, and open questions. Detailed rules and milestone acceptance contracts live here. [Documents outside the wiki](../README.md) have distinct architectural, operational, onboarding, or legal roles; they do not introduce competing gameplay rules.

The current source-only release is [v0.6.2](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.6.2), at `255eaedc8e27b483b0fd4e2fe0bccf050486b3bf`. Its gameplay baseline remains [v0.6.0 — Faction Observation and Response](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.6.0), using V8 saves. Development additionally contains unreleased M6A combat and the completed installed-system migration, using ship content V6, system-definition content V1, and V10 saves. M3, M5, and M6 remain partial; no packaged game artifact is published.

Use [Implementation status](implementation-status.md) for the release/development distinction and verification evidence, [Content, assets, and persistence](content-assets-and-persistence.md) for format compatibility, and the [roadmap](../../ROADMAP.md) for sequence. Historical V5/V9 M6A and V6/V7/V8 faction/reporting admissions are not the current development format.

The [recurring reconciliation procedure](development-and-governance.md#recurring-design-reconciliation) governs review. The [source catalog](sources.md#review-record) records the initial September 6 baseline, later full and targeted reviews, their limitations, and the next full-sweep date.

## Implemented substrate migration (development branch)

[Issue #121](https://github.com/L3DigitalNet/star-trek-alter-course/issues/121) completed the [ship-system substrate migration](ship-system-substrate.md) required by [ADR 0014](../adr/0014-use-an-extensible-bounded-ship-system-substrate.md), through Final PR #123. Runtime, content, persistence, and generic Engineering presentation are implemented and unreleased. Read that contract before changing ship systems.

Recovery, refit gameplay, and multi-instance aggregation remain separate future decisions. The migration is not M6B or a release, and earlier recovery prompts are not the current work order. ADRs 0015–0018 formalize command, information, asset, and session boundaries; they do not select the next gameplay slice.

## Start here

- [Vision and scope](vision-and-scope.md): captain-level play, persistent consequences, simulation priorities, and non-goals.
- [Implementation status](implementation-status.md): implemented, preview-only, approved-not-implemented, and absent systems.
- [Milestone proofs](milestone-proofs.md): detailed acceptance criteria; [the roadmap](../../ROADMAP.md) owns sequence.

## Implemented systems and their contracts

- [World, navigation, and time](world-navigation-and-time.md): identity, bootstrap, strategic orders, tactical space, and scheduling.
- [Sensors, knowledge, and AI](sensors-knowledge-and-ai.md): local observations, scan/hail, explainable decisions, and information boundaries.
- [Strategic Contact Reporting](strategic-contact-reporting.md): durable, reference-frame-qualified last-known information.
- [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md): direct NPC assignment, own-asset administrative knowledge, and bounded continuation.
- [Observation-Driven Faction Response](observation-driven-faction-response.md): delayed historical reports and deterministic investigation through ordinary orders and sensing.
- [Engineering and combat](engineering-and-combat.md): power, condition, repairs, and bounded first-engagement rules.
- [Ship-system substrate](ship-system-substrate.md): installed-system identity, heterogeneous live loadouts, compatibility, and generic common mechanics.
- [Interface and player commands](interface-and-player-commands.md): Command Deck, Engineering, Combat, controls, session lifetime, and preview boundaries.
- [Content, assets, and persistence](content-assets-and-persistence.md): definitions, snapshots, migrations, and the independent AssetCtl pipeline.

## Implemented response and future design

The faction assignment and observation-response contracts are bounded implemented contributions toward M5, not the complete political model. Strategic Contact Reporting retains its original scope; it is not a renamed M3B and did not itself introduce faction autonomy.

- [Factions and organizations](factions-and-organizations.md): owner-approved political principles; hierarchy and organizations remain future runtime work.
- [Diplomacy, economy, and campaigns](diplomacy-economy-and-campaigns.md): later political consequences, history, trade, and campaign work.
- [Decision register](decision-register.md): stable approved decision IDs and owning contracts.
- [Open questions](open-questions.md): unresolved choices and the concrete consumers that would justify resolving them.

## Architecture, development, and evidence

- [Architecture](architecture.md) and the [ADR catalog](../adr/README.md): authority, dependencies, command commitment, information ownership, asset publication, and session boundaries.
- [Development and governance](development-and-governance.md): toolchain, quality gate, branch/release workflow, and documentation discipline.
- [Asset pipeline tool](asset-pipeline-tool.md): detailed development-tool contract, not proof that every planned provider operation exists.
- [Source catalog](sources.md): source/test entry points, consolidation map, historical provenance, and dated coverage.

Read a system's owning page first. Summaries link contracts rather than redefining them; the substrate contract does not duplicate M6A formulas, and an architectural record does not turn deferred design into implemented gameplay.

## Status vocabulary

**Implemented** means reviewed source provides the behavior, with linked tests or release evidence where available. **Approved design, not implemented** means a direction was selected but corresponding runtime behavior is not established. **Planned** describes a future slice. **Proposed/open** has not been approved. **Historical** explains earlier intent or an earlier implementation boundary.

Frontmatter `status: active` describes the document, not feature completion. An accepted ADR, mockup, preferred package candidate, or milestone proof is not evidence that its conditional future capabilities exist.

## Authority and change discipline

The wiki governs gameplay design; active ADRs govern architectural decisions. Change an architectural boundary through a new or amended ADR and reconcile the wiki in the same governed work. Resolve disagreement between intended behavior and source explicitly rather than silently rewriting either to match the other.

Keep rules, formulas, state transitions, edge cases, and acceptance contracts in the owning wiki page. Update implementation claims when behavior lands. Keep operational facts in STATUS/TODO/handoff, and preserve historical decisions through stable IDs and fixed-revision references. Do not recreate a parallel design/specification tree.

A useful system page covers purpose and current scope, state and invariants, actions and refusal behavior, timing/order, actor knowledge, persistence, and evidence. Use only the sections the system needs. Split a specialized contract when it has a distinct consumer, preserving established topic URLs and anchors. Future sections separate approved intent from unanswered choices instead of inventing implementation detail.
