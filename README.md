# Star Trek: Alter Course

[![Verify](https://github.com/L3DigitalNet/star-trek-alter-course/actions/workflows/verify.yml/badge.svg?branch=dev)](https://github.com/L3DigitalNet/star-trek-alter-course/actions/workflows/verify.yml)

**Star Trek: Alter Course (ST:AC)** is an early-development, single-player Star Trek strategy and starship-command fan game inspired by EGA Trek, Super Star Trek, and Netrek. It is built with Godot and C# around a persistent, systems-driven world.

The [design wiki](docs/wiki/README.md) owns game behavior, approved decisions, and open questions. The [ADR catalog](docs/adr/README.md) owns architectural boundaries. This README is the onboarding guide, not a parallel game specification.

## Project status

The current source-only release is [v0.6.2](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.6.2). Its gameplay baseline remains [v0.6.0 — Faction Observation and Response](https://github.com/L3DigitalNet/star-trek-alter-course/releases/tag/v0.6.0), using V8 saves.

Current `dev` additionally contains unreleased M6A combat and the completed [installed-system substrate migration](docs/wiki/ship-system-substrate.md). Development uses ship content V6, system-definition content V1, and V10 saves under `installed-ship-system-substrate-v1`. M3, M5, and M6 remain partial. There is no published packaged game artifact.

See [implementation status](docs/wiki/implementation-status.md) for reviewed behavior and verification, [content and persistence](docs/wiki/content-assets-and-persistence.md) for historical/current compatibility, and the [roadmap](ROADMAP.md) for sequence. ADRs 0015–0018 document existing command, knowledge, asset, and session boundaries; their adoption does not add a gameplay feature or release.

## Technology

- Godot .NET/C# presentation and a pure `AlterCourse.Core` simulation assembly with one-way dependencies.
- .NET 8 targets for Core/Godot compatibility and an independent .NET 10 AssetCtl development tool.
- Serilog through Microsoft logging abstractions for bounded, nonauthoritative diagnostics.
- xUnit for ordinary .NET tests, vendored GdUnit4 for current engine integration, and focused Core-test-only architecture/property checks.
- One canonical `./scripts/verify.sh` path shared by contributors and CI.

Exact SDK, editor, language, tool, and package selections belong in the repository configuration and [development runbook](docs/development-quality.md), not a second version list here.

## Getting started

The supported development environment is Linux x86_64 with Git, Bash, Node 24 with `npx`, `curl`, `tar` with xz support, `unzip`, and `sha256sum`. The repository resolves its pinned .NET SDK, Godot editor, and native development tools; [`.node-version`](.node-version) owns the Node major.

```bash
git clone --branch dev https://github.com/L3DigitalNet/star-trek-alter-course.git
cd star-trek-alter-course
./scripts/setup-git-hooks.sh
./scripts/verify.sh
```

See [Development quality](docs/development-quality.md) for complete setup, verification, and deep validation. Run normal work from `dev`; `main` is release-only.

## Run the gameplay slice

After setup, launch from the repository root:

```bash
./scripts/launch-game.sh
```

The launcher resolves the pinned SDK, performs a locked restore, builds the Debug Godot assembly with warnings treated as errors, and launches only after those steps succeed. Additional arguments are passed to Godot.

Use [Interface and player commands](docs/wiki/interface-and-player-commands.md) for controls. [World, navigation, and time](docs/wiki/world-navigation-and-time.md), [Engineering and combat](docs/wiki/engineering-and-combat.md), [Sensors and knowledge](docs/wiki/sensors-knowledge-and-ai.md), and [Persistence](docs/wiki/content-assets-and-persistence.md) own their detailed rules.

## Asset pipeline

AssetCtl searches the tracked catalog, plans configuration-driven routes, creates deterministic local SVG/PNG placeholders, validates untrusted output, and publishes selected assets with manifests. Committed defaults disable external generation and spending, so offline validation and local fallback need neither provider credentials nor network access.

```bash
dotnet run --project tools/AlterCourse.AssetCtl -- find --query marker --output json
dotnet run --project tools/AlterCourse.AssetCtl -- plan --asset-id tooling.assetctl.fixture.generated-marker-svg --output json
dotnet run --project tools/AlterCourse.AssetCtl -- generate --asset-id tooling.assetctl.fixture.generated-marker-svg --offline --output json
```

Tracked configuration and manifests live under [`config/assets/`](config/assets/). Candidates, receipts, locks, logs, and local overrides remain under ignored `.assetctl/`. Approved assets are immutable under the current lifecycle; replacement uses a new semantic identity and supersession. Approval and deprecation of approved assets require explicit current owner instruction and the documented confirmations.

Provider configuration stores credential environment-variable names only. Its caller resolves any OpenBao-backed credential before starting AssetCtl and populates the named variable; tracked AssetCtl scripts do not resolve OpenBao themselves. Neither credential values nor `bao://` references belong in tracked configuration, fixtures, tests, manifests, receipts, logs, or command output.

[ADR 0017](docs/adr/0017-generate-assets-outside-the-game-through-bounded-validated-publication.md) owns tool isolation, bounded external operations, and recoverable publication. The [tool contract](docs/wiki/asset-pipeline-tool.md) and [development workflow](docs/development-quality.md#assetctl-development) own implementation detail and commands. No live provider test or legal clearance is implied by the examples.

## Contributing

Read [CONTRIBUTING](CONTRIBUTING.md) before proposing work. Significant changes use a governing issue, topic branch, and draft pull request. Follow [ADR 0013](docs/adr/0013-use-dev-for-development-and-main-for-releases.md) and the installed workflow for admission, readiness, and merge; architectural documentation is not trivial direct-admission prose.

## Licensing and legal status

Star Trek: Alter Course is an unofficial, non-commercial fan project and is not endorsed by, sponsored by, licensed by, or affiliated with Paramount, CBS Studios, or any official Star Trek licensee.

Original project software is available under the MIT License subject to the repository's explicit licensing boundaries. The MIT grant does **not** cover Star Trek intellectual property or other third-party material.

See [LICENSE](LICENSE.md) for licensing scope, [the MIT text](LICENSES/MIT.txt) for covered original software, and [LEGAL](LEGAL.md) for fan-project and third-party notices.
