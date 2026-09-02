---
schema_version: '1.1'
id: 'spec-pju59d-asset-pipeline-tool'
title: 'Asset Pipeline Tool Specification'
description: 'Implementation specification for a configuration-driven AI-assisted 2D asset generation, validation, provenance, and approval tool.'
doc_type: 'spec'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'agent'
tags:
  - 'development'
  - 'infrastructure'
  - 'validation'
aliases:
  - 'asset-pipeline'
  - 'assetctl-spec'
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0002-use-one-canonical-quality-gate.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0008-use-structured-observability-with-serilog.md'
  - 'docs/adr/0009-use-layered-testing-and-architecture-conformance.md'
  - 'docs/development-quality.md'
  - 'LICENSE.md'
  - 'LEGAL.md'
confidence: 'high'
visibility: 'public'
license: 'MIT'
---

# Asset pipeline tool specification

Date: 2026-09-01

## Executive summary

Star Trek: Alter Course needs a reliable way for coding agents to obtain 2D visual assets without stopping implementation to ask the project owner for every icon, marker, texture, background, or temporary illustration. The repository will provide a standalone command-line tool named `assetctl` that can find an existing asset or create, validate, select, catalog, and publish a new placeholder or production candidate.

`assetctl` will be implemented as an ordinary C#/.NET 10 console application under `tools/`. It will be part of `AlterCourse.sln`, but it will reference neither `AlterCourse.Core` nor `AlterCourse.Godot`. The tool will not require the Godot editor to perform normal asset generation or validation. The Godot project consumes only the selected asset files through ordinary `res://` paths.

Provider instances, model identifiers, endpoints, credentials references, capabilities, prices, routing priorities, quality tiers, style profiles, retry policy, and spending policy will be defined in versioned YAML configuration. The orchestration code will route on declared capabilities rather than vendor names. Adding another account, endpoint, or model that uses an existing adapter must require configuration only. Supporting a genuinely different API protocol may require one thin C# adapter, but must not require changes to catalog, routing, lifecycle, validation, or publishing logic.

The initial external generation adapters will support Recraft, OpenAI, and xAI. Recraft is the preferred starting point for vector icons and map symbols, OpenAI is the preferred starting point for high-quality raster generation and reference-driven editing, and xAI is an alternate raster provider. A zero-cost local placeholder adapter will always remain available so a coding agent can continue when credentials, budget, network access, or external services are unavailable.

Every selected asset will have a tracked YAML manifest containing its semantic request, lifecycle, output path, provider and model provenance, final prompt, configuration hashes, integrity hash, validation results, estimated cost, and rights classification. Generated candidates and run receipts will remain in a repository-local ignored work area unless explicitly retained. There will be no database, daemon, web application, MCP server, or external asset-management service in the first implementation.

Agents may autonomously create and replace `placeholder` assets and may create `candidate` assets. An agent must never promote an asset to `approved` or replace an approved asset without an explicit project-owner instruction. The tool will enforce approved-asset immutability by default, while repository agent instructions will enforce the human authorization boundary that software alone cannot prove.

## Bottom line

Build one configuration-driven `.NET` CLI with four hard boundaries:

1. Game code does not know or call image providers.
2. Provider and model policy lives in YAML, not routing branches in C#.
3. External generation is optional because a local deterministic placeholder fallback always exists.
4. No generated result becomes an approved permanent asset without an explicit human-controlled lifecycle transition.

The smallest complete version must support raster and SVG placeholders, Recraft, OpenAI, xAI, independent mechanical validation, optional AI semantic review, deterministic routing, cost controls, tracked manifests, offline tests, and canonical repository verification.

## Normative language

The words **MUST**, **MUST NOT**, **SHOULD**, **SHOULD NOT**, and **MAY** express normative requirements.

- **MUST** and **MUST NOT** are required for conformance.
- **SHOULD** and **SHOULD NOT** are defaults that require a documented reason to override.
- **MAY** identifies an optional implementation choice.

Examples are illustrative unless a section explicitly labels them normative.

## Context

ST:AC is a map-centric 2D starship command and strategy game. Development will require many small visual assets before the final visual language and production art are settled. Typical needs include:

- tactical-map markers;
- ship and faction symbols;
- engineering-system icons;
- weapon, shield, sensor, and navigation indicators;
- planet, starbase, anomaly, and resource markers;
- UI glyphs and status badges;
- temporary ship or location illustrations;
- space backgrounds, nebulae, planets, and other raster art;
- reference-driven variants of an established visual language.

Without a standard mechanism, implementation agents will either block on missing art, create inconsistent files in arbitrary locations, copy inappropriate third-party material, or embed provider-specific scripts throughout the repository. This specification creates one controlled path from semantic need to usable Godot asset.

The repository already separates pure simulation from Godot through ADR 0001 and uses one canonical quality gate through ADR 0002. Dependency admission follows ADR 0003; YAML asset-tool configuration remains bounded development and presentation metadata under ADR 0005; operational logging follows ADR 0008; and automated testing follows ADR 0009. `assetctl` must fit those decisions rather than create a parallel architecture or toolchain.

## Goals

The implementation MUST achieve all of the following:

- Let a coding agent obtain a usable placeholder without asking the owner for routine art creation.
- Search for and reuse a suitable existing asset before generating a duplicate.
- Support tracked semantic asset specifications rather than requiring agents to hand-author vendor prompts.
- Keep providers, models, endpoints, route priorities, prices, and quality policy configurable.
- Permit new instances and new models for an existing protocol without recompilation.
- Isolate new API protocols behind small adapters without modifying orchestration logic.
- Support at least SVG and PNG outputs in the first complete release.
- Support vector generation, raster generation, image editing, reference images, and semantic review as independently declared capabilities.
- Validate generated bytes before they enter the Godot asset tree.
- Evaluate semantic fitness through an independently configured vision reviewer when the quality tier requires it.
- Generate multiple candidates, reject invalid candidates, score valid candidates, and select deterministically.
- Record enough provenance to identify how an asset was produced and whether it has changed.
- Protect approved assets from unattended replacement.
- Continue development through a local zero-cost placeholder fallback when external generation is unavailable.
- Enforce bounded requests, retries, candidate counts, and configured spending ceilings.
- Remain usable from Codex, Claude Code, CI, shell scripts, and a human terminal.
- Produce stable JSON output suitable for coding agents.
- Participate in `AlterCourse.sln` and `./scripts/verify.sh` without making canonical verification depend on network access or paid APIs.

## Non-goals

The first implementation MUST NOT attempt to provide:

- a Godot editor plugin or graphical asset browser;
- a daemon, web service, cloud database, or shared asset server;
- an MCP server;
- dynamic loading of arbitrary third-party assemblies;
- a universal REST API described through an unrestricted YAML request language;
- local diffusion, PyTorch, or other heavyweight machine-learning inference;
- automatic web searching or downloading of third-party artwork;
- browser automation of ChatGPT, Grok, Recraft Studio, or other consumer applications;
- audio, music, voice, video, animation, 3D model, shader, or font generation;
- a digital-asset-management system for every project file;
- automatic legal clearance or a claim that AI review proves originality;
- automatic promotion to `approved`;
- automatic Git commits, pushes, pull requests, or merges.

The design should leave room for additional media types, but no abstraction may be added solely for a hypothetical future asset type.

## Governing architectural decisions

### Standalone tool boundary

The tool MUST be an ordinary .NET console application:

```text
AlterCourse.Core
    Pure game simulation and domain behavior

AlterCourse.Godot
    Godot nodes, scenes, resources, UI, input, and adapters
    References AlterCourse.Core

AlterCourse.AssetCtl
    Repository development infrastructure
    References neither game project
```

The following project references are prohibited:

```text
AlterCourse.AssetCtl -> AlterCourse.Core
AlterCourse.AssetCtl -> AlterCourse.Godot
AlterCourse.Core -> AlterCourse.AssetCtl
AlterCourse.Godot -> AlterCourse.AssetCtl
```

The projects communicate through files and versioned contracts. A request to inspect a game definition is not sufficient reason to reference the simulation assembly; the required information should be expressed in an asset specification instead.

### One repository quality path

`AlterCourse.AssetCtl` and `AlterCourse.AssetCtl.Tests` MUST be added to `AlterCourse.sln`. The repository's central compiler, analyzer, formatter, package, and warning settings apply without local weakening.

`./scripts/verify.sh` MUST remain the authoritative validation command. It MUST build the tool, run its tests, validate tracked asset configuration and manifests offline, and remain read-only for tracked files. It MUST NOT call a paid provider, require provider credentials, perform semantic review over the network, or generate assets.

### C# rather than Python or GDScript

The tool and its ordinary .NET test project MUST use C# and target `net10.0` using the repository-pinned .NET 10 SDK. The Godot-facing game projects remain free to target `net8.0` where required for Godot compatibility; that runtime constraint does not apply to this standalone development tool because it references neither game project nor Godot. The tool MUST NOT introduce Python, Node application dependencies, or Godot as runtime dependencies for the tool itself.

Existing Node-based Markdown and structured-text verification remains repository infrastructure and is not part of the asset tool runtime.

### Asset configuration versus game domain content

The YAML configuration, style profiles, and asset manifests defined by this specification are development and presentation metadata, not canonical `AlterCourse.Core` domain content. They MUST NOT become an alternate source of authoritative simulation definitions or game rules. Any ships, factions, weapons, world topology, or other reusable simulation definitions consumed by Core remain subject to ADR 0005 and its canonical JSON and validation requirements unless a separate active ADR explicitly authorizes an exception.

### Structured observability

