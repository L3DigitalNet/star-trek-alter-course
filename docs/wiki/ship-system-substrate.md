---
schema_version: '1.1'
id: 'spec-wnw8vo-ship-system-substrate'
title: 'Ship-System Substrate Migration'
description: 'ADR 0014 implementation contract for heterogeneous installed systems, compatibility, information safety, and conformance evidence, implemented on the development branch.'
doc_type: 'spec'
status: 'active'
created: '2026-09-26'
updated: '2026-09-27'
tags:
  - 'architecture'
  - 'simulation'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0014-use-an-extensible-bounded-ship-system-substrate.md'
  - 'docs/wiki/engineering-and-combat.md'
  - 'docs/wiki/content-assets-and-persistence.md'
  - 'docs/wiki/interface-and-player-commands.md'
  - 'docs/wiki/implementation-status.md'
---

# Ship-system substrate migration

[Wiki home](README.md) · [ADR 0014](../adr/0014-use-an-extensible-bounded-ship-system-substrate.md) · [Engineering](engineering-and-combat.md) · [Implementation status](implementation-status.md)

## Authority, status, and sequencing

This page owns the bounded implementation and admission contract for [Issue #121](https://github.com/L3DigitalNet/star-trek-alter-course/issues/121). ADR 0014 owns the architectural decision. The owner selected implementation of that decision before further ship-system or Damage Control expansion; this page reconciles that instruction with the existing simulation and information boundaries.

**Implemented on `dev` (Final PR #123, merged as `17637dd`); unreleased.** ADR 0014 was adopted by PR #120. Runtime, content, persistence, and Godot's generic Engineering presentation now use the installed-system substrate described below: ship content V6, system-definition content V1, and saves V10 under rules identity `installed-ship-system-substrate-v1`. The latest source release remains v0.6.2 with v0.6.0/V8 gameplay; this migration is unreleased development, not a claim of merge or release. [Implementation status](implementation-status.md) and [Milestone proofs](milestone-proofs.md) track admission of the Final PR.

The sequence is M6A → ADR 0014 conformance migration → separately refined recovery or tactical gameplay. This migration is not M6B and does not complete M3, M5, or M6. Earlier Damage Control prompts and any ship-system prompt conflicting with this page are superseded as implementation instructions. They must not be used to introduce generation repair, recovery AI, or another system kind here.

## Complete migration, not a compatibility facade

The five current kinds are power generation, sensors, impulse propulsion, shields, and directed-energy weapons. Migrate them through content, bootstrap, runtime, commands, scheduling correlations, persistence, projections, and Godot. One canonical installed-system representation owns common state. Named fields must not remain authoritative behind a collection-shaped wrapper.

Keep actual specialized rules typed. Sensor observation, tactical propulsion, shield absorption, weapon firing, and defensive decisions remain their existing domains. A kind switch that constructs a typed definition or implements genuinely different behavior is legitimate. Independently repeated kind lists for common condition lookup, repairability, allocation accounting, current-format serialization, or generic controls are not.

Frozen historical wire models and narrowly isolated legacy translators may retain named fields. They are compatibility boundaries, not a second current runtime model. Derived specialized read-only views are acceptable; duplicated mutable state and a second authoritative allocation are not.

## Identity and ownership

Distinguish semantic kind, reusable definition, and installed instance. Preserve the five existing semantic kind strings. Exact C# type names are implementation choices, but types must prevent confusing a kind or definition with an installation.

Use stable ship-local installation IDs and explicit monotonic next-ID continuation. The pair of ship ID and installation ID identifies a world installation. Own-ship public commands bind the ship internally; trusted internal commands validate both coordinates. Reusing the same local number on two ships is valid and must never cause cross-ship mutation.

Initial loadout entries have explicit stable IDs or an equivalently explicit authored mapping. Identity must not depend on array position, hash iteration, labels, or file discovery. Retained IDs may have gaps. Preserve allocator continuation rather than rebuilding it as `max(existing) + 1` on load, because that would reuse removed identities. Validate duplicates, uninitialized IDs, bounds, and exhausted allocation without partial mutation.

A ship design owns an initial loadout only. A live ship owns its actual installations. Bootstrap distinguishes an explicit empty loadout from an omitted request to use defaults. Loading never overlays class defaults onto saved installations. Two ships of one design may omit different systems or reference different definitions of one kind.

## Reusable content and finite shapes

Use strict versioned JSON system definitions and ship-loadout references. A focused system-definition catalog is sufficient; no directory-discovery framework, database, plugin system, or executable JSON rules are required. Load and validate definitions before resolving ship-loadout references.

Common data has explicit capability support: condition/damage participation, optional repair parameters, optional allocatable demand, and stable common ordering. Avoid redundant support flags that can contradict the presence of required parameters. An installed consumer allocated zero power is valid; an absent installation is not represented by a fake zero-condition or zero-demand component.

Equipment-derived range, scan duration, speed contribution, generation output, repair parameters, and weapon tuning belong to the relevant typed component definition. Ship definitions retain design identity, initial loadout, and genuinely hull-owned facts. This does not claim that every future propulsion characteristic will be equipment-only.

Declare and validate finite maxima for definitions, loadout entries, installations, allocation entries, specialized state, strings, and JSON input before expensive materialization. Current kinds remain a closed typed behavior vocabulary. Another model of an existing kind is content, not a new behavior registration. Unknown kinds, duplicate definitions, wrong-kind specialized data, and unresolved references fail closed.

## Cardinality and absent capabilities

Common collections, allocation, repair identity, snapshot shapes, and generic UI support multiple installations of a kind. This migration does not invent aggregation or multi-bank combat. Current specialized simulation admission may enforce zero-or-one installation for each of the five current kinds, explicitly and separately from collection/storage validation. Do not hide that restriction in a kind-keyed dictionary that discards duplicates or in `First()` selection.

Prove multiple same-kind entries at the common substrate/snapshot boundary and prove the separate typed-world refusal where aggregation is unsupported. Do not describe that as implemented multi-generator, multi-shield, or multi-weapon gameplay.

Absence is valid at the common level. Absent generation yields zero supply; absent sensors yield zero detection and no scan capability; absent impulse yields zero attainable tactical speed; absent shields absorb nothing; absent weapons cannot fire. A restored ship without impulse cannot have positive tactical speed, and a scan or weapon continuation cannot reference an absent installation. Strategic travel and existing orders retain their current independent rules; this migration does not silently introduce warp requirements.

## Power, condition, and repair

Store one exact allocation per installed consumer, including zero allocations; nonconsumers have no allocation entry. Validate the complete consumer-key set, target ownership, nonnegative bounded values, per-installation demand, and total supply. Public exact allocation is a complete replacement, not an undocumented sparse patch. No consumers means empty allocation and all available supply remains reserve; handle zero denominators explicitly.

Balanced uses installed consumer demands; priority takes an installed consumer ID. Brownout scales prior committed allocations, not demands. Preserve existing flooring and one-pass remainder behavior using an explicit total order, with installation ID as a stable tie-break after authored common order. For the existing loadout, the order remains Sensors → Impulse → Shields → Directed Energy. Use checked wider intermediates for sums/products and test zero and maximum shapes.

Preserve the one-generator formula `floor(nominal output × condition)` and the current capability formulas. Voluntary allocations that make actual speed illegal remain atomic rejections. Physical damage still applies forced brownout, speed clamping, observation reconciliation, and scan interruption through the established Core paths.

One active repair targets an installed ID. Resolve support and duration from its definition without a repairable-kind switch. Keep existing full-authored-duration analytical interpolation, improving targets, exact completion correlation, travel continuity, and interruption semantics. Generation remains nonrepairable. This feature does not add repair cancellation, retargeting, rate changes, teams, or recovery policy.

Positive shield absorption interrupts repair of the actual shield installation. Positive penetration interrupts repair of the actual receiver. Damage elsewhere leaves repair running. Preserve exact invalidation of already-dequeued completion work at equal timestamps; do not weaken ordinary missing/orphan validation.

## Specialized state and observations

Associate directed-energy readiness with the installed weapon and active scan continuation with the installed sensor. Persist those associations. Validate kind, presence, timing, and exact work correlation. Keep specialized state typed and require exactly the state justified by the installed capabilities; do not populate arbitrary nullable fields for every kind.

Ship motion, actor contact knowledge, strategic orders, and defensive stimulus remain ship-owned. Discovering an installation identity does not create a global known-vessel identity. Review fixed-step/materialization guards as well as obvious lookups: sensor repair must still expand observation at the correct simulation boundaries after its target changes from kind to installation ID.

The scheduler remains the existing Ship/Faction scheduler. An installation-bound repair or scan can be correlated through its ship-owned operation; this refactor does not justify a third target domain or a new polling work kind. All accepted and rejected transitions preserve established deterministic ordering and atomicity.

## Remote targeting must not become an inventory probe

Own-ship repair, allocation, and component selection use true installed IDs. Remote fire continues to use an observer-local contact and a semantic aim kind; the attacker does not receive the target's true installed IDs or current inventory.

Public aim choices come from admitted target-kind definitions and permitted observer knowledge, not enumeration of hidden target installations. In particular, do not use the attacker's own installed inventory as the remote target-kind catalog: a ship without shields may still aim at shields on another vessel. Internal receiver eligibility derives from the victim's actual installed definitions; there is no parallel manually curated common damage list.

An absent hidden target installation must not produce a free `TargetSystemUnavailable` rejection, change a pre-shot button, or otherwise disclose presence. For the newly representable absent-kind case, a shot that passes the existing actor-safe firing prerequisites discharges normally and consumes its ordinary readiness interval. Resolve shield absorption normally; residual directed damage has no subsystem receiver when the aimed kind is absent and must not be redirected to another system or invented hull pool. The existing delayed defense stimulus still follows its legitimate observation rule for the shot.

Attacker feedback reports only the discharge and permitted qualitative shield interaction or beam penetration, with subsystem damage unconfirmed. It must not confirm that the aimed installation exists or was damaged. Equivalent observer information and equivalent shield interaction must yield equivalent attacker-visible results whether that hidden receiver exists or not. The owner of the victim may receive its own actual damage events. Unknown aim kinds remain ordinary actor-safe command validation failures; this is not permission to accept malformed input.

This closes a contradiction in the earlier handoff between hidden-loadout protection and an absence-specific refusal. It preserves current canonical M6A numerical outcomes while making heterogeneous targets respect the existing information boundary; it does not introduce exact-installation targeting or a new identification model.

## Compatibility and current-format capture

Ship content V6, system-definition content V1, and saves V10 under the explicit rules identity `installed-ship-system-substrate-v1` are implemented on the development branch. Content-family versions and save versions are separate.

V10 capture must serialize current installed state directly. Do not capture a V10 world into V8/V9's fixed five fields and then upgrade it; that would lose heterogeneous installations or leak legacy assumptions into current serialization. Existing bounded snapshot subrecords for unrelated state may be reused where their contracts are unchanged.

Supported historical DTOs and fixtures retain their wire meaning. Legacy executable adapters may be changed only as needed to translate into current domain types, with regressions; freezing DTOs does not require preserving obsolete runtime constructors forever. Each migration has fixed source and target version/rules constants. An older migration must not start emitting V10 because it uses a mutable `CurrentSchemaVersion` constant.

Use an explicit version-qualified mapping from each supported historical ship definition and historical system kind to a compatible component definition and stable installation ID. Do not infer that mapping from the current default loadout, its order, labels, or the first matching definition. V9's five represented conditions map deterministically, including installed-but-offline combat systems; zero condition is not evidence of absence. Preserve allocations, repair start/target/times/work ID, scan continuation, weapon readiness, counters, contacts, orders, factions, reports, and all scheduler identity/order. The earlier supported chain composes through V9; preserve valid null legacy observation frames and zero-faction worlds.

Resolve component compatibility explicitly. A stable definition ID alone cannot prove unchanged semantics. Use the existing content-compatibility facilities where adequate and document a bounded version/fingerprint or equivalent check for relevant component semantics and ordering. Missing, ambiguous, or incompatible historical definitions fail with actionable diagnostics, not silent defaults. Compatible changes to a class's default loadout must not replace a saved live loadout. Do not promise identical replay across arbitrary content changes.

Re-derive current save-size and work bounds from legal combined shapes. Retain the 128 MiB envelope unless separately justified. Include allocator growth, definition IDs, installed records, allocation and specialized continuation, and unchanged high-width contact/faction state. Distinguish measured fixtures from conservative supported-shape bounds.

## Generic presentation and refit readiness

**Implemented on the development branch.** Core projects an ordered list of the player's actual installations plus ship totals and existing specialized views (`EngineeringProjection`, `InstalledSystemProjection`, `GameSimulation.Projection.cs`). Generic Balance, Prioritize, and Begin Repair actions carry installed IDs with Core-owned availability and reasons, bound to the owning ship and load generation (`OwnShipActionBinding`, `GameScreen._simulationGeneration`) so a stale control cannot act on another installation after refresh or quick-load. Absent installations create no fake Engineering row. A lack of a specialized capability has an explicit unavailable status ("UNAVAILABLE") in the relevant specialized panel, e.g. `SensorProjection.SensorInstallation` is null for an absent sensor. The per-kind `EngineeringAction`, `PowerAllocationPreset`, and the temporary `GameSimulation.EngineeringAdapter.cs` bridge are removed; Godot switches only on `EngineeringOperation`.

Stable UI action keys include operation and installed identity, never list position or display name. Resolve the current payload at activation; an obsolete control cannot act on another installation after refresh/load. Preserve keyboard/mouse paths, focus, preview isolation, and current Combat behavior. Player refit controls are outside this feature.

The state and persistence must be ready for later validated installation/removal/replacement/modification. This feature need not implement those gameplay transitions or decide replacement-condition/cooldown carryover, physical slots, compatibility matrices, facilities, costs, or time. No mutable collection escape hatch is allowed. Test divergent loadouts through typed bootstrap and persistence, not a debug-only mutation path advertised as a completed refit feature.

## Admission proof and enforcement

Issue #121 is not complete until the following evidence is in its Final PR and the canonical gate:

- Characterize the reviewed five-system baseline before changing it. Preserve ordinary sensing, scanning, power, motion, shield/damage, repair interruption, delayed defense, withdrawal, and continuation with semantic comparisons and the exact current proof values. Historical test totals are evidence, not acceptance targets.
- Use arbitrary, nonconsecutive installation IDs and reordered definitions/entries. Prove per-ship isolation, deterministic common order, bounds, and allocation conservation independently of the implementation helper.
- Exercise two ships of one design with distinct loadouts, an alternate component definition of an existing kind, and legitimate absence. Save/load must retain each actual loadout even when a compatible class default differs.
- Prove multiple same-kind common records without claiming unsupported specialized aggregation. Validate separate typed cardinality errors instead of silent selection or collapse.
- Prove repair/scan/weapon instance references, malformed-candidate rejection, exact work cancellation, equal-time collisions, and current versus legacy continuation. Failed commands/loads leave state, time, events, scheduler, and allocators unchanged.
- Add paired hidden-loadout tests covering pre-shot projection, availability, command outcome, feedback, and readiness. An absence-only difference must not become a targeting oracle.
- Add durable architecture tests against duplicate current common-state authority and per-kind generic command/DTO fields. Exempt explicitly historical DTOs, compatibility translators, and legitimate typed behavior. String-search counts alone are not conformance proof.
- Demonstrate extension with another definition/installation using existing common capabilities without editing common algorithms. No production sixth kind, universal behavior framework, or new package is needed to manufacture this proof.
- Run populated persistence and long-horizon scenarios, including high-width combined state, plus Godot controls/absence/focus/quick-load coverage. Reproduce and correctly resolve or isolate the previously observed `grab_focus` diagnostic; passing test counts do not make unexpected engine errors harmless.

[Development governance](development-and-governance.md) owns readiness and verification. This documentation establishes required evidence; it does not claim those new runtime conformance tests already exist. The Final must show actual tests, reviewed source changes, named-page reconciliation, and passing checks on its exact final head. No policy, test, warning, or validation weakening is an acceptable substitute.

## Source baseline

[ADR 0014](../adr/0014-use-an-extensible-bounded-ship-system-substrate.md), [Issue #121](https://github.com/L3DigitalNet/star-trek-alter-course/issues/121), [installed-system identities](../../src/AlterCourse.Core/Ships/InstalledSystem.cs), [installed-system collection](../../src/AlterCourse.Core/Ships/InstalledSystemCollection.cs), [cardinality admission](../../src/AlterCourse.Core/Ships/ShipSystemAdmission.cs), [Engineering state](../../src/AlterCourse.Core/Ships/ShipEngineeringState.cs), [exact allocation](../../src/AlterCourse.Core/Ships/PowerAllocation.cs), [repair state](../../src/AlterCourse.Core/Ships/SystemRepairState.cs), [system-definition catalog](../../src/AlterCourse.Godot/content/systems/pathfinder-systems.json) and [schema](../../src/AlterCourse.Godot/content/schemas/system-definition-v1.schema.json), [ship content V6](../../src/AlterCourse.Godot/content/ships/pathfinder.json) and [schema](../../src/AlterCourse.Godot/content/schemas/ship-definition-v6.schema.json), [V10 save models](../../src/AlterCourse.Core/Persistence/SaveModelsV10.cs) and [capture/migration](../../src/AlterCourse.Core/Persistence/GamePersistence.V10.cs), [installed-system identity tests](../../tests/AlterCourse.Core.Tests/Ships/InstalledSystemIdentityTests.cs), [cardinality bootstrap test](../../tests/AlterCourse.Core.Tests/Gameplay/HeterogeneousBootstrapTests.cs), [V10 compatibility tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV10CompatibilityTests.cs), [V10 substrate tests](../../tests/AlterCourse.Core.Tests/Persistence/GamePersistenceV10SubstrateTests.cs), [Engineering projection tests](../../tests/AlterCourse.Core.Tests/Player/EngineeringProjectionTests.cs), [Godot Engineering workspace](../../src/AlterCourse.Godot/src/Gameplay/EngineeringWorkspace.cs), [own-ship action binding](../../src/AlterCourse.Godot/src/Gameplay/OwnShipActionBinding.cs), [kind presentation table](../../src/AlterCourse.Godot/src/Gameplay/EngineeringKindPresentation.cs), and [shell tests](../../src/AlterCourse.Godot/tests/GameplayShellTest.gd) establish the implemented runtime, persistence, and presentation baseline for this migration. [Substrate conformance tests](../../tests/AlterCourse.Core.Tests/SubstrateConformanceTests.cs), the [extension demonstration](../../tests/AlterCourse.Core.Tests/SubstrateExtensionTests.cs), [heterogeneous continuation tests](../../tests/AlterCourse.Core.Tests/Gameplay/HeterogeneousLoadoutContinuationTests.cs), and [long-horizon tests](../../tests/AlterCourse.Core.Tests/Gameplay/M6CombatLongHorizonTests.cs) guard the integrated result.
