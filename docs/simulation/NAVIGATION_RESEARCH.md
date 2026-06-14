# Navigation Research Reading Guide

Status: research notes  
Owner: sim/navigation  
Last updated: 2026-05-24

This document collects academic and engineering references for future navigation work. It is not an implementation specification. The goal is to give future agents and developers enough context to make informed design choices without locking the project into one algorithm too early.

The current project direction is:

- keep deterministic weighted A* as the general-purpose baseline;
- add higher-level navigation layers when scale requires them;
- use flow/vector fields for many-to-one movement;
- use reservation or local conflict resolution for multi-actor movement;
- treat flying, aquatic, large, and vehicle movement as movement-profile problems rather than as separate world models.

## 1. Baseline Heuristic Search

### Hart, Nilsson, and Raphael — A Formal Basis for the Heuristic Determination of Minimum Cost Paths

Original A* paper. A* remains the correct baseline for this project because it works on weighted graphs, supports deterministic tie-breaking, and can be adapted to grid, chunk, and multi-Z maps.

Use this for:

- understanding admissible and consistent heuristics;
- understanding why `f = g + h` is useful;
- reasoning about optimality versus performance;
- designing deterministic tie-breaking.

Project relevance:

- high priority;
- already reflected in the current deterministic A* implementation;
- should remain the fallback pathfinder when specialized strategies do not apply.

Reference:

- Hart, P. E., Nilsson, N. J., and Raphael, B. (1968). *A Formal Basis for the Heuristic Determination of Minimum Cost Paths*. IEEE Transactions on Systems Science and Cybernetics.
- Overview: https://en.wikipedia.org/wiki/A*_search_algorithm

## 2. Hierarchical Pathfinding

### Botea, Muller, and Schaeffer — Near Optimal Hierarchical Path-Finding

Hierarchical pathfinding reduces long-distance search cost by planning first on an abstract graph, then refining locally. This direction is especially relevant for a large fortress map with 32x32 chunks, rooms, tunnels, roads, ramps, and portals.

Use this for:

- chunk or region graph design;
- portal-based pathfinding;
- reducing raw tile-level A* calls;
- long-distance job and travel queries.

Project relevance:

- very high priority;
- likely more important than simply parallelizing raw tile A*;
- should be explored before introducing more exotic grid accelerators.

Reference:

- Botea, A., Muller, M., and Schaeffer, J. (2004). *Near Optimal Hierarchical Path-Finding*.
- Overview: https://en.wikipedia.org/wiki/Pathfinding

### Partial Refinement A* and Related Hierarchical Methods

Partial refinement methods avoid fully expanding a low-level route until the actor needs it. This can be useful for long jobs where only the immediate segment is needed in the current tick window.

Use this for:

- long routes across many chunks;
- route corridors that are refined only near the actor;
- large maps with frequently changing local terrain.

Project relevance:

- medium to high priority;
- useful after a basic region or portal graph exists.

## 3. Grid Search Acceleration

### Harabor and Grastien — Online Graph Pruning for Pathfinding on Grid Maps

Jump Point Search is an A* optimization for uniform-cost grid maps. It prunes symmetric paths and can greatly reduce node expansions in suitable maps.

Use this for:

- understanding grid symmetry;
- possible optimization of flat, uniform-cost regions;
- learning how pruning can preserve optimality under restricted assumptions.

Project relevance:

- medium priority;
- not suitable as the default pathfinder for all actors;
- best treated as an optional accelerator for simple regions.

Caution:

- JPS is strongest on uniform-cost grids;
- weighted terrain, fluids, doors, traffic, ramps, stairs, large footprints, and unusual movement profiles weaken or invalidate its assumptions;
- always keep weighted deterministic A* as fallback.

Reference:

- Harabor, D., and Grastien, A. (2011). *Online Graph Pruning for Pathfinding on Grid Maps*.
- Overview: https://en.wikipedia.org/wiki/Jump_point_search

### Zhao, Harabor, and Stuckey — Reducing Redundant Work in Jump Point Search

Constrained JPS studies pathological redundant work in JPS and proposes an online approach to reduce repeated scanning and suboptimal node generation. It is relevant if the project later experiments with JPS on dynamic game maps.

Use this for:

- understanding JPS failure modes;
- evaluating whether a JPS variant is worth maintaining;
- thinking about dynamic map behavior.

Project relevance:

- medium priority;
- useful only if JPS becomes a serious implementation candidate.

Reference:

- Zhao, S., Harabor, D., and Stuckey, P. J. (2023). *Reducing Redundant Work in Jump Point Search*.
- Link: https://arxiv.org/abs/2306.15928

### Baum — Jump Point Search Pathfinding in 4-connected Grids

JPS4 adapts jump-style pruning to 4-connected grids. This may be relevant because fortress movement may prefer 4-neighbor semantics in some modes.

Use this for:

