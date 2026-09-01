---
schema_version: '1.1'
id: 'adr-0012-star-trek-alter-course-keep-branching-narrative-subordinate-to-simulation'
title: 'ADR 0012: Keep Branching Narrative Subordinate to the Simulation'
description: 'Defines narrative authority, typed consequence boundaries, package evaluation, save state, and localization ownership.'
doc_type: 'adr'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'narrative'
  - 'simulation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md'
supersedes: []
superseded_by: null
source:
  - 'https://github.com/inkle/ink'
  - 'https://github.com/inkle/ink/blob/master/Documentation/RunningYourInk.md'
  - 'https://github.com/nathanhoad/godot_dialogue_manager'
  - 'https://dialogue.nathanhoad.net/'
  - 'https://docs.godotengine.org/en/4.6/tutorials/i18n/internationalizing_games.html'
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

# Keep branching narrative subordinate to the simulation

## Context and Problem Statement

Star Trek: Alter Course needs authored communications, mission briefings, diplomatic exchanges, investigations, first-contact situations, officer recommendations, and other branching narrative. These interactions can benefit from a narrative language or dialogue editor that manages text, choices, conditions, and flow.

The game is not a sequence of isolated scripted encounters. Factions, ships, treaties, damage, resources, and strategic events remain active outside a conversation. A narrative engine that owns world variables or directly mutates arbitrary game state would create a second rules system, make consequences difficult to validate, and encourage scripts to bypass the persistent simulation.

Ink is a mature engine-agnostic narrative runtime written in C# and designed to integrate with a host game's own UI and logic. Dialogue Manager is a Godot-native alternative with editor integration and a stateless design. Either could help, but adopting one before the project has an authored branching use case would add syntax, tooling, save state, and content workflow prematurely.

This decision governs authored branching narrative, dialogue-runtime authority, communication with Core, narrative-package evaluation, persistence of in-progress narrative, and localization boundaries. It applies whenever authored text or choices can lead to simulation consequences.

It does not govern ordinary UI labels, procedurally assembled status reports, autonomous faction AI, diplomacy formulas, mission generation, voice synthesis, or optional generative text. Those concerns remain under their own systems.

How should the project use narrative tooling without allowing authored scripts to become an alternate world simulation or bypass domain rules?

## Decision Drivers

- Narrative choices must produce durable world consequences through the same rules as other commands.
- Conversations need access to actor-appropriate information without unrestricted mutable world access.
- The game must remain simulation-first rather than encounter-script-first.
- Narrative content should be authorable and testable separately from Godot scenes.
- In-progress conversations and story variables may need save compatibility.
- The project should remain fully playable offline.
- Localization should use stable text identities and existing Godot capabilities where practical.
- A mature narrative runtime can avoid building parsers, branching flow, and authoring tools.
- Engine coupling is acceptable only when editor benefits outweigh loss of portability.
- No narrative package should be installed before a concrete authored branching feature exists.

## Considered Options

- Keep Core authoritative, integrate a narrative runtime through typed context and consequence adapters, and prototype Ink first when needed.
- Adopt Ink immediately as the narrative authority.
- Adopt Dialogue Manager immediately as the narrative authority.
- Build a custom branching narrative language and editor.
- Encode conversations directly in C# or Godot scenes.
- Use an LLM to generate and adjudicate conversations dynamically.

## Decision Outcome

Chosen option: "Keep Core authoritative, integrate a narrative runtime through typed context and consequence adapters, and prototype Ink first when needed", because it gains mature branching tools without creating a second simulation authority or a speculative dependency.

This decision governs authored narrative with simulation consequences. It does not require a narrative package for linear messages, reports, tooltips, or a first vertical slice that has no meaningful branching dialogue.

### Authority boundary

`AlterCourse.Core` remains authoritative for:

