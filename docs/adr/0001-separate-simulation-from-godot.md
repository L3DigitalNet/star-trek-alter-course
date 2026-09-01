---
schema_version: '1.1'
id: 'adr-0001-star-trek-alter-course-separate-simulation-from-godot'
title: 'ADR 0001: Separate Simulation from Godot'
description: 'Defines the one-way dependency boundary between pure simulation code and Godot adapters.'
doc_type: 'adr'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'testing'
aliases: []
related:
  - 'docs/adr/0002-use-one-canonical-quality-gate.md'
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

# Separate simulation from Godot

## Context and Problem Statement

The project needs deterministic strategic and ship simulation that can run quickly without starting Godot, while scenes, nodes, resources, input, and presentation still require the engine. This decision governs new runtime C# code and project references. It does not prescribe game-system design or require abstractions before code needs them.

How should the repository make the simulation-to-engine dependency direction mechanically enforceable?

## Decision Drivers

- Pure simulation tests need a fast ordinary `dotnet test` loop.
- Simulation results must not depend accidentally on engine state, ambient time, or process randomness.
- Godot-facing code must remain free to use engine APIs and its main-thread execution model.
- The repository is early enough to establish the boundary without moving functional game code.

## Considered Options

- Separate pure Core and Godot adapter projects with one-way references.
- Keep all C# in one Godot project and rely on namespaces and review.
- Introduce a larger multi-layer framework before game systems exist.

## Decision Outcome

Chosen option: "Separate pure Core and Godot adapter projects with one-way references", because project references make the important dependency direction a compile-time property without speculative layers.

`AlterCourse.Godot` may reference `AlterCourse.Core`; Core must never reference Godot. Pure tests target Core directly. GdUnit tests exercise engine-facing integration separately.

### Consequences

- Good, because simulation code compiles and tests without loading the engine.
- Good, because banned ambient APIs can be enforced narrowly in Core without restricting legitimate adapters.
- Bad, because cross-boundary data must use types that do not expose Godot objects.

### Confirmation

The solution reference graph, the Core architecture smoke test, and the canonical verifier confirm the boundary. A change that adds a Godot reference to Core fails before merge.

## Pros and Cons of the Options

### Separate pure Core and Godot adapter projects with one-way references

- Good, because the compiler and ordinary tests enforce the boundary.
- Good, because it adds only one meaningful project split.
- Bad, because developers must choose the correct project for new code.

### Keep all C# in one Godot project and rely on namespaces and review

- Good, because initial file placement is simpler.
- Bad, because engine coupling becomes a convention that agents and humans can violate silently.

### Introduce a larger multi-layer framework before game systems exist

- Good, because it could reserve more future boundaries.
- Bad, because those boundaries would be speculative and costly to change.
