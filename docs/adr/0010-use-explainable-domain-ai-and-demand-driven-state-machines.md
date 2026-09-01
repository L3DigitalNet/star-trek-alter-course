---
schema_version: '1.1'
id: 'adr-0010-star-trek-alter-course-use-explainable-domain-ai-and-demand-driven-state-machines'
title: 'ADR 0010: Use Explainable Domain AI and Demand-Driven State Machines'
description: 'Defines strategic and tactical AI ownership, information limits, decision traces, determinism, and state-machine package policy.'
doc_type: 'adr'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'simulation'
  - 'ai'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0008-use-structured-observability-with-serilog.md'
  - 'docs/adr/0009-use-layered-testing-and-architecture-conformance.md'
supersedes: []
superseded_by: null
source:
  - 'https://github.com/dotnet-state-machine/stateless'
  - 'https://github.com/chickensoft-games/LogicBlocks'
  - 'https://github.com/limbonaut/limboai'
  - 'https://limboai.readthedocs.io/en/stable/'
confidence: 'high'
visibility: 'public'
license: 'MIT'
project:
  decision_makers:
    - 'project owner'
  consulted: []
  informed: []
  amends: []
  amended_by: []
---

# Use explainable domain AI and demand-driven state machines

## Context and Problem Statement

Artificial intelligence is a core simulation system in Star Trek: Alter Course. Factions and ships must pursue goals, respond to incomplete information, allocate resources, honor or violate treaties, negotiate, reinforce territory, withdraw, assist, deceive, and fight without waiting for the player to enter a scene.

Generic game AI packages commonly center on behavior trees, scene-bound agents, navigation, or finite-state machines. Those techniques can be useful for moment-to-moment behavior, but they do not supply the political, economic, doctrinal, informational, and temporal model that determines strategic action in this game. An external large-language-model service would add latency, cost, nondeterminism, availability risk, and an opaque decision boundary without replacing the need for authoritative game rules.

State machines pose a related design question. Explicit states are useful for equipment, missions, diplomacy, and tactical modes, but making a state-machine framework the universal domain architecture would turn ordinary state transitions into framework configuration and encourage speculative hierarchy.

This decision governs autonomous faction and ship decisions, AI access to information, decision reproducibility and explanation, and the use of behavior-tree or state-machine packages. It applies whenever non-player actors select consequential actions or a system proposes a framework to control domain state transitions.

It does not define the complete strategic-AI algorithm, faction doctrines, tactical maneuvers, diplomacy formulas, difficulty adjustments, or content values. Those are system designs within the boundary established here.

How should the project implement autonomous actors and state transitions so that they remain explainable, deterministic, information-limited, and aligned with the persistent simulation?

## Decision Drivers

- Factions must act independently of the player and current Godot scene.
- Decisions must use goals, doctrine, relationships, resources, commitments, and uncertainty.
- AI must act on information available to the actor rather than omniscient world state.
- Strategic behavior needs deterministic reproduction and diagnostic explanation.
- AI outcomes must remain authoritative Core commands and state transitions.
- The game must function fully offline without a hosted AI service.
- Tactical behavior may need different techniques from strategic planning.
- State-machine libraries should remove demonstrated complexity rather than define every domain object.
- Visual behavior-tree tooling is valuable only where its debugging benefit exceeds engine and addon coupling.
- The implementation must remain testable through headless scenario and long-running simulation tests.

## Considered Options

- Project-owned explainable domain AI with technique and package selection per concrete subsystem.
- A behavior-tree framework as the common AI architecture.
- A universal finite or hierarchical state-machine framework.
- Scripted encounter and mission AI.
- A hosted or local large language model as the primary decision engine.

## Decision Outcome

Chosen option: "Project-owned explainable domain AI with technique and package selection per concrete subsystem", because the game's defining decisions arise from its own world model and must remain deterministic, inspectable, and available without external services.

This decision governs consequential AI and domain transition architecture. It does not prohibit behavior trees, state machines, utility scoring, planners, or scripts as bounded implementation techniques.

### Strategic AI ownership

Strategic faction and fleet AI is implemented in pure C# within `AlterCourse.Core`.

The project owns the model for:

- actor goals and priorities;
- faction doctrine and values;
- relationships and diplomatic posture;
- resources, force availability, logistics, and territorial interests;
- obligations, treaties, missions, and prior commitments;
- known threats, opportunities, and uncertainty;
- candidate actions and preconditions;
- costs, risks, benefits, and time horizons;
- deterministic conflict resolution and tie-breaking;
- resulting Core commands;
- explanation and diagnostic output.

No package is expected to provide these semantics. A package may supply a focused algorithm or data structure admitted under ADR 0003, but the project remains able to express why an action was legal, considered, scored, and selected.

### Decision pipeline

Consequential AI decisions follow an explicit conceptual pipeline:

