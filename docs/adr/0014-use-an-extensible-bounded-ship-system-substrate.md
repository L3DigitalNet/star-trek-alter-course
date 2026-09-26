---
schema_version: '1.1'
id: 'adr-0014-star-trek-alter-course-use-an-extensible-bounded-ship-system-substrate'
title: 'ADR 0014: Use an Extensible Bounded Ship-System Substrate'
description: 'Requires shared ship-system state and cross-system mechanics to scale by semantic system identity and capabilities without a universal component framework.'
doc_type: 'adr'
status: 'active'
created: '2026-09-26'
updated: '2026-09-26'
reviewed: '2026-09-26'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'simulation'
  - 'validation'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0009-use-layered-testing-and-architecture-conformance.md'
  - 'docs/wiki/engineering-and-combat.md'
supersedes: []
superseded_by: null
source: []
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

# Use an extensible bounded ship-system substrate

## Context and Problem Statement

The first Engineering and combat slices intentionally implemented only the systems needed by concrete gameplay: power generation, sensors, impulse propulsion, shields, and directed-energy weapons. Their behavior is valid, but the representation grew incrementally by adding named fields and per-system branches across Engineering state, power allocation, authored definitions, bootstrap, persistence DTOs, repair validation, combat target lists, projections, player actions, and Godot presentation.

That approach was appropriate while proving the first consumers, but it is not a sustainable architectural boundary. Future systems such as warp propulsion, life support, computers, communications, tractor systems, specialized weapons, or other justified ship capabilities must be addable without redesigning every cross-system mechanism or adding another parallel field and switch in each layer.

This decision governs the representation of **ship systems that participate in shared cross-system mechanics** such as condition, damage, repair, power allocation, persistence, generic projection, and generic presentation. It applies to the five currently implemented systems and to future ship systems when they participate in one or more of those shared mechanics.

It does not require every ship feature to use identical state or behavior. Sensor observation, propulsion limits, shield absorption, weapon firing, and future system-specific rules remain typed domain logic. It does not authorize an ECS, universal component framework, reflection-driven behavior, data-scripted rule engine, generic ability/effect system, or arbitrary user-defined systems whose behavior Core does not understand.

The current five-system implementation partially satisfies this decision through stable semantic `ShipSystemId` values and generic repair/target identities, but its surrounding named-field representation does not yet conform. It also conflates a system's semantic role with the one installed system that currently fulfills that role. That assumption will not survive heterogeneous ships, refits, or multiple installations of one kind. The next behavior-affecting ship-system feature must correct that representation before adding more parallel system-specific plumbing.

How should ship systems expose shared state and capabilities so that adding a future system is an incremental domain change rather than a redesign of Engineering, damage, repair, persistence, projection, and UI?

## Decision Drivers

- A future ship system should require its own identity and domain behavior, not repeated redesign of unrelated cross-system infrastructure.
- Stable semantic identities must remain authoritative across content, saves, commands, projections, and events.
- Common mechanics such as condition, damage, repair, and power accounting should operate from one bounded authoritative representation.
- System-specific behavior must remain explicit and strongly typed rather than becoming data-driven executable logic.
- Deterministic iteration and tie-breaking must not depend on dictionary order, enum declaration order, reflection order, or localized labels.
- Content and save formats must remain strict, bounded, versioned, and migratable.
- Player and AI information boundaries must remain explicit; a generic system collection is not permission to expose hidden ship state.
- Godot should be able to render generic Engineering rows and actions without becoming the authority for system rules.
- The architecture should be extensible without introducing infrastructure solely for hypothetical scale.

## Considered Options

- Use a bounded semantic ship-system substrate for shared state and capabilities, with typed system-specific behavior.
- Continue adding named fields, switches, DTO properties, and UI actions for every new system.
- Replace ship systems with a universal ECS/component or generic behavior framework.

## Decision Outcome

Chosen option: "Use a bounded semantic ship-system substrate for shared state and capabilities, with typed system-specific behavior", because it removes repeated cross-system redesign while preserving the simulation-first, strongly typed architecture.

