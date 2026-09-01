---
schema_version: '1.1'
id: 'adr-0004-star-trek-alter-course-own-semantic-spatial-model-and-adapt-godot-rendering'
title: 'ADR 0004: Own the Semantic Spatial Model and Adapt Godot Rendering'
description: 'Defines the authoritative map model, scale boundaries, pathfinding ownership, and Godot presentation role.'
doc_type: 'adr'
status: 'active'
created: '2026-09-01'
updated: '2026-09-01'
reviewed: '2026-09-01'
owner: 'project-maintainers'
consumer: 'mix'
tags:
  - 'architecture'
  - 'simulation'
  - 'godot'
aliases: []
related:
  - 'docs/adr/0001-separate-simulation-from-godot.md'
  - 'docs/adr/0003-prefer-native-capabilities-and-demand-driven-dependencies.md'
  - 'docs/adr/0007-use-deterministic-simulation-time-scheduling-and-randomness.md'
  - 'docs/adr/0010-use-explainable-domain-ai-and-demand-driven-state-machines.md'
  - 'docs/adr/0011-represent-physical-quantities-with-explicit-units.md'
supersedes: []
superseded_by: null
source:
  - 'https://docs.godotengine.org/en/4.6/classes/class_astar2d.html'
  - 'https://docs.godotengine.org/en/4.6/classes/class_astargrid2d.html'
  - 'https://docs.godotengine.org/en/4.6/classes/class_tilemaplayer.html'
  - 'https://docs.godotengine.org/en/4.6/classes/class_navigationserver2d.html'
  - 'https://docs.godotengine.org/en/4.6/tutorials/2d/'
  - 'https://github.com/nol1fe/delaunator-sharp'
  - 'https://github.com/AngusJohnson/Clipper2'
  - 'https://github.com/NetTopologySuite/NetTopologySuite'
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

# Own the semantic spatial model and adapt Godot rendering

## Context and Problem Statement

The map is the primary interface and a foundational simulation concept in Star Trek: Alter Course. The game must eventually represent galaxy regions, sectors, star systems, routes, political territories, sensor contacts, local points of interest, and tactical space. These scales share identities and relationships, but they do not share one useful physical representation.

A generic tile map is suitable for some local spaces but not for an entire galaxy. A point graph is useful for strategic routing but does not model tactical heading, velocity, range, firing arcs, or shield facings. Godot supplies capable rendering, cameras, tile maps, and pathfinding primitives, but making a Godot node graph authoritative would prevent fast headless simulation and would couple persistent world state to scene structure.

This decision governs the authoritative spatial model, the relationship among map scales, the separation between strategic and tactical space, the ownership of routefinding, and the role of Godot map primitives. It applies to all persistent locations, routes, territorial geometry, contacts, and movement represented by the simulation.

It does not determine the final coordinate systems, galaxy-generation algorithm, tactical movement equations, visual style, fog-of-war presentation, or exact user interaction design. Those remain system-design concerns within the boundary established here.

How should the project represent multiple spatial scales while preserving a pure, persistent simulation and taking advantage of Godot's 2D capabilities?

## Decision Drivers

- The map is the principal means through which the player understands and commands the world.
- Strategic and tactical space have materially different semantics.
- Important location and movement state must persist independently from Godot scenes.
- AI and long-running simulations need routefinding without starting Godot.
- The architecture must permit additional scales without replacing the underlying identity and relationship model.
- Sensor knowledge and actual world state must be representable separately.
- Rendering, selection, zoom, labels, and effects should use Godot rather than a custom graphics framework.
- Path costs depend on domain state such as faction access, hazards, fuel, ship capability, treaties, and current knowledge.
- Spatial algorithms must remain deterministic and testable where they affect simulation outcomes.
- Large map representations must not inherit arbitrary tile-coordinate or scene-tree limits.

## Considered Options

- Own a semantic multi-scale model in Core and render it through Godot adapters.
- Use Godot scenes, nodes, and navigation objects as the authoritative map.
- Represent every scale as one tile grid.
- Represent every scale as one universal graph.
- Adopt a third-party map or strategy framework as the authoritative model.

## Decision Outcome

Chosen option: "Own a semantic multi-scale model in Core and render it through Godot adapters", because the simulation needs stable domain identities and scale-specific behavior while Godot is best used for presentation and engine-local navigation.

This decision governs persistent spatial state and simulation-affecting movement. It does not require every possible map scale to be implemented initially, nor does it require a generalized spatial framework before a second concrete scale exists.

