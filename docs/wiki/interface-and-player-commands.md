---
schema_version: '1.1'
id: 'reference-s5bmr9-interface-and-player-commands'
title: 'Interface and Player Commands'
description: 'Command Deck design, live Engineering, actor-safe presentation, and current player controls.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-07'
tags:
  - 'godot'
  - 'ui'
aliases: []
related:
  - 'README.md'
  - 'docs/wiki/engineering-and-combat.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
---

# Interface and player commands

[Wiki home](README.md) · [Implementation status](implementation-status.md) · [Engineering and combat](engineering-and-combat.md) · [Source catalog](sources.md)

## Approved presentation framework

Use one persistent map-dominant Command Deck and a screen-dominant Engineering workspace. The compact Systems Spine communicates state/alerts; a contextual inspector follows selection and exposes actions. The strategic/tactical map remains Command Deck's primary workspace. Engineering uses a wider hierarchy, technical workspace, inspector, and clear return to Command.

`GameScreen` owns session-lifetime simulation, player projection, selection, workspace switching, save/load, and rate continuity. Switching workspaces must not recreate or replace simulation. Native Godot Controls, Containers, input, focus, drawing, scenes, and the project-owned [runtime theme](../../src/AlterCourse.Godot/assets/ui/command_theme.tres) are the presentation framework. Do not introduce a UI framework, global manager, service locator, generalized event bus, or generalized MVVM infrastructure that would become a second command authority.

The Theme owns runtime styling: command cyan for command/navigation, engineering amber, nominal green, and tactical/critical red; Rajdhani SemiBold/Bold headings, IBM Plex Sans labels, and IBM Plex Mono telemetry. The dark canvas and panel surfaces use the Theme's subtle/strong border and text roles. Structural rules are square and 1 px; active rails, selection indicators, and alert bands may use 2–3 px emphasis.

## Authority, references, and layout

Core is authoritative for simulation/spatial truth. Godot adapts player-known Core projections into controls and typed commands; it must neither fabricate domain state nor expose hidden NPC truth. Selection supplies context, never legality. A deterministic preview fixture is illustrative only in explicit development/test preview mode. When no real projection exists, production labels a value/action **Unavailable** rather than substituting preview data. Preview never enters simulation, persistence, or production truth.

The approved visual reference is [the Figma file](https://www.figma.com/design/9lH6uDhXSqhELwg05j8wEC), inspected at Travel `16:5`, Combat `16:152`, and Engineering `16:312`; stable comparison images are [Travel](../ui/reference/command-deck-travel.png), [Combat](../ui/reference/command-deck-combat.png), and [Engineering](../ui/reference/engineering-workspace.png). These are presentation references, not runtime authority or proof that depicted systems exist.

The 1920×1080 reference composition uses a 122 px Systems Spine, expanding map, and 356 px Command Deck inspector; Engineering uses a 250 px hierarchy, expanding workspace, and 430 px inspector. These are composition guidance, not fixed pixels. Containers preserve hierarchy/readable minima at 1600×900 and 2560×1440; the current shell is tested at 1024×640 and 1440×900, with 1024×640 the practical minimum.

## Live surface and commands

M3A made actor-local tactical contacts, identification, and hail live. M4 made generation, allocation, sensor/impulse condition/capability, and one sensor or impulse repair live. Strategic Contact Reporting added a minimal **LAST KNOWN CONTACTS** section to the strategic inspector. Combat fire solutions, shields/weapons, detailed EPS topology, unsupported component telemetry, and repair-team queues remain preview-only, unavailable, or absent; reference imagery is not an implementation commitment.

The live Engineering hierarchy is Overview, Power, Sensors, Propulsion, and Repairs. It presents Core values and Core-supplied action availability/reasons; it does not simulate allocation preview or optimistically mutate a ship. The strategic map selects connected destinations and engages scheduled travel. Tactical view shows the local frame, actor-known contact markers, selected-contact facts, scan, and hail. The demonstration course is 045 degrees at 2 km/s, subject to effective impulse limit; it is not a complete navigation console.

Live Engineering actions are **Balance power allocation**, **Prioritize sensors**, **Prioritize propulsion**, **Begin sensor repair**, **Begin impulse repair**, and **Return to Command Deck**. They use stable presentation identities and Core-supplied legality; a display label is not an authoritative system or command identity.

The inspector's own `KnownContactReports` section lists retained reports: learned vessel or tactical label, last-seen location/time, Current/Stale/Lost status, and learned design name. It is capped/summarized when necessary, not a strategic-map marker or intelligence dashboard.

Shortcuts are 1 strategic, 2 tactical, Space pause/resume, R cycle rate, U advance to player-relevant event, Ctrl+S/Ctrl+L quick save/load, E engage selected travel, and C submit the demonstration course. Running rates are 0.5x, 1x, 2x, and 4x; pause is separate. The quick-save slot and its failure-preservation boundary are owned by [content, assets, and persistence](content-assets-and-persistence.md).

## Interaction and precision

Mouse and keyboard route through the same selection and typed-intent path. Disabled actions remain visible with explanatory tooltips and never submit invalid requests. Selected, disabled, hover, and keyboard-focus states are visible. Focus traversal refreshes with active workspace/actionable controls and excludes hidden/disabled controls. Reconciliation uses stable presentation IDs: live refresh preserves valid focus and resolves current payload at activation, while removed actions become non-activatable. Space pause is handled before focused controls can activate it.

Coordinates, times, and quantities are formatted only at the adapter boundary. Tactical north and Godot screen Y are explicitly converted. The interface must not display precision unsupported by rule or player knowledge.

Future station workspaces need a concrete domain consumer and later decision; this page does not commit Tactical, Navigation, Science, Communications, or Operations as implementation work.

## Sources and evidence

See [GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs), [EngineeringWorkspace](../../src/AlterCourse.Godot/src/Gameplay/EngineeringWorkspace.cs), [shell scene](../../src/AlterCourse.Godot/Main.tscn), and [Godot shell tests](../../src/AlterCourse.Godot/tests/GameplayShellTest.gd). The [root README](../../README.md) owns setup and launch instructions; this page owns player controls.
