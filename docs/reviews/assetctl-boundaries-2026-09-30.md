---
schema_version: '1.1'
id: 'reference-zt6k6r-assetctl-boundaries-2026-09-30'
title: 'AssetCtl Boundary Corrections — September 30, 2026'
description: 'Records scoped AssetCtl corrections, source and regression evidence, operator recovery implications, and verification limits.'
doc_type: 'reference'
status: 'active'
created: '2026-09-30'
updated: '2026-10-03'
tags:
  - 'development'
  - 'validation'
aliases: []
related:
  - 'docs/development-quality.md'
  - 'docs/wiki/content-assets-and-persistence.md'
  - 'docs/wiki/sources.md'
  - 'docs/wiki/asset-pipeline-tool.md'
---

# AssetCtl boundary corrections — September 30, 2026

## Scope and authority

[Bug #141](https://github.com/L3DigitalNet/star-trek-alter-course/issues/141) addresses the AssetCtl correction targets found during [PR #140's repository review](../wiki/sources.md#post-remediation-repository-review--2026-09-30). The session baseline was `9352855551b6906fc608bca2fcff773d0a49ab87`; source inspection started at integrated candidate `d6fab0c16b56c8bac3fe7dd263461a68abdbaaea`, with the integration correction verified at `d9e25d2f2eca2b2c396af6936b0c1cdb3009d21e`. [ADR 0008](../adr/0008-use-structured-observability-with-serilog.md) and [ADR 0017](../adr/0017-generate-assets-outside-the-game-through-bounded-validated-publication.md) govern these boundaries. The [tool contract](../wiki/asset-pipeline-tool.md) owns normative detail; the [runbook](../development-quality.md#reporting-and-recovery) owns operator actions.

This record preserves the earlier review as historical evidence. It does not amend the prior R1–R3 remediation, claim certification of all 18 binding ADRs, or establish a release. Development ship V6/system V1/save V10 remain unreleased. The latest full gameplay-semantic sweep remains September 26, with the next due October 3. Bug #139's enforcement correction is separate.

## Corrections and source evidence

| Boundary | Corrected behavior | Regression evidence |
| --- | --- | --- |
| Selected bytes and lifecycle | [SelectedAssetReader](../../tools/AlterCourse.AssetCtl/Catalog/SelectedAssetReader.cs) rejects oversize metadata before allocation, caps consumption, and requires exact EOF. [LifecycleBoundary](../../tools/AlterCourse.AssetCtl/Publishing/LifecycleBoundary.cs) retains admitted parents/files and selected bytes; [ManifestMutation](../../tools/AlterCourse.AssetCtl/Publishing/ManifestMutation.cs) rechecks evidence and owned staging before replacement and treats successful rename as commitment. | [SelectedAssetReaderTests](../../tests/AlterCourse.AssetCtl.Tests/SelectedAssetReaderTests.cs), [LifecycleBoundaryRegressionTests](../../tests/AlterCourse.AssetCtl.Tests/LifecycleBoundaryRegressionTests.cs), and [LifecycleStreamFaultTests](../../tests/AlterCourse.AssetCtl.Tests/LifecycleStreamFaultTests.cs). |
| Optional diagnostics | [Program](../../tools/AlterCourse.AssetCtl/Program.cs) and [BestEffortLoggerFactory](../../tools/AlterCourse.AssetCtl/Diagnostics/BestEffortLoggerFactory.cs) isolate nonfatal construction, emission, disposal, and fallback-stderr faults. Default output avoids uncontrolled exception objects/prose. | [DiagnosticBoundaryRegressionTests](../../tests/AlterCourse.AssetCtl.Tests/DiagnosticBoundaryRegressionTests.cs), [DiagnosticPrivacyRegressionTests](../../tests/AlterCourse.AssetCtl.Tests/DiagnosticPrivacyRegressionTests.cs), and [SourceDiagnosticPrivacyRegressionTests](../../tests/AlterCourse.AssetCtl.Tests/SourceDiagnosticPrivacyRegressionTests.cs). |
| Required output and receipts | [CliTypes](../../tools/AlterCourse.AssetCtl/Cli/CliTypes.cs) records commitment separately from required stdout and returns exit 9 on reporting failure. [GenerationOrchestrator](../../tools/AlterCourse.AssetCtl/Generation/GenerationOrchestrator.cs) preserves a proven publication when receipts fail and reports `rollback: not-established` when no rollback result exists. | [OutputBoundaryRegressionTests](../../tests/AlterCourse.AssetCtl.Tests/OutputBoundaryRegressionTests.cs) and [GenerationEndToEndTests](../../tests/AlterCourse.AssetCtl.Tests/GenerationEndToEndTests.cs). |
| Publication and recovery | [PublishingTypes](../../tools/AlterCourse.AssetCtl/Publishing/PublishingTypes.cs) admits parents, snapshots, object identities, and mutable semantic pair ownership before destructive operations. Version-1 journals require corroborating authority evidence. Legacy version-0 journals are quarantined without using their assertions to delete unproven artifacts. | [PublicationObjectBoundaryTests](../../tests/AlterCourse.AssetCtl.Tests/PublicationObjectBoundaryTests.cs) and [PublicationRecoveryTests](../../tests/AlterCourse.AssetCtl.Tests/PublicationRecoveryTests.cs). |

## Execution evidence and limits

Focused worker receipts record lifecycle 117, diagnostic B1 46, output B2 231, and publication 83 passing cases, each with exit 0 and no failures/skips. These are scoped candidate runs, not four disjoint totals or a final canonical receipt. Retained behavioral RED controls include selected-read/lifecycle refusal, diagnostic/output faults, unsafe publication recovery, and unproven-rollback receipt reporting. Compilation failures during intermediate repairs are not behavioral RED proof.

The first full AssetCtl run at integrated source `d6fab0c16b56c8bac3fe7dd263461a68abdbaaea` returned exit 1: 500 passed, eight failed, 508 total, no skips. All eight failures were existing SVG validation regressions whose trusted target-size or known element/attribute detail was lost by overbroad diagnostic normalization. This result is retained as integration RED evidence; it does not establish final admission or undermine the distinction between trusted detail and uncontrolled source prose.

Corrected source `d9e25d2f2eca2b2c396af6936b0c1cdb3009d21e` restores bounded trusted SVG diagnostic detail while keeping unknown source prose out of output, and disables YAML `FileStream` read-ahead at the bounded-read boundary. Its full AssetCtl run passed 511 cases, with no failures/skips and exit 0. The scoped native follow-up confirmed all seven reviewed claims with no remaining findings. YAML's physical-read bound is established by source/API inspection and regression evidence, not a native syscall trace.

The session's raw commands, logs, and exit receipts are retained under ignored `.workflow/evidence/assetctl/{lifecycle,diagnostics,output,publication}/`, with adjacent scoped security reviews, the initial `integrated-full.log`/`.exit`, and corrected `integrated-green.log`/`.exit`. They are local execution evidence, not distributed documentation dependencies. The cross-agent review outcome and its corrections are recorded below; hosted admission and merge state belong to the governing PR.

## Cross-agent review corrections — October 3, 2026

The opposite-provider review of `d9e25d2` confirmed four findings, each reproduced before correction:

- **F1/F2 — receipt sinks replaced outcomes.** Receipt filters omitted `UnauthorizedAccessException`, which Linux raises for permission-denied path writes, and failure-receipt construction or candidate retention could escape a caller's catch. A denied primary sink now falls back normally; both sinks failing after a known commit returns exit 9; failure receipts and retention can no longer replace an original pre-commit refusal; `retained_path` names only files actually written. Cancellation and process-fatal exceptions still propagate.
- **F3 — native ABI.** The open-flag constants are Linux x86-64 values that mean `O_DIRECT`/`O_LARGEFILE` on Linux Arm and Arm64. Every native entry point now refuses with exit 7 unless the OS is Linux and the process architecture is X64; the [tool contract](../wiki/asset-pipeline-tool.md) records the qualification. No other architecture was executed or is supported.
- **F4 — legacy quarantine.** A real interrupted legacy version-0 projection is quarantined with exit 7 and all predecessor, backup, stage, and live-manifest artifacts preserved.

The independent verifier then found that the post-spend generation catch had the same `UnauthorizedAccessException` gap, leaving billed attempts without a failure receipt; it was reproduced and fixed with the exit code unchanged. Raw pre-commit local I/O failures still exit 1: the contract's exit-code table is suggested, while its binding rule preserves existing pre-commit exit codes.

Behavioral RED controls failed on the uncorrected source: 10 receipt cases, four modeled non-X64 admissions, and one post-spend receipt case. All permission cases ran as a non-root user and fail rather than skip under root. Platform tests model admission at the shared production guard; they do not prove each call site invokes it, which is established by source review. Canonical verification at integrated source `7c83d72` passed 1,411 Core and 529 AssetCtl cases plus Godot suites; final source `d3c46fa` passed canonical verification with 1,411 Core and 530 AssetCtl cases, Godot suites, and no failures, skips, or secret-scan findings.

Linux descriptor admission requires `openat`/`statx` and mounted procfs. Asset locks serialize cooperating writers; the authority envelope does not authenticate arbitrary same-UID rewrites of all evidence. No atomic revision comparison against those writers, simultaneous two-file replacement, universal power-loss durability, exported-game execution, live-provider qualification, or spend qualification is claimed. Operators must inspect selected state and receipts before retrying degraded reporting and preserve legacy quarantined artifacts for manual inspection.