### Authoritative ownership

`AlterCourse.Core` owns:

- stable identifiers for locations, spatial entities, routes, and regions;
- parent-child or containment relationships among implemented scales;
- domain coordinates and reference frames;
- actual positions, headings, velocities, and movement state;
- route topology and domain-specific traversal costs;
- faction ownership, jurisdiction, access, and territorial state;
- hazards and movement constraints that affect simulation;
- actual contacts and each actor's knowledge or uncertainty about them;
- commands and state transitions that move actors;
- deterministic route and movement resolution.

`AlterCourse.Godot` owns:

- cameras, viewport transforms, zoom, pan, and screen-space layout;
- sprites, icons, labels, lines, overlays, selection regions, and effects;
- mouse, keyboard, focus, tooltip, and contextual-action interaction;
- scene composition and visibility optimization;
- conversion between domain coordinates and display coordinates;
- engine-local tile maps and navigation data for presentation-specific or local-map needs.

Godot nodes and resources may reference Core identifiers or immutable view data. Core state must not store `Node`, `Resource`, `Vector2`, `Transform2D`, `AStar2D`, `TileMapLayer`, or other Godot objects.

### Scale model

The architecture must permit at least these conceptual scales without requiring them all in the first playable version:

- galaxy region or quadrant;
- sector;
- star system;
- local orbital or point-of-interest space;
- tactical space.

A scale is not merely a zoom level. Each scale may have different movement rules, time resolution, visibility, topology, and displayed entities. Connections among scales use stable domain relationships, such as a star system belonging to a sector or a tactical encounter occurring at a location in a system.

Implementations should add only the scales needed for current gameplay. They must not encode assumptions such as "every location is a tile", "every route is between adjacent grid cells", or "all positions are pixels" into shared identities or persistence.

### Strategic space

Strategic space uses a semantic graph combined with coordinates and metadata.

The graph represents reachable transitions and known or possible routes. Coordinates support geography, distance, display, sensor relationships, and procedural generation. A route is not necessarily a permanent unweighted edge; its availability and cost can depend on:

- ship propulsion and condition;
- distance and travel mode;
- fuel and power constraints;
- hazards and temporary events;
- faction borders, treaties, and permissions;
- known information rather than omniscient truth;
- strategic doctrine or mission constraints.

The route cost model belongs to Core. It may use a focused project-owned Dijkstra or A* implementation over domain interfaces, or another pure .NET implementation admitted under ADR 0003. It must not require Godot's `AStar2D` to run strategic AI or headless simulation.

Owning a small routefinding adapter or implementation is justified because the domain cost and knowledge model are the difficult parts, while a general graph framework would add more abstraction than the algorithm requires.

### Tactical space

Tactical space uses continuous two-dimensional positions and motion, not a tile grid.

The model must be capable of representing:

- position and relative bearing;
- heading and orientation;
- velocity and acceleration;
- distance and closing rate;
- weapon ranges and firing arcs;
- shield facings;
- sensor confidence and contact error;
- hazards, obstacles, and engagement boundaries.

The exact tactical integration method and time step remain separate design decisions. The important boundary is that tactical truth lives in Core and Godot renders snapshots or interpolated views of it.

A tactical map may display a grid, range rings, tactical symbols, or quantized movement choices. Those are presentation or command affordances and do not convert the underlying space into a tile map.

### Local tile-based space

`TileMapLayer` and `AStarGrid2D` are approved for concrete grid-based local maps such as a station interior, planetary surface, colony site, or ship deck diagram. Their adoption for a local feature does not make tiles the common representation for strategic or tactical space.

Better Terrain may be evaluated under ADR 0003 if authored terrain rules become a material local-map requirement. It is not needed for the galaxy or tactical-space model.

### Godot pathfinding and navigation

`AStar2D` and `AStarGrid2D` may be used within `AlterCourse.Godot` for engine-local concerns, prototypes, editor tools, or local-map navigation. Their outputs must be converted into domain commands before they change authoritative simulation state.

`NavigationServer2D` is not a foundational tactical or strategic dependency. Its agent and navigation-region model does not directly represent starship tactical decisions, and Godot currently documents it as experimental. It may be reevaluated for a concrete obstacle-avoidance or local-agent requirement whose behavior remains outside the authoritative strategic model.

### Geometry libraries

No geometry package is adopted by this ADR.

When a concrete requirement appears:

- DelaunatorSharp is a candidate for Delaunay triangulation or Voronoi-derived generation.
- Clipper2 is a candidate for bounded polygon boolean and offset operations.
- NetTopologySuite is reserved for requirements that genuinely need robust topology or GIS-like operations.

Territory may initially be represented by system ownership, influence values, or derived display geometry. The project must not introduce a heavy geometry engine merely to draw approximate colored borders.

Any geometry that affects legal movement, ownership, combat, or AI becomes authoritative Core data or a deterministic derivation. Purely decorative territory polygons remain presentation data.

### Sensor knowledge and map views

The true spatial state and an actor's observed state are distinct.

A player or AI map view may contain:

- confirmed entities;
- stale contacts;
- estimated positions;
- uncertainty regions;
- inferred faction activity;
- unknown routes or hazards.

Rendering code receives an actor-appropriate projection rather than unrestricted access to the complete world model. Strategic AI likewise evaluates its own information state, as further governed by ADR 0010.

This separation prevents UI convenience and AI implementation from accidentally making fog of war cosmetic.

### Consequences

- Good, because the persistent world and AI can operate without Godot.
- Good, because strategic and tactical maps can use representations appropriate to their actual rules.
- Good, because Godot still supplies the full rendering, camera, input, and local-navigation stack.
- Good, because future scales can share stable identities without sharing one inappropriate coordinate model.
- Good, because sensor uncertainty becomes a first-class simulation concern.
- Bad, because adapters must translate Core view data into Godot nodes and screen coordinates.
- Bad, because some routefinding and spatial query code must be maintained in Core.
- Bad, because cross-scale transitions require explicit contracts rather than implicit scene changes.
- Bad, because the same entity may require different projections at different scales.

### Confirmation

A change is in scope when it adds or changes a persistent location, route, coordinate, territory, sensor contact, movement rule, map scale, pathfinding service, or Godot map component.

Conformance is confirmed by:

- Core tests that run spatial and route decisions without Godot;
- project references that preserve ADR 0001;
- save models containing domain data rather than scene-tree objects;
- deterministic pathfinding tests with stable tie-breaking;
- tests that distinguish true state from actor knowledge;
- Godot integration tests that verify projection, selection, and command translation without making presentation state authoritative.

## Pros and Cons of the Options

### Own a semantic multi-scale model in Core and render it through Godot adapters

- Good, because domain state remains persistent, deterministic, and testable.
- Good, because each scale can use appropriate rules and geometry.
- Good, because Godot remains responsible for the work it performs best.
- Bad, because it requires explicit view models and adapters.
- Bad, because no single editor surface automatically authors the entire galaxy model.

### Use Godot scenes, nodes, and navigation objects as the authoritative map

- Good, because editor visualization and runtime representation would be closely aligned.
- Good, because less translation code would be required initially.
- Bad, because headless strategic simulation would depend on Godot.
- Bad, because scene identity, persistence, and domain identity would become entangled.
- Bad, because large persistent worlds would be pressured into scene-tree lifecycle semantics.

### Represent every scale as one tile grid

- Good, because tile algorithms and tooling are simple and mature.
- Good, because discrete movement can be easy to inspect.
- Bad, because tactical heading, velocity, range, and firing arcs become artificial.
- Bad, because galaxy scale inherits unnecessary memory and coordinate constraints.
- Bad, because route and political semantics become encoded indirectly through cells.

### Represent every scale as one universal graph

- Good, because routefinding and connectivity are uniform.
- Good, because strategic maps map naturally to graphs.
- Bad, because continuous tactical geometry becomes awkward or falsely discrete.
- Bad, because a universal graph tends to become a speculative abstraction with scale-specific exceptions.

### Adopt a third-party map or strategy framework as the authoritative model

- Good, because some rendering and editor features could arrive quickly.
- Bad, because the framework's world model would constrain Core semantics.
- Bad, because package or engine updates could affect save and simulation compatibility.
- Bad, because the project would still need custom tactical, sensor, political, and travel rules.

## More Information

This ADR distinguishes semantic ownership from algorithm ownership. Using Godot's rendering or pathfinding implementation in an adapter is not a violation when the resulting action is validated and applied through Core. Conversely, reimplementing a camera or tile renderer in Core would not improve the architecture.

The first implementation should be narrow: one strategic representation and one tactical representation sufficient for a playable loop. The architectural requirement is that neither representation precludes the additional scales named here.
