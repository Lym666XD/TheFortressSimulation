# HumanFortress Content System

Updated: 2026-07-10
Status: current implementation notes plus future direction

This document replaces the old split between `CONTENT_REGISTRY_OVERVIEW.md` and the early `.cpack` build-pipeline plan. The current game loads JSON directly at runtime. Compiled content packs remain a future goal, not the current implementation.

## Current Source Layout

Runtime registries:

```text
content/
  registries/
    materials.authoring.json
    materials.registry.json
    terrain_kinds.json
    geology.json
    zones.json
    input.bindings.json
    orders.registry.json
    professions.json
    ui.workshop_categories.json
    tuning.*.json
  schemas/
    *.schema.json
  templates/
    biomes/
```

Core game catalogs:

```text
data/core/
  creatures/
  items/
  placeable/
  recipes/
  workshops/
```

Important distinction:

- `content/registries` contains runtime registries, tunings, schemas, UI/input convenience registries, geology, terrain, and zones.
- `data/core` contains item definitions, creature definitions, construction/workshop definitions, recipes, and placeable data.

## Current Loader Ownership

The current content entry point is:

```csharp
HumanFortress.Content.Loading.FortressContentLoader
```

It resolves both published-output paths and source-checkout paths:

- `ResolveContentPath(baseDir)`
- `ResolveCoreDataPath(baseDir)`
- `ResolveRegistryFile(baseDir, fileName)`

It returns:

```text
FortressContentLoadResult
  ContentPath
  CoreDataPath
  RuntimeContentRegistryLoadResult?
  CoreContentCatalogLoadResult?
  RegistriesAlreadyLoaded
  Issues
```

`Issues` contains structured `FortressContentIssue` values with severity, code, and message. App logs them through App.Diagnostics `FortressContentIssueLogger`, and runtime session glue receives that as an injected callback. `FortressContentLoadResult.ThrowIfInvalid(...)` and `FortressContentLoader.LoadStrict(...)` provide the Content-owned fail-fast path for CI/headless checks.

## Runtime Registry Loading

`FortressContentLoader` is the public bootstrap facade. Internally, `RuntimeContentRegistryLoader` loads the structured runtime registry:

- `HumanFortress.Content.Registry.ContentRegistry`

The concrete implementation now compiles from `HumanFortress.Content.Registry`; registry contracts compile from `HumanFortress.Contracts.Content.Registry`. Concrete registry helper classes are internal implementation details.

Current behavior:

1. Load the structured registry from `content/`.
2. Optionally continue when the structured registry fails, depending on `continueOnStructuredRegistryError`.
3. Return warnings, errors, and structured failure text.

The old `HumanFortress.Core.Content.ContentRegistry` source has been deleted. Runtime bootstrap now loads the structured registry only. Runtime geology and zone JSON DTOs now compile from `HumanFortress.Contracts.Content`.

## Core Catalog Loading

`CoreContentCatalogLoader` aggregates the `data/core` load into one result:

```text
CoreContentCatalogLoadResult
  Items
  Creatures
  CoreData
  Constructions
  Recipes
```

Internal loader implementations:

- `ItemDefinitionCatalogLoader`
- `CreatureDefinitionCatalogLoader`
- `CoreDataRegistryLoader`

`CoreDataRegistryLoader` lives in `HumanFortress.Content.Definitions` and loads:

- `data/core/workshops/core_workshop_*.json`
- legacy `data/core/placeable/workshops.json` when present
- `data/core/recipes/*.json`

It returns immutable construction and recipe catalog snapshots. `FortressRuntimeContentSnapshotLoader.ApplyCoreData(...)` applies those snapshots to the structured registry and returns the active runtime snapshot used by App composition.

Cross-module runtime content contracts such as `IRuntimeMaterialCatalog`, `IRuntimeTerrainKindCatalog`, `IRuntimeGeologyCatalog`, `ConstructionTuning`, `PlaceableTuning`, `ContentVersion`, material/terrain/geology/biome definition DTOs, terrain bit-layout DTOs, alias/migration DTOs, and fixed-point material primitives compile from `HumanFortress.Contracts` while preserving their historical namespaces. Tuning JSON parsing uses `System.Text.Json` so Simulation/Jobs/Runtime can consume tuning objects without depending on the concrete content registry implementation.