- faction and personal relationships;
- treaties, wars, permissions, and reputation;
- ship state, damage, resources, and capabilities;
- mission state and objective validity;
- item, cargo, and personnel transfers;
- time advancement and scheduled consequences;
- skill, trait, sensor, and information checks;
- random outcomes;
- legal commands and state transitions;
- actor knowledge and hidden information.

A narrative runtime owns only narrative-local flow, such as:

- current passage or knot;
- available authored lines and choices;
- local variables used to navigate the authored sequence;
- text tags and presentation metadata;
- return from a bounded subroutine;
- narrative-local history needed by that runtime.

A script cannot directly set arbitrary Core fields, create unrestricted entities, change faction state, grant resources, or bypass validation.

### Context adapter

Before entering or continuing a narrative, the application constructs a read-only context projection.

The projection contains only values the narrative is permitted to observe, such as:

- stable participant and location identifiers;
- localized or display-ready participant names;
- actor-appropriate relationship and reputation bands;
- known facts and sensor results;
- mission phase and permitted objectives;
- ship capability summaries;
- prior choices intentionally exposed to the narrative;
- explicit feature flags or content tags;
- values derived for this authored interaction.

The narrative runtime does not receive a mutable world object, service locator, database handle, Godot scene tree, or unrestricted Core repository.

Context field names and semantics form a versioned integration contract. A narrative should not depend on incidental C# property names or reflection over domain entities.

### Typed consequences

Narrative output crosses into Core through a finite typed consequence or command boundary.

Examples include:

- propose a diplomatic response;
- accept or reject a treaty offer;
- request assistance;
- transfer declared cargo;
- reveal an authorized fact;
- set a mission decision;
- attempt a deception or persuasion action;
- order withdrawal or engagement;
- schedule a follow-up communication.

The adapter validates each consequence through normal Core command handlers. A command may fail because the world changed, the actor lacks authority, resources are unavailable, or the choice is no longer legal. The narrative integration must represent that failure and select an authored fallback, retry, or clean exit; it must not force the state mutation.

Consequences are identified by stable machine names and validated payloads, not by parsing displayed dialogue text.

A group of consequences that must be atomic is represented by one domain command or explicit transaction boundary. The narrative runtime does not approximate a transaction by issuing unchecked mutations in sequence.

### Time and autonomy

Entering a conversation does not implicitly freeze the universe.

The system design must explicitly decide whether a communication:

- consumes no simulation time while the player reads;
- advances a declared duration when a choice is committed;
- occurs concurrently with tactical or strategic events;
- pauses only presentation while Core remains at a decision boundary.

The narrative engine does not own clocks or Godot timers that advance authoritative state. It requests time-bearing Core commands under ADR 0007.

Other factions and actors remain autonomous unless a valid game rule establishes a pause or synchronized encounter.

### Narrative package trigger

No narrative-runtime package is added until a concrete feature needs at least one of:

- branching authored choices with reconvergence;
- reusable authored subflows;
- conditional text driven by exposed context;
- author-managed narrative-local variables;
- save and resume within an authored sequence;
- localization workflow beyond ordinary UI strings.

Linear messages and small fixed menus use ordinary content and UI.

At the trigger, the implementation performs a focused prototype rather than constructing a general narrative platform.

### Ink as the first prototype

Ink is the preferred first candidate because:

- its runtime is written in C# and can run independently from Godot;
- authored Ink compiles to a portable runtime representation;
- it is designed to integrate with a host game's own state and UI rather than supply a complete game engine;
- branching, choices, variables, tags, and reusable narrative flow are mature capabilities;
- Core-facing integration can be tested without requiring a Godot scene;
- the narrative source remains text suitable for version control.

The prototype must verify:

- current .NET and Godot compatibility;
- deterministic behavior for the supported feature set;
- save and restore of narrative-local state;
- typed external-function or adapter boundaries without unrestricted callbacks;
- content build and validation in the canonical quality gate;
- source-level diagnostics for invalid narrative;
- localization strategy;
- license and distribution requirements;
- acceptable authoring and debugging workflow.

