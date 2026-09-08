---
schema_version: '1.1'
id: 'index-223j5h-decision-register'
title: 'Design Decision Register'
description: 'Stable index of architectural decisions, approved implementation/design directions, political principles, and nonapproved proposals.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-07'
tags:
  - 'design'
  - 'architecture'
aliases: []
related:
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/observation-driven-faction-response.md'
  - 'docs/wiki/factions-and-organizations.md'
  - 'docs/wiki/implementation-status.md'
  - 'docs/wiki/open-questions.md'
---

# Design decision register

[Wiki home](README.md) · [Source catalog](sources.md) · [Open questions](open-questions.md)

## Architectural decisions already adopted

ADRs 0001-0013 remain active architectural constraints. Their complete titles, links, and topic coverage are in [Sources](sources.md). Adoption of an ADR is not proof that every future component or preferred package it discusses has been installed.

The governing themes are pure Core authority; one canonical gate; native-first, demand-driven dependencies; semantic spatial scales; strict ordinary JSON content; explicit versioned snapshot saves; deterministic time/scheduling/randomness; structured nonauthoritative diagnostics; layered tests; explainable information-limited AI; explicit units; subordinate narrative; and development/release branch separation.

## Existing implementation and presentation decisions

**D-01 — Persistent command simulation.** The player is a captain commanding one ordinary ship in an autonomous world; progression is durable consequences, not captain levels. See [Vision](vision-and-scope.md).

**D-02 — Plural world and durable orders.** M1/M2 establish definition/instance/bootstrap separation, stable ship identity, targeted scheduling, and offscreen orders. See [World, navigation, and time](world-navigation-and-time.md).

**D-03 — Information-limited local contact.** M3A establishes observer-local contacts, hidden target correlation, scan, hail, and cautious AI. It does not complete all of M3. See [Sensors, knowledge, and AI](sensors-knowledge-and-ai.md).

**D-04 — Concrete Engineering, not a universal component framework.** M4 owns power, condition, sensor/impulse capability, and one repair. See [Engineering](engineering-and-combat.md).

**D-05 — Persistent native Command Deck and Engineering workspace.** Godot adapts Core projections; the runtime Theme owns visual styling; preview fixtures never become production truth. [Interface](interface-and-player-commands.md) owns the consolidated UI decision, references, and interaction contract.

**D-06 — Independent asset tooling.** AssetCtl is a separate .NET tool with configuration-driven providers, validated assets, provenance, local fallback, and owner-controlled approval. See [Content, assets, and persistence](content-assets-and-persistence.md) and the [full tool contract](asset-pipeline-tool.md).

## Completed sequencing decision — September 6, 2026

**D-07 — Strategic Contact Reporting before faction autonomy.** Selected as the next development slice after v0.4.0 and now implemented in v0.5.0, this extends M3A's local actor-safe contact knowledge into bounded, durable, reference-frame-qualified last-known strategic contact information. It reuses observer-local `SensorContactId` without global known-vessel identity, affiliation/intent learning, faction knowledge sharing, faction runtime, or faction AI. See [Implementation status](implementation-status.md) and [Strategic Contact Reporting](strategic-contact-reporting.md).

D-07 resolved Q-01. The slice is not canonically named `M3B`, did not complete M3, and did not itself begin M5. It left Q-02 through Q-05 unresolved at its completion. The later D-08 through D-13 approvals below select the next slice without retroactively changing D-07's scope.

## Approved faction-assignment decisions — September 6, 2026

All six decisions below are owner-approved. [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) owns their complete meaning and proof. They select the first bounded contribution toward M5, not the entire milestone. Feature #86 / Final PR #87 implements them in `dev` as `0217296` with V7; v0.5.0 remains V6 and no release is claimed.

**D-08 — Era-neutral autonomous-assignment proof.** Two root factions; A has an objective to establish presence at Vesper Reach and at least two controlled NPC ships; B has a ship there. A's deterministic choice changes when the preferred candidate is already committed. Existing orders, travel, and sensors produce an offscreen NPC-NPC consequence without player interaction. Q-05 is resolved for this slice; Q-06 remains open for the eventual campaign.

**D-09 — Single direct controller and bounded assignment authority.** Store an optional direct controlling `FactionId` on the asset side and derive the roster. Only idle, directly controlled NPC ships satisfying existing command preconditions may receive assignments. No preemption, order replacement, voyage interruption, controller transfer, or player-command override is approved. Organization control remains future work; no generic controller/actor abstraction is introduced.

