# Project Status

## Current snapshot

- Feature #36 completed Milestone 1 through merged Final PR #37 (`eb3c976`); manual gameplay testing is the next release-facing step.
- Version 0.1.0 remains the first source-only release. No packaged gameplay artifact is published.
- The shell retains strategic travel, local tactical movement, sensor repair, deterministic time controls, player-relevant advance-until, and quick save/load.
- The proof world has three vessels: USS Pathfinder at Dawn Anchor, USS Wayfarer repairing at Vesper Reach, and USS Horizon traveling to Meridian Drift.
- Core owns an immutable ship-definition catalog and plural ordinary `ShipState`; `PlayerShipId` selects one ship for commands and projection.
- Strategic, tactical, sensor, repair, and scheduled consequence state is ship-owned. Targeted same-kind work cannot cross ship identities.
- Ships use stable identity order, removing collection insertion order from simulation semantics.
- Typed `GameBootstrap` consumes declared starting state. `FirstGameSetup` produces the playable three-ship world, while `Milestone2ProofSetup` supplies the long-horizon headless order scenario.
- Ordinary NPC ships can own one stable `ShipOrder`: one-shot `TravelTo`, bounded cyclic `PatrolRoute`, or time-only `HoldUntil`. The player ship cannot execute an autonomous order.
- Deterministic order execution reuses the same targetable Core travel command as player travel; cancellation removes only the identified order and its exact hold wake while preserving a physical journey already underway.
- Ship-definition schema V2 separates reusable capability from vessel names and starting sensor condition.
- The proof world reuses one validated Pathfinder-class definition for three vessel instances.
- Save schema V3 persists bounded plural world state, definition references, active orders, and the order allocator; authored definitions remain external.
- Adjacent V1-to-V2-to-V3 and V2-to-V3 migrations preserve historical rule identities. Old travel remains orderless because migration does not invent autonomous intent.
- The generic quick-save slot is `user://quick-save.json`. Only the unchanged default path may discover the legacy `user://quick-save-v1.json` file when the generic slot is absent.
- World admission is capped at 256 ships. Each advancement is bounded to 1,000,000 actual moving-ship tactical steps and 10,000 scheduled consequences at the current prototype scale.
- Strategic-only intervals jump event-to-event and update repairs analytically; ships with active local tactical motion retain deterministic 100 ms integration.
- Godot projects player-visible state and adapts coordinates, input, and elapsed presentation time; it neither owns authoritative ships nor exposes unrestricted NPC truth.
- The solution uses a one-way Godot-to-Core project reference, exact .NET SDK 10.0.111, C# 12, Godot 4.7.2 .NET, .NET 8 runtime support, Node 24, xUnit, and GdUnit4 6.2.0.
- `scripts/verify.sh` is the canonical read-only gate for formatting, analysis, policy, builds, tests, Godot integration, security, and smoke checks.
- AssetCtl remains an isolated .NET 10 tool with bounded inputs, offline-first defaults, safe publication, provenance, and Godot import validation.
- `dev` is the protected development branch and `main` is release-only. Significant work follows the governed issue, topic-branch, and pull-request workflow in ADR 0013.
- Actor knowledge, contacts, first contact, faction strategy, general mission assignment, tactical AI, combat, engineering depth, diplomacy, economy, narrative, networking, and final art remain deferred.
