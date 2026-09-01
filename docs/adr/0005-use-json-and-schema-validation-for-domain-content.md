---
schema_version: '1.1'
id: 'adr-0005-star-trek-alter-course-use-json-and-schema-validation-for-domain-content'
title: 'ADR 0005: Use JSON and Schema Validation for Domain Content'
description: 'Defines the canonical default format, validation pipeline, identity rules, and Godot boundary for data-driven game definitions.'
doc_type: 'adr'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'data'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0009-use-layered-testing-and-architecture-conformance.md'
supersedes: []
superseded_by: null
source:
  - 'https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/overview'
  - 'https://json-schema.org/'
  - 'https://docs.json-everything.net/schema/basics/'
  - 'https://www.nuget.org/packages/JsonSchema.Net/'
  - 'https://github.com/aaubry/YamlDotNet'
  - 'https://docs.godotengine.org/en/4.6/classes/class_resource.html'
confidence: 'high'
visibility: 'public'
license: 'MIT'
project:
  decision_makers:
    - 'project owner'
  consulted: []
  informed: []
  amends: []
  amended_by:
    - 'docs/adr/0012-keep-branching-narrative-subordinate-to-simulation.md'
---

# Use JSON and schema validation for domain content

## Context and Problem Statement

Star Trek: Alter Course will contain many definitions that are data rather than executable behavior: ships, hull classes, systems, weapons, factions, doctrines, commodities, encounters, locations, hazards, mission templates, and related content. These definitions must be readable by humans and coding agents, reviewable in Git, validatable in CI, loadable in headless Core tests, and independent from Godot scenes.

Godot `Resource` objects provide strong editor integration, while YAML is pleasant for some hand-authored configuration. JSON has stricter syntax, first-class .NET support, and a mature machine-readable schema standard. A format choice alone is insufficient, however. Syntactically valid content can still contain duplicate identifiers, broken references, impossible values, or relationships that violate game invariants.

This decision governs authored, generated, or imported definitions that describe reusable simulation content. It applies when a definition is intended to be loaded by `AlterCourse.Core`, referenced by stable identity from simulation state, or validated as part of the game's ordinary domain-content set.

JSON is the canonical default for ordinary domain definitions. A specialized canonical authoring language or format may be authorized only by an explicit ADR amendment when the content domain materially benefits from semantics that JSON does not provide well. Such an exception does not waive stable identity, deterministic build and validation, versioning, typed integration boundaries, headless compatibility where applicable, or save-compatibility requirements. ADR 0012 establishes the first such exception for a future specialized narrative source language.

This decision does not govern save games, runtime world state, visual assets, Godot scene files, Godot import metadata, transient caches, tool configuration that has an independently justified native format, or a specialized content language explicitly authorized by an amending ADR. Save games are governed by ADR 0006.

How should the project represent and validate data-driven simulation content without making Godot or permissive parsing the source of truth?

## Decision Drivers

- Core content must load and validate without starting Godot.
- Text definitions must produce useful Git diffs and merge conflicts.
- Coding agents need a format with deterministic parsers and explicit schemas.
- Invalid content must fail before it can produce subtle runtime behavior.
- Stable identities and references must survive file moves and display-name changes.
- Content validation must include cross-record and domain-specific rules that a structural schema cannot express.
- The format must support versioning and controlled evolution.
- Presentation metadata must remain separable from simulation semantics.
- The project should avoid adding a custom authoring language or database without a demonstrated domain need and explicit architectural approval.
- Future mod support must remain possible without being designed prematurely.

## Considered Options

- Canonical JSON with System.Text.Json, JSON Schema, and semantic validation as the default domain-content format, with specialized formats permitted only by explicit ADR amendment.
- Godot `Resource` files as the canonical content representation.
- YAML as the canonical content representation.
- C# source code as the canonical definition format.
- A database or custom content service.

## Decision Outcome

Chosen option: "Canonical JSON with System.Text.Json, JSON Schema, and semantic validation as the default domain-content format, with specialized formats permitted only by explicit ADR amendment", because it gives Core a strict, portable, toolable representation while allowing a narrowly justified domain-specific language to remain canonical when converting it to JSON would discard the reason for adopting that language.

This decision governs ordinary reusable definitions consumed by Core. It does not require all definitions to exist before gameplay needs them, and it does not prescribe one monolithic schema or directory layout.

### Canonical format and parser

Canonical ordinary domain content is stored as UTF-8 JSON and deserialized with `System.Text.Json`.

The loader will use explicit options rather than ambient defaults. At minimum, production content loading must:

- reject malformed JSON;
- reject or report unknown members unless an explicitly versioned extension point permits them;
- treat property names according to one documented casing convention;
- avoid runtime type-name metadata and unsafe arbitrary polymorphism;
- produce diagnostics that identify the file, record, and failing path;
- apply bounds before allocating collections or strings from untrusted external content;
- avoid reflection-based behavior whose compatibility cannot be tested.

Source generation for `System.Text.Json` may be introduced when it materially improves startup behavior, trimming compatibility, or diagnostics. It is not required before measurement.

### Specialized canonical formats