Profession definitions follow the same boundary: public callers use `ProfessionRegistryLoader.Load(...)` and receive `IProfessionRegistry`; the concrete profession registry implementation remains internal to Content.

The old hard-coded `MaterialIdRegistry` display table has been removed; runtime terrain display should use loaded geology/material content and active-session geology handles instead of fixed numeric material ids.

The old Core-owned `MaterialSelectionService` global preference cache has also been removed because it had no write call sites. Future user material preferences should live in an explicit App/UI or saved-player-preferences model, not in Core content state.

## Runtime Application Flow

Fortress session content flow:

```text
Runtime.SimulationWorldContentLoader.LoadCoreContent(world, baseDir)
  -> FortressContentLoader.Load(baseDir)
  -> injected App content issue logger
  -> world.Creatures.SetDefinitionCatalog(...)
  -> world.Items.SetDependencies(world)
  -> world.Items.SetDefinitionCatalog(...)
  -> FortressRuntimeContentSnapshotLoader.ApplyCoreData(...)
  -> snapshot.ZoneDefinitions registered into world.Zones.Manager
  -> returns FortressRuntimeContentSnapshot
```

Long-lived runtime systems should prefer injected read-only catalog interfaces over parsing files or using singleton registries directly.

Preferred runtime reads:

```csharp
IItemDefinitionCatalog
ICreatureDefinitionCatalog
IConstructionCatalog
IRecipeCatalog
IRuntimeMaterialCatalog
IRuntimeTerrainKindCatalog
IRuntimeGeologyCatalog
```

Avoid new runtime reads through:

```csharp
ContentRegistry.Instance
```

Content-owned bootstrap, registry implementation, and snapshot code may still read `ContentRegistry.Instance` while capturing the active runtime snapshot. Runtime, Jobs, Simulation, Navigation, WorldGen, App UI, and App job adapters should receive the resulting snapshot/catalog interfaces instead of reaching back into the singleton.

The old `ConstructionRegistry` and `RecipeRegistry` singleton compatibility classes have been deleted. Construction/recipe definitions, read-only catalog interfaces, and immutable catalog stores now compile from `HumanFortress.Contracts.Content.Registry`.

## App Convenience Registries

Some App/UI registries are still presentation conveniences:

- input bindings;
- order display names;
- profession definitions;
- workshop category mapping;
- legacy tuning compatibility paths.

These should resolve files through:

```csharp
FortressContentLoader.ResolveRegistryFile(baseDir, "file.json")
```

Do not add new App-side `Path.Combine(baseDir, "content", "registries", ...)` call sites.

## Current Error Policy

Current content loading should:

- report missing `content/` or `data/core/` paths as structured issues;
- report zero-count item/creature/construction/recipe catalogs as errors;
- report loader error counts as errors;
- report structured registry warnings as warnings;
- log issues without crashing the UI host by default;
- throw `FortressContentLoadException` when callers use `LoadStrict(...)` or `ThrowIfInvalid(...)`;
- support warning promotion through `treatWarningsAsErrors`.

The app exposes this through `--strict-content` and `--content-warnings-as-errors`. The recommended CI smoke command is:

```sh
dotnet run --project src/HumanFortress.App/HumanFortress.App.csproj -- --init-only --strict-content --content-warnings-as-errors
```

## Future Pack Pipeline

The old `.cpack` design is not deleted. It is archived as:

```text
docs/archive/legacy/CONTENT_BUILD_PIPELINE_FUTURE.md
```

Future pack direction:

- JSON + schemas compile to deterministic registry snapshots.
- Pack signatures are stored for save compatibility.
- Hot reload swaps immutable snapshots at barriers.
- Runtime keeps string IDs in saves and uses numeric handles only in-session.

That future design should not be cited as current implementation until a real builder/loader exists.

## Documentation Rules

- JSON files under `data/core` and `content/registries` are the machine-readable truth.
- Markdown under `docs/workshops` is human-readable reference only.
- `docs/archive/other/core_workshop_*.json` contains old workshop JSON copies and should be treated as historical until reconciled.
- Any future content doc should state whether it describes current runtime loading or future compiled packs.
