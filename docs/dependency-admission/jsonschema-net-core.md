---
schema_version: '1.1'
id: 'reference-k7zc82-jsonschema-net-core'
title: 'JsonSchema.Net Core Dependency Admission'
description: 'Records ADR 0003 evidence for JsonSchema.Net as AlterCourse.Core structural ship-content validation.'
doc_type: 'reference'
status: 'active'
created: '2026-09-02'
updated: '2026-09-02'
reviewed: '2026-09-02'
owner: 'project-maintainers'
consumer: 'agent'
tags:
  - 'dependencies'
  - 'validation'
  - 'content'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/dependency-admission/assetctl.md'
source:
  - 'https://www.nuget.org/packages/JsonSchema.Net/9.4.0'
  - 'https://www.nuget.org/packages/JsonSchema.Net/9.4.0/License'
  - 'https://github.com/json-everything/json-everything'
confidence: 'high'
visibility: 'public'
license: null
---

# JsonSchema.Net Core dependency admission

This record admits `JsonSchema.Net` 9.4.0 as a direct `AlterCourse.Core` consumer for the first production game-content schema. It is a material role expansion from AssetCtl, so it is assessed independently under ADR 0003. Evidence was refreshed from the official NuGet package, package license, and upstream repository on 2026-09-02.

## Current consumer and alternatives

`ShipDefinitionCatalogLoader` uses `Json.Schema.JsonSchema` to construct the canonical V1 ship schema and evaluate each raw JSON document before typed mapping. The loader then keeps project-owned responsibilities: strict UTF-8 JSON parsing, duplicate-member detection, semantic validation, stable-ID registration, and diagnostics. `ShipDefinition`, runtime ship state, snapshots, and player projections do not expose `JsonSchema.Net` types.

`System.Text.Json` remains the parser and typed-mapping implementation, but it does not implement JSON Schema drafts, keyword evaluation, instance locations, or schema locations. Reimplementing that generic standards surface would add a larger and less reliable project-owned validator at an untrusted-content boundary. The BCL-only alternative is therefore insufficient; the focused package is the smallest concrete fit for ADR 0005's structural-validation requirement.

## Compatibility, maintenance, and distribution

The official 9.4.0 NuGet package declares included `net8.0`, `net9.0`, `net10.0`, and `netstandard2.0` assets, so its included `net8.0` asset fits both the Core target and the Godot-managed runtime. It is managed code; the resolved dependency graph has no native runtime package, so it adds no platform-specific binary or export-plugin obligation.

NuGet records the 9.4.0 release as updated on 2026-07-26 and links its upstream `json-everything` source repository. That is sufficient maintenance evidence for this focused, isolated consumer; update review remains part of ordinary central dependency maintenance rather than a promise that upstream will remain available.

The upstream source is MIT-licensed. The package's published license page also describes an Open Source Maintenance Fee Agreement for binary releases used in revenue-generating activity at or above its stated threshold, while preserving the OSI-license rights for source and self-compiled binaries. This non-commercial project may consume the package under its present scope. Before any revenue-generating distribution, maintainers must review the then-current package-license terms and choose either compliant package distribution or a reproducible source-build path; do not assume this record grants that future distribution decision.

## Central ownership and resolved graph

`Directory.Packages.props` centrally pins `JsonSchema.Net` to 9.4.0. `AlterCourse.Core.csproj` contains the direct package reference with no local version. The committed Core lock file resolves:

- `JsonSchema.Net` 9.4.0 as direct;
- `JsonPointer.Net` 7.0.2 as the package's direct transitive dependency;
- `Humanizer.Core` 2.14.1 and `Json.More.Net` 3.0.1 through `JsonPointer.Net`.

The Core-test and Godot lock files record the same dependency closure where their project graphs require it. Locked restore is the reproducibility boundary; package updates require a central-pin and lock-file review rather than a local floating version.

## Determinism, failure, and removal

Schema construction and evaluation run wholly in headless Core tests. The loader supplies a fixed schema base URI and invariant culture, and neither the package nor the surrounding validation path becomes a source of simulation time, randomness, Godot state, or rendering data. The validated immutable definition crosses into simulation only after structural and project-owned semantic checks complete.

Malformed schemas, malformed JSON, duplicate members, structural violations, and semantic violations fail closed with source-aware diagnostics before a definition can enter the catalog. If the package cannot restore, load, or evaluate, content loading fails rather than substituting a permissive parser; locked restore and the canonical verification gate make that failure visible before a release candidate is accepted.

All direct Core package API use is confined to `AlterCourse.Core/Content/ShipDefinitionCatalogLoader.cs`. Replacing or removing the dependency means replacing schema construction and evaluation behind that loader while retaining the project-owned content input, semantic checks, catalog, runtime definitions, and save contract. No save JSON or authored ship definition serializes package types, so the replacement does not itself require a save-format migration.

## Review result

Admitted for this one Core structural-validation consumer. The dependency is focused, managed, compatible with the repository's .NET 8 runtime, deterministic under the established loader path, and bounded behind a concrete content adapter. This admission does not authorize `JsonSchema.Net` use in unrelated Core subsystems or create a generic validation framework.
