---
schema_version: '1.1'
id: 'concept-81vyg9-engineering-and-combat'
title: 'Engineering and Combat'
description: 'Implemented Engineering capabilities and the planned systems-driven combat foundation.'
doc_type: 'concept'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'engineering'
  - 'simulation'
aliases: []
related:
  - 'docs/design/engineering-backbone.md'
  - 'docs/adr/0011-represent-physical-quantities-with-explicit-units.md'
  - 'ROADMAP.md'
---

# Engineering and combat

[Wiki home](README.md) · [Sensors and AI](sensors-knowledge-and-ai.md) · [Implementation status](implementation-status.md)

## Implemented Engineering chain

M4 connects generation → allocation → effective system capability → detection/movement → repair and subsequent capability. It is deliberately a small interconnected model rather than a final catalog of ship systems.

The concrete system identities are power generation, sensors, and impulse propulsion. `SystemCondition` is bounded from zero to one. `PowerUnits` is a bounded abstract integral game quantity, not watts, energy, antimatter, or a fuel inventory. Sensors and impulse accept allocation and repair; generator repair is not supported in this slice.

Available power is the floor of nominal generation multiplied by generator condition. Allocations cannot exceed each consumer's nominal demand or total available power. Unallocated reserve is legal. Consumer capability is condition multiplied by allocated-to-demand power satisfaction, capped at one. Effective passive range and maximum tactical speed derive from that capability.

Core generates Balanced, Prioritize Sensors, and Prioritize Propulsion choices. It also validates exact allocation. An allocation that would leave current tactical speed above the resulting propulsion limit is rejected atomically; it does not silently decelerate the ship. Strategic travel timing is unchanged by this impulse model.

## Current proof values

The Pathfinder definition provides 120 generation units, 70 sensor-demand units, and 50 impulse-demand units. The player starts with generator condition 0.625, yielding 75 available units. Balanced allocates 44/31, Sensors-first 70/5, and Propulsion-first 25/50. These are present proof values, not permanent balance commitments.

The authored passive range is 30 km; active scan takes 2,000 ms. Sensor repair takes 8,000 ms and impulse repair 6,000 ms. Initial damaged sensor condition and repair make detection change as simulation time passes. These values are owned by [current content](../../src/AlterCourse.Godot/content/ships/pathfinder.json) and the [Engineering design](../design/engineering-backbone.md).

## Repair and degradation

Each ship can own one active `SystemRepairState`, targeting sensors or impulse. It retains start/target conditions, times, and one exact completion-work identity. Progress is analytically interpolated, and completion materializes the exact target once. Repair continues during travel and while other workspaces are displayed.

Power changes reconcile contacts immediately. Zero sensor capability interrupts the exact active scan and cancels its work; returning power does not resume it automatically. Tactical player and cautious-AI courses use the same effective propulsion limit.

This is degraded operation and repair, not arbitrary combat damage generation. There are no crew-assigned repair teams, queues, spare parts, fuel consumption, EPS bus simulation, heat, batteries, or generator repair mechanics yet.

## Planned combat foundation

M6 should compose existing movement, knowledge, power, condition, AI, persistence, and engineering rather than create a parallel hit-point game. The roadmap proposes a small engagement with targeting, shields, a bounded weapon family, subsystem consequences, repair pressure, maneuver, and withdrawal.

Targeting must use actor knowledge. Shield and weapon capability must connect to power and condition. Damage should affect what ships can do, even if structural survival is also represented. Surviving actors retain their identity and consequences when an engagement ends; leaving the tactical view does not despawn them.

The roadmap suggests directed energy as a likely first weapon but does not irrevocably select it. Shield facings, firing geometry, damage distribution, disengagement, and random-resolution details remain open until combat refinement. Torpedo logistics, boarding, cloaking, large fleets, advanced electronic warfare, and final balance are deferred.

## Verification boundary

M4 tests cover allocation conservation/bounds, atomic rejection, contact/scan effects, propulsion limits, analytical repair, exact work correlation, V5 continuation, content validation, and stable live UI actions. Combat requirements above are not claims about passing combat tests or current playable weapons.

## Sources

[Engineering Backbone](../design/engineering-backbone.md), [quantities ADR](../adr/0011-represent-physical-quantities-with-explicit-units.md), [Core ships](../../src/AlterCourse.Core/Ships/), and [roadmap M4/M6](../../ROADMAP.md).
