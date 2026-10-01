---
schema_version: '1.1'
id: 'runbook-96lglv-development-quality'
title: 'Development Quality'
description: 'Canonical setup and verification workflow for Godot, C#, and AssetCtl development.'
doc_type: 'runbook'
status: 'active'
created: '2026-09-01'
updated: '2026-09-30'
tags:
  - 'development'
  - 'testing'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0002-use-one-canonical-quality-gate.md'
  - 'docs/adr/0017-generate-assets-outside-the-game-through-bounded-validated-publication.md'
  - 'docs/adr/README.md'
---

# Development quality

`./scripts/verify.sh` is the canonical quality gate for local developers, agents, editors, and CI. It is read-only for tracked files and must pass before work is admitted as complete. Read [CONTRIBUTING](../CONTRIBUTING.md) and the [ADR catalog](adr/README.md) for governing scope and admission.

## Required environment

- Linux x86_64 with Git, Bash, `curl`, `tar` with xz support, `unzip`, and `sha256sum`.
- The exact .NET SDK in [global.json](../global.json), with roll-forward disabled. The resolver supplies that SDK and the .NET 8 runtime Godot needs. `scripts/resolve-dotnet.sh` downloads the checksum-pinned SDK to the user cache and links untracked root `.dotnet`, which `global.json` searches before host SDKs. If an editor cannot resolve it, run the resolver once and reload the workspace.
- Node 24 with `npx`, selected by [`.node-version`](../.node-version). Scripts reject another Node major before npm-based tools run.
- Godot 4.7.2 stable .NET/C#. The verifier accepts an exact matching `GODOT_BIN` or `godot`, or downloads the checksum-pinned editor to the user cache.
- GdUnit4 6.2.0, vendored from upstream commit `d18770221c2df4a3c991a42fdce7907df40eea75` under the Godot project.

Core, Godot, and Core tests target .NET 8; AssetCtl and its tests target .NET 10. All use the C# 12 baseline from [Directory.Build.props](../Directory.Build.props). Exact package/tool versions remain in configuration and lock files; these are repository selections, not claims about newest upstream releases.

Repository-local .NET tools and checksum-pinned native tools restore automatically. Native binaries are cached outside the repository without replacing global installations.

## Normal workflow

Apply supported formatting, then run the complete gate:

```bash
./scripts/fix.sh
./scripts/verify.sh
```

`fix.sh` runs CSharpier for repository-owned C#, Prettier for tracked Markdown/configuration, and shfmt for shell/hooks. `verify.sh` checks those outputs, locked dependencies, markdownlint, ShellCheck, actionlint, gitleaks, diagnostic/solution policies, warning-free solution Release build, Core and AssetCtl tests, offline read-only asset configuration/catalog validation, language-server transport tests, and Godot integration/smoke.

After proving the solution's Release mapping, verification explicitly builds Godot as Debug because the editor runtime loads that managed configuration for GdUnit and smoke. This is not permission to silently remap Release to Debug in the solution.

CSharpier alone owns C# whitespace. Bare `dotnet format` and `dotnet format whitespace` are noncanonical because Roslyn formatting can conflict with it. EditorConfig, SDK analyzers, and Meziantou own semantic style; the compiler owns language correctness. Private instance fields use `_camelCase`; private constants and static readonly fields use PascalCase.

## AssetCtl development

The independent .NET 10 CLI references neither game project nor Godot. It locates the repository root from an internal directory, writes command results to stdout, and diagnostics to stderr. [ADR 0017](adr/0017-generate-assets-outside-the-game-through-bounded-validated-publication.md) owns isolation, side effects, publication, and approval; the [tool contract](wiki/asset-pipeline-tool.md) owns detailed commands and schemas.

```bash
dotnet run --project tools/AlterCourse.AssetCtl -- validate-config --offline --output json
dotnet run --project tools/AlterCourse.AssetCtl -- doctor --output json
dotnet run --project tools/AlterCourse.AssetCtl -- status --output json
```

Tracked `config/assets/` is authoritative for provider instances, endpoints, credential environment-variable names, models, capabilities, economics, routes, quality, and styles. Never place credential values in YAML. Committed policy denies paid generation; enabling it requires an authorized untracked `.assetctl/config.local.yaml` override with bounded spend. Provider calls are never part of canonical verification.

Search before generating a duplicate. `--offline` selects an endpoint-free local target; `--dry-run` reports the plan without provider calls or tracked writes.

```bash
dotnet run --project tools/AlterCourse.AssetCtl -- find --query engineering --output json
dotnet run --project tools/AlterCourse.AssetCtl -- generate \
  --asset-id tooling.assetctl.fixture.generated-marker-svg \
  --offline \
  --output json
```

