---
schema_version: '1.1'
id: 'reference-lff74y-content-assets-and-persistence'
title: 'Content Assets and Persistence'
description: 'Distinct contracts for reusable definitions, durable saves, visual assets, and their validation.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-27'
tags:
  - 'architecture'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/wiki/asset-pipeline-tool.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/ship-system-substrate.md'
---

# Content, assets, and persistence

[Wiki home](README.md) · [Architecture](architecture.md) · [Source catalog](sources.md)

## Three different contracts

Authored domain definitions describe reusable game capability. Runtime instances describe the actual evolving world. Visual assets and presentation configuration describe how something is displayed. These must not become alternative authorities for the same fact.

A ship class definition can be shared by many vessels; their names, conditions, activities, and future political affiliations are instance/world facts. ADR 0014 additionally distinguishes a class default loadout from each live ship's actual installations. A sprite or asset manifest cannot grant a ship a weapon or faction affiliation.

## Domain content

ADR 0005 makes strict UTF-8 JSON the canonical ordinary Core content format. System.Text.Json parsing, structural JSON Schema validation, explicit input models, stable IDs, reference resolution, and semantic validation admit immutable definitions. Reject malformed content, unknown members where not explicitly allowed, duplicate IDs, invalid bounds, unsupported versions, and broken references.