1. Build an actor-specific information and capability snapshot.
2. Identify active goals, obligations, and constraints.
3. Generate plausible candidates from available actions.
4. Reject candidates that violate hard rules or unavailable capabilities.
5. Evaluate remaining candidates using explicit rule, score, plan, or policy components.
6. Apply doctrine, relationships, risk tolerance, uncertainty, and resource modifiers.
7. Resolve ties deterministically, using an identified random stream only when intentional.
8. Return a typed Core command or a deliberate no-action result.
9. Retain or emit a structured explanation appropriate to the consequence.

The exact algorithm can vary by decision class. A border reinforcement decision may use scored candidates, while a multi-step war objective may use a goal-oriented planner. The common requirement is that the inputs, constraints, choice, and resulting command are explicit.

An AI does not mutate arbitrary world state while evaluating candidates. It proposes commands that pass the same domain validation and application boundaries as player or scripted commands.

### Information boundary

An autonomous actor receives an information projection appropriate to that actor.

The projection may include:

- confirmed observations;
- stale reports;
- estimates and confidence;
- known routes and hazards;
- diplomatic messages;
- intelligence shared by allies;
- doctrine and internal resources;
- public or treaty-governed information.

It does not expose hidden ships, exact enemy damage, undiscovered locations, player-only UI state, or arbitrary access to the complete mutable world unless a specific actor capability makes that information legitimately known.

Difficulty settings may alter doctrine, planning budget, error, confidence, or other declared behavior. They must not silently grant ordinary actors omniscience unless the game explicitly labels that mode.

The same information model supports player sensors and map projections under ADR 0004. AI-specific convenience adapters cannot bypass it.

### Explainability

Every strategically consequential decision produces an explanation object or equivalent structured result sufficient to answer:

- what the actor was trying to achieve;
- what information and capabilities it used;
- which candidates were considered;
- which hard constraints rejected candidates;
- which score or policy components favored the selected action;
- whether and how randomness affected the choice;
- how ties were resolved;
- which command was issued;
- why no action was possible when that is the result.

The explanation is available to tests and diagnostics. Player-facing explanations may reveal only information the player is allowed to know.

ADR 0008 governs how explanation data is logged. Logs are not the sole storage location for explanations needed by gameplay, debugging tools, or assertions.

### Determinism and budgets

AI runs on the simulation clock and scheduler under ADR 0007.

Given the same actor snapshot, configuration, content, random stream state, and decision budget, the AI produces the same semantically observable result. Candidate iteration and tie-breaking use stable ordering.

Each strategic decision has a bounded work budget expressed in a form suitable to the algorithm, such as candidate limits, planning depth, node count, or simulated horizon. Wall-clock timeout is not the sole determinant of the chosen action because machine speed would change game behavior.

If a budget is exhausted, the AI returns the best valid result found according to a documented fallback, or no action. It does not leave partial world mutations.

### Tactical AI

Tactical ship AI also resides in Core when it affects authoritative maneuvering, targeting, power, communications, withdrawal, or combat outcomes.

Tactical behavior may use a combination of:

- explicit modes and state transitions;
- utility scoring;
- doctrine-specific rules;
- short-horizon planning;
- steering or geometric calculations;
- scripted procedures for a bounded maneuver;
- deterministic random variation.

Tactical AI is not required to use the same internal algorithm as strategic AI. It does share the information boundary, command boundary, deterministic ordering, and explanation requirements appropriate to its consequence.

Godot-facing animation or purely visual actors may use engine-local behavior without becoming tactical authority.

### State-transition policy

Use explicit domain state and transition methods before adding a state-machine package.

An enum or discriminated domain state plus validated transitions is preferred when:

- the state set is small;
- transitions are easy to inspect;
- entry and exit behavior is limited;
- persistence can represent the state directly;
- hierarchy would add no clarity.

A library becomes justified when repeated transition matrices, guards, entry and exit actions, hierarchy, reentry, or visualization create material local complexity.

When a general managed finite-state-machine library is justified, Stateless is the preferred first candidate because it is focused, mature, and independent from Godot. Adoption still requires a concrete consumer and must keep persisted state in project-owned models.

LogicBlocks may be evaluated when a concrete subsystem genuinely benefits from hierarchical state composition, async lifecycle handling, or richer state-machine serialization that Stateless does not provide. It is not the default.

A state-machine package controls transition mechanics. It does not own entity identity, persistence, domain commands, or the strategic-AI model.

### Behavior trees and LimboAI

A behavior-tree framework is not the strategic-AI foundation.

LimboAI may be reevaluated for a concrete Godot-facing or tactical behavior whose complexity materially benefits from:

- visual behavior-tree authoring;
- hierarchical state-machine editing;
- runtime visual debugging;
- reusable scene-integrated tasks.

Before adoption, the project must verify the current Godot and C# integration path, supported export platforms, addon update process, headless-test strategy, license, and separation from Core authority.