Operational logging MUST conform to ADR 0008. The `assetctl` composition root configures Serilog and connects it to `Microsoft.Extensions.Logging`; application code that needs operational logging receives `ILogger<T>` rather than locating a global logger or service locator. Run receipts remain provenance artifacts with their own contract and MUST NOT become a substitute logging backend or be reconstructed from logs.

### Testing architecture

`AlterCourse.AssetCtl.Tests` MUST use xUnit as an ordinary .NET test project under ADR 0009. Tests exercise pure tool behavior without Godot or hosted provider dependencies. CsCheck MAY be introduced only when a meaningful generated invariant exists, and ArchUnitNET MAY be used only when a durable architecture rule cannot be enforced more simply through the project graph, analyzers, or focused tests. Godot-specific asset import behavior remains in the existing Godot-aware test layer.

## Terminology

| Term | Definition |
| --- | --- |
| Asset | A selected visual file intended for use by the Godot project. |
| Asset ID | Stable semantic identifier independent of path, provider, model, and lifecycle. |
| Manifest | Tracked YAML record describing one selected asset and its provenance. |
| Asset specification | Semantic portion of a manifest that states purpose, visual intent, output contract, and constraints. |
| Candidate | One provider or local-adapter result considered during a generation run. |
| Selected asset | Candidate chosen after required validation and scoring. |
| Provider instance | Configured API account or endpoint, such as `recraft-primary`. |
| Model profile | Configured model under a provider instance, including capabilities and economics. |
| Adapter | C# implementation of one API protocol or local generation mechanism. |
| Route | Ordered YAML policy that matches an asset request to eligible provider/model profiles. |
| Generator | Adapter capability that creates or edits visual assets. |
| Reviewer | Adapter capability that evaluates an asset and returns structured semantic scores. |
| Quality tier | Named policy controlling candidates, attempts, validation, review, and thresholds. |
| Lifecycle | `placeholder`, `candidate`, `approved`, or `deprecated`. |
| Run receipt | Untracked detailed JSON record of one invocation and all attempts. |
| External spend | Any provider operation that may consume metered API units or create a billable event. |

## Supported asset scope

### Required output formats

The first complete implementation MUST support:

- SVG for scalable UI, engineering, and tactical symbols;
- PNG for raster art and rasterized review previews.

It SHOULD support WebP after the same decoder, integrity, dimension, and alpha checks exist. JPEG MAY be accepted as an input reference but SHOULD NOT be a committed output unless an asset specification explicitly requires an opaque photographic format.

### Required asset kinds

The manifest schema MUST support at least:

- `icon`;
- `map-marker`;
- `ship-sprite`;
- `emblem`;
- `illustration`;
- `background`;
- `texture`;
- `other`.

Kinds drive routing and validation defaults. They do not create separate orchestration implementations.

### Initial capability vocabulary

The core MUST define a closed, versioned capability vocabulary. Initial values are:

```text
raster.generate
vector.generate
image.edit
image.reference-input
image.transparent-output
image.background-remove
image.vectorize
review.semantic
review.reference-comparison
```

Adding a capability changes the application contract and requires code and tests. Adding a provider, model, endpoint, route, or price that uses existing capabilities requires configuration only when an existing adapter supports the protocol.

## Repository layout

The implementation MUST use this initial layout unless an existing repository convention discovered during implementation requires an equivalent adjustment:

```text
AlterCourse.sln

config/
  assets/
    assetctl.yaml
    providers.yaml
    routing.yaml
    quality-tiers.yaml
    schemas/
      asset-manifest.schema.json
      assetctl.schema.json
      providers.schema.json
      routing.schema.json
      quality-tiers.schema.json
      style-profile.schema.json
    styles/
      global.yaml
      tactical-map.yaml
      engineering-icons.yaml
    catalog/
      <semantic folders>/<asset-name>.asset.yaml

src/
  AlterCourse.Godot/
    assets/
      <semantic folders>/<asset-name>.<ext>

tools/
  AlterCourse.AssetCtl/
    AlterCourse.AssetCtl.csproj
    Program.cs
    Cli/
    Configuration/
    Domain/
    Providers/
    Routing/
    Generation/
    Review/
    Validation/
    Publishing/
    Provenance/

  .gitkeep only where Git requires an otherwise empty tracked directory

tests/
  AlterCourse.AssetCtl.Tests/
    AlterCourse.AssetCtl.Tests.csproj
    Fixtures/
    Golden/

.assetctl/
  work/
  runs/
  state/
  logs/
```

`.assetctl/` MUST be ignored by Git. It contains temporary candidates, run receipts, local locks, local budget state, bounded structured logs, and optional previews. It is not a source of repository truth.

Only selected game assets belong under `src/AlterCourse.Godot/assets/`. Manifests remain outside the Godot project under `config/assets/catalog/` so YAML provenance does not clutter or complicate the Godot import tree.

## Source project and package requirements

### Project names

The application project MUST be named `AlterCourse.AssetCtl`. The test project MUST be named `AlterCourse.AssetCtl.Tests`. The executable command SHOULD publish as `assetctl`.

### Target and repository settings

Both projects MUST:

- target `net10.0`;
- inherit nullable reference types, warnings-as-errors, C# language version, deterministic builds, documentation-file generation, analyzers, and package locking from repository settings;
- use central package version management through `Directory.Packages.props`;
- commit updated `packages.lock.json` files;
- avoid project-local warning suppression or analyzer weakening.

### Dependency policy

The implementation SHOULD use the smallest practical dependency set. Every new or materially expanded package MUST satisfy ADR 0003's dependency-admission requirements, including a concrete current consumer, why a higher-ranked native or standard-library choice is insufficient, maintenance state, .NET 10 and Linux compatibility, license and distribution obligations, transitive and native dependencies, expected failure mode, coupling scope, and a plausible removal or replacement boundary. That evidence belongs in the implementation PR and should be proportional to the dependency's impact.

Recommended responsibilities are:

- `System.CommandLine` or an equivalently small, actively maintained CLI parser for command dispatch;
- `YamlDotNet` for safe YAML parsing and serialization;
- `System.Text.Json` for provider DTOs, receipts, and machine output;
- `HttpClient` with explicit typed clients for provider HTTP calls;
- `Microsoft.Extensions.Logging` with Serilog as the structured logging backend required by ADR 0008;
- a centrally pinned, license-compatible image library for raster decode, resize, alpha inspection, normalization, and PNG encoding;
- a centrally pinned, license-compatible SVG parser and renderer for sanitization and review previews.

SkiaSharp and Svg.Skia are acceptable starting candidates because they provide cross-platform .NET raster and vector rendering, but the implementing agent MUST verify current stable versions, .NET 10 and Linux support, transitive native packages, and licenses before adoption. Dependency selection belongs in the implementation PR evidence, not as an unreviewed assumption in this specification.

Vendor SDK packages SHOULD NOT be used initially. Provider REST protocols should be implemented with typed request and response DTOs so the repository does not inherit unrelated SDK dependencies or vendor-specific application structure. A vendor SDK MAY be introduced when a required capability cannot reasonably be implemented or maintained through the documented REST API, with the reason recorded in the implementation PR.

## Configuration system

### Design principles

The configuration system is a bounded tool-specific YAML contract authorized by ADR 0005's development-tool configuration exception. It does not authorize YAML as a second representation for ordinary simulation content.

Configuration MUST satisfy all of the following:

- Provider and model identifiers are not compiled constants in routing logic.
- API base URLs are configuration values.
- Credentials are references, never values.
- Unknown keys fail validation.
- Duplicate provider, model, route, style, and quality-tier IDs fail validation.
- YAML anchors, aliases, custom tags, and polymorphic type tags are prohibited.
- Configuration is deserialized only into explicit types.
- Model capabilities are explicit and machine validated.
- Provider-specific options are isolated beneath an `options` object and validated by the selected adapter.
- Prices include an effective date and are configuration data, never code constants.
- Effective configuration is hashable and recorded in generation receipts and manifests.

### Configuration precedence

Configuration precedence MUST be deterministic:

1. hardcoded safety defaults that deny external spend and protect approved assets;
2. tracked files under `config/assets/`;
3. optional untracked `.assetctl/config.local.yaml` operational overrides;
4. explicit CLI flags for the current invocation.

The local override MAY enable or disable providers, select routes, and set stricter or owner-approved spending limits. It MUST NOT contain secret values. Environment variables provide credentials only and MUST NOT act as an unrestricted hidden configuration layer.

The effective merged configuration and each contributing file hash MUST be included in the run receipt. The committed manifest MUST include the effective configuration hash used to select its asset.

### Root configuration

`config/assets/assetctl.yaml` controls repository paths and global safety behavior. An illustrative configuration follows:

```yaml
schema_version: '1'

paths:
  godot_asset_root: 'src/AlterCourse.Godot/assets'
  catalog_root: 'config/assets/catalog'
  style_root: 'config/assets/styles'
  work_root: '.assetctl/work'
  receipt_root: '.assetctl/runs'
  state_root: '.assetctl/state'
  log_root: '.assetctl/logs'

policy:
  external_generation_enabled: false
  unknown_price: 'reject'
  protect_approved_assets: true
  local_placeholder_fallback: true
  require_https_endpoints: true
  allow_remote_reference_urls: false
  retain_unselected_candidates: false

limits:
  maximum_download_bytes: 33554432
  maximum_reference_bytes: 16777216
  maximum_candidates_per_request: 10
  maximum_total_attempts: 12
  default_http_timeout_seconds: 120
  maximum_http_timeout_seconds: 300

spending:
  maximum_estimated_cost_per_asset_usd: 0.00
  maximum_estimated_cost_per_run_usd: 0.00
  maximum_estimated_cost_per_day_usd: 0.00
```

