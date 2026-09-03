# Star Trek: Alter Course Development Roadmap

## Purpose

This roadmap defines the near- and mid-term development direction for **Star Trek: Alter Course (ST:AC)** after the v0.1.0 gameplay walking skeleton. It is intended to keep successive feature slices moving toward the same game: a persistent, systems-driven Star Trek command simulation in which the player commands one starship inside a universe that was already active before the player arrived and continues to change whether or not the player is present.

The roadmap is deliberately more concrete than a list of desired systems and deliberately less prescriptive than an implementation specification. Each milestone is expected to become a separately governed issue or feature specification before coding begins. That refinement step may change internal design, divide a milestone, reorder bounded work, or narrow acceptance criteria when new evidence from the codebase makes that appropriate.

The architecture decisions in [`docs/adr/`](docs/adr/) remain authoritative. If this roadmap conflicts with an active ADR, the ADR wins. If a future milestone reveals that an ADR should change, that architectural decision must be made explicitly rather than being smuggled into feature implementation.

## Planning horizon and execution model

The milestones below describe substantial development slices rather than small tickets. As a planning target, each should contain enough coherent work for an orchestrated implementation effort with roughly **6–8 parallel specialist workstreams** and several hours of focused implementation, integration, testing, and review. That is a sizing heuristic, not a requirement to keep work artificially large. A milestone should be split when discovery shows that its risks cannot be safely resolved together.

Each implementation milestone should normally include:

1. a current review of the active ADRs and affected code paths;
2. a refined governing issue/specification with explicit in-scope and out-of-scope behavior;
3. domain implementation in `AlterCourse.Core` where simulation authority is involved;
4. persistence and migration work for any new durable state;
5. Godot projection/input work only for the player-visible portion of the slice;
6. focused unit, negative, persistence, scenario, and architecture tests at the lowest appropriate layer;
7. at least one playable or headless proof that demonstrates why the architecture exists;
8. canonical verification and the repository's normal governed pull-request workflow.

The goal is not to predict the final shape of every future subsystem. The goal is to establish the important **joints between domains** early enough that new systems can be added without repeatedly redesigning the simulation around assumptions made for the player ship or the current screen.

---

## Strategic direction

### The universe is not player-centric

From the next major gameplay work onward, ST:AC should be designed under the invariant that the player ship is one actor inside a larger world, not the object around which reality is created.

The universe should be able to contain ships that are already traveling, repairing, patrolling, surveying, transporting, waiting, investigating, or pursuing other orders when a campaign begins. Those ships continue to act while the player travels elsewhere, waits, repairs, communicates, or never encounters them at all.

A useful recurring question for every major simulation feature is:

> **What happens if the player never interacts with this?**

If the answer is that the system never progresses, never has consequences, or only exists when the player enters a scene, the feature probably does not yet fit the intended world model.

### Prefer risk-first vertical slices over subsystem construction

The roadmap intentionally does **not** say to finish sensors, then finish propulsion, then finish shields, then finish factions. That approach could create individually elaborate systems whose relationships were never proven.

Instead, each milestone should introduce one important architectural dimension and make it interact with capabilities that already exist. Examples:

- do not merely add another ship; add a ship that already has an order, travels independently, persists, and can later be observed;
- do not merely add sensors; make sensors mediate the difference between world truth and what an actor knows;
- do not merely add power; make power allocation change the capability of propulsion and sensors;
- do not merely add a faction record; make that faction's ship pursue a strategic objective while the player is elsewhere;
- do not merely add shields; make shield capability depend on power, condition, tactical geometry, and damage;
- do not merely add reputation; make a remembered incident alter a later decision.

This keeps architecture grounded in gameplay while exposing bad assumptions early.

### Define the joints, not the final catalog

The project does not need to define every possible ship system, faction rule, diplomatic action, weapon, mission, or canonical event before development can continue.

Early engineering work should prove that systems can have stable identity, runtime condition, resource relationships, capabilities, persistence, and domain-specific behavior. It should **not** create a universal interface containing every property that any future system might need.

Likewise, the first multi-actor work should justify plural ships and stable actor identity, not a universal entity hierarchy or ECS. The first autonomous behavior should justify goals, information, candidate actions, deterministic choice, and commands, not a general behavior-tree framework. New abstractions should appear because two or more concrete consumers need them, not because the project can imagine hypothetical future scale.

### World truth, knowledge, and history are different things

The persistent simulation will ultimately need to distinguish at least three concepts:

1. **World truth** — what actually exists and what actually happened.
2. **Actor knowledge** — what a particular player, ship, faction, or other actor currently believes or has observed, including uncertainty and stale information.
3. **Durable historical meaning** — facts or interpreted incidents the simulation must remember because they can influence later rules and decisions.

Diagnostic logs are not a substitute for any of these. If a later simulation decision depends on a prior event, the relevant memory of that event belongs in authoritative domain state and save data.

### Offscreen simulation is first-class, but not uniformly high frequency

A living universe must not require every ship to run tactical updates every 100 ms.

The simulation should use the coarsest resolution that preserves intended behavior. A strategic voyage may be represented by an origin, destination, departure, arrival, order, and scheduled consequences. A tactical encounter may need continuous positions and small fixed steps. Strategic AI may wake only at meaningful decision boundaries.

This is how the project can eventually support many autonomous actors without turning persistence into continuous frame-by-frame polling.

### Canon supplies history; simulation supplies present activity and future divergence

ST:AC does not need Dwarf Fortress-style generation of centuries of fictional prehistory because Star Trek already supplies the broad historical and political context for a chosen starting era.

The useful analogue is to treat canon as a set of **starting boundary conditions** and then populate the enormous amount of activity canon does not specify: which patrols are underway, which freighters are late, which science ship is mid-survey, which station expects a shipment, which vessel is returning for repairs, and which local commanders are already pursuing orders.

A later milestone will explicitly define how canonical future events interact with an already-running simulation. Until that policy is refined, the roadmap's preferred direction is that canon strongly establishes initial conditions and pressures without silently forcing the universe to erase legitimate simulation divergence.

---

## Milestone overview

| Order | Milestone | Primary architectural proof | Representative player-visible proof |
| --- | --- | --- | --- |
| 1 | **World State and Bootstrap Generalization** | The simulation can own multiple persistent ships and initialize world state without treating the player ship as the world root. | A campaign loads a world containing the player plus independently identified NPC ships and valid scheduled work. |
| 2 | **Active World and Persistent Orders** | NPC actors can already be doing meaningful work, progress offscreen, and retain intentions through save/load. | At game start at least one NPC is already in transit and later completes its order without player involvement. |
| 3 | **Sensor Knowledge and First Contact** | World truth is distinct from actor knowledge, and autonomous ships can make limited explainable decisions from what they know. | The player detects, scans, hails, approaches, avoids, or leaves an NPC whose identity and intent are initially uncertain. |
| 4 | **Engineering Backbone and Degraded Operations** | Ship systems can interact through condition, power, capability, damage, and repair without defining the final system catalog. | The player redistributes limited power between propulsion and sensors while managing degraded equipment and an ongoing repair. |
| 5 | **Living Sector and Faction Autonomy** | Multiple actors and factions can pursue goals and create consequential offscreen changes over strategic time. | A small sector changes while the player is elsewhere: patrols move, objectives progress, and NPC interactions alter later circumstances. |
| 6 | **Tactical Combat Foundation** | Combat composes existing motion, knowledge, power, system condition, AI, and persistence rather than introducing a parallel hit-point game. | A small multi-ship engagement supports maneuver, targeting, shields, weapons, subsystem damage, repair pressure, and withdrawal. |
| 7 | **Diplomacy, Incidents, and Durable Consequences** | The world can remember interpreted events and use them in later relationships and decisions. | Assistance, threats, treaty violations, restraint, or attacks affect how ships or factions respond later. |
| 8 | **Canon-Anchored Campaign Bootstrap and Divergent History** | A campaign can begin inside a canon-consistent but already active generated world and then evolve according to simulation results. | Starting the same epoch with a known seed creates reproducible in-progress activity; changing the seed changes non-canonical local circumstances. |
| 9 | **Persistent Regional Campaign Integration** | The preceding systems form one durable gameplay loop that remains stable over extended simulation time. | The player can operate through travel, contact, engineering, autonomous faction activity, combat or avoidance, and lasting consequences across a small region. |