A specialized canonical format is an architectural exception, not a parallel convenience representation.

An ADR authorizing one must identify:

- the concrete content domain and why JSON is materially insufficient for its authoring or runtime semantics;
- the canonical source representation and build or compilation pipeline;
- stable machine identifiers for definitions referenced by Core or persistence;
- deterministic parsing, compilation, and validation behavior;
- explicit versioning and compatibility rules;
- typed read and consequence boundaries into Core rather than reflection or unrestricted mutation;
- headless validation or evaluation where the content participates in non-presentation behavior;
- save and migration treatment for any persisted references or runtime-local state;
- how the format remains subordinate to the authoritative simulation model.

The exception applies only to the content domain named by the amending ADR. It does not authorize the same format for unrelated definitions or create a general multi-format content pipeline.

ADR 0012 authorizes a future specialized narrative source language when its package-admission trigger is reached. Ships, factions, weapons, world topology, and other ordinary Core definitions remain JSON unless another explicit amendment establishes a separate exception.

### Schema validation

`JsonSchema.Net` is the selected JSON Schema implementation when the first production content schemas are introduced. Its role is structural validation, including:

- required properties;
- primitive and object shapes;
- enumerated values;
- numeric and collection bounds;
- conditional shape rules;
- reusable schema definitions;
- format and pattern constraints where appropriate.

Schemas are versioned repository artifacts and are validated by the canonical quality gate. Content must pass the schema version declared or selected for its content family.

JSON Schema is not treated as the complete game-rules validator. It cannot reliably express every relationship among files, every reference constraint, or every simulation invariant.

### Semantic validation pipeline

Content admission proceeds through ordered stages:

1. Discover only files within declared content roots and supported content families.
2. Parse JSON strictly.
3. Validate the raw document against the applicable JSON Schema.
4. Deserialize into explicit input models.
5. Normalize only values whose normalization is documented and semantics-preserving.
6. Register stable identifiers and reject duplicates.
7. Resolve references and reject missing, ambiguous, or type-incompatible targets.
8. Apply cross-record and domain-specific invariants.
9. Construct immutable or controlled runtime definitions.
10. Produce a deterministic content catalog or content-set identity for tests and saves.

A specialized format authorized by an amending ADR follows an equivalent domain-appropriate parse or compile and validation pipeline before its definitions or runtime representation are admitted. The exception does not bypass deterministic identity, reference, or semantic validation.

Validation errors are accumulated when doing so is safe, so an author can correct a useful set of independent problems in one pass. Loading must fail closed when any error prevents trustworthy construction.

Warnings are reserved for conditions that are genuinely acceptable at runtime. A warning must not be used to admit content whose meaning is unknown or contradictory.

### Stable identity and references

Every independently referenceable definition has a stable machine identifier separate from its display name and file path.

Identifiers:

- use one documented, case-sensitive canonical form;
- are unique within a declared namespace or globally when cross-family references require it;
- do not change merely because display text, folder placement, or visual assets change;
- are used by saves, missions, world state, and cross-record references;
- are validated before runtime objects are exposed.

Display names and localized text are not identifiers. File names may mirror identifiers for discoverability but are not authoritative unless a later convention explicitly makes them so.

References are typed at validation boundaries. A field that requires a weapon definition cannot silently resolve to a ship or arbitrary generic record.

### Content versions and migrations

Each content family or content-set manifest has an explicit version when its shape or semantics require compatibility handling.

Content migrations differ from save migrations:

- a content migration updates repository content or an external content package to the current definition shape;
- a save migration updates historical runtime state while preserving the meaning of an existing playthrough.

The project may choose to migrate only repository-owned content forward rather than support loading every historical content schema indefinitely. That policy does not remove the requirement to preserve stable definition identifiers referenced by supported saves.

A semantic rules change that alters the meaning of an unchanged field must be documented and tested even when the JSON shape remains valid.

Specialized formats follow the compatibility and migration policy established by their amending ADR while preserving stable identifiers referenced by supported saves.

### Godot resources and presentation data

Godot `Resource` files remain appropriate for engine-facing data such as:

- sprites and texture collections;
- audio references and bus-related presentation settings;
- fonts, themes, materials, shaders, and effects;
- editor-authored visual layouts;
- presentation adapters that map a Core identifier to Godot assets.

Godot resources are not the canonical source for simulation definitions or persistent world state. A resource may refer to a Core definition by stable identifier; a Core definition must not refer to a Godot resource object.

Where one conceptual item has both simulation and presentation data, the simulation definition remains valid and loadable without the presentation record. Missing presentation data receives a controlled fallback or an engine-layer validation error rather than corrupting Core semantics.

### YAML and other text formats

YAML is not the canonical simulation-content format.

YamlDotNet may be admitted under ADR 0003 for a bounded human-edited configuration where comments, anchors, or operational ergonomics provide concrete value and where the configuration is not part of the stable domain-content contract. Examples could include a development tool's provider configuration or a build-time manifest.

A YAML file that defines ships, factions, weapons, world topology, or other ordinary Core content would require an amendment to this ADR. The project will not support parallel JSON and YAML representations for the same content family because dual representations create inconsistent validation and tooling.

