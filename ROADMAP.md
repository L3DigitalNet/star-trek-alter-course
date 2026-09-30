# Star Trek: Alter Course Development Roadmap

## Purpose and authority

This roadmap records sequence and milestone status. Game behavior and detailed acceptance criteria belong in the [design wiki](docs/wiki/README.md), including [milestone proofs](docs/wiki/milestone-proofs.md). A future milestone is not an implementation authorization.

[Implementation status](docs/wiki/implementation-status.md) distinguishes development from release. [STATUS](docs/STATUS.md) and [TODO](docs/TODO.md) own operational work; [ADR 0013](docs/adr/0013-use-dev-for-development-and-main-for-releases.md) owns release admission. Completing a milestone does not create a release.

## Sequence

| Milestone | Status | Acceptance criteria |
| --- | --- | --- |
| M1 — World State and Bootstrap Generalization | Implemented | [World/bootstrap proof](docs/wiki/milestone-proofs.md#milestone-1--world-state-and-bootstrap-generalization) |
| M2 — Active World and Persistent Orders | Implemented | [Orders proof](docs/wiki/milestone-proofs.md#milestone-2--active-world-and-persistent-orders) |
| M3 — Sensors, Knowledge, and First Contact | Partial: M3A and Strategic Contact Reporting implemented | [Remaining M3 scope](docs/wiki/milestone-proofs.md#milestone-3-completion-remains-open-beyond-the-implemented-slices) |
| M4 — Engineering Backbone and Degraded Operations | Implemented | [Engineering proof](docs/wiki/milestone-proofs.md#milestone-4--engineering-backbone-and-degraded-operations) |
| M5 — Living Sector and Faction Autonomy | Partial: faction assignment and Observation-Driven Faction Response implemented | [Broader autonomy proof](docs/wiki/milestone-proofs.md#milestone-5--living-sector-and-faction-autonomy) |
| M6 — Tactical Combat Foundation | Partial: M6A first combat engagement implemented on dev | [Combat proof](docs/wiki/milestone-proofs.md#milestone-6--tactical-combat-foundation) |
| M7 — Diplomacy, Incidents, and Durable Consequences | Future | [Political consequence proof](docs/wiki/milestone-proofs.md#milestone-7--diplomacy-incidents-and-durable-consequences) |
| M8 — Canon-Anchored Campaign Bootstrap and Divergent History | Future | [Campaign proof](docs/wiki/milestone-proofs.md#milestone-8--canon-anchored-campaign-bootstrap-and-divergent-history) |
| M9 — Persistent Regional Campaign Integration | Future | [Regional integration proof](docs/wiki/milestone-proofs.md#milestone-9--persistent-regional-campaign-integration) |

The order expresses dependencies and risk, not a dated release schedule. Governed refinement may split or combine work when evidence justifies it. Resolve only the [open questions](docs/wiki/open-questions.md) required by the next selected slice; preserve completed slices' scope and history.

## Current position

The current source-only release is v0.6.2; its gameplay baseline remains v0.6.0 with V8 saves. It includes [Faction Intent and Autonomous Assignment](docs/wiki/faction-intent-and-autonomous-assignment.md) and [Observation-Driven Faction Response](docs/wiki/observation-driven-faction-response.md), two bounded M5 contributions. The latter closes a legitimate observation → faction knowledge → investigation loop without completing M3 or M5.

[Strategic Contact Reporting](docs/wiki/strategic-contact-reporting.md) retains its original scope and historical v0.5.0/V6 admission. It is not a renamed M3B and did not itself introduce faction autonomy. Full M3/M5 completion was not required before the first combat refinement.

M6A first engagement is implemented and unreleased: directed-energy combat, all-aspect shields, power/damage reconciliation, actor-safe targeting, and bounded defensive reaction. Its V9 admission is historical; current development uses V10 after the substrate migration. [Content and persistence](docs/wiki/content-assets-and-persistence.md) owns the complete compatibility record.

## Completed substrate prerequisite and next selection

[Issue #121](https://github.com/L3DigitalNet/star-trek-alter-course/issues/121) implemented [ADR 0014](docs/adr/0014-use-an-extensible-bounded-ship-system-substrate.md) for the five existing system kinds. Final [PR #123](https://github.com/L3DigitalNet/star-trek-alter-course/pull/123) merged as `17637dd` and remains unreleased. The [substrate contract](docs/wiki/ship-system-substrate.md) owns compatibility, information safety, and admission evidence. Current development uses ship content V6, system-definition V1, and V10 saves.

The prerequisite is complete; the next gameplay slice awaits owner selection. Recovery, refit, aggregation, and later tactical refinements need their own bounded design. The earlier recovery prompt is not the current work order. This migration is not M6B, a release, or completion of M3/M5/M6.

The September 27 documentation work adds ADRs 0015–0018 for existing command, knowledge, asset, and session boundaries. These [architectural records](docs/adr/README.md) do not select another gameplay milestone or change release status.

Keep behavior and proof details in the wiki, architecture through ADRs, and operational work in its existing records. Do not turn an illustrative future proof into approved scope merely by moving it on the roadmap.
