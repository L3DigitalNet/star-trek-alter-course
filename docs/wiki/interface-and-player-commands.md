---
schema_version: '1.1'
id: 'reference-s5bmr9-interface-and-player-commands'
title: 'Interface and Player Commands'
description: 'Command Deck design, live Engineering, actor-safe presentation, session lifetime, and current player controls.'
doc_type: 'reference'
status: 'active'
created: '2026-09-06'
updated: '2026-09-27'
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
  - 'docs/adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md'
  - 'docs/adr/0016-own-actor-knowledge-and-share-immutable-observation-reports.md'
  - 'docs/adr/0018-separate-simulation-session-lifetime-from-workspaces.md'
---

# Interface and player commands

[Wiki home](README.md) · [Implementation status](implementation-status.md) · [Engineering and combat](engineering-and-combat.md) · [Source catalog](sources.md)

## Approved presentation framework

Use one persistent map-dominant Command Deck and a screen-dominant Engineering workspace. The compact Systems Spine communicates state/alerts; a contextual inspector follows selection and exposes actions. The strategic/tactical map remains Command Deck's primary workspace. Engineering uses a wider hierarchy, technical workspace, inspector, and clear return to Command.

`GameScreen` currently owns the session-lifetime simulation, projection, selection, workspace switching, save/load, and rate continuity. Switching workspaces does not recreate or replace simulation. [ADR 0018](../adr/0018-separate-simulation-session-lifetime-from-workspaces.md) owns that lifetime and stale-action boundary without permanently assigning all presentation responsibilities to one class.

Native Godot Controls, Containers, input, focus, drawing, scenes, and the project-owned [runtime theme](../../src/AlterCourse.Godot/assets/ui/command_theme.tres) are the presentation framework. Do not introduce a UI framework, global manager, service locator, generalized event bus, or MVVM infrastructure that becomes a second command authority.

The Theme owns runtime styling: command cyan for command/navigation, engineering amber, nominal green, and tactical/critical red; Rajdhani SemiBold/Bold headings, IBM Plex Sans labels, and IBM Plex Mono telemetry. The dark canvas and panels use the Theme's border and text roles. Structural rules are square and 1 px; active rails, selection indicators, and alert bands may use 2–3 px emphasis.

## Authority, references, and layout

Core owns simulation/spatial truth. Godot adapts player-known projections into controls and typed commands; it neither fabricates domain state nor exposes hidden NPC truth. Selection supplies context, not legality. [ADR 0015](../adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md) separates execution/commit outcomes from presentation; [ADR 0016](../adr/0016-own-actor-knowledge-and-share-immutable-observation-reports.md) covers disclosure through feedback and controls as well as projection fields.

A preview fixture is illustrative only in explicit development/test preview mode. Without a real projection, production labels a value/action **Unavailable** instead of substituting preview data. Preview does not advance simulation, submit live commands, or enter persistence.

