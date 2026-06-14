# Status Snapshots

Updated: 2026-06-12
Status: historical reference

Historical phase-completion snapshots and implementation summaries have been moved to `../archive/status/`.

Those files preserve what was true when each phase was written, but they are not current run/build/test instructions.

Current run and test guidance:

- Current run notes are in `../operations/README-RUN.md`.
- Current tests live in `tests/HumanFortress.App.Tests` and are launched by `../../RunTests.sh` or `../../RunTests.bat`.

Known historical wording in those files includes old paths such as `game/HumanFortress.App.exe`, old App `--test` / `--validate` harness references, and the old `HaulJobSystem` name. Do not update those snapshots in place unless intentionally rewriting history into a new current document.
