---
schema_version: '1.1'
id: 'reference-bz8n9f-unitsnet-evaluation'
title: 'UnitsNet evaluation for existing quantity boundaries'
description: 'Present-day semantic fit evidence and approved bounded treatment of existing quantity types under ADR 0011.'
doc_type: 'reference'
status: 'active'
created: '2026-09-27'
updated: '2026-09-27'
tags: []
aliases: []
related: []
---

# UnitsNet evaluation

## Decision status

This September 27, 2026 evaluation addresses F06 in [Issue #127](https://github.com/L3DigitalNet/star-trek-alter-course/issues/127). The owner explicitly approved the bounded amendment below in the implementation session on that date. It does not invent a historical rejection. UnitsNet is not admitted to production by this record; the baseline provenance gap remains in the conformance register.

Available-ref `git log --all -S'UnitsNet'` and current-source searches found the ADR and candidate list, but no recorded suitability proof. Relevant introductions are ADR `b4257115b957b91e81c5a6e4e8c4e82b97e01105`, heading/speed/time `71e37a4fcf475b5f98e01ffeb8942f04dbe1c1e9`, distance `f7a3e7cfa07f787a3819c9c488e9a0e36a5043f2`, and abstract power `0f2278ed76b8f0c8e24de7433abeb5d08f5a76d2`. This is absence of repository evidence, not proof that no off-repository discussion occurred.

## Candidate and admission boundary

The evaluated stable package is [UnitsNet 5.75.1](https://www.nuget.org/packages/UnitsNet/5.75.1), targeting .NET Standard 2.0, .NET 8 and .NET 9, under MIT-0 with no declared package dependencies. The [tagged project](https://github.com/angularsen/UnitsNet/blob/UnitsNet/5.75.1/UnitsNet/UnitsNet.csproj) and [tagged Length implementation](https://github.com/angularsen/UnitsNet/blob/UnitsNet/5.75.1/UnitsNet/GeneratedCode/Quantities/Length.g.cs) are version-specific evidence. Upstream v6 prerelease behavior, including relaxed nonfinite handling, is not evidence for this evaluated v5 version.

The current consumers use canonical kilometers, kilometers per second, normalized headings, signed tactical coordinate pairs, and exact integer simulation milliseconds. Abstract `PowerUnits`, `SystemCondition`, scores and counts are not candidates for forced SI modeling. Generic distance/speed conversions are a legitimate UnitsNet capability; negative quantities and unnormalized angles still need project validation. No gameplay consumer currently needs a broad conversion catalog, localized unit parser, or formatted library value.

Wire formats remain project-owned numeric DTOs under ADRs 0005/0006 regardless of runtime library choice. A future library adapter must construct quantities after validation and extract explicit canonical values for JSON and Godot. Library type names, unit enums and localized strings cannot become save identity. Godot mapping also retains the existing scale, orientation, and origin contracts.

## Executed semantic proof

A temporary .NET 8 console probe restored the exact 5.75.1 package through central package configuration local to the probe and ran through `rexec`. The project inherited repository compiler/analyzer policy. The production manifests and lockfiles were not changed. Command: `rexec --pull .scratch/units-proof/packages.lock.json --shell 'export PATH="$(./scripts/resolve-dotnet.sh):$PATH"; dotnet run --project .scratch/units-proof/UnitsProof.csproj -c Release --disable-build-servers'`; exit 0.

| Probe | Actual result | Implication |
| --- | --- | --- |
| `Length.FromKilometers(-1).Kilometers` | `-1` | Generic length does not enforce the existing nonnegative distance invariant. |
| Length/speed canonical `double.MaxValue` | Preserved | Canonical extreme input alone is not a material failure. |
| Maximum kilometers to meters; maximum km/s to m/s | `ArgumentException` | Overflow fails predictably rather than becoming authoritative infinity. |
| Maximum length plus maximum length | `ArgumentException` | Arithmetic overflow is rejected. |
| Length/speed/angle with NaN and either infinity | `ArgumentException` in all nine probes | Evaluated stable version rejects nonfinite input. |
| `Angle.FromDegrees(-90/360)` | `-90` and `360` | A heading wrapper remains necessary for `[0,360)` semantics. |
| 1 km to meters; 1 km/s to meters/second | `1000` in both cases | Required elementary conversions exist. |
| Duration milliseconds `0`, `1`, `2^53-1`, `2^53` | Preserved | Exactness holds within this tested range. |
| Duration milliseconds `2^53+1` | Returned `2^53` | Material failure for exact integer simulation counters. |
| Duration milliseconds `long.MaxValue` | Rounded double `9.223372036854776E+18` | Cannot replace checked integer simulation time/duration without changing semantics. |

UnitsNet duration is unsuitable as the simulation clock representation. That does not establish material failure for length or speed. Signed coordinate composites, heading normalization and fictional quantities also retain project-specific contracts. The current distance/speed structs remain sound; the missing historical proof cannot be repaired by pretending the library failed all dimensions.

No representative comparative performance benchmark or Godot export experiment was performed. No performance superiority is claimed. A new UnitsNet hot-path adoption still requires ADR 0011's representative allocation/execution and adapter proof. A pure package restore/build is compatibility evidence, not complete admission proof.

## Owner-approved bounded ADR amendment

Retain UnitsNet as the preferred first candidate for new standard physical dimensions or a concrete cross-unit conversion requirement. Explicitly permit the existing project-owned canonical distance and speed structs to remain where they enforce finite/nonnegative invariants and expose only the existing canonical units, provided conversion/overflow/boundary tests remain in the canonical suite. Preserve typed public/cross-subsystem boundaries and numeric wire independence. A future expansion into general unit conversion must repeat the focused UnitsNet comparison rather than treating this exception as a blanket rejection.

Rationale: replacing two sound canonical structs solely to satisfy an undocumented historical selection would spread a package API without a demonstrated new conversion consumer. This is a narrow selection-policy amendment, not an assertion that UnitsNet is unsuitable for standard quantities. Existing exact time, heading, signed-position and fictional-domain semantics remain independently justified. The owner's explicit approval resolves the policy decision prospectively; it does not retroactively establish the missing historical evaluation.
