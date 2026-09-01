---
schema_version: '1.1'
id: 'adr-0003-star-trek-alter-course-prefer-native-capabilities-and-demand-driven-dependencies'
title: 'ADR 0003: Prefer Native Capabilities and Demand-Driven Dependencies'
description: 'Defines how the project selects Godot features, .NET packages, Godot addons, and custom implementations.'
doc_type: 'adr'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'development'
  - 'dependencies'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0002-use-one-canonical-quality-gate.md'
  - 'docs/adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md'
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
supersedes: []
superseded_by: null
source:
  - 'https://docs.godotengine.org/en/4.6/getting_started/step_by_step/scripting_languages.html'
  - 'https://docs.godotengine.org/en/4.6/tutorials/scripting/c_sharp/c_sharp_collections.html'
  - 'https://docs.godotengine.org/en/4.6/tutorials/ui/'
  - 'https://learn.microsoft.com/en-us/nuget/consume-packages/central-package-management'
  - 'https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files'
  - 'https://gaea-docs.readthedocs.io/en/2.x/'
  - 'https://github.com/Portponky/better-terrain'
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

# Prefer native capabilities and demand-driven dependencies

## Context and Problem Statement

Star Trek: Alter Course can draw from three overlapping ecosystems: Godot's built-in engine capabilities, the ordinary .NET Base Class Library and NuGet ecosystem, and Godot-specific addons or GDExtensions. The project also has the option to implement narrow capabilities itself. All four paths can be appropriate, but selecting dependencies opportunistically would create inconsistent architecture, unnecessary maintenance, engine-version coupling, and transitive risk.

This decision governs the admission and lifecycle of runtime libraries, development-only libraries, Godot addons, GDExtensions, and framework-like internal abstractions. It applies whenever a change proposes a new dependency, expands the role of an existing dependency, or replaces native functionality with third-party code.

It does not prohibit a dependency merely because equivalent code could be written locally. It also does not require every small algorithm to be custom. The purpose is to select the least costly reliable owner for each capability while preserving the simulation boundary established by ADR 0001.

This decision does not choose the detailed spatial model, content format, save format, AI design, narrative integration, or test architecture. Those concerns are governed by their own ADRs.

How should the project decide whether a capability belongs in Godot, the .NET runtime, a focused package or addon, or project-owned code?

## Decision Drivers

- The game must remain maintainable by a small team using coding agents extensively.
- Simulation correctness and testability have higher priority than framework convenience.
- `AlterCourse.Core` must remain independent from Godot under ADR 0001.
- Godot already supplies capable 2D rendering, UI, input, audio, localization, navigation, tile-map, and procedural-noise facilities.
- Godot-specific addons can create engine-version, export-platform, native-binary, and editor-workflow coupling.
- Ordinary .NET packages generally have broader tooling, testing, and package-management support than Godot addons.
- Every dependency creates update, security, licensing, compatibility, and removal obligations.
- Reimplementing mature generic infrastructure can be less reliable than adopting a focused library.
- Generalized frameworks can force the game's unusual simulation into abstractions designed for conventional RPGs, shooters, or scripted encounters.
- Dependency versions and transitive resolution must be reproducible for local agents and CI.

## Considered Options

- Prefer native and standard-library capabilities, then admit focused dependencies only for demonstrated needs.
- Select a comprehensive Godot game framework and use its conventions across the project.
- Prefer third-party packages for most reusable algorithms and infrastructure.
- Implement nearly all capabilities in project-owned code.

## Decision Outcome

Chosen option: "Prefer native and standard-library capabilities, then admit focused dependencies only for demonstrated needs", because it reuses mature generic infrastructure without surrendering the project's domain model or accumulating speculative framework commitments.

This decision governs all new or materially expanded dependencies. It does not govern ordinary asset files, operating-system packages used only to run the supported toolchain, or dependencies already mandated by Godot and the .NET SDK.

### Capability ownership order

A proposed capability is evaluated according to the layer in which it belongs.

For `AlterCourse.Core`, use this order:

1. Existing project domain code when the capability is already represented correctly.
2. The .NET Base Class Library when it provides a sufficient, independently testable implementation.
3. A focused, mature NuGet package when it removes meaningful generic complexity or risk.
4. A small project-owned implementation when the behavior is domain-defining, the required algorithm is narrow, or dependency cost exceeds implementation cost.
5. A broad framework only when multiple concrete consumers demonstrate that the framework's model is a better fit than focused components.

For `AlterCourse.Godot`, use this order:

1. Godot's native nodes, resources, servers, editor facilities, and import pipeline.
2. A thin project adapter around native Godot behavior.
3. A focused managed NuGet package that remains compatible with Godot's supported .NET runtime.
4. A Godot addon or GDExtension whose engine compatibility, export behavior, maintenance, and license have been verified.
5. A custom engine extension only when no supported managed or native Godot path satisfies a measured requirement.

This ordering is a decision procedure, not an automatic rejection rule. A lower-ranked choice may win when evidence shows it is safer, simpler, or more capable for the concrete use case.

### Native Godot defaults

The project will use Godot's native presentation stack for:

- `Control` nodes, containers, themes, focus, and input for the information-dense command interface;
- `Node2D`, `Camera2D`, drawing, hit testing, and scene composition for 2D map presentation;
- audio playback and buses;
- input actions and device mapping;
- localization and bidirectional layout support;
- `TileMapLayer`, `AStar2D`, and `AStarGrid2D` where their engine-facing use cases apply;
- `FastNoiseLite` for ordinary noise generation;
- asset import and Godot `Resource` objects for presentation-specific data.

A third-party UI framework, map renderer, localization framework, or general procedural-generation framework is not part of the baseline.

### Dependency admission evidence

