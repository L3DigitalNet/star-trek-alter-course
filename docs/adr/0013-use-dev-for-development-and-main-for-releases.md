---
schema_version: '1.1'
id: 'adr-0013-star-trek-alter-course-use-dev-for-development-and-main-for-releases'
title: 'ADR 0013: Use Dev for Development and Main for Releases'
description: 'Defines branch roles, change admission, merge history, protection, hotfix synchronization, and release versioning.'
doc_type: 'adr'
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
  - 'docs/design/branch-release-governance.md'
supersedes: []
superseded_by: null
source:
  - 'https://github.com/L3DigitalNet/star-trek-alter-course/issues/9'
  - 'https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/managing-a-branch-protection-rule'
  - 'https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-rulesets/available-rules-for-rulesets'
  - 'https://docs.github.com/en/code-security/concepts/supply-chain-security/immutable-releases'
confidence: 'high'
visibility: 'public'
license: 'MIT'
project:
  decision_makers:
    - 'project owner'
  consulted: []
  informed: []
  amends: []
  amended_by: []
---

# Use dev for development and main for releases

## Context and Problem Statement

The repository needs continuous integration of development work without making every development commit appear released. It also uses the Project Standards GitHub workflow, whose issue, T0, pull-request, and lifecycle contracts deliberately leave branch topology to the repository.

At adoption, `main` was the only permanent branch and the GitHub default. There was no release history, product version source, tracked Git-hook policy, or standing need for parallel release stabilization. Agent Handoff state is transient operational context and must remain cheap to update, while significant development needs durable issue and pull-request evidence.

This decision governs repository-owned branches, commits, pull requests, merge methods, branch and tag protection, releases, and hotfix synchronization. It applies to all repository development after adoption. It does not define game packaging, deployment, downloadable artifacts, or the content of a future 1.0 readiness decision.

How should the repository separate development from releases while enforcing significant-work governance and retaining a narrow direct path for transient handoff state?

## Decision Drivers

- `main` must identify released history rather than ordinary development.
- `dev` must be permanent, protected, and safe as the default development base.
- Significant work must use the adopted issue and pull-request lifecycle.
- Major features need isolated branches and may need governed Supporting pull requests.
- Agent Handoff and T0 prose updates must not acquire disproportionate ceremony.
- Merge history must distinguish topic admission from release promotion and hotfix synchronization.
- Normal tooling must reject accidental policy violations before publication.
- Releases need stable SemVer identity and immutable records without inventing an unused artifact pipeline.

## Considered Options

- Use permanent `dev` and release-only `main`, with a narrow direct-admission exception and purpose-specific merge methods.
- Continue developing on `main` and identify releases only with tags.
- Require pull requests for every change, including transient handoff state.
- Maintain permanent development, release, and main branches.
- Use one merge method for every pull request.

## Decision Outcome

Chosen option: "Use permanent `dev` and release-only `main`, with a narrow direct-admission exception and purpose-specific merge methods", because it gives development and released history distinct meanings, retains the adopted issue and pull-request lifecycle, and keeps transient state maintenance proportionate.

This decision governs all repository changes after adoption. Existing untagged history is not retroactively declared a release.

### Branch roles and names

`dev` is the permanent GitHub default and integration branch. It must never be deleted or force-pushed. Ordinary work branches from current `dev` and uses one of:

- `feature/<issue>-<slug>` for a user-visible or system-visible capability;
- `fix/<issue>-<slug>` for a defect based on `dev`;
- `task/<issue>-<slug>` for maintenance, infrastructure, refactoring, or policy work;
- `docs/<issue>-<slug>` for documentation work;
- `hotfix/<issue>-<slug>` for an urgent correction based on `main`.

Slugs use lowercase letters, digits, and hyphens. The issue number is required. Short-lived branches are deleted after merge.

A major feature normally uses one `feature/` branch and a Final pull request. An Initiative may use that feature branch as a temporary integration target for Supporting pull requests only when its parts cannot land independently on `dev`. This does not create another permanent branch.

