---
schema_version: '1.1'
id: 'adr-0017-star-trek-alter-course-generate-assets-outside-the-game-through-bounded-validated-publication'
title: 'ADR 0017: Generate Assets Outside the Game Through Bounded Validated Publication'
description: 'Defines AssetCtl isolation, external-operation limits, validation, recoverable publication, provenance, and owner-controlled approval.'
doc_type: 'adr'
status: 'active'
created: '2026-09-27'
updated: '2026-09-27'
reviewed: '2026-09-27'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'development'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0002-use-one-canonical-quality-gate.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0008-use-structured-observability-with-serilog.md'
  - 'docs/adr/0009-use-layered-testing-and-architecture-conformance.md'
  - 'docs/wiki/asset-pipeline-tool.md'
  - 'docs/wiki/content-assets-and-persistence.md'
  - 'docs/development-quality.md'
  - 'docs/dependency-admission/assetctl.md'
supersedes: []
superseded_by: null
source: []
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

# Generate assets outside the game through bounded validated publication

## Context and Problem Statement

Development agents need usable visual assets without embedding provider-specific scripts in gameplay or blocking ordinary implementation on external generation. Provider output is untrusted, calls may spend money, selected bytes and provenance must agree, and automated quality review cannot grant owner approval.

The existing AssetCtl specification already defines a standalone configuration-driven tool and a substantial publication/lifecycle boundary. This record owns the durable architectural choice and its alternatives. The specification remains the detailed tool contract; the wiki retains gameplay-facing content policy; package admissions remain separate evidence under ADR 0003.

This decision applies to repository visual-asset discovery, generation, review, selection, publication, and approval through AssetCtl. It does not govern Core content authoring, simulation AI, narrative, a runtime asset service, arbitrary media generation, or legal clearance.

How should agents obtain visual assets while isolating game runtime, external side effects, selected-file publication, and human approval?

## Decision Drivers

- Playing, building, and verifying the game must not require live image providers.
- Untrusted output must be validated before it enters the selected asset tree.
- Provider/model configuration should change independently of orchestration code when the protocol is already supported.
- Spending and retries require explicit finite policy.
- Selected bytes, provenance, lifecycle, and approval must remain attributable and recoverable.
- Existing project-native tools are preferable to a service or second application stack.

## Considered Options

- Use a standalone .NET CLI exchanging selected assets and manifests through files.
- Call generation providers directly from gameplay or a required Godot editor integration.
- Maintain ad hoc agent-specific provider scripts and manually copy their outputs.
- Operate a persistent asset-management service and database.

## Decision Outcome

Chosen option: "Use a standalone .NET CLI exchanging selected assets and manifests through files", because it isolates development side effects without making game runtime or ordinary verification depend on a network service.

### Assembly and runtime isolation

`AlterCourse.AssetCtl` references neither `AlterCourse.Core` nor `AlterCourse.Godot`; neither game project references AssetCtl. The game consumes selected local files through ordinary Godot resource paths. It does not call providers or depend on the tool being running.

The tool uses the repository's C#/.NET stack and canonical quality path. The current tool targets .NET 10 while the Godot-facing projects retain their own compatibility target. Exact versions belong in project configuration and the development runbook, not an independent version policy in this ADR.

Tracked YAML describes development/presentation metadata: requests, provider instances, capability profiles, routes, style/quality choices, and selected manifests. It is not an alternate source of authoritative ship, faction, or system rules under ADR 0005.

### Configuration and external-operation authority

Routing selects among declared capabilities and configured policies. A supported protocol is implemented by a focused adapter; adding a model or account for that protocol is configuration work when the adapter supports the required contract. New protocols or capabilities require explicit implementation and tests, not an unrestricted YAML programming language or dynamic plugin framework.

External operations, candidate counts, retries, response sizes, decoded output, and spend are bounded. A failure must not silently authorize more expensive routing, weaker validation, or an increased budget. Committed defaults deny external generation and spending; an explicitly authorized local override is separate from ordinary tracked policy.

Eligible routine development retains a deterministic endpoint-free placeholder fallback. Offline validation and local placeholder generation do not require provider credentials. A placeholder is not a claim of production approval or successful external semantic review.

Credential values enter only through the caller-owned credential/environment boundary. Tracked configuration identifies environment-variable names, not secrets. Requests, manifests, receipts, diagnostics, and command output must preserve the existing redaction and credential-exclusion policy.

### Validation, provenance, and lifecycle

