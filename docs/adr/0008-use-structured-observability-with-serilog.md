---
schema_version: '1.1'
id: 'adr-0008-star-trek-alter-course-use-structured-observability-with-serilog'
title: 'ADR 0008: Use Structured Observability with Serilog'
description: 'Defines the logging backend, Core abstraction, simulation trace fields, retention boundary, and non-authoritative role of logs.'
doc_type: 'adr'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'observability'
  - 'simulation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0009-use-layered-testing-and-architecture-conformance.md'
  - 'docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md'
supersedes: []
superseded_by: null
source:
  - 'https://github.com/serilog/serilog'
  - 'https://github.com/serilog/serilog-extensions-logging'
  - 'https://learn.microsoft.com/en-us/dotnet/core/extensions/logging'
  - 'https://learn.microsoft.com/en-us/dotnet/core/extensions/logging-providers'
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

# Use structured observability with Serilog

## Context and Problem Statement

A persistent simulation can fail in ways that are difficult to reproduce from a screenshot or exception alone. A faction may declare war because of a sequence of relationship changes, an AI may select an apparently irrational target, a repair may stall because of an upstream power failure, or an event may execute in an unexpected order days after its cause.

Plain text statements such as "Klingon ship attacked" do not preserve enough context to investigate these behaviors. Conversely, treating every domain event as a permanent log record would create excessive volume and tempt the project to use diagnostics as a second save system.

This decision governs application logging, structured simulation diagnostics, decision traces, sink selection, and the boundary between observability and authoritative state. It applies to Core orchestration, Godot integration, content and save loading, tooling, AI decisions, and long-running test runs.

It does not define player-facing event history, captain's logs, mission journals, replay files, analytics collection, crash-report upload, or external telemetry. Those may consume some of the same structured facts but require separate privacy, retention, and product decisions.

How should the project capture enough structured evidence to explain simulation behavior without coupling Core to global logging state or making logs authoritative?

## Decision Drivers

- Long-running simulation failures need actor, time, location, and causal context.
- Strategic AI must be able to explain candidate actions, constraints, and scores.
- Structured fields must be queryable without parsing prose.
- Logging must not alter simulation outcomes or event order.
- Core must remain headless and independent from Godot.
- Runtime configuration should control sinks and verbosity without changing domain rules.
- High-volume traces need bounded retention and selective enablement.
- Secrets, personal paths, and copyrighted source material must not leak into logs.
- Tests need to capture diagnostics without writing uncontrolled files.
- The project should use mature logging infrastructure rather than implement formatting, filtering, rolling files, and sink routing itself.

## Considered Options

- Use Microsoft logging abstractions with Serilog as the runtime structured-logging backend.
- Depend on Serilog directly throughout Core.
- Use Godot's print and error functions.
- Build a project-specific logging framework.
- Store every domain event as an authoritative event log.

## Decision Outcome

Chosen option: "Use Microsoft logging abstractions with Serilog as the runtime structured-logging backend", because it combines a stable dependency-injection and testing boundary with Serilog's mature structured-event and sink ecosystem.

This decision governs diagnostic logging and traces. It does not require log calls in every domain method, and it does not make logging a prerequisite for a state transition to succeed.

### Logging ownership and dependency direction

The application composition root configures Serilog and connects it to `Microsoft.Extensions.Logging`.

Core orchestration or application-service code that needs operational logging receives `Microsoft.Extensions.Logging.ILogger<T>` through construction. Domain entities, value objects, and pure rule functions do not locate a logger through static state or a service locator.

High-value simulation explanations are represented first as typed result or diagnostic data when the caller needs them for gameplay, tests, or AI explanation. Logging may serialize those facts, but a required explanation must not exist only in an ephemeral log message.

A narrow project-owned diagnostic sink may be introduced for extremely high-volume deterministic traces if ordinary logging abstractions cannot express the required batching or capture semantics. It must adapt to the same structured pipeline and cannot become a parallel general logging framework.

`AlterCourse.Core` must not depend on Godot's print, error, or debugger APIs. `AlterCourse.Godot` may bridge Godot engine errors and application lifecycle information into the configured logger.

### Structured event model

