---
schema_version: '1.1'
id: 'index-223j5h-decision-register'
title: 'Design Decision Register'
description: 'Stable index of architectural decisions, approved implementation/design directions, political principles, and nonapproved proposals.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'design'
  - 'architecture'
aliases: []
related:
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
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

**D-05 — Persistent native Command Deck and Engineering workspace.** Godot adapts Core projections; the runtime Theme owns visual styling; preview fixtures never become production truth. See [Interface](interface-and-player-commands.md) and the [owning UI decision](../design/command-deck-ui.md).

**D-06 — Independent asset tooling.** AssetCtl is a separate .NET tool with configuration-driven providers, validated assets, provenance, local fallback, and owner-controlled approval. See [Content, assets, and persistence](content-assets-and-persistence.md) and the [full specification](../specs/asset-pipeline-tool.md).

**D-07 — Strategic Contact Reporting before faction autonomy.** Selected after v0.4.0 and implemented in v0.5.0, this slice extends M3A's local actor-safe contact knowledge into bounded, durable, reference-frame-qualified last-known strategic information. It reuses observer-local `SensorContactId` without global known-vessel identity, affiliation/intent learning, faction sharing, faction runtime, or faction AI. See [Strategic Contact Reporting](strategic-contact-reporting.md) and [Implementation status](implementation-status.md).

D-07 resolves Q-01. The slice is not canonically named `M3B`, does not complete M3, and did not itself begin M5. Q-02 through Q-05 were still open at its release; D-08 through D-13 below record the later approved next-slice refinement rather than retroactively changing D-07's scope.

## Next-slice decisions approved September 6, 2026

All six decisions below are **approved design, not implemented**. [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) owns their complete behavior and acceptance contract. They apply existing ADRs 0005, 0006, 0007, and 0010; they introduce no new ADR or generic framework.

**D-08 — First bounded M5 faction-assignment proof.** Resolve Q-05 for an era-neutral two-root-faction scenario in the existing proof region. Faction A chooses an eligible controlled NPC ship to establish presence at Vesper Reach, where Faction B has a ship. A committed-preferred-ship variant changes the selected ship; a no-eligible-ship variant returns explained no-action. Ordinary offscreen travel and ship-local sensing produce a durable NPC-NPC consequence. This selects the next design after v0.5.0, not the full M5 exit condition or campaign era.

**D-09 — One authoritative direct-controller link, bounded assignment authority.** A participating ship has zero or one direct controlling `FactionId`; the authoritative link is asset-side and rosters are derived. Factions may assign only their idle, directly controlled NPC ships, without preempting an existing order or physical travel. The player is outside autonomous faction control in this proof. Organization controllers, political hierarchy, and controller transfers remain future consumers.

**D-10 — Narrow own-asset administrative knowledge.** Partially resolve Q-04 only for current controlled-asset identity, strategic state, and assignment/order status, supplied through an immutable faction input alongside its own objective and known topology. No ship-local contacts, scans, external reports, enemy truth, or other faction objectives are granted by this rule. Own-asset real identity does not create `KnownShipId`; controller truth does not reveal affiliation. Q-02/Q-03 and the rest of Q-04 remain open.

**D-11 — Closed Ship/Faction scheduler targets.** The first faction wake justifies extending existing scheduled work to a typed Ship or Faction target with exact correlation, stable ordering, and target-kind validation. Reuse the existing scheduler under ADR 0007, with bounded meaningful decision wakes rather than a tactical-frequency faction loop. No generic Actor/Entity target or scheduler framework is approved.

**D-12 — Planned V7 compatibility and typed political bootstrap.** The implementation plans V7 and adjacent V1→V7 support. V6→V7 creates zero factions, null controller links, no faction decision state/wakes, and preserves equivalent typed Ship work with existing IDs/timing/order. Zero-faction worlds stay valid. New political state and wakes enter through typed bootstrap, not postconstruction mutation or load-time new-game seeding. V6 and ship-definition content V4 remain current until implementation; reusable faction content follows ADR 0005.

**D-13 — No randomness or new political presentation in the first slice.** Policy evaluation is deterministic, bounded, information-limited, and explainable under ADR 0010. No RNG algorithm/stream is introduced; the stochastic part of Q-14 stays open. The proof uses era-neutral actors and no new faction/affiliation UI. Diagnostics are not player knowledge or political memory. Headless acceptance does not require a political dashboard.