Selected bytes and manifest are one recoverable publication unit. Publication admits the destination parents, file identities, bounded snapshots, and mutable lifecycle/semantic ownership before replacement or recovery. Selected-file reads reject oversize input before allocation and reject shortened or growing snapshots; verification and approval validate the same admitted bytes. Lifecycle replacement retains admitted Linux parents and rechecks manifest revision, selected evidence, and stage ownership before commitment.

Secure descriptor-bound reads, lifecycle mutation, and publication currently require Linux with `openat`/`statx` and mounted procfs. Cooperating writers hold the asset lock. These checks do not authenticate arbitrary same-UID rewrites, provide an atomic revision comparison against such writers, replace two files simultaneously, or guarantee universal power-loss durability.

Approved assets are immutable: replacement uses a new semantic ID and `supersedes`. Approval or deprecation of an approved asset requires explicit current owner instruction. Approval requires exact-ID confirmation, actor, note, unchanged hash, passing validation, and complete non-placeholder rights data. Automated review cannot supply owner authorization or legal clearance.

### Reporting and recovery

Optional diagnostic construction, emission, and disposal failures preserve the command outcome; fallback stderr is best effort. Default diagnostics use stable context and failure categories instead of arbitrary exception prose or exception objects. Required stdout has a separate contract: a write or flush failure returns exit **9**. After a proven mutation, stderr identifies the commit and `reporting-degraded` when available. A receipt-write failure after generation publication can also return exit 9 while retaining the proven commit. Neither result establishes rollback.

Before retrying a mutation after degraded reporting, inspect `status`, the selected manifest and asset, and any receipt under the configured receipt root (default `.assetctl/runs`). A receipt's `rollback: not-established` means rollback was not proven. Do not infer unchanged state from a nonzero exit or absent output, or automatically rerun generation or approval.

Publication recovery uses version-1 authority evidence to corroborate owned transaction/file identities and mutable lifecycle/semantic ownership. Legacy version-0 journals in ignored local state lack this evidence and are refused and quarantined. This local recovery-state version does not change tracked manifest or receipt formats. Preserve and manually inspect quarantined journals and predecessor/stage/backup artifacts; quarantine does not authorize deletion of unproven artifacts. Avoid blind cleanup of `.assetctl/` or publication backups to unblock a retry. The [targeted correction record](reviews/assetctl-boundaries-2026-09-30.md) links the implementation and regression evidence.

## Deep validation

Mutation testing is intentionally outside the fast gate. Run it when simulation behavior or tests change materially:

```bash
./scripts/test-mutation.sh
```

Stryker is pinned but has no mutation-score threshold until an evidence-based baseline exists.

## Testing framework availability

xUnit supports ordinary .NET tests; vendored GdUnit4 runs current Godot integration. The [architecture-testing admission](dependency-admission/architecture-testing.md) adds CsCheck for bounded scheduler/power/shield invariants and ArchUnitNET for durable namespace rules beyond the project graph. Specialized behavioral and IL probes remain alongside them. Both packages stay Core-test-only.

GdUnit4Net remains conditional on a C# subject that genuinely requires engine runtime; current GDScript fixtures remain permitted by ADR 0009. An ADR's preferred future package is not an installed dependency.

## Managed Markdown policy

Repository verification runs Prettier and markdownlint over tracked Markdown/structured text. Complementary managed Project Standards workflows own their formatting, structure, and frontmatter policy; they are intentionally not reproduced by `verify.sh`. Keep overlapping formatter/linter versions aligned when the managed package changes.

Follow [ADR maintenance guidance](adr/README.md#adding-or-changing-a-record) for stable IDs, record scope, and amendments. Keep source inspection, actual execution, and historical verification distinct. A link check or frontmatter date change does not establish semantic agreement or implementation conformance.

## Enforcement philosophy

- Canonical CI uses the same `verify.sh` as local work; managed Project Standards checks remain complementary under ADR 0002.
- Warnings are failures. Fix causes rather than suppressing diagnostics or weakening central settings.
- Core remains independently buildable/testable without Godot. Engine types stay in the adapter project.
- Behavior changes and regressions require tests at the lowest layer that can prove them; new ADR documentation does not certify unexecuted paths.

[ADR 0013](adr/0013-use-dev-for-development-and-main-for-releases.md) requires both `Canonical verification` and `Branch policy` for PR admission to `dev` and `main`, together with its other protection requirements and explicitly bounded owner exception on `dev`. ADR 0013 and the installed workflow govern branch admission, readiness, and merge; a missing execution capability is a disclosed blocker, not an alternative verification path.
