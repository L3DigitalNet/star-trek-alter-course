# Deployed State

## Current environments

- Source-only v0.2.0 is the immutable GitHub Release at `163b8e2`, with annotated tag `v0.2.0`.
- Its release page is [v0.2.0](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.2.0).
- No packaged gameplay artifact is published; source launch uses `./scripts/launch-game.sh`.
- GitHub Actions runs canonical C# and Godot verification, structured-text formatting, Markdown lint, and standards validation on pull requests and `main`.
- `main` branch protection strictly requires the GitHub Actions `Canonical verification` check for all actors, including administrators.
- Force pushes and branch deletion are disabled for `main`; pull request #3 and its post-merge workflows are the initial green deployment evidence.