This decision governs the common representation and cross-system mechanics of ship systems. It does not make all ship systems behaviorally interchangeable.

### Three distinct system identities

The architecture distinguishes three meanings that the current one-instance-per-kind implementation partially conflates:

1. **System kind identity** — a stable semantic role such as power generation, sensors, impulse propulsion, shields, or directed-energy weapons. The current `ShipSystemId` values already express this meaning and must either become the explicit kind identity or migrate losslessly to an equivalently stable `ShipSystemKindId`.
2. **System definition identity** — a stable authored-content identity for a reusable component/model. Two installed systems of the same kind may reference different definitions with different supported characteristics.
3. **Installed system instance identity** — a stable persistent identity for one actual installed component on one ship. It is distinct from both kind and definition and survives damage, repair, save/load, refit decisions, and other live-state changes until that installation is removed.

A ship may legally have zero, one, or multiple installed instances of a kind when that kind's typed domain rules permit it. Common infrastructure must not assume exactly one sensor, one weapon, one generator, or one installation of any future kind merely because the first five-system proof did.

Initial bootstrap must assign installed-system identity deterministically from authored loadout data rather than incidental array, dictionary, reflection, or file traversal order. Runtime installation allocates a new stable identity through an explicit bounded allocator/identity rule. Removed identities are not silently reused in a way that can retarget persisted or queued references.

Numeric enum ordinals, localized labels, field position, dictionary iteration order, and UI control identity must never become persistent or authoritative system identity.

Adding a new production system kind may require declaring its semantic kind and implementing its actual domain behavior. Adding another definition or another installed instance of an existing kind must not require redesigning existing condition, repair, power allocation, persistence, generic projection, or generic Engineering actions.

### Reusable system definitions and authored ship loadouts

Reusable system-component definitions are ordinary strict versioned content keyed by **system definition identity**. Each definition declares its system kind plus only the common metadata and typed specialized data needed by admitted gameplay.

A ship design/class owns an explicit bounded deterministic **initial loadout** that references reusable system definitions. The design describes how a newly bootstrapped ship starts; it is not the live authority for what a particular ship still has installed later.

A live ship instance owns its actual bounded installed-system collection. Two ships with the same ship-definition identity may therefore diverge after bootstrap through legitimate damage, refit, replacement, removal, installation, or future typed modification mechanics. Loading or projecting a live ship must never reconstruct its current loadout by blindly reapplying the class default.

The common authored metadata may include:

- whether the system has condition and can receive damage;
- whether it is repairable and its authored repair parameters;
- whether it consumes allocatable power and its nominal demand;
- deterministic ordering metadata required by shared allocation, remainder, or presentation rules; and
- other future cross-system attributes admitted by a concrete consumer.

The common metadata and each ship design's initial loadout have explicit finite bounds. The exact bounds are implementation choices justified by supported save/content shapes and tests; they are not permission for unbounded dynamic systems.

System-specific authored data remains in focused typed definitions where it belongs. For example, weapon range, damage, cooldown, sensor range, propulsion performance, or future specialized behavior must not be flattened into an untyped property bag merely because the system also participates in common condition or power mechanics.

### Bounded runtime installed-system state

Authoritative runtime state stores common values in a bounded ordered collection of **installed system instances**, keyed by installed-system identity rather than one property per system kind.

Each installed entry identifies its system definition and therefore its kind. At minimum, installations that participate in damage expose current `SystemCondition`; installations that consume allocatable power expose current allocation through the shared power representation. Runtime values belong to the installation, not to the immutable class definition.

A system that does not support a capability does not receive a meaningless placeholder value merely to make every row identical. Capability support is determined from the authored definition and validated state.

Shared queries and transitions such as:

- get or replace condition;
- enumerate legal damage targets;
- determine repair support and timing;
- calculate available and allocated power;
- generate balanced or priority allocations;
- reconcile forced brownout;
- project generic Engineering state; and
- validate common system invariants

