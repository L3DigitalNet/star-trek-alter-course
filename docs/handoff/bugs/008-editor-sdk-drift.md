# 008: Editors could not resolve the pinned .NET SDK after a system patch update

## Cause

`global.json` pins SDK 10.0.111 with `rollForward: disable`. Fedora's `dotnet-sdk-10.0` package moved the system SDK to 10.0.112, and VS Code's C# tooling, which starts `/usr/bin/dotnet`, reported "No required version of a NET SDK was found". Repository scripts were unaffected because they prepend the cached SDK from `scripts/resolve-dotnet.sh`.

## Fix

Issue #114 and PR #115 (`7efb162`) added the .NET 10 `sdk.paths` order `[".dotnet", "$host$"]` and a resolver-naming `errorMessage` to `global.json`. The resolver maintains an untracked `.dotnet` link to its verified cache, which is excluded from Git and rexec sync. `scripts/test-resolve-dotnet.sh` guards the link and pin contract.

## Lesson

An exact SDK pin breaks every tool that does not use the resolver whenever the system SDK patches. If an editor reports a missing SDK, run `./scripts/resolve-dotnet.sh` once, then reload the workspace. Ignore and exclude the symlink with anchored patterns that have no trailing slash; a directory-only pattern does not match a symlink.