**D-10 — Own-asset administrative knowledge.** Faction policy receives its objective, explicitly known proof-map topology, and only the assignment-relevant identities, strategic states, and order status of directly controlled assets. No unrestricted world/ship state, ship-local sensor knowledge, foreign reports, or automatic affiliation knowledge is exposed. This resolves only the administrative portion of Q-04; Q-02, Q-03, and remaining intelligence sharing stay open.

**D-11 — Closed Ship/Faction work targets and typed bootstrap.** Extend the existing deterministic scheduler with two explicit typed target domains, preserving stable ordering, exact correlation, validation, and budgets. Faction state, controller links, and initial wakes enter through typed bootstrap, not post-construction proof mutations. This implements ADR 0007 without changing its boundary or adding a framework.

**D-12 — V7 and non-inventive migration.** Implemented in `dev`, this uses ordinary validated JSON faction definitions and explicit mutable runtime state. Its adjacent V6→V7 migration produces zero factions, null historical ship controllers, and no faction state or wakes. Existing ship work retains its semantics and ordering as explicitly ship-targeted work. Zero-faction worlds remain valid; no new-game political history is injected into old saves.

**D-13 — No randomness in this policy.** Use deterministic candidates, constraints, selection, tie-breaking, and explanations. Q-14's next migration is selected and this policy consumes no RNG; the eventual versioned random algorithm and later compatibility decisions remain open.

The slice adds no faction/affiliation UI, political hierarchy runtime, organizations, treaties, diplomacy, combat, economy, or intelligence network. Broader political principles remain approved; they are not all required by this first consumer.

## Approved next-development decisions — September 7, 2026

The owner selected [Observation-Driven Faction Response](observation-driven-faction-response.md) as the next bounded slice and selected M6 Tactical Combat Foundation as the next major development family after it. Feature #93 / Final PR #94 implements the selected response contract. Unreleased development uses V8; v0.5.0 remains the released V6 baseline.

**D-14 — Observation must drive faction action before combat.** The next slice closes one information-to-action loop: a legitimate NPC observation produces a bounded delayed report to its direct controlling faction, the received information changes an explainable faction decision, an eligible ordinary NPC ship investigates the reported location, and ordinary local sensing establishes the outcome. This is a bounded M5 contribution and partial Q-04 resolution, not completion of M3/M5 or a general intelligence architecture.

**D-15 — Direct historical reports, not shared live sensors.** The first reporting channel is directly controlled NPC ship → direct controlling faction only. Reports are immutable historical observation snapshots preserving observer provenance and observer-local `SensorContactId`; they do not carry hidden target identity/controller, infer affiliation/intent, or correlate contacts across observers. Delivery is deterministic after 2,000 ms of simulation time. Player, ally, hierarchy, organization, and communications-network propagation remain future work.

**D-16 — One bounded deterministic investigation response.** Factions may investigate a fresh reported strategic location using only received reports, already-approved own-asset administrative facts, and legitimately known routes. The policy never preempts existing orders or commands the player, excludes the reporting observer as its own responder, uses stable report/candidate tie-breaks, allows at most one active investigation per faction, and treats arrival plus ordinary sensing as completion even when the originally observed vessel is gone. The slice caps each faction at 8 in-flight and 16 received reports and uses a 60,000 ms observation freshness window with location-based completion suppression to prevent feedback loops. Existing presence intent is processed first and keeps its one-shot meaning.

**D-17 — Adjacent non-inventive persistence for reported knowledge.** Unreleased development advances V7 to V8 under rules identity `observation-driven-faction-response-v1`. It persists only consequential queued/received knowledge, exact delivery/response continuation, bounded handling state, posture, and required identity continuation. Migration creates no reports, investigations, or delivery work and disables the new posture for migrated factions. New-game bootstrap enables it explicitly for the proof. The derived scheduler bounds are 68,864 stored work items, 68,853 same-instant consequence executions, and 78,853 total consequence executions. Compact V8's tested conservative persistence ceiling is 113,024,376 bytes, below the unchanged 128 MiB envelope. No database or unbounded event history is admitted.

**D-18 — Tactical combat follows this slice; Engineering grows through combat consumers.** M6 first combat engagement is the next major development family. Full M3 or M5 completion is not a prerequisite. The first M6 refinement should compose one bounded directed-energy/shield/targeting/damage/withdrawal interaction with existing sensing, motion, Engineering, AI, and persistence; exact Q-10 mechanics remain open until that refinement. Ship-system depth is added when it creates or materially changes a command decision rather than through an exhaustive pre-combat subsystem catalog.

## Political decisions approved September 6, 2026

