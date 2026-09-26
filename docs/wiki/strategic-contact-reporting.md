---
schema_version: '1.1'
id: 'decision-q4p7ns-strategic-contact-reporting'
title: 'Strategic Contact Reporting'
description: 'Implemented slice connecting local contact knowledge to durable strategic last-known information, with its original non-faction boundary preserved.'
doc_type: 'decision'
status: 'active'
created: '2026-09-06'
updated: '2026-09-26'
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
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/observation-driven-faction-response.md'
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

**Implemented** (Feature #77, Final PR #78, merged into `dev` as `80c3084`; released in v0.5.0). After reviewing the v0.4.0 implementation, the roadmap, the sensor/AI code, and the approved political model, the owner selected **Strategic Contact Reporting** as the next bounded development step after v0.4.0.

This resolved the sequencing question in Q-01. The slice bridges M3A's local observer knowledge and later living-sector/faction autonomy by establishing reference-frame-qualified reports without committing to a complete intelligence system or political runtime.

The historical sequence after v0.5.0 was [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md), followed by [Observation-Driven Faction Response](observation-driven-faction-response.md), both included in v0.6.0. The first M5 contribution uses only own-asset administrative knowledge. The second implements a direct NPC ship-to-direct-faction historical report channel and bounded investigation. That channel is no longer future work; broader sharing remains open. Neither later slice changes Strategic Contact Reporting's original ownership or non-goals.

Do **not** canonically rename this slice `M3B`. Its historical milestone classification remains separate from the approved name **Strategic Contact Reporting**. Current development has V9 saves through M6A; references to V6 below identify this slice's introduction and historical migration, not the current writer format.

### Implementation outcome

The runtime uses the smallest representation that satisfies the behavioral boundary:

- `SensorContactTrack` has a `LocationId? ObservedAtLocationId` frame: the strategic location where the observation was recorded. A fresh observation carries one through required non-nullable plumbing; a null frame represents a contact migrated from a pre-V6 save.
- `StrategicProjection.KnownContactReports` exposes `StrategicContactReportProjection`, one per retained contact qualified by an observation location: local contact ID, observed-at `LocationId`, last observed tactical position/time, retained status (Current, Stale, or Lost), identification, and learned vessel/design names. It omits hidden `ShipInstanceId` correlation and legacy contacts lacking a qualifying frame.
- This slice advanced saves to V6 under `strategic-contact-reporting-v1` and supported the adjacent V1→V6 chain. Its V5→V6 hop sets every legacy contact frame to null and derives nothing. A migrated contact remains on the tactical surface without a strategic report until a new qualifying observation occurs. [Content and persistence](content-assets-and-persistence.md) owns the later chain through V9.
- The Command Deck strategic inspector presents a **LAST KNOWN CONTACTS** telemetry section; the tactical surface continues to omit Lost contacts.
- The headless Pathfinder/Kestrel scenario proves observation at Dawn Anchor, Stale/Lost history, unchanged reports despite hidden NPC travel or player travel, save/load equivalence, and an update on legitimate reacquisition.

Q-02, Q-03, Q-04, and Q-05 were left open at this slice's completion. Faction assignment later resolved Q-05 and Q-04's own-asset administrative portion. Observation response subsequently resolved one direct historical reporting channel, without changing the observation contract here or resolving general intelligence sharing.

## Why this came first

M3A established the local knowledge boundary: each ship owns bounded observer-local contacts; local `SensorContactId` is not ship identity; Core may retain hidden ship correlation for rule resolution; and actor-safe projections omit that correlation. Identification means learned vessel/design names, not political affiliation or intent. Current, Stale, Lost, and reacquired states preserve the observer's knowledge.

Combining reporting, global identity, affiliation, faction state, scheduling, and decision logic into one first feature would entangle distinct questions. Strategic Contact Reporting established the narrower observation seam first:

```text
local tactical observation
        ↓
durable reference-frame-qualified last-known information
        ↓
actor-safe strategic report/projection
        ↓
later faction knowledge sharing and strategic decisions
```

The final arrow was deferred by this original slice. Observation-Driven Faction Response now implements one bounded direct reporting/investigation path from legitimate observation; it is not a general sharing network or a prerequisite retroactively added to the own-asset assignment policy.

## Goal

Prove that a ship can retain legitimate local observations as durable, strategically meaningful last-known information after a contact is no longer current, **without gaining or exposing hidden world truth**.

The player can leave local tactical space and retain what was actually observed: a vessel's known identification, observation location/reference frame, local position, and simulation time. Hidden movement must not silently refresh those facts.

## Required behavioral boundary

### Reference-frame-qualified observations

A tactical position is meaningful only inside the strategic location/reference frame where it was observed. Each report must identify both its strategic observation location and the position within that local space, even after observer or target departs.

A retained report means, conceptually:

> Contact 1 / Survey Vessel Kestrel was last observed at Dawn Anchor, tactical position `(x, y)`, at simulation time `T`.

Later unobserved movement does not change that report. The Core representation is an implementation choice within this reference-frame contract.

### Strategic contact/report projection

Use an actor-safe projection derived from legitimate observer knowledge. Depending on its API boundary, it may contain source/observer identity, local contact ID, observation location, last observed position/time, retained lifecycle status, identification state, and learned vessel/design names. It must not reveal true target `ShipInstanceId` merely because Core internally correlates the contact to a ship. Names are learned labels, not authority to perform global or cross-observer identity matching.

### Lost contacts remain legitimate knowledge

Lost means the observer no longer has a current sensor track; it does not erase the historical observation. Strategic presentation retains bounded last-known information while the underlying contact store retains it, even though the live tactical projection drops Lost contacts. This is not a permanent historical intelligence-retention system.

### Hidden truth does not backfill knowledge

After observation, later authoritative changes to target position, strategic location, Engineering, affiliation, orders, or other hidden state must not alter the old report without a new legitimate information source. Moving the observer must not reinterpret old coordinates in the observer's new location. A projection is not permission to query live target state to fill missing history.

## Identity decision: reuse existing observer-local contact identity

This slice does **not** approve a distinct `KnownShipId` or global known-vessel identity. Reuse observer-local `SensorContactId` for statements such as:

> Pathfinder / Contact 1 — last observed at Dawn Anchor — identified as Survey Vessel Kestrel.

Q-02 remains open for a consumer that needs identity across observers or reports about a vessel never personally observed. The implemented direct faction report preserves the reporting observer and that observer's local contact ID; it does not solve target identity correlation. A later consumer that genuinely needs a distinct identity concept requires governed refinement rather than a hidden extension here.

## Affiliation and intent remain unresolved

This slice does not change identification or approve allegiance/intent as scan results. Scanning establishes vessel/design names only. Do not infer faction, organization, government, allegiance, intent, or controller from a scan.

Q-03 remains open. A later feature must select an information source—communications, transponder data, prior reports, intelligence, recognition, or another justified mechanism—and define what it teaches the observer. Observation response demonstrates that a faction can investigate reported activity without knowing the reported vessel's allegiance. Neither it nor own-asset administrative assignment approves affiliation learning.

## Faction knowledge sharing remains deferred

This heading describes the **original Strategic Contact Reporting boundary**, not the absence of all later reporting. The slice records information for the observing ship/player and did not establish transport, delay, fusion, or allied/player distribution.

[Observation-Driven Faction Response](observation-driven-faction-response.md) now owns the implemented direct NPC ship → direct controlling faction path, using delayed immutable snapshots and bounded investigation. Its contracts must not be copied into this page as a second authority. Remaining Q-04 sharing—including hierarchy, allies, organizations, general fusion, and faction-to-player intelligence—stays open. D-10's administrative visibility alone is still not permission to ingest arbitrary sensor stores.

## Player-visible proof

The existing Pathfinder/Kestrel scenario supplies the representative acceptance flow:

1. Pathfinder detects Kestrel through ordinary sensing and identifies it through the existing path, learning only permitted facts.
2. The observation records a strategic location/reference frame and local position/time.
3. Kestrel becomes Stale/Lost or departs; Pathfinder may also change strategic location.
4. Tactical presentation no longer claims a Current contact, while strategic reports retain the original location/time and legitimately learned identification.
5. Kestrel moves again in hidden truth; the report stays unchanged until legitimate new observation.
6. Save/load preserves the same actor-visible semantics, and reacquisition updates the retained contact through ordinary observation.

The proof is durable actor-safe information, not a broad intelligence UI. A report's reference frame must survive both the target's and observer's movement.

## Persistence and compatibility

Follow ADR 0006: persist authoritative meaning, not projection caches or duplicated truth. At this slice's admission V5 was the input save schema. Because an observation frame could not be reconstructed safely, V5→V6 initializes it to null without consulting either vessel's present position. V6 shipped in v0.5.0; the later complete supported chain is owned by [content and persistence](content-assets-and-persistence.md).

Migration must not invent affiliation, unsent historical reports, cross-observer correlation, intelligence-sharing history, treaty/political state, or unobserved target movement. A field that is an unambiguous derived cache should remain derived rather than become a second persisted authority. Loading must preserve historical Stale/Lost facts rather than apply present-location rules to them.

## Required test themes

The maintained tests must cover:

- two observers retaining different knowledge about the same authoritative ship, with no hidden target identity in actor-safe APIs;
- hidden target changes leaving retained actor-visible reports unchanged;
- Lost-contact history surviving target and observer movement without a reference-frame change;
- save/load equivalence, legitimate reacquisition, and non-inventive legacy migration;
- unobserved ships remaining absent from the report set and identification adding only permitted facts;
- bounded canonical ordering and input validation;
- existing cautious-contact policy retaining its information boundary; and
- large-step/small-step equivalence only where existing simulation rules guarantee it.

A headless scenario proves the complete information path. Godot tests cover the added presentation/input, not duplicate Core rules. Test execution belongs to the feature/admission evidence and must not be inferred from this requirement list.

## Explicit non-goals

Strategic Contact Reporting itself did **not** add or approve faction or organization runtime, faction control fields, `FactionId` solely for reporting, `KnownShipId`, cross-observer correlation, report distribution, faction strategic AI, faction scheduler targets, affiliation/intent learning, strategic long-range sensing, confidence/error models, intelligence networks, treaties, reputation, governments, jurisdiction, randomness, combat, or a generic actor/rules/event framework. It added no ECS, database, or service architecture.

These preserve the original feature scope; separately approved later faction and combat implementations are not violations of that historical boundary.

## Exit condition and relationship to M5

This implemented slice carries actor-safe, reference-frame-qualified local observations into durable strategic reports, preserves them through movement/save/load, and proves that hidden truth does not leak into them. It did not itself complete M3 or begin M5.

The later direct information-to-action path is now implemented by Observation-Driven Faction Response:

```text
legitimate NPC local observation
        ↓
immutable observer-local report snapshot
        ↓
delayed delivery to the direct controlling faction
        ↓
bounded investigation decision from permitted knowledge
        ↓
existing ShipOrder / travel machinery
        ↓
offscreen arrival and ordinary local observation
```

This path consumes the observation model, not a new shared live sensor store or the player's strategic projection. The first [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) instead starts from an objective and own-asset facts, with no reporting prerequisite. The ownership of all three slices remains distinct.

## Implementation guidance

The original feature extended the M3A contact lifecycle, persistence mapping, player projection, strategic-location identity, and tests. Continue reusing those mechanisms where they satisfy approved behavior rather than creating parallel knowledge stores.

A later identity, affiliation, or additional distribution rule requires refinement on its owning wiki page and in its governing work. D-11's Ship/Faction scheduler selection belongs to the assignment slice; D-15's direct historical reporting belongs to observation response. Neither rewrites the original scope of Strategic Contact Reporting.
