---
schema_version: '1.1'
id: 'reference-3ygl9y-architecture-testing'
title: 'Architecture and Property Testing Dependency Admission'
description: 'Records test-only ArchUnitNET and CsCheck admission for durable dependency rules and generated simulation invariants.'
doc_type: 'reference'
status: 'active'
created: '2026-09-27'
updated: '2026-09-27'
reviewed: '2026-09-27'
owner: 'project-maintainers'
consumer: 'agent'
tags:
  - 'architecture'
  - 'dependencies'
  - 'testing'
aliases: []
related:
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0009-use-layered-testing-and-architecture-conformance.md'
source:
  - 'https://github.com/TNG/ArchUnitNET'
  - 'https://www.nuget.org/packages/TngTech.ArchUnitNET/0.13.4'
  - 'https://github.com/AnthonyLloyd/CsCheck'
  - 'https://www.nuget.org/packages/CsCheck/4.9.1'
confidence: 'high'
visibility: 'public'
license: null
---

# Architecture and property testing dependency admission

This record admits ArchUnitNET 0.13.4 and CsCheck 4.9.1 solely in `AlterCourse.Core.Tests` under ADRs 0003 and 0009. Central package versions and the committed test lockfile own reproducible resolution. Official upstream guides, current NuGet metadata, and the restored packages' manifests were reviewed on September 27, 2026.

## Consumers and alternatives

`DependencyArchitectureTests` uses ArchUnitNET's dependency model to prevent Core dependencies on Godot, presentation, test, tooling, and concrete Serilog implementations. It also prevents ship, simulation, and AI domain namespaces from depending on persistence adapters. Logging abstractions remain outside pure ship, simulation, AI, quantity and tactical namespaces; Gameplay orchestration may consume them. Project references cannot enforce relationships between namespaces inside one assembly. BCL reflection can inspect public signatures but requires custom IL analysis to cover executable dependencies. The selected library owns that generic analysis; the project owns the small rule set and isolated positive and negative probes, including actual logging package dependencies across assemblies.

Existing `ArchitectureBoundaryTests`, `SubstrateConformanceTests`, and `IlTypeReferenceWalker` retain their specialized actor-safe projection, member exemption, and migration-call reachability checks. Those checks encode narrower semantics than a namespace dependency rule and are not replaced by ArchUnitNET.

`GeneratedSimulationInvariantTests` uses CsCheck for bounded scheduler ordering, cancellation and restored continuation against an independent list model; balanced power conservation, demand bounds and installation permutation; and shield damage conservation, condition bounds and monotonic protection. Ordinary named xUnit examples remain. A custom BCL random loop would need to reimplement shrinking and reproducible counterexample diagnostics. CsCheck supplies those facilities without entering the simulation.

## Compatibility, maintenance, and licensing

CsCheck 4.9.1 supplies a `net8.0` asset and no package dependencies. ArchUnitNET 0.13.4 supplies a managed `netstandard2.0` asset compatible with the test project's .NET 8 target. Both packages publish Apache 2.0 licenses. Preserve required license and notice material if redistributing their binaries or source; this admission does not put either package in game exports.

The current releases and linked upstream source commits provide maintenance evidence for these bounded consumers, rather than a guarantee of future availability. CsCheck's package records source commit `50503557f23266f140021f65632612ceb4ac248f`; ArchUnitNET records `1ab5943d761d48f86b42f45ef047130dc9aff1c6`.

ArchUnitNET directly requires CycleDetection 2.0.0, JetBrains.Annotations 2026.2.0, Mono.Cecil 0.11.6, Newtonsoft.Json 13.0.4 and System.ValueTuple 4.6.2. The committed lockfile records the complete graph, including framework support dependencies. These are test infrastructure dependencies, not public Core APIs, content types, or save models.

The `TngTech.ArchUnitNET.xUnit` 0.13.4 adapter was evaluated and rejected for this consumer. In addition to ArchUnitNET, it requests System.Net.Http 4.3.4, System.Text.RegularExpressions 4.3.1 and xunit.assert 2.4.1 (resolved to the existing 2.9.3 pin). The trial restore pulled legacy `runtime.native.System` 4.3.0, `runtime.native.System.Net.Http` 4.3.0 and OpenSSL platform packages at 4.3.2. Direct use of the core library's evaluated results with ordinary xUnit assertions supplies the required enforcement without that adapter-only graph. This is a dependency-cost choice, not a replacement architecture-test implementation.

## Determinism, failure, and replacement boundary

Each generated property runs 256 iterations with an explicit seed, one thread, and no wall-clock cutoff. Generator bounds keep arrays within installed-system limits and scheduler cases small. CsCheck reports a replay seed and shrinks the failing input. Its test RNG is not a gameplay random stream. Architecture loading uses the built assemblies once, and the same rule factory accepts a valid fixture and rejects an isolated intentional violation for each dependency boundary.

A missing package, restore failure, invalid architecture or failed invariant fails the ordinary .NET test gate. There is no permissive fallback. Neither library starts Godot or calls hosted services during tests. The canonical `scripts/verify.sh` invokes the Core test project; no separate optional command hides these tests.

Package API coupling is confined to the two test classes and their isolated architecture fixtures. Removal requires replacing dependency analysis or generated-case/shrinking facilities while preserving the rules, invariants and diagnostic contract, then deleting the central pins, test references and lock entries. No game content, runtime, save schema or export migration is required.

GDScript GdUnit4 fixtures remain a valid engine-aware layer under ADR 0009. This work identifies no new C# subject that requires the engine runtime, so it does not admit GdUnit4Net.