operate against the canonical system definition/state collections rather than hard-coded lists of known systems.

### Typed system-specific behavior

The substrate owns shared representation, not all behavior.

Specific domain rules remain explicit typed code. They decide cardinality and aggregation when more than one installed instance of a kind exists. The common substrate does not guess whether two generators add output, two shield installations combine, one propulsion unit is primary, or multiple weapon emitters fire together.

Examples include:

- sensor capability affecting observation and scan continuity;
- impulse capability limiting tactical speed;
- shield condition and power participating in absorption;
- directed-energy capability participating in firing;
- future warp, communications, life-support, tractor, or specialized weapon rules.

Those rules may query installed systems by semantic kind and operate on stable installed-system identities plus focused typed definitions. Specialized runtime state that is not common to every system may remain in a focused typed aggregate keyed by installed-system identity; it must not be forced into an untyped generic property bag. The rules must not require every system to inherit one behavior interface, register arbitrary callbacks, or execute rules declared in JSON.

A new system kind therefore has two deliberate integration surfaces:

1. opt into the common capabilities it actually uses; and
2. add focused domain behavior only where that system creates new gameplay, including any allowed cardinality and multi-instance aggregation rule.

A new definition or installed instance of an already supported kind normally uses those existing rules without a new cross-cutting implementation branch.

### Runtime installation, removal, replacement, and modification

The substrate must permit future gameplay to change a ship's installed systems without replacing the ship or mutating its class definition.

Installation, removal, replacement, and modification are explicit validated Core transitions. They are atomic with respect to common state and any system-specific consequences. A transition must reconcile or reject, as appropriate:

- power allocation involving the affected installation;
- active repair targeting that installation;
- scheduled work or exact correlations owned by that installation;
- tactical capability that would become illegal after removal/change;
- system-specific cooldown/state that cannot survive replacement;
- persistence identity and allocators; and
- actor-safe projections and knowledge.

There is no direct mutable-list escape hatch that presentation or AI may use to bypass those transitions.

A refit may replace an installation's definition, remove it, add another installation, or later apply a bounded typed modification model admitted by a concrete consumer. Immutable authored definitions are never edited in place to represent one ship's upgrade. Per-instance modifications, when introduced, use explicit typed/versioned state or content references rather than an arbitrary property bag.

The first ADR-conformance refactor need not expose player refit gameplay. It **must** establish a runtime model that can represent heterogeneous current loadouts and preserve them through save/load so later refit gameplay does not require another state-model replacement.

### Deterministic ordering

Any common mechanic whose result depends on iteration order uses an explicit stable order from the ship-system definition contract.

Balanced allocation, priority fallback, brownout remainder distribution, generic system projection, and similar mechanics must not derive ordering from:

- hash/dictionary enumeration;
- enum declaration order;
- object construction order;
- reflection;
- source-file ordering outside the explicit content contract; or
- display labels.

Changing the authored semantic order is a rules/content change and receives the same validation and compatibility treatment as other consequential definition changes.

### Generic damage and repair participation

Damage targeting and repair availability derive from installed system definitions and runtime state.

Combat must not maintain a second manually curated list of damageable system kinds when the canonical installed-system set already knows which installations have condition and are legal damage targets.

Repair remains one bounded ship-level operation unless a later concrete design changes that rule. The repair target is an installed-system identity; its kind and authored repair parameters are resolved from that installation's definition rather than a switch that names every repairable kind.

System-specific consequences of damage remain explicit. A generic condition transition does not replace forced propulsion reconciliation, scan interruption, shield behavior, weapon behavior, or other domain-specific effects.

### Extensible power consumers without another power model

Power generation is derived from the installed generation-capable systems according to the typed generation aggregation rule. Allocatable consumers are the installed systems that declare a positive power demand.

The authoritative allocation model must be able to represent the bounded set of installed consumers by installed-system identity. Adding another power-consuming system must not require adding another property to `PowerAllocation`, another preset enum member, another save field, and another projection field merely to participate in ordinary allocation accounting.

