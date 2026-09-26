---
schema_version: '1.1'
id: 'reference-s5bmr9-interface-and-player-commands'
title: 'Interface and Player Commands'
description: 'Command Deck design, live Engineering, actor-safe presentation, and current player controls.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-26'
tags:
  - 'godot'
  - 'ui'
aliases: []
related:
  - 'README.md'
  - 'docs/wiki/engineering-and-combat.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/ship-system-substrate.md'
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

M3A made actor-local tactical contacts, identification, and hail live. M4 made generation, allocation, sensor/impulse condition/capability, and one repair live. Strategic Contact Reporting added a minimal **LAST KNOWN CONTACTS** section to the strategic inspector. M6A adds the minimal live Combat workspace, shield/weapon power and repair controls, and native heading/speed Apply and Stop through existing typed Core course rules. Combat uses only local contact selection and actor-safe feedback; it does not show target health, affiliation, controller, faction, condition, capability, or pending NPC intent. Detailed EPS topology, unsupported component telemetry, and repair-team queues remain unavailable or absent; reference imagery is not an implementation commitment.

The live M6A Engineering hierarchy is Overview, Power, Sensors, Propulsion, Shields, Weapons, and Repairs. It presents Core values and Core-supplied action availability/reasons; it does not simulate allocation preview or optimistically mutate a ship. The strategic map selects connected destinations and engages scheduled travel. Tactical view shows the local frame, actor-known contact markers, selected-contact facts, scan, hail, heading and speed fields, Apply, and Stop. Course controls remain subject to the effective impulse limit; they are not a complete navigation console.

Live Engineering actions include **Balance power allocation**, priorities for Sensors, Propulsion, Shields, and Directed Energy, repairs for all repairable systems, and **Return to Command Deck**. Combat provides stable target selection and **Fire directed energy**. Actions remain visible when disabled and provide Core-supplied reasons, including no selected/Current/Identified contact, out of range, unpowered or offline weapons, cooldown, unsupported target kind, and unavailable repair/allocation/course state. A display label is not an authoritative system or command identity.

An M6A fire control submits an observer-local contact and semantic target-system kind; it does not calculate range, capability, readiness, damage, or hit outcome. Actor-safe attacker feedback may state shot fired, shield hit, or beam penetration toward the selected kind, never target identity/controller/faction/condition/capability/percentages. It does not authorize disclosure of hidden installed inventory. The player victim may receive its own damage, brownout, forced-speed, and repair-interruption consequences. Pending NPC intent is never displayed.

The inspector's own `KnownContactReports` section lists retained reports: learned vessel or tactical label, last-seen location/time, Current/Stale/Lost status, and learned design name. It is capped/summarized when necessary, not a strategic-map marker or intelligence dashboard. Observation-Driven Faction Response keeps received NPC-faction reports, investigations, report IDs, and controller facts out of Godot/player projection; player visibility still depends on the player's own legitimate sensing.

Shortcuts are 1 strategic, 2 tactical, Space pause/resume, R cycle rate, U advance to player-relevant event, Ctrl+S/Ctrl+L quick save/load, E engage selected travel, and C submit the current course fields. Engineering and Combat workspaces are selected through their visible controls. Running rates are 0.5x, 1x, 2x, and 4x; pause is separate. The quick-save slot and its failure-preservation boundary are owned by [content, assets, and persistence](content-assets-and-persistence.md).

## Selected installed-system interface migration

Issue #121's [substrate contract](ship-system-substrate.md#generic-presentation-and-refit-readiness) requires generic own-ship rows and Repair/Prioritize actions addressed to installed identities, not another action enum member per kind. It is selected work, not current UI behavior. Absent installations have no fake Engineering row; specialized views may explicitly report unavailable capability. Stable operation/installation keys preserve selection and focus, and activation resolves the current payload rather than a stale captured ID.

Remote aim remains different from own-ship component selection. Neither target inventory nor an absence-specific fire refusal may become an implicit scan. Follow the [remote-targeting contract](ship-system-substrate.md#remote-targeting-must-not-become-an-inventory-probe), including non-confirming feedback for newly representable absent-kind targets. Do not build player refit controls in this migration.

## Interaction and precision

Mouse and keyboard route through the same selection and typed-intent path. Disabled actions remain visible with explanatory tooltips and never submit invalid requests. Selected, disabled, hover, and keyboard-focus states are visible. Focus traversal refreshes with active workspace/actionable controls and excludes hidden/disabled controls. Reconciliation uses stable presentation IDs: live refresh preserves valid focus and resolves current payload at activation, while removed actions become non-activatable. Space pause is handled before focused controls can activate it.

Coordinates, times, and quantities are formatted only at the adapter boundary. Tactical north and Godot screen Y are explicitly converted. The interface must not display precision unsupported by rule or player knowledge.

Future station workspaces need a concrete domain consumer and later decision; this page does not commit Tactical, Navigation, Science, Communications, or Operations as implementation work.

## Sources and evidence

See [GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs), [EngineeringWorkspace](../../src/AlterCourse.Godot/src/Gameplay/EngineeringWorkspace.cs), [shell scene](../../src/AlterCourse.Godot/Main.tscn), and [Godot shell tests](../../src/AlterCourse.Godot/tests/GameplayShellTest.gd). The [root README](../../README.md) owns setup and launch instructions; this page owns player controls. Issue #121 must supply fresh conformance evidence before its selected interface migration is marked implemented.
