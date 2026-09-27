---
schema_version: '1.1'
id: 'index-223j5h-decision-register'
title: 'Design Decision Register'
description: 'Stable index of architectural records, approved gameplay directions, political principles, and nonapproved proposals.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-27'
tags:
  - 'design'
  - 'architecture'
aliases: []
related:
  - 'docs/adr/README.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/observation-driven-faction-response.md'
  - 'docs/wiki/factions-and-organizations.md'
  - 'docs/wiki/implementation-status.md'
  - 'docs/wiki/open-questions.md'
---

# Design decision register

[Wiki home](README.md) · [ADR catalog](../adr/README.md) · [Source catalog](sources.md) · [Open questions](open-questions.md)

## Architectural decisions already adopted

ADRs 0001–0018 are active architectural records. The [ADR catalog](../adr/README.md) owns their complete navigation and scope map. Adoption does not prove that every conditional future component or preferred package has been installed.

ADRs 0001–0014 cover pure Core authority, one canonical gate, demand-driven dependencies, semantic spatial scales, strict JSON content, versioned snapshots, deterministic time/scheduling/randomness, nonauthoritative diagnostics, layered tests, explainable information-limited AI, explicit units, subordinate narrative, development/release separation, and the extensible bounded ship-system substrate.

### Cross-cutting boundary records — September 27, 2026

The owner selected four supplementary records after the additional-ADR review:

- [ADR 0015](../adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md) owns staged command application, correlated changes, and explicit commitment/failure outcomes. ADR 0007's separately specified safely incremental alternative remains available.
- [ADR 0016](../adr/0016-own-actor-knowledge-and-share-immutable-observation-reports.md) owns information provenance, actor-local ownership, historical transfer, and indirect disclosure. It does not resolve broader vessel identity, affiliation learning, or sharing.
- [ADR 0017](../adr/0017-generate-assets-outside-the-game-through-bounded-validated-publication.md) formalizes D-06's AssetCtl isolation, bounded external operations, recoverable publication, and owner approval.
- [ADR 0018](../adr/0018-separate-simulation-session-lifetime-from-workspaces.md) formalizes D-05's session lifetime and context-bound actions, without prescribing another workspace or permanent ownership by one class.

These records do not renumber the D/P decisions, change detailed gameplay, select the next development slice, or extend the historical 14-ADR conformance result to new obligations. Their owning wiki contracts remain below.

## Existing implementation and presentation decisions

**D-01 — Persistent command simulation.** The player is a captain commanding one ordinary ship in an autonomous world; progression is durable consequences, not captain levels. See [Vision](vision-and-scope.md).

**D-02 — Plural world and durable orders.** M1/M2 establish definition/instance/bootstrap separation, stable ship identity, targeted scheduling, and offscreen orders. See [World, navigation, and time](world-navigation-and-time.md).

**D-03 — Information-limited local contact.** M3A establishes observer-local contacts, hidden target correlation, scan, hail, and cautious AI. It does not complete all of M3. See [Sensors, knowledge, and AI](sensors-knowledge-and-ai.md).

**D-04 — Concrete Engineering, not a universal component framework.** M4 introduced power, condition, sensor/impulse capability, and one repair. D-19 extends that behavior for M6A. ADR 0014 subsequently required an extensible shared substrate while preserving typed domain behavior and rejecting a universal component/ECS framework; Issue #121 implements that migration. See [Engineering](engineering-and-combat.md) and [Ship-system substrate](ship-system-substrate.md).

**D-05 — Persistent native Command Deck and Engineering workspace.** Godot adapts Core projections; the runtime Theme owns styling; preview fixtures never become production truth. [Interface](interface-and-player-commands.md) owns controls and presentation references; ADR 0018 owns the cross-cutting session/action-lifetime boundary.

**D-06 — Independent asset tooling.** AssetCtl is a separate .NET tool with configuration-driven providers, validated assets, provenance, local fallback, and owner-controlled approval. ADR 0017 records the architecture; [Content, assets, and persistence](content-assets-and-persistence.md) and the [full tool contract](asset-pipeline-tool.md) retain their detailed responsibilities.

## Completed sequencing decision — September 6, 2026

