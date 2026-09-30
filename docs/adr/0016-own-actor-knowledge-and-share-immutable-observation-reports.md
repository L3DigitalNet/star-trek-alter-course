---
schema_version: '1.1'
id: 'adr-0016-star-trek-alter-course-own-actor-knowledge-and-share-immutable-observation-reports'
title: 'ADR 0016: Own Actor Knowledge and Share Immutable Observation Reports'
description: 'Separates world truth, actor knowledge, administrative facts, and historical report transfer without implicit identity correlation or disclosure.'
doc_type: 'adr'
status: 'active'
created: '2026-09-27'
updated: '2026-09-27'
reviewed: '2026-09-27'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'sensors'
  - 'ai'
aliases: []
related:
  - 'docs/adr/0004-own-semantic-spatial-model-and-adapt-godot-rendering.md'
  - 'docs/adr/0006-use-versioned-json-snapshot-saves.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md'
  - 'docs/adr/0014-use-an-extensible-bounded-ship-system-substrate.md'
  - 'docs/adr/0015-use-staged-core-command-application-and-explicit-commit-outcomes.md'
  - 'docs/wiki/sensors-knowledge-and-ai.md'
  - 'docs/wiki/strategic-contact-reporting.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/wiki/observation-driven-faction-response.md'
  - 'docs/wiki/open-questions.md'
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

# Own actor knowledge and share immutable observation reports

## Context and Problem Statement

Sensors, AI, factions, presentation, and persistence all consume information about a world that no ordinary actor knows completely. A ship-local contact, an administrative record of an owned asset, and a delayed observation report have different origins and authority. Treating them as interchangeable views of the current target object would make fog of war cosmetic and rewrite historical knowledge when hidden truth changes.

ADRs 0004 and 0010 require actor-appropriate spatial and AI inputs. This record governs the production, ownership, retention, and transfer of information feeding those consumers, including indirect disclosure through command feedback. It applies when consequential information is observed, remembered, reported, projected, or used for decisions.

It does not define a universal intelligence service, sensor physics, confidence model, political-memory system, communications network, or cross-observer vessel identity. Their concrete rules remain separately governed.

How should actors acquire and share knowledge without implicitly acquiring current world truth or identities that they have not learned?

## Decision Drivers

- Information must influence decisions while retaining its actual source and age.
- World identity, observer-local contact identity, and reporting provenance are different meanings.
- Political or asset control must not silently become omniscient sensor access.
- Retention, scheduling, and save/load must preserve bounded consequential knowledge.
- UI and command feedback must not become hidden-state probes.
- Current narrow channels must not require a generalized message or intelligence framework.

## Considered Options

- Store actor-owned knowledge and transfer explicit immutable observation snapshots through governed channels.
- Let every consumer query a shared world repository and filter what it displays.
- Give each faction a live union of all controlled ships' sensor stores.

## Decision Outcome

Chosen option: "Store actor-owned knowledge and transfer explicit immutable observation snapshots through governed channels", because knowledge is consequential domain state rather than a last-minute rendering filter.

### Distinct information authorities

Core world truth remains authoritative for resolving actual physical and domain consequences. Actor knowledge records what an actor has legitimately learned. Administrative facts describe the explicitly admitted knowledge an actor has about its own assets and obligations. A historical report records what a source legitimately observed, not what the target currently is doing.

These responsibilities may use focused existing domain types; they do not require a common actor base class or universal knowledge object. Each consumer receives only the facts appropriate to its role. AI policies do not receive an unrestricted `SimulationState` or foreign `ShipState` merely because Core can access them.

Own-asset administrative facts need not be fabricated through sensors. Conversely, a direct controller relationship does not authorize a live union of sensor stores, foreign affiliation knowledge, or every possible fact about an owned asset. The owning feature contract specifies the permitted fields and authority.

### Identity and provenance

A hidden target's runtime identity, an observer-local contact ID, and a report's source provenance remain distinct. A local contact ID is meaningful in its observer's context; matching numeric IDs or learned names from different observers do not establish one vessel identity.

Core may retain hidden target correlation to apply rules correctly. That correlation must not escape into player projections, player-safe events, report payloads, or policy inputs unless a separately approved learning mechanism makes the relevant identity legitimately known. Source observer identity in a report identifies the reporter, not the hidden target.

No implicit cross-observer correlation is permitted. This does not prohibit a later explicit, justified correlation mechanism; it leaves that product and architectural decision open.

### Observation and historical transfer

Knowledge changes through legitimate observation, receipt, communication, or another explicitly admitted source. A report snapshots the facts available to its source at observation time, including applicable location/reference frame, time, local contact provenance, and learned identification.

Observation and receipt are distinct events. Delivery must not enrich a snapshot from current target state, later movement, true controller, hidden installation inventory, or a name-based identity inference. A received report is not a pointer whose meaning follows the live target.

