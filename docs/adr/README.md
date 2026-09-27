---
schema_version: '1.1'
id: 'index-s8f15p-architecture-decision-records'
title: 'Architecture Decision Records'
description: 'Catalog, scope map, and maintenance guidance for the active architectural decisions.'
doc_type: 'index'
status: 'active'
created: '2026-09-27'
updated: '2026-09-27'
tags:
  - 'architecture'
  - 'documentation'
aliases: []
related:
  - 'docs/README.md'
  - 'docs/adr/adr.template.md'
  - 'docs/wiki/architecture.md'
  - 'docs/wiki/decision-register.md'
  - 'docs/wiki/sources.md'
  - 'docs/reviews/adr-conformance-2026-09-27.md'
---

# Architecture decision records

[Documentation home](../README.md) · [Design wiki](../wiki/README.md) · [Architecture](../wiki/architecture.md) · [Decision register](../wiki/decision-register.md)

ADRs record significant architectural choices, their applicability, alternatives, rationale, and confirmation criteria. The wiki owns detailed gameplay contracts. Source, schemas, tests, and dated verification establish implementation; an active ADR is not proof that every conditional component it discusses exists.

## Active catalog

- [0001 — Separate simulation from Godot](0001-separate-simulation-from-godot.md): one-way assembly dependencies and independently testable Core behavior.
- [0002 — Use one canonical quality gate](0002-use-one-canonical-quality-gate.md): shared, pinned, fail-fast verification without tracked-file mutation.
- [0003 — Prefer native capabilities and demand-driven dependencies](0003-prefer-native-capabilities-and-demand-driven-dependencies.md): package admission and rejection of speculative infrastructure.
- [0004 — Own a semantic spatial model and adapt Godot rendering](0004-own-semantic-spatial-model-and-adapt-godot-rendering.md): distinct spatial scales, authoritative geometry, and actor-appropriate maps.
- [0005 — Use JSON and schema validation for domain content](0005-use-json-and-schema-validation-for-domain-content.md): ordinary content formats, stable identities, and strict admission.
- [0006 — Use versioned JSON snapshot saves](0006-use-versioned-json-snapshot-saves.md): explicit snapshot mapping, compatibility, validation, and file replacement.
- [0007 — Use deterministic simulation time, scheduling, and randomness](0007-use-deterministic-simulation-time-scheduling-and-randomness.md): one simulation timeline, stable work ordering, bounded advancement, and randomness admission when needed.
- [0008 — Use structured observability with Serilog](0008-use-structured-observability-with-serilog.md): composition-owned, nonauthoritative diagnostics.
- [0009 — Use layered testing and architecture conformance](0009-use-layered-testing-and-architecture-conformance.md): behavioral, scenario, property, architecture, and engine-test responsibilities.
- [0010 — Use explainable domain AI and demand-driven state machines](0010-use-explainable-domain-ai-and-demand-driven-state-machines.md): explicit actor inputs, decisions, constraints, and typed proposals.
- [0011 — Represent physical quantities with explicit units](0011-represent-physical-quantities-with-explicit-units.md): physical and fictional quantity boundaries, including the recorded bounded exception.
- [0012 — Keep branching narrative subordinate to simulation](0012-keep-branching-narrative-subordinate-to-simulation.md): conditional narrative integration without narrative-owned world authority.
- [0013 — Use dev for development and main for releases](0013-use-dev-for-development-and-main-for-releases.md): topic admission, release promotion, and purpose-specific history.
- [0014 — Use an extensible bounded ship-system substrate](0014-use-an-extensible-bounded-ship-system-substrate.md): kind, definition, installation, and typed shared mechanics.
- [0015 — Use staged Core command application and explicit commit outcomes](0015-use-staged-core-command-application-and-explicit-commit-outcomes.md): correlated candidate changes, atomic refusal, and truthful post-commit failure reporting.
- [0016 — Own actor knowledge and share immutable observation reports](0016-own-actor-knowledge-and-share-immutable-observation-reports.md): information production, provenance, retention, transfer, and indirect disclosure.
- [0017 — Generate assets outside the game through bounded validated publication](0017-generate-assets-outside-the-game-through-bounded-validated-publication.md): independent tooling, bounded external operations, recoverable publication, and approval authority.
- [0018 — Separate simulation-session lifetime from workspaces](0018-separate-simulation-session-lifetime-from-workspaces.md): shared session ownership, replacement, current action payloads, and preview isolation.

