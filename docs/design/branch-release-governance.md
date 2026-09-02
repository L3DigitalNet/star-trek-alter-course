---
schema_version: '1.1'
id: 'decision-bdx6zl-branch-release-governance'
title: 'Branch and Release Governance'
description: 'Approved design for development branches, pull-request admission, protected releases, and Agent Handoff exceptions.'
doc_type: 'decision'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'development'
  - 'standard'
aliases: []
related:
  - 'docs/adr/0013-use-dev-for-development-and-main-for-releases.md'
source:
  - 'https://github.com/L3DigitalNet/star-trek-alter-course/issues/9'
  - 'https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/managing-a-branch-protection-rule'
  - 'https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets'
  - 'https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases'
confidence: 'high'
visibility: 'public'
license: 'MIT'
---

# Branch and release governance

## Status and provenance

- Status: `approved`
- Operation: `create`
- Owner: project owner
- Created: 2026-09-01
- Last revised: 2026-09-01
- Revision: baseline promotion amendment
- Prior design brief: none
- Prior brief SHA-256: not applicable
- Reopened decisions: tag-led immutable releases, for a bounded pre-release baseline exception
- Revision reason: the project owner requested one baseline on `main` before game files exist, without representing it as a release
- Working-state source: conversation-only discovery; the fallback state path was not Git-ignored

The project owner approved the integrated design on 2026-09-01 after separately approving the admission boundary, merge history, protection model, branch conventions, and release contract.

## Problem and intended outcome

The repository needs a stable development line without allowing ordinary development to turn `main` into an integration branch. Contributors and agents also need one unambiguous route for significant work, a narrow exception for transient Agent Handoff state, and a repeatable way to promote tested development history into immutable releases.

The intended outcome is a permanent, protected `dev` default branch; a release-only `main`; issue-governed pull requests for significant work; mechanically checked topic branches and commit metadata; and SemVer-tagged immutable GitHub Releases.

## Current context

At design approval, `main` was the only long-lived branch and the GitHub default. The repository had no release tags, GitHub Releases, tracked Git hooks, or product version source. Project Standards `github-workflow` 1.8 already defined T0 admission, draft-first pull requests, governing work declarations, and lifecycle transitions without prescribing repository branch topology. Its admission contract permits only T0 or a pull request, so the approved Handoff path is an explicit repository-level exception rather than a package configuration.

GitHub can require pull requests, checks, and protected refs and can grant bypass to an actor. Its available protection and ruleset controls do not make that actor bypass conditional on the changed paths. A direct handoff exception therefore needs defense in depth: a documented actor bypass plus a tracked local hook, with the remaining bypass capability acknowledged rather than overstated.

## Scope

### In scope

- Permanent and topic branch roles, names, bases, and deletion policy.
- Direct-commit and pull-request admission boundaries.
- Merge methods and history shape.
- Protected-branch, required-check, local-hook, and tag enforcement.
- Release and hotfix promotion, synchronization, versioning, and release records.
- Bootstrap from the pre-policy `main` history.

### Non-goals

- A game export, packaging, distribution, or artifact-signing pipeline.
- Mandatory human approval in a single-developer repository.
- A server-perfect path-conditioned bypass that GitHub does not provide.
- A standing release branch before release stabilization must overlap with next-version development.

### Deferred considerations

- Introduce temporary `release/vX.Y.Z` branches only when a real release must stabilize while `dev` advances.
- Add an embedded product version only when the game or its artifacts need to display or consume one.
- Add downloadable release artifacts only after an export pipeline has its own design and verification contract.

## Constraints and assumptions

### Constraints

- Significant development follows the adopted issue and pull-request lifecycle.
- Agent Handoff state remains cheap to update directly on `dev`.
- `dev` and `main` must resist deletion and force-pushes.
- GitHub-side administration uses the Full Access GitHub PAT from OpenBao without persisting its value.
- Moderate and heavy repository validation runs through `rexec`.

### Assumptions

- One project owner remains the routine maintainer, so a mandatory approval count would add ceremony without independent review.
- A brief release freeze on `dev` is acceptable while a release pull request is open.
- The current lack of a version file and artifact pipeline means the Git tag can be the sole product-version authority initially.

### Agent-applied defaults

- Conventional Commit types follow the commonly used repository vocabulary rather than introducing a repository-specific taxonomy.
- Release work uses a Task issue unless its breadth warrants an Initiative.
- Existing untagged history remains historical development; the first tag comes from a deliberate release.