The committed default MUST deny paid generation until the owner deliberately configures a nonzero limit and enables external generation. This does not block development because the local placeholder fallback remains available.

Tool-side spending limits are guardrails, not authoritative billing controls. The owner SHOULD also configure prepaid credits or provider-side spending limits. `assetctl` MUST never enable provider auto-recharge or modify billing settings.

### Provider configuration

`config/assets/providers.yaml` declares instances and models. Provider instance names are repository-defined identifiers. The orchestration layer MUST treat them as opaque data.

```yaml
schema_version: '1'

providers:
  local-placeholder:
    adapter: 'local-placeholder'
    enabled: true
    models:
      default:
        model: 'local-svg-v1'
        capabilities:
          - 'raster.generate'
          - 'vector.generate'
          - 'image.transparent-output'
        economics:
          currency: 'USD'
          estimated_cost_per_output: 0.00
          effective_date: '2026-09-01'

  recraft-primary:
    adapter: 'recraft-images'
    enabled: true
    endpoint: 'https://external.api.recraft.ai/v1'
    credentials:
      api_key:
        source: 'environment'
        name: 'RECRAFT_API_TOKEN'
    downloads:
      allowed_hosts: []
    models:
      raster-v4-1:
        model: 'recraftv4_1'
        capabilities:
          - 'raster.generate'
          - 'image.transparent-output'
        economics:
          currency: 'USD'
          estimated_cost_per_output: 0.035
          effective_date: '2026-09-01'
      vector-v4-1:
        model: 'recraftv4_1_vector'
        capabilities:
          - 'vector.generate'
          - 'image.transparent-output'
        economics:
          currency: 'USD'
          estimated_cost_per_output: 0.08
          effective_date: '2026-09-01'

  openai-primary:
    adapter: 'openai-images'
    enabled: true
    endpoint: 'https://api.openai.com/v1'
    credentials:
      api_key:
        source: 'environment'
        name: 'OPENAI_API_KEY'
    models:
      image:
        model: 'gpt-image-2'
        capabilities:
          - 'raster.generate'
          - 'image.edit'
          - 'image.reference-input'
          - 'image.transparent-output'
        economics:
          currency: 'USD'
          pricing_basis: 'provider-calculated'
          effective_date: '2026-09-01'

  xai-bulk:
    adapter: 'xai-images'
    enabled: true
    endpoint: 'https://api.x.ai/v1'
    credentials:
      api_key:
        source: 'environment'
        name: 'XAI_API_KEY'
    models:
      standard:
        model: 'grok-imagine-image'
        capabilities:
          - 'raster.generate'
          - 'image.edit'
          - 'image.reference-input'
        economics:
          currency: 'USD'
          estimated_cost_per_output: 0.02
          effective_date: '2026-09-01'
      quality:
        model: 'grok-imagine-image-2.0'
        capabilities:
          - 'raster.generate'
          - 'image.edit'
          - 'image.reference-input'
        economics:
          currency: 'USD'
          pricing_basis: 'quality-and-resolution'
          effective_date: '2026-09-01'

  openai-reviewer:
    adapter: 'openai-vision-review'
    enabled: true
    endpoint: 'https://api.openai.com/v1'
    credentials:
      api_key:
        source: 'environment'
        name: 'OPENAI_API_KEY'
    models:
      economical:
        model: 'gpt-5.6-luna'
        capabilities:
          - 'review.semantic'
          - 'review.reference-comparison'
        economics:
          currency: 'USD'
          pricing_basis: 'token-usage'
          effective_date: '2026-09-01'
```

The values above are seed examples dated 2026-09-01. The implementation agent MUST verify current official model identifiers, endpoint behavior, capabilities, and pricing while implementing each adapter. After implementation, normal model and price changes are configuration updates rather than code changes.

The committed configuration MUST NOT claim a fixed cost when the provider charges by dimensions, quality, input, or token usage unless the model profile can calculate a conservative upper-bound estimate. Unknown or unbounded cost MUST fail closed when the policy is `reject`.

### Adapter and model capability validation

An adapter declares the protocol operations it can implement. A model profile declares which of those operations the configured model is intended to use. Startup MUST reject any configured capability outside the adapter's supported set.

The effective model capabilities are:

```text
adapter protocol capabilities
INTERSECT
configured model capabilities
```

This check proves that application code can perform the operation. It does not prove that a vendor has not changed a model remotely. `assetctl doctor --probe` MAY perform a minimal provider-specific capability probe when credentials and owner-approved spend are available. Canonical verification MUST use fixture-based adapter contract tests instead of live probes.

### Provider-specific options

Common request properties belong in the core request model. Provider-specific features MAY be represented beneath the model or route `options` node:

```yaml
options:
  output_quality: 'medium'
  provider_style_id: 'optional-provider-style-id'
```

The selected adapter MUST validate every key under `options`. Unknown provider-specific options fail before any API call. Core orchestration MUST NOT inspect or branch on those keys.

### Quality tiers

`config/assets/quality-tiers.yaml` defines candidate counts and validation policy:

```yaml
schema_version: '1'

quality_tiers:
  disposable:
    candidates: 1
    attempts_per_route: 1
    mechanical_validation: 'required'
    semantic_review: 'disabled'
    allow_unreviewed_placeholder: true

  development:
    candidates: 3
    attempts_per_route: 2
    mechanical_validation: 'required'
    semantic_review: 'when-available'
    allow_unreviewed_placeholder: true
    minimum_semantic_score: 0.70

  production-candidate:
    candidates: 6
    attempts_per_route: 2
    mechanical_validation: 'required'
    semantic_review: 'required'
    allow_unreviewed_placeholder: false
    minimum_semantic_score: 0.82

  final-candidate:
    candidates: 8
    attempts_per_route: 2
    mechanical_validation: 'required'
    semantic_review: 'required'
    allow_unreviewed_placeholder: false
    minimum_semantic_score: 0.88
```

Candidate and attempt counts remain bounded by root limits. A lifecycle does not automatically imply a quality tier, but default mappings SHOULD be:

| Lifecycle     | Default quality tier   |
| ------------- | ---------------------- |
| `placeholder` | `development`          |
| `candidate`   | `production-candidate` |
| `approved`    | Not generatable        |
| `deprecated`  | Not generatable        |

### Routing configuration

`config/assets/routing.yaml` MUST contain ordered declarative routes. Routing policy belongs here rather than in provider-specific C# branches.

```yaml
schema_version: '1'

routes:
  - id: 'vector-placeholder'
    priority: 100
    match:
      lifecycle:
        - 'placeholder'
      formats:
        - 'svg'
      required_capabilities:
        - 'vector.generate'
    targets:
      - provider: 'recraft-primary'
        model: 'vector-v4-1'
      - provider: 'local-placeholder'
        model: 'default'
    fallback:
      capability_match: true

  - id: 'raster-placeholder'
    priority: 90
    match:
      lifecycle:
        - 'placeholder'
      formats:
        - 'png'
        - 'webp'
      required_capabilities:
        - 'raster.generate'
    targets:
      - provider: 'xai-bulk'
        model: 'standard'
      - provider: 'openai-primary'
        model: 'image'
      - provider: 'recraft-primary'
        model: 'raster-v4-1'
      - provider: 'local-placeholder'
        model: 'default'
    fallback:
      capability_match: true

  - id: 'raster-production-candidate'
    priority: 110
    match:
      lifecycle:
        - 'candidate'
      formats:
        - 'png'
        - 'webp'
      required_capabilities:
        - 'raster.generate'
    targets:
      - provider: 'openai-primary'
        model: 'image'
      - provider: 'recraft-primary'
        model: 'raster-v4-1'
      - provider: 'xai-bulk'
        model: 'quality'
    fallback:
      capability_match: false

review_routes:
  - id: 'default-semantic-review'
    priority: 100
    match:
      required_capabilities:
        - 'review.semantic'
    targets:
      - provider: 'openai-reviewer'
        model: 'economical'
    fallback:
      capability_match: true
```

The committed initial ordering is a project policy choice and MAY be adjusted through YAML as provider quality, cost, reliability, or rights terms change.

### Style profiles

Style profiles state the project's visual language independently of any model. A profile MAY extend one parent profile. Cycles are invalid.

```yaml
schema_version: '1'
id: 'engineering-icons'
extends: 'global'

intent:
  - 'high-information-density command interface'
  - 'technical schematic clarity'
  - 'restrained Star Trek influence without copying a screen capture'

composition:
  perspective: 'orthographic'
  silhouette_priority: 'high'
  detail_level: 'low'
  centered: true
  padding_fraction: 0.10

rendering:
  gradients: 'avoid'
  texture: 'none'
  lighting: 'none'
  stroke_weight: 'consistent'
  background: 'transparent'

constraints:
  - 'no text unless the asset specification explicitly permits it'
  - 'no embedded logos or watermarks'
  - 'must remain legible at the smallest target size'
  - 'must remain distinct when desaturated'

avoid:
  - 'generic neon hologram styling'
  - 'ornamental detail without operational meaning'
  - 'skeuomorphic physical controls'
```

The prompt compiler combines semantic asset data, the resolved style profile, quality-tier instructions, and output constraints in a stable order. A provider adapter maps that canonical prompt and normalized request to provider fields. The manifest stores the final prompt sent to the provider.

Style profiles MUST NOT contain credentials, API endpoints, prices, or provider model IDs.

## Asset identity and catalog

### Asset IDs

