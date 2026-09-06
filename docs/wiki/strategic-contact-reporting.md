---
schema_version: '1.1'
id: 'decision-q4p7ns-strategic-contact-reporting'
title: 'Strategic Contact Reporting'
description: 'Owner-approved next development slice connecting local contact knowledge to durable strategic last-known information without introducing faction runtime.'
doc_type: 'decision'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'design'
  - 'simulation'
  - 'sensors'
  - 'knowledge'
aliases: []
related:
  - 'docs/wiki/sensors-knowledge-and-ai.md'
  - 'docs/wiki/open-questions.md'
  - 'docs/wiki/decision-register.md'
  - 'docs/wiki/factions-and-organizations.md'
  - 'ROADMAP.md'
  - 'docs/adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md'
source:
  - 'https://github.com/L3DigitalNet/star-trek-alter-course/issues/74'
  - 'https://github.com/L3DigitalNet/star-trek-alter-course/pull/78'
---

# Strategic Contact Reporting

[Wiki home](README.md) · [Sensors, knowledge, and AI](sensors-knowledge-and-ai.md) · [Open questions](open-questions.md)

## Status and decision

**Implemented** (Feature #77, Final PR #78, merged into `dev` as `80c3084`; released in v0.5.0). After reviewing the v0.4.0 implementation, the roadmap, the current sensor/AI code, and the approved political model, the owner selected **Strategic Contact Reporting** as the next bounded development step.

This resolves the sequencing question in Q-01. The slice is intentionally a bridge between M3A's local observer knowledge and M5's later living-sector/faction autonomy. It should remove the largest information-model ambiguity before faction AI is introduced, while avoiding premature commitment to a complete intelligence system or political runtime.

Do **not** canonically call this slice `M3B` yet. It may later be described as a continuation of Milestone 3, a prerequisite to Milestone 5, or another historical subdivision once implementation evidence exists. The approved name for the work itself is **Strategic Contact Reporting**.

### Implementation outcome

The runtime chose the smallest representation that satisfies the required behavioral boundary below:

- `SensorContactTrack` gained a `LocationId? ObservedAtLocationId` frame: the strategic location the observation was recorded in. A fresh observation always carries one (non-nullable plumbing, required positional parameter); the frame is null only for a contact migrated from a pre-V6 save.
- `StrategicProjection.KnownContactReports` exposes `StrategicContactReportProjection`, one per retained contact still legitimately qualified by an observation location: contact id, observed-at `LocationId`, last observed tactical position, last observed time, retained status (Current, Stale, or Lost), identification, and the learned vessel/design display names. It omits the hidden `ShipInstanceId` and omits any legacy contact with no qualifying frame.
- Save schema advanced to V6 with simulation rules identity `strategic-contact-reporting-v1`. The adjacent chain stays V1→V6; the new V5→V6 hop sets every legacy contact's frame to null and derives nothing, so a migrated contact stays on the tactical surface without appearing in strategic reports until it is observed again post-migration.
- The Command Deck strategic inspector presents the reports in a "LAST KNOWN CONTACTS" telemetry section; the tactical surface is unchanged and still drops Lost contacts.
- A headless Core-only Pathfinder/Kestrel scenario proves the full seam without Godot: observe at Dawn Anchor, go Stale/Lost, hidden NPC travel leaves the report unchanged, player travel leaves it unchanged, save/load equivalence holds, and reacquisition updates it.

Q-02, Q-03, Q-04, and Q-05 remain open; this slice did not need to resolve them.

## Why this comes next

M3A established a sound local knowledge boundary:

- each ship owns bounded observer-local sensor knowledge;
- sensor contacts have observer-local `SensorContactId` identity;
- Core may retain hidden `ShipInstanceId` correlation for rule resolution;
- actor-safe projections omit that hidden target identity;
- identification currently means learned vessel/design display names, not political affiliation or intent;
- Current, Stale, Lost, and reacquired contact states already survive through the local contact model.

M5 will eventually require a faction or political actor to make strategic decisions from information it is legitimately allowed to know. Jumping directly to M5 would force one feature to invent faction identity, faction-owned knowledge, reporting/sharing, faction scheduling, political affiliation, strategic decision logic, assignment generation, and persistence at once.

The next slice therefore proves a narrower causal seam first:

```text
local tactical observation
        ↓
durable reference-frame-qualified last-known information
        ↓
actor-safe strategic report/projection
        ↓
later faction knowledge sharing and strategic decisions
```

The final arrow is deliberately deferred to M5 or another later governed slice.

## Goal

Prove that a ship can convert legitimate local observations into durable, strategically meaningful last-known information that remains useful after the contact is no longer current, **without gaining or exposing hidden world truth**.

The player should be able to leave a tactical encounter and retain an accurate statement of what was actually observed: for example, that an identified vessel was last seen at a particular strategic location, at a particular tactical position, at a particular simulation time. Later hidden movement must not silently update that knowledge.

## Required behavioral boundary

### Reference-frame-qualified observations

A tactical position is meaningful only inside the strategic location/reference frame where it was observed. The slice must therefore make the last-known observation self-contained enough to answer both:

- **where in strategic space** the contact was observed; and
- **where in local tactical space** it was observed within that strategic location.

The exact runtime representation is an implementation decision, but the authoritative observation must not become ambiguous after either the observer or target leaves the location.

A previously observed contact should continue to mean, conceptually:

> Contact 1 / Survey Vessel Kestrel was last observed at Dawn Anchor, tactical position `(x, y)`, at simulation time `T`.

If Kestrel later moves elsewhere without being observed again, that report remains unchanged.

### Strategic contact/report projection

The implementation should expose a small, actor-safe strategic report or projection derived from legitimate observation knowledge. The exact type name is not prescribed. It should contain only facts the observer has actually learned, such as:

- source/observer identity where appropriate to the API boundary;
- observer-local contact ID;
- strategic location/reference frame of the observation;
- last observed tactical position;
- observation simulation time;
- current retained contact status where meaningful;
- identification state;
- known vessel display name, if learned;
- known design display name, if learned.

The report must **not** expose the true target `ShipInstanceId` through actor-safe/player-facing APIs merely because Core internally correlates the contact to a ship.

### Lost contacts remain legitimate knowledge

A contact becoming Lost means the observer no longer has a current sensor track. It does not erase the historical fact that the contact was previously observed.

Strategic presentation may therefore retain bounded last-known information for a Lost contact when the underlying knowledge model still legitimately retains it. This slice does not define permanent historical intelligence retention; it uses the existing bounded contact knowledge unless concrete implementation evidence requires a narrower supporting structure.

### Hidden truth does not backfill knowledge

After an observation is made, later authoritative changes to the target's position, strategic location, engineering condition, affiliation, order, or other hidden state must not mutate the observer's prior report unless a new legitimate observation or information source updates it.

Likewise, moving the observing ship must not reinterpret the previous tactical coordinates inside the observer's new location.

## Identity decision: reuse existing observer-local contact identity

This slice does **not** approve a new durable `KnownShipId` or global known-vessel identity.

Use the existing observer-local `SensorContactId` as far as it remains sufficient for this feature. That supports statements such as:

> Pathfinder / Contact 1 — last observed at Dawn Anchor — identified as Survey Vessel Kestrel.

The slice does not need to solve cross-observer correlation, reports about a vessel never personally observed, or whether two observers' reports refer to the same unknown vessel. Q-02 remains open for the first consumer that genuinely needs those semantics.

If implementation evidence proves that the approved player-visible proof cannot be implemented correctly without a distinct identity concept, stop and return to governed design refinement rather than silently adding one.

## Affiliation and intent remain unresolved

This slice does **not** change the meaning of sensor identification and does not approve political affiliation or intent as scan results.

Scanning currently establishes vessel/design identity only. Do not infer that a successful scan automatically reveals faction, organization, government, allegiance, intent, or controller.

Q-03 remains open. A later feature must deliberately select which information source establishes affiliation—communications, transponder data, prior reports, intelligence sharing, recognition, or another justified mechanism—and what the observer actually learns.

The first M5 strategic decision can legitimately react to an unknown or merely identified vessel report; political affiliation knowledge is not required for this slice to provide useful architectural value.

## Faction knowledge sharing remains deferred

This slice records information **for the observing ship/player knowledge boundary**. It does not establish how a faction receives reports from ships or organizations, whether reporting is automatic or delayed, how information is merged, or what the player receives from allied actors.

Q-04 therefore remains open. The report/projection created here should be a plausible future input to a sharing mechanism, but it must not become an unreviewed faction intelligence network.

## Player-visible proof

Use the existing Pathfinder/Kestrel contact scenario where practical rather than inventing unrelated content merely for this slice.

A representative acceptance flow is:

1. Pathfinder detects Kestrel through the existing local sensor model.
2. Pathfinder performs the existing identification path and learns only the facts that M3A currently permits.
3. The legitimate observation is recorded against its strategic location/reference frame.
4. Kestrel later becomes Stale/Lost or departs local tactical observation.
5. Pathfinder travels elsewhere or otherwise changes strategic context.
6. The tactical display no longer presents Kestrel as a current contact.
7. A strategic known-activity/report surface can still state that the contact was last observed at the original strategic location and time, with only the identity facts actually learned.
8. Kestrel moves again in hidden world truth.
9. The player's retained report does not change until legitimate new information arrives.
10. Save/load produces the same actor-visible report semantics.

The presentation can be deliberately small. The architectural proof is the durable actor-safe information boundary, not a polished intelligence UI.

## Persistence and compatibility

Follow ADR 0006. Persist authoritative meaning, not presentation caches or duplicated truth.

If the approved behavior requires new durable state that cannot be reconstructed safely from the current V5 snapshot, advance the save schema through the normal adjacent migration process. The implementation used a V5→V6 adjacent migration under rules identity `strategic-contact-reporting-v1`: a migrated legacy contact carries no reference frame and is omitted from strategic reports until a new qualifying observation is recorded.

Any migration must create only facts legitimately derivable from the older snapshot. It must not invent:

- faction affiliation;
- historical reports that V5 never stored;
- cross-observer vessel correlation;
- intelligence-sharing history;
- treaty/political state;
- target movement that occurred outside stored observation state.

If a new stored field is merely a cache derivable unambiguously from other authoritative persisted values, prefer derivation over duplication.

## Required test themes

The implementation should include focused unit, persistence, projection, scenario, and negative tests for at least the following behaviors:

- two observers can retain different knowledge about the same authoritative ship;
- changing hidden target truth while holding an observer's report constant does not change actor-visible output;
- a Lost contact can retain legitimate last-known strategic observation information;
- moving the target after observation does not update the old report;
- moving the observer after observation does not change the old observation's reference frame;
- save/load preserves equivalent report semantics;
- no actor-safe or ordinary player-facing API exposes hidden target `ShipInstanceId` correlation;
- an unobserved ship never appears in the observer's report set;
- identification adds only facts actually learned by the existing rule;
- bounded/canonical ordering and validation remain enforced;
- existing cautious-contact AI remains information-limited and does not gain a new truth-access path;
- large-step/small-step equivalence is asserted only where existing simulation rules guarantee it.

A headless scenario should demonstrate the complete proof without requiring Godot. Godot-facing tests should cover only the presentation/input behavior added for the player-visible report surface.

## Explicit non-goals

Strategic Contact Reporting does **not** add or approve:

- faction runtime state;
- organization runtime state;
- `FactionId` solely for this feature;
- ship political affiliation/controller fields;
- `KnownShipId` or global known-vessel identity;
- faction knowledge sharing or report distribution;
- faction strategic AI;
- faction-owned scheduler targets;
- affiliation/intent learning rules;
- strategic long-range sensor simulation;
- confidence/probability/measurement-error systems;
- intelligence networks;
- treaty, reputation, diplomacy, government, or jurisdiction mechanics;
- random-number consumption merely to make sensing less certain;
- combat;
- a generic actor/entity/rules framework;
- an event bus, ECS, database, or service architecture.

## Exit condition and relationship to M5

This slice is complete when the simulation can carry an actor-safe, reference-frame-qualified last-known contact observation from local tactical sensing into a durable strategic report/projection, preserve it correctly through movement and save/load, and demonstrate that hidden target truth does not leak into it.

After that proof lands, M5 can focus on the political causal chain it is meant to establish:

```text
ship observes something
        ↓
actor-safe strategic report        ← Strategic Contact Reporting
        ↓
report reaches faction             ← later governed work
        ↓
faction evaluates goal/resources   ← M5
        ↓
faction assigns an existing ship
        ↓
existing ShipOrder machinery
        ↓
offscreen durable world change
```

Strategic Contact Reporting does not itself complete Milestone 3 or begin Milestone 5. Its historical milestone classification can be decided after implementation; the design purpose is to provide the smallest safe information seam between the two.

## Implementation guidance

Before coding, inspect the current M3A contact lifecycle, persistence mapping, player projection, strategic-location identity, and tests. Extend existing mechanisms when they satisfy the approved behavior rather than creating parallel knowledge stores.

If implementation discovery shows that one of the explicitly deferred identity, affiliation, reporting-distribution, or scheduler decisions is actually required to satisfy the approved exit condition, stop and refine the wiki/governing issue before adding that mechanism.
