---
schema_version: '1.1'
id: 'adr-0015-star-trek-alter-course-use-staged-core-command-application-and-explicit-commit-outcomes'
title: 'ADR 0015: Use Staged Core Command Application and Explicit Commit Outcomes'
description: 'Defines command transaction boundaries, correlated candidate changes, rejection semantics, and post-commit failure reporting.'
doc_type: 'adr'
status: 'active'
created: '2026-09-27'
updated: '2026-09-27'
reviewed: '2026-09-27'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'simulation'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0008-use-structured-observability-with-serilog.md'
  - 'docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md'
  - 'docs/adr/0014-use-an-extensible-bounded-ship-system-substrate.md'
  - 'docs/adr/0016-own-actor-knowledge-and-share-immutable-observation-reports.md'
  - 'docs/adr/0018-separate-simulation-session-lifetime-from-workspaces.md'
  - 'docs/wiki/architecture.md'
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

# Use staged Core command application and explicit commit outcomes

## Context and Problem Statement

A single command can change a ship, its installed systems, actor knowledge, faction continuation, identity allocators, and scheduled work. Damage may force power and speed reconciliation or interrupt a repair. Travel may change observation and trigger later decisions. Committing only part of such an operation produces a world that no valid command intended.

Core already constructs candidate state and commits through `GameSimulation`. ADR 0007 requires serialized, exception-safe advancement but allows either staging or explicitly safe incremental transitions. ADR 0010 requires AI proposals to pass ordinary command validation. Neither makes every operation's commitment and caller-visible failure semantics the subject of a focused record.

This decision governs authoritative Core command application, advancement, and their caller-facing outcomes. It does not govern filesystem publication, save-file durability, gameplay formulas, or the internal algorithm of a policy. It codifies the current staged transaction pattern without replacing ADR 0007's allowance for a separately specified incremental operation.

Where does one authoritative operation commit, and what may its caller conclude when validation, execution, diagnostics, or presentation fails?

## Decision Drivers

- Coupled domain changes must preserve aggregate invariants and exact scheduled-work correlations.
- Expected refusal must not consume time, identities, or resources for an atomic command.
- Player, AI, and bootstrap entry points must not create competing rule authorities.
- Intermediate candidate state must be composable without becoming externally observable truth.
- A post-commit display or logging failure must not invite an unsafe retry.
- The contract must remain implementable without a command bus or transaction framework.

## Considered Options

- Stage correlated Core changes and return outcomes that distinguish commitment from presentation.
- Mutate the live aggregate in place and maintain compensating rollback for each consequence.
- Commit individual subsystem changes as they happen and report a single generic success or failure.

## Decision Outcome

Chosen option: "Stage correlated Core changes and return outcomes that distinguish commitment from presentation", because the current immutable candidate model makes coupled changes inspectable and prevents rejected work from leaking into the live world.

### One authoritative application boundary

Player intent and AI proposals pass explicit Core validation and application. Availability shown by a projection is advisory: application rechecks current authority, identity, location, capability, and operation prerequisites. Policy evaluation may construct candidates and explanations but does not mutate the live aggregate while ranking actions.

Core may resolve actor-safe intent to trusted internal correlations. Those correlations do not become permission to expose hidden targets or installed inventory; ADR 0016 governs information disclosure. A UI selection, display label, or disabled-button calculation is not command legality.

No universal command envelope, event bus, service locator, mediator package, or serialized executable callback is required. Focused typed entry points and application results are sufficient.

### Candidate composition and commitment

An atomic operation stages all of its consequential changes, including affected ships and factions, time, allocators, exact work cancellation/replacement, and player-safe outcome events. A candidate becomes live only after the complete operation boundary satisfies the required aggregate invariants.

Internal helpers may return intermediate candidates. Full aggregate validation must occur at the complete public operation or composed batch boundary, not indiscriminately after every helper. For example, a same-time batch may dequeue work before resolving each owner; that intermediate representation must not be published as a valid world or committed separately.

A failed candidate does not consume authoritative IDs, alter scheduler sequence continuation, advance the live clock, or publish its accumulated gameplay outcomes. Diagnostic evidence of the failure may still be recorded under ADR 0008; diagnostics are not committed gameplay state.

Voluntary command refusal remains different from mandatory consequence reconciliation. Damage that invalidates a speed or allocation must apply its specified forced reconciliation within the same candidate, not be rejected as though the victim had voluntarily requested an illegal configuration.