A reporting channel explicitly defines eligible sources and recipients, authority, publication triggers, payload, timing, retention, and actionable meaning. The current direct NPC ship-to-direct-faction channel is one bounded consumer, not blanket approval of sharing with players, allies, organizations, or political parents.

### Bounded continuation

Persist only consequential knowledge and continuation required by the owning rules. Queued reports, exact delivery correlations, identity continuation, and handling/suppression state may be authoritative; derived rosters, formatted summaries, and reconstructible indexes are not alternate stores of truth.

Admission, expiry, eviction, duplicate handling, and feedback suppression must have deterministic bounded contracts. Removing a report or its delivery authority must not allow stale queued work, save/load, or cache eviction to manufacture a new observation or repeat an already-handled response.

Migration represents the historical world's information without inventing reports that were never sent, identifications that were never learned, or political history inferred from current bootstrap. ADRs 0006 and 0007 continue to own migration and scheduler mechanics.

### Decision inputs, consequences, and disclosure

A policy's inputs come from actor-appropriate information. Command application may use hidden truth to resolve actual consequences, but that is not permission to expose all of that truth in feedback.

Projections, enabled/disabled actions, refusal reasons, target choices, event text, and summaries are all disclosure surfaces. For example, remote semantic targeting must not reveal whether an unobserved target installation exists through an absence-specific refusal. Own-ship Engineering may legitimately expose the player's installed inventory; remote targeting has a different contract.

Different hidden worlds may produce different physical consequences. The information-safety requirement is not identical physical outcomes: it is that unauthorized facts do not enter decision inputs or disclosure, and feedback reveals only effects admitted by the acting observer's knowledge rules.

### Scope of existing gameplay contracts

The current contact lifecycle, publication episodes, report delay, capacities, freshness window, and investigation ordering remain in [Sensors, knowledge, and AI](../wiki/sensors-knowledge-and-ai.md), [Strategic Contact Reporting](../wiki/strategic-contact-reporting.md), and [Observation-Driven Faction Response](../wiki/observation-driven-faction-response.md). They are not universal constants imposed by this ADR.

Q-02 through Q-04 and Q-11 remain scoped or open as recorded in [Open questions](../wiki/open-questions.md). This record does not approve `KnownShipId`, affiliation learning, confidence, live shared sensing, intelligence fusion, broader propagation, or political incidents. D-03, D-09, D-10, D-15, and D-16 retain their historical and feature-specific meanings.

### Consequences

- Good, because remembered and shared information cannot silently follow hidden world truth.
- Good, because AI, UI, and communications share a coherent information boundary.
- Good, because reporting remains explainable through provenance, age, and recipient knowledge.
- Bad, because observations and reports require their own bounded state and validation.
- Bad, because apparent convenience features must account for indirect disclosure and historical context.

### Confirmation

Apply this record when adding information fields, observation/report channels, actor inputs, contact identities, or feedback that can reveal target facts. Confirm hidden-truth variation with legitimate actor inputs held constant, observer-local ID collisions, immutable report contents after target movement, exact delivery authority, bounded retention/suppression, and save/load continuation. Inspect refusals and disabled controls as well as projection properties.

Evidence entry points include [SensorKnowledge](../../src/AlterCourse.Core/Sensors/SensorKnowledge.cs), [report publication](../../src/AlterCourse.Core/Gameplay/GameSimulation.Observation.cs), [faction observation state](../../src/AlterCourse.Core/Factions/FactionObservationState.cs), [faction knowledge tests](../../tests/AlterCourse.Core.Tests/Gameplay/FactionKnowledgeBoundaryTests.cs), [observation-response scenarios](../../tests/AlterCourse.Core.Tests/Gameplay/ObservationResponseScenarioTests.cs), and the [remote-targeting contract](../wiki/ship-system-substrate.md#remote-targeting-must-not-become-an-inventory-probe). Linked tests are evidence to inspect and run for affected changes, not a claim that this documentation change reran them.

## Pros and Cons of the Options

### Actor-owned knowledge and explicit historical reports

- Good, because observation, administration, and transfer have explicit authority and persistence.
- Bad, because the implementation must manage freshness and retention rather than reading a live object.

### Shared world queries filtered at presentation

- Good, because consumers initially need fewer information models.
- Bad, because hidden facts can already affect choices before display filtering occurs.

### Live union of controlled sensor stores

- Good, because faction coordination is easy to implement.
- Bad, because it silently selects instantaneous sharing and erases channel timing, provenance, and knowledge ownership.

## More Information

The owner selected this record after the September 27, 2026 additional-ADR review. It supplements ADRs 0004 and 0010 without superseding them and codifies implemented information boundaries at `efd4ac93894fc28ea0c3ea40ca1be008dd4d6631`. Revisit only when a concrete learning, sharing, or historical-consequence consumer needs a new explicit contract.
