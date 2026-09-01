# Contributing to Star Trek: Alter Course

Thanks for considering a contribution. The project is in early development, so an issue is the best place to align on significant work before investing in an implementation.

## Before you start

Search the open issues and pull requests for related work. Create or join a typed issue for a feature, bug, or substantial maintenance change, and make its intended outcome and acceptance criteria concrete.

Small, low-risk maintenance may use a Standalone pull request. Trivial prose-only corrections may qualify for the repository's T0 path. Agent Handoff admission is reserved for project-maintainer operational state and is not a general contribution route.

Do not submit copyrighted Star Trek artwork, audio, dialogue, scripts, data, or other third-party material unless you have the right to contribute it and preserve every required notice. Read [`LICENSE.md`](LICENSE.md) and [`LEGAL.md`](LEGAL.md) before adding content derived from an external source.

## Branch and commit workflow

Start from current `dev`. Use the governing issue number and a lowercase hyphenated slug:

- `feature/<issue>-<slug>`
- `fix/<issue>-<slug>`
- `task/<issue>-<slug>`
- `docs/<issue>-<slug>`

Maintainers reserve `hotfix/<issue>-<slug>` for urgent work based on `main`. Do not open ordinary development pull requests against `main`.

Configure the tracked hooks once per checkout:

```bash
./scripts/setup-git-hooks.sh
```

Commit subjects use Conventional Commit form, such as `feat: add sector navigation`, `fix(sensors): retain contact confidence`, or `docs: clarify setup`.

## Architecture boundaries

Keep pure simulation and domain behavior in `AlterCourse.Core`, independent of Godot. Godot nodes, resources, scenes, and presentation belong in `AlterCourse.Godot`. Add behavior and regression tests at the lowest layer that can prove the change.

The architecture decisions in [`docs/adr/`](docs/adr/) are active project constraints. [ADR 0013](docs/adr/0013-use-dev-for-development-and-main-for-releases.md) defines branch, pull-request, hotfix, and release governance.

## Verify the change

Run formatting when appropriate, then the canonical gate:

```bash
./scripts/fix.sh
./scripts/verify.sh
```

The gate checks formatting, static analysis, repository policy, secret scanning, a warning-free Release build, Core tests, Godot integration, and headless startup. Fix failures at their cause; do not weaken central settings or add suppressions merely to pass.

## Open the pull request

Open the pull request as a draft against `dev`. Keep the repository template's exact headings and replace its comments with:

- a concise summary of what changed and why;
- exactly one governing declaration: `Final: #N`, `Supporting: #N`, or `Standalone` with its required risk line;
- acceptance coverage tied to the issue or Standalone outcome;
- commands and checks that actually ran, with their outcomes.

Maintainers use the repository's GitHub workflow to admit, ready, and merge changes. Topic pull requests normally squash into `dev`, and merged topic branches are deleted automatically.