The ordering expresses dependency and architectural risk, not an immutable release schedule. A future implementation discussion may combine or split adjacent milestones when the codebase supplies better evidence.

---

## Milestone 1 — World State and Bootstrap Generalization

### Implementation outcome

Feature #36 implements this milestone through Final PR #37. The authoritative aggregate now owns canonically ordered ordinary ship state plus an explicit `PlayerShipId`; strategic state and scheduled ship consequences belong to a specific ship. A typed `GameBootstrap` accepts declared `ShipStart` values, and the first setup is a thin producer of a three-ship proof world: USS Pathfinder is the player ship, USS Wayfarer repairs independently, and USS Horizon begins in strategic transit.

Ship definitions use schema V2 and contain reusable design capability, while bootstrap owns vessel names and starting sensor condition. V2 saves persist the plural world and ship-targeted scheduler state, and the deterministic pre-1.0 V1 migration reconstructs the one representable ship without creating a permanent compatibility promise. The headless signature scenario proves simultaneous movement, repairs, and travel; target isolation; save/load continuation; and insertion-order independence.

The current 256-ship admission limit bounds untrusted bootstrap/save input and fixed-step work at the present prototype scale. It is a safety limit, not a claim about final universe capacity, and should change only with profiling and a concrete scale requirement. The implementation deliberately adds no NPC order, autonomous decision, actor/entity hierarchy, ECS, or event bus.

### Goal

Evolve the v0.1.0 single-player-ship walking skeleton into a world model that can contain multiple persistent ship instances without prematurely inventing a universal entity architecture.

This milestone should pressure the assumptions currently embodied by player-specific state, player-specific strategic state, one ship definition, and scheduled work that only needs to distinguish the walking-skeleton event kinds.

### Architectural question

> Can the simulation represent several durable ships, their identities, positions or strategic activity, scheduled consequences, and save state while the player is simply identified as the ship currently under human command?

### Gameplay proof

A campaign starts with the player ship plus a small number of NPC ship instances. They are all valid world state with stable IDs and definitions. The player remains the only directly controlled ship, but NPC ships are no longer special-case presentation objects or encounter-local spawn data.

The proof does not require sophisticated NPC behavior yet.

### Candidate workstreams

#### 1. Plural ship ownership

Evolve authoritative state toward a collection keyed by stable ship instance identity plus an explicit player-ship identity. The exact type and internal organization should follow current code evidence rather than a generic entity framework.

Important properties:

- each ship instance has one stable runtime identity;
- each instance references immutable authored definition identity separately from runtime state;
- the player ship is selected by identity rather than stored as the only ship-shaped field in the universe;
- validators detect duplicates, missing definitions, broken player references, and invalid spatial state;
- code does not add `EnemyShip`, `FriendlyShip`, `SecondShip`, or similar cardinality-specific roots.

#### 2. Definition, instance, and starting-condition separation

Begin removing walking-skeleton scenario assumptions from reusable ship definitions.

The target conceptual boundary is:

- **ship definition** — what the design/class is capable of;
- **ship instance state** — current condition, location, movement, system state, orders, and other mutable facts;
- **scenario/bootstrap state** — where this particular ship starts and which conditions or activities are initially in progress.

Only move fields when a concrete new consumer proves the distinction. Do not attempt to design every future ship-definition section in this milestone.

#### 3. Targetable scheduled work

Extend scheduled work only as far as multiple actors require. The scheduler should be able to identify the intended target/owner and carry the bounded data needed to resolve a known future consequence safely.

The design should retain ADR 0007's data-only, serializable, deterministic event semantics. Avoid delegates, generic workflow engines, event buses, reflection-dispatched arbitrary payloads, or a giant universal event hierarchy.

#### 4. World/scenario bootstrap boundary

Replace growth of `FirstGameSetup` as a hardcoded universe constructor with a narrow bootstrap responsibility that can create a valid initial world from declared scenario inputs and content.

The first bootstrap format may remain intentionally small. It needs to support only the current playable start plus the few NPC instances required by the milestone.

It should make room for later inputs such as:

- campaign/canon epoch;
- starting region;
- required locations;
- player starting ship and condition;
- NPC starting ships and state;
- initial scheduled activities;
- deterministic generation seed or generation constraints.

Do not build a mission DSL, narrative language, procedural galaxy framework, or full scenario editor.

#### 5. Persistence evolution

Extend the explicit snapshot mapping so all authoritative ship instances and new scheduler targeting survive save/load. Preserve candidate-load validation and deterministic continuation.

If the save schema changes, use the existing version/migration policy rather than silently changing serialized meaning.

#### 6. Core projections and Godot adaptation

Keep the player-facing projection narrow. Godot does not need unrestricted access to all world truth merely because Core now stores it. Expose only the minimum new information needed to prove multiple actors exist and to prepare for the next contact milestone.

#### 7. Tests and architecture protection

Add coverage for:

- multiple unique ship instances;
- invalid duplicate/missing identities;
- targeted scheduled work;
- save/load of plural actors;
- deterministic continuation;
- restoration/bootstrap rejection of missing or orphaned scheduled-work targets;
- insertion-order independence where applicable;
- preservation of the Core/Godot dependency boundary.

### Acceptance themes

The milestone is successful when:

- the world can contain multiple ships without making them player-specific fields;
- the player ship is an identified member of that world;
- all consequential multi-ship state persists;
- scheduled work can safely address the actor it belongs to;
- bootstrap creates the initial world without turning reusable ship definitions into scenario records;
- the existing walking-skeleton player loop still works;
- no general entity/ECS/event-bus framework was introduced solely to obtain plural ships.

### Deliberately deferred

- faction strategy;
- sensor uncertainty;
- detailed NPC AI;
- combat;
- a complete ship-system architecture;
- arbitrary actor types;
- procedural galaxy generation;
- a general scenario scripting system.

### Resolved refinement decisions

- Use plural ordinary `ShipState` values with stable `ShipInstanceId` values and one explicit `PlayerShipId`; do not introduce a general actor abstraction.
- Keep maximum tactical speed and sensor-repair duration on the reusable definition. Move vessel display name and initial sensor integrity to typed starting state.
- Give every ship-affecting scheduled item a concrete target ship ID; keep the existing finite kind-based consequence model.
- Use typed Core bootstrap input for this milestone. Authored scenario JSON and a scenario language remain unjustified.
- Write V2 saves and retain one deterministic V1-to-V2 migration during pre-1.0 development. Future pre-1.0 compatibility remains an explicit per-version decision.