Search the selected catalog before generating a duplicate. Validate output bytes independently of provider claims, including the applicable format, size, dimensions, safety, and requested output contract. Optional semantic review supplies structured quality evidence; a high aggregate score cannot override a mandatory hard failure.

Selected artifacts retain stable semantic identity, integrity information, request/configuration provenance, applicable validation/review evidence, and rights classification. Working candidates, receipts, locks, logs, and local overrides remain ignored working state rather than a second source of repository truth.

Generation capability, publication, and owner approval are separate authorities. Agents may perform permitted placeholder/candidate work within policy; promotion to approved and deprecation of an approved asset require explicit current owner instruction and the tool's confirmations. Automated review cannot establish that instruction or prove legal clearance.

Approved files are immutable under the current lifecycle contract. Replacement uses a new semantic asset ID and an explicit supersession relationship rather than silently overwriting approved bytes. Owner instruction is necessary for controlled lifecycle transitions; it is not permission to bypass validation, provenance, or the supported replacement mechanism.

### Recoverable paired publication

A selected asset and its manifest form one publication unit. Stage and validate their intended contents, serialize conflicting mutation, and preserve recoverable evidence of interrupted publication. Recovery must distinguish a live transaction from an abandoned one and must not invent a complete pair from unrelated files.

The implementation uses staging, journals, leases, ownership checks, and rollback/recovery. This is recoverable paired publication, not a claim that two separate filesystem entries change atomically in one primitive or that every storage device survives sudden power loss.

Path safety must bind validation to the actual objects used during mutation, not only to an earlier string comparison. The current descriptor-bound secure state/publication implementation requires Linux. A portability change must provide equivalent safety and recovery evidence; do not replace those checks with a weaker path-only implementation merely to make another platform run.

### Verification and diagnostics

Canonical verification builds/tests the tool and validates tracked configuration/catalog data offline without generation, credentials, or paid APIs. Provider-call tests use controlled fixtures or substitutes. Optional live probes are separately authorized operations, not a hidden prerequisite to passing the gate.

Operational diagnostics remain nonauthoritative under ADR 0008 and separate from provenance receipts. A broken logging sink must not change route selection, spending authority, or publication success semantics.

### Consequences

- Good, because runtime and canonical verification remain independent of hosted generation.
- Good, because provenance, integrity, and approval have explicit boundaries.
- Good, because a focused adapter can change without replacing routing or publication.
- Bad, because manifests, local policy, and recovery state require maintenance.
- Bad, because equivalent secure publication on another platform needs deliberate implementation rather than assuming .NET portability alone is sufficient.

### Confirmation

Apply this record when changing project references, routing, credentials, spending, output admission, lifecycle, or publication/recovery. Confirm assembly isolation, offline/read-only verification, bounded failure/retry paths, approved-asset refusal, malformed output rejection, path/ownership safety, concurrent mutation, and interruption recovery. Check that provenance describes selected bytes and that semantic review cannot bypass mandatory gates.

Evidence entry points include [composition](../../tools/AlterCourse.AssetCtl/Program.cs), [generation](../../tools/AlterCourse.AssetCtl/Generation/GenerationOrchestrator.cs), [publication](../../tools/AlterCourse.AssetCtl/Publishing/PublishingTypes.cs), [tool tests](../../tests/AlterCourse.AssetCtl.Tests/), and [canonical verification](../../scripts/verify.sh). Existing provider adapters do not establish live-provider availability or a fresh paid-call test.

## Pros and Cons of the Options

### Standalone CLI and file contracts

- Good, because it reuses the existing toolchain and keeps the game offline-capable.
- Bad, because selected-file lifecycle and recovery are explicit responsibilities.

### Runtime or required editor provider integration

- Good, because generation could be close to asset consumption.
- Bad, because it couples gameplay/editor availability to credentials, network behavior, and external side effects.

### Ad hoc provider scripts

- Good, because the first request can be implemented quickly.
- Bad, because safety, spending, provenance, and approval become inconsistent across agents.

### Persistent asset service

- Good, because it could coordinate a future shared asset operation.
- Bad, because the present repository has no consumer that justifies its deployment, database, or service-maintenance cost.

## More Information

This record formalizes D-06 and the architecture in the [full AssetCtl contract](../wiki/asset-pipeline-tool.md), inspected at `efd4ac93894fc28ea0c3ea40ca1be008dd4d6631`. The owner selected it after the September 27, 2026 review. CLI flags, schemas, provider choices, prices, and detailed recovery steps remain in their existing owning sources. No new service, dependency, media type, paid operation, or asset approval is authorized by adoption.
