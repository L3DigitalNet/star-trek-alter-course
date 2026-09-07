---
schema_version: '1.1'
id: 'index-l97l7i-documentation'
title: 'Project Documentation'
description: 'Entry point to the design wiki, the single source of truth for design.'
doc_type: 'index'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'design'
aliases: []
related:
  - 'docs/wiki/README.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'ROADMAP.md'
---

# Project documentation

Start with the [project design wiki](wiki/README.md). It is the single source of truth for the game's design: vision, implemented and planned systems, architecture, approved decisions such as the political model, and remaining questions.

The next owner-approved design is [Faction Intent and Autonomous Assignment](wiki/faction-intent-and-autonomous-assignment.md). Its proof, six decisions, non-goals, and acceptance contract are recorded before implementation. Runtime remains v0.5.0 with save V6 and content V4; planned V7 and faction behavior are not implemented by this documentation.

The wiki is stored in this repository so changes use the same review and version history as the game. It is not a second GitHub Wiki repository or a separately deployed documentation service.

[Current status](STATUS.md), [future work](TODO.md), and [the roadmap](../ROADMAP.md) describe operational state and sequence, not design. [ADRs](adr/) record architectural decisions. The documents under [`design/`](design/) and [`specs/`](specs/) supply supporting detail for wiki pages and may not contradict them; the wiki [source catalog](wiki/sources.md) indexes them together with development references and historical records.