---

## Milestone 2 — Active World and Persistent Orders

### Implementation outcome

Feature #38 implements this milestone through Final PR #39. Ordinary `ShipState` values can own one durable `ShipOrder` with stable identity: one-shot `TravelTo`, bounded cyclic `PatrolRoute`, or time-only `HoldUntil`. Orders explain why a ship acts, strategic state remains the physical truth, and focused scheduled work defines the next deterministic boundary. Player travel and autonomous patrol progression share one targetable Core travel command, while exact order cancellation preserves an in-progress physical journey and removes only correlated hold work.

The scheduler now jumps directly across strategic-only intervals, materializes repair state analytically at meaningful boundaries, and retains 100 ms integration for ships with active local tactical motion. Per-advancement ship-step and consequence budgets bound actual work and preserve candidate-before-commit atomicity. Player-facing advance-until semantics process hidden NPC activity offscreen but stop and report only for work targeting `PlayerShipId` until Milestone 3 defines actor knowledge.

Typed bootstrap declarations construct a 03:00 proof world in which USS Sentinel has patrolled since 00:00 toward a 06:00 arrival and USS Vigilant independently holds until 09:00. Save schema V3 persists order state and the order allocator; explicit V1-to-V2-to-V3 and V2-to-V3 migrations validate the historical `first-playable-v1` rules identity at their source schema, then write the current `active-world-orders-v1` identity and never infer an order from old travel state. Headless tests cover 72 simulated hours, save/load continuation, chunking, cancellation, insertion order, and hidden-event isolation without adding faction AI, pathfinding, randomness, a general entity hierarchy, ECS, or an event bus.

### Goal

Prove that the universe is already in motion when the player begins and that autonomous activity progresses without requiring the player to enter a scene or observe it.

Milestone 1 can bootstrap an NPC already in transit and resolve that scheduled travel independently, but it does not represent why the NPC is traveling or choose what it does next. Milestone 2 adds that durable intent and autonomous decision boundary rather than re-proving plural ship storage.

This is the first direct implementation of the project's "lived-in universe" invariant.

### Architectural question

> Can an NPC ship possess a durable reason for what it is doing, carry out that activity on strategic simulation time, and continue correctly while the player ignores it?

### Gameplay proof

At campaign start, at least one NPC ship is already partway through a strategic journey under an existing order. Another NPC may be waiting for or executing a different scheduled activity. Advancing time causes their activities to progress and complete even if the player remains stationary or travels elsewhere.

Saving and loading in the middle of those activities produces the same later outcome as uninterrupted simulation.

### Candidate workstreams

#### 1. Minimal persistent order/objective model

Represent enough intent to answer why an NPC ship is currently acting.

Examples of initially sufficient intent:

- travel to a destination;
- patrol a bounded route;
- survey a location;
- return to base;
- hold position until a declared condition/time.

Do not design the final mission system. The important property is that the actor's current activity has durable domain meaning and does not exist only as a pathing callback.

#### 2. Strategic activity execution

Allow NPC strategic activity to schedule meaningful boundaries rather than receive tactical-frequency updates. Travel should reuse or generalize the existing strategic travel semantics where practical.

NPC activity should be able to:

- begin from an already-in-progress state at campaign start;
- reach a scheduled completion boundary;
- update authoritative location/activity state;
- request a next decision or follow-on order through explicit Core behavior.

#### 3. Offscreen progression

Create a headless scenario in which player and NPC paths do not intersect and verify that NPC progress still occurs.

The player UI should not be the trigger that activates or advances the actor.

#### 4. Initial world activity

Extend bootstrap data just enough to express current in-progress NPC activity. Initially this may be directly authored or deterministically constructed; the later campaign-bootstrap milestone will generalize generation.

#### 5. Persistence and resumption

Persist order/activity identity and any consequential progress needed for deterministic continuation. Do not persist caches that can be reconstructed from authoritative order and time state.

#### 6. Minimal observability

Emit structured diagnostics or explanation data sufficient to trace:

- which actor owns an order;
- when it began or is considered active;
- which scheduled work advances/completes it;
- why the next activity was selected if a decision occurs.

Logs remain diagnostic; required ongoing activity remains domain state.

#### 7. Long-horizon deterministic tests

Create small scenarios that advance several hours or days and assert:

- orders complete in stable order;
- actors do not freeze when unobserved;
- save/load is equivalent to uninterrupted advancement;
- no zero-time scheduling loop appears;
- NPC movement does not depend on Godot frame cadence;
- removing/canceling an order leaves no invalid scheduled residue.

### Acceptance themes

The milestone is successful when a new campaign can truthfully say that other ships were already doing things before the player became relevant to them.

A strong acceptance scenario is:

1. start the campaign with NPC A already in transit;
2. never display or interact with NPC A;
3. advance past its arrival and next activity boundary;
4. confirm that authoritative world state changed appropriately;
5. repeat with a save/load in the middle and obtain the same semantic result.

### Deliberately deferred

- deep mission generation;
- faction-level assignment logic;
- economy/logistics simulation;
- tactical encounter AI;
- actor knowledge/fog of war beyond what the next milestone requires;
- hundreds of active ships.

### Refinement questions before implementation

- Is "order", "assignment", "objective", or another term the narrowest useful domain concept for the first consumer?
- What activity completion actually requires a new AI decision versus a deterministic next leg?
- How should an in-progress activity be represented at bootstrap without duplicating scheduler authority?
- Which offscreen behaviors can be event-driven immediately and which genuinely need elapsed-time integration?

---

## Milestone 3 — Sensor Knowledge and First Contact

### Implementation outcome — Milestone 3A: First Observed Contact

Feature #58 implements the first governed slice of this milestone. It does **not** mark Milestone 3 complete.

Each `ShipState` now owns bounded sensor knowledge with observer-local `SensorContactId` values. The aggregate retains target-ship correlation only for Core rule resolution; player projections and autonomous decision input receive actor-safe contact snapshots without the target's ship ID. Local passive observation applies only to distinct ships at the same strategic location. Effective range is the authored passive sensor range multiplied by the observing ship's sensor integrity. Contacts are observed as Current, become Stale when no longer detectable, become Lost after a fixed retention interval, and can be reacquired with their existing observer-local ID and learned identity.

The slice adds one active scan and one hail seam. A player can scan a current detected contact to learn its vessel and design display names, then hail an identified current contact. The proof vessel's persisted `CautiousContact` posture receives only its own contact snapshot. Its pure explainable policy chooses Hold, Approach, or Withdraw deterministically; the proof path withdraws from an unidentified contact and holds after a valid identified hail. The resulting motion uses the same validated targetable tactical-course command as player course input.

Contact-sensitive local advancement reuses the Core's 100 ms tactical grid, while inactive strategic simulation remains event-driven. Contact loss, scan completion, and autonomous decision wakes use appended scheduled-work kinds with exact correlations, stable ordering, and revalidation at resolution. Player advance-until remains silent about hidden NPC work but may stop for a player-safe contact or scan event.

Save schema V4 persists sensor knowledge, active scans, autonomous contact posture, and new scheduler correlations under `sensor-knowledge-first-contact-v1`. V3-to-V4 migration deliberately creates empty knowledge, a next contact ID of 1, no active scan, no posture, and no decision wake, preserving historical behavior. Ship-definition schema V3 adds explicit passive sensor range and active-scan duration. The four-ship Dawn Anchor proof world gives damaged USS Pathfinder and full-integrity Survey Vessel Kestrel different knowledge at the same time, then proves scan, hail, autonomous response, and save/load continuation.

