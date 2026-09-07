---
schema_version: '1.1'
id: 'decision-1ejhng-faction-intent-and-autonomous-assignment'
title: 'Faction Intent and Autonomous Assignment'
description: 'Owner-approved first bounded M5 slice: direct NPC assignment, own-asset knowledge, typed scheduling, V7 migration, and deterministic proof.'
doc_type: 'decision'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'design'
  - 'simulation'
  - 'ai'
aliases: []
related:
  - 'docs/wiki/factions-and-organizations.md'
  - 'docs/wiki/decision-register.md'
  - 'docs/wiki/open-questions.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/adr/0005-use-json-and-schema-validation-for-domain-content.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md'
  - 'ROADMAP.md'
---

# Faction Intent and Autonomous Assignment

[Wiki home](README.md) · [Political framework](factions-and-organizations.md) · [Decision register](decision-register.md) · [Open questions](open-questions.md)

## Status, authority, and provenance

**Owner-approved design; not implemented.** On September 6, 2026, after the v0.5.0 repository review and pre-implementation decision discussion, the owner explicitly approved all six recommendations and requested documentation reconciliation through a merged pull request before implementation. This page records that approval; examples and implementation names below do not authorize additional scope.

This is the next bounded development slice after Strategic Contact Reporting and the first selected contribution toward **Milestone 5 — Living Sector and Faction Autonomy**. It is not all of M5, does not complete M3, and does not rename the earlier contact-reporting slice to M3B. Design selection does not mean M5 runtime implementation has started. The current release remains v0.5.0, ship-definition content remains V4, and the implemented save schema remains V6.

The documentation work does not implement a faction, controller, scheduler target, content family, save schema, or UI; does not publish a release; and does not start the later gameplay implementation. The subsequent implementation must use its own governed feature and acceptance evidence.

This page owns the bounded slice. The broader political framework remains authoritative for the principles it touches; the roadmap owns sequence, not additional design. Decisions D-08 through D-13 index the six approvals in the decision register. Existing ADRs already permit these choices; no new or amended ADR is required because no architectural boundary changes.

## Architectural claim

> faction objective → explainable choice → ordinary ship assignment/order → offscreen execution → durable world or actor-knowledge consequence

The faction policy is pure Core code. It receives only its explicitly allowed information, evaluates candidates, returns a typed order proposal or deliberate no-action result with an explanation, and uses the existing validated ship-order/application path. It does not mutate the world during evaluation or create a parallel faction movement simulation.

Strategic Contact Reporting established a useful information boundary before this work. This first faction slice does **not** consume or distribute external contact reports merely to exercise that boundary. It starts with narrowly defined own-asset administrative knowledge; intelligence sharing remains a later consumer.

## D-08 — The approved proof and milestone boundary

Use an **era-neutral two-root-faction proof**, reusing Dawn Anchor, Vesper Reach, and Meridian Drift where practical. No campaign year, canon polity assignment, or starting diplomatic relationship is selected.

Faction A has an objective to establish a ship presence at Vesper Reach and at least two directly controlled NPC ships. Faction B has a ship already at Vesper Reach. The player ship remains outside faction autonomous control in this proof.

In the baseline, A's policy evaluates at least two eligible candidates and chooses one deterministically. In a comparison scenario, the otherwise preferred ship is already committed, so the same policy rejects it and selects the other eligible ship. This commitment constraint is the selected proof of actual choice; a fixed patrol with faction labels is insufficient.

The selected ship receives an ordinary existing `TravelTo` order and travels offscreen. On arrival, geometry and existing sensor rules must permit a legitimate NPC-NPC contact consequence involving B's ship while the player does nothing. The consequence must exist in authoritative world or actor-knowledge state, not only in a log. It must not automatically reach A's faction policy or the player. Establishing presence means a ship is there; it does not grant territory, jurisdiction, ownership, treaty rights, or political influence.

The first slice proves bounded assignment plus an offscreen interaction. It does not claim to satisfy the full roadmap's broader two-actor strategic-autonomy, political-choice, or living-sector exit criteria. Later M5 work remains necessary; do not silently enlarge this proof to complete the entire milestone.

Q-05 is resolved for this slice. Q-06 is scoped to an era-neutral proof, not resolved for the eventual campaign.

## D-09 — Direct control and assignment authority

A participating NPC ship has zero or one direct controlling `FactionId`. Keep the authoritative relationship on the asset side, conceptually an optional direct-controller faction reference on `ShipState`; derive the faction's roster from that relationship. Do not maintain a second independently mutable controlled-ship list as another authority.

A missing controller means no direct faction controller is modeled. It does not assert neutral allegiance, independence, or a historical political fact. A non-null reference must resolve to an existing faction. The faction's stable identity is separate from display names, political labels, and content paths.

