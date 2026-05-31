# Concurrency Research Notes

Status: research notes  
Owner: sim/core  
Last updated: 2026-05-24

This document collects research and engineering notes related to concurrency, ECS-style data layout, GPU acceleration, PDES, and deterministic simulation. It is not a normative implementation spec. Normative rules belong in `CONCURRENCY_MODEL.md`.

The project direction is:

- keep authoritative simulation on the CPU fixed-tick core;
- preserve deterministic replay as a core feature;
- use data-oriented storage where it improves real system access patterns;
- parallelize read-heavy planning, chunk jobs, interaction groups, and background workers;
- use deterministic resolve/commit for authoritative results;
- use GPU acceleration primarily for rendering, overlays, derived fields, or non-authoritative batch work.

## 1. GPU Acceleration

GPU acceleration can mean two different things:

1. Rendering acceleration: using the GPU for drawing, instancing, batching, particles, shaders, culling, overlays, and visual effects.
2. General-purpose GPU compute: using compute shaders, CUDA, or similar systems for non-rendering workloads.

For this project, GPU acceleration should initially be treated as a support layer, not as the owner of authoritative simulation state.

Good candidates:

- tile and entity rendering;
- instanced items, creatures, particles, and overlays;
- debug heat maps and visualization layers;
- flow/vector fields where deterministic readback is not required every frame;
- non-authoritative field estimates such as traffic, smell, sound, or danger maps;
- visual fluids, smoke, fire, lighting, and atmospheric effects;
- background analysis or cache warmup with CPU fallback.

Poor early candidates:

- job assignment;
- inventory mutation;
- item reservation;
- authoritative movement commits;
- combat outcome resolution;
- construction and mining commits;
- save/replay canonical state.

Reasoning:

- GPU workloads are strongest when the work is highly parallel, data-parallel, and branch-light.
- Authoritative simulation often needs stable ordering, exact conflict resolution, and strong debugging support.
- CPU/GPU synchronization and readback can erase performance gains if required too frequently.
- Floating-point differences, atomics, driver behavior, and execution ordering can complicate deterministic replay.

Recommended principle:

```text
CPU owns truth.
GPU accelerates views, fields, effects, and batches.
Deterministic CPU resolve decides what enters authoritative state.
```

## 2. Dyson Sphere Program Style GPU Acceleration

Public information suggests that factory-scale games can use GPU acceleration heavily for rendering and visualization. Dyson Sphere Program is often discussed as a Unity-based, data-oriented factory game with large-scale rendering requirements.

The main lesson for HumanFortress is not that all logistics or simulation must move to the GPU. The safer lesson is:

- keep the authoritative factory/simulation rules on a deterministic CPU path;
- make renderable state cheap to export;
- use GPU instancing and batching to draw huge numbers of objects;
- avoid per-object CPU draw overhead;
- let visuals diverge from simulation representation when useful.

For HumanFortress, this means render snapshots should be compact, immutable, and GPU-friendly.

## 3. ECS and Data-Oriented Design

ECS-style design is useful when it helps systems process hot data in compact batches.

Benefits:

- better cache locality when data is packed by access pattern;
- clearer system read/write sets;
- easier parallel scheduling;
- easier snapshotting and diffing;
- improved batch processing for high-count entities.

Risks of full or overzealous ECS conversion:

- domain concepts become fragmented across too many systems;
- hot systems may need to join too many sparse components;
- archetype churn can become expensive;
- rare state can pollute hot loops;
- debugging can require tracing many systems;
- mod APIs may become harder to stabilize;
- dense tile state may become slower if every tile becomes an entity.

Recommended hybrid:

```text
Terrain / fluids / fields:
  chunked SoA arrays

Creatures / items / projectiles / movable actors:
  ECS-style or archetype-like storage

Rooms / workshops / stockpiles / jobs / reservations:
  domain aggregates plus indexes when they preserve invariants better

Hot movement data:
  packed by system access pattern, not split into excessive micro-components
```

Core rule:

```text
ECS is a data layout and scheduling tool, not a religion.
Split data because systems access it that way, not because every noun needs a component.
```

## 4. Cache Locality and Over-Splitting

ECS can improve cache behavior, but excessive component splitting can hurt performance.

Common failure modes:

