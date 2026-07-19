# Transport Planner Worker Default Remains One

- Date: 2026-07-16
- Status: accepted for the current Stage 6 checkpoint
- Scope: `determinism-ci-v1`, local macOS arm64 run
- Decision: keep the production default at `transport-workers=1`

## Evidence

Baseline A and the four-worker variant produced the same deterministic hash for
this run. That is useful local correctness evidence for the tested profile and
revision; it is not a cross-platform result.

| Variant | Tick p95 | Total allocated bytes |
| --- | ---: | ---: |
| Worker 1, baseline A | 6,948 us | 4,490,270,408 |
| Worker 4 | 6,636 us | 4,728,027,888 |

The four-worker result reduced the observed p95 by 312 us, approximately 4.5%,
while increasing total allocation by 237,757,480 bytes, approximately 5.3%.

## Decision Rationale

One local measurement is not enough to distinguish a stable planner improvement
from process, scheduler, GC, thermal, or sampling noise. The measured p95 change
is modest, and it comes with a material allocation increase. Equal hashes protect
the deterministic result for this sample, but they do not establish that four
workers are faster across representative workloads or platforms.

The conservative production default therefore remains one worker. The four-worker
path remains part of the determinism matrix so its correctness continues to be
checked without making it the runtime default.

## Revisit Criteria

Reconsider the default only with repeated, warmed measurements across relevant
profiles and supported CI platforms. Evidence should show a material and stable
tail-latency or throughput improvement, retain identical deterministic evidence,
and explain or remove the allocation regression. Linux/Windows CI equality and
local performance measurements must be reported separately.
