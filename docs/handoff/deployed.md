# Deployed State

## Current environments

- v0.6.1 is the immutable source-only GitHub Release: `Star Trek: Alter Course v0.6.1`.
- Release URL: <https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.6.1>.
- Signed annotated tag `v0.6.1` targets Final PR #103's release merge `f0af2653ca44f17b9f701f6271e199cda1429d16`.
- The release is non-draft, non-prerelease, and has zero assets. Source launch uses `./scripts/launch-game.sh`.
- `main` contains the v0.6.1 release merge. The required `main`-to-`dev` synchronization pull request is in progress.
- PR #101 fixed verification build-server reuse. Release and Debug verification passed with Core 598, AssetCtl 324, Godot integration and smoke checks.
- A post-verification scan found no remaining `dotnet`, `MSBuild`, or `VBCSCompiler` processes.
- GitHub Actions runs canonical C# and Godot verification, structured-text formatting, Markdown lint, and standards validation on pull requests and `main`.
- `main` branch protection strictly requires the GitHub Actions `Canonical verification` check for all actors, including administrators.
- Force pushes and branch deletion are disabled for `main`; pull request #3 and its post-merge workflows are the initial green deployment evidence.
