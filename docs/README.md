---
schema_version: '1.1'
id: 'index-l97l7i-documentation'
title: 'Project Documentation'
description: 'Routes game design, architectural decisions, development guidance, and operational evidence to their owning sources.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-27'
tags:
  - 'design'
aliases: []
related:
  - 'docs/wiki/README.md'
  - 'docs/adr/README.md'
  - 'ROADMAP.md'
---

# Project documentation

Start with the [design wiki](wiki/README.md) for game behavior and with the [ADR catalog](adr/README.md) for architectural decisions. Both live in this repository and use the same reviewed version history as the game; there is no separate GitHub Wiki or deployed documentation service.

## Choose the owning source

- **Game design:** the [wiki](wiki/README.md) owns intended behavior, detailed rules, [milestone proofs](wiki/milestone-proofs.md), approved design, and unresolved questions. Its [implementation status](wiki/implementation-status.md) distinguishes reviewed behavior from future intent.
- **Architecture:** [ADRs](adr/README.md) own significant boundaries and their rationale. They do not introduce a second set of gameplay formulas or certify implementation merely by being active.
- **Work state:** [STATUS](STATUS.md), [TODO](TODO.md), and [handoff](handoff/) own operational facts and session history, not new game-design decisions.
- **Onboarding and sequence:** the [root README](../README.md), [contribution guide](../CONTRIBUTING.md), and [roadmap](../ROADMAP.md) route readers to contracts rather than duplicate them.
- **Development:** [Development quality](development-quality.md) and [agent setup](development-agent-skills.md) own tool setup, verification, and harness maintenance. [Dependency admissions](dependency-admission/) record bounded package evidence under ADR 0003, not blanket permission for future usage.
- **Review evidence:** the [source catalog](wiki/sources.md#review-record) indexes dated coverage. The [ADR conformance register](reviews/adr-conformance-2026-09-27.md) records the original 14-ADR review; it is not an amendment, release record, or certification of later ADRs.
- **Rights:** [License](../LICENSE.md) and [legal notice](../LEGAL.md) own licensing boundaries. Design references and automated asset review do not establish legal clearance.

## Preserve one contract per subject

Detailed game contracts formerly spread across design/specification files are consolidated in the wiki. The [consolidation map](wiki/sources.md#consolidation-map) preserves their destinations, and [historical provenance](wiki/sources.md#historical-provenance) links fixed revisions rather than maintaining competing copies.

Source, schemas, and tests establish what the implementation actually does. They do not silently approve a change to intended design. Reconcile a mismatch through the owning contract and the [review procedure](wiki/development-and-governance.md#recurring-design-reconciliation), keeping inspected evidence separate from freshly executed checks.