Asset IDs MUST be stable, semantic, lowercase, and dot-namespaced. They MUST NOT encode provider, model, lifecycle, quality tier, file extension, or temporary implementation details.

Recommended examples are:

```text
ui.engineering.warp-drive.disabled
ui.engineering.shields.degraded
tactical.marker.hostile-ship
tactical.marker.anomaly
illustration.planet.m-class.placeholder-01
background.space.nebula.blue-01
```

The schema SHOULD enforce:

```regex
^[a-z0-9]+(?:[.-][a-z0-9]+)*$
```

An output path and an asset ID are independently unique. Two manifests may not claim the same ID or output path.

### Manifest location

Manifest paths SHOULD mirror the semantic output folder but need not be derived from the ID. Example:

```text
config/assets/catalog/ui/engineering/warp-drive-disabled.asset.yaml
src/AlterCourse.Godot/assets/ui/engineering/warp-drive-disabled.svg
```

### Manifest schema

A selected asset MUST have one manifest. An illustrative complete manifest follows:

```yaml
schema_version: '1'

id: 'ui.engineering.warp-drive.disabled'
lifecycle: 'placeholder'
kind: 'icon'
revision: 1

purpose: >-
  Indicate that warp propulsion is unavailable because damage or power state prevents operation.

output:
  path: 'src/AlterCourse.Godot/assets/ui/engineering/warp-drive-disabled.svg'
  format: 'svg'
  width: 64
  height: 64
  transparency: 'required'
  target_display_sizes:
    - 16
    - 24
    - 32
    - 64

visual:
  style_profile: 'engineering-icons'
  importance: 'secondary'
  tags:
    - 'warp'
    - 'damage'
    - 'propulsion'

constraints:
  required:
    - 'simple silhouette'
    - 'visually distinct from impulse propulsion'
    - 'recognizable when desaturated'
  prohibited:
    - 'text'
    - 'watermark'
    - 'opaque background'

references: []

rights:
  classification: 'unreviewed-generated-placeholder'
  license: null
  attribution: null
  source: null
  notes: 'Not approved for release.'

generation:
  source_type: 'generated'
  generated_at: '2026-09-01T13:42:17Z'
  run_id: '24a43c30-d86f-4e97-a213-207acfd4f1f6'
  route: 'vector-placeholder'
  provider: 'recraft-primary'
  adapter: 'recraft-images'
  model_profile: 'vector-v4-1'
  model: 'recraftv4_1_vector'
  quality_tier: 'development'
  final_prompt: >-
    Canonical prompt text sent to the provider.
  prompt_sha256: '...'
  request_sha256: '...'
  effective_config_sha256: '...'
  provider_request_id: null
  estimated_cost_usd: 0.24
  actual_cost_usd: null

validation:
  mechanical:
    status: 'pass'
    validator_version: '1'
    checks:
      - id: 'svg.safe-elements'
        status: 'pass'
      - id: 'svg.no-embedded-raster'
        status: 'pass'
      - id: 'output.dimensions'
        status: 'pass'
  semantic:
    status: 'pass'
    reviewer_provider: 'openai-reviewer'
    reviewer_model: 'gpt-5.6-luna'
    independence: 'different-provider-family'
    score: 0.91
    rubric_version: '1'
    findings: []

integrity:
  sha256: '...'
  byte_length: 4231
  media_type: 'image/svg+xml'

approval:
  approved_by: null
  approved_at: null
  approval_note: null

supersedes: null
```

The exact schema may refine field names during implementation, but it MUST retain equivalent information and MUST preserve the separation between semantic intent, generation provenance, validation, rights, and approval.

### Manifest authority

The tracked manifest is authoritative for:

- asset identity;
- lifecycle;
- intended output path;
- selected file integrity;
- semantic purpose and constraints;
- generation provenance;
- review status;
- rights classification;
- human approval record.

`.godot/imported`, provider dashboards, run receipts, shell history, and Git commit messages are not substitutes for the manifest.

## Lifecycle model

### States

| Lifecycle | Meaning | Autonomous creation | Autonomous replacement | Release eligibility |
| --- | --- | --- | --- | --- |
| `placeholder` | Development asset that unblocks implementation | Yes | Yes | No |
| `candidate` | Potential permanent asset awaiting human decision | Yes | Yes | No |
| `approved` | Human-accepted permanent asset | No | No | Yes, subject to rights policy |
| `deprecated` | Asset intentionally retired from new use | No | No | No |

A visually excellent asset remains a placeholder until its lifecycle changes. Cost, provider, or semantic score does not imply approval.

### Allowed transitions

```text
new -> placeholder
new -> candidate
placeholder -> candidate
placeholder -> deprecated
candidate -> approved
candidate -> deprecated
approved -> deprecated
```

`candidate -> approved` and `approved -> deprecated` require an explicit project-owner instruction. Tooling cannot cryptographically determine whether a human originated a command, so this boundary MUST be enforced by both a separate high-friction CLI operation and repository agent instructions.

### Approved asset immutability

Generation and publishing commands MUST refuse to overwrite an `approved` asset or mutate its generation fields. To replace an approved asset, create a new asset ID, set `supersedes` to the previous ID, approve the new asset explicitly, update game references, and then deprecate the old asset.

This rule avoids hidden replacement history and keeps the first implementation simple. Git history remains supporting evidence, not the only lifecycle record.

### Rights requirements by lifecycle

- `placeholder` MAY use `unreviewed-generated-placeholder`, but MUST be marked not approved for release.
- `candidate` MUST identify the expected rights basis and any reference inputs.
- `approved` MUST have a non-placeholder rights classification, license or rights note, and attribution when required.
- `approved` MUST NOT contain `unreviewed`, `unknown`, or an empty rights record.

AI semantic review MUST NOT be treated as legal clearance or proof of originality.

## Agent operating contract

Repository agent instructions added during implementation MUST establish this behavior:

1. Search the asset catalog before creating a new asset.
2. Reuse an existing asset when its semantic purpose and output contract fit.
3. When implementation needs a missing routine visual asset, create or update an asset specification and invoke `assetctl` rather than asking the owner to produce the file.
4. Default new development assets to lifecycle `placeholder` and quality tier `development`.
5. Use the local fallback when external generation is unavailable or disallowed.
6. Include the selected asset and manifest in the same implementation branch or PR that uses them.
7. Do not commit `.assetctl/` candidates, receipts, locks, logs, or local state.
8. Never invoke approval or approved-asset deprecation without an explicit owner instruction in the current task context.
9. Never change spend limits, enable paid providers, weaken validation, or bypass rights requirements merely to unblock a generation.
10. Report the chosen asset ID and `res://` path in the implementation summary.

The agent should ask the owner only when the semantic asset requirement itself is ambiguous in a way that affects gameplay or final art direction, not because a provider failed.

## CLI contract

### General behavior

The CLI MUST:

- run from any directory inside the repository;
- locate the repository root deterministically;
- accept `--output human|json` on every read or execution command;
- write command results to standard output and operational diagnostics and console logs to standard error, so structured JSON output is never contaminated by logging;
- support cancellation and bounded timeouts;
- use stable documented exit codes;
- avoid interactive prompts in normal agent workflows;
- support `--dry-run` for commands that could call providers or write tracked files;
- support `--offline` where applicable;
- never commit or push Git changes.

### Required commands

#### `assetctl validate-config`

Validates all tracked asset configuration, schemas, style profiles, and catalog manifests. It performs no network calls and no writes.

#### `assetctl doctor`

Reports:

- effective configuration files and hashes;
- provider and model enablement;
- credential variable names and presence without values;
- writable roots;
- decoder and renderer availability;
- route integrity;
- optional provider health.

`--probe` enables live provider checks. A probe MUST state whether it may spend. Default doctor behavior MUST be offline and free.

#### `assetctl find`

Searches manifests by ID, kind, lifecycle, tags, style profile, and text purpose. It returns deterministic ordering and Godot `res://` paths.

#### `assetctl status`

Summarizes catalog counts, missing files, integrity mismatches, placeholder inventory, candidates awaiting review, and approved assets.

#### `assetctl plan`

Resolves a specification without calling a provider or writing files. It reports:

- required capabilities;
- matching routes;
- eligible and rejected targets with reasons;
- reviewer selection;
- candidate and attempt counts;
- estimated maximum cost;
- whether local fallback would be used.

#### `assetctl generate`

Generates or regenerates a `placeholder` or `candidate` from a manifest or specification. It MUST support an existing manifest path and an asset ID. It MAY also support explicit CLI fields for rapid placeholder creation, but any committed asset must end with a complete manifest.

#### `assetctl verify`

Verifies one asset or the complete catalog. Mechanical checks are offline. Semantic review runs only when explicitly requested or required as part of a generation command with an available reviewer.

#### `assetctl approve`

Promotes a candidate to approved only after explicit confirmation fields are provided. It MUST require at least:

```text
--approved-by
--approval-note
--confirm-approved-asset
```

It MUST validate integrity, mechanical results, semantic policy, rights data, and lifecycle. It MUST NOT generate or alter image bytes.

Repository agents are prohibited from invoking this command without explicit owner instruction.

#### `assetctl deprecate`

Marks an asset deprecated with an actor and reason. Approved assets require the same explicit owner authorization boundary as approval.

### Suggested exit codes

| Code | Meaning                                                       |
| ---- | ------------------------------------------------------------- |
| 0    | Success or requested state already satisfied                  |
| 1    | Validation or policy failure                                  |
| 2    | Invalid invocation or invalid configuration                   |
| 3    | Provider authentication or authorization failure              |
| 4    | Provider unavailable, rate limited, or timed out after policy |
| 5    | No eligible route or reviewer                                 |
| 6    | Budget refusal                                                |
| 7    | Filesystem, integrity, or publish failure                     |
| 8    | Protected lifecycle operation refused                         |