The slice deliberately defers affiliation and intent knowledge, scalar confidence or measurement error, strategic contacts, cloaking and electronic warfare, NPC scanning, additional doctrines, faction AI, dialogue, sensor power, combat, and generalized encounter generation. See [First Observed Contact](docs/design/first-observed-contact.md) for the implemented boundary and rules.

### Goal

Make incomplete information a real simulation boundary and create the first autonomous multi-ship encounter that does not require combat.

This milestone should establish that the same world truth can produce different knowledge for different actors and that AI decisions consume actor-appropriate information rather than unrestricted truth.

### Architectural question

> Can the player and an NPC observe one another imperfectly, update their own knowledge over time, make explainable decisions from that knowledge, and interact without either the UI or AI becoming omniscient?

### Gameplay proof

The player encounters an NPC ship whose exact identity, affiliation, capability, or intent is not initially known. Through distance, movement, sensor capability, scanning, and communication, the player can improve that knowledge.

The player can maneuver, scan, hail, approach, avoid, or leave. The NPC uses one or two deterministic behaviors such as hold, approach, or withdraw according to its goal and what it knows. Weapons are not required.

### Candidate workstreams

#### 1. Truth-versus-knowledge model

Introduce a project-owned actor knowledge representation sufficient for ship contacts.

A useful conceptual distinction is:

```text
World truth
    ship instance
    actual position
    actual affiliation
    actual class/capabilities

Actor knowledge
    contact identity
    observed/estimated position
    confidence or certainty
    classification/affiliation knowledge
    last observation time
    stale or inferred information
```

The exact fields should be justified by the first playable contact. Do not build a universal intelligence database.

#### 2. Contact lifecycle

Define how a contact is first acquired, updated, becomes stale, is lost, and is correlated with a known ship when appropriate.

Contact identity should not accidentally reveal true ship identity before the observing actor has earned that knowledge.

#### 3. Sensor capability integration

Use existing sensor condition as the first consumer, then make observation quality depend on actual domain factors required by the slice: range, condition, elapsed time, relative state, or another bounded model chosen during refinement.

Do not attempt final Star Trek sensor physics. Build enough uncertainty to prove the knowledge boundary and create command decisions.

#### 4. Actor-appropriate projections

The player projection must expose what the player knows, not unrestricted world truth. Debug/test paths may inspect truth separately when clearly labeled.

The same principle should apply to the NPC decision input.

#### 5. Minimal explainable ship AI

Implement the smallest consequential autonomous decision pipeline consistent with ADR 0010:

- one persisted goal/posture;
- an actor-specific knowledge snapshot;
- a small candidate set, for example hold/approach/withdraw;
- hard constraints;
- explicit scoring or policy rules;
- deterministic tie-breaking;
- a typed Core command or deliberate no-action result;
- a structured explanation.

Do not introduce a behavior-tree foundation or universal state-machine framework.

#### 6. Communication/hailing seam

Allow the player to hail or otherwise communicate at a minimal level sufficient to prove a non-combat response path. This can remain a small typed interaction rather than a branching narrative system.

#### 7. Encounter and tactical-space lifecycle

Define the minimum relationship between strategic actor activity and local tactical presence. Entering tactical interaction should not create the NPC from nothing or erase its strategic identity when the encounter ends.

#### 8. Tests and diagnostics

Important tests include:

- changing hidden truth while holding actor knowledge constant does not change the actor's decision;
- observation updates are deterministic for identical inputs/random stream state;
- stale/lost contacts behave predictably;
- save/load preserves actor knowledge and contact identity where consequential;
- the player projection never leaks hidden fields;
- AI explanations identify goal, known information, candidates, constraints, and selected action;
- encounter exit returns durable actors to appropriate world state rather than despawning them as encounter props.

### Acceptance themes

The milestone is successful when **fog of war is a simulation rule, not a rendering effect**.

The player should be able to meet another ship, initially know less than Core truth contains, deliberately spend time/capability to learn more, and make a non-combat decision while the NPC does the same from its own bounded information.

### Deliberately deferred

- final sensor formulas;
- cloaking;
- electronic warfare/deception depth;
- full diplomacy;
- weapons and shields;
- strategic faction planning;
- large intelligence-sharing networks.

### Refinement questions before implementation

- Which facts are uncertain in the first contact: exact position, class, affiliation, intent, or some smaller subset?
- Is contact identity observer-local, globally correlated, or both through an explicit mapping?
- What sensor observation rule creates meaningful decisions without excessive bookkeeping?
- What exact NPC posture gives the best architecture proof with the least AI complexity?
- How should strategic travel transition into and out of local tactical interaction?

---

## Milestone 4 — Engineering Backbone and Degraded Operations

### Goal

Establish the minimum ship-system structure needed for later engineering and combat depth, while deliberately avoiding a complete definition of every possible Star Trek subsystem.

The milestone should make **power generation/distribution, sensors, and propulsion** interact with condition and repair strongly enough to prove that future systems can attach to the same domain relationships.

### Architectural question

> Can a ship contain distinct systems whose condition and resource allocation change what the ship can actually do, while each system retains domain-specific behavior and the model remains open to later shields, weapons, life support, computers, transporters, and other capabilities?

### Gameplay proof

The player operates a ship with degraded equipment and insufficient power to run propulsion and sensors at full capability simultaneously. The player can redistribute power, accept degraded sensing or maneuvering, begin or continue repairs, and see capability change as system condition and allocation change.

A compact engineering crisis should be playable without becoming a bookkeeping simulator.

### Candidate workstreams

#### 1. Minimal ship-system ownership model

Define only the common state that two or more concrete systems actually share, likely including concepts such as:

- stable system identity within a ship;
- system type/role;
- condition or integrity;
- operational/degraded/offline state where meaningfully distinct;
- repair linkage;
- capability exposure.

Do not create one `IShipSystem` interface containing hypothetical fields such as heat, ammo, frequency, range, charge, coolant, or crew merely because some future system might need them.

#### 2. Power generation and allocation

Introduce the simplest authoritative relationship that proves:

```text
generation -> available power -> allocation -> effective system capability
```

Initial power can be deliberately abstract. The project does not need the final EPS-grid, auxiliary-reactor, battery, overload, or bus-isolation simulation in this milestone.

The rules should nevertheless make conservation, bounds, allocation changes, and degraded generation mechanically testable.

#### 3. Sensors as an engineering consumer

Refactor sensor capability so it depends on system condition and allocated power rather than only a standalone integrity value. Connect the result to the knowledge/contact behavior from Milestone 3.

This is the first proof that engineering choices change what the player and AI can know.

#### 4. Propulsion as an engineering consumer

Make tactical or strategic propulsion capability depend on condition and allocated power to the degree justified by the slice. A damaged or underpowered propulsion system should limit available choices rather than merely subtract generic hit points.

#### 5. Damage, degradation, and repair

Generalize repair beyond the walking-skeleton's one sensor repair only as much as multiple systems require.

Important semantics include:

- damaged systems can remain partially functional where useful;
- repair targets a stable system identity;
- repair consumes simulation time and survives travel/save/load as appropriate;
- completing repair changes capability through the same authoritative system state used by normal operations;
- invalid simultaneous or conflicting repair states fail cleanly.

