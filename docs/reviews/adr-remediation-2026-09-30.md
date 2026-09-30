---
schema_version: '1.1'
id: 'reference-avt52d-adr-remediation-2026-09-30'
title: 'ADR Remediation Follow-up: September 30, 2026'
description: 'Draft evidence register for the bounded content-admission and defensive-heading follow-up under Issue 135.'
doc_type: 'reference'
status: 'draft'
created: '2026-09-30'
updated: '2026-09-30'
tags:
  - 'architecture'
  - 'validation'
aliases: []
related:
  - 'docs/reviews/adr-conformance-2026-09-27.md'
  - 'docs/reviews/adr-expansion-documentation-review-2026-09-27.md'
  - 'docs/wiki/sources.md'
---

# ADR remediation follow-up: September 30, 2026

## Admission scope and authority

This draft records the focused R1–R3 follow-up for [Issue #135](https://github.com/L3DigitalNet/star-trek-alter-course/issues/135). It gives the owner and reviewers an admission baseline before production corrections. Findings remain **unverified** until exact-revision regressions establish their behavior; this draft claims no reproduction, correction, or final closure.

The [September 27 register](adr-conformance-2026-09-27.md) retains its fourteen-ADR scope, original findings, dispositions, and execution receipts. This report adds evidence without rewriting that history. [Active ADRs](../adr/README.md) own architecture; the [wiki](../wiki/README.md) owns gameplay; source and executed tests establish actual behavior. This is neither a gameplay specification nor an implementation plan.

The initial audited `dev` revision is `efd4ac93894fc28ea0c3ea40ca1be008dd4d6631`, with fourteen ADRs. The documentation candidate is `9e31f9e9ac8795ac7e145c774f1a6caee61ff3d9`, with eighteen ADRs and unchanged production source/tests. [Issue #136](https://github.com/L3DigitalNet/star-trek-alter-course/issues/136) and replacement [PR #137](https://github.com/L3DigitalNet/star-trek-alter-course/pull/137) own adoption of [PR #134](https://github.com/L3DigitalNet/star-trek-alter-course/pull/134)'s documentation. The exact merged remediation base is **pending insertion after adoption is verified**. Documentation adoption alone does not certify ADR 0015–0018 or remediate R1–R3.

## Findings and obligation mapping

The stable `ADR-NNNN-Rnn` IDs below refer to the [historical obligation register](adr-conformance-2026-09-27.md#obligation-register). Source inspection establishes the entry points and arithmetic shown here; their consequences still require measured regression evidence.

| Finding | Source hypothesis and consequence to test | Applicable historical obligations | Admission disposition |
| --- | --- | --- | --- |
| R1 | [GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs) calls `ReadRequiredText` for definitions and schemas; its `GetAsText` materializes the complete resource before downstream content admission. Test whether oversized input bypasses the intended pre-allocation bound at this adapter boundary. | ADR-0001-R01/R04/R05; ADR-0005-R01/R02/R03/R06/R07/R11/R15. | **Unverified.** Reproduction, controlled rejection, resource-read bounds, and Core/Godot boundary evidence pending. |
| R2 | `FromText` in [ship](../../src/AlterCourse.Core/Content/ShipDefinitionContent.cs), [system](../../src/AlterCourse.Core/Content/SystemDefinitionContent.cs), and [faction](../../src/AlterCourse.Core/Content/FactionDefinitionContent.cs) content uses `Encoding.UTF8` for byte count and encoding. Test whether isolated UTF-16 surrogates are replaced before strict JSON admission can reject them. | ADR-0005-R01/R02/R03/R06/R07/R11/R15. | **Unverified.** Raw .NET text regressions, valid-Unicode controls, typed diagnostics, and correction evidence pending. |
| R3 | [DefensiveCombatDecisionPolicy](../../src/AlterCourse.Core/AI/DefensiveCombatDecisionPolicy.cs) unconditionally applies `Math.ScaleB(..., -1)` to both endpoints before subtraction. Test whether a nonzero `double.Epsilon` displacement disappears and changes the withdrawal heading. | ADR-0010-R02/R03/R05/R06/R07; ADR-0004-R01/R05; ADR-0011-R01/R02/R12/R15. | **Unverified.** Tiny/ordinary/extreme geometry, typed proposal/explanation, validated application, and deterministic continuation evidence pending. |

Historical F09 concerns parser options and malformed Unicode reaching strict content admission. Its JSON-path evidence is not proof that isolated UTF-16 survives or fails correctly at the earlier `FromText` encoding boundary. Historical F08 corrected overflow in the cautious contact policy; it does not prove the defensive policy preserves tiny nonzero displacement. Neither historical finding is reopened or declared invalid by this narrower follow-up.

## Compatibility decision and preservation boundaries

On September 30 the owner selected **v1.0.0** as the start of cross-version save compatibility. Pre-1.0 development/testing saves may break. The separate compatibility documentation leg owns the corresponding ADR 0006 and related prose amendments; this report does not itself amend those contracts.

For R3, retain the current rules identifier and numeric wire representation, preserve historical DTOs and fixtures, and require deterministic save/load continuation within the same build. A compatibility-policy amendment does not replace that behavioral proof.

The bounded UnitsNet exception remains visible in the historical register. Randomness, narrative, refit, recovery, and aggregation retain their deferred triggers or unresolved decisions. Current ship content V6, system-definition content V1, and save V10 remain implemented and unreleased. This follow-up makes no release, broader gameplay, or full-ADR certification claim.

## Inherited baseline and preliminary observations

The orchestrator supplied retained receipts for `rexec -- ./scripts/verify.sh` at the initial audited revision and the documentation candidate. Both exited **0**, with **1,199 Core**, **324 AssetCtl**, and Godot **one integration, two asset-import, and 92 gameplay** cases, plus smoke. The local evidence packet contains `baseline-verify.log`/`.exit` and `adr134-verify.log`/`.exit`; counts and exit files were inspected for this draft. These are inherited baseline executions, not new R1–R3 tests or final-candidate verification. The orchestrator also reported five passing hosted PR #137 checks; a durable final receipt remains pending.

Read-only governance observations supplied in `governance-summary.md` span September 30, 19:11:59–19:20 UTC. They record classic `dev`/`main` protection, required Canonical verification and Branch policy checks, conversation resolution, immutable published releases, and the active `v*` tag ruleset. This is preliminary point-in-time configuration evidence, not a runtime enforcement exercise or comprehensive ADR 0013 certification. No governance mutation is part of R1–R3.

The [source-catalog review record](../wiki/sources.md#review-record) retains the full semantic sweep due **2026-10-03**. This targeted follow-up does not reset it. Final reconciliation must name the affected content/persistence and AI/spatial pages and compare their claims with the actual corrected revision.

## Evidence required before final disposition

| Pending evidence | Required record |
| --- | --- |
| Adopted base | Exact merged SHA and its relationship to the unchanged-source documentation candidate. |
| R1–R3 reproduction | Exact test names, revisions, commands, exits, and observed failures; distinguish execution from source inspection. |
| Corrections and focused proof | Production/test commits, final focused results, relevant valid-input/extreme controls, and refusal preservation. |
| Compatibility reconciliation | Landed policy-document revision and same-build save/load continuation proof; retain identifier/wire/DTO limits. |
| Integrated verification and review | Exact candidate SHA, canonical and documentation gate receipts, independent review findings/dispositions, and hosted checks. |
| Final status | Per-finding outcome with named wiki/ADR coverage, unavailable evidence, and remaining limitations. |

Until these fields are populated from measured evidence, R1–R3 remain unverified and this document remains a draft admission artifact.