Provider-specific errors MUST be normalized into stable application categories while retaining a redacted diagnostic summary in the run receipt.

## Core domain model

The application domain SHOULD contain immutable records equivalent to:

```csharp
public sealed record AssetRequest;
public sealed record AssetOutputContract;
public sealed record AssetVisualIntent;
public sealed record AssetReference;
public sealed record ProviderInstance;
public sealed record ModelProfile;
public sealed record RouteDefinition;
public sealed record GenerationPlan;
public sealed record GenerationAttempt;
public sealed record GeneratedCandidate;
public sealed record MechanicalValidationResult;
public sealed record SemanticReviewResult;
public sealed record AssetManifest;
public sealed record RunReceipt;
```

Provider DTOs MUST remain in provider-specific namespaces and MUST NOT leak into core routing or catalog records.

## Adapter architecture

### Interfaces

Generation and review are distinct capabilities. The implementation SHOULD use small interfaces equivalent to:

```csharp
public interface IAssetGenerator
{
    string AdapterId { get; }
    IReadOnlySet<AssetCapability> SupportedCapabilities { get; }

    Task<GenerationBatchResult> GenerateAsync(
        ProviderExecutionContext context,
        NormalizedGenerationRequest request,
        CancellationToken cancellationToken);
}

public interface IAssetReviewer
{
    string AdapterId { get; }
    IReadOnlySet<AssetCapability> SupportedCapabilities { get; }

    Task<SemanticReviewResult> ReviewAsync(
        ProviderExecutionContext context,
        SemanticReviewRequest request,
        CancellationToken cancellationToken);
}

public interface IProviderHealthProbe
{
    Task<ProviderHealthResult> CheckAsync(
        ProviderExecutionContext context,
        HealthProbeMode mode,
        CancellationToken cancellationToken);
}
```

Exact names MAY change, but generation and review MUST remain separable so an adapter is not forced to expose meaningless methods.

### Initial adapters

The first complete implementation MUST provide:

- `local-placeholder` generator;
- `recraft-images` generator;
- `openai-images` generator and editor;
- `xai-images` generator and editor;
- at least one structured vision-review adapter, initially `openai-vision-review`.

The implementation MAY share internal HTTP helpers among adapters with similar endpoint shapes. Similarity does not justify pretending distinct providers have identical semantics. Each adapter owns its request mapping, response parsing, errors, and option validation.

### Adapter registration

Adapters MAY be registered in one composition root through dependency injection or an explicit factory registry. The generation orchestrator and router MUST NOT contain provider-name `if`, `switch`, or pattern-matching branches.

Dynamic assembly discovery is deferred. Adding it requires a demonstrated consumer outside this repository or another concrete need and should receive a separate architectural decision.

### Adding providers later

A provider addition follows one of two paths.

**Existing adapter protocol:** add a provider instance, endpoint, credential reference, model profiles, economics, and routes in YAML. No application code changes are allowed.

**New API protocol:** add one adapter, typed DTOs, fixture-based contract tests, registration, and YAML configuration. Catalog, routing, generation orchestration, review orchestration, lifecycle, validation, and publishing code must remain unchanged.

An adapter that requires changes across those core components indicates a missing general capability or a leaky provider design and must be corrected at the boundary rather than patched with vendor branches.

## Local placeholder adapter

The local adapter is a required reliability feature, not a test fake.

It MUST produce deterministic, visually obvious placeholders from the asset ID and output contract without network access or external spend. It SHOULD:

- generate SVG directly for vector requests;
- render PNG when raster output is mandatory;
- use a stable geometric composition based on the asset kind;
- include a short sanitized identifier only when the specification permits text;
- otherwise use distinctive geometry and a placeholder mark;
- respect dimensions and transparency;
- never claim semantic fidelity beyond the supplied purpose;
- always classify output as a placeholder;
- never be eligible for candidate or approved lifecycle routes.

The same request and local-adapter version MUST produce equivalent normalized bytes. Golden-file tests MUST protect this behavior.

## Request and prompt model

### Semantic request

Agents state what the asset means, not how a particular model should be prompted. The semantic request includes:

- stable asset ID;
- lifecycle and quality tier;
- kind and purpose;
- output format, dimensions, transparency, and target display sizes;
- style profile;
- required and prohibited visual constraints;
- references with rights metadata;
- optional provider-neutral composition and rendering controls.

### Prompt compilation

The prompt compiler MUST produce a stable prompt in this order:

1. asset role and semantic purpose;
2. kind-specific composition guidance;
3. resolved style-profile intent;
4. output and target-size requirements;
5. required constraints;
6. prohibited content;
7. reference instructions;
8. lifecycle-specific reminder that placeholders must remain functionally clear rather than polished at any cost.

Provider adapters MAY transform the canonical request into multiple fields, but MUST NOT silently discard a hard constraint. Unsupported requirements make the provider ineligible before invocation.

The final provider prompt and its SHA-256 hash MUST be recorded. Prompt construction version MUST be versioned so changes are visible in provenance.

### Reference inputs

Reference images MUST:

- use repository-confined local paths by default;
- have a SHA-256 hash recorded before upload;
- declare a rights classification and purpose;
- satisfy a configured byte and dimension limit;
- be uploaded only to provider profiles permitted to receive reference images;
- never be fetched automatically from arbitrary remote URLs.

Remote reference URLs are prohibited in the initial configuration. Adding them later requires HTTPS, host allowlisting, size limits, rights metadata, and an explicit owner policy change.

## Routing algorithm

Routing MUST be deterministic and explainable.

For each request:

1. Resolve and validate the manifest, quality tier, and style profile.
2. Derive required capabilities from output and references.
3. Find routes whose match predicates apply.
4. Order routes by descending priority, then configuration order.
5. Evaluate explicit targets in listed order.
6. Reject targets that are disabled, uncredentialed, unhealthy, over budget, incompatible, disallowed for the lifecycle, outside configured limits, or missing capabilities.
7. When enabled, evaluate capability-based fallback across remaining model profiles.
8. Break fallback ties by configured fallback rank and then provider/model ID for deterministic output.
9. Select a reviewer through the same process when semantic review applies.
10. Emit the plan before any external operation.

`assetctl plan --output json` MUST expose every eligibility and rejection reason so an agent can diagnose configuration without reading code.

Provider health SHOULD be cached only for the current process unless a short-lived local cache is needed to avoid repeated failing calls. Persistent health state is advisory and may not override fresh explicit requests indefinitely.

## Generation orchestration

### End-to-end sequence

`assetctl generate` MUST perform this sequence:

1. Acquire a per-asset local lock.
2. Load and validate effective configuration.
3. Resolve an existing manifest and protect approved or deprecated states.
4. Search the catalog for an already suitable asset when the command creates a new semantic request.
5. Compile the generation plan.
6. Calculate a conservative maximum cost.
7. Enforce per-asset, per-run, and local daily guardrails.
8. Create a unique run work directory.
9. Invoke the selected provider/model for the configured candidate count.
10. Normalize each candidate to a local file with trusted media metadata.
11. Run mandatory mechanical validation.
12. Create target-size previews.
13. Run semantic review when required or available.
14. Reject hard failures.
15. Score remaining candidates.
16. Select one candidate deterministically.
17. Write the selected asset and complete manifest through a rollback-safe publish operation.
18. Write the detailed run receipt.
19. Release the lock.
20. Return the asset ID, repository path, Godot `res://` path, lifecycle, validation status, cost estimate, and receipt path.

### Candidate batching

When a provider supports multiple outputs in one request, the adapter SHOULD use that facility within provider and policy limits. Otherwise, it MAY issue bounded concurrent requests. Concurrency MUST be configurable and MUST NOT exceed provider or root limits.

Generation SHOULD prefer several independent candidates over repeated edit loops for ordinary placeholders. Edit or refinement passes MAY be used when the request explicitly supplies a reference or the quality tier permits them.

### Idempotency

If an existing placeholder or candidate:

- has the same request hash;
- has the same effective style and quality configuration hash;
- passes current mechanical validation;
- satisfies required review policy;
- and its output hash matches the manifest;

then generation MUST return the existing asset without external calls unless `--force` is explicitly supplied.

`--force` MUST NOT bypass approved-asset protection, budget checks, rights policy, or validation.

### Concurrency

A per-asset lock under `.assetctl/state/locks/` MUST prevent two agents from publishing the same asset concurrently. Locks MUST contain process and timestamp diagnostics and MUST have a safe stale-lock recovery policy.

Different asset IDs MAY generate concurrently subject to configured global concurrency and budget limits.

### Publishing and rollback

Publishing modifies an asset file and a manifest. The tool MUST:

- stage normalized output and manifest in the work directory;
- verify hashes before publication;
- create parent directories safely;
- use same-filesystem temporary files and atomic rename where supported;
- preserve previous placeholder or candidate files until both new files are ready;
- roll back to the previous pair if either replacement fails;
- never leave a manifest claiming an output hash that was not published;
- refuse symlink traversal outside configured roots.

The tool MUST NOT touch unrelated Godot files or `.godot/imported` state.

## Candidate validation

### Mechanical validation is mandatory

Every selected asset MUST pass mechanical validation. AI review can supplement but never replace it.

Common checks include:

- nonempty file;
- expected extension and media type agree;
- decoder or parser accepts the file;
- file and decoded dimensions remain within limits;
- expected width, height, or aspect tolerance is satisfied;
- no unexpected animation;
- output path is repository-confined;
- byte size is within policy;
- integrity hash is calculated after normalization.

