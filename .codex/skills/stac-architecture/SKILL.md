---
name: stac-architecture
description: Route Star Trek: Alter Course work across the pure deterministic Core, Godot presentation, content, persistence, observability, testing, AI, units, narrative, dependencies, and canonical verification boundaries.
---

# ST:AC architecture router

Load this skill before project implementation. Active ADRs and normative repository documentation remain authoritative; this is a compact routing aid, not a substitute.

## Boundary

- `AlterCourse.Core` owns authoritative deterministic simulation/domain state: semantic multi-scale space, ships, factions, systems, sectors, actors/entities, diplomacy/treaties, damage, missions, schedules, domain AI, typed physical quantities, explicit time, seeded randomness, content models, and versioned persistence models.
- `AlterCourse.Godot` owns scenes, nodes, input, UI, rendering, animation, audio, and engine adapters. It projects Core state and submits typed intent.
- Core never references Godot. Nodes, transforms, Resources, scene lifecycle, timers, signal order, wall clock, ambient RNG, and runtime graph saves never become domain authority.

## Cross-cutting routes

- Canonical ordinary domain content is UTF-8 JSON with versioned schemas plus semantic validation. Resources remain presentation assets.
- Saves are versioned JSON snapshots of explicit Core models, with compatibility validation and migration policy—not scene trees.
- Simulation time, scheduling, and randomness are explicit and deterministic. Presentation may interpolate or animate without changing truth.
- Application logging uses Serilog with Microsoft logging abstractions at boundaries; structured events carry useful identifiers and context without duplicating domain state.
- Tests are layered: xUnit for Core, CsCheck for properties, GdUnit4Net for engine integration, ArchUnitNET for boundaries, named scenarios for behavior, and long-running simulations for stability.
- AI is explainable, deterministic where authoritative, and project-owned. Do not select or embed an LLM by product/model name as the decision authority.
- Physical quantities use explicit units and conversions. Never pass ambiguous numeric distance, duration, velocity, mass, or energy values.
- Branching narrative consumes read-only typed context and requests finite typed consequences. Core validates outcomes; narrative flow never becomes a second rules engine.
- Prefer native Godot/.NET capabilities. Add packages, addons, frameworks, or managers only with demonstrated need and ADR 0003 admission evidence.

## ADR routing

| Concern | Actual file |
| --- | --- |
| Core/Godot boundary | `docs/adr/0001-separate-simulation-from-godot.md` |
| Canonical gate | `docs/adr/0002-use-one-canonical-quality-gate.md` |
| Dependencies/native-first | `docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md` |
| Semantic spatial model | `docs/adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md` |
| Canonical content | `docs/adr/0005-use-json-and-schema-validation-for-domain-content.md` |
| Snapshot saves | `docs/adr/0006-use-versioned-json-snapshot-saves.md` |
| Time/scheduling/randomness | `docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md` |
| Observability | `docs/adr/0008-use-structured-observability-with-serilog.md` |
| Testing | `docs/adr/0009-use-layered-testing-and-architecture-conformance.md` |
| Explainable AI | `docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md` |
| Physical units | `docs/adr/0011-represent-physical-quantities-with-explicit-units.md` |
| Narrative | `docs/adr/0012-keep-branching-narrative-subordinate-to-simulation.md` |
| Branch/release | `docs/adr/0013-use-dev-for-development-and-main-for-releases.md` |

## Verification

Run the narrowest relevant test while iterating. Use `./scripts/fix.sh` when automated formatting is appropriate. Before declaring integrated implementation complete, run the canonical read-only `./scripts/verify.sh`; ADR 0002 owns that gate. Do not weaken analyzers, central settings, or tests to make a failure disappear.
