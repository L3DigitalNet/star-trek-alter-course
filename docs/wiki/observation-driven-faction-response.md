---
schema_version: '1.1'
id: 'spec-ber4ie-observation-driven-faction-response'
title: 'Observation-Driven Faction Response'
description: 'Approved bounded design for delayed ship-to-faction observation reporting and deterministic investigation assignments.'
doc_type: 'spec'
status: 'active'
created: '2026-09-07'
updated: '2026-09-07'
tags:
  - 'design'
  - 'simulation'
aliases: []
related:
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/sensors-knowledge-and-ai.md'
  - 'docs/wiki/world-navigation-and-time.md'
  - 'docs/wiki/content-assets-and-persistence.md'
  - 'docs/wiki/milestone-proofs.md'
  - 'docs/wiki/open-questions.md'
---

# Observation-Driven Faction Response

[Wiki home](README.md) · [Strategic reporting](strategic-contact-reporting.md) · [Faction assignment](faction-intent-and-autonomous-assignment.md) · [Open questions](open-questions.md)

## Status and purpose

**Approved design, not implemented.** This is the next bounded gameplay slice after Faction Intent and Autonomous Assignment. It connects existing local sensor knowledge to existing faction decision and ship-order machinery without introducing a general intelligence service, political hierarchy, affiliation inference, combat, or communications-network simulation.

The required causal proof is:

> **legitimate ship observation → bounded delayed report → recipient-faction knowledge → explainable investigation decision → ordinary ship order/travel → legitimate local observation on arrival**

A report must actually change a faction decision. A fixed dispatch timer, omniscient faction lookup, debug-only report list, or scripted patrol does not satisfy the slice.

This work resolves only the direct ship-to-direct-faction reporting portion of Q-04 and the provenance needed to carry an observer-local contact reference. It does not create durable known-vessel identity, cross-observer correlation, affiliation/intent knowledge, hierarchy propagation, organizations, allies, or player intelligence feeds.

## Approved decision mapping

The [decision register](decision-register.md) is the stable SSOT for D-14 through D-18; this map links each approved decision to its governing contract.

