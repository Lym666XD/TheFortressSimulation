id: determinism.ci.v1
status: normative
owner: engine/ci
last_updated: 2025-09-15
applies_to:
  - Simulation (all systems in UPDATE_ORDER)
  - Job scheduler & diff-log merge
  - Chunk actor messaging
  - Mapgen & region instances
  - Save/load & RNG restore
references:
  - UPDATE_ORDER.md
  - DIFF_LOG_AND_MERGE_STRATEGIES.md
  - CHUNK_ACTOR_PROTOCOL.md
  - SAVE_FORMAT.md
  - ERROR_HANDLING_POLICY.md
  - JOB_SCHEDULER_SPEC.md
  - CONCURRENCY_MODEL.md
goal:
  - The same inputs (seed, packs, config, player input replay) MUST produce identical canonical results across runs, thread counts, and supported OSes.
0) Definitions (Normative)
Determinism: For a given tuple
T = {engine_build, format_version, packset_signature, cfg, seed, replay}
and a target tick range [0..N], the canonical snapshot hash and event trace are identical across:

OS: Windows, Linux, macOS

Thread counts: {1, 2, 4, 8} (at min 1 and max supported)

Hardware: x64 CPUs (AVX2+) within support matrix

Canonical snapshot: A normalized byte stream of authoritative state (see §3) at ticks t∈S (checkpoints), hashed with BLAKE3-256.

Replay: A deterministic sequence of player inputs (device-independent), time-stamped in ticks.

1) Sources of Non-Determinism & Hard Rules (Normative)
PRNG

Use counter-based streams; namespace by (system, scope) e.g., (Fluids, chunk:cx,cy,cz).

Derive seeds deterministically: stream_seed = H(world_seed, instance_key?, system_name, scope_id).

Persist and restore stream counters exactly (SAVE_FORMAT §2.8).

Ordering

Iterate sorted by key for all unordered collections.

Diff-log merge order: (chunk → tile → system_priority → systemId) (DIFF_LOG_AND_MERGE_STRATEGIES).

Chunk actor mailbox: deliver by (tick, senderChunkId, seq) only (CHUNK_ACTOR_PROTOCOL).

Floating point

Simulation-critical accumulations MUST be fixed-point or quantized integer.

If floats are unavoidable: quantize to Q24.8 (round half to even) at each write, and ban FastMath/flush-to-zero/denormals-are-zero toggles.

Time & environment

No wall-clock, time zone, locale, or high-res timers in sim paths.

File IO and thread IDs MUST NOT influence sim decisions.

Data structures

No iteration over Dictionary<> without sorting keys.

No parallel reductions that depend on completion order. Use deterministic reducers.

Content

Packset ordering is part of input T. Loader MUST sort packs by (kind, id) after declared order to break ties and log a stable signature.

2) Canonicalization & Hashing (Normative)
Canonicalization: Build a byte stream by:

Sorting top-level entities (chunks asc; then actors/items/buildables by id→position→creationTick).

Serializing only authoritative fields (no caches).

Using little-endian fixed encodings; booleans as 0/1; strings as UTF-8 with length prefix.

Replacing all runtime handles with string IDs (material/item/creature/faction/recipe).

Quantizing floats as specified (or rejecting them in canonicalization).

Hash: BLAKE3-256 of the canonical stream. Store per checkpoint.

Checkpoints (default): S = {N/4, N/2, N} ticks; plus post-load and pre-save snapshots for save/load invariants.

3) What Goes Into the Canonical Snapshot (Normative)
Include (must): terrain/fluids depth, items & containers, buildables state, actors (hp/wounds/status/inventory/equipment), fields, reservations, job queues/progress, undelivered mailbox envelopes, RNG cursors, region-instance states for persistent types, faction memory, player adventure memory (if present).

Exclude (must not): all caches/indexes (pathing, spatial, stockpile cache), rendering data, connectivity graphs, view cones, profiling counters.

(Exact field lists must match SAVE_FORMAT §4.)

4) CI Pipeline (Normative)
4.1 Job Matrix
Axis	Values
OS	win-x64, linux-x64, osx-x64
Threads	1, max (e.g., 8 or hardware limit)
Seeds per scenario	10 (smoke) / 100 (nightly)
Scenarios	fort_small, dungeon_raid, fluid_stress, siege_pathing, economy_crafting, mapgen_roundtrip

4.2 Stages
Build (reproducible): pinned toolchain; DeterminismMode=On (no FastMath; deterministic JIT flags if available).

Unit + Property tests: invariants (no NaN, bounded sums, stable merges).

Replay runs: run each scenario for N=10_000 ticks with seed set S; record checkpoint hashes & traces.

Cross-thread replay: repeat with thread counts {1, max}; compare hashes per checkpoint.

Cross-OS replay: compare artifacts from different runners (same seed).