### Raster validation

Raster checks MUST include:

- successful full decode, not header-only identification;
- width and height validation;
- alpha-channel presence when transparency is required;
- transparent-pixel validation when transparent background is required;
- no frame animation unless a future asset type explicitly supports it;
- normalized orientation;
- removal of EXIF, location, comments, and unnecessary provider metadata;
- bounded pixel count to prevent decompression bombs;
- deterministic or stable normalization to the selected committed format;
- review previews at every configured target size.

### SVG validation and sanitization

SVG output MUST be treated as untrusted XML. The validator MUST reject or remove, according to explicit policy:

- scripts;
- event-handler attributes;
- external URLs and network references;
- `<foreignObject>`;
- embedded HTML;
- embedded raster `<image>` elements when true vector output is required;
- data URLs unless explicitly permitted;
- external fonts and stylesheets;
- entity expansion and DTDs;
- unsupported filters or masks when the asset profile forbids them;
- dimensions or view boxes outside policy;
- text elements when text is prohibited;
- provider metadata that is not required for rendering.

The sanitized result MUST be reparsed and rendered before acceptance. Sanitization behavior MUST be covered by malicious fixtures and regression tests.

### Target-size validation

Icons and markers MUST be evaluated at their actual target sizes. The validator creates PNG previews for semantic review and records target-size generation success. An image that appears good at provider resolution but becomes unreadable at 16 or 24 pixels must be rejectable by the semantic rubric.

## Semantic AI review

### Purpose

Semantic review answers whether a mechanically valid candidate is useful for the stated purpose and consistent with the selected visual language. It does not decide legal ownership or approval.

### Independence

The router SHOULD prefer a reviewer from a different provider family than the generator. The manifest records one of:

```text
different-provider-family
same-provider-family
local-only
not-run
```

A production or final candidate SHOULD require `different-provider-family` unless configuration explicitly permits same-provider review. A placeholder MAY proceed with mechanical validation only when its quality tier allows unreviewed placeholders.

### Review input

The reviewer receives:

- the semantic asset request;
- required and prohibited constraints;
- resolved style summary;
- original-size image or safe render;
- target-size previews;
- approved style-reference images when configured;
- a strict JSON result schema.

It MUST NOT receive credentials, signed download URLs, unrelated repository content, or hidden provider diagnostics.

### Review rubric

The first rubric version MUST score at least:

```json
{
	"matches_subject": true,
	"required_constraints_satisfied": true,
	"prohibited_content_absent": true,
	"readable_at_target_sizes": true,
	"style_adherence": 0.88,
	"semantic_clarity": 0.91,
	"visual_defects": [],
	"unrequested_text_detected": false,
	"logo_or_watermark_detected": false,
	"overall_score": 0.9,
	"decision": "pass"
}
```

Boolean hard failures reject the candidate. Numeric metrics are range-checked and combined only after schema validation. Free-form reviewer prose MAY be retained as findings but MUST NOT control the result without structured fields.

### Reviewer failure

When review is `required`, failure to obtain a valid structured review rejects that generation route and triggers configured fallback. When review is `when-available`, the placeholder may proceed if mechanical validation passes; its manifest must record `not-run` or `unavailable` rather than falsely claiming review.

## Candidate scoring and selection

Candidate selection MUST be deterministic after provider outputs and review results exist.

1. Remove candidates with any hard mechanical failure.
2. Remove candidates with required semantic hard failures.
3. Remove candidates below the quality-tier minimum score.
4. Rank by configured weighted semantic score.
5. Use kind-specific readability as the first tie-break for icons and markers.
6. Use candidate creation order as the final deterministic tie-break.

Cost MUST be controlled before generation, not used to justify selecting a worse candidate after money has already been spent. The run receipt retains scores for all candidates even though only the selected asset is committed.

## Cost and usage controls

### Fail-closed policy

External generation MUST be denied when any of the following applies:

- global external generation is disabled;
- the provider or model is disabled;
- required credential environment variables are missing;
- cost cannot be conservatively estimated and unknown price policy is `reject`;
- per-asset, per-run, or local daily limit would be exceeded;
- candidate or retry counts exceed limits;
- the provider is not allowed for the requested lifecycle;
- the request requires an unsupported billable operation.

For placeholders, denial falls through to the local adapter when enabled. For production candidates, denial returns a clear failure rather than silently substituting a simplistic local placeholder.

### Estimates and actuals

The tool MUST record:

- estimate basis;
- number of requested outputs;
- estimated maximum cost;
- known provider usage fields;
- actual cost only when the API returns enough trustworthy information to calculate it.

It MUST distinguish `null` actual cost from zero.

### Local usage ledger

A best-effort append-only ledger under `.assetctl/state/` MAY enforce daily limits across processes. It MUST use a lock and atomic update. The ledger is not authoritative financial accounting and MUST NOT be committed.

### Subscription boundary

Interactive subscriptions are not provider automation credentials. OpenAI states that API usage is billed separately from ChatGPT subscriptions. xAI states that Grok and API billing are separate. Recraft distinguishes API Units from Studio subscription credits for programmatic API use. `assetctl` therefore uses API credentials and provider-side billing controls only; it MUST NOT attempt to consume interactive web subscription allowances through browser automation.

## Reliability and fallback

### Error categories

Adapters MUST normalize at least:

- invalid request;
- authentication failure;
- authorization failure;
- insufficient balance or quota;
- rate limit;
- transient network failure;
- provider server failure;
- provider timeout;
- malformed provider response;
- unsafe download response;
- unsupported output;
- validation failure.

### Retry policy

Retries MUST be bounded and configuration-driven. Default retryable conditions SHOULD be network interruption, HTTP 408, HTTP 429, and HTTP 5xx. Authentication, authorization, invalid request, unsupported capability, and policy failures MUST NOT be retried unchanged.

Adapters MUST honor `Retry-After` within the configured maximum and add bounded jitter when appropriate. No request may loop indefinitely.

### Fallback behavior

Fallback is attempted only when the route permits the observed error category. A validation failure MAY try another candidate or provider. An authentication failure SHOULD skip the failed provider instance and continue to another configured target. A budget refusal MUST NOT be bypassed by trying another paid target whose estimate also exceeds the limit.

The final placeholder fallback is `local-placeholder`. This is what ensures routine implementation continues without operator interruption.

## Security requirements

### Credentials

The first implementation supports environment-variable credential references only.

```yaml
credentials:
  api_key:
    source: 'environment'
    name: 'RECRAFT_API_TOKEN'
```

The tool MUST:

- report only variable name and presence;
- never log, print, hash into a manifest, or serialize the value;
- never accept a literal credential in tracked YAML;
- never add arbitrary authorization headers from configuration;
- redact provider headers and signed query strings from diagnostics;
- remain compatible with the repository's gitleaks checks.

External secret-manager adapters are deferred until there is a concrete need.

### Path containment

All configured repository paths, manifest paths, output paths, references, and temporary publish targets MUST be normalized and verified to remain under their allowed roots. Symlinks MUST NOT permit escape. Absolute output paths and parent traversal are prohibited.

### Network response safety

When a provider returns a URL instead of bytes, the adapter MUST:

- require HTTPS;
- restrict the hostname to the provider profile's allowlist;
- validate each redirect independently;
- apply connection and total timeouts;
- enforce maximum response bytes while streaming;
- verify media type and decode the final bytes;
- avoid persisting signed URLs;
- redact URL query strings from logs and receipts.

The tool MUST prefer inline bytes or base64 responses when provider support makes that practical.

### YAML and JSON safety

YAML parsing MUST reject custom tags, duplicate keys, anchors, aliases, and unbounded recursive structures. JSON parsing MUST use bounded streams and explicit DTOs. Provider response text MUST be treated as untrusted data.

### SVG safety

SVG sanitization requirements in this specification are security requirements, not optional style checks.

### Logging and structured observability

Operational logging MUST conform to ADR 0008 in addition to the redaction requirements below.

- Serilog MUST be configured at the application composition root and connected through `Microsoft.Extensions.Logging`.
- Application components that need operational logging MUST receive `ILogger<T>` through construction rather than use static global logging state or a service locator.
- Console logging and diagnostics MUST write to standard error. Standard output is reserved for the command's human or JSON result contract.
- The default development configuration MUST use a human-readable console sink and a bounded structured rolling-file sink beneath `.assetctl/logs/`. Tests that assert diagnostics SHOULD use an in-memory or collecting sink.
- Structured fields SHOULD include stable command, run, asset, route, provider, model-profile, attempt, error-classification, and elapsed-duration identifiers where applicable.
- Logs SHOULD use stable identifiers and hashes instead of copying full prompts, reference material, image bytes, provider response bodies, or other large or rights-sensitive content. The manifest and run receipt remain the specified provenance locations for the final prompt and detailed attempt evidence.
- Logging sink failure MUST NOT change routing, scoring, selection, lifecycle, or publish semantics and MUST NOT leave partially applied tracked state. A sink failure may be surfaced as an operational diagnostic through a safe fallback path.
- Logs are diagnostics, not authority. The tool MUST NOT decide whether generation, validation, review, approval, or publication occurred by querying a log sink.

Logs and receipts MUST redact:

- API keys and authorization headers;
- signed URLs and sensitive query parameters;
- raw environment values;
- provider response bodies that may echo secrets;
- local paths outside the repository when not required for diagnosis.

A redaction regression test MUST cover each provider adapter. Run receipts remain a separate provenance contract and MUST NOT be treated as a Serilog sink or reconstructed from log events.

## Rights, licensing, and project legal boundaries

