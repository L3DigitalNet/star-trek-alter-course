# Project Status

## Current snapshot

- Feature #25 delivers the first executable gameplay walking skeleton: a pure deterministic Core simulation, a map-centric Godot command shell, and a V1 quick-save/load path.
- The playable development slice has three arbitrarily positioned strategic locations and connected routes; travel is persistent state until its scheduled arrival rather than an immediate relocation.
- The player ship starts with damaged sensors and an active repair. Repair progresses on the same simulation timeline during real-time play, accelerated play, and strategic travel.
- The Godot shell exposes Pause, 0.5x, 1x, 2x, and 4x presentation-rate controls. It converts elapsed presentation time into bounded, explicit 100 ms Core steps; rendering cadence is not simulation truth.
- Strategic and tactical views are distinct open-map presentations. Tactical state uses continuous kilometer coordinates and a Core-owned Y-positive-up reference frame; the Godot adapter performs screen projection.
- The first validated game-domain ship definition is `src/AlterCourse.Godot/content/ships/pathfinder.json`, with its V1 schema in the adjacent `content/schemas/` directory. It is intentionally separate from AssetCtl.
- V1 JSON quick saves use Godot's `user://quick-save-v1.json` boundary and restore active travel, repair, scheduler, and runtime identity state only after validation succeeds.
- The walking skeleton deliberately defers combat, shields, weapons, power distribution, crew, factions, diplomacy, economy, missions, AI, narrative, networking, and final art.
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
- `dev` is the protected default branch; `main` accepts one pre-release baseline, then release and hotfix promotions only.
- Branch policy and canonical verification protect both permanent branches; `dev` permits the documented owner bypass for handoff-only pushes.
- Releases use SemVer `v*` tags and immutable GitHub Releases; no playable release has been published yet.
- ADR 0013 and its design brief define the branch, PR, commit, hotfix, and release lifecycle.
- Public presentation includes a corrected description, eleven GitHub topics, contributor guidance, and community health at 87%.
- The asset-pipeline specification is aligned with the repository ADRs and was merged through governed PR #15.
- A legacy-named PR was auto-closed by branch enforcement; its content was recovered through Issue #14 and policy-compliant PR #15.
- Local and remote topic branches are pruned; only permanent `dev` and `main` branches remain, with `origin/HEAD` at `dev`.
- Project-local Godot/C# guidance is available as five byte-identical paired Claude/Codex skills with project-relative Codex registration and a canonical parity gate.
- The skill set pins upstream `7110607ab816ece9669274bc84937857a8819796`, retaining Apache-2.0 and NOTICE obligations with provenance/update guidance.
- Feature #16 fully implements AssetCtl and is merged into `dev` through governed PR #19.
- AssetCtl has strict offline configuration, safe atomic publication, lifecycle approvals, provenance, and Godot import support.
- AssetCtl credentials remain OpenBao-owned; tracked content contains only references and environment-variable names.
- Tooling now uses C# 12, exact SDK 10.0.111, Node 24, Release-mapped Godot builds, and idempotent owned-source formatting.
- CSharpier owns C# whitespace; analyzers enforce semantic policy, including `_camelCase` private instance fields.
- Issue #21 tooling convergence was delivered through merged Final PR #22.
