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
| F01 | `GameScreen.LogDiagnostic` interpolates raw exception messages into `GD.PrintErr`; no game composition logging pipeline. Synthetic reproduction later confirmed sensitive-path leakage; the original discovery remains baseline evidence. ADR 0008 also conflicts internally on original exceptions versus sensitive output. | High source confidence; implement selected structured pipeline and rendered/structured redaction and failure-isolation proof. | ADR 0008; owner explicitly authorizes correction and bounded clarification. | Implemented with retained ADR selection and bounded privacy clarification: real pre-fix synthetic path leakage reproduced; 27 focused logging cases and actual console/retention proof pass. Whole candidate receipt below remains authoritative. |
| F02 | ADR 0013 admits low-risk Standalone work but topic branch syntax requires an issue number. | High; reconcile `standalone/<slug>` with package-owned eligibility and complete-route proof. | ADR 0013 and installed github-workflow package. | ADR 0013, base-package admission script, branch/workflow caller, guidance and full-route fixtures aligned in `0030415`. Semantic eligibility remains author/reviewer judgment; no live issue-less test PR was invented. |
| F03 | Custom architecture rules exist beyond project references; ArchUnitNET and CsCheck are absent. | High; evaluate current triggers and selected test-only dependencies; preserve useful specialized probes. | ADRs 0003 and 0009. | ArchUnitNET 0.13.4 and CsCheck 4.9.1 admitted test-only with central pins/locks. 26 focused final rules/properties pass after negative probes exposed and corrected the first candidate's weaknesses (F11). Specialized IL tests retained. |
| F04 | ADR 0011 Confirmation says typed or named, weaker than runtime-boundary strong typing. | High textual confidence; clarify, inspect consequential APIs and preserve numeric wire semantics. | ADR 0011 and owner bounded clarification. | ADR confirmation tightened; tactical heading/speed/duration and consequential power/capability boundaries typed in `8c9b9ec`/`479a0e5`, preserving numeric wire units. Finite/range/overflow, conversion and characterization tests pass. |
| F05 | No authoritative random consumer found in initial runtime inspection; envelope wording is unconditional. | Initial medium confidence, strengthened by completed call-path trace; clarify explicit no-state case and future admission gate. | ADRs 0006/0007; no dummy RNG authorized. | Call-path and dependency review found no authoritative RNG. ADRs 0006/0007 now state the no-state case and indivisible first-consumer admission gate; no generator, seed or save version added. |
| F06 | Available-ref history search found UnitsNet references but no suitability evaluation. | Medium; present-day evaluation required; this is not proof no off-repository evaluation ever existed. | ADRs 0003/0011; policy alternatives require owner decision. | Present-day UnitsNet 5.75.1 proof executed; historical provenance still absent in available refs. Owner explicitly approved the narrow existing distance/speed exception. This remains an owner-approved exception, not retroactive compliance. |

## Obligation register

The initial register was committed before remediation. The 191 stable `ADR-NNNN-Rnn` obligations below join exact sections and triggers to the separate baseline/candidate outcome table, evidence index and revision-specific receipts. Future admission triggers and approved exceptions remain distinct from active defects.

## Verification receipts

Baseline `rexec -- ./scripts/verify.sh` ran from an isolated clean `dev` worktree at `2ef6c77aa4587719aac0b59cfda3d6ee7c560e5a` and exited 0: 1,035 Core and 324 AssetCtl tests passed. Godot reported 86 gameplay, one integration and two asset-import cases with no failures/skips; smoke passed. The initial primary-checkout attempt stopped on the orchestrator's unsupported `chore/` branch prefix, which was corrected to `task/`; an interrupted redundant attempt returned rexec control exit 21, not a repository test failure. Neither attempt substitutes for the isolated passing baseline. `rexec config show` confirms sanitized Git context enabled; `rexec doctor` exited 0. A passing baseline gate does not certify every obligation.

## Review and delivery

Independent bounded native reviews cover ADR clauses, persistence safety, runtime determinism/information boundaries, units/history, and workflow/test-tool admission. Claude cross-agent preflight passed with verified saved-login and read-only tool-permission containment; completed reviews challenged governance composition, persistence admission, architecture/property tests and logging. Their findings and dispositions are retained below. No merge, tag, release or administrative change is authorized.

The last full wiki semantic sweep remains September 26, with October 3 due date. This ADR effort does not reset it without completing the wiki's full procedure.

### E-GOV — live governance receipt

September 27 authenticated administrative reads used the existing credential reference from `docs/handoff/credentials.md`, scoped only to the GitHub calls. Every read exited 0: `gh api repos/L3DigitalNet/star-trek-alter-course`, `branches/{dev,main}/protection`, `rulesets?includes_parents=true`, `rules/branches/{dev,main}`, `rulesets/22032602`, and `releases`. No credential values or raw authenticated responses are published. No protection, bypass, organization schema or release setting was changed.

The repository is public and its default branch is `dev`. Merge commits and squash are enabled, rebase merge disabled, automatic head-branch deletion enabled. Both classic branch protections require strict `Canonical verification` and `Branch policy` checks (app 15368), conversation resolution and PR review configuration with zero required approving reviews; force-push and deletion are disabled. Main enforces administrators; dev's explicit owner direct-admission exception remains. Effective branch ruleset arrays are empty because classic protections supply these rules. Tag ruleset 22032602 is active for `refs/tags/v*`, denies update/deletion, and has no bypass actors. `git config --get core.hooksPath` returns `.githooks`.