**D-07 — Strategic Contact Reporting before faction autonomy.** Selected after v0.4.0 and implemented in v0.5.0, this extends M3A's local contact knowledge into bounded, durable, reference-frame-qualified last-known strategic information. It reuses observer-local `SensorContactId` without global known-vessel identity, affiliation/intent learning, faction sharing, faction runtime, or faction AI. See [Implementation status](implementation-status.md) and [Strategic Contact Reporting](strategic-contact-reporting.md).

D-07 resolved Q-01. The slice is not canonically named `M3B`, did not complete M3, and did not itself begin M5. It left Q-02 through Q-05 unresolved at its completion. D-08 through D-13 later selected the next slice without retroactively changing that scope.

## Approved faction-assignment decisions — September 6, 2026

All six decisions below are owner-approved. [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) owns their complete meaning and proof. They selected the first bounded contribution toward M5, not the entire milestone. Feature #86 / Final PR #87 implements them in `dev` as `0217296` with V7; v0.5.0 remains the historical V6 release and v0.6.0 advances the released format to V8.

**D-08 — Era-neutral autonomous-assignment proof.** Two root factions; A has an objective to establish presence at Vesper Reach and at least two controlled NPC ships; B has a ship there. A's deterministic choice changes when its preferred candidate is already committed. Existing orders, travel, and sensors produce an offscreen NPC-NPC consequence without player interaction. Q-05 is resolved for this slice; Q-06 remains open for the eventual campaign.

**D-09 — Single direct controller and bounded assignment authority.** Store an optional direct controlling `FactionId` on the asset side and derive the roster. Only idle, directly controlled NPC ships satisfying existing command preconditions may receive assignments. No preemption, order replacement, voyage interruption, controller transfer, or player-command override is approved. Organization control remains future work; no generic controller/actor abstraction is introduced.

**D-10 — Own-asset administrative knowledge.** Faction policy receives its objective, explicitly known proof-map topology, and only the assignment-relevant identities, strategic states, and order status of directly controlled assets. No unrestricted world/ship state, ship-local sensor knowledge, foreign reports, or automatic affiliation knowledge is exposed. This resolves only the administrative portion of Q-04; Q-02, Q-03, and remaining intelligence sharing stay open.

**D-11 — Closed Ship/Faction work targets and typed bootstrap.** Extend the existing deterministic scheduler with two explicit typed target domains, preserving stable ordering, exact correlation, validation, and budgets. Faction state, controller links, and initial wakes enter through typed bootstrap, not post-construction proof mutations. This implements ADR 0007 without changing its boundary or adding a framework.

**D-12 — V7 and non-inventive migration.** This slice uses ordinary validated JSON faction definitions and explicit mutable runtime state. Its adjacent V6→V7 migration produces zero factions, null historical ship controllers, and no faction state or wakes. Existing ship work retains its semantics and ordering as explicitly ship-targeted work. Zero-faction worlds remain valid; no new-game political history is injected into old saves.

**D-13 — No randomness in this policy.** Use deterministic candidates, constraints, selection, tie-breaking, and explanations. Q-14's next migration was selected for that slice and this policy consumes no RNG; the eventual versioned random algorithm and later compatibility decisions remain open.

The slice added no faction/affiliation UI, political hierarchy runtime, organizations, treaties, diplomacy, combat, economy, or intelligence network. Broader political principles remain approved; they were not all required by this first consumer.

## Approved next-development decisions — September 7, 2026

The owner selected [Observation-Driven Faction Response](observation-driven-faction-response.md) before its implementation and M6 Tactical Combat Foundation as the next major family after it. Feature #93 / Final PR #94 implements the response contract and v0.6.0 releases it with V8. v0.5.0 remains the historical V6 release.

**D-14 — Observation must drive faction action before combat.** This slice closes one loop: legitimate NPC observation produces a bounded delayed report to its direct controlling faction; received information changes an explainable decision; an eligible ordinary NPC investigates the reported location; ordinary sensing establishes the outcome. It is a bounded M5 contribution and partial Q-04 resolution, not completion of M3/M5 or a general intelligence architecture.

**D-15 — Direct historical reports, not shared live sensors.** The first channel is directly controlled NPC ship → direct controlling faction only. Immutable historical snapshots preserve observer provenance and observer-local `SensorContactId`; they do not carry hidden target identity/controller, infer affiliation/intent, or correlate observers' contacts. Delivery occurs after 2,000 ms of simulation time. Player, ally, hierarchy, organization, and communications-network propagation remain future work.

