# Pull Request Standard

The pull request is the execution record for a work contract. From package version 1.7 this reference also owns _admission_: how a change reaches the default branch at all, what a PR declares about the work it serves, and what a PR must say before it is ready. Repository rulesets, branch protection, and required checks still decide what is mechanically permitted; nothing here authorizes routing around them.

## Admission: T0 or a pull request

There are exactly two admission classes. A change is either a **T0** direct commit or it goes through a pull request. There is no third tier and no repository-configurable middle ground.

T0 is a conjunctive predicate: every condition must hold, and one failure disqualifies the whole change.

1. Every hunk is an unambiguous spelling, grammar, punctuation, or prose-reflow correction.
2. Every proposition, obligation, instruction, identifier, reference, and machine interpretation is unchanged.
3. No protected surface is touched (below).
4. The change lies outside the scope of active governed work.
5. No non-T0 change is mixed in.
6. It spans at most 3 files and at most 30 added-plus-deleted lines.
7. Repository validation passes.
8. Repository instructions and live GitHub enforcement permit the push.

The file and line ceiling is a blast-radius backstop, not the test. Semantic impact is the boundary: a two-word edit that changes what a sentence obliges is not T0, and neither is a typo fix inside a code block.

**Protected surfaces.** Executable source and tests; comments; scripts; structured or machine-consumed data; CI, build, deploy, and infrastructure configuration; dependencies and lockfiles; schemas and migrations; generated, digest, or release state; security or enforcement material; code blocks and operational command examples; and normative decisions, specifications, standards, policy, acceptance criteria, legal text, or work-state records.

A direct T0 commit carries exactly one trailer:

```text
Workflow-Admission: T0
```

A PR-mediated commit and a commit on a construction branch carry none. The trailer is a classification you apply and stand behind — no subcommand evaluates the predicate, and none can, because conditions 1, 2, and 4 are judgments about meaning. Its value is retrospective: `git log --grep 'Workflow-Admission: T0'` enumerates every direct admission for an on-demand audit, which is the only T0 review this package defines. There is no routine T0 report and no standing register of admitted commits.

Everything that is not T0 begins as a draft pull request. Draft is the default because it makes Ready a real boundary: structural problems are visible before anyone is asked to review, and no reviewer is summoned by an incomplete change.

## Governing work

Every PR declares exactly one canonical relationship beneath an exact `## Governing work` heading, as the entire content of that section:

| Declaration | Meaning |
| --- | --- |
| `Final: #N` | This PR claims to satisfy every remaining acceptance criterion of Issue #N. |
| `Supporting: #N` | This PR contributes to Issue #N without claiming completion. |
| `Standalone` | This PR owns its own outcome, acceptance criteria, and risk; it has no governing Issue. |

The declarations are mutually exclusive, and only these three spellings establish the relationship — a closing keyword, a title mention, or prose elsewhere in the body does not. One Issue may have any number of Supporting PRs but at most one open Final. The GitHub closing keyword is restricted to an exact `Closes #N` on a Final PR; a Supporting or Standalone PR that carries one is declaring a completion it does not own.

The relationship is mutable only while the PR is a draft and auto-merge is disabled; changing it after that requires returning the PR to draft, and Structural and Ready validation run again. A terminal PR's relationship is immutable evidence: a historical contradiction is recorded as an additive evidence-integrity finding, never repaired by rewriting the body.

## The ready contract

A PR that is ready for review has exactly four required sections:

```markdown
## Summary

What changed and why.

## Governing work

Final: #N | Supporting: #N | Standalone

## Acceptance coverage

How this change satisfies the governing acceptance criteria — the Issue's for Final and Supporting, its own for Standalone.

## Verification

Commands and checks actually executed, with their outcomes.
```

Four sections, no boilerplate: there is no empty "Risk" or "Follow-up" heading to fill in when there is nothing to say. **Acceptance coverage** ties the change to stated criteria so a reviewer can judge completeness without reconstructing intent. **Verification** records what actually ran; a command listed there but never executed is a false evidence claim.

A Standalone PR additionally declares its own risk on the line immediately after `Standalone`:

```text
Change risk: R2 Moderate
```

The value is one of exactly `R1 Low`, `R2 Moderate`, `R3 High`, or `R4 Critical` — the same four spellings [org-schema.yaml](org-schema.yaml) gives the `Change risk` field, because a Standalone PR's declaration is authoritative in the same way an Issue's field value is. A bare `R2` is refused: the Ready gate reports `GHW-PR-READY-RISK-INVALID` and names the four accepted values.

`Change risk` measures how dangerous it is to implement the change incorrectly. `R1 Low` takes normal tests and review; `R2 Moderate` adds an acceptance-criteria trace and focused regression coverage; `R3 High` adds independent review, negative testing, and explicit rollback consideration. **`R4 Critical` requires evidence, in the Summary or Acceptance coverage, of all four of:** a plan agreed before implementation, a recovery or rollback procedure, negative testing, and independent verification. `R4 Critical` requires no ceremonial Issue and no second approval — the controls are technical, not procedural. [review-checklist.md](review-checklist.md) carries the review ladder these values drive.

## Lifecycle coherence

The governing Issue's `Workflow` field is the sole lifecycle authority for governed work. A PR is evidence; merging is an event, not a lifecycle write.

| Condition | Requirement |
| --- | --- |
| Open Final or Supporting PR | Issue `Workflow` is `In progress`, `In review`, or `Blocked` |
| Final PR marked ready | Issue `Workflow` is `In review` or `Blocked`; `ready --pr N` performs the `In progress` → `In review` synchronization itself |
| Final PR merging | Refused while the Issue is `Blocked` |
| Supporting PR merging | Permitted while the Issue is `Blocked` only with explicit acceptance-coverage rationale that neither resolves nor conceals the blocker |
| Final PR merged | `merge --pr N` converges the Issue to `Workflow = Done` and closes it as completed, in the same call |
| Supporting or Standalone PR merged | Lifecycle-neutral; never authorizes `Done` |
| Final PR closed unmerged | No lifecycle outcome is inferred; `close --pr N --as OUTCOME --reason S` records an explicit disposition and converges the Issue |

Closing an open Final without a merge is the one PR closure this package owns end to end: it writes an immutable `Final-Disposition: VALUE` / `Reason: S` comment on the PR before closing it, so the abandonment carries its reason permanently.

## Follow-up work

Durable work discovered during implementation is disposed of before the session ends, never silently lost: fixed in place when this repository owns it and the session can take it, filed against the owning repository when an upstream dependency in the organization owns it, or put to the operator when it warrants its own session. Do not leave significant future work only as a review comment, a prose TODO, or a session note.

## Existing pull requests

A PR opened before this version's conventions is repaired when it is next touched — when a summary, check, ready, or merge run reports it. Nothing scans for incompatible PRs proactively, and no terminal PR's evidence is rewritten to match the current shape.