- many tiny components used together every tick;
- random entity-id lookups across multiple arrays;
- query-heavy systems that scan too many entities;
- frequent structural changes between archetypes;
- hot and cold data stored together;
- per-frame polling for rare events;
- false sharing between worker threads;
- poor chunk or archetype packing.

Better approach:

- identify the hot loop first;
- record which fields are accessed together;
- pack those fields together or in parallel arrays with the same dense ordering;
- keep cold data out of hot iteration;
- use deferred structural changes;
- use events or observer lists for rare changes;
- measure cache misses and memory bandwidth, not only CPU time.

For pathfinding and movement, avoid one tiny task per path or per entity when batching would reduce overhead.

## 5. PDES: Parallel Discrete Event Simulation

PDES stands for Parallel Discrete Event Simulation.

Discrete event simulation represents the world as a set of events ordered by simulation time. Parallel discrete event simulation partitions the model into logical processes that process events while preserving causal correctness.

Useful concepts:

- logical process;
- event queue;
- timestamp;
- causality;
- conservative processing;
- optimistic processing;
- rollback;
- deterministic event ordering;
- disjoint access parallelism;
- batch processing.

Conservative PDES:

```text
Only process an event when it is safe to know no earlier event can invalidate it.
```

Optimistic PDES / Time Warp:

```text
Process speculatively, then roll back if an earlier conflicting event arrives later.
```

HumanFortress should not initially implement a full optimistic rollback engine. The complexity, memory overhead, and debugging cost are too high for the current goals.

Useful adaptation:

```text
Chunk = logical process
Mailbox = event queue
Tick = logical time
Stable drain order = deterministic total ordering
Diff/intent/resolve = batch processing
```

PDES should inspire partitioning and ordering, not replace the fixed-tick deterministic simulation model.

## 6. Interaction-Group Parallelism

Interaction-group parallelism is a practical middle ground between single-threaded simulation and unsafe entity-level preemption.

The core idea:

```text
If two groups cannot interact during this stage, they may run in parallel.
If they may interact, merge or resolve them together.
```

Potential groups:

- connected fluid basins;
- pathing regions;
- independent logistics networks;
- combat islands;
- field diffusion regions;
- room or district service networks;
- disconnected construction zones.

Important policies:

- merge groups promptly when a new interaction edge appears;
- split groups lazily when safe;
- defer shared side effects;
- resolve side effects through deterministic ordering;
- keep group detection conservative.

This resembles the engineering lesson from large factory games: parallelize independent interaction groups, not arbitrary individual objects.

## 7. Speculative Entity Planning

Speculative entity planning allows entity or AI logic to run in parallel without allowing parallel workers to directly write authoritative state.

Pattern:

```text
1. Freeze read snapshot.
2. Run entity/system planning in parallel.
3. Emit intents.
4. Resolve intents deterministically.
5. Convert accepted intents into diffs/messages.
6. Commit through the normal staged pipeline.
```

Examples of intents:

- MoveIntent;
- JobIntent;
- ItemReservationIntent;
- AttackIntent;
- DoorIntent;
- WorkshopUseIntent;
- PathRequestIntent.

Important distinction:

```text
Intent = I want to do X.
Commit = X happened.
```

Entity workers may produce intents but must not directly mutate the world.

Useful metadata:

- tick;
- stage;
- entity id;
- system id;
- priority;
- local sequence;
- target id;
- source read version;
- payload;
- rejection reason.

Possible rejection reasons:

- stale read;
- item already reserved;
- tile occupied;
- path blocked;
- target no longer valid;
- cross-chunk receiver rejected;
- budget exhausted.

## 8. GPU and Determinism

GPU acceleration should be feature-flagged and treated carefully when it affects simulation results.

Safer modes:

- GPU produces visual-only output;
- GPU produces advisory fields;
- GPU output is quantized and resolved on CPU;
- GPU has deterministic CPU fallback;
- GPU results are used only for ranking candidates, not directly committing state.

Riskier modes:

- GPU directly mutates authoritative state;
- GPU atomics decide winners;
- GPU floating-point reduction determines canonical simulation results;
- GPU readback is required inside tight fixed-tick loops;
- GPU-only simulation without CPU replay fallback.

Recommended rule:

```text
If GPU output can change the canonical world hash, it must pass the same determinism and replay gates as CPU simulation.
```

## 9. Recent Research and Engineering References