`git log origin/main --first-parent` and annotated tag peeling establish the following exact post-baseline merge targets. The live release list reports all eight immutable, published releases; API `target_commitish: dev` is not used as tag-target proof. `git merge-base --is-ancestor origin/main origin/dev` exits 0. The last sync is `1778634bd192ed393f40b30708fa88e049714a8a` (#107); earlier release/sync pairs are retained in dev history. No active hotfix or release operation is part of this task.

| Release | Tagged main merge                          |
| ------- | ------------------------------------------ |
| v0.1.0  | `cd8ab32474b18513550dd3a3df9f8e56895225dd` |
| v0.2.0  | `163b8e222aec309663799f59287274ba3db39da3` |
| v0.3.0  | `fae21bd81533b33f2d2d350a4042b9c239cf40e3` |
| v0.4.0  | `b3b6635470003d11260b99a2a56f03a3bfa201f6` |
| v0.5.0  | `0547d061ca4bde76b382274a16e079f46cd076d8` |
| v0.6.0  | `d00460ea8b472c44ea2a8343d43e676efb96000b` |
| v0.6.1  | `f0af2653ca44f17b9f701f6271e199cda1429d16` |
| v0.6.2  | `255eaedc8e27b483b0fd4e2fe0bccf050486b3bf` |

The one-time untagged adoption merge is `5028501ba607045b6b302fd62957f140ce65fcbc`; it is historical, not permission to repeat baseline admission. Standalone eligibility remains a bounded author/reviewer judgment after package contract checks. Full mocked admission-route tests are executable proof; no live issue-less test PR was created solely for this audit, and server-perfect semantic classification is not claimed.

## Register conventions and authority

The initial extraction marked every row unverified. The separate outcome table records subsequent source tracing and executable proof without erasing the original baseline. A documented current-state claim alone is evidence of declared scope, not proof of implementation.

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
| ADR-0008-R13 | Error reporting | Current | Recovery retains the original exception and logs one contextual, allowlisted classification without forwarding the exception object or sensitive data; structured results represent expected failures. Baseline wording instead required the original exception object, conflicting with its privacy rule (F01). |
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

## Per-obligation baseline and candidate outcomes

These outcomes join the stable IDs and exact source sections above. Every applicable row is assessed against the scoped ADR and owning wiki, not against the prior assistant audit. Baseline means `2ef6c77aa4587719aac0b59cfda3d6ee7c560e5a`. Candidate executable outcomes remain **unverified** until the integrated receipt below is complete; worker passes are supporting evidence only.

Baseline confidence is high for traced source plus executed named tests and reproduced defects; absence-of-history claims have medium confidence and retain their limits. For compliant rows, no mismatch or impact was identified in the stated implemented scope and the disposition is to retain the mechanism. For deferred rows, the original obligation/trigger above and the evidence note define admission; no placeholder implementation is required. The authorizing ADR remains the decision authority, except explicitly identified owner-approved amendments. An owner-approved exception is a separate visible status, not a compliance relabel.

| ID | Baseline status | Candidate status | Implementation, proof, disposition and limitation |
| --- | --- | --- | --- |
| ADR-0001-R01 | compliant-with-evidence | unverified | Core.csproj and Godot.csproj reference direction; ArchitectureBoundaryTests.CoreAssemblyDoesNotReferenceGodot (E-ARCH). |
| ADR-0001-R02 | compliant-with-evidence | unverified | verify.sh invokes Core xUnit independently before Godot (E-GATE). |
| ADR-0001-R03 | compliant-with-evidence | unverified | IntegrationProbeTest, GeneratedAssetImportTest and GameplayShellTest run separately (E-GATE). |
| ADR-0001-R04 | compliant-with-evidence | unverified | Core/BCL-only domain and projection signatures; ActorSafeProjectionsExposeNoHiddenIdentityOrPersistenceTypes (E-ARCH). |
| ADR-0001-R05 | compliant-with-evidence | unverified | ArchitectureBoundaryTests plus positive/31 negative compiler probes in test-core-boundaries.sh (E-ARCH). |
| ADR-0002-R01 | compliant-with-evidence | unverified | scripts/verify.sh ordered canonical contract, shared by verify.yml (E-GATE). |
| ADR-0002-R02 | compliant-with-evidence | unverified | Directory.Build.props warning policy, Meziantou and Core banned APIs (E-GATE/E-ARCH). |
| ADR-0002-R03 | compliant-with-evidence | unverified | verify.sh uses only CSharpier for C#; narrow IDE0055 exception (E-GATE). |
| ADR-0002-R04 | compliant-with-evidence | unverified | global.json, central pins, tool manifest, checksum resolvers, diagnostic-suppressions.allowlist (E-GATE). |
| ADR-0002-R05 | compliant-with-evidence | unverified | verify.yml calls verifier once; Node setup is a prerequisite, not duplicated verification (E-GATE). |
| ADR-0002-R06 | compliant-with-evidence | unverified | verify.sh set -euo pipefail and tracked_state before/after hashes (E-GATE). |
| ADR-0002-R07 | compliant-with-evidence | unverified | test-mutation.sh remains separate; no invented mutation threshold (E-DEEP). |
| ADR-0002-R08 | compliant-with-evidence | unverified | Managed Markdown workflows complement canonical script; hosted checks recorded separately (E-GATE). |
| ADR-0003-R01 | compliant-with-evidence | unverified | Existing schema/AssetCtl records and new gameplay-logging/architecture-testing admissions inventory actual expanded consumers (E-DEPS). |
| ADR-0003-R02 | compliant-with-evidence | unverified | Admissions compare native/project-graph options against selected ADR0008/0009 requirements (E-DEPS). |
| ADR-0003-R03 | compliant-with-evidence | unverified | Native Control/Node2D, input/theme/import and map adapters; engine shell tests (E-UI). |
| ADR-0003-R04 | compliant-with-evidence | unverified | Traced Core scheduling/navigation/sensor/combat/AI consume no Godot noise (E-RUNTIME). |
| ADR-0003-R05 | compliant-with-evidence | unverified | Admission records cover managed closure, compatibility/licenses, offline failures and replacement seams (E-DEPS). |
| ADR-0003-R06 | compliant-with-evidence | unverified | Directory.Packages.props plus committed lockfiles; canonical locked restore (E-GATE/E-DEPS). |
| ADR-0003-R07 | compliant-with-evidence | unverified | GdUnit4 6.2.0 immutable upstream d18770221c2df4a3c991a42fdce7907df40eea75; original vendoring predates ADR0003. Historical rationale unavailable; no retroactive approval invented (E-DEPS). |
| ADR-0003-R08 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | ADR0014 narrowly owns substrate; no database/ECS/DI container/general event bus added (E-ARCH). |
| ADR-0003-R09 | compliant-with-evidence | unverified | New admission records, managed test-only dependency closure and locked restore; Core architecture rules (E-DEPS/E-ARCH). |
| ADR-0003-R10 | compliant-with-evidence | unverified | Core uses ordinary immutable/BCL collections; engine collection conversion stays in Godot (E-ARCH). |
| ADR-0004-R01 | compliant-with-evidence | unverified | Core StrategicMap/TacticalPosition/TacticalMotion/SensorKnowledge and GameSimulation own implemented spatial truth (E-RUNTIME). |
| ADR-0004-R02 | compliant-with-evidence | unverified | TacticalMapView/TacticalMapTransform and strategic map adapter own camera/display transforms; Core no Godot types (E-UI/E-ARCH). |
| ADR-0004-R03 | compliant-with-evidence | unverified | LocationId/routes and separate strategic/tactical coordinate models; no tile/pixel identity coupling (E-RUNTIME). |
| ADR-0004-R04 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No pathfinding consumer; present direct-route ApplyShipTravel remains Core. Future authoritative pathfinding activates admission (E-RUNTIME). |
| ADR-0004-R05 | compliant-with-evidence | unverified | GameSimulationTests.TacticalMovementIsContinuousAndBatchEquivalent; heading/cardinal and motion conversion tests (E-QUANTITY). |
| ADR-0004-R06 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No engine navigation output consumed by authoritative movement; future output requires Core legality/cost validation (E-RUNTIME). |
| ADR-0004-R07 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No consequential geometry package consumer; future admission must preserve semantic geometry authority (E-DEPS). |
| ADR-0004-R08 | compliant-with-evidence | unverified | Actor-safe contact/report projections; hidden-truth tests hold knowable inputs fixed (E-AI). |
| ADR-0004-R09 | compliant-with-evidence | unverified | Current coordinate/heading/projection/engine tests; no nonexistent pathfinding feature demanded (E-QUANTITY/E-UI). |
| ADR-0005-R01 | noncompliant | unverified | FactionDefinitionCatalogLoader.ParseStrict now supplies explicit depth/comments/trailing options; three syntax/depth characterizations. Historical defaults already strict; configuration ownership gap, not unsafe acceptance (F09/E-CONTENT). |
| ADR-0005-R02 | noncompliant | unverified | StrictContentJson and faction strict reader reject malformed Unicode names/values as typed content validation before schema decoding; four paired raw/escaped property/value regressions cover all three families. Duplicate/unknown members remain rejected (F09/E-CONTENT). |
| ADR-0005-R03 | compliant-with-evidence | unverified | DefinitionContent byte/stream bounds and LoadCatalog limits; schema string/record limits before typed mapping (E-CONTENT). |
| ADR-0005-R04 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | ADR0012 reciprocal narrative amendment; no admitted narrative runtime/source (E-NARRATIVE). |
| ADR-0005-R05 | compliant-with-evidence | unverified | JsonSchema.Net pinned; production shipV6/systemV1/factionV1 schema fixtures (E-CONTENT/E-DEPS). |
| ADR-0005-R06 | compliant-with-evidence | unverified | System definitions precede ship loadouts; strict parse/schema/reference/cardinality before runtime catalog (E-CONTENT). |
| ADR-0005-R07 | noncompliant | unverified | Malformed Unicode previously escaped as raw InvalidOperationException; strict prescan now preserves family-specific validation errors. Unsafe definitions never reach runtime; deterministic diagnostics remain (F09/E-CONTENT). |
| ADR-0005-R08 | compliant-with-evidence | unverified | Typed IDs/ordinal registration; display/file names not lookup authority (E-CONTENT). |
| ADR-0005-R09 | compliant-with-evidence | unverified | ShipLoadoutContentTests.RejectsUnresolvedDefinitionReference and typed catalog resolution (E-CONTENT). |
| ADR-0005-R10 | compliant-with-evidence | unverified | Content versions, frozen save migration descriptors and V10CompatibilityTests (E-SAVE). |
| ADR-0005-R11 | compliant-with-evidence | unverified | Ship/SystemDefinition load headlessly; EngineeringKindPresentation.For fallback uses authored label/common layout. Global theme/fonts are scene assets explicitly outside ADR0005 scope (E-UI/E-CONTENT). |
| ADR-0005-R12 | compliant-with-evidence | unverified | Ordinary production ship/system/faction definitions JSON; AssetCtl YAML is independently admitted tool config (E-CONTENT/E-DEPS). |
| ADR-0005-R13 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No generated/imported ordinary Core definition pipeline; any future one must use same validators (E-CONTENT). |
| ADR-0005-R14 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No mod packaging/override consumer; admission remains future (E-CONTENT). |
| ADR-0005-R15 | compliant-with-evidence | unverified | Content loader negative suites including strict faction characterization; actual production content runs in gate (E-CONTENT/E-GATE). |
| ADR-0006-R01 | compliant-with-evidence | unverified | Explicit SaveGame DTO mapping; SubstrateConformanceTests.RuntimeCodeReferencesNoSaveDto (E-SAVE/E-ARCH). |
| ADR-0006-R02 | compliant-with-evidence | unverified | Explicit System.Text.Json UTF8/options, closed DTO shapes and finite converter (E-SAVE). |
| ADR-0006-R03 | compliant-with-evidence | unverified | CaptureV10 contains schema/rules, metadata, references/descriptors, time/scheduler/allocator (E-SAVE). |
| ADR-0006-R04 | ambiguous/pending-decision | deferred-by-explicit-trigger | No authoritative RNG; amended applicability preserves future full algorithm/stream state requirement (F05/E-RUNTIME). |
| ADR-0006-R05 | compliant-with-evidence | unverified | CaptureEngineeringV10 and continuation tests preserve actual installations, repair/scan/cooldown/knowledge/order; projections derive (E-SAVE). |
| ADR-0006-R06 | noncompliant | unverified | GamePersistenceAdmissionTests reject V5/V6 null outstandingWork with typed InvalidData; every supported-version continuation remains (F07/E-SAVE). |
| ADR-0006-R07 | compliant-with-evidence | unverified | Adjacent migrations and frozen V9 map; V10CompatibilityTests independent expected mapping (E-SAVE). |
| ADR-0006-R08 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No supported-save removal; future removal requires explicit policy/compatibility decision (E-SAVE). |
| ADR-0006-R09 | noncompliant | unverified | ValidateInputBounds precedes byte copy/DOM/DTO; collection/string/member adversarial admission tests (F07/F10/E-SAVE). |
| ADR-0006-R10 | compliant-with-evidence | unverified | Closed shapes and staged RestoreV10; GamePersistenceTests invalid load preserves live world (E-SAVE). |
| ADR-0006-R11 | compliant-with-evidence | unverified | Save validates candidate, sibling CreateNew/write/Flush(true)/close/replace; failure seam proves previous slot. Filesystem limits explicit (E-SAVE). |
| ADR-0006-R12 | compliant-with-evidence | unverified | Explicit snapshot restoration, no database/log replay authority (E-SAVE). |
| ADR-0006-R13 | compliant-with-evidence | unverified | Supported V1–V10 migrations and exact continuation; new null/bounds/malformed-member regressions (E-SAVE). |
| ADR-0006-R14 | noncompliant | unverified | SaveFileOperations + GamePersistenceWriteFailureTests create/write/flush/close/replace/cleanup; F14 fixes secondary close failure masking the original exception. No universal crash/power-loss guarantee (E-SAVE). |
| ADR-0007-R01 | compliant-with-evidence | unverified | Explicit SimulationTime/Duration and advance APIs; Godot pause/time controls; organizational timestamp never advances simulation (E-RUNTIME/E-UI). |
| ADR-0007-R02 | compliant-with-evidence | unverified | AdvanceTo boundary traversal and AdvanceSegment active motion; inactive-ship/event-boundary tests (E-RUNTIME). |
| ADR-0007-R03 | compliant-with-evidence | unverified | SimulationScheduler immutable due/sequence/target/kind work, persisted ordering; no delegates (E-RUNTIME). |
| ADR-0007-R04 | compliant-with-evidence | unverified | Exact cancellation/replacement and repair work correlation; actor-removal consumer absent (E-RUNTIME). |
| ADR-0007-R05 | compliant-with-evidence | unverified | AdvanceTo validates candidate, resolves stable order, returns outcomes; public call commits afterward (E-RUNTIME). |
| ADR-0007-R06 | compliant-with-evidence | unverified | Same-boundary/total/ship-step finite budgets and candidate staging; rejection tests preserve counters/world (E-RUNTIME). |
| ADR-0007-R07 | compliant-with-evidence | unverified | Synchronous authoritative commit; Godot submits explicit ordered typed commands (E-RUNTIME/E-UI). |
| ADR-0007-R08 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No background authoritative calculation consumer; future immutable-result deterministic commit required (E-RUNTIME). |
| ADR-0007-R09 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No authoritative RNG consumer; first feature must admit owned versioned generator alongside rules (F05/E-RUNTIME). |
| ADR-0007-R10 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No generator algorithm selected/needed today; reference vectors mandatory at first random feature (F05/E-RUNTIME). |
| ADR-0007-R11 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No streams currently; future ownership/derivation/state requirement retained (F05/E-RUNTIME). |
| ADR-0007-R12 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No future-affecting random state today; first consumer must persist complete supported state (F05/E-RUNTIME). |
| ADR-0007-R13 | compliant-with-evidence | unverified | Stable-order scheduler/AI, hidden-truth and exact continuation; semantic scope does not promise universal cross-platform bytes (E-RUNTIME/E-AI/E-SAVE). |
| ADR-0007-R14 | compliant-with-evidence | unverified | Core metadata supplied explicitly; GameScreen.UtcNow only save organization. No exact clock-dependent engine test; AssetCtl SpendGuard already accepts Func<DateOnly> (E-RUNTIME). |
| ADR-0007-R15 | noncompliant | unverified | Scheduler order/cancel/restore/overflow/budget tests and 31 forbidden API negative compilations plus legal fixtures (E-RUNTIME/E-ARCH). |
| ADR-0007-R16 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | Non-stochastic horizon fixtures identify no RNG. First random consumer activates vectors/stream continuation/failure seed context (E-RUNTIME). |
| ADR-0008-R01 | noncompliant | unverified | `GameScreen.LogDiagnostic` uses `GD.PrintErr`; no gameplay logging composition. `GameplayLogging.Create/CreateFactory`, `GameScreen._Ready`, `GameSimulation` constructed `ILogger<GameSimulation>`; `DependencyArchitectureTests.PureCoreDoesNotDependOnLogging` and `CoreHasNoForbiddenDependencies`. |
| ADR-0008-R02 | compliant-with-evidence | unverified | Baseline typed `ShipContactDecisionExplanation`, `FactionAssignmentDecisionExplanation`, `FactionInvestigationDecisionExplanation`, `DefensiveCombatDecisionExplanation`, `ScheduledConsequenceTrace`; Logging changes serializes these after committed results, no separate diagnostic authority. |
| ADR-0008-R03 | noncompliant | unverified | Baseline raw interpolated `GameScreen.LogDiagnostic`; Logging changes generated `GameDiagnostics.LifecycleLog/FailureLog/PersistenceLog/ConsequenceLog` and `GameSimulation.ContactDecisionLog/FactionDecisionLog/CombatDecisionLog/CandidateLog/ConstraintLog`; `GameplayLoggingTests.RenderedAndStructuredFailuresExcludeUntrustedExceptions`, `SimulationStateAndPersistenceIgnoreProviderFailure`. Field assertions currently do not enumerate every event schema. |
| ADR-0008-R04 | unverified | unverified | Logging changes events carry `SimulationTimeMilliseconds` from Core occurrence/decision time, with separate Serilog wall timestamp and `SessionCorrelation`; `ConsequenceLog` uses `PlayerAdvanceEvent.OccurredAt`. No simulation decision reads logging timestamps. No new performance timing consumer. |
| ADR-0008-R05 | unverified | unverified | Logging changes lifecycle/selection/consequence Information, candidates/constraints Debug, classified unexpected operation failures Error. Expected command refusals remain typed outcomes. Source review; no complete severity-matrix test. |
| ADR-0008-R06 | compliant-with-evidence | unverified | Baseline typed actor-known explanations and AI policy tests; `LogContactDecision/LogFactionDecision/LogInvestigationDecision/LogCombatDecision` captures candidates, ranks/scores, reasons, constraints, ties and applicable randomness. No gameplay random source exists. Faction candidate reasons encode rejected constraints rather than omniscient world detail. |
| ADR-0008-R07 | noncompliant | unverified | GameplayLogging.CreateFactory uses invariant-culture console and JSON daily/size rolling files: 4 MiB, seven files, Information default. ProductionFileRolloverUsesActualSizeAndRetentionLimits exercises the shared production configuration, evicts oldest events, preserves newest and proves handle cleanup. Whole-event rollover can exceed the byte threshold by one event (E-LOG). |
| ADR-0008-R08 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No remote/additional hosted logging service admitted; local console/file/test sinks only. Offline canonical tests and game behavior do not require a collector. |
| ADR-0008-R09 | unverified | unverified | `GameSimulation.EmitDiagnostic` runs after Commit and catches provider failure; `GameDiagnostics.Emit`, `GameplayLogging.Create/GetLogger/Dispose/SafeFallback`; `ThrowingLoggerAndFallbackRemainOptional`, `StartupAndShutdownFailuresRemainOptional`, `SimulationStateAndPersistenceIgnoreProviderFailure`, `GamePersistenceLoggingTests.DeserializeRetainsLoggerWithoutChangingContinuation/FileLoadRetainsLogger`. |
| ADR-0008-R10 | noncompliant | unverified | `GD.PrintErr` forwards raw `exception.Message`. `GameDiagnostics.Classify/FailureLog` never forwards exception object/message/stack/data; `RenderedAndStructuredFailuresExcludeUntrustedExceptions`, `DefaultFileBackendProducesSafeStructuredOutputAndCloses` inspect structured event and rendered JSON; startup/provider/disposal fallback is bounded literal. |
| ADR-0008-R11 | unverified | unverified | No Baseline high-volume runtime logs. Logging changes Information default, explicit `GameScreen.EnableDecisionTraceDiagnostics` opt-in, Debug candidate/constraint emission; state/save equivalence with no-op/collecting/failing providers. No RNG is consumed by logging. |
| ADR-0008-R12 | compliant-with-evidence | unverified | `GamePersistence` explicit state DTOs and `OutputContainsOnlyExplicitPersistenceContract`; Logging changes logger excluded from captures/saves and emission occurs after Commit. Required orders, actor knowledge and outcomes remain typed state/results. No declared durable captain's-log feature. |
| ADR-0008-R13 | noncompliant | unverified | Baseline raw exception emission conflicts with sensitive-output rule. Reviewed amended Error reporting retains original exception in recovery flow but emits allowlisted classifications only. `GameDiagnostics.Classify` input/version/content/I/O/invariant/defect/cancellation classes; typed expected refusals. Not all classification branches have individual tests. |
| ADR-0008-R14 | noncompliant | unverified | 27 GameplayLoggingTests plus persistence logging tests cover actual rendered/structured redaction, provider/sink/construction/disposal failures, semantic results and continuation, levels, fields and retention. LongHorizonTestContext scopes six horizon cases with time, scheduler and bounded last-event context (E-LOG/E-RUNTIME). |
| ADR-0009-R01 | compliant-with-evidence | unverified | Baseline Core xUnit scenarios/persistence/content/AI tests; `GameplayShellTest.gd`, `IntegrationProbeTest.gd`, `GeneratedAssetImportTest.gd` handle engine wiring. Test changes adds pure headless architecture/property tests; Logging changes logging backend tests link engine-independent C# logging files into ordinary test project. |
| ADR-0009-R02 | compliant-with-evidence | unverified | `AlterCourse.Core.Tests.csproj` xUnit; named theories in `CombatFoundationTests`, `SimulationTimeTests`, content and persistence negatives. Test changes CsCheck adds generated spaces rather than replacing named regression cases. |
| ADR-0009-R03 | compliant-with-evidence | unverified | `EngineeringBackboneTests.AllocationThatWouldInvalidateCurrentSpeedIsAtomic/InvalidDirectAllocationPreservesCompleteAggregateIdentity/ZeroSensorAllocationCancelsExactScanAndReconcilesContactsAtSameTime`; `GameSimulationTests.AdvanceRejectsShipStepWorkOverBudgetWithoutMutation`; supported-version persistence continuation and AI refusal tests. New ratio/quantity regressions strengthen boundaries. |
| ADR-0009-R04 | noncompliant | unverified | FactionLongHorizonTests, M6CombatLongHorizonTests and ObservationResponseLongHorizonTests now use LongHorizonTestContext. A disposable deliberate assertion failure preserved the original assertion and fixture/replay/time/scheduler/last-four-event diagnostics; six clean horizon cases pass. No authoritative RNG exists, so no gameplay seed is invented (E-RUNTIME). |
| ADR-0009-R05 | noncompliant | unverified | Baseline example/independent decimal-oracle tests exist; package absence alone does not prove trigger violation. `GeneratedSimulationInvariantTests.SchedulerOrderingAndContinuationMatchListModel/BalancedPowerConservesBudgetAcrossGeneratedLoadouts/ShieldDamageConservesDamageAndMonotonicallyProtects`; CsCheck 4.9.1, 256 individually seeded single-iteration cases, one thread, no wall cutoff; independent decimal share/capacity oracles and complete-sequence replay; `GeneratedFailureReportsCounterexampleAndReplays` verifies failure replay/shrinking. |
| ADR-0009-R06 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No discovered significant generated product defect in supplied new property evidence. Intentional library failure test verifies diagnostics and is not a product counterexample. A real future counterexample triggers named regression admission. |
| ADR-0009-R07 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | Current engine fixtures naturally GDScript backed by vendored GdUnit4. No new C# test subject requiring Godot identified; linked logging classes are engine-independent. GdUnit4Net absence is not by itself a violation. |
| ADR-0009-R08 | compliant-with-evidence | unverified | `IntegrationProbeTest.gd.test_csharp_node_enters_the_scene_tree`, `GeneratedAssetImportTest.gd`, `GameplayShellTest.gd` are engine/addon fixtures; production gameplay remains C#. |
| ADR-0009-R09 | unverified | unverified | Baseline project graph enforces Core→no Godot; `ArchitectureBoundaryTests` attempts actor-safe signatures/immutability. Test changes fixes indirect reachability, `ActorSafetyWalkDetectsIdentityAndPersistenceReachedIndirectly/OwnIdentityExemptionMatchesALiveScannedIdentityMember`; architecture IL checks complement project graph. |
| ADR-0009-R10 | noncompliant | unverified | Baseline durable non-project-graph actor-safe dependency rules already existed without selected ArchUnitNET. Test changes admits 0.13.4; `DependencyArchitectureTests.CoreHasNoForbiddenDependencies/CoreOutsidePersistenceDoesNotDependOnPersistence/PureCoreDoesNotDependOnLogging/NamespaceRuleDetectsAnIntentionalViolation/RuleDetectsActualLoggingPackageDependencies`; specialized actor-safe/legacy IL walkers retained for narrower semantic rules. |
| ADR-0009-R11 | compliant-with-evidence | unverified | Baseline explicit `SimulationTime/SimulationDuration`, real worlds/in-memory schedulers and temp-file persistence; Logging changes capturing/throwing logger boundary doubles; Test changes independent scheduler list model. No global clock/RNG patches introduced. |
| ADR-0009-R12 | compliant-with-evidence | unverified | Baseline versioned persistence fixture helpers and `GamePersistenceTests.MigratesV1WithActiveWorkTransitivelyToV4/ReloadedPluralWorldContinuesLikeUninterruptedWorld/RejectsMalformedPluralIdentityAndReferenceGraphs/RejectsMalformedOrTruncatedJson/RejectsNonfiniteTacticalValuesAndInvalidKind`; exact bytes are continuation/wire proofs, semantic state otherwise. Reviewed persistence leg retains historical fixtures. |
| ADR-0009-R13 | compliant-with-evidence | unverified | Baseline baseline passed supplied canonical environment; deterministic fixture inputs and UnixEpoch save metadata. Test changes explicit CsCheck seed/single thread, no hosted dependency; Logging changes temp directories isolated. This is source/gate evidence, not a comprehensive multi-culture/timezone matrix. |
| ADR-0009-R14 | compliant-with-evidence | unverified | New tests use local sinks and unique temp directories, generators use one thread; no new shared process-global mutations. Baseline canonical passed. No flaky retry policy introduced. Whole-suite final isolation still pending integrated gate. |
| ADR-0009-R15 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | Existing horizon tests measure bounded work/save growth without claiming CPU performance or freezing all tuning. No new noisy benchmark/balance distribution consumer. UnitsNet evaluation explicitly lacks comparative hot-path benchmark. |
| ADR-0009-R16 | compliant-with-evidence | unverified | `scripts/test-mutation.sh`, `stryker-config.json`: separate deep Core mutation command, no invented threshold. Separate scoped deep validation remains pending; no mutation result is inferred from the canonical gate. |
| ADR-0009-R17 | unverified | unverified | Baseline canonical passed despite the revalidated ADR gaps. Selected dependency rules, isolated negative probes, generated independent oracles, diagnostics and targeted regressions strengthen actual coverage. Final exact-revision gate remains separately required (E-GATE). |
| ADR-0010-R01 | compliant-with-evidence | unverified | Core FactionAssignmentPolicy/FactionInvestigationPolicy/CautiousContactDecisionPolicy/DefensiveCombatDecisionPolicy (E-AI). |
| ADR-0010-R02 | compliant-with-evidence | unverified | Bounded immutable inputs/candidates/reasons, stable ties, typed proposal/no action/explanation (E-AI). |
| ADR-0010-R03 | compliant-with-evidence | unverified | Policies evaluate immutable values; proposals apply through ordinary validated travel/combat commands (E-AI). |
| ADR-0010-R04 | compliant-with-evidence | unverified | FactionKnowledgeBoundaryTests.HiddenWorldAndOwnShipSensorStateDoNotChangeDecisionOrExplanation; topology is approved common knowledge (E-AI). |
| ADR-0010-R05 | compliant-with-evidence | unverified | Typed explanation objects retain known facts/candidates/reasons/tie/outcome; RandomnessUsed=false (E-AI/E-LOG). |
| ADR-0010-R06 | compliant-with-evidence | unverified | Bounded inputs/candidate search, scheduler budgets, duration/identity tie rules and rejected-command preservation (E-AI/E-RUNTIME). |
| ADR-0010-R07 | compliant-with-evidence | unverified | Core defensive/contact policies and typed fire/course application; hidden receiver metamorphic tests (E-AI). |
| ADR-0010-R08 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No state-machine package trigger demonstrated; explicit domain state remains validated (E-DEPS). |
| ADR-0010-R09 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No strategic behavior-tree/LimboAI consumer; future adoption needs stated proof (E-DEPS). |
| ADR-0010-R10 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No mission-script/narrative runtime consumer; ordinary bootstrap is not this trigger (E-NARRATIVE). |
| ADR-0010-R11 | compliant-with-evidence | unverified | Offline ordinary Core C# policies; no LLM authority dependency or runtime service call (E-AI/E-ARCH). |
| ADR-0010-R12 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No optional authoritative-adjacent LLM consumer; development AssetCtl generation does not adjudicate gameplay (E-DEPS). |
| ADR-0010-R13 | compliant-with-evidence | unverified | Core policy/hidden-truth/replay/explanation/refusal/scenario/horizon tests and architecture checks (E-AI/E-RUNTIME). |
| ADR-0011-R01 | unverified | unverified | Quantity changes classifies physical heading/speed/duration, signed coordinate composite; Runtime changes consequential demand satisfaction/capability, bounded `SystemCondition`; counts and fictional `PowerUnits`; validated normalized damage tuning distinct from physical energy. No blanket every-number wrapping. |
| ADR-0011-R02 | noncompliant | unverified | `TacticalProjection.HeadingDegrees/SpeedKilometersPerSecond` doubles and `TacticalPosition.Advance(...double seconds)`. Quantity changes typed public properties and internal typed duration; reflection `TacticalProjectionPreservesTypedQuantityBoundary`, `TacticalMotionConvertsMillisecondsToSeconds`. Logging changes owns matching main-source/Godot callers. |
| ADR-0011-R03 | noncompliant | unverified | `ShipEngineeringState.PowerSatisfaction/Capability` public doubles and `ShieldDamage.Resolve(...double powerSatisfaction)` feed consequential motion/sensing/combat. `PowerSatisfactionRatio`, `SystemCapability`, typed `CombatOwnFacts.WeaponCapability`, explicit arithmetic/display extraction; `PowerSatisfactionUsesBoundedTypeAcrossSubsystems`, `ConsequentialRatiosRejectInvalidValues/ConsequentialRatiosPreserveLegalValues`. Sensor display progress remains normalized read-only snapshot. |
| ADR-0011-R04 | compliant-with-evidence | unverified | `PowerUnits` bounded integer abstraction, `SystemCondition` independent rules; no warp/energy/canon conversion invented. `EngineeringBackboneTests.PowerUnitsAdditionIsCheckedAndBounded/SystemConditionRejectsNonfiniteAndOutsideBounds`. Normalized damage is explicitly validated scalar gameplay output/tuning, not SI energy. |
| ADR-0011-R05 | compliant-with-evidence | unverified | Baseline counts/IDs use bounded integral objects; `DirectedEnergyWeaponDefinition` validates named `BaseNormalizedDamage` (0,1], cooldown and range; `ShieldDamage.ValidateNormalized` validates damage. Local formula intermediates stay doubles; no tuning changes. |
| ADR-0011-R06 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No UnitsNet production package. `docs/dependency-admission/unitsnet-evaluation.md`, `.workflow/units-proof.log`: exact 5.75.1 .NET8 probe exit0, conversions/overflow/nonfinite/arithmetic, precise integer-time mismatch. Complete production admission proof not claimed: no comparative performance or Godot export experiment. |
| ADR-0011-R07 | unverified | owner-approved exception | Historical suitability/rejection proof absent in searched refs. Owner-approved ADR amendment permits existing canonical distance/speed wrappers; evaluation does not falsely reject library length/speed. Future dimensions/conversion requirements repeat selected-candidate proof and central admission. Missing historical provenance remains recorded. |
| ADR-0011-R08 | compliant-with-evidence | unverified | JSON schemas/DTO fields `xKilometers/yKilometers/headingDegrees/speedKilometersPerSecond/timeMilliseconds`, authored sensor/weapon range and cooldown explicit units. Quantity changes introduce no canonical-unit/schema migration or wire representation change; `RoundTripsMaximumTacticalSpeed/OutputContainsOnlyExplicitPersistenceContract`. |
| ADR-0011-R09 | unverified | unverified | Baseline command intents already typed; Quantity changes removes projection and duration primitive leaks. Content loaders construct `DistanceKilometers/SpeedKilometersPerSecond/SimulationDuration` after validation; persistence maps numeric wire forms to typed runtime. `RejectsNonfiniteTacticalValuesAndInvalidKind`, supported-version continuation. |
| ADR-0011-R10 | compliant-with-evidence | unverified | No UnitsNet types/enum ordinals/text enter current DTOs; explicit persistence mapping excludes logger/service/Godot types (`OutputContainsOnlyExplicitPersistenceContract`). Quantity changes wrappers are derived runtime values; numeric saves unchanged. |
| ADR-0011-R11 | unverified | unverified | `TacticalMapView.PixelsPerKilometer=18`, origin at viewport center and north-positive Y inverted for screen; Core coordinates retained. Quantity changes presenter `.Value`, Logging changes GameScreen spinbox/metadata `.Value`, typed course intents. `GameSimulationTests.TacticalHeadingUsesClockwiseDegreesFromNorth`; `GameplayShellTest.gd.test_live_course_inputs_apply_stop_and_preserve_draft_on_refresh/test_public_controls_approach_identify_stop_allocate_and_fire_with_stable_selection`. Scale/orientation source reviewed; existing `GameplayShellTest` map projection cases assert the 18 pixels/km scale, viewport origin, north-positive inversion and fractional positions. |
| ADR-0011-R12 | noncompliant | unverified | Baseline finite/nonnegative physical constructors, bounded condition/power, checked integer duration/time. `ExtremeFiniteHeadingsRemainInCanonicalRange/TinyNegativeHeadingNormalizesBelowFullTurn/NegativeZeroFoldsToPositiveZero/TacticalMotionRejectsOverflowingCoordinates/TacticalPositionRejectsNonfiniteCoordinates`; `SimulationTimeTests.TimeAndDurationAdvancementRejectOverflow`; Runtime changes validates both ratio types/default/negative zero. |
| ADR-0011-R13 | compliant-with-evidence | unverified | `SystemRepairState.ProgressAt` explicitly elapsed milliseconds / completion interval; `ConditionAt` interpolate bounded conditions. `ShipEngineeringState.Capability` condition×typed demand satisfaction; shield/damage/power allocation remain named gameplay operations. Conversion does not hide rules. |
| ADR-0011-R14 | compliant-with-evidence | unverified | Ship/system schemas plus `SystemDefinitionCatalogLoader` finite/range/cross-reference validation; `CombatFoundationTests.InvalidWeaponTuningIsRejected`, content semantic negatives, persistence nonfinite values. No alternate-unit parser or unsupported-unit-string input introduced. |
| ADR-0011-R15 | unverified | unverified | Reviewed aligns weak Confirmation wording with typed boundary obligations; Quantity changes reflection/validation/conversion/overflow tests; numeric wire remains independent. UnitsNet performance/export proof explicitly unperformed/inapplicable to current no-adoption exception. Parent integrated canonical and Godot proof required. |
| ADR-0012-R01 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R01 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0012-R02 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R02 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0012-R03 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R03 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0012-R04 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R04 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0012-R05 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R05 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0012-R06 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R06 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0012-R07 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R07 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0012-R08 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R08 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0012-R09 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R09 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0012-R10 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R10 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0012-R11 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R11 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0012-R12 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R12 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0012-R13 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No admitted narrative runtime/assembly/source; current linear reports/hail are explicitly exempt. R13 activates with its declared narrative consumer (E-NARRATIVE). |
| ADR-0013-R01 | compliant-with-evidence | unverified | E-GOV repository receipt: default dev; classic protection receipts block deletion/force. `branch-policy.sh` topology/ancestry checks and hotfix main ancestry; baseline local refs reviewed. No new permanent branch. |
| ADR-0013-R02 | noncompliant | unverified | Baseline numbered-name policy contradicts allowed issue-less Standalone admission. Governance changes ADR amendment + `standalone_pattern` + package-gated dev-only route; `test-branch-policy.sh` positive standalone syntax, invalid uppercase/hyphen, wrong-target and missing-contract negatives. Receipt `delete_branch_on_merge=true` supports automatic deletion policy, not deletion of every historical topic. |
| ADR-0013-R03 | compliant-with-evidence | unverified | `branch-policy.sh` main approved dev/hotfix sources and pre-push rejects routine direct main pushes. Reviewed main first-parent post-baseline history contains release merges only; Governance changes refuses standalone→main. No standing release branch admitted. |
| ADR-0013-R04 | noncompliant | unverified | Significant current work governed by Issue #127 and Supporting draft PR #132 (parent evidence). `check-standalone-admission.sh` extracts trusted base package/policy/schema, checks receipt relationship and explicit Ready verdict, brackets head/body snapshots; low-risk semantic judgment remains owner responsibility. |
| ADR-0013-R05 | compliant-with-evidence | unverified | `branch-policy.sh` direct-range classes reject code/config/enforcement/normative edits; Governance changes standalone still PR-mediated with package contract. Current implementation branches have no direct-admission trailers; publication uses governed Supporting PR #132. |
| ADR-0013-R06 | compliant-with-evidence | unverified | `.githooks/commit-msg/pre-push`, `branch-policy.sh` T0/Handoff trailer/path checks; `test-branch-policy.sh` allowed handoff, mixed scope, invalid trailer/merge negatives. No new direct Standalone class. |
| ADR-0013-R07 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No GitHub-workflow package upgrade in this fix. Explicit ADR Handoff exception retained; a future upgrade triggers reevaluation. |
| ADR-0013-R08 | compliant-with-evidence | unverified | Live repo allows squash+merge, disables rebase; `branch-policy.sh` conventional subject/title types, topic squash contract. Construction commits inspected use Conventional Commit form. Live setting alone does not force purpose-specific merge choice; wrapper/policy owns that. |
| ADR-0013-R09 | compliant-with-evidence | unverified | Main first-parent release subjects `chore(release): v0.1.0` through `v0.6.2`, all tag targets match release commits. `test-branch-policy.sh` exact release, hotfix fix, sync titles and topology positive/negative cases. No active hotfix observed. |
| ADR-0013-R10 | compliant-with-evidence | unverified | Historical baseline `5028501ba607045b6b302fd62957f140ce65fcbc`, exact `chore(baseline): establish main baseline (#23)`, no release tag; policy tests reject repeated ADR-present baseline and fork bypass. Exception not reusable after adoption. Historical admission closed; no current reuse permission. |
| ADR-0013-R11 | compliant-with-evidence | unverified | E-GOV authenticated live reads prove strict Canonical verification and Branch policy, conversation resolution, zero required approvals, no force/deletion, main administrator enforcement and dev owner exception; active tag rule denies updates/deletion with no bypass. Effective branch ruleset arrays are empty because classic protections apply. |
| ADR-0013-R12 | compliant-with-evidence | unverified | Local core.hooksPath=.githooks; tracked commit/pre-push hooks and setup remain. Complete branch-policy fixtures exercise trusted-base package receipt/Ready validation, topology and hostile metadata. Hosted candidate receipts verify the actual PR route (E-GOV/E-GATE). |
| ADR-0013-R13 | compliant-with-evidence | unverified | ADR explicitly documents owner bypass/local hook skip residual risk and lack of path-conditioned GitHub actor bypass. Governance changes keeps owner/reviewer semantic low-risk responsibility; no claim server-perfect admission. |
| ADR-0013-R14 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No current release action in this conformance change. Historical release Task/PR notes available under `.workflow/release-*`; live immutable records and exact merge/tag ancestry verified. Brief freeze is operational owner evidence, not derivable from tag alone. |
| ADR-0013-R15 | compliant-with-evidence | unverified | Reviewed release receipts contain v0.1.0,v0.2.0,v0.3.0,v0.4.0,v0.5.0,v0.6.0,v0.6.1,v0.6.2 all immutable=true/non-draft. Local annotated tags peel respectively to cd8ab324,163b8e22,fae21bd8,b3b66354,0547d061,d00460ea,f0af2653,255eaedc: every post-baseline main merge matches. API target_commitish dev is not used as tag-target proof. All versions0.x; no embedded-version/artifact/changelog consumer asserted. |
| ADR-0013-R16 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No active/historical post-baseline hotfix main merge in inspected first-parent history. Hotfix main ancestry and sync-route title/method covered by policy fixtures; no operational synchronization claim invented. |
| ADR-0013-R17 | not-applicable | not-applicable | Administrative receipt reads succeeded (E-GOV); no protection mutation/recovery performed here. ADR records narrow credential-backed temporary recovery, mandatory restore/live verification; no ordinary workflow bypass authorized. |
| ADR-0013-R18 | compliant-with-evidence | unverified | Baseline hook path+parent rexec canonical PASS; policy fixtures and live repo/protection/release receipts. Governance changes new route requires leg checks and final canonical; current governing PR's actual Branch policy/Canonical CI and Ready/merge receipts are not yet final evidence. |
| ADR-0014-R01 | compliant-with-evidence | unverified | ShipSystemKind/SystemDefinitionId/InstalledSystemId distinct; five production kinds preserved (E-SUBSTRATE). |
| ADR-0014-R02 | compliant-with-evidence | unverified | InstalledSystemIdAllocator restore and explicit bootstrap IDs; nondefault ID/allocator continuation tests (E-SUBSTRATE/E-SAVE). |
| ADR-0014-R03 | compliant-with-evidence | unverified | System definitions own equipment; class loadout bootstrap vs independent live installations (E-SUBSTRATE). |
| ADR-0014-R04 | compliant-with-evidence | unverified | InstalledSystemCollection canonical common state; optional allocation/capabilities; derived views not duplicate authority (E-SUBSTRATE). |
| ADR-0014-R05 | compliant-with-evidence | unverified | ShipSystemAdmission separate common multiplicity and current zero-or-one typed capability admission (E-SUBSTRATE). |
| ADR-0014-R06 | compliant-with-evidence | unverified | HeterogeneousBootstrapTests same-design divergence, immutable collection and save preservation (E-SUBSTRATE). |
| ADR-0014-R07 | deferred-by-explicit-trigger | deferred-by-explicit-trigger | No player refit/install/remove transition; future atomic modification admission retained (E-SUBSTRATE). |
| ADR-0014-R08 | compliant-with-evidence | unverified | CommonOrder then InstalledSystemId; semantic compatibility descriptor includes consequential order (E-SUBSTRATE). |
| ADR-0014-R09 | compliant-with-evidence | unverified | Capability-derived damage/repair participation and exact installed-ID work; typed specialized consequences (E-SUBSTRATE). |
| ADR-0014-R10 | compliant-with-evidence | unverified | Generic consumer allocation; SubstrateExtensionTests alternate demand through installations (E-SUBSTRATE). |
| ADR-0014-R11 | compliant-with-evidence | unverified | RemoteKnowledgeProjectionsCannotReachInstalledLoadout + HiddenLoadoutTargetingTests (E-SUBSTRATE/E-AI). |
| ADR-0014-R12 | compliant-with-evidence | unverified | CaptureEngineeringV10 directly captures actual installations/allocator; active repair/scan/cooldown continuation (E-SAVE). |
| ADR-0014-R13 | compliant-with-evidence | unverified | MigrateV9ToV10 frozen map and V10CompatibilityTests/V10SubstrateTests strict negatives (E-SAVE). |
| ADR-0014-R14 | compliant-with-evidence | unverified | Ordered current own rows, installed-ID repair/priority keys and validated Core commands (E-UI/E-SUBSTRATE). |
| ADR-0014-R15 | compliant-with-evidence | unverified | M6ABaselineCharacterizationTests and continuation preserve approved five-kind/four-consumer semantics (E-SUBSTRATE). |
| ADR-0014-R16 | compliant-with-evidence | unverified | SubstrateExtensionTests unchanged common algorithms plus paired architecture negatives (E-SUBSTRATE/E-ARCH). |
| ADR-0014-R17 | compliant-with-evidence | unverified | ShipSystemIsolationTests foreign IDs, explicit-empty bootstrap, multiplicity/cardinality distinction (E-SUBSTRATE). |
| ADR-0014-R18 | compliant-with-evidence | unverified | Exact consumer keys/bounded sums/remainder order/Balanced versus brownout; voluntary rejection and exact equal-time cancellation (E-SUBSTRATE). |
| ADR-0014-R19 | compliant-with-evidence | unverified | V10 scan/readiness identities and scheduler closed Ship/Faction targets/budgets (E-SAVE/E-RUNTIME). |
| ADR-0014-R20 | compliant-with-evidence | unverified | HiddenLoadoutTargetingTests compare same knowable inputs; absent receiver discards residual, no hull/redirection invention (E-AI). |
| ADR-0014-R21 | compliant-with-evidence | unverified | Frozen descriptors/direct current capture and CompatibleClassDefaultChangeLeavesEverySavedInstallationAndContinuationIntact (E-SAVE). |
| ADR-0014-R22 | compliant-with-evidence | unverified | MaximumWidthReportWorldFitsCompactSaveEnvelope: measured109030603/conservative114536452 bytes,128MiB retained (E-SAVE). |
| ADR-0014-R23 | compliant-with-evidence | unverified | OwnShipActionBinding owner/generation/fresh payload; stale load/focus/absent installation Godot tests (E-UI). |
| ADR-0014-R24 | compliant-with-evidence | unverified | Baseline characterization, extension/isolation/absence/hidden pairs, exact continuation, high-width and horizon/Godot suites (E-SUBSTRATE/E-GATE). |

### Evidence index

Full paths are repository-relative. Grouped Core symbols live under `src/AlterCourse.Core/`, and grouped headless test classes under `tests/AlterCourse.Core.Tests/`; Godot paths are identified separately. Named tests are methods or test classes in those suites. The baseline canonical receipt executes the unchanged baseline tests; the final candidate receipt must execute both those tests and the new regressions. These are scoped behavioral witnesses and source traces, not claims of exhaustive mathematical certification.

- **E-GATE:** `scripts/verify.sh`, `.github/workflows/verify.yml`, central build/tool pins and `scripts/check-policy.sh`; canonical local and hosted receipts below own the exact revisions/results. Managed frontmatter/Markdown and handoff checks are separate.
- **E-ARCH:** `tests/AlterCourse.Core.Tests/ArchitectureBoundaryTests.cs`, `DependencyArchitectureTests.cs`, `SubstrateConformanceTests.cs`, `Support/ArchitectureProbes/`; `scripts/test-core-boundaries.sh` compiles 31 separate forbidden API uses expecting RS0030 and legal Core/nonauthoritative fixtures. The same ArchUnitNET rule factory accepts valid and rejects isolated invalid dependencies, including real logging assemblies. Specialized actor-safe reachability and frozen-migration checks remain; the own-identity exemption is checked against a live scanned member.
- **E-DEPS:** `Directory.Packages.props`, project manifests/lockfiles, `docs/dependency-admission/{jsonschema-net-core,assetctl,gameplay-logging,architecture-testing,unitsnet-evaluation}.md`, and `docs/development-quality.md`. Existing analyzers/xUnit/toolchain are selected by ADRs 0002/0009; schema/AssetCtl have existing admission records. New role expansion and test packages receive current records. GdUnit4 was originally vendored in `9cc4ab86b11d33476fceb4761fa3bf036efad03b`, before ADR 0003 adoption in `d1129728d9c1b0dd4fd01d0f9772024b43fce31e`; immutable upstream and MIT license are recorded, historical vendoring rationale was not found.
- **E-CONTENT:** `src/AlterCourse.Core/Content/{StrictContentJson,ShipDefinitionCatalogLoader,SystemDefinitionCatalogLoader,FactionDefinitionCatalogLoader}.cs`; `tests/AlterCourse.Core.Tests/Content/` loader and loadout suites; `schemas/`. Tests cover strict UTF8/JSON, duplicate/unknown members, versions, bytes/counts/strings, reference/capability/cardinality admission and production catalogs. Faction explicit parse settings preserve the prior strict defaults.
- **E-SAVE:** `src/AlterCourse.Core/Persistence/GamePersistence.cs`, `.Bounds.cs`, `.V10.cs`, `SaveFileOperations.cs`; `tests/AlterCourse.Core.Tests/Persistence/` supported-version, V10 compatibility/substrate, admission/collection/string/write-failure/logging suites; `Gameplay/HeterogeneousLoadoutContinuationTests.cs`. Current capture uses actual installations directly; frozen historical DTO reuse is not itself lossy. Fault tests preserve an existing loadable slot and original typed failure; same-filesystem replacement visibility is distinct from crash/power-loss durability.
- **E-RUNTIME:** `Simulation/SimulationScheduler.cs`, `Gameplay/GameSimulation.cs`, `AI/CautiousContactDecisionPolicy.cs`; scheduler/time/contact/transaction tests, `GeneratedSimulationInvariantTests`, `Gameplay/{FactionLongHorizonTests,M6CombatLongHorizonTests,ObservationResponseLongHorizonTests}.cs`. Call-path review follows fixed-step/event advancement through navigation, scans, repair, damage and policy choices. No authoritative RNG consumer exists; filesystem temporary names, logging correlation and CsCheck generation are nonauthoritative.
- **E-AI:** Core `AI/` policies and typed inputs/explanations; `Gameplay/GameSimulation.{Factions,Observation,Combat}.cs`; `tests/AlterCourse.Core.Tests/AI/`, `Gameplay/HiddenLoadoutTargetingTests.cs` and faction knowledge-boundary tests. Paired hidden-truth tests retain legitimate actor inputs and compare decisions/projections; private victim consequences may differ.
- **E-SUBSTRATE:** `Ships/{InstalledSystemCollection,ShipSystemAdmission,ShipEngineeringState,InstalledSystemIdAllocator}.cs`; `tests/AlterCourse.Core.Tests/SubstrateExtensionTests.cs`, `tests/AlterCourse.Core.Tests/Gameplay/ShipSystemIsolationTests.cs`, heterogeneous bootstrap/continuation, `Characterization/M6ABaselineCharacterizationTests.cs`, architectural negative fixtures and high-width persistence/horizon tests. Current zero-or-one specialized behavior is an explicit bounded admission, not a failure of extensible common storage.
- **E-QUANTITY:** Core `Quantities/`, `Tactical/TacticalPosition.cs`, `Player/TacticalProjection.cs`, `Ships/{PowerSatisfactionRatio,SystemCapability,ShieldDamage,ShipEngineeringState}.cs`, `Gameplay/CombatOwnFacts.cs`; `Quantities/PhysicalQuantityTests.cs`, `Simulation/SimulationTimeTests.cs`, `Ships/{CombatFoundationTests,EngineeringBackboneTests}.cs`, generated conservation and AI/characterization tests. Numeric wire units are unchanged.
- **E-LOG:** `src/AlterCourse.Godot/src/Gameplay/Logging/{GameplayLogging,GameDiagnostics}.cs`, `GameScreen.cs`, Core `Gameplay/GameSimulation.Diagnostics.cs` and persistence logger plumbing; `GameplayLoggingTests.cs` and `Persistence/GamePersistenceLoggingTests.cs`. Proof inspects rendered JSON and structured fields, actual default backend, no-op/collecting/failing providers/sinks and lifecycle failures, serialized state and continuation.
- **E-UI:** `src/AlterCourse.Godot/src/Gameplay/{GameScreen,OwnShipActionBinding,CommandInterfacePresenter,TacticalMapView,EngineeringKindPresentation}.cs`; Godot `tests/{GameplayShellTest,IntegrationProbeTest,GeneratedAssetImportTest}.gd` and `SmokeRunner.tscn`. Core definitions load without presentation; absent kind presentation has controlled fallback. Global theme/font scene resources are outside ADR 0005's domain-definition contract, so their hypothetical absence is not a discovered violation of that ADR.
- **E-NARRATIVE:** `docs/wiki/architecture.md`, `diplomacy-economy-and-campaigns.md`, current solution/package/content inventory and reciprocal ADR 0005/0012 metadata. No Narrative assembly/runtime or specialized source is admitted; current linear reports/hail remain exempt.
- **E-DEEP:** `scripts/test-mutation.sh` and `stryker-config.json` own separate mutation validation. No score threshold or blanket mutation adequacy is inferred from canonical success; actual scoped run results and survivor dispositions must be recorded separately.

## Overlap, conflicts, and bounded resolutions

1. **ADR 0006/0007 RNG — envelope wording versus present consumer scope.** ADR 0006 “Save envelope” lists random metadata without a qualifier; ADR 0007 governs consumed randomness. `world-navigation-and-time.md#randomness-and-future-scale`, decision D-13/D-19, and open question Q-14 explicitly describe no stochastic policy/current algorithm. Treat current bans and deterministic continuation as current requirements; generator implementation/full stream state becomes triggered by a real authoritative random consumer. ADRs 0006/0007 now make that applicability explicit. E-RUNTIME traces the absence; 31 compiler negatives guard prohibited ambient sources.

2. **ADR 0009 tool selections — baseline versus trigger.** CsCheck has explicit meaningful-generator/invariant trigger; ArchUnitNET has explicit first non-project-graph rule trigger. GdUnit4Net is selected specifically for C# engine tests; GDScript engine fixtures are explicitly permitted. Existing durable namespace rules triggered ArchUnitNET; scheduler, power and damage invariants triggered CsCheck. E-DEPS records selected admission and E-ARCH records intentional-violation proof.

3. **ADR 0011 body versus confirmation.** “Standard physical quantity” and “Domain and persistence boundaries” require strong types; “Confirmation” says “typed or explicitly named quantities.” Do not weaken body obligations into naming-only conformance. The authorized F04 amendment aligns confirmation with these classifications; consequential typed boundaries were corrected without changing wire units.

4. **ADR 0011 UnitsNet proof versus library preference.** Preferred candidate is not adoption without proof. Existing project-owned types are not evidence that the required historical proof occurred. F06 searched available refs and recorded a present-day evaluation; the owner-approved exception remains visible in ADR-0011-R07.

5. **ADR 0013 Standalone versus mandatory numbered branches.** Admission allows eligible issue-less Standalone PRs; branch-name text requires issue number for every named topic family. This is a real internal policy gap. Preserve package eligibility and mandatory PR admission; clarify a narrowly admitted Standalone naming route with full topology/positive/negative proof. Do not convert all issue-less work into significant work or bypass branch enforcement.

6. **ADR 0013 versus generic package direct-admission rules.** Handoff is an explicit narrow repository exception, not accidental drift. Standalone does not authorize direct push. T0/Handoff cannot touch ADR/wiki/normative/enforcement material. Package upgrade requires reevaluation.

7. **ADR 0014 versus native-first/no speculative framework policy.** Common substrate has five concrete consumers and explicit owner direction; it is a justified domain abstraction under ADR 0003, not authorization for ECS or generic executable components.

8. **ADR 0014 common multiplicity versus typed gameplay limits.** Common storage supports multiple same-kind entries; current world admission may separately reject unsupported aggregation. Requiring implemented multi-generator/shield/weapon gameplay would expand scope.

9. **ADR 0014 hidden-loadout safety versus absent-kind error.** Detailed wiki contract explicitly supersedes absence-specific remote rejection. Own commands use actual IDs; remote aim remains semantic/observer-local, with normal discharge and unconfirmed receiver effects. This is already settled design.

10. **Architecture confirmation and test tool ownership.** Compile-time references, analyzer bans, reflection-based smoke tests, and selected ArchUnitNET have different scopes. A single passing gate does not prove every durable relation, and duplicated string searches are not substrate architectural proof.

11. **Observability versus determinism/persistence/AI.** Typed explanation is rule output, logging is optional serialization. Failing/no-op logging must preserve Core semantics, and actor-known diagnostics must not become player-facing omniscient projections or a second persistence authority.

12. **Documentation discrepancy found.** The baseline Engineering source note incorrectly described remaining Godot presentation work. The corrected wiki now agrees with implemented generic Engineering status. A later stale forthcoming-string-validation note was also reconciled with the implemented bounds. No separate gameplay change is implied.

## Coverage and unresolved owner choices

Obligation extraction covers ADRs 0001–0014. Authority/trigger evidence read from wiki home, architecture, development/governance, source-catalog review record, world/navigation/time, content/persistence, ship-system substrate, and open questions; decision/status/Engineering headings and relevant statements were searched.

Historical dependency provenance is limited to available refs and recorded reviews. This evidence does not certify legal clearance, every filesystem's crash durability, every platform, all possible inputs, or a manual graphical playtest. Final executable status remains governed by the revision-specific receipts.

No new owner choice is needed merely to extract this register. Deferred consequential choices remain explicitly deferred: RNG algorithm, broader compatibility promises, narrative consumer/time semantics, modding, refit/recovery/aggregation gameplay, fuller spatial/knowledge/political systems, and release/artifact readiness. They must not be silently resolved during conformance remediation.

## Additional findings and regression history

All reproduced implementation findings below have high confidence. They were corrected toward existing ADR/wiki requirements; no additional gameplay policy was selected.

| Finding | Baseline or candidate defect and impact | Correction and actual evidence |
| --- | --- | --- |
| F07 | V5/V6 null outstanding work escaped as NullReferenceException; save collections/strings could allocate before the intended admission bound. | `44abf79`, `11f19b6`: early strict prescan and typed invalid-data rejection. Original null suite had three failures/two passes; 21 string cases failed before correction. Subsequent persistence suite passed 352 cases. |
| F08 | Extreme finite contact coordinates overflowed subtraction, selecting an incorrect withdrawal heading (225 instead of approximately 233.130102 degrees). | `2d40a19`: scale only the overflow case; three extreme/ordinary regressions and 15 contact-policy cases pass. The source defect and post-fix result are retained; no separately preserved pre-fix execution receipt is claimed. |
| F09 | Faction parser relied on implicit strict defaults; malformed Unicode in names/values escaped content admission as raw InvalidOperationException. | `2220fb3`: explicit parse options and validated Unicode before schema/DTO work. Four malformed-Unicode cases failed for the expected exception mismatch before correction; final content/physical suite passed 183 cases. Existing strict-default behavior was characterized, not falsely reported as unsafe acceptance. |
| F10 | Oversized/invalid JSON member names and eagerly concatenated paths amplified allocation or escaped as untyped decoder failures. | `4da680d`: bounded encoded names, strict decoder handling and lazy diagnostic paths. A 6 KiB malicious input allocated 8,522,816 bytes before the fix; the isolated lazy-path regression measured 873,168 bytes against a 512 KiB cap. Supported escaped names and frozen DTO inventory remain admitted; 352 persistence cases pass. |
| F11 | First candidate architecture rules ignored unloaded target types; conservation-only properties accepted greedy allocation or zero shield absorption; CsCheck's single seed did not fix later samples. | `9f9552e`: include referenced targets, cover every pure Core namespace, use independent decimal oracles and 256 full-state seeds. Actual unloaded-target RED plus three disposable wrong-algorithm/seed REDs; 26 final focused cases pass. Failure-only shrinking may choose a different minimized input, but the reported seed replays that invariant. |
| F12 | A Godot refresh failure after an authoritative command/save/load could report the operation as safely failed; synthetic advancement returned zero after time advanced. | `493c64a`: distinguish committed outcomes from presentation failure. Six freed-control regressions; 92 gameplay engine cases pass. Initial bootstrap readiness remains fail-closed before play. |
| F13 | Ambient API bans omitted additional clock/timer entry points. | `2011448` and `2220fb3`: 31 independent forbidden compile cases require RS0030; supplied timestamps and nonauthoritative fixture operations remain accepted. The canonical verifier invokes the probe script. |
| F14 | Write/flush failure followed by stream-close failure replaced the primary exception, although the old slot survived. | `1d50ddc`: preserve the original failure during secondary IO/access cleanup, while a lone close failure still prevents replacement. Pre-fix: two expected Assert.Same failures/eight passes. Post-fix integrated suite: 63 passes including all ten save-failure cases, 26 architecture/generated cases and 27 logging cases. |

No supported fixture was regenerated to match new output. V10 writer units, supported V1–V10 interpretation, current five-kind gameplay and Core authority remain unchanged.

## Independent review dispositions

The explicitly invoked `cross-agent` skill (`/home/chris/.codex/skills/cross-agent/SKILL.md`) ran four bounded Claude reviews with saved-login preflight and read-only tool permissions. That is tool-level containment, not operating-system isolation. Source drift flags during parallel integration are retained as a limitation: each review covers its supplied source, and later fixes require separate verification. Raw private review packets are not committed.

| Review | Actual finding and disposition |
| --- | --- |
| Claude governance `1d361aea-ede7-432c-928b-511932ee80d4` | Confirmed package receipt plus explicit Ready check is the authority; recommended trusted base-SHA package extraction, now implemented. Refuted the initial claim that the package necessarily blocks eligible Standalone work. Review responsibility for semantic eligibility remains explicit. |
| Claude persistence `ed2fbdc6-448e-4a65-8450-7a4b6be84fa1` | Found F10 allocation/Unicode defects; reproduced and corrected. |
| Claude test adequacy `89568f5e-6a99-47bf-8f06-6f2af68aa433` | Found F11 vacuous target analysis, weak properties and seed semantics plus F13 missing bans. Negative proofs reproduce the weaknesses; focused and canonical suites cover corrections. |
| Claude logging `492ed20a-a62f-4356-92c5-d5669b615606` | No observed privacy bypass; found conservative classification, post-commit presentation and proof gaps. Fixed classification/nullable actors, F12, actual retention/default-level and expanded failure-equivalence proof. No current reentry/RequestReady consumer supported a lifecycle redesign. |
| Native final ADR/authority review | Confirmed all seven material amendment/relationship claims. Corrected a stale original-exception obligation and a wrong E-LOG source path. Static only; it did not execute gates or certify live APIs. |
| Native final production review at `71548bf` | Confirmed content bounds, safe logging, committed UI outcomes, heading fallback and wire quantities by source inspection. Found F14; bounded followup inspected the correction and new cases, resolving the finding statically. Parent executed the 63-case integration proof separately. |

Review findings are evidence, not owner approval or a substitute for exact-candidate verification.

## Executed focused receipts

The integrated source/test tree tested by the 63-case command is captured in `1d50ddcb2f6ef7d8f8634a3f96d9612d1e4eb243`; documentation edits do not substitute for executable proof. All moderate/heavy work used rexec and the repository SDK resolver.

```bash
rexec --shell 'export PATH="$(./scripts/resolve-dotnet.sh):$PATH"; dotnet test tests/AlterCourse.Core.Tests/AlterCourse.Core.Tests.csproj -c Release --no-restore --disable-build-servers --filter "FullyQualifiedName~GamePersistenceWriteFailureTests|FullyQualifiedName~DependencyArchitectureTests|FullyQualifiedName~GeneratedSimulationInvariantTests|FullyQualifiedName~GameplayLoggingTests"'
```

Actual result: exit 0, 63 passed, zero failures/skips. The executed invocation also formatted and pulled back only the two changed persistence files before testing. The preceding save-close RED used `FullyQualifiedName~GamePersistenceWriteFailureTests` against `9f9552e` plus the new regression: exit 1, two expected primary-exception identity failures, eight passes.

The content/quantity receipt at source `2220fb3` used the same test command with `FullyQualifiedName~Content|FullyQualifiedName~PhysicalQuantityTests`: exit 0, 183 passes, zero skips. The final candidate gate must include these cases again.

The isolated actual Godot console probe used the resolved engine with `--headless --path src/AlterCourse.Godot --script <temporary-probe>` after locked restore, Debug warning-as-error build and import. It instantiated three normal scene lifetimes: a synthetic missing content path, default Information, and explicit Debug. Result: exit 0; the synthetic secret/path/payload name was absent; each lifetime emitted exactly one Started and Stopped; Information emitted zero candidate/constraint details; Debug emitted 16 candidate and nine constraint records. This manual probe used worker source `07335ca` with the tests later committed as `7781438`; its saved transcript was inspected independently. Temporary probe/capture files were removed; this is a witnessed manual receipt, not a claimed additional permanent engine test. Permanent rendered/structured/retention and lifecycle tests remain in `GameplayLoggingTests`.

The baseline leak was reproduced with the same synthetic missing-file scenario: the old diagnostic rendered the raw `user://SYNTHETIC_SECRET/private-home/save-payload.json` path. No real sensitive input was needed to prove leakage.