Generic allocation commands may target an installed-system identity for priority behavior. Specialized power rules remain possible where a concrete future system requires them.

### Actor knowledge and external targeting

Live installed-system truth is part of the owning ship's authoritative state. The existence of a generic system collection does not make another actor omniscient about a target's current loadout, refits, damage, or installation identities.

Player-facing and AI-facing information about another ship's systems must come from legitimate observer knowledge. A remote targeting command must never require presentation to submit a hidden true installed-system identity that the observer has not learned. A bounded first implementation may continue to target a known semantic system kind where M6A already permits that behavior, but future exact-installation targeting requires an observer-safe known-system identity/correlation rule.

System heterogeneity must therefore fail closed at the knowledge boundary: adding or removing a target's hidden installation cannot silently appear in another actor's projection unless an existing or future information rule legitimately reveals that fact.

### Persistence and migration

Frozen historical save models remain frozen.

The first current save version that adopts this ADR stores each ship's actual installed-system set, stable installation identities, definition references, common runtime state, and any required installation allocator/continuation in a bounded representation instead of adding another named condition/allocation property for each new system. Adjacent migration from the preceding field-oriented format creates exactly one installed instance for each of the five represented historical kinds, maps each historical condition/allocation/repair to that deterministic installation, and invents no additional installations, damage, repairs, allocation, capability, refit history, or modifications.

Persistence continues to store only authoritative values required for deterministic continuation. Derived capability, presentation rows, indexes, and generic lookup caches remain reconstructible.

Unknown, duplicate, unsupported, missing-required, or capability-inconsistent system entries fail validation according to the current version's explicit contract. Silent member dropping or best-effort reinterpretation is not extensibility.

### Projection and presentation

Core exposes a bounded ordered projection of the player's **currently installed** systems containing only actor-safe common facts needed by presentation and generic controls.

Godot may use that projection to render Engineering system rows, condition, allocation, repair availability, and generic repair/power actions without a new hard-coded control path for every system.

Specialized panels remain appropriate for specialized behavior. For example, weapon fire controls or sensor scan controls need not become generic system actions.

Player commands that operate on a common own-ship mechanic carry installed-system identity rather than using one action enum member per concrete system kind. Godot submits intent; Core remains authoritative for support, legality, consequences, ordering, and reasons.

### Transition of the existing five systems

The current M6A behavior remains valid, but the five-system field-oriented representation is an interim implementation.

Before implementing the next behavior-affecting ship-system expansion—including Damage Control and Recovery—or before adding any sixth system, the repository must bring the existing five systems into conformance with this ADR.

That transition must:

- preserve existing M6A gameplay semantics unless a separately approved design decision changes them;
- preserve the stable semantic meaning of the five current `ShipSystemId` values as system kinds, whether the type is retained or renamed;
- introduce distinct stable system-definition and installed-system-instance identities;
- represent each existing ship's five current system kinds as explicit installed instances, with absence supported for ship designs that legitimately omit a capability;
- move system-derived authored values out of ship-wide parallel fields where needed so a live replacement can actually change the owning ship's capability;
- migrate all five current conditions into the common installed-system runtime representation;
- migrate the four current power consumers into the common per-installation allocation representation;
- derive damage targets and repair support from the actual installed-system set and canonical system definitions;
- remove per-system repair/action plumbing where a generic system-targeted command is sufficient;
- evolve the current content/save formats through ordinary versioned schema changes and adjacent non-inventive migration, preserving each ship's actual loadout independently of its class default;
- retain specialized sensor, propulsion, shield, and weapon rules as typed domain behavior;
- preserve actor-knowledge and Core/Godot authority boundaries; and
- add conformance tests proving common mechanisms do not require one branch per concrete system.

Do not add a sixth parallel condition field, allocation field, repair-duration branch, damage-target list entry, persistence property, or UI repair action as a shortcut around this transition. Do not encode a permanent one-instance-per-kind assumption into the replacement substrate.

### Consequences

