# Star Trek: Alter Course Development Roadmap

## Purpose and authority

This roadmap defines the development sequence for **Star Trek: Alter Course (ST:AC)**. It is a planning document: it records what has been implemented, which architectural proof each remaining milestone must provide, and which approved bounded slice comes next.

The [design wiki](docs/wiki/README.md) is the single source of truth for game design. This roadmap may summarize approved design only to explain sequencing. If a roadmap statement conflicts with the wiki, the wiki governs and this file should be corrected. Active [architecture decision records](docs/adr/) govern architectural boundaries and override this roadmap where they conflict.

The current operational state is tracked in [STATUS](docs/STATUS.md), while [Implementation Status](docs/wiki/implementation-status.md) records the reviewed gameplay baseline. Release promotion and branch behavior are governed separately by ADR 0013; roadmap completion does not itself create a release.

## Current reviewed baseline — September 6, 2026

- **v0.5.0 — Strategic Contact Reporting** is the current immutable source-only release; it succeeds v0.4.0 — First Contact & Engineering Backbone.
- **Milestone 1** and **Milestone 2** are implemented.
- **Milestone 3A — First Observed Contact** is implemented, but **Milestone 3 as a whole is not complete**.
- **Milestone 4 — Engineering Backbone and Degraded Operations** is implemented.
- Released v0.5.0 uses content schema **V4** and save schema **V6**; the `dev` runtime adds strict faction content V1 and save V7 through Feature #86 / Final PR #87, merged as `0217296`.
- The broader approved [factions and organizations](docs/wiki/factions-and-organizations.md) political model remains **design only** beyond the implemented root-faction/direct-control subset. No faction hierarchy, organization runtime, government model, treaty system, layered jurisdiction runtime, or strategic affiliation-knowledge system exists yet.
- [Strategic Contact Reporting](docs/wiki/strategic-contact-reporting.md) is **implemented** (Feature #77, Final PR #78), resolving Q-01. It is not canonically named `M3B`, does not complete Milestone 3, and did not itself begin M5.
- [Faction Intent and Autonomous Assignment](docs/wiki/faction-intent-and-autonomous-assignment.md) is the implemented first M5 slice, resolving Q-05. Current `dev` uses V7; released v0.5.0 remains V6.

## Execution model

Each substantial gameplay milestone should normally proceed through a governed issue or feature specification before implementation. Refinement should be limited to decisions required by the selected vertical slice; open questions must not be answered speculatively merely because a future system can be imagined.

An implementation slice should normally include, where applicable:

1. review of the active ADRs, owning wiki pages, current code paths, and persistence boundaries;
2. explicit in-scope and out-of-scope behavior in the governing issue/specification;
3. authoritative simulation work in `AlterCourse.Core`;
4. persistence and adjacent migration work for new durable state;
5. Godot projection/input work only for player-visible behavior;
6. focused unit, negative, persistence, scenario, long-horizon, and architecture tests at the lowest useful layer;
7. at least one playable or headless proof of the architectural claim; and
8. canonical verification and the repository's governed pull-request workflow.

Prefer narrow vertical slices that connect existing domains over broad subsystem construction. Add abstractions because concrete consumers require them, not because later scale can be imagined.

## Strategic guardrails

### The universe is not player-centric

The player commands one ordinary ship inside a world that can continue to travel, repair, investigate, patrol, negotiate, trade, fight, and change without the player's presence. A useful test for every major feature remains:

> **What happens if the player never interacts with this?**

If the answer is that the system never progresses or has no consequences, the feature is not yet proving the intended persistent-world model.

### World truth, actor knowledge, and remembered meaning are different

The simulation must continue to distinguish:

1. **world truth** — what exists and what happened;
2. **actor knowledge** — what a ship, faction, organization, or player currently knows or believes; and
3. **durable historical meaning** — remembered facts, allegations, incidents, or interpreted consequences that later rules actually need.

Diagnostic logs are not a substitute for authoritative knowledge or political memory. Presentation and autonomous AI must not gain hidden world truth merely because Core owns it.

### Offscreen simulation uses the coarsest correct resolution

Strategic activity should wake at meaningful boundaries rather than forcing every actor through tactical-frequency updates. Tactical motion may need fixed-step integration; strategic travel, orders, faction decisions, and other long-horizon work should remain event-driven where that preserves intended behavior.

### Canon supplies boundary conditions; simulation supplies activity and divergence

Canon should establish the chosen campaign's required historical and political starting conditions. The simulation should populate the enormous amount of local activity canon does not specify and then allow legitimate consequences to accumulate. Exact campaign era, treatment of later canonical events, initialization/warm-up, and random-version policy remain refinement questions for Milestone 8 rather than assumptions to encode now.

---

## Milestone overview

| Order | Milestone | Status | Primary architectural proof |
| --- | --- | --- | --- |
| 1 | **World State and Bootstrap Generalization** | **Implemented** | The world owns plural persistent ships and explicit player identity rather than treating the player ship as the world root. |
| 2 | **Active World and Persistent Orders** | **Implemented** | Ships can own durable intent, progress offscreen, and retain that intent across save/load. |
| 3 | **Sensor Knowledge and First Contact** | **Partial — M3A and Strategic Contact Reporting implemented; M3 not complete** | M3A proves observer-local knowledge and information-limited ship behavior; Strategic Contact Reporting carries legitimate last-known contact information into strategic context without defining the final intelligence model. |
| 4 | **Engineering Backbone and Degraded Operations** | **Implemented** | Power, condition, capability, and repair interact with existing sensing and maneuvering rather than living in a parallel subsystem. |
| 5 | **Living Sector and Faction Autonomy** | **First bounded slice approved; runtime not started** | Faction intent can cause explainable autonomous assignments, offscreen activity, and durable world change using actor-appropriate information; the selected first assignment slice does not complete this entire milestone. |
| 6 | **Tactical Combat Foundation** | **Future** | Combat composes motion, observation, Engineering, AI, persistence, and withdrawal instead of becoming a separate hit-point game. |
| 7 | **Diplomacy, Incidents, and Durable Consequences** | **Future** | The world distinguishes events, knowledge/attribution, legal status, attitudes, and remembered consequences that affect later decisions. |
| 8 | **Canon-Anchored Campaign Bootstrap and Divergent History** | **Future** | A campaign begins from reproducible canon-consistent boundary conditions plus already-active noncanonical local activity. |
| 9 | **Persistent Regional Campaign Integration** | **Future** | The preceding systems form one durable regional gameplay loop that remains coherent over extended simulation time. |

The order expresses current dependency and risk, not an immutable release schedule. A governed refinement may split or combine adjacent work when code and design evidence justify it.

---

## Completed foundation

### Milestone 1 — World State and Bootstrap Generalization

**Implemented by Feature #36 / Final PR #37.**

Core owns plural ordinary `ShipState` values with stable ship identity and one explicit `PlayerShipId`. Strategic state and scheduled ship consequences are ship-targeted. Typed bootstrap separates reusable ship definitions from vessel-specific starting state. Save evolution and validation preserve the plural world, and headless scenarios proved independent movement/repair/travel, target isolation, deterministic continuation, and insertion-order independence.

The milestone intentionally did **not** introduce a universal actor/entity hierarchy, ECS, event bus, general scenario language, or faction system.

### Milestone 2 — Active World and Persistent Orders

**Implemented by Feature #38 / Final PR #39.**

Ships can own durable `TravelTo`, `PatrolRoute`, and `HoldUntil` orders with stable identity. Strategic work progresses offscreen at meaningful boundaries, while tactical motion retains the finer integration it actually needs. Player and autonomous travel share domain commands, order state survives save/load, and work budgets bound long-horizon advancement.

The milestone proved that NPC activity can proceed without the player while deliberately deferring factions, broad strategic AI, political knowledge, and combat.

### Milestone 3A — First Observed Contact

**Implemented by Feature #58 / Final PR #61; included in v0.4.0.**

Observer-local sensor knowledge now supports stable local contact identity, Current/Stale/Lost/reacquired lifecycle, active Scan, typed Hail acknowledgement, exact occurrence times, and bounded cautious NPC behavior that acts from actor-safe information rather than hidden target state. Godot presents only the player-safe projection.

This is a **partial Milestone 3 outcome**. Strategic contacts, affiliation/intent knowledge, durable reports beyond the current local-contact model, and faction information sharing remain absent in v0.4.0.

### Milestone 4 — Engineering Backbone and Degraded Operations

**Implemented by Feature #62 / Final PR #63; included in v0.4.0.**

Core owns bounded generation, power allocation, concrete sensor/impulse condition, derived capability, and deterministic system repair. Those values affect real sensor reach, tactical speed, scan continuity, AI inputs, persistence, and the live Engineering workspace. Ship-definition content V4 is current. Save V5 was introduced by this milestone; Strategic Contact Reporting added released V6, and the faction implementation adds V7.

The milestone intentionally stops short of a universal component system, arbitrary combat damage, detailed EPS topology, batteries, warp power, fuel, heat/coolant, repair teams/queues, shields, weapons, or crew simulation.

### Strategic Contact Reporting

**Implemented by Feature #77 / Final PR #78; included in v0.5.0.**

Q-01 is resolved. This bounded slice bridges M3A's local observer knowledge to strategically meaningful last-known information before M5 introduces faction autonomy, proving:

> **local tactical observation → durable reference-frame-qualified last-known information → actor-safe strategic report/projection**

It reuses existing observer-local `SensorContactId`, qualifies retained tactical observations with the strategic location/reference frame where they occurred via `SensorContactTrack.ObservedAtLocationId`, preserves legitimate last-known information after a contact becomes Stale/Lost or either ship moves, and proves that later hidden target truth does not backfill actor knowledge. Save schema advanced to V6 under rules identity `strategic-contact-reporting-v1`.

The canonical wiki page owns the full behavior, persistence constraints, test themes, and player-visible proof. This roadmap records only sequence and major boundaries.

The slice did **not** approve or introduce:

- a canonical `M3B` label;
- `KnownShipId` or global/cross-observer vessel identity;
- affiliation or intent as scan results;
- faction knowledge sharing or a report-distribution network;
- faction runtime or faction strategic AI;
- faction-owned scheduler targets;
- strategic long-range sensor simulation;
- combat; or
- a generic actor/entity/rules framework.

It left Q-02 through Q-05 unresolved at completion and did not itself complete M3 or begin M5. The later approved assignment design resolves Q-05 and scopes other questions without changing this historical contact-reporting boundary.

---

## Milestone 3 completion remains open beyond the implemented slices

M3A and Strategic Contact Reporting are implemented, but their completion does **not** automatically close Milestone 3.

The original Milestone 3 horizon also discussed affiliation/intent knowledge, strategic contacts, broader reporting, and identity questions. Strategic Contact Reporting resolves the smaller behavior of carrying legitimate local observations into actor-safe strategic last-known information.

Evaluate remaining M3 scope against actual consumers. Q-02 and Q-03 remain open; Q-04's sensor/intelligence-sharing portion remains open even though the first faction assignment now has an approved own-asset administrative view. Do not delay that bounded assignment slice to invent a final intelligence schema or M3 completion contract.

---

## Milestone 5 — Living Sector and Faction Autonomy

### Approved first bounded slice — Faction Intent and Autonomous Assignment

**Implemented in Feature #86 / Final PR #87, merged into `dev` as `0217296`.** The [canonical wiki decision](docs/wiki/faction-intent-and-autonomous-assignment.md) records the selected era-neutral root-faction proof and six decisions D-08 through D-13. Q-05 is resolved for this first consumer; neither M3 nor M5 is complete.

Faction A selects an idle directly controlled NPC ship to establish presence at Vesper Reach, where B has a ship. Making A's preferred candidate already committed must change the selected valid assignment. The existing order, travel, and sensor paths produce an offscreen NPC-NPC consequence without player interaction. Presence does not imply political territory or a treaty effect.

The policy uses only its objective, explicitly known proof-map topology, and own-asset identities/strategic states/order status. It does not consume external sensor reports. New control lives on the asset side with a derived roster; existing orders are not preempted and the player ship is excluded from faction autonomous assignment in the proof.

The implementation has closed Ship/Faction scheduled targets and typed bootstrap, and introduces V7 with a V6→V7 migration that creates zero factions, null ship controller links, and no faction work while preserving existing ship state and scheduling. Zero-faction worlds remain valid. No RNG, hierarchy runtime, organizations, political UI, intelligence network, or general actor framework is admitted.

This slice is a contribution toward the broader goals below, not a claim that all M5 requirements are satisfied. Existing Core and Godot compatibility evidence cover policy, scheduler, runtime, content, persistence, private catalog loading, player-safe projection, and targeted production and long-horizon scenarios.

### Goal

Prove the causal chain:

> **faction intent → explainable decision → ship assignment/order → offscreen activity → durable world change**

The smallest useful scenario for the complete M5 milestone should contain multiple locations, multiple ships, and enough political context for at least two autonomous factions or political actors to make a consequential choice. At least one NPC-NPC interaction should matter even if the player never witnesses it.

### Required architectural proof

- Faction decisions live in pure Core and obey ADR 0010's information, determinism, command, budget, and explanation boundaries.
- Strategic decisions consume actor-appropriate knowledge rather than unrestricted world truth.
- Existing ship orders and scheduler semantics are reused where they fit instead of creating a parallel faction simulation loop.
- A faction's objective or constrained resource/capability changes which valid action it selects; the scenario is not a fixed patrol script with political labels.
- Consequential faction state and decisions needed after load persist explicitly and validate atomically.
- Long-horizon tests cover starvation, zero-time loops, oscillation, dangling references, unbounded growth, and save/load divergence.

### Political design boundary

The approved [factions and organizations](docs/wiki/factions-and-organizations.md) model governs this work, but Milestone 5 does **not** require implementing its entire possible surface.

The first consumer preserves the approved principles it touches: stable faction identity, autonomous political will, actor knowledge, and one direct controller per participating asset. Root factions are sufficient for the selected assignment proof; recursive parentage, role/depth separation, independent attitudes, organizations, governments, covert operations, layered jurisdiction, treaty inheritance, and political transitions remain approved broader principles or deferred runtime rather than first-slice requirements.

Do not create three faction classes for Polity/Constituent/Internal, infer powers from depth labels, or add a generic actor/rules framework solely because the future political model is rich.

### Deliberately deferred unless a later slice proves need

- complete authority/permission matrices;
- treaty engine and precedence rules;
- espionage probability/resource systems;
- government succession and coalition mechanics;
- economy/market simulation;
- complete organization taxonomy;
- broad mission/narrative runtime; and
- galaxy-scale strategic optimization.

---

## Milestone 6 — Tactical Combat Foundation

### Goal

Add the smallest combat model that proves existing systems compose under pressure.

### Required architectural proof

- Weapons, shields, targeting, maneuver, sensing, Engineering capability, system condition, AI, and persistence use one authoritative simulation rather than a parallel combat-state graph.
- Targeting and tactical decisions are constrained by what the acting ship knows.
- Power and degraded condition create real choices and failure modes.
- Damage creates operational consequences through concrete systems rather than merely reducing generic hull hit points.
- Withdrawal, disengagement, and non-engagement remain valid outcomes.
- Tactical AI issues validated Core commands and remains deterministic/explainable at the consequence boundary.

Before admission, resolve **Q-10 — Tactical scope**. Do not define a broad weapon catalog, final damage topology, or advanced repair model until the first combat slice demonstrates the need.

---

## Milestone 7 — Diplomacy, Incidents, and Durable Consequences

### Goal

Make prior behavior alter later political decisions without collapsing diplomacy into one global reputation score.

### Required architectural proof

The simulation must distinguish at least:

- what actually happened;
- what each relevant actor knows, believes, or alleges happened;
- who is attributed responsibility;
- formal/legal status and obligations;
- independent attitudes, interests, grievances, and preferences; and
- the bounded durable memory needed for later rules.

Assistance, threats, restraint, violations, covert support, or attacks should be able to change later behavior when the relevant actor has legitimate knowledge of them. A secret action must not automatically become universal political knowledge.

The approved political model's distinction between superior obligations and independent faction attitudes applies here. A binding arrangement may constrain descendants within its jurisdiction without cloning authoritative treaty state into every child, and violation remains possible and consequential.

Before admission, resolve **Q-11 — Political memory** and only the minimum Q-08 authority/consequence rules required by the chosen interaction. Do not implement an unbounded event log, universal reputation number, exhaustive treaty law, or complete attribution model by default.

---

## Milestone 8 — Canon-Anchored Campaign Bootstrap and Divergent History

### Goal

Start a campaign inside a canon-consistent but already active world, then allow simulation results to produce durable divergence.

### Required architectural proof

- Required canonical facts and starting political conditions are explicit inputs rather than hidden corrections.
- Noncanonical local activity can be generated reproducibly from declared inputs, generation/rules versions, and a versioned random source once randomness has a real consumer.
- Different supported seeds may vary local circumstances without violating required starting facts.
- Optional pre-start warm-up, if justified, uses ordinary Core simulation rather than a second history engine.
- Saving an evolved campaign stores the evolved authoritative snapshot; loading does not regenerate from seed and discard history.
- Later canonical events do not silently overwrite legitimate simulation consequences without an explicit design rule.

Before admission, resolve **Q-12 — Canon divergence and initialization** and the relevant portions of **Q-14 — Compatibility and stochastic behavior**. Choose a campaign era/region only when content work requires it; earlier assistant-proposed dates are not approvals.

---

## Milestone 9 — Persistent Regional Campaign Integration

### Goal

Prove that the earlier systems form one durable game loop before broadening scope further.

### Representative loop

The player should be able to operate across a bounded region through strategic travel, local observation, Engineering tradeoffs, autonomous faction activity, contact/communication, combat or avoidance, and durable political consequences while the world continues to change offscreen.

### Required architectural proof

- Long-running deterministic/scenario tests remain stable over extended simulated time.
- Save/load preserves the meaningful world, knowledge, orders, political state, Engineering state, and scheduled consequences without dangling identity.
- Offscreen actors continue to pursue objectives while the player performs unrelated work.
- Player-visible projections reveal only legitimate knowledge at each scale.
- The loop supports combat and noncombat resolution where the situation permits it.
- Performance work is driven by measured regional scale rather than speculative galaxy-wide infrastructure.

Milestone 9 is the point to judge whether the established joints are strong enough to deepen economy, missions, organizations, crew, additional Engineering systems, broader spatial scales, or more sophisticated AI without architectural rewrites.

---

## Roadmap maintenance rules

- Record **design behavior** on the owning page in `docs/wiki/`; do not turn this roadmap into a competing design specification.
- Record **architecture changes** through the ADR process when an active decision must change.
- Record **implementation truth** in code/tests and reconcile [Implementation Status](docs/wiki/implementation-status.md) when a milestone lands.
- Record **operational/release truth** in [STATUS](docs/STATUS.md), release records, and ADR 0013's branch/release workflow.
- Keep unresolved choices in [Open Design Questions](docs/wiki/open-questions.md) until a concrete consumer requires them.
- Do not silently convert a recommendation, example, mockup, or future milestone description into owner-approved behavior.
- Split, reorder, or rename future slices only through governed refinement that explains the dependency/risk evidence.

## Design references

- [Design wiki](docs/wiki/README.md)
- [Implementation Status](docs/wiki/implementation-status.md)
- [Design Decision Register](docs/wiki/decision-register.md)
- [Strategic Contact Reporting](docs/wiki/strategic-contact-reporting.md)
- [Faction Intent and Autonomous Assignment](docs/wiki/faction-intent-and-autonomous-assignment.md)
- [Open Design Questions](docs/wiki/open-questions.md)
- [Architecture](docs/wiki/architecture.md)
- [Factions and Organizations](docs/wiki/factions-and-organizations.md)
- [Diplomacy, Economy, and Campaigns](docs/wiki/diplomacy-economy-and-campaigns.md)
- [Active ADRs](docs/adr/)
- [Project Status](docs/STATUS.md)
