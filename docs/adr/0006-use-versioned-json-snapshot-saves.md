---
schema_version: '1.1'
id: 'adr-0006-star-trek-alter-course-use-versioned-json-snapshot-saves'
title: 'ADR 0006: Use Versioned JSON Snapshot Saves'
description: 'Defines save ownership, serialization, migration, validation, atomicity, and database exclusions.'
doc_type: 'adr'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'persistence'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0009-use-layered-testing-and-architecture-conformance.md'
supersedes: []
superseded_by: null
source:
  - 'https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview'
  - 'https://learn.microsoft.com/en-us/dotnet/standard/io/how-to-write-text-to-a-file'
  - 'https://learn.microsoft.com/en-us/dotnet/api/system.io.file.move'
  - 'https://learn.microsoft.com/en-us/dotnet/api/system.io.file.replace'
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

# Use versioned JSON snapshot saves

## Context and Problem Statement

Star Trek: Alter Course requires durable saves for a large, evolving world: ships, damage, repairs, faction relationships, wars, treaties, discoveries, locations, missions, scheduled events, and other consequences continue independently of the current screen. The save format therefore becomes a long-lived compatibility boundary rather than a convenience for restoring the player ship.

Serializing live C# object graphs or Godot nodes would preserve implementation details rather than game meaning. Introducing a database would add schema, deployment, backup, locking, and query infrastructure without a demonstrated need. A compact binary serializer could reduce file size, but it would make early debugging and migration failures less transparent.

This decision governs persisted player saves, autosaves, checkpoints, and test fixtures intended to represent recoverable simulation state. It applies whenever runtime state must survive process termination or be loaded by a later compatible build.

It does not govern authored domain content, preferences, keybindings, caches, logs, screenshots, telemetry, or editor state. Domain content is governed by ADR 0005. Simulation time, event scheduling, and random-state requirements are governed by ADR 0007.

How should the project persist the authoritative world state so that saves are diagnosable, migratable, testable, and independent from Godot implementation details?

## Decision Drivers

- A save must represent the persistent world, not only the player vessel.
- Historical saves need explicit compatibility handling as the simulation evolves.
- Save loading must work in headless Core tests.
- Corruption or partial writes must not silently replace the last valid save.
- Save data is untrusted input at load time, even when produced by the game.
- The format should support human inspection during early development.
- Runtime caches, scene state, and object references must not become compatibility obligations.
- Deterministic continuation requires simulation time, event order, and random-source state.
- Definition references should use stable content identifiers rather than duplicate all authored data.
- The project should avoid database and binary-format complexity until evidence justifies it.

## Considered Options

- Explicit versioned snapshot models serialized as JSON.
- Serialize the live Core object graph directly.
- Serialize Godot nodes and resources.
- Use an embedded database.
- Use a compact binary serializer as the canonical format.
- Use event sourcing as the canonical persistence model.

## Decision Outcome

Chosen option: "Explicit versioned snapshot models serialized as JSON", because it creates an intentional compatibility contract that remains inspectable, testable, and independent from the runtime object graph.

This decision governs recoverable simulation state. It does not require indefinite support for every development save, nor does it guarantee that all pre-release saves will remain compatible. It requires any supported compatibility promise to be explicit and mechanically tested.

### Snapshot boundary

A save is constructed from explicit persistence models owned by Core. Persistence models describe durable game meaning and are separate from:

- mutable domain entities used during simulation;
- Godot nodes, resources, signals, and scene paths;
- UI state and screen layout;
- logger objects and diagnostic sinks;
- delegates, tasks, threads, timers, and service instances;
- caches and indexes that can be reconstructed;
- transient command buffers and presentation interpolation;
- package-specific runtime types unless that package's format is itself an accepted compatibility boundary.

The mapping between runtime state and persistence models is explicit code. Reflection-based serialization of arbitrary runtime objects is not the save architecture.

Persistence models may be immutable records or similarly constrained data-transfer types. They should favor values with stable serialized meaning over inheritance-heavy polymorphic graphs.

### Canonical representation

The canonical save representation is UTF-8 JSON serialized with `System.Text.Json`.

The serializer configuration is explicit, version-controlled, and tested. It must not emit assembly-qualified type names or rely on default polymorphism that can instantiate arbitrary runtime types.

