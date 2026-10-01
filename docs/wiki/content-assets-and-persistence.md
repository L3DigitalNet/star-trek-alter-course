---
schema_version: '1.1'
id: 'reference-lff74y-content-assets-and-persistence'
title: 'Content Assets and Persistence'
description: 'Distinct contracts for reusable definitions, durable saves, visual assets, and their validation.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-30'
tags:
  - 'architecture'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md'
  - 'docs/adr/0016-own-actor-knowledge-and-share-immutable-observation-reports.md'
  - 'docs/adr/0017-generate-assets-outside-the-game-through-bounded-validated-publication.md'
  - 'docs/adr/0018-separate-simulation-session-lifetime-from-workspaces.md'
  - 'docs/wiki/asset-pipeline-tool.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/ship-system-substrate.md'
---

# Content, assets, and persistence

[Wiki home](README.md) · [Architecture](architecture.md) · [Source catalog](sources.md)

## Three different contracts

Authored definitions describe reusable capability. Runtime instances describe the evolving world. Visual assets and presentation configuration describe how information is displayed. None is an alternate authority for the others.

A ship class may serve many vessels; their names, conditions, activities, and affiliations are instance facts. ADR 0014 distinguishes class default loadout from actual installations. A sprite or asset manifest cannot grant weapons or faction affiliation.

ADRs 0005/0006 own domain content and durable snapshots; [ADR 0017](../adr/0017-generate-assets-outside-the-game-through-bounded-validated-publication.md) owns visual-asset generation/publication. [ADR 0015](../adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md) owns in-memory command commitment, not file durability. [ADR 0018](../adr/0018-separate-simulation-session-lifetime-from-workspaces.md) owns installing a validated replacement in the live session. These are related but different boundaries.

## Domain content

ADR 0005 selects strict UTF-8 JSON for ordinary Core content. System.Text.Json parsing, structural JSON Schema validation, explicit input models, stable IDs, reference resolution, and semantic validation admit immutable definitions. Reject malformed input, unknown members where not allowed, duplicate IDs, invalid bounds, unsupported versions, and broken references.

