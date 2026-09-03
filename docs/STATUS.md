# Project Status

## Current snapshot

- v0.4.0 is the current immutable source-only GitHub Release: `b3b6635470003d11260b99a2a56f03a3bfa201f6`, tagged `v0.4.0`; no assets are published.
- Milestone 3A first observed contact and Milestone 4 Engineering Backbone are released; Features #58 and #62 are Done.
- Content schema V4 and save schema V5 are current.
- `main` contains the release merge and `dev` is synchronized at `2edd19460d9b096863b9f3d8a2c2438c3b4dfab0`, with identical trees.
- Canonical verification is green: Core 376, AssetCtl 324, Godot 1+2+60, and zero warnings or errors.
- No gameplay feature is active or admitted. The next scope requires governed admission.
- The shell retains strategic travel, tactical movement, Engineering power and repair, deterministic time controls, and quick save/load.
- Core owns plural ordinary `ShipState`; Godot projects player-visible state and does not own authoritative simulation state.
- The tracked launch script restores and builds before Godot starts, preventing stale local Debug content after branch changes.