Human readability is a development and support advantage, not permission for users to edit saves without validation. Formatting may be indented during early development. Compression or compact output may be added without changing the logical snapshot contract if measurement shows meaningful size or load-time benefit.

A compact binary format such as MessagePack may be evaluated later only when representative saves demonstrate a material problem that cannot be addressed adequately by JSON formatting, compression, or data-shape improvements. Any such change requires a migration and diagnostic strategy; a binary format must not make the logical save schema implicit.

### Save envelope

Each save contains an envelope sufficient to determine compatibility before constructing the world. The exact property names are implementation details, but the envelope must represent:

- a save-schema version;
- a stable save identifier;
- creation and last-write timestamps for user-facing organization, not simulation decisions;
- the simulation time;
- the simulation-rules or build compatibility identity needed to interpret the snapshot;
- the content-set identity or required content references;
- the random algorithm version and persisted random-source states;
- the next stable scheduler sequence or equivalent event-order state;
- the authoritative world snapshot;
- optional integrity metadata;
- optional user-facing summary metadata that can be read without loading the entire world.

The save does not copy immutable authored definitions by default. It refers to definitions through stable identifiers governed by ADR 0005. A save may persist generated or mutated instance data that cannot be reconstructed from the base definition.

### Persisted and derived state

Persist state when at least one of these applies:

- player or autonomous actions can change it;
- reconstructing it would lose information;
- reconstructing it would require replay that is not part of the compatibility contract;
- it determines the future ordering or result of simulation;
- it is an intentional durable consequence.

Examples include faction relationships, ship condition, actor knowledge, scheduled events, mission state, territorial changes, inventories, random-source state, and the current simulation clock.

Do not persist state when all of these apply:

- it is a deterministic derivation of already persisted state and current compatible content;
- rebuilding it is bounded and reliable;
- it is not needed to preserve event ordering or identity;
- omitting it reduces compatibility surface.

Examples can include spatial indexes, lookup dictionaries, rendered labels, cached path results, compiled UI projections, and diagnostic logs.

The implementation must document any expensive derived state deliberately persisted as a cache and must be able to invalidate or rebuild it safely.

### Migration policy

Save compatibility is handled by an ordered migration pipeline.

A loader:

1. reads and bounds-checks the envelope;
2. identifies the source schema version;
3. rejects versions newer than it understands with a clear diagnostic;
4. applies a tested sequence of migrations to a current persistence model;
5. validates structural and semantic invariants;
6. resolves required content references;
7. constructs runtime state only after the snapshot is trustworthy.

Migrations operate on persistence representations, not live domain entities. Each migration has a single source and target version and is independently testable. Skipping versions is implemented by composing adjacent migrations unless an explicit optimized path is proven equivalent.

A migration does not silently invent consequential state. When old data lacks a required value, the migration uses a documented deterministic rule, a safe default whose meaning is understood, or rejects the save with an actionable explanation.

The project may declare development-era save versions unsupported. Removing support is an explicit compatibility decision accompanied by release notes or project documentation appropriate to the current distribution stage.

### Validation and trust boundary

Save files are untrusted input.

Before exposing loaded state to the simulation, the loader must validate:

- supported schema and algorithm versions;
- bounded collection sizes and string lengths where malicious or corrupt input could exhaust resources;
- unique identities;
- required references;
- finite and in-range numeric values;
- valid enum and state-machine states;
- scheduler ordering and event targets;
- random-state shape;
- cross-object domain invariants;
- compatibility with the selected content set.

Unknown members are rejected unless the envelope or a specific versioned extension point explicitly permits forward-compatible data. Silent member dropping is not a migration strategy.

A failed load does not partially mutate the active world. Construction occurs in isolation and commits only after validation succeeds.

### Atomic save writes and recovery

The save service uses a transactional file-write pattern:

1. serialize and validate the candidate snapshot;
2. write to a new temporary file in the target filesystem;
3. flush and close the file according to the supported platform's durability policy;
4. retain or rotate the previous known-good save when configured;
5. atomically replace or rename the target where the filesystem and platform support it;
6. surface a failure without deleting the previous valid save.

The exact filesystem calls may vary by supported platform. The required outcome is that interruption cannot make a partially written candidate indistinguishable from a completed save.

Autosave rotation, backup count, cloud synchronization, and conflict resolution remain product decisions. They must build on the same atomic save service rather than bypass it.

### Database exclusion

