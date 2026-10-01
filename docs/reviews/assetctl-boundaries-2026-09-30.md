---
schema_version: '1.1'
id: 'reference-zt6k6r-assetctl-boundaries-2026-09-30'
title: 'AssetCtl Boundary Corrections — September 30, 2026'
description: 'Records scoped AssetCtl corrections, source and regression evidence, operator recovery implications, and verification limits.'
doc_type: 'reference'
status: 'active'
created: '2026-09-30'
updated: '2026-09-30'
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

The session's raw commands, logs, and exit receipts are retained under ignored `.workflow/evidence/assetctl/{lifecycle,diagnostics,output,publication}/`, with adjacent scoped security reviews, the initial `integrated-full.log`/`.exit`, and corrected `integrated-green.log`/`.exit`. They are local execution evidence, not distributed documentation dependencies. Cross-agent review, final canonical verification, and hosted admission remain pending until their exact receipts are established by the governing change. No canonical counts or merge state are inferred from the AssetCtl results.

Linux descriptor admission requires `openat`/`statx` and mounted procfs. Asset locks serialize cooperating writers; the authority envelope does not authenticate arbitrary same-UID rewrites of all evidence. No atomic revision comparison against those writers, simultaneous two-file replacement, universal power-loss durability, exported-game execution, live-provider qualification, or spend qualification is claimed. Operators must inspect selected state and receipts before retrying degraded reporting and preserve legacy quarantined artifacts for manual inspection.
