---
name: stac-architecture
description: Route ST:AC work through the pure Core, Godot, installed-system, command, knowledge, asset, session, content, persistence, AI, and verification boundaries.
---

# ST:AC architecture router

Load this skill before implementation. The design wiki under `docs/wiki/` owns game contracts; active ADRs own architectural decisions. This project-owned skill is a routing aid, not a replacement for either. Keep its `.claude/` and `.codex/` copies identical.

## Boundary

- `AlterCourse.Core` owns authoritative simulation and domain rules. Future diplomacy, campaigns, narrative consequences, or stochastic features belong behind that authority when admitted; listing a domain here does not mean it is implemented.
- `AlterCourse.Godot` owns scenes, nodes, input, workspaces, rendering, animation, audio, and engine adapters. It projects actor-safe Core state and submits typed intent.
- `AlterCourse.AssetCtl` is independent development tooling. Neither game project references it, and it references neither game project. The game consumes selected files, not providers.
- Core never depends on Godot. Scene lifecycle, transforms, Resources, timers, signal order, wall clock, ambient RNG, and runtime graph serialization do not become domain authority.

## Cross-cutting routes

- **Content and saves:** ADRs 0005/0006 own strict ordinary JSON definitions and explicit versioned snapshots, with compatibility, bounded admission, semantic validation, and non-inventive migrations. Resources remain presentation assets.
- **Time and randomness:** ADR 0007 owns deterministic simulation time, stable scheduled work, and bounded advancement. There is no current authoritative RNG consumer; do not introduce a random call before the required versioned source, stream ownership, state capture, and continuation proof.
- **Commands:** ADR 0015 owns candidate composition, complete commit boundaries, refusal preservation, and truthful post-commit outcomes. Validate current intent in Core; do not let UI availability become legality. Preserve ADR 0007's separately specified safely incremental alternative rather than silently changing atomic operations.
- **Knowledge:** ADR 0016 owns actor-local information, source provenance, immutable historical reports, and disclosure through selectors/refusal reasons as well as projections. Own-asset administration is not a shared sensor network. Hidden target correlation is not a public identity.
- **Sessions:** ADR 0018 keeps workspace lifetime separate from simulation lifetime. Resolve current action payloads and invalidate stale context on simulation replacement even when IDs match. Preview is explicit and never live authority.
- **Assets:** ADR 0017 owns capability-based routing, bounded external operations, untrusted-output validation, recoverable paired publication, provenance, and owner-controlled approval. Offline verification needs no generation, credentials, or paid provider calls.
- **Diagnostics:** ADR 0008 composes Serilog through Microsoft logging abstractions. Core records allowlisted post-commit facts through an optional logger; logging failure cannot change a committed result.
- **Tests:** ADR 0009 owns layered evidence. xUnit and vendored GdUnit4 support current tests; CsCheck and ArchUnitNET remain admitted Core-test-only consumers. GdUnit4Net is conditional, not presumed installed. Named scenarios, negative cases, and long horizons complement structural tests.
- **AI and quantities:** ADR 0010 owns explainable information-limited policies and typed proposals; no LLM becomes core decision authority. ADR 0011 owns explicit physical/fictional quantities and the recorded bounded existing-quantity exception. Do not turn selective UnitsNet admission into a claim that it is installed everywhere.
- **Narrative and dependencies:** ADR 0012 keeps conditional branching narrative subordinate to Core. ADR 0003 requires a concrete need and admission evidence before packages, addons, frameworks, or managers are added.

## Ship-system conformance

Before ship-system expansion, read ADR 0014 and `docs/wiki/ship-system-substrate.md`. Issue #121's migration is implemented through Final PR #123 and remains unreleased. Current runtime, content, persistence, and generic Engineering use installed systems; recovery gameplay and additional system kinds remain separate decisions.

Common condition, repair, allocation, current snapshots, and generic controls use installed-system identity. Kind, reusable definition, and installation are distinct. Live loadouts are authoritative; class defaults are bootstrap input. Specialized effects and cardinality stay typed. Frozen historical DTO fields and isolated translators are valid; parallel current fields behind a collection facade are not.

Preserve the owning contract's evidence for heterogeneous same-class loadouts, arbitrary IDs/order, exact continuation, absent capabilities, cardinality validation, direct current-format capture, and hidden-loadout safety. Do not expose remote inventory through target choices, action availability, or absence-specific rejection. Own-ship operations and remote semantic aim have different information contracts.

## Design routing

Read the owning wiki page before changing behavior and reconcile it in the same governed work. Keep formulas, failure rules, and milestone acceptance in the wiki. README/ROADMAP are entry points; ADRs retain architectural authority. Unapproved ideas belong in open questions rather than becoming requirements through an agent prompt.

Follow `docs/wiki/development-and-governance.md#recurring-design-reconciliation` at task start, behavior/bug checkpoints, Ready, and landing. Check `docs/wiki/sources.md#review-record` for overdue full sweeps. Record named pages and source/test evidence in PR acceptance coverage; an implementation constraint does not silently amend approved design.

| Concern | Wiki page |
| --- | --- |
| Vision, non-goals, priorities | `docs/wiki/vision-and-scope.md` |
| Implemented versus planned | `docs/wiki/implementation-status.md` |
| Project boundaries and cross-cutting ownership | `docs/wiki/architecture.md` |
| Installed-system substrate and admission | `docs/wiki/ship-system-substrate.md` |
| Ships, bootstrap, orders, space, time | `docs/wiki/world-navigation-and-time.md` |
| Sensors, contacts, scan/hail, AI | `docs/wiki/sensors-knowledge-and-ai.md` |
| Historical contact information | `docs/wiki/strategic-contact-reporting.md` |
| Own-asset administrative assignment | `docs/wiki/faction-intent-and-autonomous-assignment.md` |
| Delayed reports and investigation | `docs/wiki/observation-driven-faction-response.md` |
| Power, condition, repair, combat | `docs/wiki/engineering-and-combat.md` |
| Factions, organizations, jurisdiction | `docs/wiki/factions-and-organizations.md` |
| Diplomacy, economy, campaigns, narrative | `docs/wiki/diplomacy-economy-and-campaigns.md` |
| Workspaces, controls, session replacement | `docs/wiki/interface-and-player-commands.md` |
| Content JSON, saves, AssetCtl | `docs/wiki/content-assets-and-persistence.md` |
| Detailed development-tool contract | `docs/wiki/asset-pipeline-tool.md` |
| Toolchain, gate, branches, documentation | `docs/wiki/development-and-governance.md` |
| Settled decisions and unresolved choices | `docs/wiki/decision-register.md`, `docs/wiki/open-questions.md` |

## ADR routing

Use `docs/adr/README.md` for the complete active catalog, scope overlap, and amendment discipline. The catalog links the actual files; this router does not maintain a second title inventory.

ADRs 0001–0014 retain their existing scopes. ADRs 0015–0018 add focused ownership for commands, knowledge, AssetCtl, and session lifetime. The September 27 191-obligation conformance register covers the original fourteen records; it is not automatic certification of later ADRs. Inspect applicability before demanding a conditional package or future component.

## Verification

Run the narrowest relevant tests while iterating. Use `./scripts/fix.sh` when formatting is appropriate, then run the canonical read-only `./scripts/verify.sh` before declaring integrated implementation complete. Keep inspected tests, inherited results, and fresh execution distinct. Do not weaken analyzers, settings, tests, or admission mechanisms to make a failure disappear.
