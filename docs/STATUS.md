# Project Status

## Current snapshot

- v0.6.0 is the current immutable source-only GitHub Release, published 2026-09-08T01:04:05Z; it has no assets.
- Signed tag `v0.6.0` targets Final PR #97's release merge `d00460ea8b472c44ea2a8343d43e676efb96000b` and exactly tested tree `13bbe41`.
- Milestone 3A first observed contact, Milestone 4 Engineering Backbone, and Strategic Contact Reporting are released; Features #58, #62, and #77 are Done.
- Strategic Contact Reporting merged into `dev` as `80c3084` (Final PR #78) and shipped in v0.5.0 through release Task #80 / Final PR #82 on 2026-09-06.
- Current released rules are `observation-driven-faction-response-v1`, content schema V4, and save schema V8; V6/V7 migrations remain noninventive.
- `main` contains Final PR #97's v0.6.0 release merge. Post-merge Verify #34175359813 succeeded.
- Supporting PR #98 synchronized `main` to `dev` as `fb58ffe1b6781bd816ac7888dd2eec545bc8129d`; at synchronization, `origin/dev` contained `main` and their trees matched.
- All sync hosted checks, including fresh Canonical verification #34175435539, passed.
- v0.5.0 verification passed: Core 406, AssetCtl 324, Godot 1+2+63, smoke OK, and zero warnings or errors; later PRs record their own checks.
- [Faction Intent and Autonomous Assignment](wiki/faction-intent-and-autonomous-assignment.md) merged into `dev` as `0217296` through Final PR #87.
- Features #86 and #93 are released in v0.6.0.
- Q-05 is resolved and Q-04 is partial for the first bounded M5 slice. Q-02/Q-03/Q-06/Q-08/Q-14 remain future or partly scoped work.
- The shell retains strategic travel, tactical movement, Engineering power and repair, deterministic time controls, quick save/load, and last-known contact reporting.
- Core owns plural ordinary `ShipState`; Godot projects player-visible state and does not own authoritative simulation state.
- The tracked launch script restores and builds before Godot starts, preventing stale local Debug content after branch changes.
- [Design wiki](wiki/README.md) is the single source of truth; the bounded direct-faction assignment is implemented while organization and hierarchy runtime remain future work.
- M3 and M5 remain incomplete. The released bounded faction slice adds no dependencies or UI; broader political runtime is future work.
- Design phase Final PR #92 merged as `ad73862`; Task #91 is Done and invalid staging PR #90 was closed unmerged; its branch was deleted.
- PR #92 passed all five final-head checks, Branch policy, and post-merge Verify; design docs and ROADMAP changed without runtime code.
- Feature #93's V8 observation response is released in v0.6.0.
- Release tree verification passed: Core 598, AssetCtl 324, Godot 1+2+67, smoke, warning-free checks, all text gates, standards 38, and frontmatter/handoff 0.
- Independent release review was confirmed, and all five hosted Final PR #97 checks passed. M6/Q-10 follows; M3/M5 remain incomplete.
- Handoff `9ebee7b` used the ADR 0013 direct route; GitHub reported a configured PR/check bypass, contrary to Feature #93's no-bypass constraint.
