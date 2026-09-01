<!-- prettier-ignore-start -->

<!-- BEGIN project-standards:agent-handoff -->
<!-- markdownlint-disable MD025 -->
# Agent Handoff

Use the repo-local `agent-handoff` skill at session startup and closeout. Do not reread state already injected by SessionStart. Keep project knowledge inside this repository and store credential references only, never values.
<!-- markdownlint-enable MD025 -->
<!-- END project-standards:agent-handoff -->

<!-- prettier-ignore-end -->

## C# and Godot development

- Run `./scripts/fix.sh` when automated formatting is appropriate, then run `./scripts/verify.sh` before declaring implementation complete.
- Fix compiler, analyzer, formatting, and test failures at their cause. Never weaken central settings or add suppression merely to pass CI.
- Keep pure simulation/domain code in `AlterCourse.Core` independent from Godot. Add behavioral and regression tests at the lowest applicable layer.

<!-- prettier-ignore-start -->

<!-- BEGIN project-standards:github-workflow -->
<!-- markdownlint-disable MD025 -->
# GitHub Workflow

This repository's work belongs to the `L3DigitalNet` organization. Route every GitHub work-state action through the complete table below. Every row is a `gh-workflow` subcommand unless it says raw `gh`. Load the `github-workflow` skill for triage, an organization-schema audit, T0 or relationship judgment, or uncommon recovery.

| Action | Command |
| --- | --- |
| Create a typed issue | `new --type T --title S [--field Name=Value]` |
| Set fields or Issue Type | `set --issue N [--type T] [--field Name=Value]` |
| Close or reopen an issue | `close --issue N --as done\|dropped` / `reopen --issue N --workflow VALUE` |
| Check or read an issue or PR | `check --issue N` / `check --pr N [--through PHASE]` / `receipt --issue N` / `receipt --pr N` |
| Summary / schema audit | `summary` / `audit` |
| Open a draft PR | raw `gh pr create --draft --body-file PATH` |
| Ready, then merge | `ready --pr N` / `merge --pr N [--method M] [--auto]` |
| Close an open Final unmerged | `close --pr N --as OUTCOME --reason S` |
| Wait for CI | `gh pr checks N --watch --fail-fast` or `gh run watch ID --exit-status` |

All ten accept `--output human|json`. The binary is at `.agents/skills/github-workflow/bin/gh-workflow` (and its `.claude/` twin); refusals name the valid values, so invoke it rather than guessing. These rules bind even when the skill was never loaded:

- An operator instruction is sufficient authority for the action it names. You author acceptance criteria and admit work to `Ready` yourself; open state never implies `Ready`.
- A T0 commit — trivial prose repair, no protected surface, one `Workflow-Admission: T0` trailer — is the only autonomous direct push; all other work starts as a draft PR.
- Every PR declares `Final: #N`, `Supporting: #N`, or `Standalone` under `## Governing work`.
- Keep terminal state paired: `Done` closes as completed, `Dropped` as not planned, and reopen returns a nonterminal `Workflow` value.
- Never create shadow state labels, mutate organization schema through this package, or bypass live enforcement.
- A related finding you can address this session needs no issue: fix it in place when this repository owns it, file it against the owning upstream repository, and ask the operator only when it needs its own session.

<!-- markdownlint-enable MD025 -->
<!-- END project-standards:github-workflow -->

<!-- prettier-ignore-end -->

<!-- prettier-ignore-start -->

<!-- BEGIN project-standards:markdown-frontmatter -->
<!-- markdownlint-disable MD025 -->
# Markdown Frontmatter

Managed Markdown in this repository carries YAML frontmatter under the Markdown Frontmatter Standard: the eleven required fields in canonical order, every scalar quoted, and an id of the form `{doc_type}-{6-char base36 token}-{slug}`.

Create a new managed document with `scripts/new-doc-id --scaffold --doc-type <type> <name>` from the repo-local skill at `.agents/skills/markdown-frontmatter/`. Read that skill's `SKILL.md` before hand-authoring or repairing a frontmatter block.

The gate is `project-standards validate`.

`AGENTS.md`, `CLAUDE.md`, and anything under `.agents/**`, `.claude/**`, or `.codex/**` never carry frontmatter.
<!-- markdownlint-enable MD025 -->
<!-- END project-standards:markdown-frontmatter -->

<!-- prettier-ignore-end -->

<!-- prettier-ignore-start -->

<!-- BEGIN project-standards:markdown-tooling -->
<!-- markdownlint-disable MD025 -->
# Markdown and structured-text tooling

Prettier owns physical formatting and markdownlint owns Markdown structure. Do not add overlapping tools.

Enabled checks: format, lint.
Markdown scope: `**/*.md`.
Structured-config scope: `**/*.json`, `**/*.jsonc`, `**/*.yml`, `**/*.yaml`.
Lint additionally skips generated directories: `.pytest_cache/**`, `.ruff_cache/**`, `.venv/**`, `node_modules/**`.

Declared exclusions:
- `.agents/skills/agent-handoff/**` (both): Centrally locked Agent Handoff skill tree; editing it to satisfy a formatter creates drift.
- `.claude/skills/agent-handoff/**` (both): Centrally locked Agent Handoff skill tree for Claude Code; editing it to satisfy a formatter creates drift.

Check formatting over exactly that scope, with Git as the corpus authority:

```bash
git ls-files -z -- ':(glob)**/*.md' ':(glob)**/*.json' ':(glob)**/*.jsonc' ':(glob)**/*.yml' ':(glob)**/*.yaml' ':(glob,exclude).agents/skills/agent-handoff/**' ':(glob,exclude).claude/skills/agent-handoff/**' | xargs -0 -r npx prettier --check --
```

Without Git, bound the same scope by glob instead. Prettier's CLI has no negative pattern, so this form does not apply the declared format exclusions above; pass them through an `--ignore-path` file inside the repository:

```bash
npx prettier --check --no-error-on-unmatched-pattern -- '**/*.md' '**/*.json' '**/*.jsonc' '**/*.yml' '**/*.yaml'
```

Never check or write with a bare `.`: it reaches undeclared languages and Git-excluded scratch.

Lint Markdown structure over the same Git-tracked scope:

```bash
git ls-files -z -- ':(glob)**/*.md' ':(glob,exclude).pytest_cache/**' ':(glob,exclude).ruff_cache/**' ':(glob,exclude).venv/**' ':(glob,exclude)node_modules/**' ':(glob,exclude).agents/skills/agent-handoff/**' ':(glob,exclude).claude/skills/agent-handoff/**' | sed -z 's|^|:|' | xargs -0 -r npx markdownlint-cli2 --no-globs
```

Never lint a bare recursive glob: it descends into any independent Git repository checked out below this one.

Run the enabled checks before claiming completion.
<!-- markdownlint-enable MD025 -->
<!-- END project-standards:markdown-tooling -->

<!-- prettier-ignore-end -->