Current `dev` adds strict faction-definition V1 loading alongside [Pathfinder content](../../src/AlterCourse.Godot/content/ships/pathfinder.json). Ship content reached V5 for M6A (bounded shield, directed-energy, and repair authoring); the development-branch installed-system substrate migration ([Issue #121](ship-system-substrate.md)) has since rewritten it to ship-definition V6, which retains only design identity and an initial installed-system loadout, plus a new system-definition V1 catalog that owns the per-kind equipment tuning V5 held directly (below, "Implemented substrate content and persistence"). Strict validation admits no fallback combat values. A faction definition has stable identity and display name; mutable objectives and direct ship control remain runtime state. Campaign content families remain absent.

Stable definition IDs are not display names or file paths. Content migration and save migration are separate responsibilities. A future specialized narrative source language requires the explicit ADR 0012 admission path; it does not authorize YAML as an alternate ordinary ship/faction definition format.

## Durable saves

ADR 0006 selects explicit versioned JSON snapshots, not serialization of live C# graphs, Godot scenes, an event store, or a database. Persist consequential state, stable references, ordering, and allocator continuation. Do not persist derived UI values, caches, logger objects, callbacks, or package-specific runtime identities.

Historical save V6 uses rules identity `strategic-contact-reporting-v1` and includes every ship, player identity, strategic/tactical state, Engineering condition/allocation/repair, active orders, actor-local contacts, scans, contact posture, the observation-location frame on each contact, correlated scheduled work, and counters. `KnownContactReports` is derived from that retained knowledge, not a separately serialized UI authority.

The released V8 line supports adjacent migrations V1→V2→V3→V4→V5→V6→V7→V8 as described below. Current development extends that chain through V9 and then V10 (below, "Implemented substrate content and persistence"). V1 reconstructs the representable single ship in a plural world; V3 adds orders without inventing historical intentions; V4 adds empty knowledge/no autonomous posture to older saves; V5 maps sensor integrity/repair into Engineering while preserving compatible historical capability and exact completion identity; V6 sets a null observation-location frame on every legacy contact and derives nothing, so a migrated contact stays on the tactical surface without appearing in strategic reports until a new qualifying observation is recorded. [Engineering and combat](engineering-and-combat.md), [Strategic Contact Reporting](strategic-contact-reporting.md), and [GamePersistence](../../src/AlterCourse.Core/Persistence/GamePersistence.cs) carry the implemented detail.

The adjacent migrations deliberately supply only values that the next schema requires to represent the old world. V1 uses the referenced definition's design label once because V1 had no vessel name; V2 persists that resolved name. V2→V3 initializes the order allocator and leaves every historical `ActiveOrder` absent. V3→V4 creates empty sensor knowledge, allocator value 1, no active scan, contact posture, or decision wake. V4→V5 maps sensor integrity and any sensor repair into Engineering, initializes nominal generation and impulse condition, retains the exact repair correlation, and uses full allocations only where the V4-authored generation can meet both demands. These are migration facts, not permission to infer later intent, knowledge, or political history.

Loading validates an entire candidate before replacing the live simulation. The current bounded envelope is 128 MiB; V6 records a 106,775,347-byte maximum-shape serialization test (previously 95,677,740 bytes under V5), not a normal four-ship save size. These are historical admission measurements, not current-format maxima or a rationale to redesign storage without measurement.

The shell uses `user://quick-save.json`. The legacy default `quick-save-v1.json` fallback is consulted only if the generic slot is absent; custom paths do not use it. Broader compatibility promises, autosave policies, and eventual distribution remain separate decisions. Pre-1.0 does not promise perpetual migration support.

## Implemented V7 and V8

[Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md), D-12, implements V7 in Feature #86 / Final PR #87, merged into `dev` as `0217296`, and retains the complete adjacent V1→V7 chain. V6→V7 introduces an empty faction collection, null historical ship controller links, and no faction decision state or faction wakes. Existing work becomes explicitly ship-targeted while preserving identities, times, total ordering, correlation, and continuation counters. Existing world/ship/knowledge state is preserved; political history is not inferred from vessel names or current new-game content.

V7 uses rules identity `faction-intent-autonomous-assignment-v1`; V6's `strategic-contact-reporting-v1` identity remains part of the historical migration contract.

Zero-faction worlds must remain valid. New-game typed bootstrap may create the proof's factions; loading a migrated save must not rerun that initialization. Persist consequential faction state, direct control, and exact typed Ship/Faction work; derive rosters and projections rather than duplicating authority. No faction/organization placeholder or RNG state is required.

Candidate validation rejects missing or wrong-domain targets, bad correlations, and corrupted JSON before replacing live state. It validates the envelope's schema and rules identity, required members, metadata, every reference and counter, and then constructs a complete candidate before a load can replace live state. The 128 MiB UTF-8 envelope and depth-32 JSON input limits apply before the candidate becomes authoritative. The historical V7 maximum-shape coverage measured 111,544,212 bytes for 256 ships, 256 factions, full contacts, and 66,302 simultaneously valid work items: 22,673,516 bytes below the unchanged 128 MiB envelope. Its V7 scheduler admission ceiling was 66,816; the lower test population reflects the incompatible per-ship commitments in that constructed world. Released V8 has a scheduler ceiling of 68,864; V9 raised it to 69,120, and the development-branch V10 substrate migration leaves that ceiling unchanged (no work kind was added; repair and scan each remain one per ship). v0.5.0 remains the historical V6 line.

The V7 Godot compatibility evidence covers both catalogs, valid V7 quick-load continuation, and failure preservation for malformed faction/controller JSON. It exposes none of that faction data in the player UI. Targeted headless continuation and long-horizon scenarios cover the original admission.

[Observation-Driven Faction Response](observation-driven-faction-response.md) advances V7 to the released V8 format under rules identity `observation-driven-faction-response-v1`. V7→V8 adds a disabled response posture, empty in-flight/received report collections, no active investigation, no report-delivery work, and no location-response history. It does not mine existing contacts or infer faction knowledge. V8 persists only bounded report/investigation continuation, report identity allocation, and sparse completion watermarks; derived projections and indexes remain absent. Candidate validation rejects malformed identities, source/recipient authority contradictions, invalid report timing or location data, wrong-domain work, duplicate work, and inconsistent active-investigation state before replacing a live simulation.

V8's source-derived scheduler limits are 68,864 outstanding work items, 68,853 same-instant consequence executions, and 78,853 total consequence executions. The existing 128 MiB save envelope remains unchanged. A fully graph-validated high-width V8 fixture measured 134,478,451 bytes in pretty JSON; compact JSON under ADR 0006's measured-benefit allowance preserves the logical DTO and saves 25,587,467 bytes, producing 108,890,984 bytes. The V8 persistence proof derives a conservative universal ceiling of 113,024,376 bytes by adding complete maximum encodings for legal shapes omitted by that combined fixture; it leaves 21,193,352 bytes below 128 MiB. Fixture values remain measurements, while the deliberately overcounting ceiling is the tested bound for the supported V8 schema.

## Implemented V9 combat persistence

M6A is unreleased development; V8 remains the released format. V9 uses rules identity `first-combat-engagement-v1` and persists shield and directed-energy conditions, four allocations, absolute readiness, and one bounded pending defensive stimulus with its exact wake correlation. It adds no combat target telemetry/history or hidden attacker identity to that stimulus; existing authoritative ship/world snapshots remain persisted under their established contracts.

V8→V9 initializes the new conditions and allocations to zero, readiness to zero, and no stimulus/combat work. It preserves existing conditions, allocation, repair, order, contact, faction, time, and scheduler identities. Migration creates no historical combat capability, damage, aggression, target identification, or new content-derived state. A new game deliberately starts the new systems nominal, while a migrated player can later repair and allocate them through ordinary commands. Frozen V1–V8 DTOs and work-kind validation retain their strict historical meanings; malformed V9 state, work, timing, or correlation fails candidate validation.

Candidate validation treats Current contacts as present-time state: observer and target must both be `AtLocation` at the same location, and a present observation-location frame must match it. A null legacy frame remains null; Stale and Lost frames remain historical and are not constrained by present locations. Rejection leaves the live simulation unchanged.

The V9 faction fixture measures 88,137,170 bytes and the high-width report vertex measures 108,935,516 bytes. The conservative supported-shape bound is 113,292,140 bytes, below the unchanged 128 MiB envelope. These are persistence-proof measurements and bounds for the V9 format; V10 supersedes them below. The empty-combat report vertex does not demonstrate a populated combat continuation; that separate proof covers the legal maximum combat stimulus shape.

## Implemented substrate content and persistence

Issue #121's [installed-system substrate](ship-system-substrate.md#compatibility-and-current-format-capture) is implemented on the development branch (unreleased): ship-definition V6, system-definition content V1, and save schema V10 under rules identity `installed-ship-system-substrate-v1`. Ship content V6 retains only design identity and an initial installed-system loadout (stable installed IDs plus definition references and an explicit next-ID allocator); the equipment tuning V5 held directly — generation output, per-consumer demand, passive range, scan duration, tactical speed, weapon range/damage/cooldown, and repair durations — now lives in the system-definition V1 catalog ([`pathfinder-systems.json`](../../src/AlterCourse.Godot/content/schemas/system-definition-v1.schema.json)), referenced by definition ID from each installed system.

V10 captures installed state directly: per-ship installed systems (definition reference, condition, allocation where the definition is a consumer), the allocator's next ID, one optional repair keyed to an installed ID, active-scan continuation keyed to an installed sensor, and directed-energy readiness keyed to an installed weapon. It never flattens current state through V8/V9's five fixed fields before capturing it. `MigrateV9ToV10` maps each of V9's five named conditions and allocations to installed IDs 1–5 through a frozen, version-qualified table (not the current default loadout, order, or labels), preserves installed-but-offline meaning (zero condition is not absence), and verifies the supplied catalog's compatibility descriptor for every referenced definition and for the whole-catalog aim vocabulary before accepting a load; a mismatch fails as `IncompatibleContent` rather than silently reinterpreting the save. Frozen historical tuning for V1–V9 validators is unaffected: those validators and earlier migrations read a fixed historical table, never the current catalog. The scheduler work-item ceiling stays 69,120 (above); no work kind changed.

The measured maximum-width V10 save is 109,030,603 bytes, with a conservative supported-shape bound of 114,536,452 bytes — below the unchanged 128 MiB envelope, with 19,681,276 bytes of headroom. These supersede the V9 measurements above for the current format; they are persistence-proof measurements and bounds, not ordinary save sizes or a rationale to raise limits. Godot's generic Engineering presentation for this substrate is implemented on the development branch ([Ship-system substrate](ship-system-substrate.md#generic-presentation-and-refit-readiness)).

## AssetCtl pipeline

AssetCtl is standalone .NET 10 development infrastructure, separate from both game assemblies. It searches the tracked catalog, plans routes, obtains candidates, mechanically validates untrusted bytes, selects/publishes assets with manifests, and retains provenance. The full [asset pipeline contract](asset-pipeline-tool.md) remains the detailed contract; this summary is not its replacement.

Tracked YAML under [config/assets](../../config/assets/) describes provider instances, capabilities, routes, models, quality/style choices, and manifests. This is development/presentation metadata, not Core game content. Existing adapters and configured options do not imply an external provider is enabled or tested live in this review.

Committed defaults disable external generation and spend. Deterministic local SVG/PNG placeholders and offline validation remain available without provider credentials or paid calls. Candidates, receipts, locks, and local overrides remain under ignored `.assetctl/`. The game consumes selected files through ordinary Godot asset paths rather than calling generation providers.

Routine placeholder work can proceed within policy. Approval and deprecation of approved assets require explicit current owner instruction and the tool's confirmations. Approved replacement uses a new semantic asset identity and supersession rather than silently overwriting an approved file. Selected assets and manifests move together through the validated publishing boundary.

Store only credential environment-variable names in tracked asset configuration. The application/launch boundary resolves credentials; values must not enter manifests, fixtures, output, or logs. Provenance and automated review do not provide legal clearance.

## Sources

[Content ADR](../adr/0005-use-json-and-schema-validation-for-domain-content.md), [save ADR](../adr/0006-use-versioned-json-snapshot-saves.md), [substrate contract](ship-system-substrate.md), [asset pipeline contract](asset-pipeline-tool.md), [V5 ship schema](../../src/AlterCourse.Godot/content/schemas/ship-definition-v5.schema.json), [ship content V6 schema](../../src/AlterCourse.Godot/content/schemas/ship-definition-v6.schema.json), [system-definition V1 schema](../../src/AlterCourse.Godot/content/schemas/system-definition-v1.schema.json), [persistence implementation](../../src/AlterCourse.Core/Persistence/GamePersistence.cs), [V10 save models](../../src/AlterCourse.Core/Persistence/SaveModelsV10.cs), [V9 persistence tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV9CombatTests.cs), [V10 compatibility tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV10CompatibilityTests.cs), [V10 substrate tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV10SubstrateTests.cs), [maximum-shape tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV4SensorTests.cs), [AssetCtl admission](../dependency-admission/assetctl.md), [JsonSchema.Net admission](../dependency-admission/jsonschema-net-core.md), and [asset development workflow](../development-quality.md).
