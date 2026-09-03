# Project Status

## Current snapshot

- Features #36 and #38 completed Milestones 1 and 2 through merged Final PRs #37 (`eb3c976`) and #39 (`fba9b438`) on `dev`.
- Task #40 consolidated their gameplay shell through Final PR #41 (`ed8ca4b`); owner gameplay evaluation found the shell working well.
- Version 0.2.0 is the current immutable source-only GitHub Release, tagged at `163b8e2`; no packaged gameplay artifact is published.
- The shell retains strategic travel, local tactical movement, sensor repair, deterministic time controls, player-relevant advance-until, and quick save/load.
- The tracked launch script restores and builds before Godot starts, preventing stale local Debug content after branch changes.
- The proof world has three vessels: USS Pathfinder at Dawn Anchor, USS Wayfarer repairing at Vesper Reach, and USS Horizon traveling to Meridian Drift.
- Core owns an immutable ship-definition catalog and plural ordinary `ShipState`; `PlayerShipId` selects one ship for commands and projection.
- Strategic, tactical, sensor, repair, and scheduled consequence state is ship-owned. Targeted same-kind work cannot cross ship identities.
- Ships use stable identity order, removing collection insertion order from simulation semantics.
- Typed `GameBootstrap` consumes declared starting state. `FirstGameSetup` produces the playable three-ship world.
- `Milestone2ProofSetup` supplies the long-horizon headless patrol and hold scenario.
- NPC ships can own one stable `ShipOrder`: one-shot `TravelTo`, bounded cyclic `PatrolRoute`, or time-only `HoldUntil`.
- The player ship cannot execute an autonomous order. Order execution reuses the same targetable Core travel command as player travel.
- Cancellation removes only the identified order and its exact hold wake while preserving a physical journey already underway.
- Ship-definition schema V2 separates reusable capability from vessel names and starting sensor condition.
- The proof world reuses one validated Pathfinder-class definition for three vessel instances.
- Save schema V3 persists bounded plural world state, definition references, active orders, and the order allocator; authored definitions remain external.
- Historical `first-playable-v1` is validated at its source schema. V2-to-V3 writes current `active-world-orders-v1`, and old travel remains orderless.
- The generic quick-save slot is `user://quick-save.json`. Only the unchanged default path may discover the legacy `user://quick-save-v1.json` file when the generic slot is absent.
- World admission is capped at 256 ships. Each advancement allows 1,000,000 moving-ship steps and 10,000 scheduled consequences.
- Strategic-only intervals jump event-to-event and update repairs analytically; ships with active local tactical motion retain deterministic 100 ms integration.
- Godot projects player-visible state and adapts coordinates, input, and elapsed presentation time; it neither owns authoritative ships nor exposes unrestricted NPC truth.
- The tactical plot is player-centered and local, keeping legitimate sustained movement visible while numeric Core coordinates remain status truth.
- The solution uses a one-way Godot-to-Core project reference, exact .NET SDK 10.0.111, C# 12, Godot 4.7.2 .NET, .NET 8 runtime support, Node 24, xUnit, and GdUnit4 6.2.0.
- `scripts/verify.sh` is the canonical read-only gate for formatting, analysis, policy, builds, tests, Godot integration, security, and smoke checks.
- AssetCtl remains an isolated .NET 10 tool with bounded inputs, offline-first defaults, safe publication, provenance, and Godot import validation.
- `main` holds v0.2.0. Sync PR #44 merged it back into protected `dev` as `851845f`; significant work follows ADR 0013.
- Feature #47 merged through Final PR #48 as squash commit `b80c669` on `dev`; Issue #47 is closed Done and all CI passed.
- The Command Deck now retains live strategic travel, tactical-map continuity, persistent Engineering navigation, rate controls, selection, and save/load.
- Command, combat, and Engineering previews use deterministic non-authoritative presentation fixtures; live unavailable systems remain labelled `Unavailable`.
- The Godot Theme and owned font assets are the runtime presentation source; the Figma nodes and repository PNGs remain visual references only.
- No release or deployment followed Feature #47. Owner manual gameplay testing is required before any later release decision.
- Actor knowledge, contacts, first contact, faction strategy, general mission assignment, and tactical AI remain deferred.
- Combat rules, engineering depth, diplomacy, economy, narrative, networking, and final art remain deferred.