## Scope and overlap

Read the record that owns the changed concern, then its related decisions. Similar vocabulary does not make two records interchangeable.

**Authority and transactions:** ADR 0001 owns the assembly boundary; 0007 owns time, scheduling, and serialized mutation; 0015 owns command commitment and caller-visible outcomes. ADR 0015 preserves 0007's allowance for an explicitly specified safely incremental operation rather than silently making every future operation atomic. ADR 0006 owns durable file/snapshot behavior, not an in-memory command's transaction.

**Information:** ADR 0004 owns spatial projections and 0010 owns consequential AI. ADR 0016 owns the provenance and lifecycle of the knowledge they consume. Sensor formulas, report delays, capacities, investigation policy, and unresolved identity/affiliation choices remain in their owning wiki contracts.

**Assets:** ADR 0017 owns AssetCtl's isolation, side-effect, publication, and approval boundaries. ADRs 0002, 0003, 0005, 0008, and 0009 continue to own verification, admission, content-format boundaries, diagnostics, and testing. The detailed [tool contract](../wiki/asset-pipeline-tool.md) is not copied into the ADR.

**Presentation:** ADR 0018 owns session and action lifetime, not layout or a permanent `GameScreen` monolith. ADR 0014 owns installed-system identity; 0015 owns command outcomes; 0016 owns actor-safe information. The [interface contract](../wiki/interface-and-player-commands.md) owns controls, layout, styling, and interaction detail.

## Reading historical context and evidence

An accepted record's context describes the decision when made. For example, ADR 0014 describes the pre-migration representation; [Issue #121's contract](../wiki/ship-system-substrate.md) and [implementation status](../wiki/implementation-status.md) record the subsequently completed migration. Do not rewrite the original rationale merely because implementation has caught up.

The [September 27 conformance register](../reviews/adr-conformance-2026-09-27.md) assessed ADRs 0001–0014 and 191 obligations at its named baseline. It does not automatically certify ADRs 0015–0018. New records provide evidence entry points and confirmation criteria; actual execution and admission evidence belongs to the governing PR. The [source catalog](../wiki/sources.md#review-record) separates full semantic sweeps, targeted reviews, and historical runs.

## Adding or changing a record

Use the existing [ADR template](adr.template.md) and repository frontmatter standard. Allocate the next unused number, keep the accepted ID stable, and make the governed population, applicability conditions, exclusions, and unresolved adjacent choices explicit.

A new record is appropriate for a distinct consequential architectural choice. Prefer an existing owning record when the change is only a clarification of that subject. Package selection, exact tuning values, temporary work state, and detailed gameplay acceptance do not each require a new ADR.

Identify whether the work codifies existing approved practice, selects previously undecided architecture, or changes an accepted requirement. For an amendment, preserve the accepted outcome and add the standard amendment note with reciprocal `project.amends`/`project.amended_by` metadata where applicable. Supersession and amendment are different relationships; do not use either for a merely related supplementary record.

Reconcile the catalog, wiki architecture, decision/source registers, affected system pages, and project-owned guidance in the same governed work. Preserve stable D/P decision IDs and existing topic anchors. Do not edit managed skills, policy, or enforcement just to make the change pass.

For confirmation, identify concrete source and test entry points, then report what was inspected and what actually ran. Documentation adoption, a link check, or an inherited test count is not a fresh conformance result. Follow the existing readiness and merge workflow; architectural prose is not trivial direct-admission work.