## Political decisions approved September 6, 2026

The following identifiers provide stable references to the decisions approved during the owner discussion. All are **approved design, not implemented**. [Factions and organizations](factions-and-organizations.md) owns their full meaning; these summaries do not introduce additional mechanics. D-08 through D-13 select only the first bounded consumer of this broader framework.

- **P-01 — Autonomous actors at every depth.** Subordinate factions possess independent political will, not just modifiers on a parent.
- **P-02 — Hierarchy does not grant every action.** Legitimate/practical interactions depend on authority and capabilities, not depth alone.
- **P-03 — Formal versus unofficial/covert action.** Lack of formal diplomatic authority does not make every external interaction impossible.
- **P-04 — Consequential covert risk.** Unsanctioned/covert action can produce discovery, attribution, and political consequences; no probability model is approved.
- **P-05 — Political role separate from depth.** A member polity, Great House, and movement can have different roles without different level-specific faction classes.
- **P-06 — At most one structural parent.** Other ties form a separate political relationship graph.
- **P-07 — Independent attitudes.** A child maintains its own posture even when a parent controls formal diplomacy.
- **P-08 — Scoped superior obligations.** Binding arrangements normally apply within jurisdiction, with explicit exceptions possible; violations remain possible and consequential.
- **P-09 — Constitutional relationship semantics.** Autonomy, delegated authority, and obligations primarily describe the parent-child arrangement rather than role alone.
- **P-10 — Three design depths, extensible structure.** Use recursive parentage with an initial three-level limit when hierarchy is implemented, not a permanently fixed three-class model.
- **P-11 — Organizations distinct from factions.** Institutions can act without automatically becoming political constituencies or extra faction depths.
- **P-12 — Specialized organization capabilities.** Organization types can have different resources, goals, capabilities, and actions; catalogs remain deferred.
- **P-13 — One primary organizational association.** An organization has at most one primary owning/chartering faction, possibly none; other ties remain separate.
- **P-14 — Mutable hierarchy, persistent faction identity.** Political status and parentage can change without automatically replacing the historical actor.
- **P-15 — Layered territory/jurisdiction.** Local governance and wider polity jurisdiction can coexist at one location.
- **P-16 — One direct asset controller.** A faction or organization directly controls an asset; wider affiliation/authority follows political relationships.
- **P-17 — Human-readable hierarchy labels.** Polity, Constituent, and Internal are labels, not authority-bearing domain classes.
- **P-18 — Species/culture separate from faction.** Political allegiance is not inferred from species.
- **P-19 — Intermediate political levels optional.** Direct links such as polity to polity-wide movement are allowed without placeholder parents; numeric depth remains derived.
- **P-20 — Alliances/coalitions separate from parentage.** Cooperation does not automatically create a new parent polity.
- **P-21 — Government separate from enduring polity.** A new ruling faction does not automatically create a new state identity.
- **P-22 — Polity interests survive government change.** Rulers influence an autonomous polity constrained by its institutions, obligations, and interests.

P-10/P-17/P-19 must be read together: no mandatory Polity→member-state→movement template and no powers inferred from display labels. Exact presentation of atypical branches remains an open UI detail. The first root-only slice does not require hierarchy runtime or revoke these principles.

## Discussed but not approved

A separate durable `KnownShipId`, cross-observer known-vessel correlation, a ship-to-faction sensor-report distribution mechanism, affiliation-learning rules, a final campaign year, and a specific random algorithm remain future questions. The closed Ship/Faction scheduler target is now approved by D-11, and own-asset administrative knowledge is narrowly approved by D-10; neither approval supplies the deferred intelligence mechanisms.

The earlier proposed `M3B→M5→M6` package is not the governing plan. D-07 selected and delivered Strategic Contact Reporting; D-08 now selects the bounded faction-assignment slice. Neither decision implies that M3 is complete or that the first assignment completes all of M5.

The [open-question register](open-questions.md) retains the unresolved topics and scoped partial answers. No detailed political permission matrix, treaty engine, economy/resource catalog, complete organization taxonomy, or new faction/affiliation UI is approved for the first faction slice.

## Maintaining the register

Keep existing decision IDs stable. When a decision changes, update its owning record and state the supersession or refinement rather than silently altering historical meaning. Use a new ADR when an architectural boundary changes; not every gameplay tuning choice requires one. Implementation claims belong in [Implementation status](implementation-status.md), not in the approval labels above.
