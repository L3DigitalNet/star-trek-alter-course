# Deployed State

## Current environments

- v0.5.0 is the immutable source-only GitHub Release, `Star Trek: Alter Course v0.5.0 — Strategic Contact Reporting`; v0.4.0 remains its immutable predecessor.
- Signed annotated tag `v0.5.0` targets release merge `0547d061ca4bde76b382274a16e079f46cd076d8`; published 2026-09-06T19:46:49Z.
- The release is non-draft, non-prerelease, and has zero assets. Source launch uses `./scripts/launch-game.sh`.
- `main` contains the release merge; sync PR #83 brought that release ancestry into `dev` at `e761249b220e827c39b4d18a0aa786dae93e9d87`.
- GitHub Actions runs canonical C# and Godot verification, structured-text formatting, Markdown lint, and standards validation on pull requests and `main`.
- `main` branch protection strictly requires the GitHub Actions `Canonical verification` check for all actors, including administrators.
- Force pushes and branch deletion are disabled for `main`; pull request #3 and its post-merge workflows are the initial green deployment evidence.