Log events use message templates and named properties. Consequential data belongs in fields, not only in rendered prose.

Depending on event type, fields should include stable values such as:

- event name or diagnostic code;
- simulation time;
- save, run, scenario, or session correlation identifier;
- command and scheduled-event identifiers;
- actor, ship, faction, system, route, mission, and encounter identifiers;
- subsystem and component identifiers;
- previous and resulting state;
- decision candidate, score components, rejected constraints, and selected action;
- content definition and schema versions;
- random stream identity and draw context when diagnostic capture is enabled;
- elapsed wall-clock duration for performance measurements;
- exception and failure classification.

Display names may accompany stable identifiers but do not replace them. Localized player-facing text is not used as a machine field.

Message templates remain stable enough for human search, while diagnostic codes or event names provide a stronger machine contract when logs feed test assertions or support tools.

### Simulation time and wall-clock time

Every simulation-affecting diagnostic includes the authoritative simulation time when one exists.

The logging infrastructure may add a wall-clock timestamp automatically. The two are never conflated:

- simulation time explains when the event occurred in the galaxy;
- wall-clock time explains when the process emitted or handled the diagnostic.

Performance timings use monotonic application timing, not simulation time. Simulation decisions must not read logger timestamps.

### Levels and expected use

Logging levels follow consistent intent:

- `Trace` records high-volume step-by-step details useful only for focused investigation.
- `Debug` records decision candidates, derived values, and development diagnostics.
- `Information` records meaningful lifecycle milestones and consequential but expected outcomes.
- `Warning` records recoverable abnormal conditions, degraded fallbacks, or suspicious input that remains safe.
- `Error` records failed operations, rejected state transitions caused by defects or external failures, and corrupted or incompatible data.
- `Critical` records failures that prevent the application or authoritative simulation from continuing safely.

Expected player choices, failed diplomacy checks, misses in combat, ordinary AI rejection of a candidate, and valid content absence behind an optional feature are not automatically warnings or errors.

A validation failure can be an expected result in a user-facing command while still carrying structured diagnostic detail at an appropriate level.

### AI and causal traces

Strategic and tactical decision systems expose enough information to answer why an action was chosen.

A decision trace may include:

- the actor's information snapshot identity;
- eligible goals and candidates;
- hard constraints that removed candidates;
- score components and modifiers;
- deterministic tie-breaking;
- selected action and alternatives;
- random stream usage, if randomness affected selection;
- resulting command identifier.

Full candidate traces are generally `Debug` or `Trace` because they may be large. A concise selection record may be `Information` when the action has strategic consequence.

The trace reports what the actor knew, not omniscient world truth, unless the diagnostic explicitly labels both. This supports ADR 0010's information-bound AI model.

### Sinks and configuration

The default development configuration uses:

- a human-readable console sink;
- a structured rolling file sink suitable for later querying;
- an in-memory or test sink when tests need assertions.

Release sink selection, file location, rotation size, retention count, and default minimum levels are configuration decisions. They must have bounded defaults and must not assume indefinite disk growth.

Additional sinks, remote collectors, dashboards, or telemetry services are admitted only under ADR 0003 with an explicit privacy and operational purpose. The game must remain fully functional offline and without a hosted logging service.

Sink failure is isolated from simulation correctness. A full disk, inaccessible log path, or failing remote sink must not change game rules or leave Core in a partially applied state. A severe logging failure may be surfaced to the user or disable further file logging, but it does not become an alternate transaction mechanism.

### Sensitive and high-volume data

Logs must not contain:

- credentials, tokens, private keys, or provider secrets;
- unrestricted environment dumps;
- full user home paths when a normalized or relative path is sufficient;
- full save payloads by default;
- large binary or text assets;
- copyrighted source material imported for research;
- personal information not required to diagnose the local operation.

Potentially sensitive values are redacted at the source or represented by stable hashes or identifiers when correlation is necessary.

High-volume tactical ticks, path candidates, sensor samples, and random draws are disabled by default or sampled through an explicit diagnostic mode. Diagnostic modes must not change random consumption, ordering, or simulation results.

### Logs are not authority

Logs are append-only diagnostic evidence, not a source of game state.

