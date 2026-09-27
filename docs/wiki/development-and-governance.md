---
schema_version: '1.1'
id: 'reference-11owgq-development-and-governance'
title: 'Development and Governance'
description: 'Repository toolchain, validation, contribution workflow, and durable documentation ownership.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-27'
tags:
  - 'development'
  - 'validation'
aliases: []
related:
  - 'CONTRIBUTING.md'
  - 'docs/development-quality.md'
  - 'docs/development-agent-skills.md'
  - 'docs/adr/README.md'
  - 'docs/adr/0013-use-dev-for-development-and-main-for-releases.md'
  - 'docs/wiki/ship-system-substrate.md'
---

# Development and governance

[Wiki home](README.md) · [Architecture](architecture.md) · [ADR catalog](../adr/README.md) · [Sources](sources.md)

## Repository toolchain

The supported source-development environment is Linux x86_64. Core, Godot, and Core tests target .NET 8; AssetCtl and its tests target .NET 10. The repository pins its SDK, editor, language, and tooling rather than relying on the newest globally installed versions.

Exact selections belong in [global.json](../../global.json), [Directory.Build.props](../../Directory.Build.props), [Directory.Packages.props](../../Directory.Packages.props), [the Node selector](../../.node-version), and the resolver scripts. The [development runbook](../development-quality.md) owns setup and verification commands. Do not maintain a competing version catalog in wiki summaries.

## Quality gate

`./scripts/verify.sh` is the canonical ordered, fail-fast, tracked-file-read-only gate used by contributors and CI. `./scripts/fix.sh` applies supported formatting. CSharpier owns C# whitespace; compiler/SDK analyzers, Meziantou, and banned-API rules own correctness and semantic style. Warnings fail builds.

The canonical path covers locked restore/build, Core and AssetCtl tests, offline asset validation, Godot integration/smoke, formatting, shell/workflow analysis, secret scanning, and repository policies. Managed Project Standards checks complement it with Markdown/frontmatter policy; they are not silently reimplemented in another gate.

Use ordinary xUnit tests for pure behavior and vendored GdUnit4 for current engine integration. The admitted ArchUnitNET and CsCheck consumers are Core-test-only, not runtime dependencies or a new test service. GdUnit4Net remains conditional on a C# engine-integration subject. ADR 0011 and the [UnitsNet evaluation](../dependency-admission/unitsnet-evaluation.md) retain the bounded existing-quantity exception. Mutation testing is separate deep validation without an invented score threshold.

The launcher restores/builds before running Godot and prepares source assets. A stale Debug assembly or unimported asset is not evidence that current Core rules are wrong; consult the [recorded gotchas](../handoff/bugs/INDEX.md).

### Ship-system admission evidence

Issue #121 completed the [substrate admission contract](ship-system-substrate.md#admission-proof-and-enforcement) through Final PR #123. Its baseline-equivalence, heterogeneous-loadout, identity, information-boundary, migration/continuation, bounds, and architecture-conformance evidence remains required regression coverage for affected changes. A passing unchanged M6A suite or a collection-shaped wrapper alone does not establish substrate conformance.

Review common mechanisms separately from typed behavior and frozen historical adapters. Require named evidence in the governing PR, not a promise to add tests later. This uses the existing canonical path; it introduces no verification service or permission to change the mechanisms judging the work.

### Cross-cutting boundary evidence