CSV may be used as an import or translation-authoring format where tabular data is genuinely dominant. Imported data must be converted into or validated against the same canonical domain model before use.

A specialized source language authorized by an amending ADR is distinct from an alternate serialization of the same ordinary content family: it is canonical only for the explicitly named domain and exists because its semantics justify the exception.

### Generated and imported content

Generated content is not trusted merely because a repository tool created it. It passes the same parse, schema, reference, and semantic validation as hand-authored content, or the equivalent validated pipeline established for an authorized specialized format.

A generator should produce deterministic output from declared inputs where practical. Generated files must have a clear ownership policy: either they are committed and reviewed, or they are reproducibly built and excluded. The project will not maintain partially hand-edited generated content.

External Star Trek reference material cannot be copied into content merely because a parser can ingest it. Licensing and project legal boundaries remain independent admission requirements.

### Modding boundary

This ADR keeps later mod support technically possible by using portable definitions and stable identifiers. It does not promise a public mod API, sandbox arbitrary code, establish load order, define conflict resolution, or guarantee compatibility for external content.

When modding becomes a concrete feature, its trust, packaging, override, dependency, and compatibility model requires a separate decision.

### Consequences

- Good, because ordinary domain content is readable, diffable, and independent from Godot.
- Good, because structural mistakes and domain contradictions fail before gameplay.
- Good, because stable identifiers support saves and durable world relationships.
- Good, because agents can generate ordinary content against explicit machine-readable constraints.
- Good, because specialized authoring languages remain possible only where their domain semantics justify an explicit exception.
- Good, because Godot resources remain available where editor and asset integration matter.
- Bad, because strict JSON is less pleasant for comments and some repetitive hand authoring.
- Bad, because schema and semantic validators require maintenance alongside content models.
- Bad, because specialized-format exceptions require their own deterministic validation and compatibility machinery.
- Bad, because authors must understand the distinction between simulation, specialized authored flow, and presentation records.
- Bad, because content changes can require explicit migration work instead of permissive fallback.

### Confirmation

A file is in scope when it defines ordinary reusable data consumed by Core or referenced by stable identity from simulation state, unless an active amending ADR explicitly assigns that content domain to a specialized canonical format.

Conformance is confirmed by:

- successful strict parsing and schema validation for ordinary JSON content;
- deterministic parse or compilation and validation for any explicitly authorized specialized format;
- deterministic identifier registration;
- complete typed reference resolution;
- semantic invariant tests;
- failure tests for unknown members, duplicates, missing references, invalid bounds, and incompatible versions;
- headless loading or evaluation where required by the content's role;
- the absence of Godot dependencies from canonical simulation definition models;
- round-trip or snapshot tests only where they verify a stable contract rather than reproduce implementation formatting.

## Pros and Cons of the Options

### Canonical JSON with System.Text.Json, JSON Schema, and semantic validation as the default domain-content format, with specialized formats permitted only by explicit ADR amendment

- Good, because the parser is part of modern .NET and works naturally in Core.
- Good, because JSON Schema is machine-readable across editors, CI, and agent tooling.
- Good, because strict syntax reduces parser ambiguity.
- Good, because a specialized DSL can remain canonical when its semantics are the reason for adoption.
- Bad, because comments are not part of standard JSON.
- Bad, because domain validation still requires project-owned code.
- Bad, because exceptional formats need separate validation and compatibility contracts.

### Godot `Resource` files as the canonical content representation

- Good, because Godot's inspector and editor provide direct authoring support.
- Good, because engine asset references are convenient.
- Bad, because Core loading and validation would become Godot-dependent.
- Bad, because simulation definitions would be coupled to engine serialization and scripts.
- Bad, because non-Godot tools and agents would have a less portable contract.

### YAML as the canonical content representation

- Good, because YAML supports comments and concise human authoring.
- Good, because YamlDotNet is mature.
- Bad, because YAML has a broader and more surprising syntax surface.
- Bad, because schema tooling and strictness are less uniform for the intended workflow.
- Bad, because accepting both JSON and YAML for the same content family would duplicate the content pipeline.

### C# source code as the canonical definition format

- Good, because the compiler provides strong typing.
- Good, because computed definitions are easy to express.
- Bad, because content changes require compilation and executable-code review.
- Bad, because data agents and external tools cannot safely treat definitions as ordinary content.
- Bad, because modding and independent validation become harder.

### A database or custom content service

- Good, because queries and centralized editing could support very large datasets.
- Bad, because the game does not have a demonstrated operational need for a database.
- Bad, because schema migrations, deployment, backup, and tooling would add infrastructure.
- Bad, because Git review and offline distribution would become less direct.

## More Information

JSON Schema validates document structure, not all gameplay meaning. The semantic-validation stage is therefore part of the chosen default rather than an optional enhancement.

The canonical format may be presented through custom Godot editor tools later. Such tooling should edit or generate the canonical contract rather than silently establish a second source of truth. An explicitly authorized specialized format is already a canonical contract for its named domain and should be edited or compiled through tooling appropriate to that format rather than translated into a hidden competing authority.
