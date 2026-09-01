---
schema_version: '1.1'
id: 'adr-0000-repo-name-short-title' # globally unique; filename omits repo-name
title: 'ADR 0000: Short Title'
description: 'Decision record for a significant architectural or project decision.'
doc_type: 'adr'
status: 'draft'
created: 'YYYY-MM-DD'
updated: 'YYYY-MM-DD'
reviewed: null
owner: ''
consumer: 'unknown'
tags: []
aliases: []
related: []
supersedes: []
superseded_by: null
source: []
confidence: 'unknown'
visibility: 'internal'
license: null
project:
  decision_makers: []
  consulted: []
  informed: []
  amends: [] # IDs of ADRs this record partially amends; reciprocal with their amended_by
  amended_by: [] # filled in later, by whoever amends this record
---

# {short title, representative of the bounded decision}

<!--
An amendment note belongs here, immediately after the title, once this record has
been amended — not while it is being drafted. Several notes share one blockquote,
oldest first, separated by a bare `>` line; each opens
`> **Amended by ADR NNNN (YYYY-MM-DD).**` or, for a change made from a
post-acceptance review, `> **Amended YYYY-MM-DD ({review}, {finding}).**`.
An amendment leaves `status` unchanged and never rewrites the accepted outcome in
place. See the Amendment workflow section of the standard.
-->

## Context and Problem Statement

{Describe the circumstances that require a decision. Make the boundary explicit:

- Governed concern: what exact choice is being made?
- Applies to: which systems, components, environments, or classes of change?
- Applies when: what conditions bring something within scope?
- Does not apply to: which realistic adjacent cases are excluded?
- Remains undecided: which related concerns require a separate decision?

End with one question no broader than this boundary.}

<!-- Optional. Remove if unused. -->

## Decision Drivers

- {quality, constraint, or force that applies within the stated boundary}

## Considered Options

{List meaningful alternatives that answer the same bounded question for the same governed population. Do not mix differently scoped policies unless scope itself is the decision.}

- {title of option 1}
- {title of option 2}
- {title of option 3}

## Decision Outcome

Chosen option: "{title of option 1}", because {justification}.

This decision governs {concern} for {population} when {applicability condition}.

It does not govern {explicit exclusions}. Those concerns remain undecided or are governed by {specific related ADR, if one exists}.

<!-- Optional. Describe effects only; do not add requirements or expand scope. -->

### Consequences

- Good, because {positive consequence}
- Bad, because {negative consequence}

<!-- Optional. Determine applicability first, then verify in-scope conformance. -->

### Confirmation

{Describe how a change is determined to be within this ADR's boundary, then how conformance is confirmed for in-scope changes. Out-of-scope changes receive no finding under this ADR.}

<!-- Optional. Remove if unused. -->

## Pros and Cons of the Options

### {title of option 1}

- Good, because {argument}
- Neutral, because {argument}
- Bad, because {argument}

### {title of option 2}

- Good, because {argument}
- Bad, because {argument}

<!-- Optional. Supporting context only; do not introduce new policy. -->

## More Information

{Additional evidence, agreement, revisit conditions, and links to related decisions.}
