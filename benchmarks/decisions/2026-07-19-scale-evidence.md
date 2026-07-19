# Scale Evidence Does Not Support The Target Profile

- Date: 2026-07-19
- Status: accepted as local exploratory evidence; target scale gate blocked
- Scope: one local macOS arm64 environment running .NET 8.0.27
- Runtime: workstation GC, 10 logical CPUs, default tiered compilation
- Decision: do not claim that the current runtime supports the target profile

## Evidence

The following measurements come from local exploratory artifacts produced by the
declared Stage 6 scenario profiles. Tick-time values are microseconds and memory
values are bytes.

| Profile | Samples | Tick p50 | Tick p95 | Tick p99 | Tick max | Alloc/tick p50 | Alloc/tick p95 | Total allocated | Peak working set |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Early | 190 | 135,942 | 150,738 | 157,470 | 161,834 | 95,697,432 | 95,946,728 | 18,188,444,136 | 159,842,304 |
| Mid | 45 | 2,561,766 | 3,028,859 | 3,111,868 | 3,111,868 | 2,304,241,320 | 2,304,251,032 | 103,694,213,096 | 814,776,320 |
| Target | 4 | 445,781,296 | 1,912,828,079 | 1,912,828,079 | 1,912,828,079 | 49,881,158,344 | 60,863,051,800 | 210,593,545,112 | 3,517,169,664 |
| Generated fortress | 9 | 52,957 | 99,973 | 99,973 | 99,973 | 6,261,312 | 6,273,816 | 56,363,328 | 112,885,760 |

All four runs reported zero scheduler failures and zero quarantined scheduler
systems. That is useful scheduler-health evidence, but it does not offset the
observed latency and allocation costs.

The Target result has only four measured samples, so its percentile estimates
must not be treated as stable distribution statistics. The observed magnitude is
nevertheless sufficient to block the scale gate: even the measured median tick
is hundreds of seconds and the run allocates tens of gigabytes per tick. These
results cannot be presented as evidence that Target is supported.

## Evidence Limits

These are single-machine exploratory measurements, not portable performance
baselines. They do not establish behavior on Linux, Windows, another CPU, server
GC, a different JIT configuration, or a sustained warmed workload. Comparisons
between profiles also combine different world sizes and workload counts; they do
not isolate a causal subsystem.

The raw artifacts remain under `/tmp` and are intentionally not committed.
Committing the summarized decision preserves the engineering conclusion without
turning environment-sensitive timing output into a repository-wide expectation.

## Stage 7 Measurement Gate

Stage 7 must begin by locating measured owners of setup and tick cost. The first
investigation targets are the full authoritative replay/hash/checkpoint path and
the relevant index and snapshot construction paths. These are investigation
candidates, not asserted root causes.

Before changing runtime behavior:

1. Separate and record scenario setup time from simulation tick time.
2. Add timing for each scheduler stage and other major tick phases.
3. Capture sampling-profiler and allocation traces on representative Early, Mid,
   and bounded Target runs.
4. Attribute latency and allocation to concrete call paths from those traces.
5. Optimize only the measured owners, then rerun the same profiles and verify
   deterministic evidence and scheduler health alongside performance.

No optimization should be approved from speculation based on aggregate counters
alone. The immediate objective is credible attribution, not a premature rewrite.