A LimboAI tree may orchestrate presentation-local behavior or translate observations into Core commands. It must not become the only representation of strategic doctrine, faction resources, treaties, or persistent world decisions.

### Scripted behavior

Scripts and authored events may constrain or propose behavior for a specific mission, tutorial, or story sequence. They do not bypass Core rules or freeze unrelated autonomous actors.

A scripted mission may say that a captain intends to negotiate, escape, or ambush. The resulting action still uses available information, capabilities, and domain commands unless the script explicitly represents an extraordinary authored event supported by game rules.

The universe does not wait for a script trigger to activate ordinary faction behavior.

### Large language models

Core gameplay does not depend on a hosted or local large language model.

An LLM is not used as the authoritative engine for:

- strategic choices;
- tactical commands;
- diplomacy outcomes;
- rules adjudication;
- content validation;
- persistent state transitions.

This preserves offline operation, deterministic testing, explainability, performance, and control over setting-consistent rules.

Future optional LLM-assisted flavor text, development tooling, or content-authoring support requires a separate trust and fallback design. Such output cannot mutate the world without validated typed commands.

### Consequences

- Good, because strategic behavior is built from the actual faction and world model.
- Good, because decisions can be reproduced and explained.
- Good, because actors are constrained by what they know and can do.
- Good, because the game remains fully functional offline.
- Good, because tactical and strategic AI can use different appropriate techniques.
- Good, because state-machine packages are introduced only when their mechanics provide demonstrated value.
- Bad, because the project must design and maintain its own decision models.
- Bad, because explanation objects and stable candidate ordering require additional implementation.
- Bad, because visually authored behavior trees are not available as the universal solution.
- Bad, because bounded deterministic planning may require careful optimization as world scale grows.

### Confirmation

A change is in scope when it adds or changes autonomous decisions, actor information access, AI difficulty behavior, planning or scoring, behavior trees, state machines, or an LLM integration.

Conformance is confirmed by:

- Core-only tests for strategic and tactical decisions;
- tests that vary hidden world truth while holding actor knowledge constant;
- stable-order and deterministic replay tests;
- assertions over explanation objects and rejected constraints;
- scenario tests for resources, treaties, doctrine, uncertainty, and degraded capabilities;
- long-running tests for starvation, oscillation, deadlock, and pathological faction behavior;
- architecture checks that prevent Godot or unrestricted presentation state from entering strategic AI;
- dependency review before any state-machine or behavior-tree package is admitted;
- a validated typed command boundary for every consequential action.

## Pros and Cons of the Options

### Project-owned explainable domain AI with technique and package selection per concrete subsystem

- Good, because the AI model aligns directly with the persistent simulation.
- Good, because strategic and tactical problems can use different appropriate algorithms.
- Good, because decisions remain inspectable and testable.
- Bad, because there is no turnkey framework for the defining behavior.
- Bad, because multiple bounded techniques require consistent command and explanation contracts.

### A behavior-tree framework as the common AI architecture

- Good, because visual trees and debuggers can make local behavior understandable.
- Good, because behavior trees are established for reactive game agents.
- Bad, because long-horizon political and resource decisions do not naturally fit a scene-oriented tree.
- Bad, because persistent faction state and doctrine would leak into framework tasks and blackboards.
- Bad, because framework execution can become the hidden source of ordering and state.

### A universal finite or hierarchical state-machine framework

- Good, because legal modes and transitions can be explicit.
- Good, because entry and exit behavior can be centralized.
- Bad, because strategic choice among goals is not primarily a state-transition problem.
- Bad, because many simple entities would acquire unnecessary framework configuration.
- Bad, because persistence and hierarchy semantics could become package-dependent.

### Scripted encounter and mission AI

- Good, because authored situations can produce precise dramatic behavior.
- Good, because small scenarios can be implemented quickly.
- Bad, because the galaxy would wait for player-triggered scripts rather than act autonomously.
- Bad, because scripts tend to bypass resources, information, and normal consequences.
- Bad, because emergent political and strategic behavior would be shallow.

### A hosted or local large language model as the primary decision engine

- Good, because natural-language output and varied apparent reasoning are possible.
- Good, because some authored dialogue could be generated dynamically.
- Bad, because outputs are nondeterministic and difficult to validate.
- Bad, because hosted use adds cost, latency, privacy, and availability dependencies.
- Bad, because a local model adds distribution and hardware constraints.
- Bad, because the game still needs deterministic rules, state, and command validation.

## More Information

"AI" in this project means autonomous decision systems, not a requirement to use machine learning. Deterministic utility, rule, planner, and state techniques are appropriate when they produce convincing behavior from the actor's information and goals.

Explainability is not limited to developer logs. It also enables in-universe reports, officer recommendations, intelligence assessments, and player-visible political consequences without revealing hidden state.
