# Project Status

## Current snapshot

- Fresh Catalog 5 adoption is configured for Project Standards v5.27.0.
- Enabled packages: markdown-frontmatter 1.15, adr 1.6, markdown-tooling 1.15, agent-handoff 1.16, and github-workflow 1.8.
- No legacy Agent Handoff implementation was found, so no migration was required.
- Codex's user-scoped MCP registration was preserved; Claude Code's project-scoped registration was added and verified.
- Repository-owned rexec configuration enables sanitized Git context and requires `git`, `npx`, and `uv` on the worker.
- The solution separates pure `AlterCourse.Core` simulation code from the Godot-facing project through a one-way project reference.
- Strict C# compilation, curated analyzers, deterministic formatting, tests, security lint, and Godot checks share one `scripts/verify.sh` gate.
- Godot 4.7.2 .NET, .NET SDK 10.0.111, the .NET 8.0.30 runtime, GdUnit4 6.2.0, and development tools are pinned.
- Reconciliation, Markdown gates, the canonical quality gate, hook checks, MCP checks, and organization workflow checks are green.
- An adoption regression report was added to [project-standards issue #130](https://github.com/L3DigitalNet/project-standards/issues/130#issuecomment-5494024929).