ST:AC is an unofficial non-commercial fan project with mixed-rights content. The repository's MIT license covers original software and expressly does not grant rights in Star Trek or other third-party material. Asset tooling MUST preserve that distinction.

The pipeline MUST NOT assume that provider output is automatically original, copyrightable, commercially usable, or covered by the repository's MIT license. Provider terms and model behavior can change.

Each manifest MUST classify rights. Initial controlled values SHOULD include:

```text
original-project-created
original-provider-generated
third-party-licensed
third-party-fan-project-reference
unreviewed-generated-placeholder
unknown
```

`unknown` and `unreviewed-generated-placeholder` are prohibited for approved assets.

References MUST identify their source and rights basis. The tool MUST NOT automatically scrape production stills, logos, fan art, screenshots, or other third-party artwork from the web. A human-supplied reference may be used only when its manifest declaration states why it may be submitted to the configured provider.

The semantic reviewer MAY flag obvious logos, watermarks, or recognizable copied elements, but its finding is advisory. Human approval remains responsible for rights review appropriate to this fan project's policy.

Provider profiles SHOULD include lifecycle permissions and reference-input policy so a provider can be permitted for disposable placeholders but excluded from permanent candidates if its current terms, privacy behavior, or output rights are unsuitable.

## Provenance and run receipts

### Committed provenance

The manifest MUST preserve the selected result's:

- provider instance;
- adapter ID and adapter version;
- model profile and vendor model ID;
- route and quality tier;
- final prompt and prompt hash;
- semantic request hash;
- effective configuration hash;
- generated timestamp;
- provider request ID when safe and useful;
- estimate and actual-cost status;
- validation results and versions;
- selected file hash and size;
- reference hashes;
- lifecycle, rights, and approval data.

### Detailed run receipt

Each generation attempt writes `.assetctl/runs/<run-id>.json` containing:

- command and sanitized arguments;
- repository and effective configuration hashes;
- generation plan;
- every attempted target;
- retry and fallback events;
- provider request IDs;
- candidate temporary paths;
- mechanical results;
- semantic reviews;
- scores and selection reason;
- estimated and known actual usage;
- redacted errors;
- publish and rollback result.

Receipts are ignored by Git because they can be numerous and provider-specific. The committed manifest carries the durable selected summary. Receipts are provenance and execution evidence rather than an operational logging sink; diagnostic logs remain independently bounded and non-authoritative under ADR 0008.

### No database

Catalog discovery scans and validates tracked manifests. The expected early catalog does not justify a database or generated central index. An in-process index MAY accelerate one invocation. A persistent index may be added only when measured catalog performance warrants it and must remain reconstructible from manifests.

## Godot integration

### Asset paths

Published assets live beneath `src/AlterCourse.Godot/assets/`. The CLI JSON result MUST include both repository-relative and Godot paths:

```json
{
	"repository_path": "src/AlterCourse.Godot/assets/ui/engineering/warp-drive-disabled.svg",
	"godot_path": "res://assets/ui/engineering/warp-drive-disabled.svg"
}
```

### Import behavior

`assetctl` MUST NOT depend on Godot for normal generation. The repository quality gate already starts and tests Godot headlessly. Implementation MUST add focused integration coverage proving that representative generated PNG and sanitized SVG fixtures import and load through the actual Godot runtime.

The tool MUST NOT commit `.godot/` import cache data.

### Runtime separation

No provider client, asset manifest parser, routing policy, generation prompt, credential reference, or review code belongs in `AlterCourse.Godot` or ships with the game. Runtime game code references selected assets only.

## Testing specification

`AlterCourse.AssetCtl.Tests` MUST use xUnit as the ordinary .NET test framework. It MUST remain Godot-independent and must not start the engine merely because generated assets are ultimately consumed by Godot. Provider, filesystem, time, and diagnostic boundaries SHOULD use focused in-memory implementations or test doubles rather than deep mocks. CsCheck MAY be added when a meaningful property, model, or metamorphic invariant justifies it; it MUST NOT be added merely so every test style named by ADR 0009 appears in this tool. ArchUnitNET MAY be used only for durable architecture rules that are not already enforced more simply by project references, analyzers, or focused assembly-reference tests.

### Unit tests

Unit tests MUST cover:

- all configuration precedence rules;
- unknown and duplicate YAML keys;
- prohibited YAML constructs;
- capability intersection and unsupported claims;
- deterministic route ordering and tie-breaking;
- route rejection reasons;
- missing credential behavior;
- fail-closed unknown pricing;
- per-asset, per-run, and daily budget calculations;
- lifecycle transitions;
- approved-asset immutability;
- asset ID and output-path uniqueness;
- repository path and symlink containment;
- prompt compilation order and hashing;
- reference hashing and rights requirements;
- candidate hard-failure filtering;
- candidate score calculation and tie-breaking;
- idempotent no-op decisions;
- error normalization;
- retry eligibility and bounds;
- structured-log and receipt redaction;
- logging sink isolation from command semantics;
- atomic publish rollback decisions.

### Adapter contract tests

Every external adapter MUST have fixture-based HTTP contract tests that verify:

- endpoint and method;
- authentication header construction without exposing values;
- request body mapping;
- model and candidate-count mapping;
- edit and reference multipart behavior where applicable;
- response parsing for inline and URL outputs;
- malformed response handling;
- 401, 403, 408, 429, and 5xx normalization;
- provider request ID capture;
- option validation;
- download allowlisting and redirects;
- timeout and maximum-byte enforcement.

These tests MUST use an in-process fake HTTP handler or local test server. They MUST NOT call live providers.

### Mechanical validator tests

Fixtures MUST include:

- valid and corrupt PNG files;
- alpha and no-alpha PNG files;
- mismatched extension and media type;
- oversized dimensions and decompression-bomb guards;
- valid minimal SVG;
- SVG script and event handlers;
- DTD and entity attempts;
- external URLs;
- `<foreignObject>`;
- embedded raster images;
- prohibited text;
- malformed view box and dimensions;
- provider metadata requiring removal;
- target-size render failure.

### Integration tests

A complete fake-provider integration test MUST exercise:

```text
manifest -> route -> candidate generation -> validation -> review -> selection
-> atomic publish -> manifest update -> JSON result
```

It MUST also prove rollback when manifest publication fails after asset staging.

### Local adapter golden tests

Golden tests MUST establish deterministic SVG and PNG placeholder output. Intentional output changes require reviewing and updating goldens rather than disabling the comparison.

### Assembly-boundary test

Tests MUST inspect `AlterCourse.AssetCtl` assembly references and fail if it references `AlterCourse.Core`, `AlterCourse.Godot`, or a `Godot*` assembly.

### Godot integration tests

At least one generated-style PNG and one sanitized SVG fixture MUST load through a headless Godot integration test using the repository-selected Godot-aware test layer. The test belongs in the existing Godot test layer, not in `AlterCourse.AssetCtl.Tests` or the AssetCtl application assembly.

### Network isolation

The full test suite and canonical verifier MUST pass with no provider credentials and no internet access. Any accidental external HTTP call in tests should fail immediately.

## Canonical verification changes

The implementation PR MUST update the canonical gate so that it performs, in order appropriate to the existing script:

```bash
dotnet restore AlterCourse.sln --locked-mode
dotnet build AlterCourse.sln -c Release --no-restore --warnaserror
dotnet test tests/AlterCourse.Core.Tests/AlterCourse.Core.Tests.csproj \
  -c Release --no-build --no-restore
dotnet test tests/AlterCourse.AssetCtl.Tests/AlterCourse.AssetCtl.Tests.csproj \
  -c Release --no-build --no-restore
dotnet run --project tools/AlterCourse.AssetCtl/AlterCourse.AssetCtl.csproj \
  -c Release --no-build -- validate-config --offline
```

The exact invocation MAY use the built executable or a repository script, but CI MUST invoke only `./scripts/verify.sh` as established by ADR 0002.

Validation executed from the canonical gate MUST be read-only. It may verify all committed assets and manifests mechanically, but it MUST NOT regenerate, normalize, or rewrite them.

## Configuration and manifest schemas

Every tracked YAML contract MUST carry `schema_version`. JSON Schema files under `config/assets/schemas/` SHOULD document editor-visible shape and examples. Runtime C# validation remains authoritative for operational constraints that JSON Schema cannot express, such as cross-file uniqueness, route references, capability intersection, root containment, and lifecycle-dependent rights requirements.

Schema-version behavior MUST be:

- reject unknown future major versions;
- support the current version without silent coercion;
- produce path-specific errors;
- require an explicit migration command before rewriting older tracked files;
- never mutate configuration during ordinary validation.

The first implementation does not need a migration command because only schema version 1 exists, but parsing and error types must not preclude one.

## Human approval workflow

A permanent-asset review should operate as follows:

1. Generate one or more assets with lifecycle `candidate` and an appropriate production quality tier.
2. Review the selected candidate and, when useful, retained contact sheet outside the game.
3. Confirm mechanical and semantic results.
4. Confirm rights classification, provider terms relevance, reference sources, and required attribution.
5. Provide an explicit instruction to approve the named asset ID.
6. Run `assetctl approve` with actor and approval note.
7. Review the manifest-only approval diff.
8. Merge through normal repository review.

Approval does not need to regenerate the asset. An approved asset's hash must be identical before and after promotion.

## Provider seed decisions

### Recraft

The initial Recraft adapter SHOULD be the preferred route for:

- SVG UI glyphs;
- tactical markers;
- engineering-system icons;
- simple emblems;
- other clean vector assets.

The current Recraft API exposes raster and vector generation, including `recraftv4_1` and `recraftv4_1_vector`. Model IDs and economics remain YAML values.

