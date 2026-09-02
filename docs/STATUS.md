# Project Status

## Current snapshot

- Feature #36 completed Milestone 1 through merged Final PR #37 (`eb3c976`); manual gameplay testing is the next release-facing step.
- Version 0.1.0 remains the first source-only release. No packaged gameplay artifact is published.
- The shell retains strategic travel, local tactical movement, sensor repair, deterministic time controls, advance-until-event, and quick save/load.
- The proof world has three vessels: USS Pathfinder at Dawn Anchor, USS Wayfarer repairing at Vesper Reach, and USS Horizon traveling to Meridian Drift.
- Core owns an immutable ship-definition catalog and plural ordinary `ShipState`; `PlayerShipId` selects one ship for commands and projection.
- Strategic, tactical, sensor, repair, and scheduled consequence state is ship-owned. Targeted same-kind work cannot cross ship identities.
- Ships use stable identity order, removing collection insertion order from simulation semantics.
- Typed `GameBootstrap` consumes declared starting state. `FirstGameSetup` produces the three-ship proof world.
- NPC orders, AI, and autonomous next-step selection remain Milestone 2 work.
- Ship-definition schema V2 separates reusable capability from vessel names and starting sensor condition.
- The proof world reuses one validated Pathfinder-class definition for three vessel instances.
- Save schema V2 persists bounded plural world state and definition references; authored definitions remain external.
- V1 migration creates the one representable ship, targets legacy work to it, and validates the V2 candidate before construction.
- The generic quick-save slot is `user://quick-save.json`. Only the unchanged default path may discover the legacy `user://quick-save-v1.json` file when the generic slot is absent.
- World admission is capped at 256 ships and simulation advancement at 1,000,000 ship-steps per call, bounding untrusted inputs and fixed-step work at current development scale.
- Godot projects player-visible state and adapts coordinates, input, and elapsed presentation time; it neither owns authoritative ships nor exposes unrestricted NPC truth.
- The solution uses a one-way Godot-to-Core project reference, exact .NET SDK 10.0.111, C# 12, Godot 4.7.2 .NET, .NET 8 runtime support, Node 24, xUnit, and GdUnit4 6.2.0.
- `scripts/verify.sh` is the canonical read-only gate for formatting, analysis, policy, builds, tests, Godot integration, security, and smoke checks.
- AssetCtl remains an isolated .NET 10 tool with bounded inputs, offline-first defaults, safe publication, provenance, and Godot import validation.
- `dev` is the protected development branch and `main` is release-only. Significant work follows the governed issue, topic-branch, and pull-request workflow in ADR 0013.
- Combat, engineering depth, factions, diplomacy, economy, missions, narrative, networking, autonomous NPC behavior, and final art remain deferred.
