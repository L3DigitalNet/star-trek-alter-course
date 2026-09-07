# Star Trek: Alter Course Development Roadmap

## Purpose and authority

This file records development sequence and milestone status. Game behavior and detailed acceptance criteria live in the [design wiki](docs/wiki/README.md), including [milestone proofs](docs/wiki/milestone-proofs.md). It does not define additional game rules or authorize implementation of a future slice.

[Implementation status](docs/wiki/implementation-status.md) distinguishes current `dev` from released behavior. [STATUS](docs/STATUS.md) and [TODO](docs/TODO.md) own operational work; [ADR 0013](docs/adr/0013-use-dev-for-development-and-main-for-releases.md) owns releases. Completing a milestone does not itself create a release.

## Sequence

| Milestone | Status | Acceptance criteria |
| --- | --- | --- |
| M1 — World State and Bootstrap Generalization | Implemented | [World/bootstrap proof](docs/wiki/milestone-proofs.md#milestone-1--world-state-and-bootstrap-generalization) |
| M2 — Active World and Persistent Orders | Implemented | [Orders proof](docs/wiki/milestone-proofs.md#milestone-2--active-world-and-persistent-orders) |
| M3 — Sensors, Knowledge, and First Contact | Partial: M3A and Strategic Contact Reporting implemented | [Remaining M3 scope](docs/wiki/milestone-proofs.md#milestone-3-completion-remains-open-beyond-the-implemented-slices) |
| M4 — Engineering Backbone and Degraded Operations | Implemented | [Engineering proof](docs/wiki/milestone-proofs.md#milestone-4--engineering-backbone-and-degraded-operations) |
| M5 — Living Sector and Faction Autonomy | Partial: Faction Intent and Autonomous Assignment implemented | [Broader autonomy proof](docs/wiki/milestone-proofs.md#milestone-5--living-sector-and-faction-autonomy) |
| M6 — Tactical Combat Foundation | Future | [Combat proof](docs/wiki/milestone-proofs.md#milestone-6--tactical-combat-foundation) |
| M7 — Diplomacy, Incidents, and Durable Consequences | Future | [Political consequence proof](docs/wiki/milestone-proofs.md#milestone-7--diplomacy-incidents-and-durable-consequences) |
| M8 — Canon-Anchored Campaign Bootstrap and Divergent History | Future | [Campaign proof](docs/wiki/milestone-proofs.md#milestone-8--canon-anchored-campaign-bootstrap-and-divergent-history) |
| M9 — Persistent Regional Campaign Integration | Future | [Regional integration proof](docs/wiki/milestone-proofs.md#milestone-9--persistent-regional-campaign-integration) |

The order expresses dependency and risk, not a fixed release schedule. Governed refinement may split or combine work when evidence justifies it. Resolve only the [open questions](docs/wiki/open-questions.md) needed by the next selected slice; preserve the scope and history of completed slices.

## Current position

The released baseline is source-only v0.5.0. Current `dev` also contains [Faction Intent and Autonomous Assignment](docs/wiki/faction-intent-and-autonomous-assignment.md), the first bounded M5 contribution. M3 and M5 remain incomplete. [Strategic Contact Reporting](docs/wiki/strategic-contact-reporting.md) remains its original bounded slice, not a renamed M3B or a claim of faction autonomy.

Maintain behavior and acceptance detail in the wiki, architecture changes through ADRs, and work/release state in the operational records. Review the [milestone proof record](docs/wiki/milestone-proofs.md) when changing sequence; do not silently turn an example or future proof into approved implementation scope.