If the prototype satisfies these criteria, Ink may be adopted under ADR 0003 without another foundational ADR.

Ink variables do not become the canonical storage for world state. Values copied from Core are refreshed or reconciled through the context contract; durable consequences are committed through typed Core commands.

### Dialogue Manager as the Godot-native comparator

Dialogue Manager is the primary alternative when Godot editor integration materially improves the authoring workflow.

It should be compared with Ink when the concrete feature values:

- Godot-native editing and previews;
- direct Control-node integration;
- stateless or headless dialogue evaluation;
- built-in Godot-oriented translation workflow;
- simpler presentation wiring.

The comparison must account for:

- engine-version and addon lifecycle coupling;
- headless Core testability;
- C# integration;
- persistence of narrative-local progress;
- command and context adapters;
- validation in CI;
- export-platform behavior.

Choosing Dialogue Manager requires documenting why its editor and engine benefits outweigh Ink's engine-agnostic runtime for the actual feature. It remains subordinate to the same Core authority boundary.

### Narrative persistence

An in-progress narrative may persist only the narrative-local state needed to resume faithfully, plus stable identifiers that reconnect it to Core context.

The save snapshot may include:

- narrative definition identifier and version;
- runtime position or serialized narrative-local state;
- participant and mission identifiers;
- pending typed choice or consequence when safe;
- declared compatibility metadata.

It does not persist copies of authoritative faction, ship, mission, or resource state inside narrative variables as an alternate truth.

On load, the adapter validates that the narrative definition and required participants remain compatible. If world state has changed in a way the narrative cannot resume safely, the system follows a documented fallback such as an authored interruption branch, clean cancellation, or explicit incompatibility error.

Narrative-runtime state is mapped into project-owned persistence models under ADR 0006. A package's opaque blob may be stored only when its versioning and validation behavior are understood and bounded.

### Narrative content validation

Narrative content is compiled or parsed during repository verification, not for the first time after release.

Validation covers:

- syntax and unresolved labels;
- declared context variables;
- typed consequence names and payload shapes;
- unreachable or dead-end paths where the authoring language can detect them;
- choice paths that require a missing fallback;
- references to content and participants;
- localization identifiers;
- prohibited direct mutation hooks;
- package-version compatibility.

Automated tests traverse critical paths and consequences. Exhaustive branch exploration is desirable where bounded, but high-value authored sequences still receive named scenario tests for intended context and failure behavior.

A test can substitute an in-memory consequence adapter to verify what the narrative requests without mutating a full world.

### Localization and presentation

Godot's native localization facilities remain the default for ordinary UI and presentation strings.

Narrative source must separate stable identity from displayed text where the selected runtime and localization workflow permit it. Translation should not change consequence identifiers, branch labels used as contracts, or Core commands.

The narrative runtime supplies logical lines, choices, tags, and speaker information. `AlterCourse.Godot` owns:

- text layout and themes;
- input and focus;
- portraits, audio, visual effects, and timing;
- accessibility presentation;
- localization selection;
- display of unavailable or failed choices according to product design.

A narrative script does not directly manipulate arbitrary scene nodes.

### AI and generated text

Narrative scripts may call on actor state or AI explanation to select authored lines, but they do not implement strategic AI under ADR 0010.

An LLM is not used to adjudicate narrative consequences or replace the typed command boundary. Optional generated flavor text would require:

- deterministic or authored fallback;
- content and safety constraints;
- offline behavior;
- disclosure and caching policy;
- no authority to mutate Core;
- a separate architectural decision.

### Consequences