For this slice, only factions are supported direct controllers. Organizations remain distinct future actors; do not add an organization placeholder, a generic `ActorId` or `ControllerId`, or a faction/organization union before an organization consumer exists. This deliberately narrow representation does not redefine the broader framework's future organization-control model.

A faction may assign only an **idle, directly controlled NPC ship** satisfying the existing order and travel preconditions. An already committed ship is ineligible. The feature may not steal, cancel, replace, reprioritize, or preempt an existing order, interrupt a physical voyage, or overwrite player commands. Existing cancellation functionality is not new faction authority to use it.

The player ship is excluded from faction autonomous assignment in the proof. Player allegiance, Starfleet command versus captain authority, delegated command, controller transfer, and broader political authorization remain deferred. This is only the direct-controller-to-idle-NPC portion of Q-08, not an authority matrix.

## D-10 — Own-asset administrative knowledge only

A faction decision snapshot may contain its objective, the explicitly known proof-map topology, and the assignment-relevant administrative facts of its directly controlled assets: their identities, current strategic states, and existing assignment/order status. This scoped own-asset visibility is approved; ownership by itself is not a general permission to read every field in a ship or the world.

Construct a bounded, immutable, actor-specific input. Do not pass `SimulationState`, unrestricted `ShipState`, the player's projection, a hidden-world accessor, or a convenience object that can traverse to foreign state into the policy. Do not silently add Engineering telemetry as decision knowledge; the committed-versus-idle comparison supplies this slice's required constraint, and ordinary command validation still enforces actual prerequisites.

The snapshot contains no ship-local sensor contacts or scans, external last-known reports, hidden enemy positions or orders, foreign controller identity, learned affiliation, or intelligence belonging to another faction. A faction can refer to its own real `ShipInstanceId` without exposing that identity to external observers or introducing `KnownShipId`.

This is a **partial, slice-local resolution of Q-04**. Current own-asset administrative visibility is allowed for assignment; it is not approval of instantaneous sensor sharing, fleet omniscience, an intelligence network, a report transport/delay model, or political hierarchy propagation. Q-02, Q-03, and the remaining Q-04 questions stay open.

Decision explanations are available to Core tests and diagnostics under ADR 0010. They are not automatically player-facing, a source of political memory, or authoritative faction knowledge.

## D-11 — Closed ship/faction scheduling and typed bootstrap

Extend scheduled-work targeting to a closed typed **Ship | Faction** representation. The exact C# spelling is an implementation choice; the selected semantics are two explicit domains, not `object`, an untyped integer/string target, a generic actor/entity registry, `ISchedulable`, or a universal target framework.

Persist target kind with its correctly typed identity. Validate that each target resolves to the matching aggregate member and that the known work kind and exact correlation are valid for that target. Existing ship consequences must preserve their ship meaning; a numerically equal faction identity cannot make a ship target resolve to a faction or vice versa.

Reuse the existing simulation clock, stable work identities, persisted same-time sequence, cancellation/correlation rules, serialized mutation, and bounded advancement. Same-time ordering must not come from target-kind enum order or collection iteration. Faction decisions wake at meaningful strategic boundaries, not in a separate loop polling every faction at tactical frequency. Define bounded reevaluation/no-action behavior without zero-time loops or assignment churn.

Faction starts, direct controller links, consequential initial faction state, and decision wakes belong in typed bootstrap and complete candidate validation. Do not add post-construction proof-only `BootstrapHiddenFaction...` mutations or serialize executable callbacks. No generalized bootstrap/scenario language is admitted.

This implements ADR 0007's existing typed-target requirements; it does not supersede that ADR. Root factions are sufficient for the proof. The approved eventual recursive parentage model remains unchanged, but parent/child runtime and hierarchy traversal are not required here.

## D-12 — Content, durable state, and the planned V7 migration

Reusable production faction definitions use strict JSON, schema validation, stable content identity, reference resolution, and semantic validation under ADR 0005. Keep immutable authored definition data separate from mutable faction state, objectives/commitments that affect continuation, direct ship control, and scheduler correlations. Do not put changing direct control into a reusable ship-class definition.

The implementation is approved to introduce **save V7**, retaining the adjacent V1 → V2 → V3 → V4 → V5 → V6 → V7 chain. V7 is planned, not the current runtime format. Persist the consequential faction state, direct controller relationships, and exact typed scheduled work needed for deterministic continuation; derive rosters, projections, and caches rather than making them parallel durable authorities.