## Selected design

`dev` is the permanent default and integration branch. Significant work begins from current `dev` on a short-lived issue branch and returns through a draft pull request governed by an issue. Small PR-based maintenance may declare `Standalone`. Direct admission to `dev` is limited to T0 prose changes and Agent Handoff state under `docs/handoff/**`, `docs/STATUS.md`, or `docs/TODO.md`; each direct commit carries its admission trailer. Handoff admission deliberately overrides `github-workflow` 1.8's two-class admission rule for these paths only. The package continues to govern issues, pull requests, T0 classification, and lifecycle actions everywhere else.

Topic pull requests squash into `dev`, producing one admitted commit per pull request. Before the first game release, one governed baseline pull request may merge `dev` into `main` without a tag or GitHub Release. Its exact `chore(baseline): establish main baseline` title is accepted only when the base lacks ADR 0013 and the head contains it, making the path self-expiring. A release uses a Final pull request from `dev` to `main` and a merge commit, preserving the development ancestry. A hotfix branches from `main`, squash-merges back to `main`, receives a patch release, and is then synchronized into `dev` through a merge-commit pull request. Rebase merging is disabled. Short-lived branches are deleted after merge; `dev` is never deleted.

GitHub protects both long-lived branches with strict checks, resolved conversations, and force-push and deletion blocks. `main` has no routine bypass. The owner can bypass `dev` protection for direct handoff or T0 pushes, while tracked hooks check commit messages, changed paths, mechanical T0 limits, and protected targets. Pull-request CI checks branch topology, naming, and titles. These controls make the normal toolchain fail closed but cannot prevent an owner from deliberately bypassing both GitHub and local enforcement.

Releases use `vMAJOR.MINOR.PATCH` tags and remain in `0.x` until an explicit 1.0 decision. Every merge to `main` after the one-time baseline promptly receives an immutable GitHub Release. The tag is the initial version source of truth, and GitHub Release notes are the initial changelog.

## Consequential decisions

### Permanent development and release branches

- Status: `approved`
- Decision: Use permanent default `dev` for development and reserve `main` for releases and hotfixes.
- Agent recommendation: Adopt the two-branch topology and keep release branches demand-driven.
- User disposition: Approved.
- Rationale: Development can integrate continuously without weakening the meaning of `main`.
- Long-term consequences: Every release is an explicit promotion, and hotfixes require synchronization.
- Reversibility: Moderate; changing topology affects protections, automation, and contributor habits.
- Reopen when: A different release cadence or multiple supported release lines make two permanent branches insufficient.

### Defense-in-depth direct admission

- Status: `approved`
- Decision: Permit owner bypass on `dev` for Agent Handoff and T0 commits, checked by tracked hooks.
- Agent recommendation: Accept the platform limitation explicitly and enforce the normal path strongly.
- User disposition: Approved.
- Rationale: Transient handoff updates stay inexpensive without making all development direct-to-branch.
- Long-term consequences: The owner retains technical ability to bypass more broadly; audits and documented discipline remain part of the trust boundary.
- Residual risk: Package upgrades may continue to report or document the Handoff path as outside the standard admission model, so this exception must be re-evaluated on each GitHub workflow upgrade.
- Reversibility: Easy to tighten by removing bypass; harder to loosen without weakening policy.
- Reopen when: GitHub supports path-conditioned bypass or more maintainers require actor-specific controls.

### Purpose-specific merge history

- Status: `approved`
- Decision: Squash topic work; merge release promotions and hotfix synchronization.
- Agent recommendation: Use history shape to distinguish admitted change units from cross-branch ancestry.
- User disposition: Approved.
- Rationale: Topic history stays concise while release and synchronization relationships remain visible.
- Long-term consequences: Pull-request titles become commit subjects and must carry meaningful Conventional Commit metadata.
- Reversibility: Easy for future merges; existing history remains unchanged.
- Reopen when: Automated release tooling requires a different ancestry contract.

### Tag-led immutable releases

- Status: `approved`
- Decision: Use SemVer tags as the initial version source and publish immutable GitHub Releases.
- Agent recommendation: Avoid a redundant version file and artifact contract until consumers exist.
- User disposition: Approved.
- Rationale: The repository currently has neither an embedded product version nor distributable artifacts.
- Long-term consequences: Release creation must promptly follow every post-baseline `main` merge.
- Reversibility: Easy to add an embedded version later; published immutable releases remain historical records.
- Reopen when: Runtime display, packaging, update checks, or distribution needs a version inside repository content.