### Current operation contracts

The current public mutation entry points in `GameSimulation` use staged commitment. This includes travel, tactical course, sensor/contact operations, Engineering operations, orders, and firing, together with their required observation and scheduler consequences.

`AdvanceFixedSteps` and `AdvanceUntilNextPlayerRelevantEvent` stage their advancement before installing the returned candidate. Reaching the requested stopping condition is a successful operation, even when event-oriented advancement stops before a larger horizon. A work-budget or invariant failure during candidate construction does not make intermediate progress live.

Save/load is related but separately owned: ADR 0006 governs validated snapshot construction and file replacement; ADR 0018 governs installing a replacement simulation in the active session. This ADR does not equate an in-memory commit with a durable save.

### Outcome and failure semantics

- **Expected refusal:** return the operation's typed refusal without changing authoritative state for an atomic command.
- **Failure before commit:** discard the candidate and preserve the live world. Do not turn an internal failure into an ordinary gameplay refusal that conceals the defect.
- **Successful commit:** return the committed outcome and player-safe consequences. The caller must not recompute the outcome from a later projection.
- **Failure after commit:** presentation or optional diagnostics may degrade, but they must not recategorize the operation as unapplied. Refresh or report the presentation problem without automatically resubmitting the command.

These meanings do not require a new global result hierarchy. Existing focused result types and separated execution/presentation paths may express them. Process-fatal failures are not covered by a promise of recoverable in-process error reporting.

### Relationship to incremental advancement

ADR 0007's safely incremental alternative remains available; this record neither silently supersedes it nor declares all imaginable future operations atomic. A future operation that intentionally commits partial progress must first define its invariant-preserving commit boundaries, stopping/failure outcomes, and retry semantics in its owning contract, with continuation and failure tests. It cannot inherit a generic failure result that implies no state changed.

Changing an existing atomic operation to partial commitment changes its observable contract and requires explicit architectural review and coordinated documentation. Internal performance optimization may change copying or staging techniques without changing the selected outcome semantics.

### Consequences

- Good, because correlated state, time, and work change together or remain unchanged.
- Good, because callers can distinguish refusal, committed success, and degraded presentation.
- Good, because internal domain helpers remain composable and independently testable.
- Bad, because candidate construction and complete validation can cost memory and computation.
- Bad, because transaction boundaries and post-commit failure paths require deliberate tests rather than generic exception handling.

### Confirmation

Apply this record when changing a mutation entry point, consequence batch, candidate/commit boundary, or caller interpretation of an outcome. Confirm rejected-command state equality, allocator and scheduler continuation, coupled successful consequences, and pre-commit failure preservation at the lowest applicable layer. Test post-commit diagnostic and presentation failure separately from command rejection. New incremental behavior additionally needs explicit partial-progress and retry proofs.

Existing evidence entry points are [GameSimulation](../../src/AlterCourse.Core/Gameplay/GameSimulation.cs), [aggregate validation](../../src/AlterCourse.Core/Gameplay/SimulationState.cs), [combat application](../../src/AlterCourse.Core/Gameplay/GameSimulation.Combat.cs), [observation composition](../../src/AlterCourse.Core/Gameplay/GameSimulation.Observation.cs), [Core gameplay tests](../../tests/AlterCourse.Core.Tests/Gameplay/), and [GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs). These links identify inspectable evidence; adoption is not a fresh execution or certification of every path.

## Pros and Cons of the Options

### Stage correlated changes with explicit commit outcomes

- Good, because it matches the existing candidate model and exposes a reviewable commitment boundary.
- Bad, because the operation must account for every correlated consequence before commitment.

### Mutate live state with compensating rollback

- Good, because it can reduce copying for a measured hot path.
- Bad, because each new subsystem consequence adds rollback obligations, including IDs, time, and work ordering.

### Commit subsystem changes independently with generic failure

- Good, because each local transition initially appears simple.
- Bad, because callers cannot determine which consequences occurred or whether retry is safe.

## More Information

The owner selected this record after the September 27, 2026 additional-ADR review. It documents existing practice at `efd4ac93894fc28ea0c3ea40ca1be008dd4d6631`; it adds no runtime framework, gameplay mechanic, save version, or dependency. Detailed legal actions and consequences remain in their owning wiki contracts. Revisit implementation technique only with a concrete consumer or measured cost, preserving explicit outcome semantics.
