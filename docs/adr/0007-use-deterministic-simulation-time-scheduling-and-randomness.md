---
schema_version: '1.1'
id: 'adr-0007-star-trek-alter-course-use-deterministic-simulation-time-scheduling-and-randomness'
title: 'ADR 0007: Use Deterministic Simulation Time, Scheduling, and Randomness'
description: 'Defines the simulation clock, event ordering, random-source contract, replay scope, and wall-clock boundary.'
doc_type: 'adr'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'simulation'
  - 'testing'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0008-use-structured-observability-with-serilog.md'
  - 'docs/adr/0009-use-layered-testing-and-architecture-conformance.md'
supersedes: []
superseded_by: null
source:
  - 'https://learn.microsoft.com/en-us/dotnet/fundamentals/runtime-libraries/system-random'
  - 'https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.priorityqueue-2'
  - 'https://learn.microsoft.com/en-us/dotnet/core/extensions/timeprovider'
  - 'https://learn.microsoft.com/en-us/dotnet/core/extensions/timeprovider-testing'
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

# Use deterministic simulation time, scheduling, and randomness

## Context and Problem Statement

The galaxy in Star Trek: Alter Course must continue to act while the player travels, repairs, negotiates, waits, or fights. Tactical actions may resolve in seconds, repairs in minutes or days, travel in hours or weeks, and faction strategy on longer cadences. The game therefore needs one authoritative simulation-time model that supports multiple operational scales without binding simulation progress to frame rate or wall-clock time.

Randomness is also necessary for uncertain sensors, damage, procedural generation, AI variation, and events. Uncontrolled use of ambient time, `System.Random`, unordered collections, asynchronous callbacks, or unspecified tie-breaking would make defects difficult to reproduce and saves unable to continue the same simulation reliably.

This decision governs all time advancement, scheduled simulation work, random choices, and deterministic ordering in `AlterCourse.Core`. It applies whenever a rule reads time, schedules future consequences, consumes randomness, or resolves multiple state changes.

It does not govern rendering interpolation, real-time UI animation, audio timing, operating-system timestamps, network lockstep, or a promise of bit-identical results across every CPU and runtime. Those concerns remain outside the current single-player simulation contract.

How should the project advance a persistent world and resolve uncertainty so that behavior remains reproducible, serializable, and independently testable?

## Decision Drivers

- The world must progress independently of player screen changes and frame rate.
- Actions at different scales must share one coherent timeline.
- Repairs, travel, diplomacy, strategic AI, and tactical resolution need schedulable consequences.
- Saving and loading must preserve future event order and random continuation.
- Tests need exact control of time and random outcomes.
- Defects found in long-running simulations need reproducible seeds and inputs.
- AI choices must be explainable rather than changing because of hidden iteration order.
- Wall-clock and asynchronous runtime scheduling must not become simulation authority.
- The implementation should use simple data structures before adopting a workflow or scheduler framework.
- Strict multiplayer lockstep is not a current requirement and should not impose premature constraints.

## Considered Options

- Explicit simulation time, a deterministic serializable scheduler, and injected versioned random sources.
- Advance the world from Godot frame and timer callbacks.
- Use wall-clock timestamps and ordinary asynchronous tasks.
- Use `System.Random` directly throughout the domain.
- Adopt a generic workflow or job-scheduling framework.

## Decision Outcome

Chosen option: "Explicit simulation time, a deterministic serializable scheduler, and injected versioned random sources", because it makes time and uncertainty part of the domain state rather than ambient process behavior.

This decision governs simulation-affecting time and randomness in Core. It does not require every subsystem to update at the smallest tactical interval, and it does not require continuous polling of inactive systems.

### Authoritative simulation time

Core owns a monotonic simulation-time value and explicit duration values.

Simulation time represents elapsed time within the game universe. It is not represented by `DateTime.Now`, `DateTime.UtcNow`, Godot frame time, an operating-system timer, or time elapsed while the process was closed.

Commands and processes advance time through explicit Core operations. Examples include:

- resolving a tactical interval;
- traveling to a destination;
- waiting for a scan or repair phase;
- advancing to the next decision opportunity;
- processing until a specified simulation time;
- processing the next scheduled event.

A pause stops simulation advancement without changing world meaning. A slow frame rate may delay presentation but does not enlarge a tactical step. Loading a save does not infer elapsed simulation time from the save file's wall-clock timestamp.

Wall-clock timestamps may be used for save organization, logs, metrics, and UI presentation, but they cannot change Core outcomes unless a future feature explicitly introduces real-world-time gameplay through a separate decision.

### Multiple time scales

One timeline does not mean one universal update frequency.

Each subsystem uses the coarsest resolution that preserves its intended behavior. Tactical motion may resolve in small fixed or bounded intervals, while strategic AI may schedule its next reassessment hours or days later. Repairs may schedule milestones or accumulate work over elapsed durations rather than execute one update per second.