- comparing 4-neighbor and 8-neighbor movement assumptions;
- evaluating jump-style search for dense obstacle maps;
- understanding why open maps may not benefit from JPS4.

Project relevance:

- low to medium priority;
- useful as an optional research branch, not as a core dependency.

Reference:

- Baum, J. (2025). *Jump Point Search Pathfinding in 4-connected Grids*.
- Link: https://arxiv.org/abs/2501.14816

## 4. Dynamic Replanning and Changing Terrain

### Lifelong Planning A*

LPA* is an incremental version of A* that reuses previous search information when graph costs change.

Use this for:

- destructible terrain;
- repeated queries with similar source or destination;
- local topology changes around doors, bridges, walls, fluids, or mining operations.

Project relevance:

- medium priority;
- potentially useful later, but likely more complex than conservative cache invalidation;
- should not be introduced until the baseline navigation and cache behavior are stable.

Reference:

- Koenig, S., Likhachev, M., and Furcy, D. (2004). *Lifelong Planning A***.
- Overview: https://en.wikipedia.org/wiki/Lifelong_Planning_A*

### D* and D* Lite

D* family algorithms support fast replanning when terrain information changes during movement. D* Lite builds on LPA* and is often easier to reason about than original D*.

Use this for:

- unknown or changing terrain;
- actors that discover blockers while moving;
- fast replanning after local obstruction.

Project relevance:

- medium priority;
- more relevant for advanced movement than for the first stable navigation layer;
- may be overkill if conservative invalidation and local replanning are sufficient.

Reference:

- Stentz, A. (1994). *Optimal and Efficient Path Planning for Partially Known Environments*.
- Koenig, S., and Likhachev, M. (2005). *Fast Replanning for Navigation in Unknown Terrain*.
- Overview: https://en.wikipedia.org/wiki/D*

## 5. Flow Fields, Vector Fields, and Crowds

### Treuille, Cooper, and Popovic — Continuum Crowds

Continuum Crowds models large crowds using dynamic potential fields. It is most useful when many actors share a broad destination or tactical objective.

Use this for:

- enemy waves;
- army movement;
- evacuation or panic movement;
- repeated traffic toward a shared district or facility;
- large crowds where individual A* per actor is too expensive.

Project relevance:

- high priority for high-population scenarios;
- not a replacement for individual job pathfinding;
- should be derived from the same authoritative cost and passability model as A*.

Reference:

- Treuille, A., Cooper, S., and Popovic, Z. (2006). *Continuum Crowds*.
- Project page: https://grail.cs.washington.edu/projects/crowd-flows/

### Flow Fields for Game Pathfinding

Game-style flow fields are often generated by running a reverse Dijkstra or BFS from a target and storing a direction or gradient per tile.

Use this for:

- many actors moving to the same target;
- tower-defense-like movement;
- low-cost per-agent steering after a shared field is built.

Project relevance:

- high for mass movement;
- low for unrelated individual jobs with many unique destinations.

Reference:

- Red Blob Games. *Flow Field Pathfinding for Tower Defense*.
- Link: https://www.redblobgames.com/pathfinding/tower-defense/

## 6. Local Avoidance and Collision Avoidance

### ORCA — Optimal Reciprocal Collision Avoidance

ORCA is a local avoidance method for multiple moving agents. It is more natural for continuous-space movement than for strictly tile-based deterministic simulation, but the concepts are useful.

Use this for:

- understanding local collision avoidance;
- continuous-space agents;
- soft collision and crowd steering;
- comparing steering-based movement with reservation-based movement.

Project relevance:

- medium priority;
- useful conceptually;
- tile-based actors may be better served by deterministic reservations.

Reference:

- van den Berg, J., Guy, S. J., Lin, M., and Manocha, D. (2011). *Reciprocal n-Body Collision Avoidance*.
- Project page: https://gamma.cs.unc.edu/ORCA/

## 7. Multi-Agent Pathfinding and Reservations

### Silver — Cooperative Pathfinding

Cooperative pathfinding uses time-expanded planning and reservations so that agents avoid future conflicts. Windowed variants limit the planning horizon to keep cost manageable.

Use this for:

- tile reservations;
- preventing two actors from entering the same tile;
- preventing head-on swaps;
- short-horizon movement coordination.

Project relevance:

- high conceptual relevance;
- likely more practical than full optimal MAPF for ordinary citizens;
- should inspire a deterministic reservation layer rather than a heavy global solver.

Reference:

- Silver, D. (2005). *Cooperative Pathfinding*.

### Sharon, Stern, Felner, and Sturtevant — Conflict-Based Search for Optimal Multi-Agent Pathfinding

CBS solves MAPF by combining individual pathfinding with high-level conflict constraints. It is a foundational optimal MAPF method but may be too expensive for large simulation populations.

Use this for:

- understanding conflict constraints;
- handling small groups of high-value actors;
- learning how to separate single-agent pathfinding from multi-agent conflict resolution.

