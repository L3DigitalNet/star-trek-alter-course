---
schema_version: '1.1'
id: 'runbook-96lglv-development-quality'
title: 'Development Quality'
description: 'Canonical setup and verification workflow for Godot, C#, and AssetCtl development.'
doc_type: 'runbook'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
tags:
  - 'development'
  - 'testing'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0002-use-one-canonical-quality-gate.md'
---

# Development quality

`./scripts/verify.sh` is the canonical quality gate for local developers, agents, editors, and CI. It is read-only for tracked files and must pass before code is complete.

## Required environment

- Linux x86_64 with Git, Bash, `curl`, `tar` with xz support, `unzip`, and `sha256sum`.
- .NET SDK 10.0.111, selected by [`global.json`](../global.json). Projects target .NET 8 for Godot compatibility.
- Godot 4.7.2 stable .NET/C#. The verifier accepts an exact matching `GODOT_BIN` or `godot` command, or downloads the checksum-pinned official editor to the user cache.
- GdUnit4 6.2.0, vendored from upstream commit `d18770221c2df4a3c991a42fdce7907df40eea75` under the Godot project.

Repository-local .NET tools and checksum-pinned native tools are restored automatically. Native binaries are cached outside the repository and never replace globally installed tools.

## Normal workflow

Apply safe formatting, then run the complete gate:

```bash
./scripts/fix.sh
./scripts/verify.sh
```

`fix.sh` runs CSharpier and `shfmt`. `verify.sh` checks locked dependencies, C# and shell formatting, ShellCheck, actionlint, gitleaks, diagnostic-suppression policy, a warning-free Release build, Core and AssetCtl .NET tests, offline read-only AssetCtl configuration and catalog validation, GdUnit integration tests, and Godot headless startup.

## AssetCtl development

The standalone .NET 10 tool references neither game project nor Godot. Run it from any directory inside the repository; it locates the repository root and keeps command results on standard output while diagnostics go to standard error.

```bash
dotnet run --project tools/AlterCourse.AssetCtl -- validate-config --offline --output json
dotnet run --project tools/AlterCourse.AssetCtl -- doctor --output json
dotnet run --project tools/AlterCourse.AssetCtl -- status --output json
```

Tracked configuration in `config/assets/` is authoritative for provider instances, endpoints, credential environment-variable names, models, capabilities, economics, routes, quality tiers, and styles. Never put credential values in YAML. The committed policy denies paid generation; enabling it requires an untracked `.assetctl/config.local.yaml` owner override with bounded spend limits. Provider calls are never part of canonical verification.

To create or refresh a development placeholder, first search the catalog, then generate from an existing manifest. `--offline` selects only a local endpoint-free target, and `--dry-run` reports the plan without provider calls or tracked writes.

```bash
dotnet run --project tools/AlterCourse.AssetCtl -- find --query engineering --output json
dotnet run --project tools/AlterCourse.AssetCtl -- generate \
  --asset-id tooling.assetctl.fixture.generated-marker-svg \
  --offline \
  --output json
```

Every published asset and manifest move as one rollback-safe pair. An approved asset is immutable: replacement requires a new semantic asset ID and `supersedes` record. Run `approve` or deprecate an approved asset only with explicit current owner authorization; approval requires the exact asset ID confirmation, actor, note, unchanged hash, passing validation, and complete non-placeholder rights data.

## Deep validation

Mutation testing is intentionally outside the fast gate. Run it when simulation behavior or its tests change materially:

```bash
./scripts/test-mutation.sh
```

Stryker is pinned but has no mutation-score threshold until the simulation suite supplies an evidence-based baseline.

## Enforcement philosophy

- CI is authoritative and runs the same `./scripts/verify.sh` implementation used locally.
- Compiler and analyzer warnings are build failures. Fix causes instead of suppressing diagnostics or weakening central settings.
- CSharpier owns C# whitespace; EditorConfig owns semantic style and analyzer severity; editor integrations are conveniences.
- `AlterCourse.Core` must remain independently buildable and testable without Godot. Godot nodes and resources belong in `AlterCourse.Godot`.
- Behavioral changes and regressions require tests at the lowest layer that can prove them.

Repository settings should require the `Canonical verification` status check before merging to `main`.
