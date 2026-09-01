# Summary, Receipt, and Envelope Formats

Three fixed shapes, so reports are comparable across sessions, agents, and repositories: the **operator summary** for a requested view of open work, the **receipt** for one issue or PR, and the **JSON envelope** every subcommand emits under `--output json`. `gh-workflow` renders all three from live state; relay that output verbatim rather than reformatting it. When rendering by hand, follow the layouts exactly.

Braced tokens such as `{number}` are substitution points, not literal text. Show an empty optional cell as `—`; never invent a value to fill one.

## Findings

Summary, receipt, and check all project the same typed findings; no renderer decides policy of its own. A finding names its `code`, `phase` (`structural`, `ready`, `merge`, or `post-merge`), `category`, `effect` (`blocks-ready`, `blocks-merge`, `requires-synchronization`, `requires-disposition`, `evidence-integrity`, or `advisory`), the kind and number of the work item, a message, and a remediation. Codes are stable: `GHW-{ISSUE|PR}-{PHASE}-{INVARIANT}`, uppercase and hyphenated, never reused for a different invariant.

Six categories exist, and every attention list presents them in this order:

1. **Blocked** — work that cannot proceed until a named dependency or decision resolves.
2. **Needs definition** — scope, acceptance criteria, or a governing decision is missing.
3. **PR admission blocked** — a structural, ready, or merge predicate the PR does not satisfy.
4. **Synchronization required** — Issue lifecycle and PR state disagree, or a terminal pairing is incomplete.
5. **Disposition required** — a closed-unmerged Final, or discovered work with no recorded outcome.
6. **Target date passed** — a dated commitment that is now in the past.

Findings are filtered by observed state rather than by a stored phase, so a report only ever asks for what is actionable now:

- a **draft** PR contributes Structural findings only — ordinary incompleteness is not a finding while the work is still being built;
- an **open, ready** PR contributes its current Structural, Ready, and Merge findings;
- a **terminal** PR contributes Post-merge and disposition findings;
- Issue findings that do not depend on a PR stay visible regardless of any PR's state.

## Operator summary

Attention first. The summary exists to drive operator decisions, so what needs a human comes before the inventory of everything else.

```markdown
# {target} — work state

Read {timestamp} · {open_issue_count} open issues · {open_pr_count} open PRs

## Needs attention

- **Blocked** — {kind} {number} {title}: {message}
- **Needs definition** — {kind} {number} {title}: {message}
- **PR admission blocked** — PR {number} {title}: {message}
- **Synchronization required** — {kind} {number} {title}: {message}
- **Disposition required** — {kind} {number} {title}: {message}
- **Target date passed** — {kind} {number} {title}: {target_date}

## Issues

| Issue | Type | Title | Workflow | Priority | Size / Severity | Execution mode |
| --- | --- | --- | --- | --- | --- | --- |
| {number} | {type} | {title} | {workflow} | {priority} | {size_or_severity} | {execution_mode} |

## Pull requests

| PR       | Title   | Governing work   | State   | CI   | Findings   |
| -------- | ------- | ---------------- | ------- | ---- | ---------- |
| {number} | {title} | {governing_work} | {state} | {ci} | {findings} |
```

Section rules:

- **Scope header.** `{target}` is the repository or the scope actually queried; `{timestamp}` is when live state was read, not when the summary was written. Counts describe what the tables below contain.
- **Needs attention.** The six categories above, in that order, one line per work item per category. Categories with no members are omitted; when all six are empty, keep the section and say so in one line rather than dropping it.
- **Issues.** `Size / Severity` carries `Severity` for Bugs and `Size` for every other Type — one column, because Severity is the value that column asks for on a Bug. A Bug pins both fields (see the pinning matrix in [field-vocabulary.md](field-vocabulary.md)); the column reports `Severity` and its `Size` simply is not surfaced here.
- **Pull requests.** `Governing work` is the declared `Final: #N`, `Supporting: #N`, or `Standalone`, or `—` when the PR declares nothing; an undeclared relationship is also a PR admission finding.

A summary is a read. It never mutates anything.

## Receipt

A receipt is a projection of observed state, not a creation ceremony. `ready` and `merge` each emit exactly one for the work they touched; otherwise ask for a receipt whenever the current picture is worth having. Raw PR creation needs none.

```text
{kind} #{number} — {title}
{link}

Type: {type} | Workflow: {workflow} | Priority: {priority}
Size / Severity: {size_or_severity} | Change risk: {change_risk}
Execution mode: {execution_mode} | Target date: {target_date}

Findings: {findings}
```

For a PR the field block instead carries what a PR actually has:

```text
PR #{number} — {title}
{link}

Governing work: {governing_work} | State: {state} | CI: {ci_status}

Findings: {findings}
```

Receipt rules:

- **Header.** Kind (`issue` or `PR`), number, title, and the link on its own line.
- **Fields.** Report the values actually set, not the values intended. An unset field appears as `—` rather than being dropped, so the operator sees the hole.
- **Findings.** One line per finding, in category order, each naming its remediation; `Findings: none` when the projection is clear. Never omit the line — a silent receipt is indistinguishable from an unchecked one.

## JSON envelope

`--output json` emits one envelope for every subcommand, so a caller parses one shape:

```json
{
	"schema_version": "1",
	"command": "ready",
	"result": "domain-finding",
	"target": { "kind": "pull_request", "number": 42, "repository": "owner/name" },
	"gate": "ready",
	"findings": [
		{
			"code": "GHW-PR-READY-ACCEPTANCE-COVERAGE-MISSING",
			"phase": "ready",
			"category": "PR admission blocked",
			"effect": "blocks-ready",
			"kind": "pull_request",
			"number": 42,
			"message": "The PR has no `## Acceptance coverage` section.",
			"remediation": "Add the section and rerun `ready --pr 42`."
		}
	],
	"steps": [{ "name": "structural-check", "status": "completed" }]
}
```

`result` is one of `clear`, `domain-finding`, `usage`, or `operational-failure`. `gate` is the phase a gate ran against, and null for commands that are not gates. Each mutation step reports `completed`, `skipped`, `pending`, or `failed`, so an interrupted paired command shows exactly how far it got. Human output compresses one line per work item per category; the JSON retains every finding, and `summary` may add an `items` projection without dropping any.
