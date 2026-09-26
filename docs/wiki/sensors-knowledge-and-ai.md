---
schema_version: '1.1'
id: 'concept-ln1g3b-sensors-knowledge-and-ai'
title: 'Sensors, Knowledge, and AI'
description: 'Actor-local sensor knowledge, contact operations, and the deterministic cautious-contact policy.'
doc_type: 'concept'
status: 'active'
created: '2026-09-06'
updated: '2026-09-08'
tags:
  - 'ai'
  - 'sensors'
  - 'simulation'
aliases: []
related:
  - 'docs/wiki/engineering-and-combat.md'
  - 'docs/wiki/content-assets-and-persistence.md'
  - 'docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md'
---

# Sensors, knowledge, and AI

[Wiki home](README.md) · [Engineering and combat](engineering-and-combat.md) · [Strategic Contact Reporting](strategic-contact-reporting.md) · [Faction assignment](faction-intent-and-autonomous-assignment.md)

## Authority and observation

World truth, actor knowledge, and durable historical meaning are distinct. Each ship owns actor-local sensor knowledge. Core retains target correlation only for rules; it never appears in player projection, Godot, player-safe events, or AI input. A contact holds observer-local ID, last observed tactical position/time, Current/Stale/Lost lifecycle, Detected/Identified state, and vessel/design names only after identification. [Strategic Contact Reporting](strategic-contact-reporting.md) owns the implemented durable last-known-report seam; [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) owns the separate bounded faction assignment knowledge boundary; [Observation-Driven Faction Response](observation-driven-faction-response.md) adds only delayed, immutable direct-faction reports from legitimate NPC observation episodes.

Only ships at the same strategic location observe each other. Passive observation compares Euclidean tactical distance in kilometres with authored passive range multiplied by the observer's effective sensor capability. It uses no randomness. An observer supports at most 255 retained contacts in the current 256-ship world. Its ID allocates monotonically, is not a ship ID, and survives stale, lost, and reacquired states.

New detection is Current and Detected; continued detection refreshes facts. Loss makes a contact Stale, keeps its last observation, and schedules one exact loss work item five seconds later. That work rechecks detectability before Lost. Reacquisition cancels its exact work, restores Current, and preserves local ID/identification. Lost contacts remain bounded correlation memory but leave live tactical projection.

## Scan, hail, and cautious response

One observer can own one active scan, targeting a current detected local contact. Completion revalidates Current status, then identifies it and records vessel/design names. Staleness/loss or zero effective sensor capability cancels exact completion work, clears the operation, and returns a player-safe interruption. Restoring a contact or power never revives it.

Hail is an immediate typed Core request for an identified current player contact. A target without CautiousContact posture or a valid reciprocal current contact gives no response. The proof vessel accepts valid hail, updates actor-local knowledge, and changes course through the ordinary tactical boundary. Hail has no dialogue/narrative runtime.

Only the proof vessel has persisted `CautiousContact`; migration does not add it to prior ships. The pure policy sees own facts, actor-safe contacts, and own effective maximum speed, never aggregate state, target runtime/definition, hidden identity/position, target Engineering, or Godot state. It selects valid incoming identified hail first, otherwise nearest Current contact with local-ID tie break. It evaluates Hold, Approach, and Withdraw with explicit constraints, scores, and stable Hold/Approach/Withdraw tie order.

An unidentified Current contact selects Withdraw at 0.5 km/s, clamped by effective maximum. Valid identified hail holds; an identified contact otherwise prefers Hold; no Current contact holds. Movement requires at-location, Current primary contact, nonzero observed displacement, and legal nonzero capped speed. Zero displacement or zero speed causes constraint rejection and Hold. Each evaluation returns typed facts, candidates, rejected constraints, selected course, tie rule, and `RandomnessUsed = false`; it is diagnostic/testable, not durable history.

## Deterministic timing and boundaries

Contact-sensitive local work uses the fixed 100 ms grid only when motion or changing sensor condition can alter observation. At a boundary Core snapshots world truth, evaluates observer/target pairs in observer-ID then target-ID order, applies contact changes in that order, and schedules same-time decision wakes in observer order. Contact loss, scan completion, repair, and decisions use exact correlations and revalidate prerequisites. Inactive strategic work remains event-to-event: no global polling sweep or future range-crossing solver.

`AdvanceUntilNextPlayerRelevantEvent` processes hidden NPC observation/decision work without reporting it. It can stop at player-safe lifecycle or scan events. Events carry exact simulation occurrence time and optional local contact ID, preserving chronology without reconstruction from final projection. The current released V8 persistence model belongs to [content, assets, and persistence](content-assets-and-persistence.md); former V4 sensor schema/migration are historical evidence.

## Deferrals and evidence

There is no confidence/error model, estimated stale position, long-range strategic sensor simulation, cloaking, emissions, electronic warfare, false contact, NPC scan, additional doctrine, dialogue tree, or Science/Communications workspace. Durable last-known strategic reports are implemented and owned by [Strategic Contact Reporting](strategic-contact-reporting.md). Observation-Driven Faction Response does not add global or live shared sensors, identity correlation, affiliation inference, or a player intelligence feed. Combat, shields, weapons, damage, and advanced Engineering remain outside this slice.

See [sensor knowledge](../../src/AlterCourse.Core/Sensors/SensorKnowledge.cs), [cautious policy](../../src/AlterCourse.Core/AI/CautiousContactDecisionPolicy.cs), [contact scenario tests](../../tests/AlterCourse.Core.Tests/Gameplay/Milestone3ProofScenarioTests.cs), [hail tests](../../tests/AlterCourse.Core.Tests/Gameplay/HailAndContactDecisionTests.cs), and [policy tests](../../tests/AlterCourse.Core.Tests/AI/CautiousContactDecisionPolicyTests.cs).