The scheduler advances from one meaningful event boundary to the next where possible. The project will not simulate every inactive ship on every tactical tick merely to preserve a conceptual global clock.

When a subsystem integrates continuously over elapsed time, its formula and rounding policy must be deterministic and tested at meaningful boundaries. Repeated small steps and one large step are not assumed equivalent unless the rule explicitly guarantees that property.

### Deterministic scheduler

Core owns a serializable scheduler implemented with focused .NET collections and project domain types. A generic workflow engine is not part of the baseline.

Every scheduled item contains enough data to:

- identify its due simulation time;
- establish a total order among items with the same due time;
- identify the target and event or command kind;
- persist the data required for later resolution;
- validate that the target and event kind remain meaningful on load;
- produce structured diagnostic context.

Same-time ordering uses an explicit monotonic sequence or another stable total-order key persisted in the save. Heap layout, object address, hash order, thread completion order, and enum declaration order are not valid tie-breakers.

Scheduled items are data, not serialized delegates. Resolution dispatches a known event kind or typed command through explicit Core code. This makes migration and validation possible.

Cancellation and replacement use stable scheduled-item identities or explicitly modeled ownership. Removing an actor must not leave an unvalidated callback that later mutates unrelated state.

### Advancement semantics

A simulation advancement operation:

1. validates the target time and command preconditions;
2. determines the next due work in stable order;
3. advances the authoritative clock to the next relevant boundary;
4. resolves all items due at that boundary according to the total order;
5. applies resulting state transitions and schedules any follow-on work;
6. repeats until the requested stopping condition is reached;
7. returns a result containing consequential outcomes and the final time.

An event scheduled for the current time during resolution receives a defined ordering policy. The implementation must prevent an unbounded zero-time event loop through validation, an execution budget, cycle detection, or another explicit guard.

Exceptions do not leave partially applied state masquerading as a successful advancement. The exact transaction technique may vary by operation, but state transitions must either be safely incremental with invariant checks or staged before commit.

### Mutation and concurrency

Authoritative Core state mutations occur in a deterministic serialized order.

Background threads may perform isolated calculations against immutable snapshots only when their results are validated and committed in a deterministic order. The first implementation will not parallelize simulation mutation.

Godot signals, timers, tasks, and frame callbacks may request a Core command. Their arrival becomes explicit input and is ordered by the Core command boundary; they do not mutate domain entities directly.

This policy favors reproducibility over speculative throughput. Performance optimization must begin with profiling representative long-running simulations.

### Random-source abstraction

Domain code receives randomness through an injected project-owned abstraction, conceptually an `IRandomSource`, rather than constructing or accessing a process-global generator.

The abstraction supports the operations actually needed by the domain, such as:

- bounded integer selection;
- uniform fractional selection;
- probability checks;
- deterministic shuffling or selection;
- export and restoration of generator state;
- identification of the algorithm and stream.

The interface must avoid ambiguous methods whose endpoint or bias semantics are unclear.

The concrete simulation generator uses a documented, fixed algorithm whose version is part of persisted state. Selection of the exact algorithm is an implementation decision that must include reference test vectors and acceptable statistical properties. PCG-family or xoshiro-family generators are reasonable candidates; the choice must be recorded before saves depend on it.

`System.Random` is not the persisted simulation contract. Microsoft explicitly does not guarantee an identical implementation across major .NET versions, so a seed alone cannot provide the required long-term continuation guarantee. `System.Random` may still be used in non-simulation tooling or tests where cross-version sequence stability is irrelevant.

Cryptographic randomness is not required for ordinary gameplay outcomes. Security-sensitive token generation, if any, uses the platform cryptographic APIs outside this simulation abstraction.

### Random streams

Unrelated systems should not consume one undifferentiated global sequence.

The simulation may derive named or scoped streams from a root seed using a documented deterministic derivation. Candidate scopes include world generation, strategic factions, tactical encounters, damage resolution, and authored event selection.

Stream partitioning serves two purposes:

- a change to cosmetic or unrelated random consumption does not perturb every later outcome;
- diagnostics can identify which subsystem made a random choice.

Streams must not be created from unstable values such as runtime hash codes, object addresses, iteration order, or localized display names. Stream identities use stable domain identifiers and an explicit derivation function.

Partitioning is not used to guarantee that all implementation changes preserve historical outcomes. A rule change may legitimately change consumption within its stream and must be treated as a simulation compatibility change when supported saves depend on exact continuation.

### Persisted random state

A save persists:

- the algorithm identifier and version;
- root seed or derivation identity where required;
- the full current state of every stream whose future sequence affects the world;
- enough stream metadata to validate and reconstruct ownership.

Persisting only the initial seed is insufficient once choices have been consumed. Persisting random outcomes as hidden derived fields is not a substitute for generator state unless the outcome itself is durable domain state.

Loading rejects unsupported algorithm versions rather than silently substituting a different generator.