Crew staffing, spare parts, detailed maintenance logistics, and damage-control teams may remain deferred.

#### 6. Quantity and unit boundaries

Apply ADR 0011 where the concrete power, distance, velocity, duration, or other physical quantities cross subsystem boundaries. Avoid false precision for fictional concepts such as integrity or sensor confidence.

#### 7. Engineering command projection/UI

Expose enough state for the player to understand the tradeoff:

- available/generation power;
- current allocation;
- system condition;
- current effective capability;
- active repair and estimated completion/progress where the rules support it.

The UI must remain a command surface over Core state, not the owner of allocation or repair calculations.

#### 8. Cross-system tests

Prioritize tests proving interactions rather than isolated setters:

- total allocation cannot exceed available power;
- reducing sensor power changes observation capability;
- reducing propulsion power changes movement capability;
- damage caps or degrades capability even with abundant allocation;
- repair restores capability through system condition;
- save/load in the middle of allocation + repair produces equivalent continuation;
- deterministic large-step/small-step equivalence is asserted only for rules that explicitly guarantee it.

Property/model tests are appropriate for conservation and bounded-allocation invariants if the concrete implementation provides a useful independent model.

### Acceptance themes

The milestone is successful when engineering creates a real operational choice rather than a collection of unrelated percentage bars.

A representative loop is:

1. sensors and propulsion are both degraded or power-constrained;
2. the player cannot maximize both simultaneously;
3. changing allocation affects actual sensing/movement rules;
4. repair progresses on simulation time;
5. future capability changes are visible and persistent.

### Deliberately deferred

- final catalog of ship systems;
- shields and weapons until combat work;
- detailed EPS topology;
- fuel economy unless required by propulsion design;
- heat/coolant simulation;
- crew assignments and individual personnel;
- inventory/spare-parts economy;
- full damage-control procedures.

### Refinement questions before implementation

- What is truly common among reactor/power, sensors, and propulsion, and what should remain system-specific?
- Does power need explicit physical units immediately, or is a project-owned bounded fictional quantity the better first model?
- How should effective capability combine condition and allocation without locking future tuning into persistence?
- Which current sensor-repair fields migrate into the new system model and which disappear as walking-skeleton assumptions?
- What is the smallest engineering UI that makes the tradeoff understandable?

---

## Milestone 5 — Living Sector and Faction Autonomy

### Goal

Scale the active-world proof from isolated NPC activity into a small regional simulation in which factions and ships pursue their own goals and create changes while the player is elsewhere.

The objective is not to build the final grand strategy simulation. It is to prove the **causal chain from faction intent to autonomous ship activity to durable world change**.

### Architectural question

> Can a small number of factions and ships make deterministic, explainable strategic decisions from their own information and resources, execute those decisions over time, and produce a region whose state changes independently of the player?

### Gameplay proof

A small sector containing roughly 3–5 locations, 2 factions, and several NPC ships continues to evolve while the player travels or waits. Patrols move, assignments complete, one faction reacts to a known condition, and at least one NPC-NPC interaction occurs or alters a later decision without requiring the player to witness it.

### Candidate workstreams

#### 1. Minimal faction domain state

Introduce only the faction state needed by the first autonomous decisions, potentially:

- stable faction identity;
- a small relationship/posture value toward another faction;
- one strategic objective or interest;
- one bounded resource/capability relevant to the objective;
- doctrine or preference values necessary to choose among candidate actions.

Do not build the full economy, government, diplomacy, territorial administration, or fleet hierarchy.

#### 2. Strategic goals and assignment generation

Create one or two faction-level decisions that can generate or alter ship assignments, such as:

- patrol a border/route;
- investigate a contact or location;
- reinforce a location;
- escort or support a vessel;
- withdraw from an unfavorable area.

The strategic AI should follow ADR 0010's explicit information/candidate/constraint/choice/explanation pipeline.

#### 3. Scheduled strategic decision cadence

Use simulation-time events and meaningful decision boundaries rather than evaluating every faction every tactical tick.

Define bounded work budgets and stable tie-breaking. Detect and prevent pathological immediate rescheduling.

#### 4. NPC-NPC world interaction

Prove that two non-player actors can affect one another without the player being the activation trigger. The first interaction can be intentionally simple: detection causing withdrawal, an intercepted route causing a changed order, or another non-combat response.

Combat between NPCs may remain out of scope until the tactical combat milestone unless a tiny abstract consequence is necessary to prove the strategic flow.

#### 5. Knowledge propagation

Allow strategic decisions to use actor/faction knowledge rather than omniscient truth. Initially this may mean local reports, observed contacts, or explicit shared information between faction-aligned ships.

Do not build a complete intelligence network. Prove only the information flow required by the first strategic decision.

#### 6. Persistence and historical continuity

Persist faction state, active assignments, relevant actor knowledge, strategic decision timing, and any world changes that determine future outcomes.

#### 7. Player-facing strategic clarity

Expose enough map/status information for the player to understand that the sector is active without revealing hidden simulation truth. Examples may include known ship movements, stale reports, faction posture, recent known activity, or changed access.

#### 8. Long-running simulation tests

This milestone should establish the first serious long-horizon world tests. Seeded runs should advance many hours/days and assert against:

- invalid or dangling actor references;
- event explosion;
- zero-time loops;
- actor starvation;
- pathological oscillation between two orders;
- AI deadlock/no-action when a valid fallback exists;
- unrestricted omniscient information access;
- unbounded growth of scheduler/state for a stable small world;
- save/load divergence;
- nondeterminism caused by collection insertion order.

### Acceptance themes

A strong acceptance scenario is:

1. save a valid sector at simulation time T0;
2. issue no player commands other than time advancement;
3. advance a meaningful strategic duration;
4. observe deterministic changes in NPC location, assignments, faction decisions, or known regional state;
5. repeat from the same seed/snapshot and obtain the same semantic result;
6. perform the same run with a save/load boundary and obtain equivalent continuation.

The world should no longer feel like a set of encounters waiting to be spawned.

### Deliberately deferred

- full economic production and trade simulation;
- large fleets and hundreds/thousands of actors;
- galaxy-wide political simulation;
- full treaty system;
- strategic warfare resolution;
- procedurally generated galaxy topology;
- mission/narrative framework.

### Refinement questions before implementation

- Which two factions and one strategic conflict best exercise autonomy without requiring lore breadth?
- What single resource or capability is necessary to make the objective a decision rather than a script?
- How much faction knowledge is shared instantly versus propagated through explicit reports?
- Which NPC-NPC interaction proves offscreen causality without prematurely implementing combat?
- What simulation duration and event count make a useful canonical long-running test budget?

---

## Milestone 6 — Tactical Combat Foundation

### Goal

Introduce the first real starship combat only after the project already has multiple persistent actors, incomplete information, autonomous decisions, power tradeoffs, propulsion, damage/repair foundations, and offscreen world continuity.

Combat should be a **composition of existing systems**, not a parallel minigame with generic hit points.

### Architectural question

> Can tactical combat emerge from ship capabilities and world rules already established elsewhere, produce subsystem-level operational consequences, and end with durable actors and political context still intact?

### Gameplay proof

A small engagement between the player and one NPC supports:

- maneuvering in continuous tactical space;
- acquiring/maintaining sufficient targeting knowledge;
- allocating power under pressure;
- shields;
- at least one directed-energy weapon;
- subsystem or capability damage;
- degraded operation and repair pressure;
- deterministic NPC tactical decisions;
- withdrawal/disengagement;
- persistence before, during, and after combat.

