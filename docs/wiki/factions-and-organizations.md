---
schema_version: '1.1'
id: 'decision-dmdchb-factions-and-organizations'
title: 'Factions and Organizations'
description: 'Owner-approved conceptual political model with implementation and unresolved-detail boundaries.'
doc_type: 'decision'
status: 'active'
created: '2026-09-06'
updated: '2026-09-06'
tags:
  - 'design'
  - 'simulation'
aliases: []
related:
  - 'docs/wiki/decision-register.md'
  - 'docs/wiki/open-questions.md'
  - 'docs/wiki/faction-intent-and-autonomous-assignment.md'
  - 'docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md'
  - 'ROADMAP.md'
source:
  - 'https://github.com/L3DigitalNet/star-trek-alter-course/issues/68'
  - 'https://github.com/L3DigitalNet/star-trek-alter-course/pull/84'
---

# Factions and organizations

[Wiki home](README.md) · [Decision register](decision-register.md) · [Open questions](open-questions.md)

## Status and provenance

**Approved conceptual design with one implemented bounded consumer under review.** This page records the owner's explicit decisions in the September 6, 2026 design discussion consolidated under issue #68. Feature #86 / Final PR #87 implements only root factions, direct ship control, and establish-presence assignment; this page is not a complete political simulation specification.

The owner subsequently approved [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md) as the first bounded M5 consumer. Its implementation is pending landing and does not replace this broader framework or introduce hierarchy, government, organization, or relationship runtime.

Detailed authority matrices, constitutional procedures, general political scoring, action catalogs, and succession remain deferred. The current v0.5.0 implementation contains no faction hierarchy, organization runtime, diplomatic relationship state, government control, or layered jurisdiction model.

Examples illustrate desired game behavior. They are not claims about exact Star Trek constitutional law, a selected campaign epoch, or an exhaustive canonical taxonomy.

## Autonomous factions at several political scales

A faction is a political constituency or power bloc capable of its own political will. Factions at every supported depth can possess goals, relationships, resources, knowledge, preferences, and decisions. A subordinate faction is not merely a modifier on its parent.

Autonomy does not imply equal powers, equal resources, or unrestricted access to every action. A member polity, a Great House, and a domestic movement can all act independently while having different legitimate and practical possibilities.

Species and culture are separate from political faction identity. The Vulcan member polity is not synonymous with every Vulcan individual, and the Earth polity is not synonymous with all Humans. Political membership and organizational affiliation need not follow species.

## Structural hierarchy, political role, and labels

The approved initial hierarchy supports three faction depths: root, child, and grandchild, conventionally depth 0, 1, and 2. Use one recursive parentage model with an initial three-level limit, not three different faction classes or three permanently nested schema shapes. Extending depth later should not require replacing faction identity or rewriting every consumer. The first bounded assignment proof uses root factions only; child/grandchild runtime is deferred rather than required by that slice.

The approved human-readable labels are **Polity / Constituent / Internal**. These are organizational/navigation shorthand, not types that determine legal powers. A separate political role describes the actual actor: federation, empire, member polity, Great House, lesser house, political movement, or another justified role.

A typical example is Federation → Vulcan member polity → Vulcan political movement. Another is Klingon Empire → Great House → internal bloc. Neither pattern is a mandatory constitutional template.

The owner also approved skipping an intermediate political level: a Federation-wide political movement can sit directly beneath the Federation without a fictitious member-state parent. For consistency with derived depth, this is a direct parent-child link; it does not create a missing numeric depth or a special level-specific class. The movement's role can be internal political movement even though its actual structural depth is 1. Exact UI wording for such atypical branches remains open rather than assigning powers from the labels.

## One structural parent; many political relationships

Each faction has at most one structural/constitutional parent. Root factions have none. This forms a hierarchy/forest, while alliances, coalitions, patrons, ideological ties, foreign support, influence, and cooperation form separate relationships across it.

An internal movement can be funded by an external faction or participate in a cross-world coalition without acquiring another structural parent. A coalition of Great Houses is not automatically a new parent faction. Cooperation becomes a new political entity only when the world actually establishes one; ordinary alliance formation does not reparent its members.

Parentage and political role can change during a campaign. A faction normally retains its persistent identity across secession, subordination, independence, or a change in political status. This preserves historical continuity. Actual dissolution, merger, and successor-identity rules remain later design work.

## Constitutional relationships and interaction authority

The relationship between parent and child carries the meaning of their constitutional arrangement: autonomy, delegated powers, obligations, and jurisdiction. Political role describes what an actor is; the relationship describes its place under this particular parent. Do not hard-code all member polities or all Houses to identical authority solely from role or depth.

Formal diplomatic authority and unofficial/covert interaction are separate capabilities. The ordinary diplomatic path might have Earth deal with the Vulcan polity rather than routinely intervene in each internal Vulcan movement. That does not mean hierarchy makes cross-level contact physically impossible.

A Klingon House may seek favor from an external polity, and an external polity or its intelligence organization may support one House to pursue its own goals. Eligibility depends on the actor and circumstances, not a universal rule that a lower tier can never interact with a higher tier.

