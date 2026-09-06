# Project Status

## Current snapshot

- v0.5.0 is the current immutable source-only GitHub Release: `0547d061ca4bde76b382274a16e079f46cd076d8`, tagged `v0.5.0`; no assets are published.
- Milestone 3A first observed contact, Milestone 4 Engineering Backbone, and Strategic Contact Reporting are released; Features #58, #62, and #77 are Done.
- Strategic Contact Reporting merged into `dev` as `80c3084` (Final PR #78) and shipped in v0.5.0 through release Task #80 / Final PR #82 on 2026-09-06.
- Content schema V4 and save schema V6 are current.
- `main` contains the release merge; sync PR #83 brought that release ancestry into `dev` at `e761249b220e827c39b4d18a0aa786dae93e9d87`.
- v0.5.0 verification passed: Core 406, AssetCtl 324, Godot 1+2+63, smoke OK, and zero warnings or errors; later PRs record their own checks.
- No development slice is admitted after v0.5.0; next-slice selection is an owner decision.
- The shell retains strategic travel, tactical movement, Engineering power and repair, deterministic time controls, quick save/load, and last-known contact reporting.
- Core owns plural ordinary `ShipState`; Godot projects player-visible state and does not own authoritative simulation state.
- The tracked launch script restores and builds before Godot starts, preventing stale local Debug content after branch changes.
- [Design wiki](wiki/README.md) is the single source of truth for design; [political design](wiki/factions-and-organizations.md) is approved, not implemented.
- Task #70 aligned README, CONTRIBUTING, agent instructions, handoff, and supporting design documents with that authority; `docs/wiki/` is a protected T0 surface.
- [Open questions](wiki/open-questions.md) records deferred choices; the proposed M3B sequence is not an admitted gameplay plan.