- Good, because new systems and heterogeneous/refitted loadouts can reuse condition, damage, repair, power, persistence, projection, and presentation infrastructure.
- Good, because shared mechanics have one authoritative representation instead of several synchronized named-field lists.
- Good, because specialized gameplay remains explicit and testable rather than becoming a generic component engine.
- Good, because content and saves retain stable semantic identities and deterministic ordering.
- Good, because the next damage-control slice can exercise the architecture against all current systems before the catalog grows further.
- Bad, because the current five-system implementation requires a deliberate behavior-preserving refactor before deeper recovery work.
- Bad, because keyed collections and capability validation are more involved than direct record fields for a very small fixed system set.
- Bad, because specialized code still needs explicit integration when a genuinely new system creates new gameplay; this ADR removes repeated plumbing, not domain work.

### Confirmation

A change is in scope when it adds or changes a ship system that participates in common condition, damage, repair, power, persistence, generic projection, or Engineering presentation.

Conformance requires evidence that:

- common system state is owned by stable installed-system identities that resolve to stable definitions and semantic kinds rather than added as another named field;
- shared mechanics derive support and ordering from the canonical definitions/state;
- another definition or installed instance of an existing kind does not require edits to common algorithms, and a new common-capability kind does not require redesigning unrelated condition, repair, allocation, persistence, and generic UI algorithms;
- specialized behavior remains explicit and typed;
- deterministic ordering is defined independently of unordered collections or enum ordinals;
- content and save shapes remain bounded and strictly validated;
- historical migrations remain adjacent and non-inventive;
- Core/Godot and actor-knowledge boundaries remain intact; and
- architecture/regression tests fail when a concrete-system branch is reintroduced into a generic cross-system mechanism without justification.

A code review that finds the same set of system kinds independently repeated across common state, common repair, common allocation, common persistence, and generic presentation—or that finds class identity being treated as the live loadout authority—should treat that duplication as architectural drift under this ADR.

## Pros and Cons of the Options

### Use a bounded semantic ship-system substrate for shared state and capabilities, with typed system-specific behavior

- Good, because it makes cross-system mechanics extensible while preserving domain-specific rules.
- Good, because stable semantic IDs remain valid across content, saves, commands, and UI.
- Good, because bounded collections fit the current deterministic and persistence architecture.
- Good, because it admits only abstractions with multiple concrete consumers: the five existing systems already supply them.
- Bad, because it requires a near-term migration of existing Engineering state and persistence.

### Continue adding named fields, switches, DTO properties, and UI actions for every new system

- Good, because each individual addition is initially straightforward.
- Good, because fields are strongly typed and easy to discover at very small scale.
- Bad, because every new system touches many unrelated layers.
- Bad, because parallel lists and switches can drift and create inconsistent support.
- Bad, because persistence and UI become increasingly expensive to evolve.
- Bad, because adding future systems eventually requires a redesign under greater compatibility pressure.

### Replace ship systems with a universal ECS/component or generic behavior framework

- Good, because arbitrary composition could be highly flexible.
- Good, because many behaviors could share one generalized dispatch mechanism.
- Bad, because the project has no demonstrated need for arbitrary runtime composition.
- Bad, because generic behavior dispatch would obscure simulation rules and weaken explainability.
- Bad, because it would add substantial infrastructure and migration complexity.
- Bad, because it conflicts with the project's demand-driven architecture and preference for strong domain boundaries.

## More Information

This ADR does not select the gameplay semantics of post-M6A Damage Control and Recovery. Repair meaning, repair rate, reprioritization consequences, autonomous damage-control doctrine, and operational recovery goals still require their own bounded design refinement.

The ADR deliberately accepts a middle ground: **genericize the common substrate, not the game rules**. A future system should be cheap to add to shared Engineering mechanics, but its actual consequences must still be modeled according to the domain rather than inherited from a universal component abstraction.

The current five systems provide enough concrete evidence to justify this abstraction now. The requirement was explicitly selected by the project owner during the September 26, 2026 post-M6A damage-control design pass after auditing the field-oriented M6A representation.
