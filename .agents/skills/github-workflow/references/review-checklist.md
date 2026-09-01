# Review Checklist

Agent-generated code should not be trusted merely because another agent reports that it looks correct. Review earns confidence in layers, each judging something the previous layer cannot.

**This checklist gates nothing.** It automates nothing and blocks nothing: it is reviewer judgment, applied by whoever is reviewing. Merge gating, required checks, and protected-branch enforcement are deterministic repository policy owned by rulesets and CI, and no agent may weaken, bypass, or substitute for them by asserting that a review passed.

## Layers

```text
Implementation agent
        ↓
Deterministic validation
        ↓
Independent review agent where useful
        ↓
Human acceptance for consequential changes
```

Each layer has a distinct job:

- **Implementation agent** — self-review before handing off; the weakest layer, because it judges its own work.
- **Deterministic validation** — tests, linters, type checks, and repository gates; the only layer whose verdict does not depend on judgment.
- **Independent review agent where useful** — a second reading with no stake in the implementation, valuable exactly where the checklist below is hard.
- **Human acceptance for consequential changes** — required for high-risk work; a human accepts, agents do not accept on a human's behalf.

## Review depth by change risk

`Change risk` sets the baseline treatment; it measures how dangerous it is to implement the change incorrectly, not how bad the existing problem is.

| Risk | Baseline treatment |
| --- | --- |
| **`R1 Low`** | Normal tests and review |
| **`R2 Moderate`** | Acceptance-criteria trace plus focused regression coverage |
| **`R3 High`** | Independent review, negative testing, explicit rollback consideration |
| **`R4 Critical`** | Human-approved plan before implementation, independent verification, explicit recovery/rollback procedure |

R3 and R4 are mechanically visible rather than merely advisory. A Standalone PR declares `Change risk:` with one of those exact four values in its body (see [pr-standard.md](pr-standard.md)), and an R4 declaration is only complete when the Summary or Acceptance coverage carries evidence of all four controls: a plan agreed before implementation, a recovery or rollback procedure, negative testing, and independent verification. Those are the whole requirement — R4 adds no ceremonial issue and no second approval artifact, and a declared risk value never substitutes for the repository's own required checks.

For higher-risk work — R3 and R4 — review explicitly examines:

1. acceptance-criteria coverage
2. repository conventions and governing ADRs
3. unintended scope expansion
4. test adequacy
5. negative-path behavior
6. rollback or recovery implications
7. security and trust boundaries
8. CI or policy changes
9. duplicate abstractions
10. evidence integrity

Items 8 and 10 deserve particular suspicion when the change was agent-authored: a change that edits the checks, relaxes a gate, or supplies its own verification evidence is judging its own work.

## Self-judging boundary

An implementation agent may not weaken the mechanisms judging its own work without heightened review. Loosening a lint rule, deleting or skipping a test, widening an exclusion, relaxing a required check, or editing CI in the same change that needs to pass it are all escalation triggers, not conveniences — raise them for human decision instead of resolving them unilaterally.