The following identifiers provide stable references to the approved political design. The broader model remains future work; the bounded root-faction and direct-control subset is implemented through D-08–D-13. [Factions and organizations](factions-and-organizations.md) owns their full meaning and implementation limits; these summaries do not introduce additional mechanics.

- **P-01 — Autonomous actors at every depth.** Subordinate factions possess independent political will, not just modifiers on a parent.
- **P-02 — Hierarchy does not grant every action.** Legitimate/practical interactions depend on authority and capabilities, not depth alone.
- **P-03 — Formal versus unofficial/covert action.** Lack of formal diplomatic authority does not make every external interaction impossible.
- **P-04 — Consequential covert risk.** Unsanctioned/covert action can produce discovery, attribution, and political consequences; no probability model is approved.
- **P-05 — Political role separate from depth.** A member polity, Great House, and movement can have different roles without different level-specific faction classes.
- **P-06 — At most one structural parent.** Other ties form a separate political relationship graph.
- **P-07 — Independent attitudes.** A child maintains its own posture even when a parent controls formal diplomacy.
- **P-08 — Scoped superior obligations.** Binding arrangements normally apply within jurisdiction, with explicit exceptions possible; violations remain possible and consequential.
- **P-09 — Constitutional relationship semantics.** Autonomy, delegated authority, and obligations primarily describe the parent-child arrangement rather than role alone.
- **P-10 — Three current depths, extensible structure.** Use recursive parentage with an initial three-level limit, not a permanently fixed three-class model; the first bounded assignment proof needs roots only.
- **P-11 — Organizations distinct from factions.** Institutions can act without automatically becoming political constituencies or extra faction depths.
- **P-12 — Specialized organization capabilities.** Organization types can have different resources, goals, capabilities, and actions; catalogs remain deferred.
- **P-13 — One primary organizational association.** An organization has at most one primary owning/chartering faction, possibly none; other ties remain separate.
- **P-14 — Mutable hierarchy, persistent faction identity.** Political status and parentage can change without automatically replacing the historical actor.
- **P-15 — Layered territory/jurisdiction.** Local governance and wider polity jurisdiction can coexist at one location.
- **P-16 — One direct asset controller.** A faction or organization directly controls an asset; wider affiliation/authority follows political relationships. D-09 selects faction-only direct control for the first consumer without implementing organizations.
- **P-17 — Human-readable hierarchy labels.** Polity, Constituent, and Internal are labels, not authority-bearing domain classes.
- **P-18 — Species/culture separate from faction.** Political allegiance is not inferred from species.
- **P-19 — Intermediate political levels optional.** Direct links such as polity to polity-wide movement are allowed without placeholder parents; numeric depth remains derived.
- **P-20 — Alliances/coalitions separate from parentage.** Cooperation does not automatically create a new parent polity.
- **P-21 — Government separate from enduring polity.** A new ruling faction does not automatically create a new state identity.
- **P-22 — Polity interests survive government change.** Rulers influence an autonomous polity constrained by its institutions, obligations, and interests.

P-10/P-17/P-19 must be read together: no mandatory Polity→member-state→movement template and no powers inferred from display labels. Exact presentation of atypical branches remains an open UI detail. D-08's root-only proof is a scope choice, not a replacement of these principles.

## Discussed but not approved

A separate durable `KnownShipId`, cross-observer known-vessel correlation, political affiliation-learning rules, a final campaign year, and a specific random algorithm remain future questions. D-15 now approves only the direct NPC ship-to-direct-faction historical reporting subset; broader report distribution, hierarchy propagation, and intelligence fusion remain unapproved. The closed Ship/Faction scheduler target is approved by D-11; D-17 reuses that boundary rather than adding a generic target registry.

The earlier proposed `M3B→M5→M6` sequence is not the governing plan: D-07 selected Strategic Contact Reporting without canonically naming it M3B, D-08 selected the bounded assignment slice, and D-14 now selects the bounded report-driven response slice before D-18 moves the main development axis to M6. Neither M3 nor M5 must be declared complete before that first combat refinement.

The [open-question register](open-questions.md) retains the unresolved portions. No detailed permission matrix, treaty engine, general political scoring system, economic resource catalog, complete organization taxonomy, or exact combat rules are approved by these decisions.

## Maintaining the register

Keep existing decision IDs stable. When a decision changes, update its owning record and state the supersession or refinement rather than silently altering historical meaning. Use a new ADR when an architectural boundary changes; not every gameplay tuning choice requires one. Implementation claims belong in [Implementation status](implementation-status.md), not in the approval labels above.