The [September 30 follow-up](../reviews/adr-remediation-2026-09-30.md#executed-reproductions-and-focused-correction) reproduced and corrected two earlier admission gaps (R1/R2). Godot now admits native bytes within each definition family's 256 KiB limit and a separate 16 KiB schema limit, rejecting oversized declared lengths before reading and independently checking actual exhaustion/length agreement with bounded reads. Definitions reach Core as unchanged native bytes. Core byte/stream factories count the full input against the limit, then remove exactly one complete initial UTF-8 BOM before strict JSON admission; schema ingress follows the same bound-before-preamble-removal order before strict decoding. Double/truncated BOMs and invalid suffixes reject. Interior U+FEFF is preserved, while literal leading U+FEFF in `FromText` remains invalid JSON. Diagnostic byte offsets for BOM-bearing definitions refer to the admitted JSON after the preamble. Malformed schema roots `[]`, `17`, and `null` reject as family `schema.invalid` through a narrow filter for the pinned dependency’s exact exception shape. `FromText` rejects isolated UTF-16 code units through existing family `json.invalid` diagnostics instead of replacing them; valid surrogate pairs, multibyte text, and authored U+FFFD retain their meaning. Latest focused proof passes (357 Core and 106 Godot cases); the earlier combined canonical receipt predates BOM/schema and fixture corrections. Final canonical, mutation, automated classification proof, and hosted admission receipts remain with [Issue #135 / PR #138](https://github.com/L3DigitalNet/star-trek-alter-course/pull/138). Development native-resource bootstrap was exercised; exported-PCK execution and nonseekable-input coverage are not claimed.

Current development uses ship-definition V6, system-definition V1, and faction-definition V1. [Pathfinder](../../src/AlterCourse.Godot/content/ships/pathfinder.json) owns design identity and initial installed loadout; the system catalog owns typed equipment characteristics. M6A's earlier ship V5 stored equipment tuning directly and is now historical. Strict admission supplies no fallback combat values. Faction definitions supply stable identity/display name; objectives and direct control remain runtime state. Campaign content families remain absent.

Definition IDs are not display names or file paths. Content migration and save migration have separate responsibilities. A specialized narrative language requires ADR 0012 admission; it does not authorize YAML as an alternate ordinary ship/faction format.

## Durable saves

ADR 0006 selects explicit versioned JSON snapshots, not live C# graph serialization, Godot scenes, an event store, or a database. Persist consequential state, stable references, ordering, and allocator continuation. Do not persist derived UI values, caches, loggers, callbacks, or package runtime identities. [ADR 0016](../adr/0016-own-actor-knowledge-and-share-immutable-observation-reports.md) governs the provenance and historical meaning of persisted actor knowledge.

The owner's September 30, 2026 [ADR 0006 amendment](../adr/0006-use-versioned-json-snapshot-saves.md#amendment--september-30-2026) starts explicit cross-version save compatibility and migration-maintenance obligations at v1.0.0. Earlier development/testing saves have no cross-build compatibility or exact-continuation guarantee, may be invalidated, and need no migration for every development change. Existing readers and fixtures below describe implemented behavior, not a continuing support promise. Strict bounded validation, explicit snapshot identities, fail-closed rejection, and same-build deterministic round trips and continuation remain required.

Before materializing a save document, persistence pre-scans arrays, strings, and member names against aggregate/schema bounds. Malformed Unicode and historical V5/V6 null work entries fail through controlled validation rather than reaching migrations or escaping as decoder errors. Write tests inject candidate-write, flush, close, replacement, and cleanup failures, preserving the previous target and original failure when cleanup also fails. This is bounded failure handling, not universal power-loss durability or a new format.

Historical save V6 uses `strategic-contact-reporting-v1`. It includes ships, player identity, strategic/tactical state, Engineering condition/allocation/repair, orders, actor-local contacts, scans, posture, observation-location frames, correlated work, and counters. `KnownContactReports` is derived from retained knowledge, not a separately serialized UI authority.

Released V8 supports adjacent V1→V2→V3→V4→V5→V6→V7→V8 migrations. Development extends that chain through V9 and V10. The historical steps preserve representable state rather than inventing intentions or knowledge:

- V1 represents the original single ship in the plural model and resolves its missing vessel name from the referenced definition once; V2 persists that resolved name.
- V2→V3 initializes the order allocator and leaves historical active orders absent.
- V3→V4 creates empty knowledge, contact allocator 1, no scan, no contact posture, and no decision wake.
- V4→V5 maps sensor integrity and repair into Engineering, preserves exact completion correlation, initializes nominal generation/impulse condition, and uses full allocations only where authored generation can meet both demands.
- V5→V6 adds a null observation-location frame without deriving one. A migrated contact does not appear in strategic reports until a new qualifying observation supplies that frame.

[Engineering and combat](engineering-and-combat.md), [Strategic Contact Reporting](strategic-contact-reporting.md), and [GamePersistence](../../src/AlterCourse.Core/Persistence/GamePersistence.cs) retain detailed migration behavior. These required representations do not authorize later history inferred from current bootstrap.

Loading validates a complete candidate before live replacement. The current envelope is 128 MiB. Historical V6's maximum-shape test measured 106,775,347 bytes, versus 95,677,740 under V5; neither is a normal save size or a current-format maximum.

The shell uses `user://quick-save.json`. It consults legacy `quick-save-v1.json` only when the generic default slot is absent; custom paths do not use that fallback. Autosave, distribution, and the supported compatibility range from v1.0.0 require separate decisions. A successful load's session invalidation and preference handling belong in [Interface](interface-and-player-commands.md#session-replacement).

## Implemented V7 and V8

[Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md), D-12, introduced V7 through Feature #86 / Final PR #87, merged as `0217296`. V6→V7 creates an empty faction collection, null historical controllers, and no faction decision state or wakes. Existing work becomes explicitly ship-targeted while preserving IDs, times, sequence, correlation, and counters. Political history is not inferred from names or new-game content.

V7 uses `faction-intent-autonomous-assignment-v1`; V6's `strategic-contact-reporting-v1` remains historical compatibility data. Zero-faction worlds remain valid. New-game bootstrap may create proof factions; migration must not rerun it. Persist direct control, consequential faction state, and exact Ship/Faction work; derive rosters/projections. No organization placeholder or RNG state is required.

Candidate validation checks envelope schema/rules, required members, metadata, references, counters, target domains, and correlations before constructing a replacement. The 128 MiB UTF-8 envelope and depth-32 input limits apply before authority. Historical V7's fixture measured 111,544,212 bytes for 256 ships/factions, full contacts, and 66,302 simultaneously valid work items: 22,673,516 bytes below the envelope. Its admission ceiling was 66,816; the fixture's smaller population reflects incompatible commitments, not relaxed validation.

V8's scheduler ceiling was 68,864; V9 raised it to 69,120. V10 keeps that ceiling because no work kind was added and scan/repair remain one per ship. v0.5.0 remains the historical V6 line. Historical V7 Godot evidence covers both catalogs, valid continuation, and failure preservation for malformed faction/controller input without exposing faction data to the player.

[Observation-Driven Faction Response](observation-driven-faction-response.md) introduced released V8 under `observation-driven-faction-response-v1`. V7→V8 creates disabled response posture, empty in-flight/received reports, no investigation, no delivery work, and no location-response history. It does not mine contacts to invent unsent reports. V8 persists bounded report/investigation continuation, report allocation, and sparse completion watermarks; derived indexes/projections remain absent.

Validation rejects malformed identities, source/recipient authority contradictions, invalid timing/location data, wrong-domain or duplicate work, and inconsistent investigations before live replacement. Historical V8 limits are 68,864 outstanding items, 68,853 same-instant executions, and 78,853 total executions.

A graph-validated high-width V8 fixture measured 134,478,451 bytes pretty-printed. Compact JSON, admitted through ADR 0006's measured-benefit allowance, preserves the DTO and saves 25,587,467 bytes, producing 108,890,984 bytes. Its conservative supported-shape ceiling is 113,024,376 bytes, leaving 21,193,352 below 128 MiB. The ceiling deliberately overcounts maximum encodings omitted from that fixture; measurement and bound are different evidence.

## Implemented V9 combat persistence

M6A is unreleased; V8 remains the released format. Historical V9 uses `first-combat-engagement-v1` and persists shield/weapon conditions, four allocations, absolute readiness, and one bounded defensive stimulus with exact wake correlation. It adds no target telemetry/history or hidden attacker identity to the stimulus. Established authoritative world/ship snapshots remain separately persisted.

V8→V9 initializes new conditions/allocations and readiness to zero, with no stimulus or combat work. It preserves existing conditions, allocation, repair, orders, contacts, factions, time, and work identities. It invents no combat capability, damage, aggression, identification, or content-derived history. New games deliberately initialize nominal systems; a migrated player may repair and allocate through ordinary commands. Frozen V1–V8 DTOs and work-kind validation retain their historical meaning.

Current-contact validation requires observer and target both `AtLocation` at the same location and any present observation frame to match. A null legacy frame remains null; Stale/Lost frames retain historical meaning without current-location constraints. Rejection preserves the live simulation.

V9's faction fixture measured 88,137,170 bytes; its high-width report vertex measured 108,935,516. The conservative supported-shape bound is 113,292,140 within 128 MiB. The empty-combat report vertex does not prove populated combat continuation; a separate proof covers the legal maximum stimulus population. V10 supersedes these measurements for current saves.

## Implemented substrate content and persistence

Issue #121's [installed-system contract](ship-system-substrate.md#compatibility-and-current-format-capture) is implemented and unreleased: ship V6, system-definition V1, and save V10 under `installed-ship-system-substrate-v1`. Ship definitions retain design identity and initial loadout with stable installed IDs, definition references, and next-ID continuation.

Under the September 30 compatibility decision, Issue #135's defensive-heading underflow correction (R3) is an implementation correction with no wire-format change and retains `installed-ship-system-substrate-v1`. Its development edge-case outcomes may differ from earlier builds; this is not a claim of exact historical cross-build continuation. Historical identifiers, DTOs, and fixtures retain their meaning, while same-build deterministic continuation and strict load validation remain required. The policy authorizes the correction; implementation and executed verification belong to its governing change.

Equipment tuning formerly held directly by ship V5—generation, demands, sensor range/scan time, tactical speed, weapon range/damage/cooldown, and repair durations—now lives in [`pathfinder-systems.json`](../../src/AlterCourse.Godot/content/systems/pathfinder-systems.json), governed by its [V1 schema](../../src/AlterCourse.Godot/content/schemas/system-definition-v1.schema.json). Loadouts refer to reusable system definitions; they are not current live-state authority.

V10 directly captures installed definitions, condition, applicable allocation, next installation ID, one optional repair keyed to installation, active-scan continuation keyed to an installed sensor, and readiness keyed to an installed weapon. It does not flatten through historical fixed-field V8/V9 before capture.

`MigrateV9ToV10` uses a frozen version-qualified map from V9's five named components to installed IDs 1–5, not current default loadout/order/labels. It preserves installed-but-offline meaning: zero condition is not absence. Load validates per-definition compatibility descriptors and the whole-catalog aim vocabulary; mismatches fail as `IncompatibleContent` rather than silently reinterpreting state. Historical V1–V9 validators/migrations retain fixed tuning tables, not current catalog reads. Scheduler capacity stays 69,120.

The maximum-width V10 measurement is 109,030,603 bytes. Its conservative supported-shape ceiling is 114,536,452, leaving 19,681,276 bytes below 128 MiB. These supersede V9 proof values for the current schema; they are not ordinary save sizes or a reason to increase limits. [Generic Engineering presentation](ship-system-substrate.md#generic-presentation-and-refit-readiness) is implemented for this model.

## AssetCtl pipeline

AssetCtl is standalone .NET development infrastructure, independent of both game assemblies. It searches the catalog, plans routes, obtains candidates, mechanically validates untrusted bytes, selects/publishes assets with manifests, and retains provenance. [ADR 0017](../adr/0017-generate-assets-outside-the-game-through-bounded-validated-publication.md) owns architecture; the [full tool contract](asset-pipeline-tool.md) owns detailed behavior.

Tracked YAML under [config/assets](../../config/assets/) describes providers, capabilities, routes, models, quality/style choices, and manifests. This is development/presentation metadata, not Core content. Existing adapters do not establish live availability or provider testing in a documentation review.

Committed defaults disable external generation and spend. Local deterministic SVG/PNG placeholders and offline validation need no credentials or paid calls. Candidates, receipts, locks, logs, and local overrides remain ignored `.assetctl/` work state. The game consumes selected files through ordinary Godot resource paths.

Routine placeholder/candidate work stays within policy. Approval and deprecation of approved assets require explicit current owner instruction and tool confirmations. Approved replacement uses a new semantic ID and supersession, not silent overwrite. Validation or an AI score cannot supply human authorization or legal clearance.

Assets and manifests form one recoverable publication unit. Selected-file admission bounds allocation and consumption, checks regular-file identity, and rejects shortened or growing snapshots. Verification and approval use the same admitted bytes. Lifecycle mutation retains admitted parents and manifest/selected evidence through replacement; publication and recovery require corroborated file identities, mutable lifecycle, and semantic pair ownership before destructive work.

Version-1 ignored recovery state supplies an authority envelope that corroborates this ownership; it does not authenticate arbitrary same-UID rewrites. Legacy version-0 journals are refused and quarantined without deleting unproven predecessor artifacts. Operators must inspect those artifacts manually. These are local recovery-state versions, not changes to tracked manifests or receipts.

Current descriptor-bound reads, lifecycle mutation, and publication require Linux `openat`/`statx` with mounted procfs and cooperating writers holding the asset lock. They do not provide an atomic revision comparison against arbitrary same-UID writers, simultaneous replacement of two files, or universal power-loss durability. Portability must preserve equivalent safety.

Optional diagnostics preserve operation outcomes. Required stdout or post-publication receipt failure can return exit 9 with a known commit and degraded reporting; an unproven rollback is reported as `not-established`. Inspect selected state and available receipts before retrying. The [operator runbook](../development-quality.md#reporting-and-recovery) and [targeted correction record](../reviews/assetctl-boundaries-2026-09-30.md) retain recovery guidance and evidence limits.

Tracked credential configuration stores environment-variable names only. The caller/launch boundary resolves credentials; values must not enter fixtures, manifests, output, or logs. Canonical verification remains offline and never generates or approves assets.

## Sources

[Content ADR](../adr/0005-use-json-and-schema-validation-for-domain-content.md), [save ADR](../adr/0006-use-versioned-json-snapshot-saves.md), [asset ADR](../adr/0017-generate-assets-outside-the-game-through-bounded-validated-publication.md), [substrate contract](ship-system-substrate.md), [tool contract](asset-pipeline-tool.md), [historical V5 ship schema](../../src/AlterCourse.Godot/content/schemas/ship-definition-v5.schema.json), [ship V6 schema](../../src/AlterCourse.Godot/content/schemas/ship-definition-v6.schema.json), [system V1 schema](../../src/AlterCourse.Godot/content/schemas/system-definition-v1.schema.json), [persistence](../../src/AlterCourse.Core/Persistence/GamePersistence.cs), [V10 models](../../src/AlterCourse.Core/Persistence/SaveModelsV10.cs), [V9 tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV9CombatTests.cs), [V10 compatibility](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV10CompatibilityTests.cs), [V10 substrate tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV10SubstrateTests.cs), [maximum-shape tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV4SensorTests.cs), [AssetCtl admission](../dependency-admission/assetctl.md), [JsonSchema.Net admission](../dependency-admission/jsonschema-net-core.md), and [development workflow](../development-quality.md).
