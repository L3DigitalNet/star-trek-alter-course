---
schema_version: '1.1'
id: 'prompt-pbv8u1-project-instructions-chatgpt'
title: 'Star Trek: Alter Course Project Instructions for ChatGPT'
description: 'Defines project-specific guidance for ChatGPT conversations about Star Trek: Alter Course.'
doc_type: 'prompt'
status: 'active'
created: '2026-09-01'
updated: '2026-09-06'
tags: []
aliases: []
related:
  - 'docs/wiki/README.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/open-questions.md'
---

# Star Trek: Alter Course — Project Instructions

## Source of Truth and References

[**GitHub repository:**](https://github.com/L3DigitalNet/star-trek-alter-course)

**Design wiki:** The [design wiki](https://github.com/L3DigitalNet/star-trek-alter-course/tree/dev/docs/wiki) is the single source of truth for the game's design. Read the relevant page before brainstorming or proposing a change; the guidance below is a summary that defers to it.

**Review:** Review the active [ADRs](https://github.com/L3DigitalNet/star-trek-alter-course/tree/dev/docs/adr) for the work being considered. `dev` is the development authority; `main` records releases.

## Approved Next Design

[Faction Intent and Autonomous Assignment](../wiki/faction-intent-and-autonomous-assignment.md) is the owner-approved next bounded M5 design after v0.5.0. Its proof and six decisions are settled; runtime implementation has not started. Content V4/save V6 remain current; V7 is planned. Do not present approved design as implemented behavior or declare M3/M5 complete.

The contract permits idle directly controlled NPC assignment without preemption, narrow own-asset administrative information rather than sensor intelligence sharing, closed Ship/Faction scheduler targets, typed bootstrap, migration that invents no political state, and no randomness or faction/affiliation UI. Read that page for acceptance and non-goals instead of rebuilding the design from this summary. The [question register](../wiki/open-questions.md) distinguishes scoped answers from broader deferred decisions.

## Identity and Vision

**Star Trek: Alter Course (ST:AC)** is a modern 2D starship command/strategy game inspired by **Super Star Trek, EGA Trek, and Netrek**, drawing primarily from **TOS, TNG, DS9, and Voyager**.

The player is a **starship captain**, not an individual RPG character. The game is map-centric and built around command decisions, interconnected ship systems, and a persistent galaxy.

Core pillars:

- tactical starship combat;
- engineering and damage management;
- navigation, sensors, and exploration;
- diplomacy, politics, trade, treaties, alliances, and war;
- autonomous factions and strategic events;
- durable consequences from player actions.

There is **no conventional character leveling or skill-tree progression**. Progression occurs through world changes such as reputation, political relationships, territory, wars, treaties, discoveries, economic effects, and ship condition.

The galaxy should behave like a simulation the player participates in, not a sequence of encounters waiting for the player.

## Presentation

The game is **2D**, resembling a modern evolution of classic map-driven Star Trek games. Favor a large central tactical/strategic map, persistent ship/status information, contextual panels, high information density, mouse and keyboard interaction, and clarity over graphical spectacle.

Do not drift toward a 3D bridge simulator, third-person starship game, or conventional character RPG.

## Technology

Use **Godot 4.x**.

Prefer native Godot capabilities before external frameworks. Keep simulation/domain logic separated from UI/rendering enough for core systems to be tested independently.

Use data-driven definitions for ships, factions, weapons, systems, encounters, maps, and similar content where practical.

Do not add databases, ECS frameworks, networking layers, service architectures, or similar infrastructure solely for hypothetical future needs.

## Architecture

### Simulation First

Model systems according to domain relationships rather than UI screens. Major domains include navigation, sensors, power, propulsion, shields, weapons, damage/repair, communications, factions, diplomacy, economy, strategic AI, and missions/events.

Subsystems should interact. Damage should create meaningful operational consequences rather than merely reduce generic health.

### Spatial Scale

The architecture should permit galaxy regions, sectors, star systems, and local tactical space without requiring a rewrite of the fundamental map/navigation model. Not every scale must exist initially.

### Persistent World

Important decisions must modify durable world state. NPC factions should have goals, relationships, resources, and conflicts independent of the player. Strategic events should occur without direct player involvement.

Avoid a universe that freezes until the player arrives.

### AI

AI is a core simulation system. Actors should make decisions from available information, objectives, doctrine, relationships, resources, and uncertainty rather than mainly from scripts.

Consequential AI is deterministic, explainable, information-limited Core logic under ADR 0010, using validated typed commands and bounded work. Core gameplay must function offline and does not use a hosted or local LLM as authoritative strategic, tactical, or rules logic. The first faction slice consumes no randomness; any later stochastic consumer must satisfy ADR 0007's versioned continuation contract.

## Tactical and Engineering Depth

Combat should revolve around **ships and their systems**, not generic hit-point attrition.

Weapons, shields, power, maneuverability, sensors, targeting, subsystem damage, degraded modes, repairs, and resource constraints should interact meaningfully.

Engineering choices should create tradeoffs. A damaged ship should often remain functional in interesting ways rather than simply becoming a lower-health version of itself. Avoid complexity that creates bookkeeping without meaningful decisions.

## Star Trek Philosophy

Star Trek is not solely a combat setting. Systems should allow multiple approaches where appropriate, including diplomacy, withdrawal, negotiation, deception, assistance, investigation, trade, and combat.

Combat may be correct, but should not automatically be the only or best solution. Consequences should reflect faction values, political conditions, treaties, prior behavior, and context.

## Development Standards

Prioritize:

1. simulation correctness;
2. maintainable architecture;
3. automated testability;
4. player-visible clarity;
5. performance;
6. visual polish.

Prefer simple implementations with strong domain boundaries over generalized frameworks. Avoid speculative abstractions without a plausible near-term consumer. Inspect existing architecture before creating parallel mechanisms. Fix root causes rather than layering workarounds.

Keep simulation logic independently testable. Test state transitions, subsystem interactions, persistence, procedural invariants, AI constraints, regressions, and important failure states. Scenario and long-running simulation tests are especially valuable for interconnected systems.

## Scope and Agent Guidance

Build the smallest complete version of each system that supports real gameplay, then deepen it. Prefer a narrow but interconnected simulation over a broad collection of shallow systems.

When implementing changes:

- preserve the simulation-first architecture;
- inspect existing code/docs before inventing conventions;
- keep domain logic separate from UI where practical;
- prefer Godot-native solutions when adequate;
- identify architectural consequences of major changes;
- do not reduce simulation depth for convenience;
- do not add complexity solely for hypothetical scale.

Interpret implementation details within the owning approved wiki contract and the persistent, systems-driven command-simulation vision. This does not authorize resolving open product questions or crossing an explicit non-goal silently. When a required consumer exceeds approved scope, return to governed design refinement rather than inventing permission from broad vision statements.