## Alternatives considered

| Alternative | Best fit | Advantages | Tradeoffs and failure modes | Long-term consequences | Disposition |
| --- | --- | --- | --- | --- | --- |
| Develop on `main` | Very small repositories without release semantics | Minimal branching | `main` cannot represent released state separately from integration | Release provenance remains ambiguous | Rejected |
| Require PRs for every handoff edit | Multi-maintainer repositories needing server-perfect review | Uniform server enforcement | High ceremony for transient work-state updates | Handoff state is likely to become stale | Rejected |
| Grant owner bypass plus tracked hooks | Single-owner repository with narrow direct state updates | Practical workflow and strong normal-path checks | Owner bypass cannot be path-conditioned by GitHub | Requires explicit residual-risk acceptance | Selected |
| Always use release branches | Multiple concurrent supported lines or long stabilization | Parallel stabilization and development | Adds branch lifecycle and synchronization burden immediately | More permanent process state | Deferred |
| Squash every PR including releases | Repositories that do not care about branch ancestry | Uniform history | Obscures promotion and synchronization relationships | Harder release ancestry inspection | Rejected |

## Complexity disposition

### Retained

- Two protected permanent branches, because development and released state have different meanings.
- Purpose-specific merge methods, because topic admission and branch promotion need different ancestry.
- Local hooks plus GitHub enforcement, because the approved direct exception crosses a GitHub capability boundary.

### Deferred

- Standing release branches until release stabilization overlaps with new development.
- Embedded versions and release artifacts until a runtime or distribution consumer exists.

### Rejected

- Mandatory approval counts in the current single-owner repository.
- Rebase merges, which add a third history shape without an approved need.
- Retroactive tagging of history that was not produced by the release workflow.

### Preserved extension seams

- `release/vX.Y.Z` remains reserved for a future temporary stabilization branch policy.
- Major Initiatives may use an explicitly governed feature integration branch when independent Supporting pull requests cannot land directly on `dev`.

## Unresolved decisions

### Blocking

None.

### Non-blocking

- The first release version remains a future release-issue decision.
- Export formats, supported platforms, and downloadable artifacts remain owned by a future packaging design.

## Downstream impact

- ADR 0013 becomes the normative decision record.
- GitHub default-branch, merge, protection, and tag settings must match this design.
- Repository hooks, verification, and pull-request CI must encode the same branch and admission vocabulary.

## Sources

- `.agents/skills/github-workflow/references/pr-standard.md` — adopted T0 and pull-request admission contract.
- `.agents/skills/github-workflow/references/issue-structure.md` — governing work-contract structure.
- `.standards/config.toml` — adopted Project Standards package versions.
- `scripts/verify.sh` — canonical repository verification boundary.
- [GitHub protected branch documentation](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/managing-a-branch-protection-rule) — branch protection and bypass capabilities.
- [GitHub ruleset rule reference](https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets) — available ref and metadata rules.
- [GitHub immutable releases](https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases) — immutable release behavior.

## Spec-authoring handoff

- Design brief: `docs/design/branch-release-governance.md`
- Operation: `create`
- Status: `approved`
- Problem and outcome: Separate continuous development from immutable released state while preserving a narrow low-friction handoff path.
- Scope boundary: Repository development, pull-request, branch, hotfix, and release governance; no packaging pipeline.
- Selected design: Permanent protected `dev`, release-only `main`, governed topic pull requests, narrow direct admission, purpose-specific merges, and SemVer immutable releases.
- Approved consequential decisions:
  - Use `dev` for development and `main` for releases.
  - Permit owner bypass on `dev` only for Agent Handoff and T0 admission.
  - Squash topics and merge promotions or synchronization.
  - Use tag-led immutable releases.
- Agent-applied defaults:
  - Use the existing Conventional Commit vocabulary.
  - Treat the Git tag as the initial version source.
- Assumptions:
  - The repository remains single-owner for routine admission.
  - A short `dev` release freeze is acceptable.
- Blocking decisions: none
- Non-blocking matters:
  - The first release version and any artifact pipeline remain future governed decisions.
- Downstream impact:
  - ADR, repository enforcement, and GitHub settings must remain aligned.
- Material source artifacts:
  - `.agents/skills/github-workflow/references/pr-standard.md`
  - `.standards/config.toml`
  - `scripts/verify.sh`