The simulation does not:

- rebuild saves from ordinary logs;
- decide whether an event occurred by querying a log sink;
- use successful emission as confirmation that a state transition committed;
- mutate state from a log processor;
- require log retention for save compatibility.

Player-facing history that must survive must be represented in durable domain state and persisted under ADR 0006. A replay or command journal, if introduced, has its own schema and compatibility policy.

### Error reporting

Exceptions are logged at a boundary that can add operation context and decide recovery. Lower layers should not repeatedly catch, log, and rethrow the same exception, which produces duplicate records without adding meaning.

Validation and expected failure paths favor structured result objects over exceptions. Unexpected exceptions carry the original exception object and stable contextual fields.

Logs must distinguish:

- invalid external input;
- unsupported compatibility version;
- recoverable I/O failure;
- violated domain invariant;
- programming defect;
- user cancellation;
- expected negative game outcome.

This classification enables reliable support and test assertions.

### Consequences

- Good, because diagnostics can be filtered by stable actor, event, location, and decision fields.
- Good, because Core remains independent from Godot and a concrete sink.
- Good, because Serilog supplies mature formatting, filtering, enrichment, and sink support.
- Good, because AI and simulation failures can carry causal evidence.
- Good, because logs remain optional diagnostics rather than save authority.
- Bad, because structured event names and fields require consistency and review.
- Bad, because detailed traces can consume significant disk space if enabled indiscriminately.
- Bad, because the runtime uses both Microsoft logging abstractions and Serilog integration packages.
- Bad, because typed explanations and logs can overlap and require a clear ownership distinction.

### Confirmation

A change is in scope when it adds logging, changes the backend or sinks, introduces a diagnostic event, logs simulation decisions, or retains log data.

Conformance is confirmed by:

- dependency tests that keep Serilog configuration at the application boundary;
- tests that Core behavior is identical with no-op, collecting, and failing sinks;
- structured-log tests for important event names and required fields;
- absence of string interpolation where a stable message template and fields are required;
- bounded sink and retention configuration;
- redaction tests for known sensitive fields;
- long-running simulation tests that report correlation, simulation time, and reproducibility context;
- review that player-required history is persisted as domain state rather than only logged.

## Pros and Cons of the Options

### Use Microsoft logging abstractions with Serilog as the runtime structured-logging backend

- Good, because Core receives a standard testable abstraction.
- Good, because Serilog provides mature structured event processing and many sinks.
- Good, because the concrete backend can be configured in one composition root.
- Bad, because integration requires more than one package.
- Bad, because careless abstraction use can still spread logging through pure domain code.

### Depend on Serilog directly throughout Core

- Good, because Serilog's native structured API is expressive.
- Good, because fewer adapter concepts are involved.
- Bad, because the concrete logging library becomes part of every Core consumer.
- Bad, because replacement and test isolation are harder.
- Bad, because domain code is more likely to depend on logging behavior.

### Use Godot's print and error functions

- Good, because no extra package is required.
- Good, because output appears directly in the editor.
- Bad, because Core becomes dependent on Godot or loses equivalent diagnostics.
- Bad, because structured fields, rolling files, and sink routing are limited.
- Bad, because long-running headless test analysis becomes difficult.

### Build a project-specific logging framework

- Good, because the API could be tailored exactly to the simulation.
- Bad, because formatting, filtering, enrichment, rolling files, and sink reliability are generic solved problems.
- Bad, because local infrastructure would require long-term maintenance.
- Bad, because integrations and ecosystem tools would need custom adapters.

### Store every domain event as an authoritative event log

- Good, because complete history and replay could become possible.
- Good, because all consequential actions would be auditable.
- Bad, because diagnostic and persistence contracts would be conflated.
- Bad, because event schema migration and unbounded history would become foundational.
- Bad, because most logs are observations or derivations, not state transitions.

## More Information

Serilog is selected as the runtime backend, not as the domain architecture. A future backend replacement should be possible at the composition root without rewriting simulation rules.

Structured logging complements deterministic reproduction. ADR 0007 provides the state and input contract needed to reproduce behavior; this ADR provides evidence for finding and understanding the relevant path.
