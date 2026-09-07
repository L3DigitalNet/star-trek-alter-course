---
schema_version: '1.1'
id: 'concept-u5zyw0-vision-and-scope'
title: 'Vision and Scope'
description: 'Game identity, command responsibilities, progression, and scope boundaries.'
doc_type: 'concept'
status: 'active'
created: '2026-09-06'
updated: '2026-09-07'
tags:
  - 'design'
aliases: []
related:
  - 'README.md'
  - 'ROADMAP.md'
  - 'docs/wiki/sources.md'
---

# Vision and scope

[Wiki home](README.md) · [Decisions](decision-register.md) · [Open questions](open-questions.md)

## The game being built

ST:AC is a single-player, map-centric, 2D starship command and strategy simulation inspired by Super Star Trek, EGA Trek, and Netrek. The player commands a starship as captain. TOS, TNG, DS9, and Voyager supply the primary setting inspiration; a particular campaign epoch is not selected by this wiki.

The core play is making command decisions inside an autonomous persistent world, not moving an individual RPG character through a sequence of encounters. Navigation, sensors, power, propulsion, damage, repair, weapons, shields, communication, politics, and logistics should interact through domain rules.

## Progression and consequences

There is no conventional captain experience-level or skill-tree progression. Progress is expressed through relationships, reputation, discoveries, political circumstances, territory, wars, treaties, resources, ship condition, and the world changed by player and NPC action.

The original owner brief also requested officers in command positions with traits and experience, crew condition and morale, and canon-bounded ship upgrades. These remain unimplemented design intent requiring their own specifications; they do not authorize a captain-leveling system. See the [historical provenance](sources.md#historical-provenance) and the current [roadmap](../../ROADMAP.md), rather than treating every early numerical suggestion as settled.

## A world that continues without the player

Every substantial simulation feature should answer: what happens if the player never interacts with it? Ships can already be traveling, patrolling, holding, or repairing when play begins. Later factions should pursue interests and create consequences while the player is elsewhere. A map or scene change must not create an actor merely for an encounter or erase a surviving actor afterward.

Persistent does not mean wall-clock online progression. The universe advances through explicit simulation time, and pausing or closing the application does not secretly advance it. See [World, navigation, and time](world-navigation-and-time.md).

## Star Trek problem-solving

Combat is one possible response, not the default definition of success. The design should support diplomacy, withdrawal, negotiation, assistance, investigation, deception, trade, and restraint when the situation permits. Consequences should reflect context, values, known facts, prior actions, and political obligations rather than a universal good/evil score.

A damaged ship should remain functional in interesting ways. Engineering complexity earns its place by creating decisions, not by requiring bookkeeping without consequences.

## Presentation and priorities

A large tactical or strategic map, persistent ship information, contextual panels, readable telemetry, mouse input, and keyboard controls take priority over spectacle. This is not a 3D bridge simulator, third-person space action game, or conventional character RPG.

Development priorities are simulation correctness, maintainability, automated testability, player-visible clarity, performance, then visual polish. Prefer small interconnected vertical slices over separately elaborate subsystems. Add generalization when concrete consumers justify it, not because the eventual galaxy might be large.

## Sources

The [root overview](../../README.md), [roadmap](../../ROADMAP.md), and [historical provenance](sources.md#historical-provenance) establish the vision. [Implementation status](implementation-status.md) distinguishes this vision from playable features.
