---
schema_version: '1.1'
id: 'concept-s51phf-sensors-knowledge-and-ai'
title: 'Sensors Knowledge and AI'
description: 'Implemented local contact rules, actor-safe decisions, and Strategic Contact Reporting.'
doc_type: 'concept'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'simulation'
  - 'sensors'
  - 'ai'
aliases: []
related:
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/design/first-observed-contact.md'
  - 'docs/design/engineering-backbone.md'
  - 'docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md'
---

# Sensors, knowledge, and AI

[Wiki home](README.md) · [Strategic Contact Reporting](strategic-contact-reporting.md) · [Engineering](engineering-and-combat.md) · [Open questions](open-questions.md)

## The information boundary

World truth, actor knowledge, and durable historical meaning are distinct. Fog of war is a simulation rule, not just a rendering mask. Core may resolve a command against hidden truth, but player projections and autonomous decision inputs receive only authorized knowledge and own-actor capability facts.

The implemented knowledge model is ship-local and tactical. A passive observation requires distinct ships at the same strategic location and within the observer's effective range. Traveling ships neither observe nor appear as local contacts. Under M4, effective range is authored range multiplied by sensor condition and power satisfaction, not sensor integrity alone.

## Current contact lifecycle

`SensorContactId` is observer-local and is not the true `ShipInstanceId`. Core retains hidden target correlation for rule resolution; neither public player projections nor AI inputs expose it. Each observer retains bounded knowledge, with a maximum of 255 possible other-ship tracks in the present 256-ship world.

Detection creates a Current, Detected contact. Continued observation refreshes last observed position/time. Loss of detectability makes it Stale and schedules one exactly correlated loss consequence five seconds later. If it remains undetectable, it becomes Lost. Reacquisition preserves its local ID and learned identity while canceling the old loss work. Lost tracks remain bounded internal correlation memory but are absent from live tactical presentation.

A last observation is not the target's current hidden position. The current implementation does not provide inferred trajectories, affiliation/intent assessment, confidence/error fields, or a faction-wide intelligence view.

## Scan and hail

One active scan per observer targets a current detected contact. Successful completion learns vessel and design display names; it does not identify a faction. Duration remains the authored fixed interval. Stale/lost targets interrupt the scan, as does zero sensor capability under M4; restoring power does not resurrect canceled work.

Hail is a typed immediate interaction with an identified current player contact. The cautious proof vessel can acknowledge when reciprocal contact conditions permit. This is a noncombat command seam, not branching dialogue, a treaty, or a full communications simulation.

## Explainable autonomous decisions

The `CautiousContact` policy receives own-ship facts and actor-safe contacts. It evaluates Hold, Approach, and Withdraw with explicit constraints, deterministic scoring/tie-breaking, and typed explanations. In the proof, an unidentified current contact leads to bounded withdrawal; a valid identified hail leads to Hold. M4 supplies the acting ship's effective impulse limit, and resulting motion passes through the same validated tactical-course boundary as player intent.

An explanation is diagnostic/test data, not automatically player-visible or durable political history. Changing hidden truth while holding actor knowledge and legitimate own capability constant must not change a pure policy's choice. Command resolution can still reject a stale proposal when real prerequisites no longer hold.

ADR 0010 generalizes the decision discipline, not a mandatory algorithm: actor snapshot, goals/constraints, candidates, rejection, evaluation, deterministic selection, typed command or no-action, and explanation. Faction strategic AI is still future work; no behavior-tree framework or external LLM is gameplay authority.

## Strategic Contact Reporting

[Strategic Contact Reporting](strategic-contact-reporting.md) is implemented (Feature #77, Final PR #78).

The slice extends the existing information boundary rather than replacing it. Its purpose is to carry a legitimate local observation into durable, reference-frame-qualified last-known strategic information. A retained report continues to describe where and when the observer actually saw a contact even after the observer or target moves elsewhere, and later hidden target truth does not backfill the report.

The implementation reuses observer-local `SensorContactId` as far as it remains sufficient. It does not add `KnownShipId`, global vessel correlation, affiliation/intent learning, faction knowledge sharing, faction AI, faction scheduler targets, or a general intelligence network. Q-02 through Q-05 remain open for later consumers.

Each retained tactical position is qualified by the strategic location it was observed in: `SensorContactTrack` carries `ObservedAtLocationId`, and `StrategicProjection.KnownContactReports` exposes the resulting `StrategicContactReportProjection` list. See Strategic Contact Reporting's Implementation outcome for the full shape.

## Strategic knowledge still deferred beyond this slice

Strategic Contact Reporting does not answer how two observers correlate reports, how a report about a never-locally-observed vessel is represented, how affiliation is learned, or how ship knowledge reaches a faction. Those questions remain in [Open questions](open-questions.md).

Do not convert the approved report seam into a second world-truth store. It must remain actor-safe, bounded, and causally tied to legitimate observation. If implementation proves that a deferred identity or sharing concept is actually required to satisfy the approved exit condition, return to governed design refinement rather than introducing it silently.

## Sources

[Strategic Contact Reporting](strategic-contact-reporting.md) owns the next approved slice. [First observed contact](../design/first-observed-contact.md) defines M3A; [Engineering Backbone](../design/engineering-backbone.md) supersedes its sensor-only condition and repair descriptions. See [AI ADR](../adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md), [Core AI](../../src/AlterCourse.Core/AI/), [Core sensors](../../src/AlterCourse.Core/Sensors/), and [gameplay tests](../../tests/AlterCourse.Core.Tests/Gameplay/).