Destroying the opponent is not required to be the only successful outcome.

### Candidate workstreams

#### 1. Tactical targeting and fire-control knowledge

Targeting must depend on actor knowledge and sensor capability rather than unrestricted world truth. Define the minimum information required to fire or achieve effective fire.

#### 2. Shield foundation

Implement the smallest shield model that creates tactical and engineering decisions. Candidate concerns include:

- available shield capability;
- power dependence;
- condition/damage;
- facing or directional exposure if included in the first combat slice;
- interaction with incoming weapon effects.

Do not define every shield frequency, modulation, regenerative edge case, or canonical variant.

#### 3. Weapon foundation

Implement one bounded weapon family, likely a directed-energy weapon, with explicit:

- weapon identity/capability;
- power or energy requirement;
- range/geometry rule;
- target/knowledge requirement;
- deterministic/random resolution using declared streams;
- effect passed into damage resolution.

Torpedoes, ammunition logistics, special weapons, boarding, and advanced firing modes can wait.

#### 4. Damage and subsystem consequence resolution

Damage should change operational capability through the engineering model. Avoid reducing all outcomes to hull hit points.

The first model may still include a structural survival concept, but meaningful damage should be able to degrade sensors, propulsion, shields, weapon capability, power generation, or another implemented system.

#### 5. Tactical AI

NPC tactical decisions should reuse the actor-information and typed-command boundaries. A small doctrine may choose among maneuver, fire, protect, or withdraw according to explicit constraints and scores.

No behavior-tree foundation is required.

#### 6. Engagement/disengagement lifecycle

Combat should begin and end without creating or deleting actors solely because a scene changed. Surviving ships retain damage, position/strategic consequence, orders, and later memory as appropriate.

#### 7. Player command/UI surface

The tactical UI should emphasize command information: range, bearing, known target state, power tradeoffs, shield/system status, weapon readiness, and available responses.

Visual spectacle remains secondary to player-visible clarity.

#### 8. Combat scenario/property tests

Coverage should include:

- targeting with insufficient/stale knowledge;
- power loss changing shields/weapons/propulsion;
- system damage changing available commands;
- same-state/same-seed deterministic combat outcomes;
- withdrawal and encounter termination;
- save/load during an engagement;
- invalid destroyed/removed targets with scheduled work;
- conservation/bounds properties where the chosen power/energy model supports them;
- AI decisions using actor knowledge rather than hidden truth.

### Acceptance themes

The combat slice is successful when the player wins or survives by making **starship command tradeoffs** rather than merely depleting a health bar faster than the opponent.

A damaged ship should often remain interestingly functional: perhaps able to maneuver but not scan well, protect itself but not fire effectively, or fire while sacrificing propulsion/sensor capability.

### Deliberately deferred

- a large weapon catalog;
- torpedo logistics unless chosen as the first weapon instead of directed energy;
- boarding actions;
- cloaking;
- advanced electronic warfare;
- fleet combat;
- detailed crew casualties;
- cinematic combat presentation;
- final balance.

### Refinement questions before implementation

- What is the smallest shield model that proves power + geometry + condition interaction?
- Which single weapon best exercises targeting, power, and damage without adding unrelated logistics?
- What subsystem damage set is sufficient for interesting degraded operation?
- What disengagement rules preserve tactical choice and strategic continuity?
- Which combat facts become durable historical incidents for the next milestone?

---

## Milestone 7 — Diplomacy, Incidents, and Durable Consequences

### Goal

Give the persistent world memory: player and NPC actions should create durable political or relational meaning that can alter later decisions, access, communication, assistance, hostility, or trust.

This milestone should also make non-combat responses more consequential so Star Trek problem-solving is not reduced to choosing whether to fire.

### Architectural question

> Can the simulation remember a consequential interaction, distinguish what different actors know about it, interpret it according to faction/domain rules, and allow that memory to change a later encounter?

### Gameplay proof

The player can create at least several distinct consequential outcomes, for example:

- render assistance;
- respond peacefully or cooperatively;
- threaten or intimidate;
- violate a declared boundary or agreement;
- attack without accepted justification;
- show restraint or withdraw.

A later ship or faction decision demonstrably changes because of one of those prior outcomes.

### Candidate workstreams

#### 1. Durable incident/event meaning

Introduce a domain representation for the minimum historical facts the simulation must remember. The design should distinguish authoritative historical meaning from diagnostics.

Potential first incident properties include:

- stable incident identity;
- simulation time/location;
- involved actors/factions;
- event category;
- observed or alleged responsibility;
- severity or consequence band;
- what each relevant party knows or believes;
- whether the incident remains active/relevant.

Do not create a universal event-sourcing architecture or persist every low-level simulation transition forever.

#### 2. Relationship/reputation consequence

Add the smallest relationship state that can be affected by incidents and consumed by later AI/interaction rules. The exact scope may be faction-to-player, faction-to-faction, or ship/captain-specific only when the first gameplay proof requires it.

#### 3. Knowledge and report propagation

A faction should not react to an event it has no way to know occurred. Establish a bounded path by which incidents become known, reported, disputed, or remain uncertain.

This is where world truth, actor knowledge, and political interpretation begin to diverge meaningfully.

#### 4. Non-combat communication actions

Expand communication from the minimal hail seam into a small set of typed, rule-governed actions. Examples may include request aid, identify intent, warn, demand withdrawal, offer assistance, or accept a limited agreement.

Keep simulation consequences in Core. Do not make dialogue text authoritative.

#### 5. Limited agreement/obligation seam

If required by the selected scenario, introduce one bounded durable obligation such as temporary access, stand-down, safe passage, or assistance owed. This can prove that consequences extend beyond scalar reputation without requiring a complete treaty framework.

#### 6. AI use of history

Make at least one autonomous decision explicitly depend on relevant relationship/incident knowledge. The explanation should show the historical modifier or constraint that changed the choice.

#### 7. Persistence and player-facing history

Persist only history that future gameplay needs. Provide the player with an appropriate known-history or recent-incidents view without exposing hidden allegations/intelligence.

#### 8. Scenario tests

Tests should prove:

- the same current tactical situation can produce a different AI response when relevant prior history differs;
- an unknown incident does not affect an actor until knowledge reaches it;
- disputed/uncertain responsibility does not become omniscient certainty accidentally;
- incident and relationship state survives save/load;
- logs are not required to reconstruct gameplay consequences;
- long-running history does not grow without an explicit retention/aggregation rule;
- a non-combat action can create a later strategic effect.

### Acceptance themes

A representative proof is:

1. the player assists or harms a ship/faction;
2. that action becomes a durable incident known to an appropriate party;
3. simulation time and unrelated activity continue;
4. the player later encounters another relevant actor;
5. its available actions, posture, or decision scores differ because of the earlier incident.

The repercussion should arise from domain state, not a bespoke mission-script check.

### Deliberately deferred

- a complete treaty language;
- government/political simulation;
- procedural diplomatic dialogue;
- full economy/trade;
- individual-character RPG reputation systems;
- unbounded historical event logs;
- narrative engine adoption unless a concrete branching authored feature now justifies ADR 0012's trigger.

### Refinement questions before implementation

