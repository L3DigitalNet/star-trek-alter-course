# Deployed State

## Current environments

- The current immutable source-only GitHub Release is available at <https://github.com/L3DigitalNet/star-trek-alter-course/releases/latest>.
- v0.6.1 is the historical build-process correction release: `Star Trek: Alter Course v0.6.1`.
- Its signed annotated tag targets Final PR #103's release merge `f0af2653ca44f17b9f701f6271e199cda1429d16`.
- The v0.6.1 release is non-draft, non-prerelease, and has zero assets. Source launch uses `./scripts/launch-game.sh`.
- PR #104 synchronized the v0.6.1 release ancestry to `dev` as `5ad16bb`; Canonical verification passed.
- PR #101 fixed verification build-server reuse. Release and Debug verification passed with Core 598, AssetCtl 324, Godot integration and smoke checks.
- A post-verification scan found no remaining `dotnet`, `MSBuild`, or `VBCSCompiler` processes.
- GitHub Actions runs canonical C# and Godot verification, structured-text formatting, Markdown lint, and standards validation on pull requests and `main`.
- `main` branch protection strictly requires the GitHub Actions `Canonical verification` check for all actors, including administrators.
- Force pushes and branch deletion are disabled for `main`; pull request #3 and its post-merge workflows are the initial green deployment evidence.

## Local agent tooling

- 2026-09-26: Codex C# hover, documentation, and diagnostics succeeded.
- 2026-09-26: Codex Godot definition and diagnostics succeeded; see [agent setup](../development-agent-skills.md).
- 2026-09-26: Claude reported both MCP entries Connected at Project scope; Codex Figma `whoami` authenticated.
