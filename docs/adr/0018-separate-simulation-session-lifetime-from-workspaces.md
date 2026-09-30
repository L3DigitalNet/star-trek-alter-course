---
schema_version: '1.1'
id: 'adr-0018-star-trek-alter-course-separate-simulation-session-lifetime-from-workspaces'
title: 'ADR 0018: Separate Simulation-Session Lifetime from Workspaces'
description: 'Keeps workspaces subordinate to one session and prevents retained actions or preview data from crossing simulation replacement boundaries.'
doc_type: 'adr'
status: 'active'
created: '2026-09-27'
updated: '2026-09-27'
reviewed: '2026-09-27'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'godot'
  - 'ui'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0014-use-an-extensible-bounded-ship-system-substrate.md'
  - 'docs/adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md'
  - 'docs/adr/0016-own-actor-knowledge-and-share-immutable-observation-reports.md'
  - 'docs/wiki/interface-and-player-commands.md'
  - 'docs/wiki/content-assets-and-persistence.md'
supersedes: []
superseded_by: null
source: []
confidence: 'high'
visibility: 'public'
license: 'MIT'
project:
  decision_makers:
    - 'project owner'
  consulted: []
  informed: []
  amends: []
  amended_by: []
---

# Separate simulation-session lifetime from workspaces

## Context and Problem Statement

Command, Engineering, and Combat present one persistent world. Workspace changes must not recreate it. Loading a different world creates a different hazard: a retained button, selection, or deferred callback can carry a numeric identity that is valid again but now refers to another ship or installation.

The existing shell keeps session ownership in `GameScreen`, resolves current action payloads, and binds own-ship Engineering actions to owner plus simulation generation. This record formalizes session and action lifetime, not screen layout or a permanent requirement that one class own every presentation responsibility.

It applies when creating, clearing, replacing, or presenting a simulation session, and when retaining actionable UI context across refreshes, workspace changes, or deferred work. It does not define new menus, multiple simultaneous worlds, networking, refit gameplay, or a new UI framework.

How should workspaces share a persistent simulation while preventing stale interaction or preview data from acting on a replacement world?

## Decision Drivers

- Workspace selection must not control domain existence or simulation progress.
- Stable numeric IDs alone do not establish identity across different loaded worlds.
- Refresh should preserve valid focus and selection without preserving obsolete command payloads.
- Failed load must preserve the live game; successful replacement must invalidate old actionable context.
- Preview fixtures must remain visibly and operationally separate from production truth.
- Native Godot composition should remain adequate without global managers or generalized MVVM infrastructure.

## Considered Options

- Own the simulation at session lifetime and bind retained actions to current session context.
- Let each workspace construct and own its own simulation or mutable copy.
- Share a global mutable simulation and trust matching control IDs after replacement.

## Decision Outcome

Chosen option: "Own the simulation at session lifetime and bind retained actions to current session context", because it preserves one authoritative world while making replacement a deliberate interaction boundary.

### Session and workspace ownership

A session owns the live `GameSimulation` and the coordination needed to present its current player projection. Workspaces select how that projection is displayed; opening or closing one does not create, clone, or replace simulation truth.

`GameScreen` is the current owner. A concrete future lifecycle consumer may justify extracting a focused session object. This ADR does not freeze `GameScreen` as a monolith or authorize a global service locator, event bus, application framework, or second command authority.

Core continues to own domain rules under ADR 0001. Native Godot controls, scenes, input, containers, drawing, and the runtime theme own presentation. Workspace state is not a Core encounter state.

### Replacement and failure boundaries

Construct and validate a loaded simulation in isolation under ADR 0006. Install it only after loading succeeds. A failed load leaves the existing playable simulation intact.

Successful installation or clearing changes the session's simulation generation and invalidates previously presented actionable context. Rebuild or revalidate selections, drafts, recent activity, and deferred interactions according to their owning UI contracts. Do not use a matching numeric ID as evidence that an old action still refers to the same object.

A post-installation presentation failure does not mean the prior world remains installed. ADR 0015 governs truthful separation of committed application work from its presentation; the same distinction applies to session replacement reporting.

### Current payload and origin binding

