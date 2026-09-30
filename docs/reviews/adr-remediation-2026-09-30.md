---
schema_version: '1.1'
id: 'reference-avt52d-adr-remediation-2026-09-30'
title: 'ADR Remediation Follow-up: September 30, 2026'
description: 'Reproduced and corrected content-admission and defensive-heading defects, focused proof, and remaining acceptance receipts for Issue 135.'
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

This draft records the focused R1–R3 follow-up for [Issue #135](https://github.com/L3DigitalNet/star-trek-alter-course/issues/135) and [Final draft PR #138](https://github.com/L3DigitalNet/star-trek-alter-course/pull/138). All three defects were reproduced and fixed, with passing focused content/adapter and geometry/continuation evidence and combined canonical verification. Native geometry and security reviews confirmed the scoped claims. Documentation gates, mutation evidence, cross-agent review, and final hosted acceptance receipts remain pending. This record does not claim Ready, final closure, or merge authorization.

The [September 27 register](adr-conformance-2026-09-27.md) retains its fourteen-ADR scope, original findings, dispositions, and execution receipts. This report adds evidence without rewriting that history. [Active ADRs](../adr/README.md) own architecture; the [wiki](../wiki/README.md) owns gameplay; source and executed tests establish actual behavior. This is neither a gameplay specification nor an implementation plan.

The initial audited `dev` revision is `efd4ac93894fc28ea0c3ea40ca1be008dd4d6631`, with fourteen ADRs. [Issue #136](https://github.com/L3DigitalNet/star-trek-alter-course/issues/136) and replacement [PR #137](https://github.com/L3DigitalNet/star-trek-alter-course/pull/137) adopted [PR #134](https://github.com/L3DigitalNet/star-trek-alter-course/pull/134)'s documentation as `7e2df746688f63df9936988d20978710e0aaf2d4`. The merged tree equals final PR #137 head `d20021d2c8147f018060a2ea83bf79a01060b0d1`; the original PR #134 was closed with the replacement reference. All eighteen active ADRs apply according to their scopes. Production source, tests, and scripts remain identical to the initial audited revision at that adopted base. Documentation adoption did not certify ADR 0015–0018 or remediate R1–R3. Compatibility amendments are integrated at `27a393d36b02da8dfcaa46ab07d0fdebc9ce5639`; the combined corrected source/runtime candidate inspected here is `e1c89f9de42f827cb8132207daa707c354fe88b1`. That is a source candidate, not this report's own commit or the final PR head. PR #138's execution receipt owns the exact final head and its hosted checks.

## Findings and obligation mapping

The stable `ADR-NNNN-Rnn` IDs below refer to the [historical obligation register](adr-conformance-2026-09-27.md#obligation-register). The following outcomes join exact-revision execution to the inspected source paths; they do not rewrite the historical obligation table.

| Finding | Reproduced baseline defect | Applicable historical obligations | Remediation disposition |
| --- | --- | --- | --- |
| R1 | [GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs) materialized definitions and schemas with `GetAsText` before downstream admission. The real adapter probe observed one text read for each oversized category instead of zero; oversized schemas still allowed playable bootstrap. | ADR-0001-R01/R04/R05; ADR-0005-R01/R02/R03/R06/R07/R11/R15. | **Reproduced and fixed.** Bounded native byte ingress and strict schema decoding; 103 passing Godot shell cases and native security review. Remaining global acceptance receipts below. |
| R2 | `FromText` in [ship](../../src/AlterCourse.Core/Content/ShipDefinitionContent.cs), [system](../../src/AlterCourse.Core/Content/SystemDefinitionContent.cs), and [faction](../../src/AlterCourse.Core/Content/FactionDefinitionContent.cs) content used replacement UTF-8 encoding. Actual isolated UTF-16 surrogates failed typed-rejection assertions; otherwise-valid labels were admitted after replacement. | ADR-0005-R01/R02/R03/R06/R07/R11/R15. | **Reproduced and fixed.** Strict encoding preserves family-specific `json.invalid` rejection; 184 passing Core content cases and native security review. Remaining global acceptance receipts below. |
| R3 | [DefensiveCombatDecisionPolicy](../../src/AlterCourse.Core/AI/DefensiveCombatDecisionPolicy.cs)'s unconditional endpoint halving erased represented tiny displacement and changed direction. | ADR-0010-R02/R03/R05/R06/R07; ADR-0004-R01/R05; ADR-0011-R01/R02/R12/R15. | **Reproduced and fixed.** 147 passing geometry/continuation cases at `7bd42bd7d3154d01028e3228be04a537f732562d`; native geometry review confirms integrated correction. Remaining global acceptance receipts below. |

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
| R1/R2: `content-green.log`/`.exit`, corrected `b6755f74d441002c69e7674283fc193d6786fcfe` | Overall exit **0**: **184 Core content cases passed**, Godot Debug build had **zero warnings/errors**, and **103 gameplay shell cases** passed with zero errors/failures/skips/orphans. Eleven ingress test functions exercise all seven resource categories, including actual invalid raw UTF-8, bounded reads, exact limits, partial reads, misleading lengths, growth, truncation, zero reads without EOF, open/read failures, schema failures, disposal, and fail-closed bootstrap. Core tests assert exact exception families and diagnostic codes for malformed UTF-16 while preserving valid Unicode. |

Content integration preserves worker patches through `43d8918` (reproduction seam), `7a5915b` (regressions), `98bec9e` (fix), and `4c7b829` (three Godot UIDs); `e1c89f9` changes formatting only. Geometry integration uses `ce5022a`, `a97a53c`, and `4361490`, then formatting-only `3c5b1d4`. The orchestrator verified patch equivalence; focused worker results remain revision-specific rather than being relabeled as executions on the final combined head.

### Corrected content boundaries

The [native adapter](../../src/AlterCourse.Godot/src/Gameplay/GodotContentFileAccess.cs) retains Godot `FileAccess` and `res://`/`user://` handling. Definitions reuse each family's **256 KiB** byte limit and reach `FromUtf8` as unchanged bytes. `ReadRequiredBytes` rejects an oversized declared length before reading, requests at most **8 KiB** at a time, independently caps actual returned data at the limit plus one sentinel byte, continues partial buffers even when they report EOF, and requires an explicit zero-byte `FileEof` completion plus exact agreement with the declared length. Misleading lengths, growth, truncation even after valid JSON, zero-byte `Ok` with unread suffix, and IO failures cannot admit a valid prefix. Opened handles are disposed; failure clears partial simulation/catalog/projection/selection state and disables gameplay.

Schemas have a separate **16 KiB** admission limit, then strict UTF-8 decoding into the existing loader interfaces. Committed system, ship, and faction schemas measure **5,014**, **1,525**, and **544 bytes**, respectively; the limit provides more than three times the largest current schema's size. Existing schema loaders remain authoritative; narrowly caught `JsonException`/`JsonSchemaException` become `schema.invalid`. The adapter retains canonical resource identities and uses a bounded fallback for long or OS-path identities. No second parser, filesystem substitute, or new trust model was introduced.

All three `FromText` factories use exception-throwing UTF-8 encoding, count bytes before allocating encoded output, and catch only `EncoderFallbackException` into their existing family validation exception with `json.invalid` and supplied source identity. Valid surrogate pairs, multibyte text, deliberate U+FFFD, exact byte limits, and equivalent text/byte/stream semantics remain covered. Existing escaped-JSON Unicode tests remain separate evidence from the actual .NET-string regressions.

The new Godot ingress test block emitted **34 `InvalidExternalInput`** and **56 `RecoverableIO`** classifications, with no `ProgrammingDefect` in that block. This is production-mapping plus runtime-log evidence: GDScript asserts bootstrap/read/disposal behavior, not exception families or diagnostic codes directly. Exact typed-code assertions are in Core. Exported-PCK execution was not performed; native API/resource compatibility was inspected and development bootstrap was executed. Nonseekable input is outside the supplied `FileAccess` seam, not an exercised guarantee.

The corrected `DecisionGeometry.HeadingBetween` subtracts represented endpoints directly and scales both components together only when subtraction overflows. Both policies use it; the cautious policy retains its previous toward-heading-plus-180 calculation and rounding. Signed subnormal axes, quadrants, mixed magnitudes, opposite finite extremes, and coincident-position fallback are covered. No rules identifier, wire field, historical DTO, or fixture rewrite is claimed.

The first content compile attempt and first Godot attempt without managed assemblies were setup failures. They do not establish R1/R2 behavior. The successful build/import run above supplies the R1 reproduction.

### Independent native reviews

The fresh native geometry verifier reviewed integrated `3c5b1d47f5f66b921b1ed0036ba132774de8f837` and confirmed all four claims: faithful failing reproduction, geometry/scope, real command/save continuation, and compatibility boundaries. The fresh native security reviewer inspected content `b6755f74d441002c69e7674283fc193d6786fcfe` (source unchanged by the later UID commit) and confirmed all five scoped content/adapter/Unicode/bootstrap claims. Neither reported a confirmed defect or blocking gap. Both were read-only source/evidence reviews, not independent test executions; the content reviewer retained the classification, exported-PCK, and nonseekable-input limits above. Cross-agent review remains pending.

### Exact executor commands

Commands ran from their respective `content135` or `geometry135` worker worktrees. The wrapper resolves the checkout-pinned SDK before execution. Logs, not these commands alone, establish the outcomes above.

The complete focused-content GREEN executor wrapper was not retained in the evidence packet. Its printed source SHA, individual test/build exits, and completed results remain in `content-green.log`; the combined canonical command and receipt below independently verify the integrated source. No reconstructed wrapper is presented as the original invocation.

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

The intermediate geometry-only candidate `3c5b1d47f5f66b921b1ed0036ba132774de8f837` passed canonical verification with exit **0**: **1,346 Core**, **324 AssetCtl**, Godot **one integration, two asset-import, and 92 gameplay** cases, plus smoke and zero C# warnings. That receipt predates integrated content correction.

Combined canonical verification independently passed on source/runtime candidate **`e1c89f9de42f827cb8132207daa707c354fe88b1`**, exit **0**: **1,376 Core**, **324 AssetCtl**, Godot **one integration, two asset-import, and 103 gameplay** cases, smoke, and zero compiler warnings. `integrated-verify.log`/`.exit` records **Linux x86_64**, SDK **10.0.111**, .NET runtimes **8.0.30/10.0.11**, and Godot **4.7.2**. This is an executed runtime-source receipt; final PR head and hosted acceptance are separately identified by PR #138's execution receipt. The exact executor command was:

```sh
rexec --shell 'git rev-parse HEAD; uname -sm; dotnet_dir="$(./scripts/resolve-dotnet.sh)"; export PATH="${dotnet_dir}:${PATH}"; dotnet --version; dotnet --list-runtimes; ./scripts/verify.sh'
```

### Point-in-time governance observation

Read-only observations span September 30, **19:11:59–19:20 UTC**, using existing access. The retained `governance-summary.md`, repository-settings, classic-protection, effective-rules, parent-inclusive ruleset, tag-ruleset, and release snapshots supplied every requested field; none was refused. Selected fields were inspected for this report. This is point-in-time configuration evidence, not a runtime enforcement exercise or comprehensive ADR 0013 certification. No administrative mutation occurred.

| Surface | Observed configuration |
| --- | --- |
| Repository | Public; default branch `dev`; merge commits and squash allowed, rebase disabled; automatic topic-branch deletion enabled; auto-merge disabled. |
| Classic `dev` and `main` protections | Strict `Canonical verification` and `Branch policy`, both app **15368**; pull-request review configuration with **zero required approving reviews**; conversation resolution required; force-push and deletion disabled; restrictions null. |
| Administrator enforcement | `dev` false under the approved owner exception; `main` true. |
| Effective branch rules | Both branch endpoint arrays are empty; classic protection supplies the observed branch controls. |
| Parent-inclusive rulesets and tags | One active tag ruleset, **22032602**, matching `refs/tags/v*`; update and deletion prohibited; bypass-actor list empty. |
| Releases | Eight published immutable releases, v0.1.0 through v0.6.2. Peeled annotated tag targets match their first-parent `main` release commits below. |

| Release | Peeled tag / first-parent `main` commit |
| --- | --- |
| v0.1.0 | `cd8ab32474b18513550dd3a3df9f8e56895225dd` |
| v0.2.0 | `163b8e222aec309663799f59287274ba3db39da3` |
| v0.3.0 | `fae21bd81533b33f2d2d350a4042b9c239cf40e3` |
| v0.4.0 | `b3b6635470003d11260b99a2a56f03a3bfa201f6` |
| v0.5.0 | `0547d061ca4bde76b382274a16e079f46cd076d8` |
| v0.6.0 | `d00460ea8b472c44ea2a8343d43e676efb96000b` |
| v0.6.1 | `f0af2653ca44f17b9f701f6271e199cda1429d16` |
| v0.6.2 | `255eaedc8e27b483b0fd4e2fe0bccf050486b3bf` |

The API's `target_commitish: dev` is not tag-target proof. Untagged adoption baseline `5028501ba607045b6b302fd62957f140ce65fcbc` has no release requirement. No release, tag, or `main` promotion is part of this remediation.

ADR 0013's approved `dev` administrator exception remains a residual bypass risk: GitHub cannot restrict that actor exemption by changed paths, and local hooks can be skipped. The observation does not establish a server-perfect admission boundary. `main` and its v0.6.2 release remain unchanged by this work.

The [source-catalog review record](../wiki/sources.md#review-record) retains the full semantic sweep due **2026-10-03**. This targeted follow-up does not reset it. Reviewed owning pages are [content/persistence](../wiki/content-assets-and-persistence.md), [Engineering/combat](../wiki/engineering-and-combat.md#defensive-response), [world/navigation/time](../wiki/world-navigation-and-time.md#strategic-and-tactical-space), [sensors/knowledge/AI](../wiki/sensors-knowledge-and-ai.md#scan-hail-and-cautious-response), and [implementation status](../wiki/implementation-status.md). The spatial and policy pages already require continuous domain position, known nonzero displacement, actor-safe proposals, and ordinary course application; R3 corrects arithmetic toward those contracts. They were preserved rather than cosmetically edited. Content and status pages now describe corrected ingress and distinguish focused proof from final acceptance.

## Evidence required before final disposition

| Pending evidence | Required record |
| --- | --- |
| Documentation gates | Repository formatting, Markdown/frontmatter/standards, and final link-validation receipts after this prose update. |
| Further acceptance evidence | Mutation execution/results and cross-agent review findings/dispositions; no score, threshold, or pass is presumed. |
| Final hosted admission | Final PR #138 head, hosted checks, acceptance receipt, and workflow disposition, recorded on the PR rather than as a self-referential SHA in this report. |

R1–R3 are reproduced and fixed with focused evidence and native review. There is no remaining product-decision blocker. The global acceptance receipts above remain pending, so the report remains draft and makes no Ready, merge, or release claim. Exported-PCK execution and nonseekable-input coverage remain explicit limitations, not silently satisfied gates.
