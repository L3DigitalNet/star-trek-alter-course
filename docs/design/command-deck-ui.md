---
schema_version: '1.1'
id: 'decision-yxdki1-command-deck-ui'
title: 'Command Deck UI'
description: 'Approved ownership and presentation decisions for the Command Deck and Engineering Workspace runtime.'
doc_type: 'decision'
status: 'active'
created: '2026-09-02'
updated: '2026-09-02'
reviewed: '2026-09-02'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'godot'
  - 'ui'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md'
  - 'docs/adr/0009-use-layered-testing-and-architecture-conformance.md'
  - 'docs/adr/0013-use-dev-for-development-and-main-for-releases.md'
source:
  - 'https://www.figma.com/design/9lH6uDhXSqhELwg05j8wEC'
  - 'docs/ui/reference/command-deck-travel.png'
  - 'docs/ui/reference/command-deck-combat.png'
  - 'docs/ui/reference/engineering-workspace.png'
confidence: 'high'
visibility: 'public'
license: 'MIT'
---

# Command Deck UI

## Decision

Use one persistent, map-dominant Command Deck shell for command work and a screen-dominant Engineering Workspace for engineering work. `GameScreen` owns the session-lifetime `GameSimulation`, player projection, selection, workspace switching, save/load, and simulation-rate continuity. Switching workspaces must not recreate or replace simulation state.

The Command Deck's compact Systems Spine communicates system state and alerts; its context inspector explains the selected map target and exposes the actions available for that target. The strategic or tactical map remains the primary workspace. Engineering instead uses a wider hierarchy, a technical workspace, and a context inspector, while retaining a clear return path to Command.

Use native Godot Controls, Containers, input, focus, drawing, scenes, and the project-owned Godot Theme at `src/AlterCourse.Godot/assets/ui/command_theme.tres`. The theme is the runtime source of presentation truth. It owns semantic color, typography, control, and focus styling; Figma does not.

Do not add a UI framework, global manager, service locator, generalized event bus, or generalized MVVM/event infrastructure. Focused presentation classes under `src/AlterCourse.Godot/src/Gameplay/CommandInterface*.cs` may compose the shell around the existing session owner without becoming a second simulation or command authority.

## Authority and data policy

Figma and its PNG comparisons are visual and presentation references only. They cannot define simulation rules, spatial positions, routes, target availability, or command results. Under ADRs 0001 and 0004, `AlterCourse.Core` is authoritative for simulation and spatial truth. Godot adapts player-known Core projections into controls and translates user intent to the existing typed Core commands; it must not fabricate domain state or expose hidden NPC truth.

Resolve displayed and actionable targets in this order:

1. The current player-known Core projection supplies the target identity, state, spatial data, and legal actions.
2. The current map or workspace selection supplies context only; it does not make an action legal or replace the Core target.
3. A deterministic preview fixture supplies illustrative data only while an explicit development or test preview mode is active.
4. When no real projection exists, production presentation labels the value or action `Unavailable`; it does not substitute preview values.

Travel uses real orders and status. Combat contacts, fire solutions, actions, and attention badges are preview-only until Core owns them. Engineering power distribution, component loads and controls, logs, and repair queues are also preview-only, except for current real sensor-repair state. Preview data never enters `GameSimulation`, persistence, or production gameplay truth.

## Visual and layout rules

Semantic colors communicate purpose rather than decoration: command cyan for command and navigation, amber for engineering, green for nominal state, amber for caution, and red for tactical or critical state. Use the dark canvas and panel surfaces with the matching subtle and strong border/text roles defined by the runtime theme.

Use Rajdhani SemiBold/Bold for station identity and headings, IBM Plex Sans Regular/Medium for labels, and IBM Plex Mono Regular/Medium for telemetry and logs. Normal structural boundaries are square, 1 px rules. Only active rails, selection indicators, and alert bands may use 2–3 px emphasis.

At the reference 1920x1080 layout, retain a 122 px Systems Spine, dominant expanding map, and 356 px Command Deck inspector; Engineering uses a 250 px hierarchy, expanding technical workspace, and 430 px inspector. These are composition references, not fixed runtime pixels: Containers preserve the information hierarchy and readable minima at 1600x900 and 2560x1440.

## Interaction and focus

Mouse and keyboard interactions route through the same selection and typed command path. Map hit testing selects a target; the inspector follows that selection; disabled actions remain visible with an explanatory tooltip rather than accepting an invalid request. Selected, disabled, hover, and keyboard focus states must be visible. Focus traversal follows the active workspace's controls, is refreshed when that workspace or its actionable controls change, and never reaches disabled or hidden controls.

## Reference evidence and non-goals

The approved Figma file is `https://www.figma.com/design/9lH6uDhXSqhELwg05j8wEC`. Its inspected reference nodes are Travel `16:5`, Combat `16:152`, and Engineering `16:312`. The stable PNG references are:

- `docs/ui/reference/command-deck-travel.png`
- `docs/ui/reference/command-deck-combat.png`
- `docs/ui/reference/engineering-workspace.png`

This decision does not create combat, power-network, damage, general repair, or engineering simulation. It also does not commit the project to future Tactical, Navigation, Science, Comms, or Operations station workspaces. Any such workspace needs an independently owned domain capability and a later decision when its user and simulation needs are demonstrated.

## Consequences

The visual language can evolve through Figma and the runtime theme without moving simulation authority out of Core. New UI work must preserve the one-way Core-to-Godot dependency, the semantic spatial model, native-first presentation stack, layered testing responsibilities, and the repository's development and release governance in ADRs 0001, 0003, 0004, 0009, and 0013.
