# Star Trek: Alter Course

[![Verify](https://github.com/L3DigitalNet/star-trek-alter-course/actions/workflows/verify.yml/badge.svg?branch=dev)](https://github.com/L3DigitalNet/star-trek-alter-course/actions/workflows/verify.yml)

**Star Trek: Alter Course (ST:AC)** is an early-development, single-player Star Trek strategy and starship-command fan game inspired by EGA Trek, Super Star Trek, and Netrek. It is being built with Godot and C#.

The [project design wiki](docs/wiki/README.md) is the single source of truth for the game's design: systems, architecture, approved decisions, and open questions.

## Project status

The current source-only release is v0.5.0 — Strategic Contact Reporting. Current `dev` also contains the first bounded faction-assignment slice. M3 and M5 remain incomplete; no packaged game artifact is published. See [implementation status](docs/wiki/implementation-status.md) for the reviewed behavior and save-version boundaries, and the [roadmap](ROADMAP.md) for development sequence.

This README is an onboarding guide. The wiki owns game rules, controls, persistence contracts, and future design; [ADRs](docs/adr/) record architectural decisions.

## Technology

- Godot 4.7.2 .NET/C#
- Exact .NET SDK 10.0.111 with C# 12, targeting .NET 8 for Godot compatibility
- A standalone .NET 10 `assetctl` development tool for validated visual placeholders and asset provenance
- A pure `AlterCourse.Core` domain assembly with a one-way dependency from `AlterCourse.Godot`
- xUnit for Core tests and GdUnit4 for Godot integration tests
- One canonical `./scripts/verify.sh` quality gate shared by contributors and CI

## Getting started

The supported development environment is Linux x86_64 with Git, Bash, Node 24 with `npx`, `curl`, `tar` with xz support, `unzip`, and `sha256sum`. The repository resolves its exact .NET SDK, Godot editor, and native development tools; [`.node-version`](.node-version) owns the supported Node major.

```bash
git clone https://github.com/L3DigitalNet/star-trek-alter-course.git
cd star-trek-alter-course
./scripts/setup-git-hooks.sh
./scripts/verify.sh
```

See [Development quality](docs/development-quality.md) for the complete setup, verification, and deep-validation workflow.

## Run the gameplay slice

After setup, launch the gameplay slice from the repository root:

```bash
./scripts/launch-game.sh
```

The launcher resolves the pinned .NET SDK, performs a locked restore, builds the Debug Godot assembly with warnings treated as errors, then resolves Godot and launches only if every preceding step succeeds. Pass Godot arguments after the command when needed.

For play instructions, use [Interface and player commands](docs/wiki/interface-and-player-commands.md). [World, navigation, and time](docs/wiki/world-navigation-and-time.md) explains the proof world and orders; [Engineering](docs/wiki/engineering-and-combat.md), [Sensors and knowledge](docs/wiki/sensors-knowledge-and-ai.md), and [Persistence](docs/wiki/content-assets-and-persistence.md) own their rules. These contracts are maintained in the wiki rather than repeated here.

## Asset pipeline

`AlterCourse.AssetCtl` searches the tracked catalog, plans configuration-driven provider routes, creates deterministic local SVG or PNG placeholders, validates untrusted image bytes, and publishes selected assets with manifests. Committed defaults disable external generation and spend, so validation and local fallback need no provider credentials or network access.

```bash
dotnet run --project tools/AlterCourse.AssetCtl -- find --query marker --output json
dotnet run --project tools/AlterCourse.AssetCtl -- plan --asset-id tooling.assetctl.fixture.generated-marker-svg --output json
dotnet run --project tools/AlterCourse.AssetCtl -- generate --asset-id tooling.assetctl.fixture.generated-marker-svg --offline --output json
```

Configuration and catalog manifests live under [`config/assets/`](config/assets/). Runtime candidates, receipts, locks, logs, and local overrides stay under ignored `.assetctl/`. Approval and deprecation of approved assets require an explicit owner instruction and the high-friction confirmation flags documented by `assetctl --help`.

Provider configuration stores environment-variable names only. The tracked launch or application boundary resolves OpenBao-backed credentials into those named variables before AssetCtl starts; credential values and `bao://` references never belong in tracked configuration, fixtures, tests, manifests, receipts, logs, or command output.

## Contributing

Development happens on the permanent `dev` branch. Significant changes use a governing issue, a named topic branch, and a pull request; `main` is reserved for releases.

Read [`CONTRIBUTING.md`](CONTRIBUTING.md) before proposing work. The full branch and release decision is recorded in [ADR 0013](docs/adr/0013-use-dev-for-development-and-main-for-releases.md).

## Licensing and legal status

Star Trek: Alter Course is an unofficial, non-commercial fan project and is not endorsed by, sponsored by, licensed by, or affiliated with Paramount, CBS Studios, or any official Star Trek licensee.

Original project software is available under the MIT License subject to the repository's explicit licensing boundaries. The MIT grant does **not** cover Star Trek intellectual property or other third-party material.

See:

- [`LICENSE.md`](LICENSE.md): repository licensing policy and scope
- [`LICENSES/MIT.txt`](LICENSES/MIT.txt): MIT License text for covered original software
- [`LEGAL.md`](LEGAL.md): fan-project and third-party intellectual-property notice