ADRs 0015–0018 provide confirmation criteria for command commitment, knowledge/reporting, AssetCtl publication, and session/action lifetime. Read the [scope map](../adr/README.md#scope-and-overlap) before applying them. They supplement existing time, persistence, AI, testing, and content decisions rather than creating parallel gates.

The [191-obligation review](../reviews/adr-conformance-2026-09-27.md) is dated evidence for ADRs 0001–0014. New records do not retroactively add certified obligations to that review. Cite the affected source/tests and distinguish inspected paths, inherited results, and fresh execution.

## Branches, issues, and releases

Ordinary work starts from permanent protected `dev`. Significant work uses a governing issue and an issue-numbered topic branch. Qualifying bounded low-risk maintenance may use `standalone/<slug>`; the automated relationship/risk check does not itself decide that significant work qualifies. Follow [CONTRIBUTING](../../CONTRIBUTING.md) and ADR 0013.

Open a draft PR with exactly one Final, Supporting, or qualifying Standalone declaration. The installed `gh-workflow` tool owns typed issue fields, lifecycle validation, readiness, merge, and terminal-state synchronization. Follow [AGENTS.md](../../AGENTS.md) and the installed skill. A connected-session capability gap must be disclosed in its work record; it does not create a standing alternative workflow or permission to bypass checks.

Normal topic PRs squash into `dev`. `main` is release-only: promotion uses a governed merge commit, corresponding tag and immutable release, and synchronization back to `dev`. A source-only release does not imply a binary, installer, Godot export, or distribution pipeline.

Narrow T0 and Handoff direct-admission exceptions cannot touch the design wiki, ADRs, supporting design documents, or specifications. Architectural documentation is not trivial prose merely because it changes no executable code.

## Documentation and agent ownership

The wiki owns detailed game contracts and [milestone proofs](milestone-proofs.md). ADRs own significant architectural choices and rationale; the [catalog](../adr/README.md) identifies active records and overlap. The [source catalog](sources.md) owns coverage/evidence navigation, the [decision register](decision-register.md) preserves approval IDs, and [open questions](open-questions.md) keeps proposals from becoming accidental commitments.

Operational facts belong in STATUS, TODO, and handoff files. Keep eager state small, preserve owner-authored tasks and historical records, and do not edit standard-owned skills, hooks, or lock inventories merely to make documentation checks pass. Project-owned architecture-router copies must stay consistent with the owning docs and with each other.

The [authority map](../README.md) distinguishes external runbooks, package admissions, licensing, onboarding, and work state. The former branch-governance discovery remains [historical provenance](sources.md#historical-provenance); ADR 0013 preserves its accepted decisions, alternatives, and residual bypass risk.

Managed Markdown uses existing frontmatter rules, stable IDs, quoted fields, Prettier, and markdownlint. `status: active` describes a document, not feature completion. Use the [ADR maintenance guidance](../adr/README.md#adding-or-changing-a-record) for new records, amendments, and supersession; preserve accepted historical rationale instead of silently rewriting it as current implementation status.

## Recurring design reconciliation

The contributor doing the work owns reconciliation; the reviewer checks its evidence before Ready. The owner or merging agent owns the landing check.

| When | Required review |
| --- | --- |
| Start or resume behavior-affecting work | Read owning wiki pages and active ADRs; compare relevant source/tests and changes since the recorded review. Check the sweep due date in the source catalog. |
| Each completed behavior change, bug fix, or unforeseen constraint | Reconcile affected contracts and supporting pages immediately, while evidence is available. Do not defer it to closeout. |
| Before Ready and after material review revision | Review the complete diff against owning contracts, supporting detail, implementation status, and navigation claims. Record named pages, source/tests, and disposition in PR Acceptance coverage. |
| Merge and release closeout | Check landing/release language against the actual revision in wiki home, owning pages, implementation status, sources, README, and roadmap. Handoff-only updates are not a substitute. |
| Every seven calendar days during active work, and before each release | Sweep every indexed wiki topic for stale claims, resolved questions, missing evidence, contradictory summaries, and broken links. |

At startup, an overdue sweep becomes part of authorized maintenance before new behavior work. No unattended process runs while development is inactive. A targeted review does not reset the full-sweep date. Record a full sweep only after every indexed topic is covered; retain incomplete coverage and its reason explicitly.

The [source catalog review record](sources.md#review-record) records date, source revision, exact scope, evidence, findings/disposition, and next full-sweep date. Fixed revisions and PRs retain detailed history rather than an unbounded session log. An updated frontmatter date or formatting/link check is not a semantic review. Inspect source/tests supporting changed claims; execute appropriate regressions when behavior changes and distinguish them from tests merely read.

### Bugs and unforeseen implementation changes

The wiki defines intended behavior; source/tests establish actual behavior. Identify which is wrong before editing either.

- Fix a bug toward the approved contract and add regression evidence. If the contract is already exact, cite the unchanged page rather than making a cosmetic edit to imply reconciliation.
- An unforeseen constraint is not permission to rewrite design to match code. Record the discrepancy and proposed resolution; unresolved product choices require owner selection. Put unapproved alternatives in open questions. Architectural changes require a new or amended ADR, with coordinated implementation, tests, and owning-page updates.
- Correct stale status against the reviewed revision. Keep released behavior, unreleased development, approved future design, and historical feature scope distinct.

Search related summaries and supporting documents for superseded claims, not only the file being edited. Fix related drift in the same governed work. Record an unavailable resource or unresolved decision as an explicit gap rather than marking its acceptance criterion satisfied.

Write review-branch documentation so it remains true after landing. Describe implemented behavior and identify the released baseline separately; avoid temporary claims such as “not implemented until this PR merges” when source already provides it. Reconcile any changed landing facts through governed documentation work, not a Handoff admission that cannot touch the wiki.

Formatting, frontmatter, link checks, and behavioral tests support this process but do not prove semantic agreement by themselves. Named-page review and truthful PR evidence remain required; no automatic semantic-drift service is introduced.

## Licensing and external content

ST:AC is an unofficial noncommercial fan project. The MIT grant applies within the repository's stated boundaries to original software, not Star Trek IP or third-party materials. Noncommercial status is not blanket permission to copy text, artwork, audio, or scripts.

Follow [LICENSE](../../LICENSE.md), [LEGAL](../../LEGAL.md), contribution requirements, and asset provenance/rights rules. References and automated review do not assert legal clearance or import a canon database.

## Sources

[Development quality](../development-quality.md), [agent setup](../development-agent-skills.md), [ADR catalog](../adr/README.md), [branch/release ADR](../adr/0013-use-dev-for-development-and-main-for-releases.md), [substrate admission](ship-system-substrate.md#admission-proof-and-enforcement), and [historical provenance](sources.md#historical-provenance).