### Determinism contract

Within one supported simulation-rules version, the following inputs must reproduce the same semantically observable Core outcome:

- the same valid initial snapshot;
- the same compatible content set;
- the same ordered command inputs;
- the same scheduler state;
- the same random algorithm and stream states;
- the same configuration that is declared simulation-affecting.

The implementation must avoid nondeterminism from unordered iteration, culture-sensitive parsing, ambient locale, unspecified tie-breaking, unbounded floating-point equality, and concurrency races.

The current contract is semantic determinism for supported single-player environments and automated tests. It is not yet a guarantee of bit-for-bit identical serialized bytes across all platforms, deterministic networking, or permanent replay compatibility across arbitrary rules changes.

If future multiplayer or verified replay requires stricter cross-platform determinism, that requirement needs a separate ADR addressing numeric representation, platform variation, command synchronization, and compatibility.

### Time abstraction outside simulation time

Code that legitimately needs wall-clock or timer behavior, such as save timestamps or application services, receives an injectable time abstraction where tests need control. .NET `TimeProvider` and its testing support are the preferred standard-library mechanism.

`TimeProvider` is not the simulation clock. It prevents ambient wall-clock coupling in application code while Core's universe time remains an explicit domain value.

### Consequences

- Good, because saves continue the same event and random streams rather than merely recreating similar state.
- Good, because long-running simulation defects can be reproduced from state, commands, and stream identities.
- Good, because tactical and strategic systems share one timeline without sharing one update rate.
- Good, because inactive systems can advance by meaningful events rather than frame polling.
- Good, because AI and other random choices can be traced to stable streams.
- Bad, because event types, ordering, and random state become explicit compatibility surfaces.
- Bad, because developers cannot casually use ambient timers or `Random.Shared` inside Core.
- Bad, because deterministic ordering can reduce opportunities for straightforward parallel mutation.
- Bad, because rules changes may intentionally invalidate exact historical replay even when a save can be migrated.

### Confirmation

A change is in scope when Core reads time, advances the world, schedules or cancels work, resolves same-time events, consumes randomness, introduces concurrency, or changes an algorithm that affects deterministic outcomes.

Conformance is confirmed by:

- banned-API or architecture checks for ambient Core time and random sources;
- scheduler tests covering same-time ordering, cancellation, rescheduling, zero-time loops, and load continuation;
- reference-vector tests for the selected random algorithm;
- tests that save and restore active random streams;
- replay tests from identical snapshots and command sequences;
- stable-order tests that vary dictionary insertion or entity construction order;
- tests proving frame rate and wall-clock time do not change Core outcomes;
- long-running scenario failures that report a reproducible seed, stream, and command context.

## Pros and Cons of the Options

### Explicit simulation time, a deterministic serializable scheduler, and injected versioned random sources

- Good, because time and uncertainty are controlled inputs and state.
- Good, because the world can progress efficiently at multiple scales.
- Good, because save continuation and automated reproduction are feasible.
- Bad, because the scheduler and random-state contracts require careful versioning.
- Bad, because all domain APIs must pass explicit time and randomness where needed.

### Advance the world from Godot frame and timer callbacks

- Good, because engine callbacks are convenient for visual gameplay.
- Good, because real-time effects naturally receive frame delta.
- Bad, because strategic outcomes become tied to rendering cadence and engine lifecycle.
- Bad, because headless Core simulation becomes difficult.
- Bad, because pausing, loading, and accelerated travel require special cases.

### Use wall-clock timestamps and ordinary asynchronous tasks

- Good, because platform APIs provide timers and task scheduling.
- Bad, because process scheduling and elapsed real time are nondeterministic inputs.
- Bad, because saves and tests cannot reproduce pending task behavior reliably.
- Bad, because the world could change merely because the application stalled.

### Use `System.Random` directly throughout the domain

- Good, because it is built into .NET and easy to call.
- Bad, because call sites become hidden and difficult to reproduce.
- Bad, because algorithm identity and complete stream state are not an explicit save contract.
- Bad, because same-seed sequences are not guaranteed across major .NET versions.
- Bad, because unrelated random consumption perturbs the entire world.

### Adopt a generic workflow or job-scheduling framework

- Good, because mature frameworks can provide persistence, retries, and monitoring.
- Good, because complex business workflows can be modeled declaratively.
- Bad, because the game needs an in-process deterministic event queue, not distributed job execution.
- Bad, because framework persistence and retry semantics would compete with Core state.
- Bad, because the scheduler's actual algorithm is small while the domain event semantics remain custom.

## More Information

Determinism is a design aid, testing capability, and save-continuation property. It is not a promise that an upgraded game must reproduce every random outcome from an older executable. Save migration and replay compatibility are related but distinct commitments.

A simulation trace may record commands, decisions, and selected random outcomes for diagnosis, but ADR 0008 makes logs non-authoritative. The persisted scheduler and random states remain the continuation contract.