Physical/practical capability, political authorization, and secrecy are distinct concerns. An action can be possible but unsanctioned, and covert activity is not automatically successful or consequence-free. This decision does not grant every faction every covert capability.

## Risks and consequences

Unsanctioned and covert action carries risk. Detection, attribution, perceived legitimacy, obligations, and responses by the parent or external parties are relevant design concerns. A child can violate a superior obligation, and discovery can produce consequences for the child, parent, or larger polity depending on what is known and how responsibility is interpreted.

The discussion approved consequential risk, not fixed probabilities, an espionage resource model, or automatic collective punishment. Those mechanics remain open. Knowledge limits still apply: hidden misconduct does not automatically become universal political knowledge.

## Independent attitudes and scoped formal obligations

Every autonomous faction can maintain its own attitude toward other factions even when a superior authority controls formal diplomacy. A member can distrust a foreign polity while remaining bound by its parent's peace agreement. Formal/legal status is not the same thing as trust, hostility, grievances, interests, or preference.

Parent-level treaties, wars, embargoes, alliances, and other binding arrangements normally apply to descendants within their jurisdiction unless the constitutional arrangement or agreement provides otherwise. Model the arrangement and its scope; do not create a separate authoritative treaty copy in every child's relationship state.

Being legally bound does not make violating an obligation impossible. It makes the violation meaningful. Detailed precedence, exceptions, enforcement, and treaty interpretation are deferred; this page approves the distinction, not a universal constitutional rules engine.

## Organizations are distinct actors

Organizations are institutions operating within or across factions, not automatically political constituencies. Intelligence services, military/exploration organizations, corporations, criminal syndicates, religious institutions, and scientific bodies can have their own resources, goals, influence, operations, and agents without being modeled as faction hierarchy levels.

Organization type has gameplay meaning. An economic organization and an intelligence organization can have different resources, capabilities, actions, and goals. The actual type catalog and action sets are deliberately not specified yet.

An organization has at most one primary owning/chartering faction, and can have none where appropriate. Foreign operations, partners, influence, sponsors, and cross-border presence are separate ties, not additional structural parents. The distinction between a legal charter and effective ownership/control requires later refinement if gameplay needs it; do not infer a complete corporate ownership model from this shorthand.

An organization is not an extra faction depth merely because a diagram draws it beneath its associated faction. Nor does this distinction require a generic actor inheritance framework.

## Layered jurisdiction and direct asset control

Territory and jurisdiction can be layered. A location can be governed by a constituent polity while also falling within a larger polity's jurisdiction. A House-controlled world can also be within Imperial territory. One flat `ownerFaction` cannot represent every political relationship.

Ships, installations, fleets, and other assets should have one direct controlling faction or organization. Broader affiliation and applicable authority are derived through that controller's political relationships. Conceptually, a ship can be directly controlled by Starfleet and associated with the Federation, or controlled by a Great House within the Klingon Empire.

Direct control, layered jurisdiction, and observer-known affiliation remain different facts. Deriving an asset's true political context does not authorize revealing that entire chain to sensors, UI, or AI. No asset-controller or faction-affiliation fields exist in the current v0.5.0 `ShipState`; these are future-domain requirements, not changes to the implemented V6 save contract.

For the approved first assignment slice, D-09 selects one optional direct controlling `FactionId` stored on the ship/asset side, with a derived roster and no second mutable membership authority. Only idle, directly controlled NPC ships may receive an assignment; no preemption or player-command override is granted. Organization controllers, hierarchy, jurisdiction, and transfers remain deferred. A null controller denotes no modeled direct faction control, not an assertion of political neutrality.

D-10 separately permits a narrow own-asset administrative snapshot for assignment. It does not share the controlled ships' sensors, reveal foreign affiliation, or make faction diagnostics player-visible. The [canonical slice](faction-intent-and-autonomous-assignment.md) owns these exact limits and the planned non-inventive V7 migration.

## Government is not the enduring polity

A polity normally persists when its government changes. Internal factions or coalitions can compete for and acquire governmental control without deleting and recreating the state itself.

Even under a new ruling faction, the polity remains an autonomous actor with interests, institutions, obligations, resources, and historical relationships. The ruling faction influences it but does not simply replace the polity's will with its own. The design permits institutional constraints and disagreements without deciding election, coup, succession, or governance algorithms now.

Structural parentage, governing control, coalition membership, and direct asset control must not be treated as synonyms. The exact representations remain implementation decisions for concrete consumers.

## Implementation guardrails and remaining questions

Build only the smallest political slice that proves a real causal chain. Preserve actor-specific knowledge, deterministic scheduling, typed commands, explicit persistence, and meaningful offscreen activity. Existing ADRs still govern those boundaries.

The first selected slice is [Faction Intent and Autonomous Assignment](faction-intent-and-autonomous-assignment.md), implemented in Feature #86 / Final PR #87 pending landing. It uses root factions, existing orders and sensors, closed Ship/Faction scheduled targets, V7 migration that invents no political state, and no randomness or political UI. It does not complete M5.

[Open questions](open-questions.md) distinguishes resolved Q-05 from scoped administrative/control/compatibility decisions and still-open intelligence, identity, campaign, organization, and political mechanics. Do not repeatedly reopen the six approved decisions or silently implement this framework's entire future surface.
