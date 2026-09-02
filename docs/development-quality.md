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
- The exact .NET SDK selected by [`global.json`](../global.json), with roll-forward disabled. The resolver supplies that SDK plus the .NET 8 runtime required by Godot.
- Node 24 with `npx`, selected by [`.node-version`](../.node-version). Repository scripts reject a different Node major before running npm-based tools.
- Godot 4.7.2 stable .NET/C#. The verifier accepts an exact matching `GODOT_BIN` or `godot` command, or downloads the checksum-pinned official editor to the user cache.
- GdUnit4 6.2.0, vendored from upstream commit `d18770221c2df4a3c991a42fdce7907df40eea75` under the Godot project.

The Core, Godot, and Core-test projects target .NET 8. AssetCtl and its tests target .NET 10. All projects use the repository-wide C# 12 baseline from [`Directory.Build.props`](../Directory.Build.props).

Repository-local .NET tools and checksum-pinned native tools are restored automatically. Native binaries are cached outside the repository and never replace globally installed tools.

## Normal workflow

Apply safe formatting, then run the complete gate:

```bash
./scripts/fix.sh
./scripts/verify.sh
```

`fix.sh` runs CSharpier for repository-owned C#, Prettier for tracked Markdown and structured configuration, and `shfmt` for shell scripts and Git hooks. `verify.sh` checks their output, locked dependencies, markdownlint, ShellCheck, actionlint, gitleaks, diagnostic-suppression and solution-configuration policy, a warning-free solution-wide Release build, Core and AssetCtl .NET tests, offline read-only AssetCtl configuration and catalog validation, and Godot integration. After proving the solution's Release mapping, verification builds the Godot project explicitly as Debug because the Godot editor runtime loads that managed configuration for GdUnit and headless smoke tests.

CSharpier is the sole C# whitespace formatter. Bare `dotnet format` and `dotnet format whitespace` are noncanonical because Roslyn's formatter can produce whitespace that CSharpier changes. `.editorconfig`, SDK analyzers, and Meziantou own semantic style; the compiler owns language correctness. Private instance fields use `_camelCase`, while private constants and static readonly fields use PascalCase.

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

## Testing framework availability

xUnit is installed for ordinary .NET tests, and vendored GdUnit4 runs the current Godot integration tests. ADR 0009 selects CsCheck for qualifying property/model tests, GdUnit4Net for C# tests that genuinely require the engine runtime, and ArchUnitNET for architecture rules that the project graph cannot express. Those three remain admission-triggered and must not be added until their stated need exists.

## Managed Markdown policy

The repository-owned gate runs Prettier and markdownlint over the same tracked Markdown and structured-text configuration adopted by Project Standards. Managed Project Standards workflows remain complementary: they own externally managed formatting, Markdown structure, and frontmatter policy and are intentionally not reproduced by `verify.sh`. Keep overlapping formatter and linter versions aligned when the managed package changes.

## Enforcement philosophy

- Canonical CI runs the same `./scripts/verify.sh` implementation used locally; managed Project Standards workflows remain separate policy checks under ADR 0002.
- Compiler and analyzer warnings are build failures. Fix causes instead of suppressing diagnostics or weakening central settings.
- CSharpier owns C# whitespace; EditorConfig owns semantic style and analyzer severity; editor integrations are conveniences.
- `AlterCourse.Core` must remain independently buildable and testable without Godot. Godot nodes and resources belong in `AlterCourse.Godot`.
- Behavioral changes and regressions require tests at the lowest layer that can prove them.

Repository settings should require the `Canonical verification` status check before merging to `main`.
