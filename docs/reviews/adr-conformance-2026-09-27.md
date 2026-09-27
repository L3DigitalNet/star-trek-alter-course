---
schema_version: '1.1'
id: 'reference-xjdwtp-adr-conformance-2026-09-27'
title: 'ADR Conformance — September 27, 2026'
description: 'Baseline findings, scoped ADR obligations, remediation and verification evidence for Issue 127.'
doc_type: 'reference'
status: 'draft'
created: '2026-09-27'
updated: '2026-09-27'
tags: []
aliases: []
related: []
---

# ADR conformance record

## Scope and evidence rules

This is an evidence register for [Issue #127](https://github.com/L3DigitalNet/star-trek-alter-course/issues/127), not a gameplay specification. [Active ADRs](../adr/) own architecture; the [wiki](../wiki/README.md) owns gameplay; source and executed tests establish actual behavior. The [documentation authority map](../README.md) governs those roles.

Fetched `origin/dev` baseline: `2ef6c77aa4587719aac0b59cfda3d6ee7c560e5a`, identical to the supplied audit baseline. Working tree was clean; no open issues or PRs preceded this work. The separately named audit attachment was not required or assumed present. Fourteen active ADRs are in scope. Ship content V6, system-definition content V1 and save V10 are implemented and unreleased.

Statuses: **compliant-with-evidence**, **noncompliant**, **ambiguous/pending-decision**, **deferred-by-explicit-trigger**, **not-applicable**, **unverified**. Inspected tests are not executed proof. Historical results do not verify this candidate. Baseline findings remain even when an ADR is clarified. Accepted risk, if any, remains separately visible; this audit cannot accept it for the owner.

## Starting findings, before remediation

| ID | Baseline finding and impact | Confidence and disposition | Authority | Final disposition |
| --- | --- | --- | --- | --- |
| F01 | `GameScreen.LogDiagnostic` interpolates raw exception messages into `GD.PrintErr`; no game composition logging pipeline. Sensitive-path leakage awaits synthetic reproduction. ADR 0008 also conflicts internally on original exceptions versus sensitive output. | High source confidence; implement selected structured pipeline and rendered/structured redaction and failure-isolation proof. | ADR 0008; owner explicitly authorizes correction and bounded clarification. | Unverified; no correction claimed. |
| F02 | ADR 0013 admits low-risk Standalone work but topic branch syntax requires an issue number. | High; reconcile `standalone/<slug>` with package-owned eligibility and complete-route proof. | ADR 0013 and installed github-workflow package. | Unverified; no correction claimed. |
| F03 | Custom architecture rules exist beyond project references; ArchUnitNET and CsCheck are absent. | High; evaluate current triggers and selected test-only dependencies; preserve useful specialized probes. | ADRs 0003 and 0009. | Unverified; no correction claimed. |
| F04 | ADR 0011 Confirmation says typed or named, weaker than runtime-boundary strong typing. | High textual confidence; clarify, inspect consequential APIs and preserve numeric wire semantics. | ADR 0011 and owner bounded clarification. | Unverified; no blanket primitive violation inferred. |
| F05 | No authoritative random consumer found in initial runtime inspection; envelope wording is unconditional. | Medium until call-path trace completes; clarify explicit no-state case and future admission gate. | ADRs 0006/0007; no dummy RNG authorized. | Unverified; no algorithm/save-version change. |
| F06 | Available-ref history search found UnitsNet references but no suitability evaluation. | Medium; present-day evaluation required; this is not proof no off-repository evaluation ever existed. | ADRs 0003/0011; policy alternatives require owner decision. | Unverified; current sound types retained pending evaluation. |

## Obligation register

Clause extraction is in progress before remediation. Each `ADR-NNNN-Rnn` row will record exact source section, obligation/trigger, amendments and rationale, implementation and proof, baseline disposition/impact/confidence, authority, final disposition/change, verified revision/command/result, and remaining limitations. Future admission triggers and permitted exceptions will remain distinct from active defects.

## Verification receipts

Baseline `rexec -- ./scripts/verify.sh` ran from an isolated clean `dev` worktree at `2ef6c77aa4587719aac0b59cfda3d6ee7c560e5a` and exited 0. Godot reported 86 gameplay, one integration and two asset-import cases with no failures/skips; smoke passed. The initial primary-checkout attempt stopped on the orchestrator's unsupported `chore/` branch prefix, which was corrected to `task/`; an interrupted redundant attempt returned rexec control exit 21, not a repository test failure. Neither attempt substitutes for the isolated passing baseline. `rexec config show` confirms sanitized Git context enabled; `rexec doctor` exited 0. A passing baseline gate does not certify every obligation.

## Review and delivery

Independent bounded native reviews cover ADR clauses, persistence safety, runtime determinism/information boundaries, units/history, and workflow/test-tool admission. Claude cross-agent preflight passed with verified saved-login and read-only containment; no peer review result exists yet. No merge, tag, release or administrative change is authorized.

The last full wiki semantic sweep remains September 26, with October 3 due date. This ADR effort does not reset it without completing the wiki's full procedure.

## Register conventions and authority

For every row below, initial **baseline status = UNVERIFIED**, **final status = UNVERIFIED**, and **implementation evidence = not inspected by this extraction**. A documented current-state claim is evidence of declared scope, not proof of implementation.

- **Current:** obligation applies to an implemented consumer or existing architecture.
- **Triggered:** obligation applies when the stated consumer/change occurs; absence alone is not a defect.
- **Exception:** bounded permission or exclusion, evaluated only within its stated scope.
- **Context:** explanation, candidate, example, rejected alternative, or historical procedure; does not independently require implementation.
- A confirmation clause supplies required acceptance evidence for an in-scope change. It does not universally require every listed test for nonexistent systems.
- Wiki governs detailed game design; ADRs govern architecture. Source/tests establish actual implementation. Document `status: active` does not establish feature completion.
- ADR 0012 explicitly amends ADR 0005 only for admitted specialized narrative content. No ADR is marked superseded; ADR 0005’s `amended_by` and ADR 0012’s `amends` are reciprocal.
- Full semantic sweep due date remains **2026-10-03**. This extraction is an ADR/authority review, not a completed sweep of every indexed gameplay topic.

### ADR 0001

Source: `docs/adr/0001-separate-simulation-from-godot.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0001-R01 | Context and Problem Statement; Decision Outcome | Current | New runtime C# and project references preserve one-way Godot→Core dependency; Core never references Godot. |
| ADR-0001-R02 | Decision Outcome | Current | Pure simulation tests target Core directly and run without starting Godot. |
| ADR-0001-R03 | Decision Outcome | Current | Engine-facing integration is tested separately with Godot-aware tests. |
| ADR-0001-R04 | Consequences | Current | Data crossing the boundary does not expose Godot objects to Core. |
| ADR-0001-R05 | Confirmation | Current | Solution reference graph, Core architecture smoke test, and canonical verifier mechanically reject Godot references entering Core before merge. |

Context: the ADR does not prescribe game-system design or speculative layers. Ambient time/randomness motivation is elaborated by ADR 0007.

### ADR 0002

Source: `docs/adr/0002-use-one-canonical-quality-gate.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0002-R01 | Decision Outcome | Current | `./scripts/verify.sh` owns the ordered canonical local/CI verification contract. |
| ADR-0002-R02 | Decision Drivers; Decision Outcome | Current | Compiler/analyzer warnings fail builds; modern SDK analyzers, curated Meziantou rules, and banned APIs constitute the baseline. |
| ADR-0002-R03 | Decision Outcome | Current | CSharpier is the sole C# formatter. |
| ADR-0002-R04 | Decision Drivers; Decision Outcome | Current | Tool versions are pinned independently of workstation-global installations; suppression exceptions use a narrow explicit allowlist. |
| ADR-0002-R05 | Confirmation | Current | Canonical CI invokes only the verifier after checkout and .NET setup rather than duplicating its checks. |
| ADR-0002-R06 | Confirmation; Consequences | Current | Verifier records tracked state before/after, fails if it changes tracked files, and does not continue silently after a failed check. |
| ADR-0002-R07 | Context; Decision Outcome | Exception | Stryker is separate deep validation until an evidence-based score policy exists; this ADR does not impose a mutation-score threshold. |
| ADR-0002-R08 | Context | Exception | Canonical verification complements rather than replaces Project Standards’ managed Markdown workflows. |

Context: checksum-pinned first-run downloads and Linux portability are operational properties requiring implementation inspection, not authority to change the toolchain.

### ADR 0003

Source: `docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0003-R01 | Context; Decision Outcome | Triggered | New/materially expanded runtime or development libraries, addons, GDExtensions, source dependencies, and framework-like abstractions receive dependency admission review. |
| ADR-0003-R02 | Capability ownership order | Triggered | Admission evaluates the appropriate Core/Godot native-first ownership order; lower-ranked choices require concrete comparative evidence. |
| ADR-0003-R03 | Native Godot defaults | Current | Presentation uses native Godot UI, 2D rendering, input, audio, localization, and asset facilities where their concrete use cases apply. |
| ADR-0003-R04 | Native Godot defaults | Current | Godot noise does not determine authoritative world generation, hazards, constraints, sensors, combat, or AI; consequential generation belongs in headless Core with ADR 0007 randomness. |
| ADR-0003-R05 | Dependency admission evidence | Triggered | Record concrete consumer/requirement, inadequacy of higher-ranked options, maintenance/compatibility, license/distribution, transitive/native dependencies, relevant exports, determinism/headless behavior, unavailability failure, API coupling, and replacement boundary. |
| ADR-0003-R06 | Version and source control | Current | NuGet versions are centrally owned in `Directory.Packages.props`; committed lock files and canonical locked restore prevent floating resolution. |
| ADR-0003-R07 | Version and source control | Triggered | Addons/source dependencies use immutable releases/commits; vendoring has a specific reviewed justification. |
| ADR-0003-R08 | Frameworks and infrastructure excluded from the baseline | Triggered | Databases, ECS, DI/event-bus/network/backend frameworks, general scripting/rules/game frameworks, and strategic behavior trees need a concrete near-term consumer and new/amended ADR; hypothetical convenience is insufficient. |
| ADR-0003-R09 | Confirmation | Triggered | Admission evidence, immutable references, licensing, lock/version ownership, and canonical verification substantiate dependency conformance; Core additions preserve ADR 0001. |
| ADR-0003-R10 | More Information | Current | Pure managed code prefers ordinary .NET collections unless crossing a Godot API boundary. |

Exceptions: ordinary assets, supported-toolchain OS packages, and dependencies mandated by Godot/.NET SDK fall outside this admission scope. Recognized Better Terrain, DelaunatorSharp, Clipper2, NetTopologySuite, Gaea, and GodotEnv candidates are not installation requirements.

### ADR 0004

Source: `docs/adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0004-R01 | Authoritative ownership | Current | Core owns persistent spatial identities, relationships, frames, positions/motion, topology/costs, jurisdiction/hazards, contacts/knowledge, and movement transitions. |
| ADR-0004-R02 | Authoritative ownership | Current | Godot owns cameras, display geometry, interaction, scenes, conversion, and engine-local navigation; Core does not store Godot objects. |
| ADR-0004-R03 | Scale model | Current | Implemented identities/persistence permit differing spatial scales without assuming every location is a tile, adjacent grid cell, or pixel. |
| ADR-0004-R04 | Strategic space | Triggered | Strategic routefinding and consequential traversal costs operate headlessly in Core, accounting for domain state and actor knowledge where the rule uses them. |
| ADR-0004-R05 | Tactical space | Current | Tactical truth uses continuous 2D domain position/motion; displayed grids and interpolation do not become authority. |
| ADR-0004-R06 | Godot pathfinding and navigation | Triggered | Engine-local pathfinding outputs cross through validated Core commands before changing authoritative state. |
| ADR-0004-R07 | Geometry libraries | Triggered | Consequential geometry is Core data/deterministic derivation; decorative geometry stays presentation-only; package admission follows ADR 0003. |
| ADR-0004-R08 | Sensor knowledge and map views | Current | Actor views distinguish world truth from confirmed/stale/estimated information; player/AI receive actor-appropriate projections. |
| ADR-0004-R09 | Confirmation | Triggered | Relevant spatial changes have headless decisions, domain-only saves, stable route ties, truth/knowledge tests, and adapter projection/selection/command tests. |

Exceptions/context: conceptual quadrant/sector/system/local/tactical scales are architectural capacity, not a requirement to implement all now. Tactical acceleration, shield facings, and richer geometry are representational capability/future design, not completed gameplay. Local tile maps are permitted. No geometry package is adopted. Current wiki explicitly declares full strategic pathfinding absent.

### ADR 0005

Source: `docs/adr/0005-use-json-and-schema-validation-for-domain-content.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0005-R01 | Canonical format and parser | Current | Ordinary reusable Core definitions use UTF-8 JSON and explicit `System.Text.Json` options. |
| ADR-0005-R02 | Canonical format and parser | Current | Parsing rejects malformed input, rejects/reports unknown members except declared versioned extensions, documents casing, avoids unsafe polymorphism/type metadata, and emits file/record/path diagnostics. |
| ADR-0005-R03 | Canonical format and parser | Current | Untrusted string/collection allocations are bounded; compatibility of reflection-dependent behavior is testable. |
| ADR-0005-R04 | Specialized canonical formats | Triggered | Another canonical format requires explicit ADR amendment specifying domain justification, pipeline, stable IDs, determinism, versions, typed boundaries, headless behavior, persistence, and subordinate authority. |
| ADR-0005-R05 | Schema validation | Current | Introduced production schemas use selected JsonSchema.Net; schemas are versioned, gate-validated, and match each content family’s declared/selected version. |
| ADR-0005-R06 | Semantic validation pipeline | Current | Declared roots/families → strict parse → schema → explicit input models → documented normalization → unique IDs → typed reference resolution → semantic invariants → controlled definitions → deterministic catalog/content identity. |
| ADR-0005-R07 | Semantic validation pipeline | Current | Unsafe/contradictory content fails closed; errors accumulate when safe; warnings cannot admit unknown semantics. |
| ADR-0005-R08 | Stable identity and references | Current | Referenceable definitions have documented case-sensitive stable machine IDs independent of display/file names, unique within appropriate namespaces, validated before exposure. |
| ADR-0005-R09 | Stable identity and references | Current | Reference resolution rejects missing, ambiguous, or wrong-family targets. |
| ADR-0005-R10 | Content versions and migrations | Current | Shape/semantic compatibility versions are explicit where required; rules changes are documented/tested even without shape changes; supported save IDs retain meaning. |
| ADR-0005-R11 | Godot resources and presentation data | Current | Resources remain presentation records; Core definitions load independently; missing visuals produce controlled presentation fallback/error. |
| ADR-0005-R12 | YAML and other text formats | Current | Ordinary ship/faction/weapon/topology content does not silently become YAML or dual JSON/YAML authority. |
| ADR-0005-R13 | Generated and imported content | Triggered | Generated/imported content undergoes equivalent validation and legal admission; generated ownership is committed/reviewed or reproducibly built/excluded, never partially hand-edited. |
| ADR-0005-R14 | Modding boundary | Triggered | Concrete modding needs a separate trust, packaging, override, dependency, and compatibility decision. |
| ADR-0005-R15 | Confirmation | Current | Content evidence covers strict schemas, stable registration, typed references, semantic invariants, unknown/duplicate/missing/bounds/version failures, and required headless operation. |

Exceptions: saves are ADR 0006; tool configuration may use justified native formats; bounded tool YAML is admissible under ADR 0003; CSV can be import/translation input. ADR 0012 is the narrow narrative exception. Source generation and custom editors remain demand-driven.

### ADR 0006

Source: `docs/adr/0006-use-versioned-json-snapshot-saves.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0006-R01 | Snapshot boundary | Current | Explicit Core-owned persistence models and explicit mapping represent durable meaning separately from runtime entities, Godot, UI, services, tasks/delegates, caches, and command/interpolation buffers. |
| ADR-0006-R02 | Canonical representation | Current | Saves use UTF-8 JSON/System.Text.Json with explicit tested configuration and no arbitrary runtime-type instantiation. |
| ADR-0006-R03 | Save envelope | Current | Envelope establishes schema, stable save ID, organization timestamps, simulation time, rules/build compatibility, content identity/references, scheduler continuation, and authoritative snapshot before construction. |
| ADR-0006-R04 | Save envelope; ADR 0007 Persisted random state | Triggered | Once authoritative RNG exists, save algorithm/version and full continuation state; absence of a real RNG consumer requires explicit scoped interpretation, not fabricated RNG fields. |
| ADR-0006-R05 | Persisted and derived state | Current | Persist mutable/irrecoverable/consequential/order-affecting state; omit safely reconstructible derivations; document/invalidate any intentionally persisted expensive cache. |
| ADR-0006-R06 | Migration policy | Current | Bound/read envelope, reject future versions, apply tested ordered migrations, validate and resolve references before runtime construction. |
| ADR-0006-R07 | Migration policy | Current | Migrations act on persistence representations, declare source/target, compose adjacent steps, and do not silently invent consequential state. |
| ADR-0006-R08 | Migration policy | Triggered | Removing supported development-save compatibility is explicit and documented; no perpetual compatibility is implied. |
| ADR-0006-R09 | Validation and trust boundary | Current | Validate schema/algorithm versions, bounds, unique identities/references, finite ranges/enums/states, scheduler order/targets, applicable random shapes, cross-object invariants, and content compatibility. |
| ADR-0006-R10 | Validation and trust boundary | Current | Unknown members fail unless explicitly permitted; failed loads do not partially mutate the active world. |
| ADR-0006-R11 | Atomic save writes and recovery | Current | Validate candidate, write same-filesystem temporary, flush/close per supported policy, retain configured known-good copy, replace/rename safely, and surface failure without deleting valid predecessor. |
| ADR-0006-R12 | Database exclusion; Event-sourcing exclusion | Current | In-memory Core remains sole live authority; snapshots are not a database or reconstruction from unbounded diagnostic events. |
| ADR-0006-R13 | Confirmation | Current | Semantic round trips, every supported migration fixture, corrupt/truncated/unknown/reference/oversize failures, continuation, model-boundary checks, and lock-step schema/migration changes establish persistence conformance. |
| ADR-0006-R14 | Confirmation; More Information | Current | Supported write durability guarantees are stated/tested; interruption/recovery tests apply where platform abstraction permits them; universal atomicity is not claimed. |

Exceptions/context: optional integrity/summary metadata is optional; compact JSON/compression may follow measured benefit. Binary storage needs measured inadequacy of JSON alternatives and migration/diagnostic strategy. Autosave retention, cloud conflicts, and wider compatibility are unresolved product policy.

### ADR 0007

Source: `docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0007-R01 | Authoritative simulation time | Current | Monotonic explicit Core time/duration advances only through explicit operations; wall clock, frame delay, pause, closed-process time, and save timestamps do not alter outcomes. |
| ADR-0007-R02 | Multiple time scales | Current | Use coarsest faithful subsystem resolution/event boundaries; continuous integration has explicit deterministic formula/rounding and boundary tests. |
| ADR-0007-R03 | Deterministic scheduler | Current | Serializable data identifies due time, stable total tie order, target/kind, payload, load validity, and diagnostics; no serialized delegates. |
| ADR-0007-R04 | Deterministic scheduler | Current | Cancellation/replacement has stable identities/ownership; actor removal cannot leave retargeting/unvalidated callbacks. |
| ADR-0007-R05 | Advancement semantics | Current | Validate, advance boundaries, resolve due work in total order, apply/schedule consequences, and return outcomes/final time through explicit stopping semantics. |
| ADR-0007-R06 | Advancement semantics | Current | Current-time follow-on order is defined; zero-time cycles are bounded; failed advancement cannot masquerade as success with unsafe partial state. |
| ADR-0007-R07 | Mutation and concurrency | Current | Authoritative mutations are serialized deterministically; async/engine callbacks submit ordered explicit input rather than directly mutating entities. |
| ADR-0007-R08 | Mutation and concurrency | Triggered | Background calculations use immutable snapshots and deterministic validated commit; mutation parallelization requires profiling rather than speculation. |
| ADR-0007-R09 | Random-source abstraction | Triggered | Authoritative random consumers receive project-owned injected abstraction with explicit endpoint/bias semantics and required state/algorithm/stream operations. |
| ADR-0007-R10 | Random-source abstraction | Triggered | Fixed documented algorithm/version is selected before saves depend on it, with reference vectors/statistical suitability; System.Random/Random.Shared is not persisted simulation contract. |
| ADR-0007-R11 | Random streams | Triggered | Any stream derivation/ownership uses documented deterministic functions and stable IDs; runtime hashes, addresses, iteration order, and localized names are forbidden seeds/identities. |
| ADR-0007-R12 | Persisted random state | Triggered | Persist all future-affecting stream state/metadata; seed-only continuation is insufficient; unsupported algorithm versions fail load. |
| ADR-0007-R13 | Determinism contract | Current | Equal compatible snapshot/content/ordered commands/scheduler/configuration and applicable RNG state reproduce semantic outcomes; avoid culture/order/tie/race nondeterminism. |
| ADR-0007-R14 | Time abstraction outside simulation time | Triggered | Test-controlled legitimate application wall time uses injectable abstraction, preferably TimeProvider; this never replaces Core time. |
| ADR-0007-R15 | Confirmation | Current | Time/scheduler evidence covers banned ambient APIs, same-time order, cancel/reschedule, loop guards, continuation, insertion-order variation, and frame/wall-clock independence. |
| ADR-0007-R16 | Confirmation | Triggered | Admitted RNG has reference vectors, stream save/restore, replay, and reproducible failure context; non-stochastic tests should identify absence rather than invent seeds. |

Exceptions/context: System.Random remains permissible in non-simulation tools/tests without sequence compatibility; crypto tokens use separate platform APIs. Named stream scopes, PCG/xoshiro, and specific random operations are candidates/examples. No cross-platform byte equality, networking lockstep, or permanent replay across arbitrary rules versions is promised.

### ADR 0008

Source: `docs/adr/0008-use-structured-observability-with-serilog.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0008-R01 | Logging ownership and dependency direction | Current | Composition root configures Serilog→Microsoft logging; logging services receive constructed ILogger, with no domain static logger/service locator or Godot logging dependency. |
| ADR-0008-R02 | Logging ownership and dependency direction | Current | Required gameplay/test/AI explanations are typed results, not log-only facts; specialized trace sinks adapt the pipeline rather than form another logging framework. |
| ADR-0008-R03 | Structured event model | Current | Consequential diagnostics use stable message templates/named fields and appropriate stable IDs/codes rather than prose/display/localized text alone. |
| ADR-0008-R04 | Simulation time and wall-clock time | Current | Simulation-affecting diagnostics carry available Core time; emission timestamps/performance timing are distinct and cannot inform rules. |
| ADR-0008-R05 | Levels and expected use | Current | Severity matches lifecycle/abnormality/failure; ordinary negative gameplay, valid optional absence, and rejected candidates are not automatically errors/warnings. |
| ADR-0008-R06 | AI and causal traces | Current | Decision diagnostics explain actor-known input, candidates, constraints, choice, ties, command, and applicable randomness; omniscient detail is explicitly distinguished. |
| ADR-0008-R07 | Sinks and configuration | Current | Development supports human console, structured rolling file, and test capture; file growth/retention and verbosity have bounded defaults. |
| ADR-0008-R08 | Sinks and configuration | Triggered | Additional/remote sinks receive ADR 0003 admission and explicit privacy/operational purpose; offline gameplay remains complete. |
| ADR-0008-R09 | Sinks and configuration | Current | Sink failure is isolated from rules/transactions and cannot leave partially applied Core state. |
| ADR-0008-R10 | Sensitive and high-volume data | Current | No credentials/environment dumps/unnecessary home paths/default full saves/assets/research text/personal data; redact or use bounded identifiers. |
| ADR-0008-R11 | Sensitive and high-volume data | Current | High-volume traces are disabled/sampled explicitly and never change random consumption, event order, or simulation result. |
| ADR-0008-R12 | Logs are not authority | Current | Logs do not reconstruct saves, prove commits, drive mutations, or become compatibility requirements; durable player history is Core state. |
| ADR-0008-R13 | Error reporting | Current | Recovery boundary logs original unexpected exceptions once with context; structured results represent expected failures; classifications distinguish input/version/I/O/invariant/defect/cancellation/game outcome. |
| ADR-0008-R14 | Confirmation | Current | Evidence covers composition dependencies, no-op/collecting/failing equivalence, important event fields, template usage, bounded retention, redaction, reproducible long-run failures, and durable history ownership. |

Context: enumerated fields depend on event type; the ADR does not require every event to contain every candidate field or every domain method to log. Player histories, telemetry, replay schemas, and upload policy need separate decisions.

### ADR 0009

Source: `docs/adr/0009-use-layered-testing-and-architecture-conformance.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0009-R01 | Test placement principle | Current | Tests run at the lowest faithful layer: Core rules/scenarios/content/saves/AI; engine-specific behavior in Godot; small wiring E2E complements lower layers. |
| ADR-0009-R02 | xUnit baseline | Current | Ordinary .NET tests use xUnit and meaningful behavioral seams; bounded named theories do not replace meaningful generated spaces. |
| ADR-0009-R03 | State-transition and subsystem tests | Current | Applicable preconditions, accepted/rejected commands, outcomes, work, bounds/degradation/interruption/identity/order/idempotence/restoration/downstream effects receive observable invariant tests. |
| ADR-0009-R04 | Scenario and long-running simulation tests | Current | Cross-system/scenario and representative horizon coverage checks consequences/global invariants; failures retain scenario/content, command fixture, time/event, diagnostics, invariant, and applicable RNG context. |
| ADR-0009-R05 | Property, model, and metamorphic testing | Triggered | Meaningful generator/invariant admits selected CsCheck; independent model/metamorphic properties avoid tautologies and report seed/minimized counterexample. |
| ADR-0009-R06 | Property, model, and metamorphic testing | Triggered | Significant generated defect becomes a named regression where that improves clarity. |
| ADR-0009-R07 | Godot integration testing | Triggered | C# tests actually requiring Godot use selected GdUnit4Net backed by GdUnit4; tests assert wiring/engine behavior rather than duplicate Core rules. |
| ADR-0009-R08 | Godot integration testing | Exception | Naturally GDScript addon/engine fixtures are allowed; new production gameplay remains C# absent separate decision. |
| ADR-0009-R09 | Architecture conformance | Current | Project graph is first boundary; durable relationships have compile-time/mechanical enforcement rather than review alone. |
| ADR-0009-R10 | Architecture conformance | Triggered | First durable rule not fully expressible in project graph admits selected ArchUnitNET into normal tests; transient folder preferences are not architecture rules. |
| ADR-0009-R11 | Test doubles and clocks | Current | Prefer real values/in-memory genuine-boundary doubles; clocks/randomness are explicit, not global patches or mock call-sequence replicas. |
| ADR-0009-R12 | Persistence and content fixtures | Current | Supported-version fixtures have identified compatibility purpose; semantic equality is default, exact JSON only for wire contract; trust-boundary negatives are maintained. |
| ADR-0009-R13 | Determinism and isolation | Current | Tests avoid order, culture/timezone, host paths, ambient time/random, irrelevant editor state, internet, and hosted-service dependence. |
| ADR-0009-R14 | Determinism and isolation | Current | Parallelism requires no shared mutable process/filesystem state; exclusive tests declare/minimize it; retry is not a fix for flakes. |
| ADR-0009-R15 | Performance and balance tests | Triggered | Representative performance/distributions account for infrastructure noise; balance expectations preserve broad rules rather than accidental tuning constants. |
| ADR-0009-R16 | Coverage and mutation testing | Current | Coverage is diagnostic; mutation stays deep without invented threshold; persistent critical survivors are addressed or explicitly analyzed. |
| ADR-0009-R17 | Confirmation | Current | Required behavior/negative/reproducibility/scenario/engine/architecture evidence executes through canonical verification and asserts requirements rather than incidental implementation. |

Context: listed scenario/property/rule candidates are not requirements to build nonexistent treaties, economies, generators, or namespaces. ADR 0002 controls normal/deep command ordering. Tool selections do not require ceremonial installation without a qualifying test.

### ADR 0010

Source: `docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0010-R01 | Strategic AI ownership | Current | Consequential faction/fleet AI is project-owned pure Core C#; packages cannot replace domain goals, constraints, choice, command, and explanation semantics. |
| ADR-0010-R02 | Decision pipeline | Current | Explicit actor snapshot/goals/candidates/constraints/evaluation/ties produce typed command or deliberate no action and structured explanation. |
| ADR-0010-R03 | Decision pipeline | Current | Evaluation does not mutate arbitrary world state; proposed commands undergo ordinary validated application. |
| ADR-0010-R04 | Information boundary | Current | Actor receives legitimate information/capability projection, never hidden world truth for convenience; difficulty cannot silently grant omniscience. |
| ADR-0010-R05 | Explainability | Current | Strategically consequential decisions explain goal, knowledge/capability, candidates/rejections, scoring/policy, randomness/tie resolution, issued command/no-action reason; tests/diagnostics can access it. |
| ADR-0010-R06 | Determinism and budgets | Current | Simulation clock, stable order, bounded algorithmic budgets, and documented fallback produce reproducible results without wall-clock-only outcome selection or partial mutations. |
| ADR-0010-R07 | Tactical AI | Current | Consequential tactical actions obey Core authority, information, commands, order, and proportionate explanation; visual actors may remain engine-local. |
| ADR-0010-R08 | State-transition policy | Triggered | Start with explicit validated domain states; demonstrated complexity justifies library; Stateless is preferred candidate, LogicBlocks conditional; persisted identity/state remains project-owned. |
| ADR-0010-R09 | Behavior trees and LimboAI | Triggered | Strategic foundation is not behavior-tree framework; any LimboAI adoption proves integration/exports/lifecycle/headless/license/boundary and cannot own doctrine/resources/treaties/world decisions. |
| ADR-0010-R10 | Scripted behavior | Triggered | Authored behavior uses legitimate Core rules/knowledge/commands and does not freeze unrelated autonomy. |
| ADR-0010-R11 | Large language models | Current | No hosted/local LLM adjudicates authoritative strategic/tactical/diplomacy/rules/content/state; gameplay works offline. |
| ADR-0010-R12 | Large language models | Triggered | Optional LLM tooling/flavor needs separate trust/fallback design and validated typed consequences. |
| ADR-0010-R13 | Confirmation | Current | Evidence covers Core-only choice, hidden-truth invariance, stable replay, explanations/rejections, applicable degraded/scenario/horizon behavior, architecture, and every consequential command boundary. |

Context: AI means autonomous systems, not ML; specific planners/doctrines/treaty rules remain system design. No universal state framework or immediate package adoption follows.

### ADR 0011

Source: `docs/adr/0011-represent-physical-quantities-with-explicit-units.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0011-R01 | Quantity classification | Current | Classify simulation numeric semantics before representation: physical dimension, count, consequential bounded ratio/probability, fictional quantity, or local scalar. |
| ADR-0011-R02 | Standard physical quantity | Current | Public domain and subsystem boundaries use strongly typed physical quantities; clear primitive names alone do not satisfy the stronger body requirement. |
| ADR-0011-R03 | Bounded ratio or probability | Current | Consequential invalid/nonfinite/out-of-range ratios use project-owned bounded types; distinct concepts retain their own rules. |
| ADR-0011-R04 | Fictional or setting-specific quantity | Current | Fictional/abstract semantic quantities use project-owned domain types rather than false physical equivalence. |
| ADR-0011-R05 | Scalar tuning value; Discrete count | Current | Appropriate integral counts/local intermediate primitives are allowed; scalar configuration/subsystem boundaries have explicit names and validation. |
| ADR-0011-R06 | UnitsNet adoption | Triggered | Before first production UnitsNet use, prove required conversions/arithmetic, canonical JSON adapters, explicit Godot mapping, representative performance, invalid-value behavior, license, and .NET compatibility. |
| ADR-0011-R07 | UnitsNet adoption | Triggered | Successful proof permits central admission; materially failed proof supports focused project-owned structs with recorded evidence before another library choice. |
| ADR-0011-R08 | Canonical units | Current | Each serialized physical field has one canonical schema-versioned unit documented by suffix/schema/singular type; ambiguous numeric fields and arbitrary unit strings/formatted package text are rejected. |
| ADR-0011-R09 | Domain and persistence boundaries | Current | Constructors/commands accept typed values; validated JSON and load mappings reconstruct them; canonical-unit/meaning changes require save migration. |
| ADR-0011-R10 | UnitsNet adoption; Domain and persistence boundaries | Current | Third-party quantity type names, enum ordinals, internal representation, Godot exports, or formatting do not become canonical wire/mod identities. |
| ADR-0011-R11 | Godot boundary | Current | Explicit adapter conversions state scale/origin/orientation/units; pixels/vectors/frame delta cannot become Core physical truth. |
| ADR-0011-R12 | Arithmetic and precision | Current | Appropriate discrete/continuous precision, tolerances/quantization, finite/range checks, and tested consequential overflow/underflow preserve semantics. |
| ADR-0011-R13 | Rates and derived quantities | Current | Rates identify numerator/denominator via typed rate or explicit formula; conversion cannot hide damage/efficiency/allocation rules. |
| ADR-0011-R14 | Content authoring | Current | Schemas and semantic validation cover units, bounds, finite values, impossible relationships, and conversion overflow. |
| ADR-0011-R15 | Confirmation | Current | Evidence covers boundary representation, canonical schemas, conversions/extremes/nonfinite/dimensions, migration, Godot conversion, equivalence, hot-path performance where applicable, and meaningful displayed precision. |

Exception/context: do not wrap all counts/IDs/enums/local calculations; primitive normalized hot-loop storage is allowed behind validated typed boundaries. UnitsNet is a selected first candidate, not unconditional package installation. Example JSON numbers are illustrative, not game tuning.

### ADR 0012

Source: `docs/adr/0012-keep-branching-narrative-subordinate-to-simulation.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0012-R01 | Authority boundary | Triggered | Narrative owns local flow only; relationships/resources/missions/time/randomness/knowledge/legal transitions remain Core. |
| ADR-0012-R02 | Narrative assembly boundary | Triggered | Admitted runtime goes in pure-.NET Narrative→Core; Godot may reference both; Core never references Narrative; no Godot needed for evaluation/validation. |
| ADR-0012-R03 | Narrative package trigger | Triggered | Concrete branching/reconvergence/subflow/context/local-variable/resume/localization need precedes focused prototype and runtime/assembly adoption. |
| ADR-0012-R04 | Context adapter | Triggered | Narrative receives read-only permitted projection, versioned named semantics, no mutable world/service locator/database/scene/repository/reflection access. |
| ADR-0012-R05 | Typed consequences | Triggered | Finite stable consequence names and payloads invoke validated Core commands; illegal choices have authored fallback/retry/exit; atomic groups use explicit Core transaction. |
| ADR-0012-R06 | Time and autonomy | Triggered | Feature explicitly decides communication time/pause semantics; narrative timers cannot advance truth or implicitly freeze unrelated actors. |
| ADR-0012-R07 | Ink as the first prototype | Triggered | Ink-first prototype proves compatibility, determinism, resume, typed callbacks, canonical build diagnostics, localization, license, and workflow before adoption. |
| ADR-0012-R08 | Dialogue Manager as the Godot-native comparator | Triggered | Comparator adoption documents concrete editor benefit and integration/export/persistence/headless costs; inability to meet pure-.NET boundary argues against adoption. |
| ADR-0012-R09 | Narrative persistence | Triggered | Persist bounded/versioned narrative-local state and reconnection IDs, never alternate Core truth; validate resume and documented interruption/incompatibility fallback. |
| ADR-0012-R10 | Narrative content validation | Triggered | Canonical source compiles/parses in verification; validate labels/context/consequences/references/fallbacks/localization/mutation hooks/version and critical paths. |
| ADR-0012-R11 | Localization and presentation | Triggered | Stable branch/command identities survive translation; Godot owns layout/input/assets/accessibility; scripts cannot manipulate arbitrary nodes. |
| ADR-0012-R12 | AI and generated text | Triggered | Narrative does not replace autonomous AI; generated flavor needs separate ADR, fallback/offline/content/disclosure/cache design, and no Core mutation authority. |
| ADR-0012-R13 | Confirmation | Triggered | Evidence covers read-only headless context, consequence registry/failure, architecture, content build, resume, localization identity, autonomy, admission, and delayed assembly creation. |

Exceptions: linear messages, reports, tooltips, and small fixed menus use existing mechanisms. Specialized source-language permission begins with admitted runtime and applies only to narrative. No narrative implementation is implied by active ADR status.

### ADR 0013

Source: `docs/adr/0013-use-dev-for-development-and-main-for-releases.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0013-R01 | Branch roles and names | Current | `dev` is permanent/default/protected integration; never delete/force-push; ordinary issue-based topics start from current dev, urgent hotfixes from current main. |
| ADR-0013-R02 | Branch roles and names | Current | Named topic families/issue numbers/lowercase digit-hyphen slugs are enforced; merged short-lived branches are deleted. Qualifying Standalone branch representation requires reconciliation below. |
| ADR-0013-R03 | Branch roles and names | Current | Main is release-only with approved dev/hotfix sources and no routine direct push; no standing release branch without later ADR. |
| ADR-0013-R04 | Admission and governing work | Current | Significant work has typed issue and draft Final/Supporting PR; bounded low-risk eligible maintenance may use package-governed Standalone PR. |
| ADR-0013-R05 | Admission and governing work | Current | Code/comments/tests/scripts/config/workflows/dependencies/schemas/releases/normative decisions/specs/enforcement require PR. |
| ADR-0013-R06 | Admission and governing work | Exception | Direct dev admission is narrowly qualifying prose T0 or handoff-only allowed paths, with exactly one matching trailer; mixed scope requires PR; construction/PR commits carry none. |
| ADR-0013-R07 | Admission and governing work | Triggered | Package upgrade reevaluates explicit repository Handoff exception without displacing package issue/PR/T0/lifecycle authority. |
| ADR-0013-R08 | Commit and merge history | Current | Subjects/squash titles use enumerated Conventional Commit types; topics→dev squash; rebase merge disabled. |
| ADR-0013-R09 | Commit and merge history | Current | Dev→main release uses exact release title and merge commit; hotfix→main uses fix title/squash; main→dev synchronization uses exact sync title/merge. |
| ADR-0013-R10 | Commit and merge history | Exception | One-time exact baseline title/ADR-presence condition permits untagged baseline merge; exception expires after adoption. |
| ADR-0013-R11 | Enforcement and trust boundary | Current | Permanent branches require strict checks, resolved conversations, PRs/zero mandatory approvals; block force/deletion; main admin enforcement has no routine bypass; dev owner exception permits approved direct classes. |
| ADR-0013-R12 | Enforcement and trust boundary | Current | Tracked commit/pre-push hooks and setup enforce subjects/targets/trailers/path/T0/direct-merge rules; PR CI enforces topology/ancestry/title/name; canonical verifies mechanisms. |
| ADR-0013-R13 | Enforcement and trust boundary | Current | Document accepted owner/hook bypass residual risk; do not claim path-sensitive server-perfect authorization. |
| ADR-0013-R14 | Releases and hotfixes | Triggered | Planned release has Task/criteria, brief freeze, Final release PR/notes, verification/merge, SemVer tag on merge, immutable release. |
| ADR-0013-R15 | Releases and hotfixes | Current | Every post-baseline main merge has corresponding tag/immutable release; 0.x until explicit 1.0; tag and release notes remain version/changelog authorities until concrete consumer. |
| ADR-0013-R16 | Releases and hotfixes | Triggered | Hotfix increments patch and published main synchronizes to dev before ordinary development resumes. |
| ADR-0013-R17 | Migration and rollback | Exception | Proven recovery may temporarily change only blocking protection using owning credential, record reason, restore and verify; never bypass ordinary issue/PR admission. |
| ADR-0013-R18 | Confirmation | Current | Evidence includes local hook path, positive/negative policy tests, rexec canonical pass, PR checks, live default/merge/deletion/protection state, and post-baseline release receipts. |

Context: initial dev creation/baseline adoption is historical, not a repeatable current migration. Source-only releases promise no exported binaries/artifacts. Multiple maintainers/lines/stabilization/packaging/path-sensitive bypass are reconsideration triggers.

### ADR 0014

Source: `docs/adr/0014-use-an-extensible-bounded-ship-system-substrate.md`; detailed refinement: `docs/wiki/ship-system-substrate.md`.

| ID | Section | Class | Independently verifiable obligation |
| --- | --- | --- | --- |
| ADR-0014-R01 | Three distinct system identities | Current | Semantic kind, reusable definition, and installed instance are distinct stable identities; preserve current five kind meanings. |
| ADR-0014-R02 | Three distinct system identities | Current | Bootstrap identity is deterministic from explicit authored mapping; bounded runtime allocator preserves continuation and does not retarget removed IDs. |
| ADR-0014-R03 | Reusable system definitions and authored ship loadouts | Current | Strict bounded definitions own common/specialized equipment data; ship design supplies initial loadout; live ship owns actual loadout independently of class defaults. |
| ADR-0014-R04 | Bounded runtime installed-system state | Current | One canonical bounded installed-ID collection owns common condition/allocation; unsupported capabilities have no meaningless placeholder; shared mechanics derive support from canonical data. |
| ADR-0014-R05 | Typed system-specific behavior | Current | Specialized behavior/cardinality/aggregation remains explicit typed rules; common storage permits multiple instances without inventing gameplay aggregation. |
| ADR-0014-R06 | Runtime installation, removal, replacement, and modification | Current | Model/bootstrap/save can represent divergent same-class current loadouts; no mutable-list escape hatch. |
| ADR-0014-R07 | Runtime installation, removal, replacement, and modification | Triggered | Future modifications use atomic validated Core transitions reconciling power, repair/work, capabilities/cooldowns, identities, and projections; immutable definitions are not edited per instance. |
| ADR-0014-R08 | Deterministic ordering | Current | Shared order comes from explicit semantic content plus stable tie-break, never hashes/enums/construction/reflection/labels; consequential order changes receive compatibility treatment. |
| ADR-0014-R09 | Generic damage and repair participation | Current | Damage/repair derive eligible installations from definitions/state, not curated kind lists; one repair targets installed ID; specific consequences remain typed. |
| ADR-0014-R10 | Extensible power consumers without another power model | Current | Generation uses typed aggregation; consumer allocations key actual installations; additional consumers do not require per-kind fields/presets/DTO/UI plumbing. |
| ADR-0014-R11 | Actor knowledge and external targeting | Current | Own truth does not expose remote loadout/IDs; semantic aim remains observer-safe; presence/absence cannot become inventory oracle. |
| ADR-0014-R12 | Persistence and migration | Current | Current saves capture actual bounded installations/IDs/definitions/common/specialized state/allocator directly; historical DTOs remain frozen. |
| ADR-0014-R13 | Persistence and migration | Current | Adjacent historical migration maps exactly represented five kinds/conditions/allocations/repair deterministically; no invented systems/history; invalid/unknown/duplicate/capability-inconsistent entries fail. |
| ADR-0014-R14 | Projection and presentation | Current | Player common projection reflects actual actor-safe ordered installations; generic own repair/power commands carry installed IDs; Godot renders/submits intent with Core legality/reasons. |
| ADR-0014-R15 | Transition of the existing five systems | Current | Complete vertical migration preserves approved M6A semantics, five kinds/four consumers, equipment ownership, absence, typed specialization, compatibility, and knowledge boundaries before sixth system/recovery expansion. |
| ADR-0014-R16 | Confirmation | Current | Prove unchanged common algorithms accept another definition/installation; reject duplicate current authority/per-kind generic plumbing; bound strict shapes, deterministic order, and non-inventive migration. |
| ADR-0014-R17 | Wiki: Identity and ownership; Cardinality and absent capabilities | Current | Ship-local IDs plus ship identity prevent cross-ship mutation; explicit empty differs from omitted default; separate common multiplicity from typed zero-or-one refusal; valid absence has typed effects. |
| ADR-0014-R18 | Wiki: Power, condition, and repair | Current | Complete exact consumer allocation, bounded sums, explicit semantic remainder order, existing Balanced/brownout distinction, atomic voluntary rejection, forced reconciliation, and exact equal-time repair invalidation remain intact. |
| ADR-0014-R19 | Wiki: Specialized state and observations | Current | Scan/readiness reference actual sensor/weapon with exact validation; ship-owned facts stay ship-owned; scheduler retains closed Ship/Faction targets and established budgets. |
| ADR-0014-R20 | Wiki: Remote targeting must not become an inventory probe | Current | Absent hidden receiver still allows otherwise legal discharge/readiness; shields resolve normally, no residual redirection/hull invention; attacker feedback remains equivalent for paired hidden receiver states. |
| ADR-0014-R21 | Wiki: Compatibility and current-format capture | Current | Fixed historical mapping/constants/compatibility descriptors retain meaning; current capture never flattens through legacy fields; class-default changes do not overwrite live saves. |
| ADR-0014-R22 | Wiki: Compatibility and current-format capture | Current | Re-derive combined save/work bounds with allocator/installation/high-width state; distinguish measured fixture from conservative bound; retain 128 MiB absent separately justified change. |
| ADR-0014-R23 | Wiki: Generic presentation and refit readiness | Current | Stable operation+installed-ID UI keys, current payload/owner/generation binding, absence/focus/preview/quick-load behavior prevent stale controls retargeting installations. |
| ADR-0014-R24 | Wiki: Admission proof and enforcement | Current | Admission proves baseline equivalence, arbitrary IDs/order, per-ship isolation, same-class divergence, absence, separate multiplicity/cardinality, exact continuation/failure preservation, hidden-loadout pairs, architecture negatives, extension, long-horizon/high-width and Godot coverage. |

Exceptions/context: historical named DTOs, isolated legacy translators, typed behavior switches, and derived read-only views are legitimate. No sixth production kind, ECS/property-bag/rule engine, refit controls, recovery semantics, mount topology, facilities/cost/time, or aggregation gameplay is required.

## Overlap, conflicts, and bounded resolutions

1. **ADR 0006/0007 RNG — envelope wording versus present consumer scope.** ADR 0006 “Save envelope” lists random metadata without a qualifier; ADR 0007 governs consumed randomness. `world-navigation-and-time.md#randomness-and-future-scale`, decision D-13/D-19, and open question Q-14 explicitly describe no stochastic policy/current algorithm. Treat current bans and deterministic continuation as current requirements; generator implementation/full stream state becomes triggered by a real authoritative random consumer. Parent F05 should make this qualification explicit without selecting a future algorithm. Code absence still needs proof.

2. **ADR 0009 tool selections — baseline versus trigger.** CsCheck has explicit meaningful-generator/invariant trigger; ArchUnitNET has explicit first non-project-graph rule trigger. GdUnit4Net is selected specifically for C# engine tests; GDScript engine fixtures are explicitly permitted. Existing architecture tests might already satisfy ArchUnitNET’s admission trigger—this requires implementation review. Package absence is not automatically conformant or nonconformant.

3. **ADR 0011 body versus confirmation.** “Standard physical quantity” and “Domain and persistence boundaries” require strong types; “Confirmation” says “typed or explicitly named quantities.” Do not weaken body obligations into naming-only conformance. Parent F04’s authorized clarification should align confirmation with classifications and preserve raw normalized DTO/hot-loop exceptions.

4. **ADR 0011 UnitsNet proof versus library preference.** Preferred candidate is not adoption without proof. Existing project-owned types are not evidence that the required historical proof occurred. Parent F06 must inspect history/current evidence; if absent, report absent historical evidence and perform the authorized current evaluation rather than inventing prior rejection.

5. **ADR 0013 Standalone versus mandatory numbered branches.** Admission allows eligible issue-less Standalone PRs; branch-name text requires issue number for every named topic family. This is a real internal policy gap. Preserve package eligibility and mandatory PR admission; clarify a narrowly admitted Standalone naming route with full topology/positive/negative proof. Do not convert all issue-less work into significant work or bypass branch enforcement.

6. **ADR 0013 versus generic package direct-admission rules.** Handoff is an explicit narrow repository exception, not accidental drift. Standalone does not authorize direct push. T0/Handoff cannot touch ADR/wiki/normative/enforcement material. Package upgrade requires reevaluation.

7. **ADR 0014 versus native-first/no speculative framework policy.** Common substrate has five concrete consumers and explicit owner direction; it is a justified domain abstraction under ADR 0003, not authorization for ECS or generic executable components.

8. **ADR 0014 common multiplicity versus typed gameplay limits.** Common storage supports multiple same-kind entries; current world admission may separately reject unsupported aggregation. Requiring implemented multi-generator/shield/weapon gameplay would expand scope.

9. **ADR 0014 hidden-loadout safety versus absent-kind error.** Detailed wiki contract explicitly supersedes absence-specific remote rejection. Own commands use actual IDs; remote aim remains semantic/observer-local, with normal discharge and unconfirmed receiver effects. This is already settled design.

10. **Architecture confirmation and test tool ownership.** Compile-time references, analyzer bans, reflection-based smoke tests, and selected ArchUnitNET have different scopes. A single passing gate does not prove every durable relation, and duplicated string searches are not substrate architectural proof.

11. **Observability versus determinism/persistence/AI.** Typed explanation is rule output, logging is optional serialization. Failing/no-op logging must preserve Core semantics, and actor-known diagnostics must not become player-facing omniscient projections or a second persistence authority.

12. **Documentation discrepancy found.** `docs/wiki/engineering-and-combat.md#sources` still describes “remaining Godot presentation work,” contradicting implemented generic Engineering status in architecture/substrate/home. Parent RQ-014 should reconcile the stale sentence. No runtime defect is implied.

## Coverage and unresolved owner choices

Covered fully: ADRs 0001–0014. Authority/trigger evidence read from wiki home, architecture, development/governance, source-catalog review record, world/navigation/time, content/persistence, ship-system substrate, and open questions; decision/status/Engineering headings and relevant statements were searched.

Not certified: current implementation, test existence/adequacy, actual library triggers, historical dependency proofs, live GitHub protections/releases, external source freshness, legal/package compatibility, baseline/final gates, and final candidate review.

No new owner choice is needed merely to extract this register. Deferred consequential choices remain explicitly deferred: RNG algorithm, broader compatibility promises, narrative consumer/time semantics, modding, refit/recovery/aggregation gameplay, fuller spatial/knowledge/political systems, and release/artifact readiness. They must not be silently resolved during conformance remediation.
