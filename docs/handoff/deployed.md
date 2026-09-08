# Deployed State

## Current environments

- v0.6.0 is the immutable source-only GitHub Release: `Star Trek: Alter Course v0.6.0`.
- Release URL: <https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.6.0>.
- Signed annotated tag `v0.6.0` targets Final PR #97's release merge `d00460ea8b472c44ea2a8343d43e676efb96000b`.
- The release was published 2026-09-08T01:04:05Z and exactly matches tested tree `13bbe41bd9b8d6c94934642d3dfdcc7a4ded1307`.
- The release is non-draft, non-prerelease, and has zero assets. Source launch uses `./scripts/launch-game.sh`.
- `main` contains the v0.6.0 release merge. Supporting PR #98 synchronized it to `dev` as `fb58ffe1b6781bd816ac7888dd2eec545bc8129d`.
- At synchronization, `origin/dev` contained `main`, their trees matched, and all sync hosted checks passed, including Canonical verification #34175435539.
- Main post-merge Verify #34175359813 succeeded.
- GitHub Actions runs canonical C# and Godot verification, structured-text formatting, Markdown lint, and standards validation on pull requests and `main`.
- `main` branch protection strictly requires the GitHub Actions `Canonical verification` check for all actors, including administrators.
- Force pushes and branch deletion are disabled for `main`; pull request #3 and its post-merge workflows are the initial green deployment evidence.