### ECS Concurrency

Paper:

- *Exploring the Theory and Practice of Concurrency in the Entity-Component-System Pattern*.

Why it matters:

- studies ECS as a concurrency-friendly programming pattern;
- emphasizes the separation of identity, data, and behavior;
- relevant to schedule-independent deterministic ECS design;
- useful for thinking about system read/write sets and safe parallel scheduling.

Use for:

- future ECS-style scheduler design;
- deterministic-by-construction component access rules;
- deciding how far ECS should go in the simulation core.

### Data-Oriented vs Object-Oriented A* Performance

Paper:

- *Impact of Data-Oriented and Object-Oriented Design on Performance and Cache Utilization with A* in Multi-Threaded CPUs*.

Why it matters:

- compares data-oriented and object-oriented designs for A*;
- highlights cache behavior and multi-threading overhead;
- reinforces that data layout can matter more than blindly adding threads.

Use for:

- pathfinding storage design;
- batching path requests;
- evaluating whether per-request parallelism is worth the overhead.

### PARSIR and Shared-Memory PDES

Paper:

- *PARSIR: a Package for Effective Parallel Discrete Event Simulation on Multi-processor Machines*.

Why it matters:

- discusses parallel discrete event simulation on shared-memory multi-processor machines;
- emphasizes batch processing, disjoint access parallelism, locality, and work distribution.

Use for:

- chunk actor scheduling;
- event batching;
- locality-aware task assignment;
- understanding conservative versus speculative simulation tradeoffs.

### Deterministic Ordering of Parallel Simulations

Paper:

- *Unbiased Deterministic Total Ordering of Parallel Simulations with Simultaneous Events*.

Why it matters:

- simultaneous events in parallel simulations need deterministic ordering;
- relevant to same-tick intent, diff, message, and event resolution.

Use for:

- tie-breaker design;
- replay stability;
- conflict resolution policies.

### Large-Scale Wargaming PDES

Paper:

- *A large-scale distributed PDES engine based on Warped2 for Wargaming simulation*.

Why it matters:

- large-scale spatial simulations face entity interaction, load balancing, and partitioning problems similar to colony games;
- spatial hashing and interaction solvers are relevant to large crowds and combat.

Use for:

- large battle simulation;
- combat island partitioning;
- entity interaction solvers;
- future load balancing.

### GPUDrive

Paper:

- *GPUDrive: Data-driven, multi-agent driving simulation at 1 million FPS*.

Why it matters:

- demonstrates extremely high-throughput GPU multi-agent simulation;
- useful as inspiration for batch simulation, but not directly as a colony-sim architecture.

Use for:

- optional AI training or stress-test mode;
- non-authoritative batch experiments;
- understanding what kinds of agent simulation map well to GPU.

### ManiSkill3

Paper:

- *ManiSkill3: GPU Parallelized Robotics Simulation and Rendering for Generalizable Embodied AI*.

Why it matters:

- demonstrates GPU-parallelized simulation and rendering;
- useful for thinking about batch workloads and simulation/rendering co-design.

Use for:

- future experiments;
- GPU-side rendering and field computation ideas;
- not recommended as the first authoritative simulation model.

## 10. Practical Takeaways for HumanFortress

Preferred concurrency path:

```text
Level 0:
  non-authoritative parallelism
  rendering, audio, UI, logging, asset streaming, save compression

Level 1:
  intra-chunk diff-log merge

Level 2:
  inter-chunk actor messaging

Level 3:
  interaction-group parallelism

Level 4:
  speculative entity planning with deterministic resolve

GPU:
  render, visualize, estimate, batch, or advise;
  do not directly own canonical state early
```

Preferred data layout path:

```text
Dense tile world:
  chunked SoA

Movable entities:
  ECS-style dense storage where useful

Domain aggregates:
  keep jobs, workshops, stockpiles, rooms, and reservations coherent

Hot loops:
  pack by actual system access pattern
```

Avoid:

- free-running entity threads that write world state;
- lock-heavy simulation hot paths;
- full optimistic rollback PDES without strong justification;
- over-fragmented ECS components;
- one task per tiny unit of work;
- GPU-only authoritative logic;
- nondeterministic conflict resolution.

Core principle:

```text
Parallelism may be opportunistic.
Authoritative results must be deterministic.
```