Retained actions must resolve their payload from the current projection at activation and establish that the context in which they were offered still applies. Session generation identifies a replacement boundary; owning-ship identity disambiguates ship-local installation IDs. These presentation bindings do not replace Core's execution-time legality checks.

`OwnShipActionBinding` currently supplies the owner/generation stamp for installed-system Engineering actions. Other controls may satisfy the contract through clearing, rebuilding, or current-context resolution; this ADR does not assert that every existing control uses that specific record type or require a stamp on a non-actionable label.

An ordinary projection refresh keeps the generation, preserves still-valid stable keys, selection, and focus, and resolves updated payloads. Removed or invalid actions become non-activatable. A load may reuse the same IDs with different meanings; controls offered before it must not operate afterward merely because those IDs still exist.

Deferred callbacks must revalidate their target lifetime and context when they run. Capturing a node or command payload is not proof that it remains attached, visible, focusable, or associated with the current session.

### Three kinds of state

Durable world state belongs to Core snapshots. Session-local interaction state includes selection, drafts, focus, and action bindings. Application preferences, such as a selected presentation rate where the UI contract preserves it, are distinct from both.

The current quick-load path preserves the selected rate but discards fractional presentation-time carry accumulated before replacement. That carry must not advance restored simulation truth. Simulation generation is a presentation-lifetime guard, not a persistent world identity or an additional save field.

Future preferences or restoration behavior require a concrete UI contract. This record does not indiscriminately persist every control value or require every preference to reset.

### Preview isolation

A deterministic preview fixture is allowed only in explicit development/test preview mode. It must not be substituted for missing live facts, advance a live simulation, issue gameplay commands, or enter persistence.

Production reports unavailable data or capability honestly. A visual reference may illustrate a future system, but neither that reference nor a preview grants its gameplay implementation. ADR 0016 continues to govern actor-safe live information.

### Consequences

- Good, because moving between workspaces preserves one coherent world.
- Good, because reused IDs cannot silently redirect stale actions after load.
- Good, because ordinary refresh can preserve usable focus and selection.
- Bad, because replacement requires deliberate invalidation and rebuilding of interaction state.
- Bad, because deferred UI work needs lifecycle-aware checks rather than assuming its captured context remains valid.

### Confirmation

Apply this record when changing session ownership, save/load installation, workspace lifecycle, retained command payloads, deferred interaction, or preview. Confirm simulation identity across workspace changes, failure preservation on invalid load, stale-action refusal after replacement with equal IDs, valid interaction across ordinary refresh, and preview's inability to mutate or persist a live world. Test deferred focus/action behavior against removed or replaced controls.

Evidence entry points are [GameScreen](../../src/AlterCourse.Godot/src/Gameplay/GameScreen.cs), [OwnShipActionBinding](../../src/AlterCourse.Godot/src/Gameplay/OwnShipActionBinding.cs), [EngineeringWorkspace](../../src/AlterCourse.Godot/src/Gameplay/EngineeringWorkspace.cs), and [shell tests](../../src/AlterCourse.Godot/tests/GameplayShellTest.gd), including the stale Engineering-control tests indexed by [Interface and player commands](../wiki/interface-and-player-commands.md#sources-and-evidence). Existing test links are not a fresh execution claim.

## Pros and Cons of the Options

### Session ownership and context-bound actions

- Good, because domain lifetime and interaction lifetime are explicit but separate.
- Bad, because session replacement needs coordinated presentation reconciliation.

### Workspace-owned simulations

- Good, because each screen appears self-contained.
- Bad, because world state, time, and commands can diverge or reset on navigation.

### Global mutable simulation with ID-only controls

- Good, because every control can easily locate the current world.
- Bad, because access does not establish the origin or continued validity of a retained command.

## More Information

The owner selected this record after the September 27, 2026 additional-ADR review. It formalizes D-05 and the implemented owner/generation boundary inspected at `efd4ac93894fc28ea0c3ea40ca1be008dd4d6631`. Layouts, colors, shortcuts, and detailed workspace behavior remain in the owning interface contract. No new workspace, session service, save schema, or gameplay feature is introduced by adoption.