The approved visual reference is [the Figma file](https://www.figma.com/design/9lH6uDhXSqhELwg05j8wEC), inspected at Travel `16:5`, Combat `16:152`, and Engineering `16:312`; stable comparison images are [Travel](../ui/reference/command-deck-travel.png), [Combat](../ui/reference/command-deck-combat.png), and [Engineering](../ui/reference/engineering-workspace.png). These are presentation references, not runtime authority or implementation proof.

The 1920×1080 reference composition uses a 122 px Systems Spine, expanding map, and 356 px Command Deck inspector; Engineering uses a 250 px hierarchy, expanding workspace, and 430 px inspector. These are composition guidance, not fixed pixels. Containers preserve hierarchy/readable minima at 1600×900 and 2560×1440; the current shell is tested at 1024×640 and 1440×900, with 1024×640 the practical minimum.

## Live surface and commands

M3A made actor-local tactical contacts, identification, and hail live. M4 made generation, allocation, sensor/impulse condition/capability, and one repair live. Strategic Contact Reporting added **LAST KNOWN CONTACTS** to the strategic inspector. M6A adds the minimal live Combat workspace, shield/weapon power and repair controls, and heading/speed Apply and Stop through typed Core course rules.

Combat uses local contact selection and actor-safe feedback. It does not show target health, affiliation, controller, faction, condition, capability, or pending NPC intent. Detailed EPS topology, unsupported component telemetry, and repair-team queues remain unavailable or absent; reference imagery is not an implementation commitment.

The live Engineering hierarchy is Overview, Power, Sensors, Propulsion, Shields, Weapons, and Repairs. It presents Core values and action availability/reasons, without simulated allocation previews or optimistic ship mutation. The strategic map selects connected destinations and engages scheduled travel. Tactical view shows the local frame, actor-known markers, selected-contact facts, scan, hail, heading/speed fields, Apply, and Stop. Course remains subject to effective impulse limits; this is not a complete navigation console.

Live Engineering actions include **Balance power allocation**, priorities for Sensors, Propulsion, Shields, and Directed Energy, repair of repairable installations, and **Return to Command Deck**. Combat provides stable selection and **Fire directed energy**. Disabled actions retain Core-supplied reasons, including missing/Current/Identified contact requirements, range, weapon power/condition, cooldown, supported semantic target kind, and repair/allocation/course availability. A label is not authoritative system or command identity.

Fire control submits an observer-local contact and semantic target-system kind. It does not calculate range, capability, readiness, damage, or hit outcome. Attacker feedback may state shot fired, shield hit, or beam penetration toward the selected kind, never hidden identity/controller/faction/condition/capability/percentages or installed inventory. The player victim may receive its own damage, brownout, forced-speed, and repair-interruption consequences. Pending NPC intent is never displayed.

`KnownContactReports` lists retained reports: learned vessel or tactical label, last-seen location/time, Current/Stale/Lost status, and learned design. It is capped/summarized when necessary, not a strategic-map marker or intelligence dashboard. NPC-faction received reports, investigations, report IDs, and controllers remain outside the player projection.

Shortcuts are 1 strategic, 2 tactical, Space pause/resume, R cycle rate, U advance to player-relevant event, Ctrl+S/Ctrl+L quick save/load, E engage selected travel, and C submit course fields. Visible controls select Engineering and Combat. Running rates are 0.5x, 1x, 2x, and 4x; pause is separate. [Persistence](content-assets-and-persistence.md) owns the quick-save slot and failure-preservation boundary.

## Installed-system interface migration

The [substrate contract](ship-system-substrate.md#generic-presentation-and-refit-readiness) is implemented on `dev`: own-ship rows and Balance/Prioritize/Begin Repair actions address installed identities rather than per-kind action enum members. Rows/buttons iterate `EngineeringProjection.Systems`/`Actions`; a Godot presentation table (`EngineeringKindPresentation`) supplies kind-specific text and reproduces the production loadout's hierarchy, labels, and button order.

Stable keys are `balance`, `prioritize:<id>`, `repair:<id>`, `return-command`, and hierarchy/selection `system:<id>`. `EngineeringAction`, `PowerAllocationPreset`, and the temporary `GameSimulation.EngineeringAdapter.cs` bridge are removed; Godot switches on `EngineeringOperation`. Missing installations have no fake Engineering row; specialized views explicitly report unavailable capability, such as a null `SensorProjection.SensorInstallation`.

Activation resolves the current payload instead of a stale captured installation ID. `OwnShipActionBinding` carries owning ship plus `GameScreen` simulation generation. A control offered before load is refused if the owner/generation no longer matches, including when the loaded world reuses IDs with different meanings. Ordinary refresh preserves the generation, valid selection, and focus.

Remote aim is different from own-ship selection. Neither inventory nor absence-specific refusal may become an implicit scan. The [remote-targeting contract](ship-system-substrate.md#remote-targeting-must-not-become-an-inventory-probe) owns non-confirming feedback for absent-kind targets. This migration does not add player refit controls.

## Session replacement

Quick-load validates a complete Core candidate before installing it. Failure preserves the existing simulation. Successful replacement advances the presentation generation, clears/rebuilds the relevant interaction context, preserves the selected rate, and drops fractional presentation-time carry so pre-load elapsed time cannot advance restored truth.

World snapshots, session selections/drafts, and application preferences have different owners. Generation is a presentation guard, not a saved world identity. A post-installation display failure does not undo the successful replacement; report it without falsely claiming the load was unapplied. Other retained controls may satisfy the lifetime contract by clearing, rebuilding, or resolving current context; not every label or action is required to use `OwnShipActionBinding` itself.

## Interaction and precision

Mouse and keyboard use the same selection and typed-intent path. Disabled actions remain visible with explanatory tooltips and do not submit invalid requests. Selected, disabled, hover, and focus states are visible. Traversal follows the active workspace and excludes hidden/disabled controls. Stable IDs preserve valid focus through refresh; removed actions become non-activatable. Space pause is handled before focused controls can activate it.

Deferred focus or action work rechecks node and session context rather than trusting a captured object. Coordinates, times, and quantities are formatted only at the adapter boundary; tactical north and Godot screen Y are explicitly converted. The interface does not display precision unsupported by the rules or player knowledge.

Future station workspaces need a concrete domain consumer and a later decision. This page does not commit Navigation, Science, Communications, or Operations as implementation work.

## Sources and evidence

See [GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs), [EngineeringWorkspace](../../src/AlterCourse.Godot/src/Gameplay/EngineeringWorkspace.cs), [OwnShipActionBinding](../../src/AlterCourse.Godot/src/Gameplay/OwnShipActionBinding.cs), [EngineeringKindPresentation](../../src/AlterCourse.Godot/src/Gameplay/EngineeringKindPresentation.cs), [shell scene](../../src/AlterCourse.Godot/Main.tscn), and [shell tests](../../src/AlterCourse.Godot/tests/GameplayShellTest.gd).

Named regressions include `test_live_engineering_rows_actions_and_labels_reproduce_base_presentation`, `test_stale_engineering_control_is_refused_after_quick_load_but_acts_after_refresh`, `test_stale_engineering_control_is_refused_after_load_changes_the_owner`, and `test_stale_engineering_control_is_refused_after_load_changes_installation_meaning`. These are evidence entry points; actual execution dates/results belong to the source catalog and governing PR. The [root README](../../README.md) owns setup and launch; this page owns player controls.
