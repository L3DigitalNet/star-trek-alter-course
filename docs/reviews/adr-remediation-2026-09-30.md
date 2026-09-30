---
schema_version: '1.1'
id: 'reference-avt52d-adr-remediation-2026-09-30'
title: 'ADR Remediation Follow-up: September 30, 2026'
description: 'Reproduction evidence, focused geometry correction, and pending integrated verification for Issue 135.'
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

This draft records the focused R1–R3 follow-up for [Issue #135](https://github.com/L3DigitalNet/star-trek-alter-course/issues/135) and [Final draft PR #138](https://github.com/L3DigitalNet/star-trek-alter-course/pull/138). All three defects have execution evidence. R3 has a corrected candidate with passing focused geometry, command, and same-build save/load proof; R1/R2 correction evidence and final integrated verification remain pending. This is an interim review record, not final closure or merge authorization.

The [September 27 register](adr-conformance-2026-09-27.md) retains its fourteen-ADR scope, original findings, dispositions, and execution receipts. This report adds evidence without rewriting that history. [Active ADRs](../adr/README.md) own architecture; the [wiki](../wiki/README.md) owns gameplay; source and executed tests establish actual behavior. This is neither a gameplay specification nor an implementation plan.

The initial audited `dev` revision is `efd4ac93894fc28ea0c3ea40ca1be008dd4d6631`, with fourteen ADRs. [Issue #136](https://github.com/L3DigitalNet/star-trek-alter-course/issues/136) and replacement [PR #137](https://github.com/L3DigitalNet/star-trek-alter-course/pull/137) adopted [PR #134](https://github.com/L3DigitalNet/star-trek-alter-course/pull/134)'s documentation as `7e2df746688f63df9936988d20978710e0aaf2d4`. The merged tree equals final PR #137 head `d20021d2c8147f018060a2ea83bf79a01060b0d1`; the original PR #134 was closed with the replacement reference. All eighteen active ADRs apply according to their scopes. Production source, tests, and scripts remain identical to the initial audited revision at that adopted base. Documentation adoption does not certify ADR 0015–0018 or remediate R1–R3. This documentation continuation starts at `27a393d36b02da8dfcaa46ab07d0fdebc9ce5639`, which includes the initial report and compatibility amendment.

## Findings and obligation mapping

The stable `ADR-NNNN-Rnn` IDs below refer to the [historical obligation register](adr-conformance-2026-09-27.md#obligation-register). The following outcomes join exact-revision execution to the inspected source paths; they do not rewrite the historical obligation table.

| Finding | Source hypothesis and consequence to test | Applicable historical obligations | Admission disposition |
| --- | --- | --- | --- |
| R1 | [GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs) materializes definitions and schemas with `GetAsText` before downstream admission. The real adapter probe observed one text read for each oversized category instead of zero; oversized schemas still allowed playable bootstrap. | ADR-0001-R01/R04/R05; ADR-0005-R01/R02/R03/R06/R07/R11/R15. | **Confirmed defect.** Godot reproduction below; bounded resource-read correction and final proof pending. |
| R2 | `FromText` in [ship](../../src/AlterCourse.Core/Content/ShipDefinitionContent.cs), [system](../../src/AlterCourse.Core/Content/SystemDefinitionContent.cs), and [faction](../../src/AlterCourse.Core/Content/FactionDefinitionContent.cs) content uses replacement UTF-8 encoding. Actual isolated UTF-16 surrogates failed typed-rejection assertions; otherwise-valid labels were admitted after replacement. | ADR-0005-R01/R02/R03/R06/R07/R11/R15. | **Confirmed defect.** Core reproduction below; strict factory encoding and final proof pending. |
| R3 | [DefensiveCombatDecisionPolicy](../../src/AlterCourse.Core/AI/DefensiveCombatDecisionPolicy.cs)'s unconditional endpoint halving erased represented tiny displacement and changed direction. | ADR-0010-R02/R03/R05/R06/R07; ADR-0004-R01/R05; ADR-0011-R01/R02/R12/R15. | **Corrected with focused evidence** at `7bd42bd7d3154d01028e3228be04a537f732562d`: 147 passing geometry/continuation cases. Integrated candidate verification and independent review pending. |

Historical F09 concerns parser options and malformed Unicode reaching strict content admission. Its JSON-path evidence is not proof that isolated UTF-16 survives or fails correctly at the earlier `FromText` encoding boundary. Historical F08 corrected overflow in the cautious contact policy; it does not prove the defensive policy preserves tiny nonzero displacement. Neither historical finding is reopened or declared invalid by this narrower follow-up.

## Compatibility decision and preservation boundaries

On September 30 the owner selected **v1.0.0** as the start of cross-version save compatibility. Pre-1.0 development/testing saves may break. The [ADR 0006 amendment](../adr/0006-use-versioned-json-snapshot-saves.md#amendment--september-30-2026), related ADR 0007 clauses, and [content/persistence contract](../wiki/content-assets-and-persistence.md#durable-saves) were reconciled in `27a393d36b02da8dfcaa46ab07d0fdebc9ce5639`. This report records that decision rather than changing it.

For R3, retain the current rules identifier and numeric wire representation, preserve historical DTOs and fixtures, and require deterministic save/load continuation within the same build. A compatibility-policy amendment does not replace that behavioral proof.

The bounded UnitsNet exception remains visible in the historical register. Randomness, narrative, refit, recovery, and aggregation retain their deferred triggers or unresolved decisions. Current ship content V6, system-definition content V1, and save V10 remain implemented and unreleased. This follow-up makes no release, broader gameplay, or full-ADR certification claim.

## Executed reproductions and focused correction

These are orchestrator-executed receipts inspected for this update, not executions by the documentation worker. Each log begins with the worker HEAD. Local receipt names identify the retained evidence packet; final public receipts belong to PR #138.

| Receipt and revision | Observed outcome |
| --- | --- |
| R2: `content-red-build2.log`/`.exit`, test-only `84c3a77e0e22c98d10af2051aaa56354e7a363f0`, production at the initial audited revision | Core exit **1**: **24 failed, six passed, 30 total**. `EveryFamilyRejectsActualIsolatedSurrogates`, `FactoriesRejectOtherwiseValidDocumentsWithIsolatedDisplaySurrogates`, and `CatalogsRejectOtherwiseValidDocumentsWithIsolatedDisplaySurrogates` cover actual high/low UTF-16 code units in three families. Otherwise-valid factory/catalog cases threw no required family validation exception. Six controls cover valid Unicode equivalence and byte limits. The following Godot Debug build exited **0**, with zero warnings. |
| R1: `content-godot-red2.log`/`.exit`, same test-only revision and production | Godot exit **100** after successful build/import: **65 executed cases, zero errors, 19 assertion failures** in `test_content_ingress_rejects_oversized_files_before_reading_or_decoding`. All seven resource categories reported `TextReads = 1`, expected zero. Oversized definitions rejected downstream after reading; oversized schemas admitted playable bootstrap. The runner's stop threshold prevented execution of the subsequent invalid-raw-UTF-8 test; no baseline result is claimed for that test. |
| R3: `geometry-red-pinned.log`/`.exit`, test-only `3b6be0020c1fe2ecce2a49135649a725d1be5853`, production at the initial audited revision | Exit **1**: **nine failed, 126 passed, 135 total**. `DefensiveWithdrawalFromSmallestPositiveXHeadsEast` expected 90 degrees and got zero with own X `double.Epsilon`, contact at zero, matched Current contact/shared context, positive propulsion, and all withdrawal constraints satisfied; `WeaponUnpowered` blocked return fire. `ExactPowerOfTwoRescalingPreservesWithdrawal` expected 36.86989764584402 degrees for represented 3:4 displacement and got 45. |
| R3: `geometry-green2.log`/`.exit`, corrected `7bd42bd7d3154d01028e3228be04a537f732562d` | Exit **0**: **147 passed, zero failed/skipped**. `WithdrawalGeometryTests` supplies 135 numerical cases; `WithdrawalGeometryContinuationTests` adds twelve command/save-load cases. `DefensiveShotResponseAppliesAndContinuesAfterLoad` uses real observation, scan, and accepted shot; `CautiousObservedWakeAppliesAndContinuesAfterLoad` uses genuine observed facts and exact correlated wake. Both assert applied courses, pre/post-decision round trips, byte-equivalent saves, and continued movement. |

The corrected `DecisionGeometry.HeadingBetween` subtracts represented endpoints directly and scales both components together only when subtraction overflows. Both policies use it; the cautious policy retains its previous toward-heading-plus-180 calculation and rounding. Signed subnormal axes, quadrants, mixed magnitudes, opposite finite extremes, and coincident-position fallback are covered. No rules identifier, wire field, historical DTO, or fixture rewrite is claimed.

The first content compile attempt and first Godot attempt without managed assemblies were setup failures. They do not establish R1/R2 behavior. The successful build/import run above supplies the R1 reproduction.

### Exact executor commands

Commands ran from their respective `content135` or `geometry135` worker worktrees. The wrapper resolves the checkout-pinned SDK before execution. Logs, not these commands alone, establish the outcomes above.

R2 reproduction and Godot Debug build:

```sh
rexec --shell 'git rev-parse HEAD; dotnet_dir="$(./scripts/resolve-dotnet.sh)"; export PATH="${dotnet_dir}:${PATH}" MSBUILDDISABLENODEREUSE=1; dotnet test tests/AlterCourse.Core.Tests/AlterCourse.Core.Tests.csproj -c Release --disable-build-servers --filter FullyQualifiedName~ContentTextAdmissionTests; content_test_exit=$?; printf "CONTENT_TEST_EXIT=%s\n" "$content_test_exit"; dotnet build src/AlterCourse.Godot/AlterCourse.Godot.csproj -c Debug --warnaserror --disable-build-servers; content_build_exit=$?; printf "GODOT_BUILD_EXIT=%s\n" "$content_build_exit"; if [ "$content_build_exit" -ne 0 ]; then exit "$content_build_exit"; fi; exit "$content_test_exit"'
```

R1 real Godot reproduction:

```sh
rexec --shell 'git rev-parse HEAD; dotnet_dir="$(./scripts/resolve-dotnet.sh)"; export PATH="${dotnet_dir}:${PATH}" MSBUILDDISABLENODEREUSE=1; dotnet build src/AlterCourse.Godot/AlterCourse.Godot.csproj -c Debug --warnaserror --disable-build-servers && godot_bin="$(./scripts/resolve-godot.sh)" && "$godot_bin" --headless --path src/AlterCourse.Godot --import && "$godot_bin" --headless --path src/AlterCourse.Godot --script res://addons/gdUnit4/bin/GdUnitCmdTool.gd --ignoreHeadlessMode -a res://tests/GameplayShellTest.gd -rd .godot/gdunit-reports-content-red'
```

R3 failing reproduction:

```sh
rexec --shell 'git rev-parse HEAD; dotnet_dir="$(./scripts/resolve-dotnet.sh)"; export PATH="${dotnet_dir}:${PATH}" MSBUILDDISABLENODEREUSE=1; dotnet test tests/AlterCourse.Core.Tests/AlterCourse.Core.Tests.csproj -c Release --disable-build-servers --filter FullyQualifiedName~WithdrawalGeometryTests'
```

R3 corrected geometry and command/save-load proof:

```sh
rexec --shell 'git rev-parse HEAD; dotnet_dir="$(./scripts/resolve-dotnet.sh)"; export PATH="${dotnet_dir}:${PATH}" MSBUILDDISABLENODEREUSE=1; dotnet test tests/AlterCourse.Core.Tests/AlterCourse.Core.Tests.csproj -c Release --disable-build-servers --filter "FullyQualifiedName~WithdrawalGeometryTests|FullyQualifiedName~WithdrawalGeometryContinuationTests"'
```

## Inherited baseline and preliminary observations

The orchestrator supplied retained receipts for `rexec -- ./scripts/verify.sh` at the initial audited revision and documentation candidate `9e31f9e9ac8795ac7e145c774f1a6caee61ff3d9`. Both exited **0**, with **1,199 Core**, **324 AssetCtl**, and Godot **one integration, two asset-import, and 92 gameplay** cases, plus smoke. The local evidence packet contains `baseline-verify.log`/`.exit` and `adr134-verify.log`/`.exit`; counts and exit files were inspected. These are inherited baseline executions, not final R1–R3 verification. At final documentation head `d20021d2c8147f018060a2ea83bf79a01060b0d1`, the orchestrator records passing hosted [canonical](https://github.com/L3DigitalNet/star-trek-alter-course/actions/runs/36765527126), [branch](https://github.com/L3DigitalNet/star-trek-alter-course/actions/runs/36765527298), [Prettier](https://github.com/L3DigitalNet/star-trek-alter-course/actions/runs/36765528422), [Markdown](https://github.com/L3DigitalNet/star-trek-alter-course/actions/runs/36765528324), and [standards](https://github.com/L3DigitalNet/star-trek-alter-course/actions/runs/36765528248) checks for PR #137. None substitutes for checks on the remediation candidate.

Read-only governance observations supplied in `governance-summary.md` span September 30, 19:11:59–19:20 UTC. They record classic `dev`/`main` protection, required Canonical verification and Branch policy checks, conversation resolution, immutable published releases, and the active `v*` tag ruleset. This is preliminary point-in-time configuration evidence, not a runtime enforcement exercise or comprehensive ADR 0013 certification. No governance mutation is part of R1–R3.

ADR 0013's approved `dev` administrator exception remains a residual bypass risk: GitHub cannot restrict that actor exemption by changed paths, and local hooks can be skipped. The observation does not establish a server-perfect admission boundary. `main` and its v0.6.2 release remain unchanged by this work.

The [source-catalog review record](../wiki/sources.md#review-record) retains the full semantic sweep due **2026-10-03**. This targeted follow-up does not reset it. Reviewed owning pages are [content/persistence](../wiki/content-assets-and-persistence.md), [Engineering/combat](../wiki/engineering-and-combat.md#defensive-response), [world/navigation/time](../wiki/world-navigation-and-time.md#strategic-and-tactical-space), [sensors/knowledge/AI](../wiki/sensors-knowledge-and-ai.md#scan-hail-and-cautious-response), and [implementation status](../wiki/implementation-status.md). The spatial and policy pages already require continuous domain position, known nonzero displacement, actor-safe proposals, and ordinary course application; R3 corrects arithmetic toward those contracts. They were preserved rather than cosmetically edited. Content and status pages now distinguish strict intended admission from the reproduced boundary gaps.

## Evidence required before final disposition

| Pending evidence | Required record |
| --- | --- |
| R1/R2 corrections and focused proof | Production/test commits, strict text/byte admission, bounded adapter reads/decoding, valid-input and byte-limit controls, refusal preservation, and actual raw-UTF-8 execution. |
| Integrated R3 revision | Final integrated SHA and relationship to focused tested worker `7bd42bd7d3154d01028e3228be04a537f732562d`; preserve identifier/wire/DTO limits. |
| Integrated verification and review | Exact candidate SHA, canonical and documentation gate receipts, independent review findings/dispositions, and hosted checks. |
| Final status | Per-finding outcome with named wiki/ADR coverage, unavailable evidence, and remaining limitations. |

R1/R2 remain confirmed defects awaiting correction evidence; R3 is corrected with focused evidence awaiting integrated verification and review. The final candidate SHA, full gates, and final per-finding dispositions remain explicitly pending. This document remains draft until those receipts are recorded.
