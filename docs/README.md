---
schema_version: '1.1'
id: 'index-l97l7i-documentation'
title: 'Project Documentation'
description: 'Entry point to the design wiki, the single source of truth for design.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-07'
tags:
  - 'design'
aliases: []
related:
  - 'docs/wiki/README.md'
  - 'ROADMAP.md'
---

# Project documentation

Start with the [project design wiki](wiki/README.md). It is the single source of truth for the game's design: vision, implemented and planned systems, architecture, approved decisions such as the political model, and remaining questions.

The wiki is stored in this repository so changes use the same review and version history as the game. It is not a second GitHub Wiki repository or a separately deployed documentation service.

Game-design contracts, detailed rules, and [milestone proofs](wiki/milestone-proofs.md) live inside the wiki. The former design/specification documents have been consolidated there; the [source catalog](wiki/sources.md#consolidation-map) records where their content went.

The remaining documents have distinct authority:

| Location | Owns | Does not own |
| --- | --- | --- |
| [Wiki](wiki/README.md) | Intended game behavior, implemented contracts, future design, decisions and questions | Live task or deployment state |
| [ADRs](adr/) | Architectural decisions and their rationale | A separate set of gameplay rules |
| [STATUS](STATUS.md), [TODO](TODO.md), [handoff](handoff/) | Work state, operational facts and historical session evidence | New game-design decisions |
| [Roadmap](../ROADMAP.md), [root README](../README.md) | Sequence and onboarding pointers | Detailed game contracts |
| [Development quality](development-quality.md), [agent skills](development-agent-skills.md) | Tool setup, verification and harness maintenance | Gameplay semantics |
| [Dependency admission](dependency-admission/) | Package-admission evidence under ADR 0003 | Game features or blanket approval of future dependencies |
| [License](../LICENSE.md), [legal notice](../LEGAL.md) | Licensing and rights boundaries | Game-design authority |

Historical discussions are linked to fixed Git revisions in the wiki's [provenance record](wiki/sources.md#historical-provenance), rather than maintained as competing design files. Source/schema/tests establish what the implementation actually does; they do not silently approve a change to intended design.