- What historical events are consequential enough to persist rather than summarize into relationship state?
- Which actors/factions can know or dispute an incident, and how does information reach them?
- Does the first durable agreement justify a separate treaty/obligation concept or can it remain a narrow typed state?
- How should old incidents decay, summarize, or remain permanently relevant?
- Is authored branching dialogue actually needed, or can typed communication choices remain sufficient for this slice?

---

## Milestone 8 — Canon-Anchored Campaign Bootstrap and Divergent History

### Goal

Turn the hand-authored active-world proof into a reproducible campaign start that is consistent with a chosen Star Trek epoch but populated with non-canonical local activity already in progress.

This milestone is the project's bounded analogue to Dwarf Fortress world-history initialization: Star Trek canon supplies the broad historical state; deterministic generation and/or a short pre-start warm-up supplies the mundane living activity surrounding the player.

### Architectural question

> Can the game construct a canon-consistent starting world that already has momentum, while preserving deterministic generation and allowing later simulation outcomes to diverge from television history when the world state meaningfully changes?

### Gameplay proof

Starting a campaign for a chosen supported epoch produces:

- declared canonical/political starting conditions;
- a small regional topology/content set;
- factions in appropriate baseline relationships;
- ships with plausible assignments and resources;
- some actors already in transit or mid-task;
- future scheduled activity;
- deterministic reproducibility from the same campaign parameters and generation seed.

Different seeds may change non-canonical local assignments/activity while preserving required canonical boundary conditions.

### Candidate workstreams

#### 1. Campaign/scenario definition

Define a narrow input contract for a campaign start. Candidate inputs include:

- epoch/canon anchor identity;
- starting simulation time/stardate representation;
- region/content-set identity;
- required canonical political facts;
- required locations or actors where the supported scenario needs them;
- player start parameters;
- generation seed and algorithm version;
- generation constraints/weights.

Avoid encoding all Star Trek canon into one giant schema.

#### 2. Canon boundary policy

Before canonical future events become implementation dependencies, explicitly refine and document the policy for events that television history says should occur after campaign start.

Questions include:

- Which facts are immutable starting history?
- Which events are scheduled pressures or high-probability developments rather than guarantees?
- Can player/world action avert, accelerate, relocate, or transform a canonical event?
- Is there ever a campaign mode that intentionally preserves stronger canon rails?

The roadmap's provisional preference is **canon as initial conditions and pressures, not an invisible correction mechanism**, but the implementation milestone should make this an explicit reviewed decision before content relies on it.

#### 3. Non-canonical activity generation

Generate the activity canon leaves unspecified:

- ship assignments;
- current travel legs;
- patrol states;
- surveys;
- routine repairs;
- local alerts/investigations;
- limited cargo/support movement if later systems justify it;
- pending strategic decisions.

Generated state must pass the same semantic validation as authored state.

#### 4. Optional bounded warm-up simulation

Evaluate whether campaign quality improves if the bootstrap creates actors/orders at a pre-start time and then runs the **normal strategic simulation** forward for a bounded period before handing control to the player.

For example, a campaign could initialize several days before player control and allow assignments, travel, and decisions to place actors naturally.

This is optional. If direct generation of valid in-progress state is simpler and equally convincing, do not add warm-up merely to imitate Dwarf Fortress.

#### 5. Random stream/version ownership

Use ADR 0007's deterministic random model. World/bootstrap generation should have stable stream identity and versioning so a seed is diagnostically meaningful.

Do not use Godot presentation noise as authoritative generation.

#### 6. Validation and failure handling

A generated campaign must fail closed if it creates broken references, impossible activities, contradictory canonical constraints, invalid schedules, duplicate identities, or unsupported combinations.

#### 7. Save identity and campaign metadata

Persist the campaign/scenario identity, generation/rules versions, and any data required to interpret the world. Do not regenerate the current world from seed on every load; the save remains an authoritative snapshot of everything that has happened since start.

#### 8. Reproducibility and variation tests

Test that:

- same campaign inputs + seed + rules version yield the same semantic starting state;
- different seeds produce allowed variation without violating required canonical facts;
- warm-up, if used, is equivalent to an ordinary headless simulation over the same inputs;
- campaign generation is independent from Godot and ambient wall-clock time;
- generated worlds can advance long enough to expose scheduling/AI pathologies before the player begins.

### Acceptance themes

A campaign should feel as though the player has entered a universe that already had a schedule that morning.

The generated background does not need hundreds of years of fake history. It needs enough causally valid **current momentum** that ships have reasons to be where they are and the first several hours/days of activity are not all spawned in reaction to the player.

### Deliberately deferred

- procedural generation of the entire galaxy;
- generation of centuries of alternate history;
- every Star Trek era at once;
- exhaustive canon database construction;
- historical character simulation;
- automatic ingestion of copyrighted third-party reference data;
- user modding API.

### Refinement questions before implementation

- Which canon epoch/region is the first supported campaign anchor?
- What canon facts must be immutable at start, and which are merely background assumptions?
- Does bounded pre-start warm-up add enough emergent quality to justify its complexity?
- Which non-canonical activities create the strongest "already alive" impression with the systems implemented by then?
- Does canon divergence policy require a new ADR or a narrower design decision/specification?

---

## Milestone 9 — Persistent Regional Campaign Integration

### Goal

Consolidate the preceding architecture into a durable regional campaign loop before expanding feature breadth substantially.

This is an integration and hardening milestone as much as a feature milestone. Its purpose is to prove that the systems developed independently remain coherent when exercised together over meaningful game time.

### Architectural question

> Can a player operate in one small region for an extended period while travel, sensors, engineering, NPC orders, faction decisions, combat or avoidance, diplomacy, history, and save/load all remain one consistent simulation?

### Gameplay proof

A bounded campaign region contains enough activity for the player to spend meaningful time making command decisions without the universe becoming static between bespoke events.

A representative loop may include:

1. begin in a canon-anchored active region;
2. review known strategic activity;
3. travel while NPC assignments and repairs progress;
4. detect and investigate or avoid an uncertain contact;
5. make engineering tradeoffs during travel or encounter pressure;
6. communicate, assist, withdraw, or fight;
7. carry damage and political consequences forward;
8. observe later faction/NPC behavior that reflects earlier events;
9. save/load at multiple points without changing future semantics.

### Candidate workstreams

#### 1. Cross-system scenario design

Build a small but intentional regional scenario/content set that exercises the implemented systems rather than maximizing content quantity.

A likely scale is still modest: several locations, a few factions, and enough ships to create overlapping activity without obscuring causality.

#### 2. Simulation invariant audit

Review boundaries that have accumulated across milestones:

- actor identity and lifecycle;
- scheduled work ownership;
- knowledge versus truth;
- ship-system capability derivation;
- faction/AI command boundaries;
- incident/history persistence;
- cross-scale movement;
- save mapping and migrations.

Refactor only where integration reveals real duplication or unsafe coupling.

#### 3. Headless campaign endurance suite

Advance representative seeded worlds through increasingly long horizons and collect reproducible failures. Add invariants around:

- scheduler growth and work budgets;
- identity/reference validity;
- faction/ship starvation;
- AI oscillation/deadlock;
- impossible system/resource states;
- history growth;
- contact/knowledge corruption;
- save/load divergence;
- memory/performance regressions.

#### 4. Performance measurement

Measure actual representative workloads before adopting performance architecture. Identify whether event-driven strategic simulation remains comfortably within budget and which operations dominate runtime.

Do not respond to hypothetical scale by introducing ECS, parallel mutation, databases, distributed services, or other foundational infrastructure without evidence.

