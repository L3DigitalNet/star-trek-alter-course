---
schema_version: '1.1'
id: 'reference-11owgq-development-and-governance'
title: 'Development and Governance'
description: 'Repository toolchain, validation, contribution workflow, and durable documentation ownership.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'development'
  - 'validation'
aliases: []
related:
  - 'CONTRIBUTING.md'
  - 'docs/development-quality.md'
  - 'docs/development-agent-skills.md'
  - 'docs/adr/0013-use-dev-for-development-and-main-for-releases.md'
---

# Development and governance

[Wiki home](README.md) · [Architecture](architecture.md) · [Sources](sources.md)

## Repository toolchain

The reviewed repository pins Godot 4.7.2 .NET/C#, .NET SDK 10.0.111, C# 12, and Node 24. These are repository selections, not claims about the newest upstream releases. Core, Godot, and Core tests target .NET 8; AssetCtl and its tests target .NET 10. The supported source-development environment is Linux x86_64.

Exact tool and package versions belong in their existing configuration and lock files. Consult [global.json](../../global.json), [Directory.Build.props](../../Directory.Build.props), [Directory.Packages.props](../../Directory.Packages.props), and the [development runbook](../development-quality.md) rather than editing versions through wiki prose.

## Quality gate

`./scripts/verify.sh` is the canonical ordered, fail-fast, tracked-file-read-only gate used by contributors and CI. `./scripts/fix.sh` applies supported formatting. CSharpier owns C# whitespace; compiler/SDK analyzers, Meziantou, and banned-API rules enforce correctness and semantic style. Warnings fail builds.

The canonical path covers locked restore/build, Core and AssetCtl tests, offline asset validation, Godot integration/smoke, formatting, shell/workflow analysis, secret scanning, and repository policies. Managed Project Standards checks complement it with Markdown/frontmatter policy; they are not silently reimplemented in another gate.

Use ordinary xUnit tests for pure behavior and vendored GdUnit4 for current engine integration. ADR-selected additional testing tools remain demand-driven. `./scripts/test-mutation.sh` is a separate deep-validation path without an invented mutation-score threshold.

The launcher restores/builds before running Godot and handles source asset preparation. A stale Debug assembly or unimported asset is not evidence that the current Core rules are wrong; consult the [recorded gotchas](../handoff/bugs/INDEX.md).

## Branches, issues, and releases

ADRs and the repository's adopted workflow remain authoritative. Ordinary work starts from permanent protected `dev`, with significant work governed by an issue and a named topic branch. Open a draft PR with exactly one Final, Supporting, or qualifying Standalone declaration. Normal topic PRs squash into `dev`; `main` is release-only.

The installed `gh-workflow` tool owns typed issue fields and lifecycle validation, readiness, merge, and terminal-state synchronization. Follow [AGENTS.md](../../AGENTS.md), the installed skill, and [CONTRIBUTING](../../CONTRIBUTING.md). A connected-session capability gap must be disclosed in its work record; it does not create a standing alternative workflow or permission to bypass checks.

Release promotion uses a governed merge commit, a corresponding tag and immutable release, and synchronization back to `dev`. A source-only release does not imply a binary, installer, Godot export, or distribution pipeline. Narrow T0 and Handoff direct-admission exceptions cannot touch the design wiki, ADRs, supporting design documents, or specifications; the branch policy enforces this.

## Documentation and agent ownership

This wiki is the single source of truth for design. ADRs record architectural decisions; the documents under `docs/design/` and `docs/specs/` supply supporting detail for wiki pages and may not contradict them. The [source catalog](sources.md) is the coverage index, the [decision register](decision-register.md) distinguishes decisions from implementation, and [open questions](open-questions.md) prevents proposals from becoming accidental commitments.

Operational facts still belong in STATUS, TODO, and the appropriate handoff files. Keep eager state small. Preserve owner-authored tasks and historical records. Do not edit standard-owned skills, hooks, or lock inventories merely to make documentation checks pass.

Managed Markdown uses the existing frontmatter schema, stable IDs, canonical quoted fields, Prettier, and markdownlint. A page being active does not mean its planned system is implemented. A design change lands on its wiki page in the same governed work that implements it, together with implementation evidence and any supporting document that restates the detail.

## Recurring design reconciliation

The agent or contributor doing the work owns reconciliation; the reviewer checks its evidence before Ready. The owner or merging agent also owns the landing check. Review happens at these boundaries:

