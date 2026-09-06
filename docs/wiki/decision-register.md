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

## Approved next-development decision — September 6, 2026

**D-07 — Strategic Contact Reporting is the next development slice.** Before M5 faction autonomy, extend M3A's local actor-safe contact knowledge into bounded, durable, reference-frame-qualified last-known strategic contact information. Reuse observer-local `SensorContactId` as far as it remains sufficient; do not introduce global known-vessel identity, affiliation/intent learning, faction knowledge sharing, faction runtime, or faction AI as part of this slice. The slice is approved design and sequencing, **not implemented gameplay**. See [Strategic Contact Reporting](strategic-contact-reporting.md).

D-07 resolves Q-01. The slice is intentionally not assigned the canonical name `M3B`; historical milestone classification can be decided after implementation. Q-02 through Q-05 remain open for later consumers.

## Political decisions approved September 6, 2026

The following identifiers provide stable references to the decisions approved during the owner discussion. All are **approved design, not implemented**. [Factions and organizations](factions-and-organizations.md) owns their full meaning; these summaries do not introduce additional mechanics.

- **P-01 — Autonomous actors at every depth.** Subordinate factions possess independent political will, not just modifiers on a parent.
- **P-02 — Hierarchy does not grant every action.** Legitimate/practical interactions depend on authority and capabilities, not depth alone.
- **P-03 — Formal versus unofficial/covert action.** Lack of formal diplomatic authority does not make every external interaction impossible.
- **P-04 — Consequential covert risk.** Unsanctioned/covert action can produce discovery, attribution, and political consequences; no probability model is approved.
- **P-05 — Political role separate from depth.** A member polity, Great House, and movement can have different roles without different level-specific faction classes.
- **P-06 — At most one structural parent.** Other ties form a separate political relationship graph.
- **P-07 — Independent attitudes.** A child maintains its own posture even when a parent controls formal diplomacy.
- **P-08 — Scoped superior obligations.** Binding arrangements normally apply within jurisdiction, with explicit exceptions possible; violations remain possible and consequential.
- **P-09 — Constitutional relationship semantics.** Autonomy, delegated authority, and obligations primarily describe the parent-child arrangement rather than role alone.
- **P-10 — Three current depths, extensible structure.** Use recursive parentage with an initial three-level limit, not a permanently fixed three-class model.
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

P-10/P-17/P-19 must be read together: no mandatory Polity→member-state→movement template and no powers inferred from display labels. Exact presentation of atypical branches remains an open UI detail.

## Discussed but not approved

A separate durable `KnownShipId`, cross-observer known-vessel correlation, a particular ship-to-faction report-distribution mechanism, political affiliation-learning rules, a final campaign year, a specific random algorithm, and a closed Ship/Faction scheduler-target implementation remain recommendations or future questions. They are not approved by D-07 or by the political framework.

The earlier proposed `M3B→M5→M6` sequence is no longer the governing planning recommendation: D-07 selects Strategic Contact Reporting as the next slice without canonically naming it M3B. This does not approve the rest of that proposed sequence or imply that Strategic Contact Reporting completes M3.

The [open-question register](open-questions.md) retains the unresolved topics. No detailed permission matrix, treaty engine, political scoring system, economic resource catalog, or complete organization taxonomy was approved in these discussions.

## Maintaining the register

Keep existing decision IDs stable. When a decision changes, update its owning record and state the supersession or refinement rather than silently altering historical meaning. Use a new ADR when an architectural boundary changes; not every gameplay tuning choice requires one. Implementation claims belong in [Implementation status](implementation-status.md), not in the approval labels above.
