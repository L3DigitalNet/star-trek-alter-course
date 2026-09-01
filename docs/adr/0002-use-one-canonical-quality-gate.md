---
schema_version: '1.1'
id: 'adr-0002-star-trek-alter-course-use-one-canonical-quality-gate'
title: 'ADR 0002: Use One Canonical Quality Gate'
description: 'Defines the deterministic compiler, analyzer, formatting, testing, and CI enforcement path.'
doc_type: 'adr'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'development'
  - 'testing'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
supersedes: []
superseded_by: null
source: []
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

# Use one canonical quality gate

## Context and Problem Statement

Agents, developers, editors, and CI can drift when each reproduces a different subset of build and test commands. This decision governs normal repository verification for C#, shell, GitHub Actions, secrets, and Godot integration. It does not put slow mutation testing in every commit gate or replace Project Standards' managed Markdown workflows.

How should the repository make normal development quality deterministic across local and CI environments?

## Decision Drivers

- Compiler and analyzer warnings must fail builds.
- Formatting and tool versions must not depend on workstation-global installations.
- Diagnostic suppression must be explicit and reviewable.
- CI must execute the repository implementation rather than duplicate it in workflow YAML.
- The normal loop must remain fast enough for every change.

## Considered Options

- One repository script with pinned tools, called directly by CI.
- Independent local documentation and duplicated CI steps.
- Git hooks as the primary enforcement boundary.

## Decision Outcome

Chosen option: "One repository script with pinned tools, called directly by CI", because `./scripts/verify.sh` gives every caller the same ordered, executable contract.

CSharpier is the sole C# formatter. Modern SDK analyzers plus curated Meziantou and banned-API rules form the analyzer baseline. Suppression exceptions live in a narrow allowlist. Stryker remains a separate deep-validation command until an evidence-based score policy exists.

### Consequences

- Good, because local success predicts CI success from the same command.
- Good, because checks cannot silently continue after a failure.
- Bad, because the first run downloads checksum-pinned native tools and the Godot editor when missing.

### Confirmation

The `Canonical verification` CI job invokes only `./scripts/verify.sh` after checkout and .NET setup. The script records tracked state before and after its checks and fails if verification changes it.

## Pros and Cons of the Options

### One repository script with pinned tools, called directly by CI

- Good, because command ordering and failure semantics have one owner.
- Good, because editor tasks can call the same entry points.
- Bad, because the script must remain portable across supported Linux environments.

### Independent local documentation and duplicated CI steps

- Good, because each environment can be tuned independently.
- Bad, because duplicated command sequences drift and create false confidence.

### Git hooks as the primary enforcement boundary

- Good, because feedback can occur before a commit.
- Bad, because hooks are optional local state and are not an authoritative merge gate.
