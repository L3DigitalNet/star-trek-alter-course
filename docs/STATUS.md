# Project Status

## Current snapshot

- v0.5.0 is the current immutable source-only GitHub Release: `0547d061ca4bde76b382274a16e079f46cd076d8`, tagged `v0.5.0`; no assets are published.
- Milestone 3A first observed contact, Milestone 4 Engineering Backbone, and Strategic Contact Reporting are released; Features #58, #62, and #77 are Done.
- Strategic Contact Reporting merged into `dev` as `80c3084` (Final PR #78) and shipped in v0.5.0 through release Task #80 / Final PR #82 on 2026-09-06.
- Content schema V4 and save schema V6 remain current in v0.5.0. Unreleased development uses V8; V6/V7 migrations remain noninventive.
- `main` contains the release merge; sync PR #83 brought that release ancestry into `dev` at `e761249b220e827c39b4d18a0aa786dae93e9d87`.
- v0.5.0 verification passed: Core 406, AssetCtl 324, Godot 1+2+63, smoke OK, and zero warnings or errors; later PRs record their own checks.
- [Faction Intent and Autonomous Assignment](wiki/faction-intent-and-autonomous-assignment.md) merged into `dev` as `0217296` through Final PR #87.
- Feature #86 is Done. The merge was verified against source-tree-identical head `09c673a`; Core 505, AssetCtl 324, Godot 1+2+65, smoke OK, and five hosted checks passed.
- Q-05 is resolved and Q-04 is partial for the first bounded M5 slice. Q-02/Q-03/Q-06/Q-08/Q-14 remain future or partly scoped work.
- The shell retains strategic travel, tactical movement, Engineering power and repair, deterministic time controls, quick save/load, and last-known contact reporting.
- Core owns plural ordinary `ShipState`; Godot projects player-visible state and does not own authoritative simulation state.
- The tracked launch script restores and builds before Godot starts, preventing stale local Debug content after branch changes.
- [Design wiki](wiki/README.md) is the single source of truth; the bounded direct-faction assignment is implemented while organization and hierarchy runtime remain future work.
- M3 and M5 remain incomplete. The merged slice adds no dependencies or UI; broader political runtime is future work, no release followed, and `main` is unchanged.
- Design phase Final PR #92 merged as `ad73862`; Task #91 is Done and invalid staging PR #90 was closed unmerged; its branch was deleted.
- PR #92 passed all five final-head checks, Branch policy, and post-merge Verify; design docs and ROADMAP changed without runtime code.
- Feature #93 is Done: Final PR #94 squash-merged as `e7bdfe3` on 2026-09-07; V8 is unreleased and `main` is unchanged.
- The tested tree matched `567ee9d`; Core 598, AssetCtl 324, Godot 1+2+67, smoke, five hosted checks, and agent-operated manual proof passed.
- Post-merge Verify #34171747308 passed. M6/Q-10 follows; M3/M5 remain incomplete.
- Handoff `9ebee7b` used the ADR 0013 direct route; GitHub reported a configured PR/check bypass, contrary to this task's no-bypass constraint.
