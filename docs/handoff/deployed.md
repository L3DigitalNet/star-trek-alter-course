# Deployed State

## Current environments

- v0.4.0 is the immutable source-only GitHub Release, `Star Trek: Alter Course v0.4.0 — First Contact & Engineering Backbone`.
- Signed annotated tag `v0.4.0` targets release merge `b3b6635470003d11260b99a2a56f03a3bfa201f6`; published 2026-09-03T23:16:48Z.
- The release is non-draft, non-prerelease, and has zero assets. Source launch uses `./scripts/launch-game.sh`.
- `main` contains the release merge; sync PR #67 brought that release ancestry into `dev` at `2edd19460d9b096863b9f3d8a2c2438c3b4dfab0`.
- GitHub Actions runs canonical C# and Godot verification, structured-text formatting, Markdown lint, and standards validation on pull requests and `main`.
- `main` branch protection strictly requires the GitHub Actions `Canonical verification` check for all actors, including administrators.
- Force pushes and branch deletion are disabled for `main`; pull request #3 and its post-merge workflows are the initial green deployment evidence.
