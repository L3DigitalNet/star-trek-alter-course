# Contributing to Star Trek: Alter Course

The project is in early development. Align significant work through a typed issue before implementation, with an intended outcome and concrete acceptance criteria.

## Before you start

Search open issues and pull requests for related work. Create or join a typed issue for a feature, bug, or substantial maintenance change. Small, low-risk maintenance may use a Standalone PR. Trivial prose-only corrections may qualify for T0; architectural decisions, specifications, and other protected surfaces do not. Agent Handoff admission is reserved for maintainer operational state, not general contributions.

Do not submit copyrighted Star Trek artwork, audio, dialogue, scripts, data, or other third-party material without the right to contribute it and preserve required notices. Read [LICENSE](LICENSE.md) and [LEGAL](LEGAL.md) before adding external content.

## Branch and commit workflow

Start from current `dev`. Use the governing issue number and a lowercase hyphenated slug:

- `feature/<issue>-<slug>`
- `fix/<issue>-<slug>`
- `task/<issue>-<slug>`
- `docs/<issue>-<slug>`

Qualifying bounded low-risk maintenance without a governing issue uses `standalone/<slug>` and targets only `dev`. The branch name grants no admission: Branch policy requires the installed workflow package to report a Standalone relationship and pass its Ready contract, including acceptance coverage, verification, and canonical risk declaration. Maintainers still judge whether the work fits; a clear automated result does not make significant work eligible.

Maintainers reserve `hotfix/<issue>-<slug>` for urgent work based on `main`. Do not target `main` for ordinary development.

Configure the tracked hooks once per checkout:

```bash
./scripts/setup-git-hooks.sh
```

Use Conventional Commit subjects, such as `feat: add sector navigation`, `fix(sensors): preserve contact history`, or `docs: clarify setup`.

## Architecture boundaries

Keep simulation and domain behavior in `AlterCourse.Core`, independent of Godot. Scenes, nodes, resources, and presentation belong in `AlterCourse.Godot`; AssetCtl remains independent from both. Add behavior and regression tests at the lowest layer that can prove the change.

Read the [ADR catalog](docs/adr/README.md) and affected records. In particular, preserve staged command outcomes, actor-owned information, bounded asset publication, and session/action lifetime under ADRs 0015–0018. These records do not approve deferred gameplay or require speculative frameworks. ADR 0013 owns branch, pull-request, hotfix, and release governance.

## Design changes

The [design wiki](docs/wiki/README.md) owns gameplay. Read the relevant page before changing behavior and reconcile it in the same PR as implementation. Keep rules, formulas, failure behavior, and milestone acceptance contracts there. An issue or implementation plan links the owning contract instead of creating a parallel specification.

An unapproved idea belongs in [open questions](docs/wiki/open-questions.md). Architectural boundaries change through new or amended ADRs; the wiki then reflects the decision. Use the existing [ADR template](docs/adr/adr.template.md) and [maintenance guidance](docs/adr/README.md#adding-or-changing-a-record), preserving stable IDs and accepted historical rationale.

Follow [recurring design reconciliation](docs/wiki/development-and-governance.md#recurring-design-reconciliation) at task start, behavior/bug checkpoints, before Ready, and at landing/release. Check the [review record](docs/wiki/sources.md#review-record) for an overdue seven-day sweep. An implementation constraint does not silently amend approved design.

PR Acceptance coverage names the reviewed pages, relevant source/tests, corrections, and any unchanged contract that remains exact. For no gameplay impact, state the concrete reason. A green gate or updated date is not semantic review; an old conformance run does not certify later ADRs.

## Verify the change

Run formatting when appropriate, then the canonical gate:

```bash
./scripts/fix.sh
./scripts/verify.sh
```

The gate checks formatting, static analysis, repository policies, secret scanning, warning-free builds, Core and AssetCtl tests, offline asset validation, Godot integration, and headless startup. Complementary managed Project Standards checks validate their adopted Markdown/frontmatter policy. [Development quality](docs/development-quality.md) owns the complete command and toolchain contract.

Fix failures at their cause. Do not weaken central settings, suppress diagnostics, alter policy, or claim an unexecuted check passed to admit the work. Distinguish local runs, hosted runs, inspected tests, and inherited evidence.

## Open the pull request

Open a draft against `dev`. Keep the exact required headings and supply:

- **Summary:** what changed and why.
- **Governing work:** exactly one `Final: #N`, `Supporting: #N`, or qualifying `Standalone` with its required risk line.
- **Acceptance coverage:** how the change satisfies its issue or Standalone outcome.
- **Verification:** commands/checks actually executed and their results, with unresolved limitations explicit.

Maintainers use the installed GitHub workflow for typed work state, readiness, merge, and lifecycle synchronization. A missing local capability does not authorize a substitute route or bypass. Topic PRs normally squash into `dev`; merged topic branches are deleted automatically.
