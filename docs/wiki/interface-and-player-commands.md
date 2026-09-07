---
schema_version: '1.1'
id: 'reference-s5bmr9-interface-and-player-commands'
title: 'Interface and Player Commands'
description: 'Command Deck design, live Engineering, actor-safe presentation, and current player controls.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'godot'
  - 'ui'
aliases: []
related:
  - 'docs/design/command-deck-ui.md'
  - 'docs/design/engineering-backbone.md'
  - 'README.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
---

# Interface and player commands

[Wiki home](README.md) · [Implementation status](implementation-status.md) · [Source catalog](sources.md)

## Approved presentation framework

Use one persistent map-dominant Command Deck and a screen-dominant Engineering workspace. The compact Systems Spine communicates state/alerts; a contextual inspector follows selection and exposes actions. Engineering uses a wider hierarchy and technical workspace with a clear return to Command.

`GameScreen` retains the session simulation, player projection, selection, workspace switching, rate continuity, and save/load. Switching screens does not recreate the universe. Native Godot Controls, Containers, focus, input, scenes, drawing, and a project-owned Theme are the presentation framework.

The [runtime theme](../../src/AlterCourse.Godot/assets/ui/command_theme.tres) owns styling. The approved design uses command cyan, engineering amber, nominal green, and tactical/critical red; Rajdhani headings, IBM Plex Sans labels, and IBM Plex Mono telemetry; mostly square, fine structural rules. These are presentation choices, not domain rules. Existing Figma and PNG references are reference artifacts, not runtime authority or proof that shown systems exist.

## Live versus preview

Resolve display and actions from the current player-known Core projection. Selection supplies context, not permission. Deterministic preview fixtures are allowed only in explicit development/test preview mode. If no implemented production projection exists, show Unavailable rather than fabricated values.

M3A made local contacts, identification, and hail live. M4 made power, sensors, impulse, and the single active repair live in Engineering. Strategic Contact Reporting made a minimal "LAST KNOWN CONTACTS" telemetry section live on the strategic Command Deck inspector. Combat fire solutions, shields/weapons, advanced engineering topology, and repair-team queues remain preview-only or absent. An earlier screenshot showing such a system is not an implementation commitment.

The live Engineering hierarchy is Overview, Power, Sensors, Propulsion, and Repairs. Allocation/repair controls carry Core-supplied availability and reasons. UI code does not locally simulate an allocation preview or optimistically mutate the ship.

## Current interaction surface

The strategic map selects connected destinations and engages scheduled travel. Tactical view shows the local frame, actor-known contact markers, selected-contact facts, scan, and hail. The current demonstration course is 045 degrees at 2 km/s and remains constrained by effective impulse capability; it is not a complete navigation command console.

The strategic inspector also lists the player's own `KnownContactReports`: one row per retained report, naming the learned vessel or the tactical contact label, the last-seen location and time, the retained Current/Stale/Lost status, and the learned design name, capped and summarized when it would overflow. This presentation deliberately stays minimal; it is not a strategic-map marker or intelligence dashboard.

Current shortcuts are 1 for strategic view, 2 for tactical view, Space for pause/resume, R to cycle rate, U to advance to a player-relevant event, Ctrl+S/Ctrl+L for quick save/load, E to engage selected travel, and C for the demonstration tactical course. Presentation rates are 0.5x, 1x, 2x, and 4x, with pause separate.

Mouse and keyboard follow the same typed intent path. Controls reconcile by stable presentation identity, retain focus through refresh, and resolve the current payload at activation. Disabled/hidden controls do not remain actionable. Space pause must not also activate a focused command button. Known bugs and their fixes are indexed in [handoff bug records](../handoff/bugs/INDEX.md).

## Approved next-slice presentation boundary

[Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) is approved design, not implemented, and adds no faction, affiliation, political hierarchy, or intelligence UI. The player ship is outside faction autonomous control in its proof. Neither true direct control nor faction decision explanations become ordinary player-facing data.

The later implementation must preserve current controls and actor-safe projections, including save/load and player-relevant event filtering. Offscreen faction work or NPC-NPC contact is not automatically a player notification or stop condition. Godot may adapt to the implemented save-format change without acquiring political simulation authority or a hidden-state diagnostic view.

## Layout and precision

The original design reference is 1920×1080, with composition guidance for other desktop sizes. The current shell documentation records tested layouts at 1024×640 and 1440×900, with 1024×640 the practical minimum. Reference panel widths are not fixed simulation or display requirements.

Coordinates, time, and quantities are formatted only at the adapter boundary. Tactical north and Godot screen Y are explicitly converted. The UI should not display precision unsupported by the actual rule or actor knowledge.

## Sources

[Command Deck decision](../design/command-deck-ui.md), [Engineering design](../design/engineering-backbone.md), [current controls](../../README.md), and [visual reference directory](../ui/reference/). Future stations require concrete domain consumers rather than automatically becoming implementation tasks because a station name appears in a design.