SQLite, LiteDB, an ORM, a document database, and an event-store product are not part of the save architecture.

An embedded database may be reconsidered only if representative world sizes or access patterns demonstrate requirements such as partial transactional updates, concurrent writers, or queries that cannot reasonably operate on loaded state. "The world is persistent" is not itself evidence that a database is needed.

The runtime has one authoritative in-memory simulation state. Save files are durable snapshots of that state, not a second live authority.

### Event-sourcing exclusion

Simulation events and structured logs are valuable for testing and diagnosis, but the canonical save is not reconstructed from an unbounded event log.

Event sourcing would make every historical event schema part of the permanent load contract, complicate migrations, and increase recovery work. Bounded replay fixtures or optional diagnostic command logs may be added for reproducibility without becoming the only persistence mechanism.

### Consequences

- Good, because save compatibility is separated from runtime class layout.
- Good, because developers can inspect and diagnose early saves directly.
- Good, because headless tests can exercise the full persistence pipeline.
- Good, because migrations are explicit and composable.
- Good, because a partial write does not automatically destroy the last valid state.
- Good, because Godot scenes and third-party runtime types do not become save obligations.
- Bad, because mapping code exists between domain and persistence models.
- Bad, because migration tests accumulate as supported versions increase.
- Bad, because JSON files may be larger and slower than compact binary representations.
- Bad, because strict load validation may reject hand-edited saves rather than guessing intent.

### Confirmation

A change is in scope when it adds persistent state, changes a persistence model, alters serializer behavior, adds a migration, changes compatibility metadata, or writes save files.

Conformance is confirmed by:

- Core-only save and load tests;
- current-version round trips checked by semantic equality rather than raw JSON equality;
- migration fixtures for every supported source version;
- corruption, truncation, unknown-member, broken-reference, and oversized-input tests;
- atomic-write interruption and recovery tests where the platform abstraction permits them;
- deterministic continuation tests that compare post-load behavior with uninterrupted behavior;
- architecture tests that reject Godot types from persistence models;
- explicit lock-step updates to schema version and migrations when serialized meaning changes.

## Pros and Cons of the Options

### Explicit versioned snapshot models serialized as JSON

- Good, because the compatibility boundary is intentional and visible.
- Good, because snapshots are easy to inspect and fixture in tests.
- Good, because format evolution can be migrated before domain construction.
- Bad, because explicit mapping and validation require code.
- Bad, because large worlds may eventually require compression or optimization.

### Serialize the live Core object graph directly

- Good, because initial implementation can be short.
- Good, because fewer mapping types are required.
- Bad, because private implementation changes become save-format changes.
- Bad, because services, caches, and cyclic references are easily serialized accidentally.
- Bad, because safe migration becomes difficult.

### Serialize Godot nodes and resources

- Good, because Godot provides engine serialization facilities.
- Bad, because saves become dependent on scene paths, scripts, and engine lifecycle.
- Bad, because headless Core tests cannot own the complete persistence path.
- Bad, because presentation and simulation compatibility become entangled.

### Use an embedded database

- Good, because transactions and partial queries are mature database capabilities.
- Good, because very large datasets can be updated without rewriting one file.
- Bad, because the current game has no demonstrated concurrent or query workload requiring it.
- Bad, because migrations, backups, corruption handling, and tooling become more complex.
- Bad, because the in-memory simulation and database can drift into dual authorities.

### Use a compact binary serializer as the canonical format

- Good, because files can be smaller and faster to parse.
- Good, because some serializers support generated high-performance code.
- Bad, because inspection and repair are harder.
- Bad, because type and version coupling can be hidden in serializer conventions.
- Bad, because performance benefit has not yet been measured.

### Use event sourcing as the canonical persistence model

- Good, because complete historical reconstruction and auditing are possible in principle.
- Good, because events can support debugging and analytics.
- Bad, because every event version becomes a compatibility burden.
- Bad, because load time and migration complexity grow with history.
- Bad, because the game does not require a distributed audit ledger.

## More Information

A readable JSON save is not a substitute for a supported diagnostic tool. As the schema grows, the project may add commands that inspect envelopes, validate saves, apply migrations to copies, and produce summaries without launching Godot.

The durability guarantees achievable by file replacement vary across filesystems and export platforms. Implementations must state and test the supported guarantee rather than claim universal atomicity from one API call.
