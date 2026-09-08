---
schema_version: '1.1'
id: 'concept-lobwve-diplomacy-economy-and-campaigns'
title: 'Diplomacy Economy and Campaigns'
description: 'Future strategic autonomy, durable political consequences, trade, narrative, and canon boundaries.'
doc_type: 'concept'
status: 'active'
created: '2026-09-06'
updated: '2026-09-08'
tags:
  - 'design'
  - 'simulation'
aliases: []
related:
  - 'ROADMAP.md'
  - 'docs/wiki/factions-and-organizations.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/adr/0012-keep-branching-narrative-subordinate-to-simulation.md'
---

# Diplomacy, economy, and campaigns

[Wiki home](README.md) · [Political decisions](factions-and-organizations.md) · [Open questions](open-questions.md)

## Status

These are future gameplay domains. The current v0.6.0 baseline supplies typed hail, bounded ship behavior, strategic last-known contact reporting, and bounded faction response, not treaties, broader faction planning, trade, governments, or a generated campaign. The broader political framework remains distinct from implemented runtime.

## Living sector and faction autonomy

M5 is intended to prove faction intent → autonomous ship assignment → offscreen activity → durable world change. The broader milestone requires multiple locations and ships, consequential choices by at least two autonomous political actors, and an NPC-NPC interaction that matters without the player witnessing it.

The first bounded contribution is implemented: [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md), delivered by Feature #86 / Final PR #87 and merged into `dev` as `0217296`. Two era-neutral root factions use the existing fictional map where practical. A chooses among its directly controlled idle NPC ships to establish presence at Vesper Reach, where B has a ship; committing A's preferred candidate must change its choice. Existing orders, travel, and sensors create the offscreen consequence. This first proof does not complete the broader M5 milestone.

The selected policy sees only its objective, explicitly known proof-map topology, and own-asset identity, strategic state, and assignment/order status. No sensor-report sharing, foreign truth, political affiliation learning, preemption, or player-command override is approved. It uses closed Ship/Faction scheduled work, typed bootstrap, the implemented non-inventive V7 migration, and no RNG or political UI. Q-05 is resolved for this consumer; the other scoped and deferred choices are maintained in [Open questions](open-questions.md).

Later faction consumers may add information, doctrine, resource constraints, and political interactions only when separately refined. Reports, investigation, rerouting, withdrawal, or support remain possible later mechanisms, not additions to the approved first assignment proof. Long-horizon tests should catch starvation, zero-time loops, order oscillation, unbounded state growth, dangling references, and save/load divergence.

The layered political model does not require implementing every depth, organization type, government mechanism, or covert operation in M5. Root-only scope now preserves the approved eventual recursive hierarchy without requiring child/grandchild runtime in the first slice.

## Diplomacy, incidents, and remembered consequences

M7 should make prior assistance, threats, restraint, violations, or attacks influence later behavior through durable domain state. Distinguish what happened, who knows or alleges it, and how a party interprets it. A faction should not react to secret activity it has no way to know about.

The approved design separates independent faction attitudes from formal legal arrangements and applies superior obligations through jurisdiction. A treaty is not just a relationship score copied to each child. Detailed diplomatic dimensions, treaty categories, attribution, memory retention, and enforcement remain open.

Logs are not political memory. Persist incidents or summarized state when later rules need them, not every tactical sample forever. History retention/aggregation requires explicit design when the first consumer arrives.

## Economy and institutions

The game intends to support trade, resources, services, assistance, repair, and political economic effects rather than a conventional RPG gold counter. The original owner brief treated money-like objects such as latinum as possible trade goods rather than a mandatory universal player balance. That is historical design intent, not an implemented accounting system.

Economic organizations can have type-specific resources, capabilities, actions, and goals under the approved organization model. It does not yet select commodities, production chains, pricing, logistics, fuel economics, or a complete market simulation. Those should be refined against a small useful gameplay proof, not imported as an external economy framework.

## Campaign bootstrap and canon

M8 should start a campaign from declared canonical boundary conditions plus valid noncanonical local activity already underway. The same supported inputs, seed, and generation/rules versions should reproduce the same starting state. Different seeds may vary local activity without violating required starting facts.

The roadmap provisionally favors canon as starting conditions and pressures rather than invisible corrections that erase legitimate divergence. The exact epoch, regional setting, treatment of later canonical events, and optional pre-start warm-up are not yet final decisions. In particular, an early assistant suggestion of 2378 in the archived brainstorm is not an approved campaign date. The first faction proof is explicitly era-neutral and consumes no RNG; it does not resolve campaign generation or select a random algorithm.

A warm-up, if justified, should run ordinary Core simulation rather than a second history engine. A saved campaign loads its evolved snapshot, not a regeneration from seed that discards subsequent actions. Entire-galaxy generation and automatic ingestion of external copyrighted reference material are not the current task.

M9 then integrates navigation, observations, engineering, autonomy, combat or avoidance, diplomacy, and persistence into one durable regional loop before broadening the world further.

## Missions and narrative

Missions/events should reflect the persistent world rather than freeze it into a sequence waiting for the player. Patrol, escort, aid, diplomacy, and investigation are design examples, not an implemented mission catalog.

ADR 0012 permits a future narrative runtime only when genuine branching authoring needs justify it. Ink is the first prototype candidate, with a separate pure-.NET Narrative boundary if admitted. Narrative receives allowed context and requests finite typed Core consequences; it never owns faction relationships, resources, time, or game-rule outcomes. Linear reports and the present hail seam do not require it.

## Sources

[Roadmap M5-M9](../../ROADMAP.md), [first approved faction slice](faction-intent-and-autonomous-assignment.md), [political framework](factions-and-organizations.md), [narrative ADR](../adr/0012-keep-branching-narrative-subordinate-to-simulation.md), [persistence ADR](../adr/0006-use-versioned-json-snapshot-saves.md), and [historical provenance](sources.md#historical-provenance).