**D-16 — One bounded deterministic investigation response.** Factions may investigate a fresh reported strategic location using received reports, approved own-asset administrative facts, and legitimately known routes. The policy never preempts orders or commands the player, excludes the reporting observer as its own responder, uses stable tie-breaks, allows one active investigation per faction, and treats arrival plus ordinary sensing as completion even when the observed vessel is gone. The slice caps each faction at 8 in-flight and 16 received reports, with a 60,000 ms freshness window and location-based completion suppression. Presence intent is processed first and keeps its one-shot meaning.

**D-17 — Adjacent non-inventive persistence for reported knowledge.** v0.6.0 advances V7 to released V8 under `observation-driven-faction-response-v1`. It persists consequential queued/received knowledge, exact delivery/response continuation, bounded handling state, posture, and identity continuation. Migration creates no reports, investigations, delivery work, or history and disables the new posture for migrated factions. New-game bootstrap enables it explicitly. Historical V8 bounds were 68,864 stored work items, 68,853 same-instant executions, and 78,853 total executions; compact V8's conservative persistence ceiling is 113,024,376 bytes within 128 MiB. [Persistence](content-assets-and-persistence.md) owns later development formats and bounds. No database or unbounded event history is admitted.

**D-18 — Tactical combat follows this slice; Engineering grows through combat consumers.** In this September 7 decision, first combat was the next major family and Q-10 remained open. Full M3 or M5 completion was not a prerequisite. The first M6 refinement should compose a bounded directed-energy/shield/targeting/damage/withdrawal interaction with existing sensing, motion, Engineering, AI, and persistence. System depth is added when it materially changes a command decision, not through an exhaustive pre-combat catalog. D-19 subsequently resolves M6A's bounded mechanics.

## M6A first-engagement decision — September 26, 2026

**D-19 — Bounded M6A first engagement.** Q-10 is resolved for this implemented interaction through [Feature #111 / Final PR #112](https://github.com/L3DigitalNet/star-trek-alter-course/pull/112): one all-aspect directed-energy/shield interaction uses Current, Identified local contacts; four-consumer allocation; direct subsystem condition loss after absorption; deterministic brownout/reconciliation; one delayed defensive wake; and adjacent V8→V9 persistence. M6A has no RNG, hull pool, recharge, broader geometry/facings, periodic weapon work, faction-driven combat policy, or travel/order preemption. [Engineering and combat](engineering-and-combat.md) owns its detail; M6A remains unreleased toward partial M6.

This is the September 26 first-combat refinement, not a detailed approval made on September 7. D-18's general Engineering direction does not select a post-M6A recovery or tactical slice. The later installed-system migration changes representation, not this historical decision's scope.

## Political decisions approved September 6, 2026

These stable IDs index approved political design. The broader model remains future work; D-08–D-13 implement the root-faction/direct-control subset. [Factions and organizations](factions-and-organizations.md) owns their full meaning and implementation limits.

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

P-10/P-17/P-19 do not prescribe a mandatory Polity→member-state→movement template or powers inferred from labels. Atypical-branch presentation remains open. The root-only proof is a scope choice, not a replacement of these principles.

## Discussed but not approved

A separate durable `KnownShipId`, cross-observer correlation, affiliation learning, final campaign year, and specific random algorithm remain future questions. D-15 approves only direct historical reporting; broader distribution, hierarchy propagation, and intelligence fusion remain unapproved. ADR 0016 preserves those exclusions. D-11's closed Ship/Faction work targets do not authorize a generic target registry.

The earlier proposed `M3B→M5→M6` sequence is not the governing plan. D-07 selected Strategic Contact Reporting without naming it M3B; D-08 selected bounded assignment; D-14 selected response; D-18 moved the main development axis to M6. Neither M3 nor M5 had to be complete first.

[Open questions](open-questions.md) retains unresolved permissions, treaties, political scoring, economy, organizations, recovery, and combat refinements beyond M6A. ADRs 0015–0018 do not answer them or reopen completed substrate implementation.

## Maintaining the register

Keep decision IDs stable. Change an owning record explicitly and describe supersession or refinement instead of silently changing historical meaning. Use an ADR for an architectural boundary, not every tuning choice. Record implementation in [Implementation status](implementation-status.md) and actual verification in the governing PR; approval labels are not test or release evidence.