- Good, because authored narrative can be expressive without becoming a second rules engine.
- Good, because choices use the same validated commands and durable consequences as other gameplay.
- Good, because Ink can be evaluated through a focused engine-independent prototype.
- Good, because Dialogue Manager remains available when Godot-native authoring has demonstrated value.
- Good, because in-progress narrative state can be saved without duplicating the world.
- Good, because localization and presentation remain in the Godot layer.
- Bad, because every consequential narrative action needs a typed adapter and failure path.
- Bad, because authors cannot freely mutate arbitrary game variables from scripts.
- Bad, because package selection remains deferred until a concrete branching feature exists.
- Bad, because resuming a conversation after the autonomous world changes requires explicit design.

### Confirmation

A change is in scope when it adds branching narrative, dialogue variables, narrative-to-Core calls, an Ink or Dialogue Manager dependency, narrative save state, or generated conversational text.

Conformance is confirmed by:

- tests that narrative evaluation works against read-only context;
- a finite registry of typed consequences and payload validation;
- failure tests when a consequence becomes illegal before commitment;
- architecture checks preventing narrative packages from becoming Core world repositories;
- build-time compilation or parsing of narrative content;
- save and resume tests for supported in-progress sequences;
- localization tests that preserve stable command and branch identities;
- scenario tests proving unrelated autonomous state is not frozen or overwritten by narrative;
- dependency-admission evidence before selecting a runtime package.

## Pros and Cons of the Options

### Keep Core authoritative, integrate a narrative runtime through typed context and consequence adapters, and prototype Ink first when needed

- Good, because simulation and authored narrative have clear ownership boundaries.
- Good, because Ink can run and test outside Godot.
- Good, because package adoption follows a real authoring requirement.
- Good, because another runtime can replace Ink without changing Core commands.
- Bad, because adapters and context schemas require deliberate maintenance.
- Bad, because authors need explicit support for every new consequence type.

### Adopt Ink immediately as the narrative authority

- Good, because mature branching tools and authoring syntax would be available early.
- Good, because C# integration is engine-independent.
- Bad, because the project does not yet have a feature proving the dependency and workflow.
- Bad, because treating Ink as authority would duplicate world variables and bypass Core validation.
- Bad, because early content could shape architecture before diplomacy and missions are understood.

### Adopt Dialogue Manager immediately as the narrative authority

- Good, because Godot-native editor and UI integration can accelerate dialogue authoring.
- Good, because its stateless design can support external authority.
- Bad, because early adoption adds addon and engine-version coupling.
- Bad, because it is not yet proven that editor integration outweighs headless portability.
- Bad, because making it authoritative would still violate the simulation boundary.

### Build a custom branching narrative language and editor

- Good, because syntax and integration could match the game exactly.
- Good, because no external runtime would be required.
- Bad, because parsing, tooling, diagnostics, branching, persistence, and editor support are substantial generic work.
- Bad, because established narrative edge cases would be rediscovered locally.
- Bad, because the work does not differentiate the simulation.

### Encode conversations directly in C# or Godot scenes

- Good, because no additional language or runtime is required.
- Good, because small linear interactions are straightforward.
- Bad, because branching text becomes difficult to review and localize.
- Bad, because narrative structure becomes mixed with presentation or executable code.
- Bad, because non-programmer and agent authoring workflows are poorer.

### Use an LLM to generate and adjudicate conversations dynamically

- Good, because surface text can be varied and responsive.
- Good, because natural-language input could be supported.
- Bad, because authoritative outcomes become nondeterministic and difficult to test.
- Bad, because hosted models add availability, cost, and privacy dependencies.
- Bad, because setting, safety, and hidden-information constraints are difficult to guarantee.
- Bad, because a validated rules and command layer remains necessary anyway.

## More Information

Narrative is one presentation of the simulation, not the container for it. The same diplomatic offer could be presented as an authored conversation, a concise command panel, or an automated report while producing the same typed Core decision.

The preferred package order records current evidence without installing a dependency prematurely: prototype Ink first for engine-independent branching; compare Dialogue Manager when Godot-native authoring is a material requirement.
