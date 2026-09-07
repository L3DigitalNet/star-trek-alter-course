---
schema_version: '1.1'
id: 'reference-lff74y-content-assets-and-persistence'
title: 'Content Assets and Persistence'
description: 'Distinct contracts for reusable definitions, durable saves, visual assets, and their validation.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'architecture'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/specs/asset-pipeline-tool.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
---

# Content, assets, and persistence

[Wiki home](README.md) · [Architecture](architecture.md) · [Source catalog](sources.md)

## Three different contracts

Authored domain definitions describe reusable game capability. Runtime instances describe the actual evolving world. Visual assets and presentation configuration describe how something is displayed. These must not become alternative authorities for the same fact.

A ship class definition can be shared by many vessels; their names, conditions, activities, and future political affiliations are instance/world facts. A sprite or asset manifest cannot grant a ship a weapon or faction affiliation.

## Domain content

ADR 0005 makes strict UTF-8 JSON the canonical ordinary Core content format. System.Text.Json parsing, structural JSON Schema validation, explicit input models, stable IDs, reference resolution, and semantic validation admit immutable definitions. Reject malformed content, unknown members where not explicitly allowed, duplicate IDs, invalid bounds, unsupported versions, and broken references.

The current runtime catalog loads [Pathfinder content](../../src/AlterCourse.Godot/content/ships/pathfinder.json) through [ship-definition schema V4](../../src/AlterCourse.Godot/content/schemas/ship-definition-v4.schema.json). There are not yet production faction, weapon, or campaign content families merely because the ADR anticipates them.

The approved [faction-assignment slice](faction-intent-and-autonomous-assignment.md) supplies the first concrete faction content consumer. Reusable faction definitions will follow the same strict JSON and semantic-validation boundary; faction runtime identity/state, mutable objectives/control, and typed initial scenario declarations remain separate responsibilities. A new faction content family does not itself require changing ship-definition content V4.

Stable definition IDs are not display names or file paths. Content migration and save migration are separate responsibilities. A future specialized narrative source language requires the explicit ADR 0012 admission path; it does not authorize YAML as an alternate ordinary ship/faction definition format.

## Durable saves

ADR 0006 selects explicit versioned JSON snapshots, not serialization of live C# graphs, Godot scenes, an event store, or a database. Persist consequential state, stable references, ordering, and allocator continuation. Do not persist derived UI values, caches, logger objects, callbacks, or package-specific runtime identities.

Current save V6 uses rules identity `strategic-contact-reporting-v1` and includes every ship, player identity, strategic/tactical state, Engineering condition/allocation/repair, active orders, actor-local contacts, scans, contact posture, observation-location frames, correlated scheduled work, and counters. `KnownContactReports` is derived from that retained sensor knowledge, not persisted as an independent projection or second knowledge authority. The active save schema is not advanced by documenting future factions.

Supported adjacent migrations are V1→V2→V3→V4→V5→V6. V1 reconstructs the representable single ship in a plural world; V3 adds orders without inventing historical intentions; V4 adds empty knowledge/no autonomous posture to older saves; V5 maps sensor integrity/repair into Engineering while preserving compatible historical capability and exact completion identity; V6 sets a null observation-location frame on every legacy contact and derives nothing, so a migrated contact stays on the tactical surface without appearing in strategic reports until a new qualifying observation is recorded. The detailed migration contract remains in [Engineering Backbone](../design/engineering-backbone.md), [Strategic Contact Reporting](strategic-contact-reporting.md), and [GamePersistence](../../src/AlterCourse.Core/Persistence/GamePersistence.cs).

Loading validates an entire candidate before replacing the live simulation. The current bounded envelope is 128 MiB; V6 records a 106,775,347-byte maximum-shape serialization test (previously 95,677,740 bytes under V5), not a normal four-ship save size. These are present admission bounds, not a rationale to redesign storage without measurement.

The shell uses `user://quick-save.json`. The legacy default `quick-save-v1.json` fallback is consulted only if the generic slot is absent; custom paths do not use it. Broader compatibility promises, autosave policies, and eventual distribution remain separate decisions. Pre-1.0 does not promise perpetual migration support.

## Approved V7 compatibility plan, not current save support

The first faction implementation is planned to advance saves to V7 for consequential faction state, direct-controller links, and typed faction-targeted work. Preserve adjacent V1→V7 migration support. V6 remains current and there is no V7 loader merely because this plan is approved.

V6→V7 will add zero factions, null ship controllers, no faction decision state, and no faction wakes. Existing ships, orders, observations, counters, and work retain their meaning; existing work maps to the equivalent typed Ship target with its original identity, due time, and same-time ordering. Never infer factions, allegiance, control, or history from names, locations, or new-game content.

Zero-faction simulations remain valid. New political starts/control/wakes use typed new-game bootstrap and complete candidate validation; loading a historical save does not invoke that bootstrap to seed political actors. Persist only the new consequential state and exact wake correlation, not derived rosters, presentation values, or unbounded decision logs.

Validate typed target kind/identity compatibility, references, bounds, allocator continuation where applicable, and uninterrupted versus restored continuation. Measure the expanded maximum shape against the existing envelope without silently raising limits. No RNG state is added: the first faction policy is deterministic and nonrandom. Q-14 is resolved only for this compatibility boundary; broader compatibility and random-algorithm choices remain open.

## AssetCtl pipeline

AssetCtl is standalone .NET 10 development infrastructure, separate from both game assemblies. It searches the tracked catalog, plans routes, obtains candidates, mechanically validates untrusted bytes, selects/publishes assets with manifests, and retains provenance. The full [asset pipeline specification](../specs/asset-pipeline-tool.md) remains the detailed contract; this summary is not its replacement.

Tracked YAML under [config/assets](../../config/assets/) describes provider instances, capabilities, routes, models, quality/style choices, and manifests. This is development/presentation metadata, not Core game content. Existing adapters and configured options do not imply an external provider is enabled or tested live in this review.

Committed defaults disable external generation and spend. Deterministic local SVG/PNG placeholders and offline validation remain available without provider credentials or paid calls. Candidates, receipts, locks, and local overrides remain under ignored `.assetctl/`. The game consumes selected files through ordinary Godot asset paths rather than calling generation providers.

Routine placeholder work can proceed within policy. Approval and deprecation of approved assets require explicit current owner instruction and the tool's confirmations. Approved replacement uses a new semantic asset identity and supersession rather than silently overwriting an approved file. Selected assets and manifests move together through the validated publishing boundary.

Store only credential environment-variable names in tracked asset configuration. The application/launch boundary resolves credentials; values must not enter manifests, fixtures, output, or logs. Provenance and automated review do not provide legal clearance.

## Sources

[Content ADR](../adr/0005-use-json-and-schema-validation-for-domain-content.md), [save ADR](../adr/0006-use-versioned-json-snapshot-saves.md), [faction assignment decision](faction-intent-and-autonomous-assignment.md), [asset specification](../specs/asset-pipeline-tool.md), [AssetCtl admission](../dependency-admission/assetctl.md), [JsonSchema.Net admission](../dependency-admission/jsonschema-net-core.md), and [asset development workflow](../development-quality.md).
