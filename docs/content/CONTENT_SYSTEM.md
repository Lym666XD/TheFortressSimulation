# HumanFortress Content System

Updated: 2026-06-12
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

`Issues` contains structured `FortressContentIssue` values with severity, code, and message. App logs them through `FortressContentIssueLogger`.

## Runtime Registry Loading

`RuntimeContentRegistryLoader` coordinates the transitional registry load while two registry systems still coexist:

- legacy `HumanFortress.Core.Content.ContentRegistry`
- structured `HumanFortress.Core.Content.Registry.ContentRegistry`

Current behavior:

1. Load the legacy registry from `content/`.
2. Load the structured registry from `content/`.
3. Optionally continue when the structured registry fails, depending on `continueOnStructuredRegistryError`.
4. Return counts, warnings, errors, and structured failure text.

This transitional shape exists because not all gameplay code has migrated to the structured registry yet.

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

Loaders:

- `ItemDefinitionCatalogLoader`
- `CreatureDefinitionCatalogLoader`
- `CoreDataRegistryLoader`

`CoreDataRegistryLoader` loads:

- `data/core/workshops/core_workshop_*.json`
- legacy `data/core/placeable/workshops.json` when present
- `data/core/recipes/*.json`

It returns immutable construction and recipe catalog snapshots. `ContentRegistry.ApplyCoreData(...)` swaps those snapshots into the structured registry.

## Runtime Application Flow

Fortress session content flow:

```text
SimulationWorldContentLoader.LoadCoreContent(world, baseDir)
  -> FortressContentLoader.Load(baseDir)
  -> FortressContentIssueLogger.LogIssues(...)
  -> world.Creatures.SetDefinitionCatalog(...)
  -> world.Items.SetDependencies(world, ContentRegistry.Instance)
  -> world.Items.SetDefinitionCatalog(...)
  -> ContentRegistry.Instance.ApplyCoreData(...)
  -> RuntimeContentRegistry.Instance.Zones registered into world.Zones.Manager
```

Long-lived runtime systems should prefer injected read-only catalog interfaces over parsing files or using singleton registries directly.

Preferred reads:

```csharp
ContentRegistry.Instance.Constructions
ContentRegistry.Instance.Recipes
IItemDefinitionCatalog
ICreatureDefinitionCatalog
IConstructionCatalog
IRecipeCatalog
```

Avoid new runtime reads through:

```csharp
ConstructionRegistry.Instance
RecipeRegistry.Instance
```

Those compatibility classes still exist but should not grow new dependencies.

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
- log issues without crashing the UI host unless the caller decides to treat warnings/errors as blocking.

The exact game-start blocking policy is still evolving.

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
- `docs/other/core_workshop_*.json` contains old workshop JSON copies and should be treated as historical until reconciled.
- Any future content doc should state whether it describes current runtime loading or future compiled packs.
