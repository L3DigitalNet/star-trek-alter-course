---
schema_version: '1.1'
id: 'reference-5qyi2p-adr-expansion-documentation-review-2026-09-27'
title: 'Additional ADRs and Documentation Review — September 27, 2026'
description: 'Records the scope, preservation boundaries, and verification limits of the owner-selected ADR expansion and documentation polish.'
doc_type: 'reference'
status: 'active'
created: '2026-09-27'
updated: '2026-09-27'
tags:
  - 'architecture'
  - 'documentation'
  - 'validation'
aliases: []
related:
  - 'docs/adr/README.md'
  - 'docs/wiki/sources.md'
  - 'docs/reviews/adr-conformance-2026-09-27.md'
---

# Additional ADRs and documentation review — September 27, 2026

## Baseline and authority

The owner requested the four additional ADRs identified in the preceding repository review, necessary documentation reconciliation, a general documentation polish, and a PR/merge. [PR #134](https://github.com/L3DigitalNet/star-trek-alter-course/pull/134) is the construction/review record from `dev` at `efd4ac93894fc28ea0c3ea40ca1be008dd4d6631`.

This is documentation work, not a gameplay slice, release, runtime refactor, or new conformance certification. The [prior 191-obligation register](adr-conformance-2026-09-27.md) retains its original ADR 0001–0014 scope and execution evidence. This record does not amend that result or claim that every confirmation criterion in the new ADRs was freshly tested.

## New architectural records

- **0015:** staged Core application, correlated candidate changes, refusal preservation, and truthful post-commit outcomes. ADR 0007's separately specified safely incremental alternative remains available.
- **0016:** actor-owned knowledge, administrative facts, immutable historical reports, source provenance, and indirect disclosure. Broader identity correlation, affiliation learning, and sharing remain unresolved.
- **0017:** independent AssetCtl, bounded external operations, untrusted-output validation, recoverable paired publication, provenance, and owner-controlled approval. No paid call or approval is authorized by documentation adoption.
- **0018:** session lifetime separate from workspaces, current action payloads, stale-context invalidation, preview isolation, and separation of world state from UI state/preferences. No new workspace or session service is introduced.

The records use the existing ADR shape and contain bounded applicability, alternatives, consequences, confirmation criteria, and source/test entry points. They supplement related records without falsely declaring supersession or rewriting accepted historical rationale.

## Documentation reconciliation

The [ADR catalog](../adr/README.md) is the maintained title/scope index. Documentation home, wiki home, architecture, the decision register, source catalog, README, contribution guide, roadmap, and affected system/runbook pages route readers to owning records rather than repeat competing contracts.

The pass reduces repeated release/migration history in entry points, distinguishes historical admission from current implementation, corrects completed-substrate future tense, and clarifies verification language. Detailed migration rules and gameplay values remain with their existing owning contracts. Source-catalog review detail is condensed only with fixed-revision links retaining the original records and headings.

Both project-owned `stac-architecture` routers are updated together with identical content. They distinguish current implementation from future domain ownership, no current RNG consumer from future RNG obligations, conditional package admission from installation, and original conformance evidence from new ADR confirmation requirements.

## Preservation checks

- D-01–D-19 and P-01–P-22 remain stable. Their gameplay and political approvals are not broadened.
- Released v0.6.2 remains source-only, with v0.6.0/V8 gameplay; unreleased development remains ship V6/system V1/save V10.
- M3, M5, and M6 remain partial. Substrate implementation is complete; recovery, refit gameplay, aggregation, political hierarchy, and broader intelligence are not approved by these ADRs.
- Existing architectural records, the original conformance register, schemas, production code, tests, dependencies, assets, legal notices, and enforcement mechanisms are not changed by this pass.
- Managed/vendor skill content and historical handoff/session evidence are not reformatted or rewritten. Only the project-owned architecture-router twins are reconciled.
- Existing major wiki navigation anchors are retained. Historical detail moved out of summaries remains linked at immutable revisions.

## Evidence and limits

Source tracing uses the unchanged baseline's `GameSimulation`, `SimulationState`, observation/report composition, installed-system and policy boundaries, `GameScreen`, `OwnShipActionBinding`, AssetCtl composition/generation/publication, and the canonical verifier. The [source catalog](../wiki/sources.md#implementation-evidence) and individual ADRs identify the relevant test entry points.

This is a broad editorial/documentation pass with targeted architecture tracing. It is not a new exhaustive gameplay-semantic sweep, fresh manual playtest, line-by-line re-audit of every runtime path, or independent peer review. The next full semantic sweep remains **2026-10-03**.

The execution environment could not clone the repository because it could not resolve `github.com`, and it lacks the authenticated local `gh`/`gh-workflow` route. Connector operations preserve the work on a topic branch and draft PR. The PR's Verification section owns actual hosted check results, final-head identity, and remaining admission limits; do not infer successful readiness or merge from this document's active status.

The initially chosen construction branch name did not satisfy repository Branch policy. A properly admitted governing relationship and compliant branch must be established through the existing workflow before readiness/merge. The local capability gap is not permission to weaken checks, choose an ineligible Standalone route, bypass branch protection, or report the work as merged.
