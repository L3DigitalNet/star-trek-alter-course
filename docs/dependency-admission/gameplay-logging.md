---
schema_version: '1.1'
id: 'reference-3y1097-gameplay-logging'
title: 'Gameplay Logging Dependency Admission'
description: 'Records the bounded Serilog gameplay-diagnostics composition and its dependency, failure, and removal boundaries.'
doc_type: 'reference'
status: 'active'
created: '2026-09-27'
updated: '2026-09-27'
reviewed: '2026-09-27'
owner: 'project-maintainers'
consumer: 'agent'
tags:
  - 'dependencies'
  - 'logging'
  - 'architecture'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0008-use-structured-observability-with-serilog.md'
source:
  - 'https://github.com/serilog/serilog-sinks-file'
  - 'https://github.com/serilog/serilog-extensions-logging'
  - 'https://github.com/serilog/serilog/wiki/Reliability'
  - 'https://github.com/serilog/serilog/wiki/Developing-a-sink'
confidence: 'high'
visibility: 'public'
license: null
---

# Gameplay logging dependency admission

This record admits the existing centrally pinned Microsoft logging and Serilog packages to the .NET 8 Godot game consumer for ADR 0008 diagnostics. It does not create a service container, framework, background service, or a second simulation authority. `AlterCourse.Core` depends only on `Microsoft.Extensions.Logging` abstractions; the Godot scene composition owns the Serilog provider.

## Consumer and behavior

`GameScreen` creates one scene-lifetime logging owner. It supplies the optional `ILogger<GameSimulation>` at Core bootstrap and emits game lifecycle, persistence outcome, player consequence, and allowlisted autonomous-decision facts. Core sends decision diagnostics only after its candidate state has committed. Diagnostics carry stable IDs, simulation time, controlled enums, and classification strings; they do not carry raw exceptions, arbitrary messages, content text, or provider SelfLog output.

The composition defaults to Information. The explicit Godot debug option enables the finer decision trace details. The normal sink writes JSON events to `gameplay-.json`, rolls daily and on the 4 MiB file threshold, and retains seven files. Rollover preserves whole events, so a file can exceed the threshold by one event; the actual production-configuration test checks this bound, newest retention, oldest eviction and released file handles. This is not a benchmark, a capacity guarantee, or a universal durability promise.

Startup, logger creation, emission, and disposal catch provider failures, use a bounded fallback notification, and preserve the original gameplay result. Serilog's reliability guidance explains that ordinary sink exceptions are suppressed to `SelfLog` unless an audit sink is used; this game does not forward `SelfLog`, because it can include local paths or exception payloads. The implementation deliberately does not use `AuditTo`.

## Dependency closure and compatibility

The consumer reuses central versions: Microsoft.Extensions.Logging 10.0.11, Serilog 4.4.0, Serilog.Extensions.Logging 10.0.0, Serilog.Sinks.Console 6.1.1, and Serilog.Sinks.File 7.0.0. The checked-in Godot lockfile is the authoritative resolved closure, including the new game-project direct references and all transitives. Serilog, its extensions package, and the sinks use Apache-2.0; Microsoft.Extensions.Logging uses MIT. Preserve their notices when a distribution requires it.

The Serilog.Extensions.Logging documentation recommends aligning its major version with the target Microsoft logging version. The project reuses the existing 10.x selection for a .NET 8 game target; source and lockfile inspection establish the intended closure, while the owning implementation's restore/build evidence establishes compatibility. This admission does not claim compatibility with arbitrary framework or package versions.

## Alternatives and removal

Core keeps the Microsoft logging abstraction because it accepts an optional logger without knowing Serilog, Godot, files, or a composition root. Native Godot output cannot provide the structured retained-file boundary or Core decision-provider seam required by ADR 0008. A global Serilog logger, a Core Serilog reference, a service locator, and a general dependency-injection container would broaden authority without serving this one scene-owned consumer.

Removal means replacing the Godot composition and its tests while preserving the optional Core abstraction and post-commit/failure-isolation behavior, then removing the Godot package references and their lockfile entries. It requires no save migration, content migration, or game-rule change because logs remain nonauthoritative.

## Evidence and limits

[GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs), [scene logging composition](../../src/AlterCourse.Godot/src/Gameplay/Logging/GameplayLogging.cs), [allowlisted diagnostic events](../../src/AlterCourse.Godot/src/Gameplay/Logging/GameDiagnostics.cs), [Core decision diagnostics](../../src/AlterCourse.Core/Gameplay/GameSimulation.Diagnostics.cs), [central package versions](../../Directory.Packages.props), and [Godot lockfile](../../src/AlterCourse.Godot/packages.lock.json) are the repository evidence. The Serilog [file sink README](https://github.com/serilog/serilog-sinks-file), [Microsoft logging provider README](https://github.com/serilog/serilog-extensions-logging), [reliability guidance](https://github.com/serilog/serilog/wiki/Reliability), and [sink-development guidance](https://github.com/serilog/serilog/wiki/Developing-a-sink) are the reviewed upstream references.

The [conformance register](../reviews/adr-conformance-2026-09-27.md) records exact runtime and test receipts, including actual rendered console/JSON privacy and disabled, collecting, failing and disposal-boundary equivalence. No performance measurement, universal crash/power-loss durability, or authoritative gameplay audit-log claim is made.
