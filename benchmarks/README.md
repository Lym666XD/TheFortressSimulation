# HumanFortress Scenario Evidence

This directory contains committed, reproducible scenario inputs for determinism
and performance investigation. It does not contain a general-purpose performance
claim or a promise that every profile meets a fixed frame budget.

## Layout

`scenarios/*.json` are strict schema-v1 workload profiles:

| Profile | Intended evidence |
| --- | --- |
| `determinism-ci.v1.json` | Fast 2,000-tick production-composition CI matrix |
| `generated-fortress.v1.json` | Seeded fortress generation through Runtime |
| `early.v1.json` | 50 workers, 10,000 items, 500 transport requests |
| `mid.v1.json` | 250 workers, 50,000 items, 3,000 transport requests |
| `target.v1.json` | 1,000 workers, 100,000 items, 10,000 transport requests |
| `stress.v1.json` | Dense transport backlog and path pressure |

`scenarios/journals/*.json` are strict command-journal fixtures. A profile refers
to its journal by relative path. `decisions/` records dated conclusions drawn
from particular evidence; a decision must retain the limits of the measurement.

Generated run artifacts belong under the ignored `artifacts/` directory, for
example `artifacts/benchmarks/<profile>/<variant>.json`. Do not commit raw timing
artifacts as though they were stable repository-wide expectations.

## Build And Run

Build the solution once before invoking the runner:

```bash
dotnet restore HumanFortress.sln
dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal \
  -p:RunAnalyzers=false -p:UseAppHost=false
```

Run a declared profile with an explicit output path:

```bash
dotnet exec tools/HumanFortress.Scenarios/bin/Debug/net8.0/HumanFortress.Scenarios.dll \
  run \
  --profile benchmarks/scenarios/determinism-ci.v1.json \
  --output artifacts/benchmarks/determinism-ci/baseline-a.json \
  --transport-workers 1
```

Useful controlled variants are:

```text
--transport-workers <positive integer>
--prime-derived-caches
--force-gc-every <positive tick interval>
--process-warm
```

Tiered JIT is controlled through `DOTNET_TieredCompilation`, not a runner option.
For example, a POSIX shell run with tiering disabled is:

```bash
DOTNET_TieredCompilation=0 dotnet exec \
  tools/HumanFortress.Scenarios/bin/Debug/net8.0/HumanFortress.Scenarios.dll \
  run \
  --profile benchmarks/scenarios/determinism-ci.v1.json \
  --output artifacts/benchmarks/determinism-ci/tiered-off.json \
  --transport-workers 1
```

Compare deterministic evidence from two artifacts with:

```bash
dotnet exec tools/HumanFortress.Scenarios/bin/Debug/net8.0/HumanFortress.Scenarios.dll \
  compare \
  --left artifacts/benchmarks/determinism-ci/baseline-a.json \
  --right artifacts/benchmarks/determinism-ci/workers-4.json
```

`compare` exits non-zero when identity or deterministic evidence differs. See
`docs/architecture/DETERMINISM_CI.md` for the exact comparison contract.

## Artifact Interpretation

Every artifact separates its data into:

- `identity`: profile, scenario, journal, and hash identity;
- `variant`: execution controls such as worker count and cache/GC/JIT settings;
- `deterministic`: replay checkpoints, authoritative counts, and counters;
- `performance`: environment, tick-time distributions, allocations, GC,
  working set, cache telemetry, and checkpoint sizes.

Performance data is diagnostic only and is never included in deterministic
comparison. Equal replay hashes do not prove a performance improvement; different
timings do not prove a determinism failure.

When recording a performance decision, include at least the profile, revision,
platform/architecture, runtime settings, warm-up policy, repeated-run method,
sample count, compared deterministic hash result, and the raw metrics used. Treat
a single local run as exploratory evidence. Promote a tuning default only after
repeatable measurements show a material benefit without an unacceptable
allocation, latency-tail, or correctness regression.
