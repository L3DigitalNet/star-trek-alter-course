# Issue Types and Body Structure

The Issue is the authorized work contract. Its Type and fields describe the work operationally; its body defines the work semantically. Choosing the Type and writing the body is agent judgment; `gh-workflow new` scaffolds the canonical headings and applies the initial field values.

## Issue Types

The Type vocabulary is deliberately small. Five types, no local extensions.

### Bug

Existing behavior violates an intended contract.

Examples:

- regression
- incorrect output
- crash
- reliability defect
- broken integration

A Bug carries `Severity`; no other Type does.

### Feature

Introduces a new user-visible or system-visible capability.

### Task

Bounded work that is neither a defect nor a new capability.

Examples include:

- maintenance
- refactoring
- dependency work
- documentation
- CI changes
- infrastructure work
- cleanup

### Initiative

A parent planning object representing a larger objective implemented through sub-issues.

An Initiative should generally not itself be dispatched to an implementation agent, which is why it omits the execution-oriented fields in the pinning matrix. Use native sub-issues for the hierarchy rather than a parent-identifier field.

### Research

A bounded investigation intended to reduce uncertainty and produce a durable result.

A Research Issue must still have acceptance criteria. For example:

> Determine whether library X satisfies requirements A–D and publish the recommendation in `docs/research/...`.

Research is work, not merely an open question.

## Issue body

Structured fields do not replace the work contract. The body carries the narrative and high-cardinality information. Canonical structure:

```markdown
## Outcome

What must become true when this Issue is complete.

## Context

Why the work exists and relevant background.

## Scope

What is included.

## Out of scope

Explicit boundaries where ambiguity would otherwise exist.

## Acceptance criteria

Observable conditions required for completion.

## Constraints

Relevant technical, architectural, compatibility, security, or repository-policy requirements.

## Evidence / references

Relevant reproduction information, logs, specifications, ADRs, external references, or prior work.

## Verification

Any specific validation required beyond normal repository policy.
```

Not every Issue requires every heading. The principle is:

> Fields describe the work operationally; the body defines the work semantically.

Acceptance criteria are the exception to that optionality for executable work: an Issue without them cannot legitimately reach `Ready`, and the honest state for one that lacks them is `Needs definition`.

## The canonical acceptance section

`## Acceptance criteria` is machine-significant. `check` and `receipt` read that exact level-2 heading, with that exact spelling, and treat its content as the criteria; the section is satisfied when it carries at least one nonempty item. A synonym — `## Acceptance`, `## Success criteria`, `## Done when` — reads as absent, and a criteria list written under any other heading is invisible to the gate no matter how good it is.

The same section is what a Final or Supporting PR's `## Acceptance coverage` answers, so the two documents line up item by item. If an Issue's criteria live somewhere else for a reason, move them under the canonical heading rather than teaching the gate a second spelling: one heading is what keeps the Issue, the check, and the PR talking about the same list.