Save/load round-trip: save at t=N/2, reload, continue to N; hashes at N/2 (post-load) and N must match baseline.

Chaos jitter: enable yield/fairness jitter (random Thread.Yield() injection points) and message shuffler (stable seed) → hashes must still match.

Fuzz content (nightly): small random perturbations of pack data that keep schema valid → engine must stay deterministic or fail with E_SCHEMA only at load.

4.3 Artifacts
det/<scenario>/<os>/<threads>/<seed>/

checkpoints.json — tick→BLAKE3 hashes

trace.ndjson — minimal sim trace (see §7)

divergence/ — only if mismatch (see §6)

5) Pass/Fail Criteria (Normative)
A pipeline fails if any of the below holds:

Any checkpoint hash differs across threads or OS for the same input T.

Post-load hash differs from pre-save hash at the same tick.

First divergence tick cannot be reproduced locally using the shipped replay artifact.

Any run emits NaN/Inf or violates ordering assertions.

RNG stream counters differ given identical inputs.

6) Divergence Handling (Normative)
On first mismatch:

Bisect ticks with a deterministic binary search to find t* (first divergent tick).

Dump minimal divergence bundle:

inputs.json (T tuple, build info)

trace_pre_t*.ndjson (last 64 ticks)

snapshot_A/B.bin (canonical pre/post for two runs)

diff.txt (human diff of high-level entities)

replay.zip (seed + deterministic input log)

Automatically open a GitHub/GitLab issue with labels determinism, attach bundle.

CI exits with failure.

7) Minimal Event Trace Schema (Normative)
json
Copy code
{ "tick": 1234, "sys":"Scheduler", "kind":"JobComplete", "job":"forge#7F3", "worker":"pawn#A1", "pos":[12,9,0] }
{ "tick": 1234, "sys":"Creatures", "kind":"Attack", "src":"wolf#B3", "dst":"pawn#A1", "dmg":{"bash":4,"stab":2}, "rng": "Fluids/c(4,7,0):123456" }
{ "tick": 1235, "sys":"Mailbox", "kind":"Deliver", "from":"(4,7,0)", "to":"(5,7,0)", "seq":22291 }
Rules:

Keep it small (sampled, not every event).

Include RNG stream ids and counters when used to branch logic.

Sorted by (tick, sys, id).

8) Engine Build Settings (Normative)
Disable non-deterministic JIT optimizations where available; ban “fast-math” equivalents.

Pin .NET runtime/SDK versions; record them in artifacts.

Treat floating denormals consistently; enable software fallback if needed for cross-CPU parity.

For native interop, pin compiler flags and SIMD target (SSE2/AVX2) consistently across CI agents.

9) Coding Rules for Determinism (Normative excerpts)
No DateTime.Now, Stopwatch.Elapsed, or Random in sim paths. Use named PRNG streams only.

No foreach over Dictionary<>; copy keys, sort, then iterate.

No parallel modifies without commutative, associative, stable reducers. Prefer “produce diffs → deterministic merge”.

Quantize at boundaries: write-side converts to fixed-point; read-side expands.

Stable tie-breakers everywhere: (id, creationTick) for equals.

Inputs must be device-agnostic: map mouse/keyboard/gamepad to logical actions with tick stamps.

10) Tools & CLI (Normative)
det run --scenario X --seed S --threads K --ticks N --record outDir

det compare --a out1 --b out2 (checks hashes & prints first divergence)

det bisect --scenario X --seed S --threads {1,K} (auto bisect)

det hash --snapshot path (compute canonical hash of a single file)

det inspect --trace trace.ndjson --at tick (filter view)

All tools must produce machine-readable outputs (JSON) and exit with proper non-zero codes on failure.

11) Save/Load Identity Tests (Normative)
For each scenario & seed:

Run to N/2; save; record hash H_save.

Reload; compute hash H_load at same tick; require H_load == H_save.

Continue to N; final hash must match a fresh run to N.

12) Mapgen & Instances (Normative)
instance_seed = H(world_seed, wx, wy, type).

Mapgen sub-streams MUST be fixed (e.g., Rooms, Loot, Monsters).

Persistent instances: deltas applied in sorted order (time → coord → system → id).

Ephemeral (wilderness/encounter): not saved; determinism verified by immediate re-entry within the same replay (no persisted state).

13) Metrics & Budgets (Normative)
CI wall clock per scenario ≤ 6 minutes (smoke), ≤ 20 minutes (nightly).

Flake rate target: 0 (any nondeterministic flake is a P0).

Divergence bundle size ≤ 10 MB per incident.

14) Waivers & Change Control (Normative)
Any system that cannot meet bit-level equality MUST:

Document quantization/tolerance in this file,

Provide a domain hash that is equality-preserving under that tolerance, and

Get explicit waiver approval from engine/ci owners.

Changes to hashing, canonicalization, RNG, or checkpoint policy require bumping id: determinism.ci.v<minor> and updating all references.