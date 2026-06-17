# HumanFortress Run And Test Guide

Updated: 2026-06-16
Status: current operating notes

## Prerequisites

- .NET 8 SDK or runtime.
- On macOS, `RunTests.sh` currently uses `/opt/homebrew/opt/dotnet@8/bin/dotnet`.
- On Windows, use the `.bat` scripts or `dotnet` directly from a terminal.

## Run From Source

From the repository root:

```sh
dotnet run --project src/HumanFortress.App/HumanFortress.App.csproj
```

Windows shortcut:

```bat
RunGame.bat
```

`RunGame.bat` first tries the Debug win-x64 executable under `src/HumanFortress.App/bin/Debug/net8.0/win-x64/`; if it is absent, it falls back to `dotnet run`.

## Published Windows Builds

Older scripts still support published output:

- `RunGame-Direct.bat` runs the self-contained published executable when present.
- `RunGameRelease.bat` is the release/publish convenience path.
- Published output should include required native libraries such as `SDL2.dll` and `soft_oal.dll`.

These published folders are build artifacts, not the source-of-truth runtime path.

## Test Entry Point

Run all current regression/smoke/phase validation tests:

```sh
./RunTests.sh
```

On Windows:

```bat
RunTests.bat
```

The old app arguments `--test` and `--validate` no longer run tests inside the game executable. They print a compatibility message that points back to `./RunTests.sh`.

## Refactor Verification Commands

For architecture refactor work on macOS, prefer explicit .NET 8 and sequential commands:

```sh
/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false
/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll
/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors
```

Do not run overlapping App/test/solution builds in parallel; shared `obj` files and macOS apphost signing can race. For `dotnet exec`, pass app arguments directly; do not insert an extra `--` separator.

If a command appears stuck, check:

```sh
pgrep -fl "[d]otnet|[H]umanFortress|[M]SBuild|[V]BCSCompiler"
```

Only Roslyn/CodeAnalysis in that list is normal editor background activity, not a stuck build.

## Useful App Arguments

```sh
dotnet run --project src/HumanFortress.App/HumanFortress.App.csproj -- --init-only
dotnet run --project src/HumanFortress.App/HumanFortress.App.csproj -- --init-only --strict-content --content-warnings-as-errors
dotnet run --project src/HumanFortress.App/HumanFortress.App.csproj -- --auto-dig
dotnet run --project src/HumanFortress.App/HumanFortress.App.csproj -- --test-crash
```

- `--init-only`: creates a small world, loads core catalogs, then exits.
- `--strict-content`: turns structured content load errors into startup failures.
- `--content-warnings-as-errors`: implies `--strict-content` and treats content warnings as startup failures.
- `--auto-dig`: starts directly into fortress play and enqueues an automated dig seed.
- `--test-crash`: runs the crash-test path.

## Current Startup Content Flow

Startup loads runtime registries through:

```text
HumanFortress.Content.Loading.FortressContentLoader
```

The loader resolves both published-output paths and source-checkout paths for:

- `content/`
- `data/core/`

The game writes startup/runtime diagnostics to `fortress_debug.log`.

## Controls

See [Controls](../ui/CONTROLS.md) for the current player-facing key and mouse summary.