After initial baseline adoption, `main` is release-only. Its accepted sources are:

- `dev` for the one-time pre-release baseline promotion described below;
- `dev` for a planned release;
- `hotfix/<issue>-<slug>` for an urgent patch;
- no direct local push in the routine workflow.

Standing release branches are not used. A temporary `release/vX.Y.Z` branch requires a later amendment or successor decision after concurrent stabilization and next-version development become a demonstrated need.

### Admission and governing work

Significant development begins with a typed issue and proceeds through a draft pull request carrying `Final: #N` or `Supporting: #N`. Bounded low-risk maintenance that does not warrant an issue may use a `Standalone` pull request under the adopted GitHub workflow.

All code, comments, tests, scripts, structured configuration, workflows, dependencies, schemas, release state, normative decisions, specifications, and enforcement material require a pull request. Direct admission is limited to:

- a T0 prose correction satisfying every condition in the adopted PR standard and carrying exactly one `Workflow-Admission: T0` trailer; or
- a commit whose changed paths are all under `docs/handoff/**`, `docs/STATUS.md`, or `docs/TODO.md` and that carries exactly one `Workflow-Admission: Handoff` trailer.

Direct admissions target `dev`. A mixed handoff and non-handoff commit requires a pull request. Construction-branch and PR-mediated commits carry no workflow-admission trailer.

The Handoff class is an explicit repository-level exception to `github-workflow` 1.8, whose package standard otherwise permits only T0 or pull-request admission and does not expose a configurable third class. The package remains authoritative for issues, pull requests, T0 classification, and lifecycle transitions. This ADR is authoritative only for the additional Handoff path required by the project owner. A future package upgrade must re-evaluate whether the exception remains necessary and compatible.

### Commit and merge history