A change introducing or materially expanding a dependency must identify:

- the concrete current consumer and requirement;
- why the higher-ranked native or standard-library choices are insufficient;
- the package's maintenance state and compatibility with the repository's Godot and .NET versions;
- its license and any distribution obligations;
- transitive dependencies and native binaries;
- supported export platforms relevant to the project;
- deterministic and headless-test behavior where the dependency touches simulation;
- the expected failure mode if the dependency becomes unavailable;
- the scope of project code coupled to its API;
- a plausible removal or replacement boundary.

The evidence may be concise for a small development-only package. A runtime framework, native extension, save-format component, or package that enters `AlterCourse.Core` requires proportionally deeper review.

### Version and source control

NuGet package versions remain centrally owned by `Directory.Packages.props`. Restore lock files remain committed, and CI restores in locked mode through the canonical verification path established by ADR 0002. Floating versions are not permitted.

Godot addons and source dependencies must be pinned to an immutable release or commit. A package manager such as GodotEnv may be evaluated when actual addon-management work justifies it; it is not added solely to anticipate future addons.

Vendoring is not the default. It requires a specific reason such as upstream distribution constraints, an unavailable package feed, a reviewed local patch, or a reliability requirement that cannot be met by an immutable upstream reference.

### Frameworks and infrastructure excluded from the baseline

The project will not add the following solely for hypothetical scale or future convenience:

- a database, ORM, embedded document database, or event-store framework;
- an entity-component-system framework;
- a dependency-injection container;
- a generic event-bus or message-broker framework;
- a networking, service, or hosted-backend layer;
- an embedded Lua, JavaScript, or other general scripting runtime;
- a comprehensive RPG, strategy-game, quest, inventory, or economy framework;
- a general rules engine;
- a behavior-tree framework as the strategic-AI foundation.

These are not permanent bans. Introducing one requires a concrete near-term use case and a new or amended architectural decision because each changes a foundational ownership boundary.

### Deferred ecosystem candidates

The following candidates are recognized but are not baseline dependencies:

- Better Terrain may be evaluated for a concrete tile-based local-map authoring requirement.
- DelaunatorSharp may be evaluated for procedural Delaunay or Voronoi generation.
- Clipper2 may be evaluated for bounded polygon clipping or offset operations.
- NetTopologySuite may be evaluated only if geometry requirements become GIS-like or require robust topology beyond a focused polygon library.
- Gaea 2.x is not a foundational dependency while its own documentation characterizes it as early-development software not ready for larger projects.
- GodotEnv may be evaluated when engine or addon version management has become an observed maintenance problem.

Package-specific decisions for state machines, AI, physical units, testing, and narrative are governed by later ADRs.

### Consequences

- Good, because the project uses Godot and .NET capabilities that already have broad maintenance and tooling support.
- Good, because simulation-defining rules remain under project control.
- Good, because every dependency has a concrete consumer and an explicit architectural boundary.
- Good, because package resolution and versions remain reproducible.
- Good, because the project avoids coupling itself prematurely to addons or frameworks that may not match its design.
- Bad, because developers must perform an admission analysis instead of installing the first plausible package.
- Bad, because some narrow algorithms will be project-owned even when larger frameworks contain an implementation.
- Bad, because deferred dependencies may require later integration work when their triggering use cases arrive.

### Confirmation

A change is in scope when it adds a package, addon, GDExtension, source dependency, runtime framework, or framework-like abstraction, or expands one into a new architectural layer.

Conformance is confirmed by review of the stated consumer and admission evidence, central NuGet version ownership, committed lock-file changes, immutable addon references, license compatibility, and successful canonical verification. A dependency entering `AlterCourse.Core` must also preserve ADR 0001's project-reference and headless-test boundary.

## Pros and Cons of the Options

### Prefer native and standard-library capabilities, then admit focused dependencies only for demonstrated needs

- Good, because it minimizes maintenance and integration surfaces while still allowing mature reuse.
- Good, because it respects the different responsibilities of Core and Godot.
- Good, because it makes removal boundaries and dependency costs visible.
- Neutral, because it does not guarantee the smallest possible line count.
- Bad, because each nontrivial dependency requires explicit evaluation.

### Select a comprehensive Godot game framework and use its conventions across the project

- Good, because common game features could arrive quickly when the framework matches.
- Good, because one framework can offer integrated editor tooling.
- Bad, because ST:AC's persistent political, engineering, and tactical simulation does not map cleanly to a conventional game framework.
- Bad, because the framework would become an additional architecture above Godot.
- Bad, because C# support and engine-version compatibility may be uneven across Godot addons.

### Prefer third-party packages for most reusable algorithms and infrastructure

- Good, because mature generic implementations can reduce local code.
- Bad, because many small dependencies increase update and compatibility work.
- Bad, because package abstractions can leak into domain models and saves.
- Bad, because package availability is not evidence that it fits the game's semantics.

### Implement nearly all capabilities in project-owned code

- Good, because the project controls every interface and behavior.
- Good, because external update risk is low.
- Bad, because it recreates mature infrastructure such as structured logging, schema validation, and testing tools.
- Bad, because local code becomes a permanent maintenance obligation.
- Bad, because reliability can suffer when generic edge cases are reimplemented unnecessarily.

## More Information

Godot's C# runtime uses ordinary .NET libraries, but engine-native types and managed .NET types have different performance and ownership implications. In particular, ordinary .NET collections are preferred within pure managed code unless data must cross a Godot API boundary. This supports the one-way adapter architecture already established by ADR 0001.

This ADR intentionally records selection policy rather than a permanent catalog of package versions. Package versions, maintenance status, and compatibility are implementation-time facts and remain governed by central package management, lock files, and dependency review.