Project relevance:

- medium conceptual priority;
- not recommended as the default solver for all actors;
- useful for designing reservation and conflict policies.

Reference:

- Sharon, G., Stern, R., Felner, A., and Sturtevant, N. R. (2015). *Conflict-Based Search for Optimal Multi-Agent Pathfinding*.
- Overview: https://en.wikipedia.org/wiki/Multi-agent_pathfinding

### Bounded-Suboptimal MAPF Solvers

Bounded-suboptimal MAPF algorithms trade optimality for speed. These ideas may be more suitable for games than strict optimal solvers.

Use this for:

- small tactical squads;
- vehicles in constrained spaces;
- cases where conflict-free movement matters but perfect optimality does not.

Project relevance:

- low to medium priority;
- revisit only after basic reservations exist.

Reference:

- Li, J., Ruml, W., and Koenig, S. (2020). *EECBS: A Bounded-Suboptimal Search for Multi-Agent Path Finding*.
- Link: https://arxiv.org/abs/2010.01367

## 8. Any-Angle and 3D Path Planning

### Daniel, Nash, Koenig, and Felner — Theta*: Any-Angle Path Planning on Grids

Theta* modifies A* so that paths are not constrained to grid edges when line-of-sight exists. It can produce shorter, smoother paths on grids.

Use this for:

- understanding any-angle movement;
- flying actors;
- large open areas;
- optional visual path smoothing.

Project relevance:

- low to medium priority;
- less important for tile-semantic fortress movement;
- potentially useful for flying, projectiles, or continuous movement layers.

Reference:

- Daniel, K., Nash, A., Koenig, S., and Felner, A. (2010/2014). *Theta*: Any-Angle Path Planning on Grids*.
- Link: https://arxiv.org/abs/1401.3843

### Field D* and 3D Field D*

Field D* extends replanning concepts to produce smoother paths through weighted grid cells using interpolation. 3D Field D* applies similar ideas in volumetric spaces.

Use this for:

- non-uniform traversal costs;
- smoother movement over weighted spaces;
- advanced flying or volumetric movement.

Project relevance:

- low priority for the current grid-based fortress model;
- useful later if flying movement becomes important.

Reference:

- Ferguson, D., and Stentz, A. (2005). *Field D*: An Interpolation-Based Path Planner and Replanner*.
- Overview: https://en.wikipedia.org/wiki/Any-angle_path_planning

## 9. Large Actors, Vehicles, and Clearance

This topic is less about one famous algorithm and more about representing the correct search state.

Important ideas:

- footprint-aware passability;
- clearance maps;
- swept-volume checks;
- orientation-aware states;
- turn costs;
- local maneuver actions instead of simple tile steps.

Use this for:

- large creatures;
- carts and wagons;
- boats;
- siege engines;
- actors that cannot rotate freely.

Project relevance:

- high design relevance if large actors or vehicles are planned;
- implementation should build on movement profiles and clearance providers rather than modifying A* directly for every actor type.

Suggested search terms for future research:

- clearance-based pathfinding;
- true clearance in grid maps;
- any-angle pathfinding with clearance;
- hybrid A* vehicle path planning;
- orientation-aware grid planning.

## 10. Suggested Reading Order

### Immediate Reading

Read these first:

1. A* original paper or a high-quality summary.
2. HPA* / hierarchical pathfinding.
3. JPS original paper.
4. Continuum Crowds.
5. Cooperative Pathfinding.
6. ORCA overview.

These cover the most important design pressures for HumanFortress: baseline search, large-map scale, mass movement, and multi-actor conflict.

### Later Reading

Read these when implementation pressure appears:

1. LPA* and D* Lite for dynamic replanning.
2. Constrained JPS and JPS4 for specialized grid acceleration.
3. CBS and bounded-suboptimal MAPF for high-value multi-agent coordination.
4. Theta*, Field D*, and 3D methods for flying or continuous movement.
5. Clearance and vehicle-planning literature for large actors and vehicles.

## 11. HumanFortress Design Takeaways

Recommended direction:

```text
Baseline:
  deterministic weighted A*

Large map:
  region / chunk / portal graph

Many actors to same destination:
  flow or vector field

Dynamic terrain:
  conservative invalidation first;
  incremental replanning later if needed

Grid acceleration:
  JPS only for suitable simple regions

Multi-agent conflict:
  deterministic reservation layer

Flying and aquatic actors:
  movement profiles and specialized neighbor providers

Large actors and vehicles:
  footprint, clearance, swept-volume, and orientation-aware planning
```

Avoid:

- replacing the baseline with an algorithm that only works under narrow assumptions;
- making pathfinding responsible for all job scheduling and collision resolution;
- using thread scheduling order as a hidden conflict resolver;
- implementing optimal MAPF for every ordinary actor;
- creating separate world navigation data for every movement type.

The long-term architecture should allow multiple navigation strategies to coexist over one authoritative world representation.