| Decision | Owning contract |
| --- | --- |
| **D-14** | [Information-to-action proof](#player-visible-and-npc-only-proofs) before combat. |
| **D-15** | [Direct historical reporting](#authority-and-information-boundary) and [publication](#publication-episodes-and-delivery) preserve local provenance. |
| **D-16** | [Bounded deterministic investigation policy](#investigation-policy) and [lifecycle](#investigation-lifecycle-and-feedback-suppression). |
| **D-17** | [Adjacent non-inventive persistence](#persistence-and-migration) preserves consequential continuation. |
| **D-18** | [Engineering/combat sequencing](engineering-and-combat.md) and [milestone proofs](milestone-proofs.md) own M6 sequencing after this slice. |

## Authority and information boundary

Only an NPC ship with a direct controlling `FactionId` may publish through this first channel, and only to that direct controlling faction. The faction must have the observation-response posture enabled by typed new-game bootstrap. The player ship cannot publish through this channel, receive autonomous faction orders, or become a faction intelligence feed.

A report is an immutable historical observation snapshot. It may contain:

- a stable report identity;
- the reporting observer's `ShipInstanceId` as legitimate source provenance;
- the observer-local `SensorContactId`;
- the strategic location/reference frame in which the contact was observed;
- the observed tactical position;
- the observation time; and
- only vessel/design identification already legitimately learned by that observer.

It must not contain or expose the hidden target `ShipInstanceId`, true controller, affiliation, intent, Engineering state, later movement, or any other world truth not present in the observer's contact knowledge. Two observers' local contact identifiers remain unrelated. Matching names are not permission to correlate them.

The recipient faction reads explicit received report snapshots plus its already-approved own-asset administrative facts and known proof-map routes. It does not receive a live union of controlled ships' sensor stores, unrestricted `SimulationState`, foreign `ShipState`, or hidden controller data.

Every otherwise legitimate local contact is eligible for publication, including a contact whose target is in fact controlled by the observer's faction. Publication and receipt must never inspect the hidden target controller or infer affiliation to filter such a contact. The report remains about observed activity at a location, and the recipient still receives no target identity or controller.

## Publication episodes and delivery

An eligible contact publication occurs only when the observer legitimately begins a new local observation episode:

1. the contact first becomes `Current`; or
2. a previously `Lost` contact is legitimately reacquired and becomes `Current` again.

Ordinary `Current` position refreshes, `Current→Stale`, `Stale→Current`, `Stale→Lost`, scan completion, screen changes, save/load, and faction decision wakes do not create another report by themselves. A `Stale→Current` transition continues the same observation episode; only a completed `Lost` transition followed by reacquisition begins a new episode. Initial new-game sensor reconciliation may publish newly created Current contacts. V7→V8 migration must never mine existing contacts for reports that were never sent.

Delivery uses a deterministic **2,000 ms simulation-time delay**. `ObservedAt` remains the source observation time; receipt time is distinct. No wall-clock behavior, random delay, relay topology, range model, jamming, bandwidth economy, or communications-system simulation is introduced.

Report payload lives in bounded authoritative Core state. Scheduled delivery uses the existing closed scheduler with a finite report-delivery work kind targeted to the recipient faction and exact report correlation. The exact in-flight report/work correlation is the sole delivery authority: once that correlation is removed, discarded delivery work cannot recreate the report.

Core uses one shared, objective-independent faction evaluation pass. It is valid when the faction has no presence objective, a satisfied presence objective, or an outstanding pending faction wake. The existing single pending faction-wake slot coordinates the earliest justified future continuation across presence, investigation, and finite own-asset release; cancellation or rescheduling updates the faction correlation and scheduler work atomically.

Delivery is a meaningful faction-decision boundary. Valid report receipts due for one faction at the same simulation instant are delivered in the scheduler's existing due-time then insertion-sequence order and coalesced into one evaluation request. A faction wake due at that instant is coalesced with the same request. Core drains all work due at that instant, including same-instant work created during observation, without adding a work-kind priority. It then runs the coalesced faction passes in ascending `FactionId` order, once per faction at that instant: presence reconciliation and assignment first, investigation response second. Any ordinary work made due at that same instant by the faction passes or their observation reconciliation is drained before time advances, without evaluating any faction a second time at that instant. Publication admission and watermark pruning wait until that final drain settles. This preserves scheduler ordering while preventing same-time insertion order from changing which concern evaluates first, and it creates no additional zero-time polling loop.

If more than eight reports for one recipient faction are simultaneously in flight, no ninth in-flight report is admitted. Whenever eligible publications at one occurrence time exceed the remaining in-flight capacity, deterministic admission is by lowest `(ObserverShipId, SensorContactId)`; declaration/insertion order must not change which reports fill the available slots. A suppressed publication from that observation episode is not retried merely because capacity later becomes available.

## Recipient knowledge, freshness, and bounds

Each faction may retain at most:

- **8 in-flight reports**;
- **16 received report snapshots**;
- **1 active investigation**; and
- **24 sparse location-completion watermarks**, bounded by the at most 8 in-flight plus 16 received witness locations.

A received report is actionable while **`0 <= currentTime - ObservedAt < 60,000 ms`**; at exactly `ObservedAt + 60,000 ms` it is expired. Future observation times are invalid. Expiry is evaluated on receipt and other meaningful faction decision boundaries; it does not require polling. Exact duplicate delivery of the same report identity is idempotent and never creates a second response.

When received retention would exceed 16 after expired/handled entries are removed, retain the 16 newest observations by `ObservedAt`, breaking ties by lowest stable report identity. A report that loses that deterministic retention contest is discarded and cannot later reappear as fresh information. Implementation must preserve enough bounded causal bookkeeping that save/load, cache eviction, or a cooldown boundary cannot turn already-handled information into a new trigger.

The implementation must derive scheduler capacity conservatively from the maximum allowed work shape, including all per-faction report-delivery slots. It must derive both the total consequence-execution budget and the same-boundary execution budget from the maximum reachable work and bounded consequences that can execute in one advancement or become due together. All three limits retain explicit finite-cycle guards; they must not be weakened merely to make a maximum-shape test pass. The maximum-shape proof must combine same-time report deliveries with the existing scheduled-work maximum and must obey real source-authority, player-exclusion, ship, and faction limits rather than constructing an unreachable fixture.

The resulting V8 worst-case scheduler/save shape must remain within the existing 128 MiB save envelope. It may refine internal report constants downward if proof demonstrates that the approved maxima cannot fit, but it must not silently enlarge the envelope or introduce a database/event log to avoid the bound.

## Investigation policy

The only new strategic meaning is **investigate activity at the reported strategic location**. Investigation is not attack, interception of a true target, territorial control, escort, pursuit, or proof of hostile intent.

The pure Core policy considers only fresh, unhandled reports that are not already covered by an equal-or-later completed investigation of that location. It orders actionable reports by:

1. newest `ObservedAt` first;
2. earliest `ReceivedAt` for equal observation time; then
3. lowest stable report identity.

For each report in that order, it evaluates directly controlled NPC candidates. A candidate is rejected when it is the reporting observer for that report, is the player ship, is already committed, lacks a valid current strategic location, or cannot legally reach the reported location under existing travel rules. An eligible ship already at the reported location may satisfy the investigation without a self-travel order.

For a report with eligible responders, selection is deterministic:

1. shortest legal direct-route duration to the reported location; then
2. lowest `ShipInstanceId`.

The result is a typed proposal or an explicit no-action explanation containing candidate/report rejections and the selected rationale. `RandomnessUsed` remains false.

Application revalidates recipient faction, report freshness/handling state, direct controller, non-player authority, idle order state, current strategic location, route legality, and absence of another active investigation. It issues the ordinary ship order/travel mechanism; it does not manufacture a presence objective or widen the existing presence-assignment policy input.

If application revalidation rejects the proposal, the application is atomic: faction state, ship state, scheduler work, and report handling remain identical to their pre-application state, so the report remains unhandled. Core does not try another candidate or report, or reevaluate the rejected proposal, in the same pass. A retry may occur only at a later meaningful existing boundary, including a finite own-asset release, a new valid receipt, or an already-valid faction wake; expiry may instead make the report ineligible.

## Coexistence with existing faction intent

`EstablishPresence` keeps its existing one-shot meaning. A satisfied presence objective never becomes a maintenance objective merely because reporting exists.

At a shared faction decision boundary, existing presence-objective reconciliation/assignment is processed before investigation response, whether the presence objective is absent, satisfied, pending, or newly actionable. Report response then sees the resulting current own-asset commitments, so the same ship cannot receive both assignments. This may allow two different idle ships to receive distinct valid work at one decision boundary; it never preempts an existing order, voyage, hold, patrol, repair, or player command.

Ship-level cautious-contact posture and autonomous tactical motion do not constitute an active strategic order and do not exclude an otherwise eligible responder. Due contact wakes are drained before faction evaluation; ordinary travel clears tactical motion, and any later contact wake retains its existing location/authority checks and cannot override the committed strategic order.

If no report responder is currently available, the faction remains dormant until a meaningful existing boundary such as a finite own-asset release, a new report receipt, or another already-valid faction wake. No periodic faction polling is introduced.

## Investigation lifecycle and feedback suppression

Once an investigation is assigned, its report identity and target location are fixed. Later hidden movement, a newer report, or expiry of the source report does not retarget or cancel the committed voyage. New reports may be retained for later consideration, but there is at most one active investigation per faction.

On arrival, Core performs the ordinary local observation reconciliation at the real destination under the responder's actual sensor capability and actor-local knowledge. The investigation then completes even if the originally observed vessel is gone or undetected. Completion means the faction sent a ship to investigate the historical report location; it does not assert that the system is empty, the original vessel was found, or any identity was correlated.

Completion marks the source report handled and records the faction's latest completed investigation time for that location. A report is response-eligible only when its `ObservedAt` is later than that location watermark. This means an observation produced by the arriving investigator during the same completion instant may be retained as knowledge but cannot immediately dispatch another ship. A later genuinely new observation episode after that watermark may justify a future response. Continuous unchanged contact does not create repeated demand.

A sparse location watermark remains authoritative while any in-flight or received report at that location has `ObservedAt` less than or equal to the watermark, even when the source report expires or is evicted. Completion records the watermark before admitting observation publications generated by the same-instant arrival. Watermark pruning occurs only after same-instant publication admission and received retention have settled, and only when no such witness report remains. Exact in-flight delivery authority prevents discarded work from replaying a removed witness.

If the selected responder was already at the destination, Core performs the same ordinary observation/completion semantics atomically at the decision boundary without constructing a zero-distance `TravelTo` or a zero-time scheduler loop. This is legitimate even when the triggering report describes contact with another ship controlled by that same faction; it remains a location investigation based on actor-local sensing, without revealing or inferring the target's controller.

## Persistence and migration

If current `dev` is still V7 when implementation begins, this slice advances saves to **V8** under a new rules identity `observation-driven-faction-response-v1`. If another governed change has already consumed V8, use the next adjacent version and preserve the same migration semantics.

Persist only consequential authoritative state needed for deterministic continuation:

- faction observation-response posture;
- in-flight report snapshots and exact delivery correlations;
- received report snapshots and bounded handling/suppression state;
- active investigation identity, source report, target location, and responder correlation; and
- any stable report identity allocator required for continuation.

Derived projections, UI formatting, candidate lists, and reconstructible indexes are not persisted.

The adjacent migration from the current development schema is deliberately non-inventive: existing factions receive the response posture **disabled**, empty in-flight/received report state, no active investigation, no report-delivery work, and no invented location-response history. Existing faction objectives, controllers, ship orders, contacts, simulation time, scheduler ordering, and identity continuation remain unchanged. Zero-faction worlds remain valid. Typed new-game bootstrap explicitly enables the response posture for both factions participating in the proof.

Candidate/load validation is atomic and rejects duplicate report identities, missing/unauthorized source or recipient factions, report/source-controller contradictions at publication, impossible observation/receipt times, malformed locations/positions/identification, orphaned or wrong-domain delivery work, active investigation/order mismatches, missing responder/controller relationships, duplicate work, invalid counters, and unjustified missing continuation.

## Player-visible and NPC-only proofs

The era-neutral production/proof scenario must make **both factions independently capable of acting as reporter and responder**. For each faction, typed proof state identifies a directly controlled observer, a different potentially eligible responder, and a legal direct route from that responder to a location legitimately reported by that faction's observer. Multi-hop routing is outside this slice. Reuse existing locations, routes, and ships wherever they satisfy the proof, and add only the minimum content or starting-state changes needed; this contract does not approve particular new ships.

Required paired proof:

- without delivery, no information-driven faction assignment occurs;
- after legitimate delayed delivery, the faction selects an eligible responder;
- each faction independently completes the observer-to-faction-to-responder decision path under its own authority;
- committing the preferred responder changes the selected eligible ship or produces an explicit no-action result;
- the selected ship travels through ordinary strategic travel;
- hidden movement of the originally observed target never changes the queued report or committed destination;
- on arrival, only ordinary local sensing establishes what the responder knows;
- at least one complete reporter→faction→responder travel chain occurs between NPCs without player involvement; and
- same-location completion, including completion following an own-asset contact report, is proved separately and cannot substitute for that complete travel chain or its without-delivery counterfactual.

The player proof uses normal actor-safe consequences. A responder becomes visible only if the player's own sensors legitimately detect it. No faction debug panel, hidden report feed, target true ID, or autonomous-decision overlay is added to ordinary gameplay.

## Acceptance themes

Implementation must prove at the lowest useful layer:

- publication episode semantics, 2,000 ms receipt timing, immutable queued snapshots, provenance, and hidden-truth invariance;
- unauthorized recipient/source rejection and observer-local contact identity remaining uncorrelated;
- delivered-information causality and stable report/candidate tie-breaks for both factions with no RNG;
- authority revalidation, ordinary order reuse, committed/player/foreign-ship exclusion, and coexistence with existing presence intent;
- target-gone arrival, already-at-destination behavior, duplicate/expiry/overflow handling, and feedback suppression;
- save/load before delivery, during travel, and after completion with equivalent deterministic continuation;
- non-inventive migration, zero-faction validity, exact work correlation, declaration-order invariance, and revised maximum-shape bounds;
- long-horizon active scenarios with recurring legitimate new observation episodes but no starvation, oscillation, zero-time loop, unbounded growth, or save/load divergence; and
- Core/Godot authority and player-safe projection remaining intact.

Semantic continuation equivalence is the governing persistence contract. Byte-equality may supplement it where serialization is intentionally canonical; this slice does not create a permanent cross-version bitwise-replay promise.

## Explicit non-goals

This slice does not implement:

- `KnownShipId` or cross-observer vessel correlation;
- affiliation, allegiance, intent, transponder, confidence, deception, or intelligence fusion;
- player-to-faction or faction-to-player reporting;
- hierarchy, organizations, allies, treaties, governments, or jurisdiction propagation;
- communications range, relays, jamming, bandwidth, encryption, or interception;
- preemption, pursuit, escort, attack orders, combat, or political incidents;
- a generic actor/entity/message/event framework; or
- a new faction/political UI.

## Development sequence after this slice

After this bounded information-to-action loop is implemented and reconciled, **M6 Tactical Combat Foundation becomes the next major development family**. M3 and M5 do not need to be declared complete first.

The first M6 slice should be a **first combat engagement** that composes existing movement, sensing, Engineering, AI, persistence, and withdrawal. The intended narrow direction is one directed-energy weapon family, a bounded shield model, sensor-constrained targeting/fire control, meaningful power competition, maneuver/range, operational damage to concrete systems, explainable combat AI, and withdrawal/non-engagement.

Ship-system depth should then grow through **combat-driven Engineering depth**: add a system when it creates or materially changes a command decision, not by building an exhaustive starship subsystem catalog in advance. Detailed EPS topology, batteries, heat/coolant, advanced warp Engineering, life support, crew/repair teams, magazines, boarding, cloaking, and electronic warfare remain deferred until concrete consumers justify them.

Q-10 remains the admission gate for exact combat rules, including shield geometry/facings, firing cadence/eligibility, damage allocation, disengagement, any first random consumer, and involuntary degradation semantics. Combat damage cannot simply reuse voluntary power-allocation rejection when damage forces generation or propulsion below a previously legal operating state; that reconciliation must be explicitly designed before M6 implementation.
