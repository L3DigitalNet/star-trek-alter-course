# Star Trek: Alter Course

[![Verify](https://github.com/L3DigitalNet/star-trek-alter-course/actions/workflows/verify.yml/badge.svg?branch=dev)](https://github.com/L3DigitalNet/star-trek-alter-course/actions/workflows/verify.yml)

**Star Trek: Alter Course (ST:AC)** is an early-development, single-player Star Trek strategy and starship-command fan game inspired by EGA Trek, Super Star Trek, and Netrek. It is being built with Godot and C#.

## Project status

The repository provides a first executable gameplay walking skeleton alongside the project architecture, pinned development environment, and quality gates. Version 0.1.0 is the first source release; no packaged game artifact is published.

The command screen proves a small, persistent, deterministic slice of play: a captain selects a connected destination on an open strategic map, begins travel, and sees the ship's damaged sensors repair as simulation time passes. Arrival is scheduled rather than immediate. A separate local tactical view displays continuous position and accepts a demonstration course command; neither map is governed by square or hex movement.

The design centers on a persistent simulation in which the player commands one starship inside a changing political and strategic world. Planned areas include:

- map-focused strategic and tactical play;
- detailed ship systems, damage, repairs, resources, officers, and crew;
- diplomacy, conflict, trade, treaties, missions, and faction autonomy;
- progression through lasting consequences rather than captain levels;
- a deterministic simulation core that can be tested without Godot.

These are design goals, not claims about currently playable features. The architecture decisions under [`docs/adr/`](docs/adr/) record the approved technical direction. The near- and mid-term sequence of major development slices is tracked in [`ROADMAP.md`](ROADMAP.md).

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

After setup, launch the Godot project from the repository root:

```bash
godot_bin="$(./scripts/resolve-godot.sh)"
"${godot_bin}" --path src/AlterCourse.Godot
```

The map-first command screen starts at the `Dawn Anchor` strategic location. Select a connected destination from the map or the status-panel buttons, then choose **ENGAGE TRAVEL**. Travel remains active until its scheduled arrival, while the visible sensor repair progresses on the same simulation timeline.

Use the **Pause**, **0.5x**, **1x**, **2x**, and **4x** controls to choose how quickly presentation elapsed time requests deterministic 100 ms Core steps. Pause leaves rendering active but submits no Core advancement. **ADVANCE UNTIL NEXT EVENT** follows the same scheduler path and stops at the earliest current repair or travel event.

Switch to **TACTICAL** to view the local continuous reference frame. **SET COURSE 045° / 2 km/s** submits the first tactical movement intent. Core tactical coordinates use kilometers with positive Y toward tactical north; the Godot map adapter performs the presentation Y-axis conversion.

**SAVE** and **LOAD** use one V1 quick-save slot at `user://quick-save-v1.json`. A save includes active travel, sensor repair, scheduled work, and deterministic runtime state. Loading validates a new candidate simulation before replacing the running one, so a failed load leaves the active game unchanged.

The runtime reads the first validated, data-driven ship definition from [`src/AlterCourse.Godot/content/ships/pathfinder.json`](src/AlterCourse.Godot/content/ships/pathfinder.json), using its adjacent V1 JSON schema. This is game-domain content, separate from the AssetCtl visual-asset catalog.

## Architecture at a glance

`AlterCourse.Core` owns explicit simulation time, scheduled work, travel, tactical state, sensor repair, authored ship definitions, and V1 save/load mapping. It exposes read-only player projections and typed operations. `AlterCourse.Godot` owns scenes, input, rendering, presentation-time accumulation, and the coordinate projection into Godot screen space; it does not become simulation authority.

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
