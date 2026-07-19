---
id: determinism.ci.v2
status: implemented-contract
owner: engine/ci
last_updated: 2026-07-16
---

# Determinism CI Contract

This document describes the determinism evidence that the repository currently
produces. It deliberately does not describe the larger, aspirational save/load,
fuzzing, or hardware matrix that used to be recorded here.

The current gate answers a limited question:

> Given the same committed scenario profile, command journal, content identity,
> seeds, and tick count, does the production Runtime composition publish the same
> deterministic evidence across supported execution variants and the Linux and
> Windows CI runners?

A configured workflow is not evidence that a particular revision has passed.
Linux/Windows equality is established for a revision only when its
`compare-platform-determinism` job succeeds.

## Current CI Surface

`.github/workflows/dotnet-ci.yml` runs on pull requests, pushes to `main`, and
manual dispatch. Its build-and-test matrix currently contains:

- `ubuntu-latest`
- `windows-latest`

Each platform restores and builds `HumanFortress.sln` with .NET 8, then invokes
the standard MSTest project through `dotnet test`. The workflow runs these
categories as separate lanes:

- `content-identity`
- `discoverable`
- `end-to-end`
- `stage6-determinism`

Each lane writes a named TRX file under `artifacts/test-results`, and the workflow
uploads those TRX files as `test-results-${runner.os}` even when a test lane
fails. These are normal VSTest/MSTest results; the old executable-only smoke
harness is not the CI contract.

The Stage 6 lane runs `Stage6ScenarioMatrixTests` against
`benchmarks/scenarios/determinism-ci.v1.json`. It launches independent runner
processes for:

- two cold baselines;
- derived-cache priming;
- forced full GC every 100 ticks;
- an unmeasured process-warm pass;
- transport planner worker counts 1 and 4;
- tiered compilation disabled and enabled.

Every variant is compared with baseline A through the same artifact comparator
used by the cross-platform job. This verifies the declared matrix only; it does
not imply equivalence for arbitrary worker counts, JIT settings, hardware, or
unlisted workloads.

## Scenario Runner

`tools/HumanFortress.Scenarios` is a .NET 8 command-line program built as part of
the solution. It uses the production Runtime composition and strict content
loading. The supported commands are:

```text
run --profile <json> --output <json> [--base-dir <path>]
    [--transport-workers <n>] [--prime-derived-caches]
    [--force-gc-every <ticks>] [--process-warm]
compare --left <artifact> --right <artifact>
create-journal --output <json>
```

Scenario profiles are strict schema-versioned JSON. They declare the world mode
and dimensions, runtime and generation seeds, workload counts, warm-up and
measured ticks, checkpoint interval, and command-journal path. Profiles under
`benchmarks/scenarios/` are committed inputs, not benchmark results.

The runner fails closed when inputs are invalid, a journal cannot be decoded, the
requested worker count is not composed, path instrumentation is incomplete, no
committed checkpoint is published, or the scheduler reports any system failure.

## Hash And Checkpoint Contract

The implemented replay hash algorithm identifier is `sha256-v1`. Hash inputs use
the canonical primitive encodings owned by `ReplayHashBuilder`; this repository
does not currently use BLAKE3 for replay evidence.

The command journal is hashed in record order under the domain
`commands.replay_journal.v1`. Journal records contain the tick, command GUID,
command type, strictly increasing positive identity sequence, and payload bytes.
The runner recomputes the journal hash and strictly decodes every record before
starting a scenario.

At each profile checkpoint, Runtime publishes a committed replay checkpoint under
the domain `runtime.replay.checkpoint.v1`. The scenario artifact records:

- aggregate checkpoint hash and committed tick;
- world hash;
- RNG hash;
- executed and pending command-journal hashes;
- transport, mining, and craft replay-state hashes when present.

The artifact also records stable input identity, content signature and mechanical
hash, initial/final authoritative counts, and deterministic counters. These
fields form the equality evidence for the scenario.

Save/load identity is not part of this Stage 6 gate. Persistence work is deferred,
so a passing determinism artifact must not be presented as save/load validation.

## Artifact Comparison

A run writes schema-v1 JSON with four top-level projections:

- `identity`: profile, scenario, journal, and hash-algorithm identity;
- `variant`: worker/cache/GC/process/JIT settings used for the run;
- `deterministic`: checkpoints, authoritative counts, content identity, and
  deterministic counters;
- `performance`: timing, allocation, GC, working-set, cache, checkpoint-size, and
  environment observations.

`compare` requires the schema and all `identity` fields to match, then compares
the complete `deterministic` projection. It intentionally ignores `variant` and
`performance`. A deterministic mismatch exits with code 1 and reports the first
checkpoint difference plus any differing authority fields. Invalid input exits
with code 2.

This separation is mandatory: elapsed time, allocation counts, GC counts,
working set, processor count, operating-system description, and cache telemetry
must never decide deterministic equality.

## Cross-Platform Workflow

After a successful build, each platform job runs the committed
`determinism-ci.v1.json` profile with one transport planning worker and uploads:

```text
determinism-Linux/run.json
determinism-Windows/run.json
```

The Linux comparison job is scheduled only after both complete matrix jobs
succeed. It downloads both artifacts and executes:

```bash
dotnet tools/HumanFortress.Scenarios/bin/Debug/net8.0/HumanFortress.Scenarios.dll \
  compare \
  --left artifacts/platforms/determinism-Linux/run.json \
  --right artifacts/platforms/determinism-Windows/run.json
```

The job fails if input identities, checkpoints, authoritative state, or
deterministic counters differ. Performance and environment fields may differ
without failing this comparison.

macOS is not in the current CI matrix. A local macOS run is useful development
evidence, but it is neither a Linux/Windows CI pass nor proof of a supported
three-platform matrix.

## Reproduction

After building the solution, a local baseline and comparison can be produced with:

```bash
dotnet exec tools/HumanFortress.Scenarios/bin/Debug/net8.0/HumanFortress.Scenarios.dll \
  run --profile benchmarks/scenarios/determinism-ci.v1.json \
  --output artifacts/determinism/baseline.json \
  --transport-workers 1

dotnet exec tools/HumanFortress.Scenarios/bin/Debug/net8.0/HumanFortress.Scenarios.dll \
  run --profile benchmarks/scenarios/determinism-ci.v1.json \
  --output artifacts/determinism/workers-4.json \
  --transport-workers 4

dotnet exec tools/HumanFortress.Scenarios/bin/Debug/net8.0/HumanFortress.Scenarios.dll \
  compare \
  --left artifacts/determinism/baseline.json \
  --right artifacts/determinism/workers-4.json
```

The profile, journal, content, build, and relevant environment controls must be
kept fixed when interpreting a comparison. A changed profile or journal is a new
input identity, not a determinism regression.

## Current Non-Claims

The current tooling does not provide automatic first-divergence bisection,
snapshot-diff bundles, chaos scheduling, fuzzed content, a nightly seed matrix,
or save/load round-trip verification. Those capabilities require separate design
and implementation before they can become CI requirements.

Changes to canonical replay fields, domain tags, hashing, artifact schemas, or
comparison semantics require an explicit schema/contract version decision and
corresponding regression coverage.