The V6 → V7 migration must produce an empty faction collection, null direct-controller references for all historical ships, and no faction decision state or faction-targeted work. Existing scheduled work becomes explicitly ship-targeted while retaining its identities, due times, sequence, correlations, and allocator continuation. Preserve all existing ship/world/knowledge state. Do not assign historical vessels to new-game factions or invent political history, objectives, reports, or affiliations.

A valid world must support **zero factions**. New-game/bootstrap content may introduce the proof's factions; loading a migrated campaign does not rerun new-game initialization. Null historical controller references express absence of modeled control, not newly learned political information.

Load a complete validated candidate before replacing live state; failure leaves the running simulation unchanged. Validate bounds, missing/wrong-domain references, duplicate identities, work ownership, counters, and compatible definitions. Recheck maximum-shape save/work bounds when adding faction data; do not assume the existing 128 MiB envelope has unlimited room or silently increase it.

This approval preserves development-save compatibility through the new adjacent migration. It does not promise indefinite support for all later pre-1.0 formats or prescribe speculative faction, organization, or intelligence schemas.

## D-13 — No randomness in the first faction policy

Use explicit candidates, hard constraints, deterministic scoring or priority, stable tie-breaking, and a structured explanation under ADR 0010. The policy consumes no randomness. No random source, stream state, algorithm choice, or probabilistic intelligence is required by this slice.

Q-14 is therefore scoped in two ways: the next save migration is selected, and this policy is non-stochastic. The eventual fixed/versioned random algorithm remains open until a real random consumer requires it under ADR 0007. Do not introduce randomness merely to break a candidate tie.

## Presentation and non-goals

No faction, affiliation, political hierarchy, or intelligence UI is added. Existing player-safe maps, contacts, Engineering controls, save/load, and event advancement remain available. Godot may adapt to the new save format when implemented, but it receives no faction diagnostic view or hidden NPC decision stream. A new offscreen consequence is not automatically a player-relevant event.

Do not implement global known-vessel identity, cross-observer correlation, report sharing, affiliation/intent learning, political attitudes, treaties, combat, diplomacy, organizations, governments, parent/child factions, layered jurisdiction, territory ownership, political resources/economy, canonical campaign generation, RNG, or a generic actor/rules framework. Do not select a complete organization taxonomy or complete M3 merely to label this slice finished.

## Required implementation acceptance evidence

These are requirements for the later gameplay feature, not tests executed by this documentation change.

| Proof | Required evidence |
| --- | --- |
| Actual autonomous choice | Same objective with two eligible ships; a committed preferred ship changes the selected valid candidate, with explicit rejection and tie-breaking explanations. |
| Offscreen causal consequence | Ordinary assignment and travel produce a legitimate NPC-NPC contact/world consequence without player interaction or a scripted substitute for Core rules. |
| Control boundary | Foreign, uncontrolled, player-controlled, already committed, and otherwise invalid targets cannot receive an unauthorized assignment; rejection causes no partial state mutation. |
| Knowledge boundary | Vary hidden foreign truth and ship-local contact knowledge while keeping permitted faction facts fixed; the pure decision and explanation remain unchanged. |
| Typed scheduler | Mixed Ship/Faction work preserves stable same-time ordering, exact correlation, and cancellation; wrong-domain, missing, and mismatched targets fail closed. |
| Persistence | V7 round-trip and interrupted/continued scenario equivalence; V6 migration adds no political state, preserves ship work, and remains valid with zero factions; the supported adjacent chain passes. |
| Bounded behavior | Deterministic long-horizon coverage for zero-time loops, starvation, oscillation, reassignment churn, dangling references, work growth, and maximum input/save shape. |
| Existing boundaries | Core remains independent of Godot; insertion/construction order cannot change semantic outcomes; no RNG or forbidden framework/dependency appears. |
| Player regression | Existing production projection, input, save/load, and player-event behavior remain safe; no hidden faction information or preview truth enters the UI. |

Run the canonical gate and appropriate focused negative, persistence, scenario, architecture, and Godot regression checks on the actual implementation. Update implementation status only after that work lands. Documentation approval alone satisfies none of the runtime evidence above.

## Sources

This page records the owner's September 6, 2026 approval in the repository-review follow-up; the documentation PR is its execution and review record. [Open questions](open-questions.md) records scoped resolutions, and [Decision register](decision-register.md) preserves stable decision IDs.

Existing constraints come from [Factions and organizations](factions-and-organizations.md), [Strategic Contact Reporting](strategic-contact-reporting.md), [content ADR 0005](../adr/0005-use-json-and-schema-validation-for-domain-content.md), [save ADR 0006](../adr/0006-use-versioned-json-snapshot-saves.md), [time ADR 0007](../adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md), and [AI ADR 0010](../adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md).
