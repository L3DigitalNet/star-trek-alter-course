---
schema_version: '1.1'
id: 'adr-0009-star-trek-alter-course-use-layered-testing-and-architecture-conformance'
title: 'ADR 0009: Use Layered Testing and Architecture Conformance'
description: 'Defines the test layers, selected frameworks, reproducibility requirements, and mechanical architecture checks.'
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
  - 'validation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0002-use-one-canonical-quality-gate.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
supersedes: []
superseded_by: null
source:
  - 'https://xunit.net/'
  - 'https://github.com/godot-gdunit-labs/gdUnit4'
  - 'https://github.com/godot-gdunit-labs/gdUnit4Net'
  - 'https://github.com/AnthonyLloyd/CsCheck'
  - 'https://github.com/TNG/ArchUnitNET'
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

# Use layered testing and architecture conformance

## Context and Problem Statement

Star Trek: Alter Course is an interconnected persistent simulation. A defect in power allocation can alter sensor range, which changes AI knowledge, which changes diplomacy or combat decisions, which changes the world days later. Ordinary method-level unit tests are necessary but cannot establish that these interactions remain valid. At the same time, running every test through Godot would make the feedback loop slow and would weaken the Core boundary.

The project already uses xUnit for pure .NET tests and one canonical verification path under ADR 0002. The remaining decision is how to divide test responsibilities among ordinary unit tests, generated properties, long-running scenarios, Godot integration tests, and architecture checks without creating overlapping frameworks or fragile end-to-end suites.

This decision governs automated tests for Core behavior, content and persistence, Godot integration, architectural boundaries, and simulation scenarios. It applies whenever a feature changes game rules, durable state, engine integration, or a protected architectural relationship.

It does not define release acceptance, manual playtesting, usability research, balance targets, performance budgets, mutation-score thresholds, or visual-art review. Those concerns may use these test facilities but require separate criteria.

How should the project layer automated testing so that simulation defects are found at the lowest useful level while cross-system and engine behavior still receive realistic coverage?

## Decision Drivers

- Pure simulation tests must run through ordinary `dotnet test` without starting Godot.
- State transitions and failure modes need fast focused regression tests.
- Cross-system interactions need scenario and long-running simulation coverage.
- Large state spaces require generated invariant testing rather than only example cases.
- Save and content compatibility need durable fixtures and negative tests.
- Godot scenes, signals, input, and lifecycle behavior require engine-aware tests.
- Architecture rules should fail mechanically rather than rely solely on review.
- Randomized failures must report reproducible seeds and minimized cases.
- Tests must not duplicate the entire implementation or depend on incidental private structure.
- The normal verification path must remain deterministic and fast enough for every change.
- Expensive deep tests need a defined place without weakening the normal gate.

## Considered Options

- A layered suite using xUnit, CsCheck, GdUnit4Net, and ArchUnitNET according to responsibility.
- Use only xUnit and custom helpers for every test type.
- Run nearly all tests through Godot.
- Rely primarily on end-to-end playthrough automation.
- Rely on unit tests and manual playtesting without property or architecture tests.

## Decision Outcome

Chosen option: "A layered suite using xUnit, CsCheck, GdUnit4Net, and ArchUnitNET according to responsibility", because no single test style efficiently covers pure rules, generated state spaces, engine behavior, and dependency constraints.

This decision governs test ownership and selected frameworks. ADR 0002 remains authoritative for command ordering, CI entry points, formatter and analyzer enforcement, and which suites run in the normal versus deep verification path.

### Test placement principle

A behavior is tested at the lowest layer that can establish the requirement faithfully.

- Pure rules and domain state transitions are tested in Core.
- Cross-domain behavior that does not require Godot remains a Core scenario test.
- Serialization, migrations, content validation, AI decisions, and long-running world evolution remain Core tests.
- Scene-tree lifecycle, Godot signals, resources, input, rendering adapters, and engine services use Godot-aware tests.
- A small end-to-end test verifies that layers are wired together; it does not replace lower-level coverage.

A test must not start Godot merely because production eventually presents the result in Godot.

### xUnit baseline

xUnit is the primary framework for `AlterCourse.Core.Tests` and other ordinary .NET test projects.

Use xUnit for:

- value-object and algorithm tests;
- domain state transitions;
- invalid-command and degraded-state behavior;
- subsystem interaction tests;
- deterministic AI decision tests;
- scheduler and random-source tests;
- content parsing and semantic validation;
- save round trips and migrations;
- scenario and regression fixtures;
- headless long-running simulations;
- architecture-test hosting where the selected library integrates with xUnit.

Tests should prefer public or internal behavioral seams over reflection into private implementation. An internal member may be exposed to a test assembly when doing so represents a meaningful test boundary, not merely to assert local variables.

Theory data is appropriate for a bounded set of meaningful named cases. Large combinatorial spaces belong in property-based tests.

### State-transition and subsystem tests

Each domain system is tested in terms of preconditions, accepted commands, resulting state, emitted outcomes, scheduled work, and rejected operations.

