# Issue Field Vocabulary

`Workflow` semantics and the field-pinning matrix: the two parts of the organization's Issue Field vocabulary the tool cannot tell you at the moment you need them. Every other value set reaches you through `gh-workflow` itself — an invalid value, field name, or Issue Type is refused with the valid set named, so invoke the tool rather than looking a vocabulary up first. `org-schema.yaml` is the machine-readable baseline it validates against. `Workflow` answers a different question from GitHub's native open/closed state: native state answers whether the issue is active, `Workflow` answers where that active work sits in its lifecycle. `Priority`, `Severity`, `Change risk`, and `Size` likewise answer four different questions; never derive one from another.

## Workflow

| Value | Meaning |
| --- | --- |
| **Inbox** | Captured but not fully triaged |
| **Needs definition** | Scope, acceptance criteria, governing decision, or other required information is insufficient |
| **Ready** | Authorized, sufficiently specified, unblocked, and eligible for work |
| **In progress** | Active implementation or investigation is occurring |
| **Blocked** | Work cannot continue until a defined dependency or decision is resolved |
| **In review** | Deliverable exists and awaits acceptance or verification |
| **Done** | Acceptance criteria have been satisfied |
| **Dropped** | Intentionally abandoned, rejected, obsolete, duplicate, or superseded |

## Field pinning

| Field          |   Bug    | Feature | Task | Initiative | Research |
| -------------- | :------: | :-----: | :--: | :--------: | :------: |
| Workflow       |    ✓     |    ✓    |  ✓   |     ✓      |    ✓     |
| Priority       |    ✓     |    ✓    |  ✓   |     ✓      |    ✓     |
| Size           |    ✓     |    ✓    |  ✓   |            |    ✓     |
| Change risk    |    ✓     |    ✓    |  ✓   |            |          |
| Execution mode |    ✓     |    ✓    |  ✓   |            |    ✓     |
| Target date    | Optional |    ✓    |  ✓   |     ✓      | Optional |
| Severity       |    ✓     |         |      |            |          |

Pinning binds before the issue exists, which is why the matrix stays in this document: `gh-workflow check` reports missing pinned fields only once there is an issue to check. `check` and `receipt` project the same machine-readable pinning authority the tool carries, so a reported gap and this table cannot disagree; `Change risk` here is the Issue's field, while a Standalone PR declares its own in its body (see [pr-standard.md](pr-standard.md)). `Target date` is the one pin `check` does not require for `Ready`: set it only when a date carries semantic meaning, and leave it empty otherwise. Initiatives deliberately omit execution-oriented fields because an Initiative itself should normally not be directly implemented. Organization schema changes — adding, renaming, or retiring a field or a value — are human work. Agents audit and report drift with `gh-workflow audit`; they never mutate the organization schema.