| When | Required review |
| --- | --- |
| Start or resume behavior-affecting work | Read the owning wiki pages and active ADRs; compare the relevant source/tests and changes since the last recorded review. Check the sweep due date in the [source catalog](sources.md#review-record). |
| Each completed behavior change, bug fix, or unforeseen constraint | Reconcile the affected contracts and supporting pages immediately, while the evidence is available. Do not postpone reconciliation to session closeout. |
| Before Ready and after a material review revision | Review the complete diff against the owning pages, linked supporting detail, implementation status, and summary/navigation claims. Record pages, source/test evidence, and findings in the PR's Acceptance coverage section. |
| Merge and release closeout | Check landing/release language in the wiki home, owning pages, implementation status, source catalog, README, and roadmap against the actual merged/released revision. A handoff-only update cannot substitute for wiki reconciliation. |
| Every seven calendar days during active development, and before each release | Sweep every page in the wiki index and source catalog for stale implementation claims, unresolved questions already answered by code or decisions, missing source coverage, contradictory summaries, and broken evidence links. |

At session startup, an overdue sweep becomes part of that session's authorized maintenance before new behavior work. No unattended process runs while development is inactive; perform an overdue sweep when work resumes. A targeted review does not reset the full-sweep date. Record a full sweep only after every indexed topic is covered; explicitly retain any incomplete coverage and its reason.

Use the [source catalog review record](sources.md#review-record) for the latest full sweep and subsequent targeted reviews: date, reviewed source revision, exact scope, implementation/test or decision evidence, findings and disposition, and next full-sweep due date. Replace superseded targeted entries rather than building another session log; PRs retain the detailed history. Updating a frontmatter date or running format/link checks is not a semantic review. Inspect the source and tests that support each changed claim; run focused regressions when behavior changes, and distinguish inspected tests from tests executed in this review.

### Bugs and unforeseen implementation changes

The wiki defines intended behavior; code and tests establish actual behavior. When they disagree, identify which is wrong before editing either:

- A bug violating approved design is fixed toward the wiki contract. Add regression evidence and clarify the owning page if an edge case or invariant was missing. If its contract is already exact, cite the unchanged page and the restoring regression in the PR instead of making a cosmetic edit.
- An unforeseen constraint requiring different behavior is not permission to rewrite approved design to match the code. Record the discrepancy, consequences, and proposed resolution in the owning work; seek an owner decision only when the product decision is unresolved. Put unapproved alternatives in [open questions](open-questions.md). Architectural changes require an ADR. Once resolved, update the owning wiki page, decision/question records as warranted, supporting detail, implementation, and tests together.
- Stale implementation status or evidence is corrected against the reviewed source revision. Keep released behavior separate from unreleased `dev`, and keep approved future design separate from both. Preserve historical decisions and original feature scope.

Search related wiki summaries and linked supporting documents for the superseded claim, not just the file being edited. Fix related drift in the same governed work. If a correction needs an unavailable resource or unresolved owner decision, record the exact gap and evidence; do not declare the affected acceptance criterion satisfied.

Write review-branch documentation so it remains true after landing: describe behavior as implemented in the change and identify the last released baseline separately. Avoid temporary claims such as “not implemented until this PR merges” when the reviewed source already implements it. If landing changes a factual claim anyway, reconcile it through a governed documentation PR; do not use the narrower Handoff admission to modify wiki files.

Formatting, frontmatter, link checks, and automated behavioral tests support this process but cannot prove agreement between design prose and implementation. The named-page review and PR evidence are required even when every automated check passes. There is no new background service or automatic semantic-drift detector.

## Licensing and external content

ST:AC is an unofficial noncommercial fan project. The repository's MIT grant applies within its stated boundaries to original software, not to Star Trek IP or third-party materials. Noncommercial status is not a blanket permission to copy reference text, art, audio, or scripts.

Follow [LICENSE](../../LICENSE.md), [LEGAL](../../LEGAL.md), contribution requirements, and asset provenance/rights rules. The wiki links existing references; it does not import a canon database or assert legal clearance for future content.

## Sources

[Development quality](../development-quality.md), [agent skills](../development-agent-skills.md), [quality-gate ADR](../adr/0002-use-one-canonical-quality-gate.md), [branch/release ADR](../adr/0013-use-dev-for-development-and-main-for-releases.md), and [branch governance discovery](../design/branch-release-governance.md).