#### 5. Player-visible clarity pass

Ensure the player can understand causal state without requiring developer knowledge. Improve the map/status surfaces for:

- current orders and travel;
- engineering capability and constraints;
- contact certainty;
- known faction activity;
- recent known incidents/consequences;
- tactical options and disengagement;
- simulation time/rate.

This is clarity work, not final visual polish.

#### 6. Persistence/migration hardening

Exercise saves at deliberately difficult boundaries: mid-travel, mid-repair, mid-contact, active faction plans, tactical engagement, and recent historical incident.

Add/retain migration fixtures according to the compatibility policy current at that release stage.

#### 7. Architecture conformance review

Add or strengthen ArchUnitNET/project checks for boundaries that have become durable through multiple consumers. Do not encode temporary folder structure as architecture.

#### 8. Release-readiness evidence for the integrated slice

Run canonical and appropriate deep validation, scenario testing, mutation analysis where useful, and manual playthroughs focused on causal consistency rather than content balance.

### Acceptance themes

This milestone is successful when the game has a **small but genuinely systemic campaign region** rather than a collection of isolated demonstrations.

The player should be able to tell stories of the form:

> I diverted to investigate a contact, spent too much power on sensors while damaged, arrived late to another situation, avoided a fight through communication, and later discovered that the faction remembered what I had done.

The exact story need not be authored. The important part is that the causal chain is supported by reusable simulation rules.

### Deliberately deferred

Unless integration evidence elevates one of them, continue to defer:

- large content catalog expansion;
- full economy and trade network;
- comprehensive crew/officer simulation;
- branching narrative framework;
- procedural galaxy generation;
- galaxy-wide war simulation;
- fleet-scale tactical combat;
- mod architecture;
- multiplayer/networking;
- final art/audio polish;
- speculative service/database infrastructure.

### Refinement questions before implementation

- Which small regional scenario provides the highest interaction density with the fewest new systems?
- What performance/invariant budgets are meaningful for the next development horizon?
- Which repeated architecture patterns now have enough consumers to justify formal shared abstractions?
- Which deferred domain has become the highest-risk next architectural dimension after integration?

---

## Cross-milestone requirements

The following expectations apply throughout the roadmap.

### 1. Player actions create durable consequences where the domain says they matter

A meaningful state change should not disappear merely because the current UI closes or the actor leaves tactical range. Damage, repairs, orders, faction posture, knowledge, agreements, and historically consequential incidents should survive according to their actual domain lifetime.

Not every action deserves permanent historical storage. Persist the minimum state required to preserve future meaning.

### 2. Autonomous actors operate through normal Core rules

Player, AI, and authored/system-generated actions should converge on validated domain commands where they produce the same kind of consequence. AI evaluation may use different decision machinery, but it must not receive a private mutation path that bypasses game rules.

### 3. Actor knowledge is part of gameplay

Player projections and AI decisions should receive information appropriate to the observer. Debug tools can expose truth deliberately, but convenience should not convert fog of war into cosmetic hiding.

### 4. The player does not activate ordinary world simulation

Entering a map, opening a panel, detecting a contact, or beginning a conversation should not be what makes unrelated actors start existing or pursuing their normal objectives.

### 5. Strategic and tactical time share one authority, not one update rate

The scheduler and explicit simulation clock remain the common temporal foundation. Systems update at the coarsest meaningful resolution and can use fixed/bounded tactical integration only where necessary.

### 6. Save state follows authoritative meaning

Every milestone that adds durable state must extend explicit persistence mapping and validation in the same development slice. Do not postpone world persistence until after a feature has accumulated runtime-only assumptions.

### 7. Long-running simulation tests grow with world autonomy

As more independent actors and systems are added, the test strategy must increasingly include seeded long-horizon scenarios. Important failure classes include starvation, oscillation, invalid references, event explosion, zero-time loops, nondeterministic iteration, impossible resource states, stale knowledge errors, and save/load divergence.

### 8. Observability explains causality but does not own it

Structured diagnostics should make it possible to explain why an AI acted, why an event executed, and which actor/system was affected. Gameplay history needed later must remain authoritative state rather than an assumption that logs will exist.

### 9. Content breadth follows system proof

One or two ship definitions, weapons, factions, scenarios, or incident types are usually enough to prove an architectural slice. Create more content only when variety itself is the feature or when another consumer is necessary to expose a bad abstraction.

### 10. Every milestone revisits abstraction pressure

At milestone start and end, explicitly inspect which assumptions have become unsafe. Current examples include:

- player-only ship state;
- scheduler work without target/payload semantics;
- scenario-specific initial condition embedded in ship definitions;
- single-system repair assumptions;
- unrestricted truth access in future AI/projections;
- encounter-local actor lifetime;
- logs being mistaken for historical state.

The purpose is not to refactor preemptively. It is to notice when a second or third concrete consumer has supplied enough evidence for a better boundary.

---

## Explicit anti-goals for this roadmap horizon

The following are intentionally **not** near-term foundations unless a milestone uncovers concrete evidence that changes the decision:

- a universal `Entity` base hierarchy;
- an ECS framework;
- a generic event/message bus;
- a database or ORM for world persistence;
- event sourcing as the save architecture;
- a general workflow/job engine for simulation scheduling;
- a behavior-tree framework as the core AI model;
- a universal finite-state-machine framework;
- an embedded scripting language for ordinary game rules;
- hosted or local LLM authority over gameplay decisions;
- networking or multiplayer architecture;
- service-oriented/backend infrastructure;
- full procedural galaxy generation;
- a complete mission/narrative framework before a concrete branching feature needs it;
- a complete ship-system taxonomy before additional systems are actually implemented;
- a complete faction/economy model before the living-sector slice proves its first strategic consumers.

These are not permanent bans. They are guardrails against solving scale and flexibility problems the game has not demonstrated yet.

---

## Likely work after this roadmap horizon

The order after Milestone 9 should be chosen from evidence produced by the integrated campaign rather than fixed now. Plausible next domains include:

- deeper diplomacy, treaties, alliances, and war;
- economy, logistics, trade, resources, and strategic supply;
- officer/crew and damage-control depth without character-level progression;
- missions and authored branching narrative through ADR 0012's typed simulation boundary;
- exploration, anomalies, scientific investigation, and discovery consequences;
- additional ship systems such as transporters, computers, life support, cloaking, advanced sensors, and electronic warfare;
- more weapons, tactical doctrines, and multi-ship/fleet engagements;
- additional spatial scales and broader regional/galactic strategy;
- richer campaign generation and strategic historical pressures;
- content tooling and authoring workflows justified by actual content volume;
- accessibility, presentation, audio, art, packaging, and release work as their own product requirements mature.

The roadmap should be revised when those choices become concrete rather than allowing this document to become a stale promise.

---

## Roadmap maintenance

This document is a planning artifact, not an immutable specification.

Update it when:

- a milestone is completed and evidence changes later priorities;
- a milestone is split, combined, or substantially re-scoped;
- a new ADR changes an architectural boundary assumed here;
- performance or testing evidence invalidates an assumption;
- a deferred feature becomes necessary to complete an earlier architectural proof;
- the project reaches the end of this near-/mid-term horizon and selects the next major domains.

When refining an individual milestone, prefer a new governing issue/specification over expanding this roadmap with implementation-level detail. The roadmap should continue to answer **where we are going, why that order matters, and what architectural question each major step must prove**.
