# Project Status

## Current snapshot

- v0.5.0 is the current immutable source-only GitHub Release: `0547d061ca4bde76b382274a16e079f46cd076d8`, tagged `v0.5.0`; no assets are published.
- Milestone 3A first observed contact, Milestone 4 Engineering Backbone, and Strategic Contact Reporting are released; Features #58, #62, and #77 are Done.
- Strategic Contact Reporting merged into `dev` as `80c3084` (Final PR #78) and shipped in v0.5.0 through release Task #80 / Final PR #82 on 2026-09-06.
- Content schema V4 and save schema V6 remain current in v0.5.0. `dev` now implements its bounded M5 faction slice with V7; V6 historical-save migration remains noninventive.
- `main` contains the release merge; sync PR #83 brought that release ancestry into `dev` at `e761249b220e827c39b4d18a0aa786dae93e9d87`.
- v0.5.0 verification passed: Core 406, AssetCtl 324, Godot 1+2+63, smoke OK, and zero warnings or errors; later PRs record their own checks.
- [Faction Intent and Autonomous Assignment](wiki/faction-intent-and-autonomous-assignment.md) merged into `dev` as `0217296` through Final PR #87.
- Feature #86 is Done. The merge was verified against source-tree-identical head `09c673a`; Core 505, AssetCtl 324, Godot 1+2+65, smoke OK, and five hosted checks passed.
- Q-05 is resolved and Q-04 is partial for the first bounded M5 slice. Q-02/Q-03/Q-06/Q-08/Q-14 remain future or partly scoped work.
- The shell retains strategic travel, tactical movement, Engineering power and repair, deterministic time controls, quick save/load, and last-known contact reporting.
- Core owns plural ordinary `ShipState`; Godot projects player-visible state and does not own authoritative simulation state.
- The tracked launch script restores and builds before Godot starts, preventing stale local Debug content after branch changes.
- [Design wiki](wiki/README.md) is the single source of truth; broader [political design](wiki/factions-and-organizations.md) has no organization or hierarchy runtime.
- M3 and M5 remain incomplete. The merged slice adds no dependencies, UI, or broader political runtime; no release followed and `main` is unchanged.
- Task #88 / Final PR #89 adds recurring wiki reconciliation and corrects stale faction landing claims; no runtime change or release.