Tests cover more than the nominal success path. Depending on the system, required categories include:

- zero, boundary, and maximum values;
- damaged, degraded, offline, and partially repaired states;
- insufficient power, fuel, crew, information, or access;
- cancellation, interruption, and competing priorities;
- invalid identities and stale references;
- same-time event ordering;
- repeated or idempotent commands where applicable;
- state restoration after save and load;
- downstream effects on dependent systems.

A test should identify the invariant or player-visible rule it protects. It should not merely reproduce the current sequence of method calls.

### Scenario and long-running simulation tests

Scenario tests exercise multiple real Core systems against a controlled initial world and ordered commands. They verify consequential outcomes rather than every intermediate object.

Representative scenarios include:

- travel while repairs and faction events progress;
- power loss cascading into sensors, shields, and tactical options;
- diplomacy changing access, routes, and strategic AI choices;
- multi-ship combat with subsystem damage and withdrawal;
- a treaty expiring while actors are in transit;
- save and load in the middle of scheduled work;
- autonomous faction behavior over extended simulation time.

Long-running tests advance seeded worlds for large numbers of events or simulated days and assert global invariants. They are intended to find accumulation, starvation, impossible states, memory growth, scheduler loops, and emergent regressions.

A long-running failure records:

- the scenario and content-set identity;
- initial seed and random stream context;
- command sequence or replay fixture;
- simulation time and last stable event;
- relevant structured diagnostics;
- the violated invariant.

Tests that are fast and stable enough belong in the canonical gate. Larger stress matrices may run through a separate deep-validation command or scheduled CI workflow governed by ADR 0002.

### Property, model, and metamorphic testing

CsCheck is the selected framework for property-based, model-based, and metamorphic tests.

CsCheck is introduced when a subsystem has a meaningful generator and invariant, not to rewrite every example test. Good candidates include:

- resource and power conservation;
- nonnegative bounded quantities;
- route and map invariants;
- scheduler order;
- damage allocation;
- repair progress;
- save round-trip equivalence;
- content reference graphs;
- diplomacy relationship constraints;
- deterministic AI candidate selection;
- collections of actors and events over long command sequences.

A property test states a rule that must hold over many generated cases. It does not assert a tautology derived from the same helper used by production.

Model-based tests compare a system under test with a simpler independently reasoned model. The model should be smaller and less optimized than production so that both are unlikely to share the same defect.

Metamorphic tests verify relationships when an exact oracle is difficult. Examples include invariance under entity insertion order, equivalent unit conversion, or route-cost changes after a controlled modifier.

Every generated failure must report a reproducible seed and the minimized counterexample. A discovered counterexample that protects a significant defect should also become a named regression test when that improves clarity.

### Godot integration testing

GdUnit4Net, backed by GdUnit4, is the selected framework for C# tests that require Godot.

Use it for:

- scene construction and teardown;
- node lifecycle;
- signals and deferred calls;
- input-action mapping and focus behavior;
- Godot resource and asset integration;
- map view projection, hit testing, selection, and command translation;
- UI state derived from Core snapshots;
- pause and frame behavior at the Core adapter boundary;
- export-specific engine behavior where supported by the test environment.

Logic-only tests remain ordinary .NET tests. GdUnit4Net tests opt into a Godot runtime only when the subject requires it.

Godot integration tests do not duplicate Core rule assertions. For example, a tactical control test verifies that clicking a valid target produces the correct Core command and displays the resulting view; Core tests establish whether the weapon hit and how damage propagated.

GDScript tests may be used when a Godot addon or engine fixture is naturally expressed in GDScript. New production gameplay remains C# unless separately decided.

### Architecture conformance

Compile-time project references are the first architecture boundary. `AlterCourse.Core` cannot reference `AlterCourse.Godot` or Godot assemblies under ADR 0001.

ArchUnitNET is the selected tool for architecture rules that are not fully expressible by the project graph. It should be added when the first such rule is introduced and then remain part of the normal test suite.

Candidate rules include:

- Core namespaces do not depend on Godot namespaces.
- Domain entities do not depend on persistence or presentation adapters.
- AI evaluation depends on actor knowledge projections rather than Godot or unrestricted world-view adapters.
- persistence models do not contain Godot or service types;
- presentation namespaces may depend on Core contracts but Core cannot depend on presentation;
- test-only or tooling assemblies are not referenced by production code;
- concrete logging configuration remains outside pure domain namespaces.

Architecture tests complement, rather than replace, project structure, analyzers, and review. A rule should encode a durable architectural boundary, not transient folder preference.

### Test doubles and clocks

Prefer real value objects and small in-memory implementations over deep mocking.

Use a test double when it isolates a genuine boundary such as file I/O, wall-clock time, diagnostic sinks, or a content repository. Do not mock entities to make the test mirror an implementation's call sequence.

Simulation time and randomness are explicit Core inputs under ADR 0007. Tests use deterministic clocks, known random streams, or controlled fakes without patching global state. Application code that needs wall-clock behavior uses injectable `TimeProvider` where appropriate.