### OpenAI

The initial OpenAI image adapter SHOULD be the preferred route for:

- high-quality raster illustrations;
- reference-driven variants;
- image edits;
- detailed planets, environments, and backgrounds where vector output is inappropriate.

The current image model seed is `gpt-image-2`. Semantic review uses a separately configured image-input text model rather than asking the image generator to judge its own output.

### xAI

The initial xAI adapter SHOULD provide:

- a low-cost alternate raster route;
- an alternate provider when OpenAI or Recraft is unavailable;
- image editing and reference input when supported by the configured model.

The current seed profiles are `grok-imagine-image` and `grok-imagine-image-2.0`. They remain configuration data.

### Provider availability

A provider's absence must not make the tool unusable. Missing credentials mark its target ineligible and explain the reason. The router continues to another target and ultimately to the local placeholder route for placeholder requests.

## Implementation sequence

The implementing agent SHOULD build vertical slices in this order.

### Phase 1: Foundation and local fallback

Deliver:

- solution and `net10.0` project scaffolding;
- CLI shell and JSON output contract;
- structured logging composition and redaction under ADR 0008;
- safe YAML loading;
- root configuration, quality tiers, style profiles, manifests, and schemas;
- catalog discovery and `find`, `status`, `validate-config`;
- lifecycle policy and approved protection;
- local placeholder generator;
- mechanical SVG and PNG validation;
- atomic publication and receipts;
- xUnit unit, golden, integration, and boundary tests;
- canonical gate integration.

At the end of Phase 1, an agent can create a validated zero-cost placeholder offline.

### Phase 2: Provider registry and Recraft

Deliver:

- provider/model configuration;
- capability routing and `plan`;
- credential references;
- cost estimates and limits;
- typed HTTP execution and redaction;
- Recraft raster and vector adapter;
- retry, fallback, and contract tests.

At the end of Phase 2, configured vector and raster placeholders can use Recraft and fall back locally.

### Phase 3: OpenAI and xAI generation

Deliver:

- OpenAI image generation and edit adapter;
- xAI image generation and edit adapter;
- reference-image upload path;
- provider-specific option validation;
- current seed YAML and fixture tests.

At the end of Phase 3, all three initial external generation families are available through configuration.

### Phase 4: Semantic review and candidate selection

Deliver:

- review-provider configuration and routing;
- OpenAI structured vision-review adapter;
- target-size previews;
- rubric schema and validation;
- independent-provider preference;
- multi-candidate scoring and selection;
- review failure fallback tests.

At the end of Phase 4, production candidates receive independent structured semantic review.

### Phase 5: Approval and agent integration

Deliver:

- `approve` and `deprecate` commands;
- rights and approval policy enforcement;
- agent instruction updates;
- Godot import fixtures and integration tests;
- final operator and agent documentation updates required by the implementation.

The implementation MAY arrive in multiple PRs, but each merged phase must be complete, tested, and useful. Do not create empty abstractions for later phases without a current consumer.

## Acceptance criteria

The complete asset pipeline is accepted when all of the following are true:

- `AlterCourse.AssetCtl` and `AlterCourse.AssetCtl.Tests` target `net10.0` using the repository-pinned .NET 10 SDK.
- It references neither game project nor Godot; the game projects' Godot-driven target framework remains an independent runtime concern.
- `AlterCourse.AssetCtl.Tests` uses xUnit and is part of the canonical quality gate.
- All tests and committed validation run offline with no provider credentials.
- New dependencies satisfy ADR 0003 admission evidence and remain centrally pinned with committed lock files.
- Asset-tool YAML configuration and manifests remain development and presentation metadata and do not become canonical Core domain content.
- Operational logging uses `Microsoft.Extensions.Logging` with Serilog configured at the composition root, keeps stdout reserved for command results, writes console diagnostics to stderr, and treats logs as non-authoritative diagnostics distinct from run receipts.
- Provider instances, endpoints, credentials references, model IDs, capabilities, economics, routes, quality tiers, and style profiles are tracked configuration.
- No orchestration or routing code branches on Recraft, OpenAI, or xAI names.
- Adding another model or provider instance through an existing adapter is proven by a configuration-only test.
- Adding a fake new adapter requires no change to catalog, routing, lifecycle, validation, selection, or publishing implementations.
- The local adapter can create a deterministic validated SVG and PNG placeholder.
- Recraft raster and vector generation are implemented with fixture contract tests.
- OpenAI image generation and editing are implemented with fixture contract tests.
- xAI image generation and editing are implemented with fixture contract tests.
- At least one independent semantic reviewer returns schema-validated structured results.
- Multiple candidates are mechanically filtered, semantically reviewed according to tier, and deterministically selected.
- Missing credentials, disabled spend, provider failure, rate limiting, and validation failure follow configured fallback behavior.
- Unknown price and over-budget operations fail closed.
- Selected assets and manifests publish together with rollback protection.
- Every committed generated asset has a valid manifest and matching SHA-256 hash.
- SVG sanitization rejects active content, external references, and prohibited embedded content.
- Raster validation fully decodes files, enforces pixel limits, verifies alpha requirements, and strips unwanted metadata.
- Approved assets cannot be regenerated or overwritten.
- Approval requires explicit actor, note, confirmation, rights data, and unchanged asset hash.
- Rights metadata reflects the repository's mixed-rights legal boundary.
- Provider credentials and signed URLs never appear in output, logs, receipts, manifests, or Git.
- The tool returns stable human and JSON output, including the final `res://` path.
- Representative generated PNG and sanitized SVG assets import through headless Godot tests in the existing Godot-aware test layer.
- `./scripts/fix.sh` and `./scripts/verify.sh` pass without changing tracked files.

## Definition of done for an implementation PR

An implementation PR is not complete merely because a provider returns an image. It must include:

- code and tests for the phase's complete vertical slice;
- ADR 0003 dependency-admission evidence for every newly introduced or materially expanded package;
- centrally pinned dependencies and lockfile updates;
- tracked configuration and schemas;
- fixture-based provider responses with no live secrets;
- negative and failure-path coverage;
- canonical verifier integration where applicable;
- relevant repository agent and operator documentation updates;
- no committed `.assetctl/` runtime state;
- verification evidence from `./scripts/fix.sh` and `./scripts/verify.sh`;
- an acceptance-criteria trace to the applicable items in this specification.

## Deferred extensions

The following are explicitly deferred and do not block the first complete implementation:

- audio, music, voice, video, animation, 3D models, and fonts;
- a Godot editor UI;
- a contact-sheet web interface;
- external provider plugin assemblies;
- a shared cross-repository asset service;
- a persistent catalog database;
- local ML-based similarity or quality scoring;
- automatic duplicate detection through embeddings;
- automatic provider benchmark runs;
- asset-marketplace search and license ingestion;
- content-addressed revision storage;
- automatic approved-asset replacement;
- remote reference URL ingestion;
- provider billing or credit management.

These extensions should be considered only when a concrete near-term consumer makes them preferable to the current simple design.

## Known uncertainties

Provider models, prices, limits, endpoints, and terms are unstable external facts. This specification deliberately makes them dated configuration rather than architectural constants. The implementation agent must verify current official documentation when writing each adapter and seed profile.

AI semantic review is probabilistic. Mechanical validation and human approval remain independent controls. Review scores are useful ranking evidence, not truth.

Tool-side daily spending state is a best-effort local guardrail and can be bypassed by another machine or direct API use. Provider-side prepaid credit and spend controls remain the stronger financial boundary.

The exact raster and SVG libraries are intentionally not frozen in this specification because their versions, Linux packaging, and licenses must be checked at implementation time. The required behavior and test evidence are frozen.

## Sources

Repository decisions and policy:

- [ADR 0001: Separate simulation from Godot](../adr/0001-separate-simulation-from-godot.md)
- [ADR 0002: Use one canonical quality gate](../adr/0002-use-one-canonical-quality-gate.md)
- [ADR 0003: Prefer native capabilities and demand-driven dependencies](../adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md)
- [ADR 0005: Use JSON and schema validation for domain content](../adr/0005-use-json-and-schema-validation-for-domain-content.md)
- [ADR 0008: Use structured observability with Serilog](../adr/0008-use-structured-observability-with-serilog.md)
- [ADR 0009: Use layered testing and architecture conformance](../adr/0009-use-layered-testing-and-architecture-conformance.md)
- [Development quality](../development-quality.md)
- [Repository .NET SDK pin](../../global.json)
- [Repository licensing policy](../../LICENSE.md)
- [Repository legal notice](../../LEGAL.md)

Current provider documentation reviewed on 2026-09-01:

- [OpenAI GPT-Image-2 model](https://developers.openai.com/api/docs/models/gpt-image-2)
- [OpenAI ChatGPT and API billing separation](https://help.openai.com/en/articles/9039756-managing-billing-settings-on-chatgpt-web-and-platform)
- [Recraft API getting started](https://www.recraft.ai/docs/api-reference/getting-started)
- [Recraft API examples](https://www.recraft.ai/docs/api-reference/examples)
- [Recraft API pricing](https://www.recraft.ai/docs/api-reference/pricing)
- [Recraft API and Studio credit distinction](https://www.recraft.ai/docs/plans-and-billing/credits)
- [xAI Imagine API overview](https://docs.x.ai/developers/model-capabilities/imagine)
- [xAI image generation](https://docs.x.ai/developers/model-capabilities/images/generation)
- [xAI models and pricing](https://docs.x.ai/developers/models)
- [xAI Grok and API billing separation](https://docs.x.ai/console/faq/accounts)