Commit subjects and squash-merge titles use Conventional Commit form. The accepted types are `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `build`, `ci`, `perf`, and `revert`, with an optional lowercase scope and optional breaking-change marker.

Topic pull requests into `dev` use squash merge. The squash title becomes the admitted commit subject.

A one-time pre-release baseline pull request from `dev` to `main` uses a merge commit and the title `chore(baseline): establish main baseline`. Enforcement admits this exception only when the base lacks this ADR and the head contains it, so it expires when the baseline merges. A release pull request from `dev` to `main` uses a merge commit and the title `chore(release): vMAJOR.MINOR.PATCH`. A hotfix pull request into `main` uses squash merge and a `fix` title. The required synchronization pull request from `main` back to `dev` uses a merge commit and the title `chore(sync): merge main into dev`.

Rebase merging is disabled. Merge commits remain enabled only for release promotion, hotfix synchronization, and an explicitly governed feature integration case.

### Enforcement and trust boundary

GitHub protection for both permanent branches requires strict passing checks, resolved conversations, and pull requests with zero mandatory approvals. Zero approvals reflects the current single-developer ownership model; `gh-workflow ready` and required verification remain the admission boundary.

Both branches block deletion and force-pushes. `main` enforces protection for administrators and has no routine bypass. `dev` exempts the repository owner from administrator enforcement so approved direct admissions remain possible.

Tracked `commit-msg` and `pre-push` hooks enforce Conventional Commit subjects, protected targets, admission trailers, allowed handoff paths, mechanical T0 bounds, and the prohibition on direct merge commits. A repository setup script configures `core.hooksPath`. Pull-request CI enforces branch names, base/head topology and ancestry, and title contracts. Canonical verification checks the enforcement scripts and hooks.

GitHub cannot condition an actor bypass on changed paths. The owner can also skip local hooks. The direct-admission control is therefore defense in depth for the normal toolchain, not a server-perfect authorization boundary. This residual risk is accepted for a public repository with one routine owner and must not be described as impossible to bypass.

### Releases and hotfixes

A planned release follows this sequence:

1. Create a governing Task issue with the intended SemVer and release acceptance criteria.
2. Briefly freeze release-affecting changes on `dev` while the Final release pull request is open.
3. Open the Final `dev` to `main` pull request with curated release notes and the exact release title.
4. Pass required verification and merge with a merge commit.
5. Create the `vMAJOR.MINOR.PATCH` tag on that merge commit.
6. Publish a GitHub Release and enable immutable releases.

Versions remain in `0.x` until an explicit decision declares 1.0 readiness. The Git tag is the version source of truth until the game or a release artifact needs an embedded version. GitHub Release notes are the changelog until a consumer requires a repository `CHANGELOG.md`.

Every post-baseline merge to `main`, including a hotfix, must promptly receive a corresponding tag and immutable GitHub Release. The one-time baseline promotion is not a release and receives no tag or GitHub Release. No downloadable artifact is promised until a separately governed export and packaging pipeline exists.

A hotfix begins from current `main`, uses a `hotfix/` branch and governed pull request, and increments the patch version. After release publication, `main` must merge back into `dev` through the required synchronization pull request before ordinary development resumes.

### Migration and rollback

Adoption creates `dev` at the current `main` commit, makes it default, and then applies repository enforcement through a governed pull request to `dev`. Before the first game release, the owner may merge one governed baseline promotion from `dev` to `main` without a tag or GitHub Release. The exact title and ADR-presence check above make this a one-time migration path. Existing history receives no retroactive tag.

If a protection setting blocks legitimate recovery, the owner may temporarily change only the setting proven to cause the failure using the OpenBao-backed administrative credential. The owner records the reason, completes the narrow recovery, restores the approved setting, and verifies live configuration. This recovery process does not authorize bypassing the issue and pull-request workflow for ordinary development.

### Consequences

- Good, because development and released history have distinct, inspectable meanings.
- Good, because significant work retains issue, acceptance, verification, and pull-request evidence.
- Good, because topic history stays concise while promotion and synchronization ancestry stays visible.
- Good, because handoff state remains inexpensive to maintain.
- Bad, because every release and hotfix requires explicit cross-branch coordination.
- Bad, because the approved owner bypass cannot be limited by path on GitHub.
- Neutral, because the first release and any artifact pipeline remain future governed work.

### Confirmation

Conformance is confirmed by all of the following:

- `./scripts/setup-git-hooks.sh` reports `.githooks` as the configured local hook path;
- `./scripts/test-branch-policy.sh` passes its positive and negative cases;
- `./scripts/verify.sh` passes through `rexec`;
- pull requests pass the `Branch policy` and `Canonical verification` checks;
- GitHub reports `dev` as default, approved merge methods, automatic topic deletion, and matching branch and tag protections;
- release receipts show a SemVer tag and immutable GitHub Release for every post-baseline `main` merge.

## Pros and Cons of the Options

### Permanent dev and release-only main

- Good, because the default branch supports continuous integration without weakening release semantics.
- Good, because release promotion and hotfix synchronization are explicit.
- Bad, because two permanent branches can diverge if synchronization is neglected.

### Development on main with release tags

- Good, because it minimizes branch administration.
- Bad, because unreleased development and released state share the same branch tip.
- Bad, because `main` cannot serve as a release-only trust signal.

### Pull requests for every handoff edit

- Good, because GitHub can enforce one uniform route.
- Bad, because transient state updates gain enough ceremony to discourage timely handoff maintenance.

### Permanent release branch

- Good, because it supports long stabilization alongside continued development.
- Bad, because the repository has not demonstrated that need and would immediately acquire another synchronization path.

### One merge method everywhere

- Good, because it is simple to explain.
- Bad, because squashing release promotion erases useful ancestry while merge-committing every topic preserves construction noise.

## More Information

The approved discovery record is [Branch and release governance](../design/branch-release-governance.md). The governing implementation work is [Issue #9](https://github.com/L3DigitalNet/star-trek-alter-course/issues/9).

Reconsider this decision when the repository gains multiple routine maintainers, supports more than one release line, needs concurrent release stabilization, gains a packaging pipeline, or GitHub adds path-conditioned actor bypass.