### Persistence and content fixtures

A fixture that represents a supported save or content version is treated as a compatibility artifact. It has an identified purpose and should not be regenerated automatically merely because output formatting changed.

Persistence tests compare semantic state. Raw JSON snapshots are used only where exact wire shape is the contract under test.

Negative fixtures are required for corruption and invalid-input classes that have historically caused defects or that cross a trust boundary. Examples include duplicate IDs, unknown fields, unsupported versions, missing references, nonfinite values, truncated saves, and scheduler targets that no longer exist.

### Determinism and isolation

Tests must be independent of:

- execution order;
- local timezone and culture;
- machine-specific paths;
- ambient wall-clock time;
- global random state;
- Godot editor state unless explicitly an engine integration test;
- external hosted services;
- internet availability.

Parallel test execution is permitted only when tests do not share mutable process or filesystem state. Tests requiring exclusive access declare and minimize that requirement.

Flaky tests are defects. Retrying a test may gather evidence but is not the accepted fix.

### Performance and balance tests

Performance tests use representative workloads and report distributions or bounded thresholds appropriate to the environment. They are separated from correctness assertions when infrastructure noise would make the canonical gate unreliable.

Balance tests can assert broad invariants, dominance limits, or scenario outcomes, but they must not freeze every tuning number accidentally. A tuning change that intentionally alters outcomes updates balance expectations with an explanation.

### Coverage and mutation testing

Line or branch coverage is diagnostic evidence, not the definition of adequate testing. Required behavior and failure states drive test selection.

Mutation testing remains a deep-validation tool under ADR 0002. A mutation-score threshold is not established by this ADR. Persistent surviving mutations in critical Core rules are evidence of missing assertions and should be addressed or explicitly analyzed.

### Consequences

- Good, because most simulation behavior runs in a fast ordinary .NET test loop.
- Good, because generated tests explore combinations humans would not enumerate.
- Good, because Godot integration receives engine-realistic coverage without contaminating Core tests.
- Good, because architecture boundaries can fail mechanically.
- Good, because long-running simulations target emergent and accumulation defects.
- Bad, because the repository will use several test libraries with distinct purposes.
- Bad, because good generators and independent models require substantial thought.
- Bad, because scenario fixtures and supported migration fixtures become maintained assets.
- Bad, because deep simulation and mutation suites may consume significant CI time.

### Confirmation

A change is in scope when it adds or changes Core behavior, persistent data, content validation, Godot integration, or an architectural boundary.

Conformance is confirmed by:

- behavioral tests at the lowest applicable layer;
- explicit negative and degraded-state coverage;
- deterministic seeds and reproducible generated failures;
- relevant scenario or long-running coverage for cross-system effects;
- GdUnit4Net tests only where Godot behavior is actually required;
- ArchUnitNET or compile-time enforcement for durable dependency rules;
- successful execution through the canonical verification command;
- review that tests assert requirements rather than incidental implementation details.

## Pros and Cons of the Options

### A layered suite using xUnit, CsCheck, GdUnit4Net, and ArchUnitNET according to responsibility

- Good, because each tool addresses a distinct testing problem.
- Good, because Core remains fast and engine-independent.
- Good, because generated, scenario, integration, and architecture defects all have an owner.
- Bad, because contributors must understand where a test belongs.
- Bad, because dependency and framework upgrades require coordination.

### Use only xUnit and custom helpers for every test type

- Good, because the framework surface is small.
- Good, because xUnit can host many test styles.
- Bad, because property shrinking, model generation, Godot lifecycle, and architecture queries would be reimplemented locally.
- Bad, because custom helpers can become undocumented internal frameworks.

### Run nearly all tests through Godot

- Good, because tests execute in an environment close to the shipped game.
- Good, because engine APIs are always available.
- Bad, because Core feedback becomes slower and more brittle.
- Bad, because simulation logic is encouraged to depend on engine lifecycle.
- Bad, because large scenario matrices become expensive.

### Rely primarily on end-to-end playthrough automation

- Good, because tests cover real wiring and player flows.
- Good, because broad regressions can be visible.
- Bad, because failures are slow and difficult to localize.
- Bad, because state-space coverage remains shallow.
- Bad, because rare simulation interactions require impractically many complete runs.

### Rely on unit tests and manual playtesting without property or architecture tests

- Good, because the initial toolchain and suite are simple.
- Good, because manual play can find usability and balance problems.
- Bad, because combinatorial and long-running invariants remain weakly covered.
- Bad, because architecture drift depends on reviewer memory.
- Bad, because reproducibility and migration failures can escape until late.

## More Information

This ADR selects testing responsibilities and libraries, not package versions. ADR 0003 and central package management govern dependency admission and pinning.

The smallest complete test suite for a new system normally includes focused transition tests, negative cases, one cross-system scenario when the system interacts with others, and a property test only when a meaningful invariant and generator exist. The objective is depth at the right layer, not ceremonial use of every test type.
