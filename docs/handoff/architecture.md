# Architecture

## Component map

This is the current operational component map, not an alternate design contract. The [wiki architecture](../wiki/architecture.md) owns design context; the [ADR catalog](../adr/README.md) owns architectural navigation and scope. Update this map when component ownership changes, not by appending contradictory snapshots.

- `AlterCourse.Core` owns pure simulation, immutable definitions, command application, and snapshot mapping. It has no Godot reference and remains independently testable.
- `AlterCourse.Godot` owns nodes, scenes, resources, input, UI, rendering, and adapters. It references Core; the reverse dependency is prohibited.
- `AlterCourse.AssetCtl` is independent development tooling with no references to/from either game assembly. Godot consumes selected asset files rather than provider APIs.
- Core tests use xUnit with focused architecture/property checks; current Godot integration uses vendored GdUnit4 against the engine runtime. Package admission and exact versions remain in their owning records/configuration.

## World, commands, and time

`GameSimulation` owns live authoritative state. `SimulationState` retains canonically ordered ships/factions, map, scheduler, player identity, and identity allocators. Player identity selects an ordinary vessel rather than a separate encounter model.

Core mutations stage correlated candidates and validate the complete boundary before commitment. Expected refusal of an atomic command preserves state, time, IDs, and work. Post-commit diagnostic or presentation failure does not make the operation unapplied. [ADR 0015](../adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md) owns this contract while preserving ADR 0007's separately specified safely incremental alternative.

The scheduler uses closed Ship/Faction targets, known data-only work kinds, exact ownership/correlation, stable same-time sequence, and finite budgets. Strategic-only intervals advance event-to-event; tactical/contact-sensitive work uses 100 ms boundaries when required, and repairs materialize analytically. Wall-clock time and scene lifecycle are not simulation authority. Numeric exhaustion fails explicitly rather than promising an indefinite successor.

Ordinary ships may own `TravelTo`, `PatrolRoute`, or `HoldUntil` orders. Orders and physical travel are distinct. Execution reuses targetable travel; cancellation removes only the order and exact work it owns, not a voyage already underway. Collection ordering and scheduler sequence are deterministic; authored map ordering retains its defined meaning.

[World, navigation, and time](../wiki/world-navigation-and-time.md) owns current capacities, work budgets, bootstrap, and detailed progression. Prototype bounds are not final galaxy-scale promises. There is no current authoritative RNG consumer; the first consumer requires ADR 0007's complete versioned source/state/continuation contract.

## Knowledge and faction boundaries

[ADR 0016](../adr/0016-own-actor-knowledge-and-share-immutable-observation-reports.md) separates world truth, actor-local contacts, own-asset administrative facts, and historical received reports. Hidden target correlation does not escape into policy inputs, projections, reports, or command feedback. Local contact IDs do not establish global or cross-observer vessel identity.

The implemented faction-assignment slice commands eligible idle directly controlled NPC ships through ordinary orders. One asset-side controller is authoritative; the roster is derived. Presence policy sees admitted own-asset facts rather than sensor knowledge. Investigation uses fresh immutable received reports and approved own-asset/routes, not current hidden target state.

Typed bootstrap constructs complete faction/ship state and work. Historical V7 migration created no factions/controllers; V7→V8 created no reports, investigations, or response history. Released v0.6.0 uses V8. These historical admissions remain part of the migration chain, not the current capture format.

Player-relevant advancement may process hidden work without exposing it. Results are player-semantic events; scheduler traces, NPC objectives, received faction reports, and pending intent remain private. Broader hierarchy, organizations, affiliation learning, political UI, or player-command override are not implied by the bounded implemented faction slices.

## Ship-system substrate (Issue #121)

[ADR 0014](../adr/0014-use-an-extensible-bounded-ship-system-substrate.md)'s substrate is implemented through Final PR #123, merged as `17637dd` and unreleased. Kind, reusable definition, and installed identity are distinct. Five existing kinds use common installed storage with typed specialized behavior and cardinality rules.

A ship definition supplies an initial loadout; each live vessel owns actual installations independently of later defaults. Common condition, allocation, repair, damage, projection, and snapshots use installed identity rather than five parallel mutable fields. Engineering owns the installed set and one exact repair; readiness and scan continuation reference their actual installations.

The current production loadout reproduces M6A's five kinds and four consumers, but those counts are not the storage model. Remote semantic aim remains actor-safe and does not disclose inventory. Own-ship Engineering can expose its legitimate installed inventory and actions. Recovery, refit gameplay, aggregation, and further kinds remain separate decisions.

The [substrate contract](../wiki/ship-system-substrate.md) and [Engineering/combat](../wiki/engineering-and-combat.md) own mechanics and proof. PR #123 retains the integrated gate and tested-tree equivalence; this map is not a fresh execution record.

## Content and persistence

Current development uses ship content V6, system-definition content V1, and save V10 under `installed-ship-system-substrate-v1`. Snapshot capture reads current installations directly. It does not flatten through V9 fields or reconstruct live loadouts from class defaults.

The adjacent supported migration chain extends V1 through V10. Frozen historical validators and the V9→V10 table preserve earlier semantics; definitions and whole-catalog aim vocabulary must pass compatibility checks. Mismatch fails as `IncompatibleContent`, not silent reinterpretation. A zero-condition installation remains installed, not absent.

Loading constructs and validates a separate candidate before live installation. Definitions are supplied by the compatible immutable catalog rather than serialized live objects. Historical V1 name reconstruction and later non-inventive faction/combat additions remain version-specific migration facts. [Content, assets, and persistence](../wiki/content-assets-and-persistence.md) owns exact bounds, compatibility, failure handling, and historical measurements; this operational map does not duplicate those numbers.

## Godot session and presentation

`GameScreen` currently owns one session-lifetime simulation. Command, Engineering, and Combat are views, not separate worlds. [ADR 0018](../adr/0018-separate-simulation-session-lifetime-from-workspaces.md) permits a focused ownership extraction for a concrete consumer without requiring a global manager.

Successful replacement advances the presentation generation and invalidates retained actionable context even when IDs match. Own-ship Engineering uses owner/generation binding and current payload resolution. Ordinary refresh preserves valid selection/focus; deferred work rechecks target lifetime. Failed load preserves the playable world. Rate preference and fractional pre-load time carry have distinct replacement rules.

Godot owns display transforms, selection, input, and formatting. The tactical plot is player-centered; numeric Core coordinates remain truth. Preview fixtures are explicit, frozen presentation and cannot submit live commands, persist state, or substitute for unavailable Core data. [Interface](../wiki/interface-and-player-commands.md) owns concrete controls and lifecycle behavior.

`scripts/launch-game.sh` restores/builds before starting Godot to avoid stale local assemblies after branch changes. The canonical verifier separately covers integration and smoke.

## Asset tooling and diagnostics

[ADR 0017](../adr/0017-generate-assets-outside-the-game-through-bounded-validated-publication.md) owns bounded AssetCtl external operations, output validation, recoverable paired publication, and owner approval. Offline verification makes no paid generation calls. Approved replacement uses new semantic identity and supersession; provider output or AI review does not establish approval or legal clearance.

Serilog composition and Microsoft logging abstractions remain nonauthoritative under ADR 0008. Core diagnostics are allowlisted and post-commit. Logs are neither persistence nor a substitute for typed decision explanations or asset provenance.

## Standing backlog

Add new simulation behavior to Core and its tests as governed gameplay consumers are selected. Preserve existing ADR boundaries and update owning wiki contracts in the same work. The completed fourteen-ADR conformance review is historical evidence, not automatic certification of ADRs 0015–0018 or future behavior.
