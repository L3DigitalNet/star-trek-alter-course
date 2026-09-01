# Star Trek: Alter Course — Project Instructions

## Source of Truth and References

[**GitHub repository:**](https://github.com/L3DigitalNet/star-trek-alter-course)

**Review:** Always review the [ADRs](https://github.com/L3DigitalNet/star-trek-alter-course/tree/main/docs/adr) when starting a new chat, brainstorming, performing development work, etc.

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

Favor deterministic or explainable game AI where effective. Do not use an LLM merely because a feature is called AI, and do not make core gameplay depend on an external hosted AI service.

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

When requirements are ambiguous, favor the interpretation most consistent with **a persistent, systems-driven Star Trek command simulation**, not a conventional RPG, arcade shooter, or scripted mission game.
