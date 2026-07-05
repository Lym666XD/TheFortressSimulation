# HumanFortress Refactor Pitfalls and Lessons

Date: 2026-06-26

This document records practical pitfalls found during the current architecture refactor. It is meant to keep future refactor work fast and predictable.

## Save/Replay Pitfalls

### Runtime command identity is not just payload identity

Normal Runtime session enqueue wraps commands in `RuntimeIdentifiedCommand` so
duplicate same-tick/same-payload commands still have distinct replay/debug ids.
When turning executed commands into persistence data, capture the optional
Runtime command identity sequence in `CommandReplayRecord`; otherwise a decoder
can rebuild the inner command payload but cannot reproduce the original wrapper
`CommandId`.

### Runtime command payloads need explicit versions

Binary payloads are compact but fragile. Runtime command `Serialize()` methods
should write the shared Runtime command payload version header and the Runtime
replay factory should reject unsupported versions, malformed enum values, extra
trailing bytes, and reconstructed id mismatches. Do not let the save path depend
on undocumented field order.

### Replay restore must be batch-atomic

Do not call `CommandQueue.RestoreCommands(...)` while decoding records one by
one. Decode the full batch first, collect structured issues, and only replace
the pending queue when every record is valid. Also advance the Runtime command
identity cursor to the restored max identity sequence, or the next live enqueue
can reuse replay/debug ids from the restored log.

### App save UI should pass slot directories, not assemble Runtime documents

The App layer owns slot selection, confirmation UI, and user-facing failure
presentation. Runtime owns save snapshot authority, document validation,
document file helpers, command replay mapping, and restore semantics. App access
interfaces should expose directory-level save/validate/restore operations rather
than Runtime-internal codec/store/restorer helpers, Core command replay records,
or live Simulation/World objects.

### Partial world restore must fail closed

When a world payload slice only supports terrain, it must fail with structured
restore issues if item, creature, reservation, stockpile, placeable, or order
sections are present. Never let a partial loader silently drop authoritative
sections just because the payload schema does not yet cover them. Each new
section should be added with a hash round-trip test before it becomes loadable.

As the payload grows, keep the supported-section message precise. The current
world payload restore supports terrain, ground items, creatures, global
reservations, stockpile zones, owned placeables/workshop state, and active
orders. Carried, contained, equipped, installed, or item-local reservation-token
state still depends on inventory, container, and job restore paths and must
remain rejected until those corresponding authoritative sections exist.

Adding payload fields without binding them into the replay hash is also a
partial-load bug. If the restorer claims hash-checked restoration, every
gameplay-affecting field in the newly supported slice needs to be exported,
restored, and included in the section hash before the slice is considered
loadable.

Keep Simulation world payload mapping split by authoritative section. The
builder's main file should assemble sections in canonical order, while
metadata/terrain, entities, stockpiles, placeables, orders, and shared
conversion helpers stay in focused partials. The restorer's main file should
own the restore order and final hash comparison, while payload validation,
placeable validation/restore, and conversion/failure helpers stay separate.
When adding a new world payload section, add the export/import/hash fields in
the section-owned partials and update the architecture smoke guard instead of
growing a new save/load God Object or moving mapping into Runtime/App.

Simulation state managers are not a dumping ground for every behavior that
touches a subsystem. Keep `ItemManager` split by catalog access, position
indexing, read queries, stack/move/remove mutations, spawn behavior, and
save/restore mapping. Keep `OrdersManager` split by haul, mining,
construction/buildable, and save/restore mapping. The ownership stays in
Simulation because these managers own authoritative runtime state, but the
files should still show which responsibility is being changed. Do not move
these helpers into Runtime/App to avoid touching Simulation, and do not merge
them back into single manager files just because all partials share the same
private dictionaries/queues.

### Replay hashes need canonical primitive encoding

Replay checkpoint hashes should be built from explicit authoritative fields
using stable primitive encodings. Avoid object `GetHashCode()`, dictionary
enumeration order, diagnostic strings, or render/UI DTOs. `ReplayHashBuilder`
owns primitive encoding only; Simulation/Runtime snapshot hash builders still
need to own the field list.

Do not leave these checks as one-off terminal commands. The formal smoke runner
now scans save/replay/hash authority paths for object `GetHashCode()`,
dictionary `Keys`/`Values` view iteration, and production `Guid.NewGuid()`.
If a future save/replay implementation needs an exception, document why the
ordering is still canonical before weakening the guard.

The same rule applies to mailbox/drain ordering and future save-slot replay
boundaries. If an ordering key needs a chunk, entity, system, or content key,
encode the explicit primitive fields into a stable sort key; do not pack
`GetHashCode()` into the persisted/replay order, even when the current runtime
only needs a small tie-breaker.

Typed mutation diff sort keys should use the centralized Simulation helper.
Enqueue-ordered command-edit diffs should use local sequence order, while
spatial/priority diffs should call the explicit chunk/cell/priority or
stockpile/cell/priority helpers. This keeps ordering policy visible and prevents
small bit-packing differences from becoming replay-only bugs.

Replay hash builders must be read-only. Do not use queue-drain APIs such as
order designation drains while hashing; use stable snapshot/read APIs instead.
Hashing should never advance simulation queues, clear pending work, mutate cache
generations, or change future tick behavior.

Private counters can still be authoritative replay state. If a future action
derives stable ids from a private sequence, RNG cursor, or generation counter,
the hash/save snapshot must include that counter even when the current visible
collection can be reconstructed. Workshop queue entry ids depend on the
workshop queue sequence, so the placeable replay hash covers the counter in
addition to the current queue entries.

Compatibility save helpers that return dictionaries are not canonical replay
streams. Use sorted snapshot rows for replay/save hashing. `RngStreamManager`
still has dictionary save/restore helpers for compatibility, but deterministic
checkpointing should use `GetStateSnapshot()` plus `RngReplayHashBuilder` so
stream ordering cannot depend on `ConcurrentDictionary` enumeration.

Derived cross-indexes should not become save authority just because they live
beside authoritative data. Placeable hashes cover owned placeable instances and
their construction/workshop/door state, but exclude chunk furniture cells and
cross-chunk external refs because those are rebuildable from owned placeables
and footprints.

Long-horizon job replay snapshots should come from job-owned state, not UI
debug DTOs. Transport now exposes pending request, active job, backlog, and
scheduling hint snapshots for replay hashing; mining exposes active job,
backlog, deferred stairwell, reserved tile, and recent-completion snapshots.
Craft exposes active job and backlog snapshots, while construction-site progress
remains placeable authority because the current construction executor is
scan-based. Keep shard indexes, planner outboxes, and debug/stat rows out of
the authoritative hash unless they start affecting behavior across tick
barriers directly.

Transport checkpointing has two authoritative owners: the pending request queue
and the executor. A Runtime aggregate checkpoint must feed both snapshots into
`TransportReplayHashBuilder`; executor-only hashing silently loses pending
transport work.

Runtime snapshot metadata should be authored by Runtime, not App. UI tick
counters drive presentation effects and toasts; they are not simulation ticks
and should not be passed into Runtime read-model builders as checkpoint or
debug authority.

Pending commands are save authority too. `CommandQueue.GetExecutedCommandRecords()`
is not enough for a barrier save because future-tick commands may still be in
the pending queue. Use `CommandQueue.GetReplaySnapshot()` when a checkpoint or
save package needs pending and executed command records to match one manifest.

Runtime save manifests should name sections, not expose live owners. World
section hashes/counts come from Simulation-owned save/replay builders, command
sections come from Core replay records, content signatures come from Content
snapshots, and Runtime assembles the manifest/package. App should not collect
`World`, job-system, or `CommandQueue` internals to build save files.

Do not make App decode command replay rows. App can own slot selection and file
IO, but the document row to `CommandReplayRecord` mapping belongs in Runtime
because Runtime owns command payload schemas and replay restore. Public save
documents should stay in Contracts DTOs; Core command records should remain an
internal Runtime/load concern at the App boundary.

Do not make App restore RNG streams either. The save document may expose
primitive RNG stream rows as Contracts DTOs, but mapping them back to
`RngStreamStateSnapshot`, checking the canonical RNG hash/count, clearing stale
materialized streams, and restoring stream state belongs in Runtime/Core. App
should only receive a structured result from a Runtime save-slot/restore port.

Do not expose save/replay checkpoint ports through the ordinary App runtime
session aggregate. `FortressRuntimeSessionFactory.Create(...)` returns the
App-facing session port set, which intentionally excludes save/replay methods.
The full aggregate is internal/friend-only for Runtime tests until a dedicated
save UI boundary exists.

Full staged restore has an intentional Runtime order: validate the document,
restore the supported world payload through the Runtime session factory, restore
RNG streams after the session factory resets services, then restore pending
commands. Reversing this order silently loses RNG or pending-command state
because new session composition clears per-session services.

Keep first-pass save document IO clearly scoped. A helper that writes the
assembled Runtime save document is useful, but it is not the full slot format:
do not treat it as chunk-sharded world persistence, autosave policy, migration,
or complete load orchestration.

## App/UI Boundary Pitfalls

### Internal implementations still need explicit interface entrypoints

When hardening implementation classes inside Jobs, Navigation, or Runtime, changing a class member from `public` to `internal` is not enough if the class implements an interface. C# requires either a public implicit implementation or an explicit interface implementation.

Preferred pattern for implementation assemblies:

```text
internal method/property
  -> used by same assembly, Runtime, and friend tests where useful

explicit interface implementation
  -> used by cross-module contracts and command/runtime seams
```

Do this especially for `ITick`, navigation contracts, job diff emitters, loggers, profession adapters, command targets, and snapshot/session facades. If existing friend tests instantiate a concrete implementation, keep an internal direct method and delegate the explicit interface method to it instead of making tests cast to the interface everywhere.

### Project references are not the full boundary

A clean `.csproj` graph is necessary but not sufficient. Source imports and
friend assemblies can drift even when project references look correct. Keep the
architecture smoke runner's source-import matrix and `InternalsVisibleTo`
matrix in sync with intentional module ownership changes.

Default rule:

```text
Contracts imports only Contracts namespaces and has no package, framework, or project references.
Core/Content/Navigation import Contracts plus their own implementation namespace.
Simulation imports Contracts/Core/Simulation.
Jobs and WorldGen import Contracts/Core/Simulation plus their own namespace.
Runtime may import implementation projects but never App.
App imports App/Contracts/Runtime only, with direct Runtime imports limited to
startup, adapter, world-generation provider, and App content-file-location
boundaries.
```

If a future refactor needs to weaken one of those edges, update the test and the
architecture docs in the same change so the exception is explicit.

### Namespace cleanup is not project renaming

When moving concrete implementation namespaces to explicit `.Implementation`
namespaces, keep project and assembly names stable unless the batch is actually
renaming projects. Mechanical rewrites can accidentally turn
`HumanFortress.WorldGen.csproj` into a fake implementation project name or
weaken guard strings that are supposed to catch old root namespaces.

For these compatibility cleanup batches, update source namespaces/usings and
then explicitly re-check:

```text
ProjectReference strings
architecture smoke-test allowlists
forbidden root-namespace guard strings
friend assembly names
```

Navigation and WorldGen now use explicit concrete implementation namespaces:

```text
HumanFortress.Navigation.Implementation
HumanFortress.WorldGen.Implementation
```

Their stable cross-module DTOs/interfaces still live in Contracts, and Runtime
should remain the production composition boundary for concrete implementation
creation.

For implementation projects that already have strong internal module folders,
avoid treating the project root namespace as a convenient catch-all. Jobs now
uses focused directory namespaces for configuration, diff emitters, logging,
orchestration, profession bridges, safety, mining, construction, craft,
transport, and replay helpers. Runtime wrappers should import those focused
role namespaces explicitly; broad `using HumanFortress.Jobs;` hides whether a
file is depending on scheduler tunings, profession bridges, orchestration
contracts, or concrete diff emitters.

### Do not pass loaded-session snapshots through input controllers

`FortressLoadedSessionSnapshot` is frame-render presentation state, not a general input dependency bag. Passing that whole snapshot through mouse, keyboard, overlay-click, placement, or map-click controllers makes UI code look like it is allowed to grow new dependencies on render/session state.

Prefer explicit dependencies:

```text
UiServices?
NavigationOverlay?
HasFortressMap
FortressRuntimeAccess snapshot/query methods
```

The frame renderer is currently the only App path that should need the loaded-session snapshot. New UI/debug panels should ask Runtime for DTOs instead of adding another `LoadedSession` parameter.

### App map renderers should draw viewport DTOs only

Main map terrain/entity display now enters App through:

```text
Runtime snapshot builder
  -> SimulationMapViewportData / MapViewportCellView

FortressMapRenderer
  -> clears the SadConsole surface
  -> draws DTO glyph/color cells
  -> draws the App-owned navigation overlay from Runtime navigation DTOs
```

Do not reintroduce App-side reads of `World`, `FortressMap`, chunks, tiles, geology catalogs, item managers, creature managers, or terrain kinds inside `FortressMapRenderer` or frame/overlay render helpers. If map rendering needs another visible fact, add it to the Runtime map viewport DTO or a focused Runtime overlay DTO.

Frame-level render data should stay aggregated:

```text
SimulationFrameRenderData
  -> SimulationMapViewportData
  -> SimulationNavigationOverlayData
  -> SimulationTileInspectionData
```

Avoid splitting `FortressFrameRenderer` back into separate Runtime calls for map viewport, navigation overlay, and tile inspection. Add fields to the frame DTO when the frame needs more read-side simulation data.

Overlay-level render data should also stay aggregated:

```text
SimulationUiOverlayFrameData
  -> build catalog
  -> jobs/workshops
  -> zone/stockpile overlay/detail
  -> management drawer / Work drawer / Debug menu data
```

Avoid adding new per-panel Runtime calls directly in `FortressUiOverlayRenderer`; add fields to the overlay-frame DTO unless the query depends on immediate drag state, command input, or App diagnostics.

### Runtime public ports should not expose presentation primitives

App can keep using SadRogue `Point`/`Rectangle` inside App-owned input, rendering, and role-access interfaces because those files are presentation glue. The cross-project Runtime session ports should instead use Contracts-owned DTOs/primitives such as `RuntimePoint`, `RuntimeRect`, and focused notification records.

Current shape:

```text
App input/rendering
  -> SadRogue Point/Rectangle

FortressRuntimeAccess
  -> maps to Contracts.Runtime primitives

Runtime session ports
  -> Contracts DTOs/primitives only

Runtime core
  -> maps back to internal SadRogue/world geometry where current implementation requires it
```

Do not "fix" a signature mismatch by importing `SadRogue.Primitives` into a public Runtime port. Add or extend a Contracts primitive/DTO, then keep the third-party geometry conversion at the App.Runtime or Runtime-internal edge.

The architecture smoke runner scans Contracts source and Runtime public
session port/factory files for SadRogue, SadConsole, MonoGame, and XNA type
names. If a public Runtime call needs geometry, color, or presentation metadata,
add a project-owned DTO in Contracts and map it at the App/Runtime adapter
boundary.

### Command targets and post-tick applicators must share the same diff log

When moving a direct mutation into an authoritative diff pipeline, wire one diff log instance through the command execution context and the post-tick pipeline. Creating one log for the command target and a second log for the applicator silently drops queued mutations.

Use the Runtime-owned `RuntimeMutationDiffLogs` bundle for command-side typed mutation logs. The active session should own that bundle through `RuntimeSessionServices`, alongside the scheduler, command queue, event bus, main diff log, and item diff log. Do not re-expand `SimulationRuntimeHost`, `SimulationRuntimeHostCore`, `SimulationCommandExecutionContext`, or `SimulationTickPipeline` into long parameter lists of individual mutation logs; that shape makes it too easy to accidentally give a command target and an applicator different instances.

Do not create a new `RuntimeMutationDiffLogs` bundle inside Runtime host composition when a session service bundle already exists. The host, command execution context, and post-tick pipeline must receive the same bundle, and `RuntimeSessionServices.ResetForNewSession()` must clear all typed command mutation logs together.

Current stockpile create/delete shape:

```text
CreateStockpileCommand
  -> IStockpileCommandTarget
  -> StockpileDiffLog.AddCreateZone(...)

DeleteStockpileCommand
  -> IStockpileCommandTarget
  -> StockpileDiffLog.AddDeleteZone(zoneId)

SimulationTickPipeline.PostTick
  -> StockpileDiffApplicator.ApplyAll(world, stockpileDiffs)
  -> StockpileDiffLog.Clear()
```

The command target can do preflight validation for user feedback and empty-command rejection, but the applicator must recheck authoritative conditions such as overlap because multiple commands in the same tick can pass preflight against the same old world state.

Applicators must also recheck any condition that earlier post-tick diffs can change. Stockpile creation now rechecks tile terrain after terrain diffs have applied; command preflight may have seen a floor that became a wall in the same tick.

Stockpile deletion follows the same rule: the command diff should carry the zone id, while `StockpileDiffApplicator` reads the current authoritative zone/member chunks at apply time. Do not capture a member-chunk snapshot in the command target; same-tick zone changes should be resolved by the applicator against the current world state.

The active stockpile applicator entry is world-level only:

```text
SimulationTickPipeline
  -> StockpileDiffLog.MergeAndSort()
  -> StockpileDiffApplicator.ApplyAll(world, stockpileDiffs)
```

Do not recreate a chunk-local `ApplyDiffs(Chunk, List<StockpileDiff>)` entry for create/delete. That old shape made it too easy to apply zone membership from stale chunk-local assumptions instead of resolving against the current authoritative world.

Typed command diff ordering is not one-size-fits-all. Order, zone, and workshop command-edit diffs intentionally preserve enqueue sequence because edits are order-sensitive. Item and stockpile diffs still use spatial/priority ordering. If this changes, update the explicit smoke coverage instead of silently normalizing every `GetSortKey()` implementation to the same policy.

SadRogue `Rectangle.MaxExtentX/MaxExtentY` is treated as inclusive in the simulation codebase. Rectangular world scans such as order/designation/item queries should iterate `<= MaxExtent*`, not `< MaxExtent*`; otherwise one-cell-high or one-cell-wide designations silently scan no cells.

Mining has the same inclusive-rectangle trap as hauling/item scans. Keep `MiningOrderRules` and `MiningSystem` cursor advancement paired: scan loops use `<= MaxExtent*`, and cursor rollover checks use `> MaxExtent*`. Add one-cell rectangle coverage when touching any designation scanner.

Runtime command ids must not use `Guid.NewGuid()` on replay-facing command paths. Command payloads should serialize meaningful mutation inputs, and Runtime session enqueue should add deterministic sequence identity when duplicate payloads can appear in the same tick. Execution order still belongs to `CommandQueue`; command ids are for stable correlation/debugging.

Command payload serialization is part of command identity. Every field that can change execution must be written to `Serialize()`, including optional filters and tag lists. When a field is semantically a set, serialize it in deterministic order so equivalent UI selection order does not create a different id; when the field is semantically ordered, preserve the order.

`CommandQueue.RestoreCommands(...)` is replay restore, not executed-history restore. Restored commands should be queued with deterministic sequence order and should not appear in `GetExecutedCommands()` until `ExecuteCommands(...)` actually runs them. Otherwise save/replay history gets duplicate entries and future commands look executed before their tick. Validate restored command batches before clearing the existing queue, so a corrupt restore payload cannot wipe pending commands and then fail halfway through.

Do not persist live `ICommand` instances. They are executable objects with runtime-only behavior and dependencies. Persist `CommandReplayRecord` data from `GetExecutedCommandRecords()` instead, then deserialize records through a command factory/registry when the real save/replay loader exists.

Keep replay decoding ownership aligned with command ownership. `ICommandReplayFactory` belongs in Core as a narrow contract, but concrete command type lookup and payload decoding belong in Runtime because Runtime owns command implementations, command type strings, and content-aware command construction. Do not make Core reference Runtime commands to "finish" replay deserialization.

Do not let the Runtime replay factory become a new command God Object. The
main factory should own command-type dispatch, payload version validation, and
shared primitive readers only. Put concrete decoders in focused partials by
command family, such as orders, zones/stockpiles, profession/workshop, and
debug spawn. New command families should add a matching decoder partial plus
architecture smoke coverage instead of expanding the main dispatch file with
domain parsing details.

The same rule now applies to zone commands:

```text
CreateZoneCommand / UpdateZoneCellsCommand / DeleteZoneCommand
  -> IZoneCommandTarget
  -> ZoneDiffLog

SimulationTickPipeline.PostTick
  -> ZoneDiffApplicator.ApplyAll(world, zoneDiffs)
  -> ZoneDiffLog.Clear()
```

Do not let `ZoneCommandTarget` regain a concrete `World` field just to call `world.Zones.*` from command execution. If zone behavior needs richer validation or ordering, add it to `ZoneDiffLog`/`ZoneDiffApplicator` so it remains an authoritative post-tick mutation.

And to workshop queue/settings commands:

```text
UpdateWorkshopQueueCommand
  -> IWorkshopQueueCommandTarget
  -> WorkshopDiffLog

SimulationTickPipeline.PostTick
  -> WorkshopDiffApplicator.ApplyAll(world, workshopDiffs, constructions)
  -> WorkshopDiffLog.Clear()
```

Workshop diffs must preserve command enqueue order because queue edits are order-sensitive. Keep `WorkshopDiff.GetSortKey()` sequence-based unless there is a deliberate operation-level merge policy.

And to order commands:

```text
CreateMiningOrderCommand / CreateHaulOrderCommand / CreateConstructionOrderCommand / ...
  -> IOrderCommandTarget
  -> OrderDiffLog

SimulationTickPipeline.PostTick
  -> OrderDiffApplicator.ApplyAll(world, orderDiffs)
  -> OrderDiffLog.Clear()
```

Do not let `OrderCommandTarget` regain a concrete `World` field just to call `world.Orders.Enqueue*` during command execution. Order diffs intentionally preserve enqueue order so multi-command placement batches remain deterministic.

Profession assignment commands follow the same post-tick rule even though they are Runtime-owned rather than Simulation-world-owned:

```text
SetProfessionWeightCommand
  -> IProfessionAssignmentCommandTarget
  -> ProfessionAssignmentDiffLog

SimulationTickPipeline.PostTick
  -> ProfessionAssignmentDiffLog.ApplyAll()
```

The profession handler binding still happens after Runtime systems are registered, but command execution must queue a diff instead of invoking the handler directly.

### Runtime snapshot builders should not become new god objects

Snapshot builders are allowed to know about live Runtime/Simulation internals, but each builder should own one read-model family or one mapping policy. Do not put navigation overlay modes, map terrain glyph rules, entity glyph rules, workshop queue summaries, construction-material progress, and aggregate frame composition into one ever-growing file just because they all return DTOs.

Current split pattern:

```text
FortressRuntimeSnapshotBuilder
  -> base/debug/catalog entrypoints
  -> frame/overlay aggregate composition
  -> map/navigation/inspection/placement queries
  -> Work/jobs/workforce/orders/workshop read models

MapViewportSnapshotBuilder
  -> viewport orchestration
  -> terrain glyph policy
  -> visible creature/item glyph policy

NavigationOverlaySnapshotBuilder
  -> basic mode overlays
  -> structural/flow/ramp mode overlays
  -> path-cell mapping
  -> grid/nav-data helpers

WorkshopSnapshotBuilder
  -> workshop scanning
  -> summary/queue mapping
  -> construction-material progress

ManagementDrawerSnapshotBuilder
  -> creatures/items/zones

StockpileSnapshotBuilder
  -> overlay/detail/hit-test/geometry

JobsDebugSnapshotBuilder
  -> active jobs/transport debug/stats

FortressRuntimeSessionSnapshotFacade
  -> frame/map/work/session access queries
```

When a snapshot needs new data, add it near the read-model family it belongs to. If the mapping starts to look like reusable domain policy rather than presentation/read-model policy, move that policy closer to Runtime/Simulation/Content instead of hiding it in a snapshot helper.

### Work drawer panels should consume one aggregate read model

The Work drawer needs jobs, workforce, order summaries, and workshop summaries in the same panel. Avoid letting each helper call a different runtime facade method from inside App UI renderers.

Current shape:

```text
Runtime snapshot builder
  -> SimulationWorkDrawerData

FortressUiOverlayRenderer
  -> fetches the aggregate when the Work drawer is open

UiWorkDrawerRenderer
  -> renders Work tabs from that DTO
```

Input paths that need a narrower read model, such as profession-weight clicks, should use a clearly named input DTO/provider instead of reaching for live systems or forcing the full drawer aggregate.

### Split App presentation by surface, not by domain ownership

SadConsole rendering helpers that take `ScreenSurface`, `ICellSurface`, `UiStore`, mouse/camera state, or UI service objects are App presentation code even when they draw simulation-derived facts. Do not move those helpers into Runtime, Jobs, Simulation, or Content just because they are large.

Current shape:

```text
Runtime snapshot DTOs
  -> App.Rendering frame/overlay coordinators
  -> App.Rendering/App.UI SadConsole glyph/panel renderers
```

Good split targets are presentation surfaces:

```text
FortressMapOverlayGlyphRenderer
FortressPlacementOverlayRenderer
UiChromeRenderer
UiManagementDrawerRenderer
UiDebugMenuRenderer
UiQuickMenuRenderer
UiWorkDrawerRenderer
UiWorkshopPanelRenderer
FortressDebugUnitOverlayRenderer
```

If a renderer needs more simulation information, add the data to a Runtime snapshot/query DTO first. Keep the App class focused on glyphs, layout, transient UI state, and command-preview visuals.

Avoid moving SadConsole renderers to Runtime, Jobs, Simulation, Content, or Contracts only because a file is large. Module ownership follows the dependency boundary: surface drawing that touches `ScreenSurface`, `ICellSurface`, `UiStore`, or App input state stays in App; simulation facts cross the boundary as Runtime DTOs.

For chrome controls, keep labels, keyboard shortcuts, and slot-to-drawer/menu mappings in `UiChromeSlots`, with geometry in `ButtonLayoutCalculator`. Do not reintroduce separate F-key or Z/X/C/V lookup arrays in renderers and input handlers.

When App UI files are still large after moving live simulation reads behind snapshots, split them by presentation surface or state domain inside App first. Partial files for `UiStore`, `UiManagementDrawerRenderer`, `UiWorkDrawerRenderer`, and `UiDebugMenuRenderer` are acceptable because they keep SadConsole/App dependencies local without pretending those UI concerns belong in Runtime, Simulation, Jobs, Content, or Contracts.

Input dispatchers should split by event channel or feature panel, not by lower-layer domain. Keep SadConsole component input, screen chrome hit testing, root quick-menu hit testing, submenu hit testing, Debug overlay click handling, Work allocation keyboard/mouse handling, and placement/menu hit testing in App input/UI partials. Do not move them to Runtime simply because they trigger Runtime commands; the command/query boundary is the facade call, not the mouse/keyboard routing code.

As App input files shrink, keep the same ownership rule: keyboard/mouse context records, root quick-menu hit testing, main-menu/world-map input, and SadConsole overlay pass-through logic remain App concerns. Split them by event channel or feature menu inside `HumanFortress.App.Input` / `HumanFortress.App.States`; do not move them to Contracts/Runtime just because they produce semantic Runtime requests.

When splitting SadConsole presentation or input partials, carry the exact extension-method imports with the moved code. Files that call `Print`, `SetGlyph`, `Keyboard`, `ScreenSurface`, or `Color` usually need `SadConsole`, `SadConsole.Input`, and/or `SadRogue.Primitives` locally. Do not rely on a sibling partial file's imports; C# using directives are file-scoped.

Runtime command targets should also split by operation role instead of accumulating helper logic in one target class. Keep command entrypoints, world-cell eligibility/collection, lookup/bootstrap of mutable runtime state, and display/name compatibility helpers in separate Runtime partial files. Do not move those helpers into App, and do not hide reusable domain policy inside App-side command factories.

Build, placement, and chrome presentation should follow the same App-local split rule. `BuildUI`, `FortressBuildKeyboardInput`, `NavigationOverlay`, `FortressPlacementOverlayRenderer`, `FortressPlacementController`, `UiChromeRenderer`, and UI command objects are App presentation/input orchestration. Split them by UI surface, event family, or command family; do not move SadConsole drawing, keyboard selection, or mouse placement routing into Runtime/Jobs/Simulation just because the final action queues a Runtime command.

Runtime composition files should split by system group, not by App caller. Planning systems and tick-facing job wrappers are both Runtime composition, but they should not live in one mixed "system groups" file once each group has enough constructor policy. Keep concrete factory wiring in Runtime and keep App limited to session/bootstrap adapters and callback injection.

### Contracts should define shapes, not runtime policy

Moving a request type to Contracts must not pull runtime defaults, content category mapping, or Simulation conversion policy with it. Contracts can own enums/records that express cross-boundary intent; Runtime should own how those intents become commands, material filters, content category keys, and Simulation DTOs.

Good shape:

```text
App UI intent
  -> Contracts.Runtime enum/record/request parameters
  -> Runtime command factory applies defaults/mapping/conversion
  -> Simulation command payload
```

If App needs to pass material preferences or UI tool options, pass the raw semantic values through the runtime facade and build the concrete filter inside Runtime. Do not put default material ids, category keys, or Simulation-facing conversion helpers in Contracts just because both App and Runtime can reference them.

### Generated-world UI should use Contracts DTOs through Session queries

World generation screens should use the contract `IWorldGenerationService` created through Runtime's `FortressRuntimeWorldGenerationFactory`. App screen/session code should not directly construct or store concrete WorldGen service/data/factory types, and App should not reference the `HumanFortress.WorldGen` project.

Later App screens should read through `FortressSessionContext.TryGetWorldSize(...)`, `TryGetWorldTileView(...)`, or bootstrap-only `WorldTileSnapshot` queries instead of reading `WorldGenResult.Tiles`, raw `WorldTile`, `BiomeType`, or `WorldParams`.

Keep stable generated-world shapes in `HumanFortress.Contracts.WorldGen` (`WorldGenerationSettings`, `WorldMapTileView`, `WorldTileSnapshot`). Keep SadConsole glyph/color policy in App.Rendering because it depends on presentation choices. Keep fortress-session selection, embark configuration, and Runtime request mapping in App.Session because those are App flow concerns.

### Runtime access facades should narrow by caller role

Do not pass the full concrete `FortressRuntimeAccess` into every App helper just because it is convenient. Rendering should use a read-only interface, play/input controllers should use a play-time query/command interface, and session initialization should be the only path that can see startup-only operations such as fortress-map generation/fill or auto-dig bootstrap.

Current split:

```text
IFortressRuntimeReadAccess
  -> wrapped by App.Rendering FortressViewRuntimePorts
IFortressRuntimeUiInputAccess
  -> wrapped by App.Rendering FortressViewRuntimePorts for UI callbacks
IFortressRuntimeBuildCatalogAccess
  -> wrapped by App.Input keyboard ports
IFortressRuntimePlacementQueryAccess
IFortressRuntimePlacementCommandAccess
  -> wrapped by App.Input FortressPlacementRuntimePorts
IFortressRuntimeMapInspectionAccess
  -> wrapped by App.Input map inspection ports
IFortressRuntimeDebugSpawnQueryAccess
IFortressRuntimeDebugSpawnCommandAccess
  -> wrapped by App.Input debug-spawn ports
IFortressRuntimeWorkshopPanelQueryAccess
IFortressRuntimeWorkshopPanelCommandAccess
  -> wrapped by App.Input workshop-panel ports
IFortressRuntimeNavigationDebugAccess
  -> wrapped by App.Input navigation-debug ports
IFortressRuntimeSimulationControlAccess
  -> wrapped by App.Input simulation-control ports
IFortressRuntimeBootstrapAccess
  -> wrapped by App.Session FortressSessionRuntimePorts
FortressStateRuntimePorts
  -> state-level bundle of module-owned view/input/session port groups only
```

For fortress play composition, use a named ports object such as `FortressStateRuntimePorts` at the state boundary, then pass module-owned port groups into input/render/session helpers. When a port constructor needs several runtime roles, use a named dependency object such as `FortressInputRuntimePortDependencies`; avoid long positional runs of the same `runtime` adapter. Do not recreate a broad keyboard/runtime facade that inherits unrelated workshop, navigation-debug, simulation-control, and build-catalog capabilities just because the keyboard router touches all four.

Likewise, avoid putting active runtime session ownership back into `GameStateManager`. Keep `SimulationRuntimeSession`, live `World`, content snapshot handling, navigation rebuild, fortress generation/fill, and startup auto-dig policy in Runtime/session-controller boundaries. App.Runtime may adapt logger/UI callbacks and forward request DTOs, but it should not grow new gameplay/domain logic.

Runtime snapshot builder/facade helpers are implementation details. Public boundaries should be snapshot DTOs plus Runtime session port interfaces/App.Runtime facade query methods, not direct calls from App into `FortressRuntimeSnapshotBuilder`, `FortressRuntimeSessionSnapshotFacade`, or the internal `FortressRuntimeSessionCore`.

Internal Runtime implementation classes should not keep ordinary `public` members just because they implement a public or friend-visible interface. Prefer explicit interface implementations for concrete commands, command targets, job-system wrappers, navigation-source adapters, command contexts, and small catalog adapters. Keep Runtime helper/factory/builder methods `internal` unless App or another project is meant to call that exact helper as a supported API. This keeps source scans aligned with the intended boundary and prevents internal implementation types from looking like stable extension points.

Keep App project references aligned with source ownership. If App source no
longer uses a module namespace directly, remove the direct ProjectReference
instead of keeping it as a convenience bridge. App currently should not directly
reference Jobs, Simulation, or Navigation; it should reach those systems through
Runtime/WorldGen DTOs, queries, or commands.

Command translation is a Runtime boundary, not an App-to-Simulation shortcut.
App may map App UI enums/options to stable Runtime request DTOs, but it should
not construct Runtime command objects, call `Runtime.Commands` factories, or
pass `Func<ulong, ICommand>` delegates. Semantic queue methods such as
`QueueHaulOrder(...)`, `QueueCreatureSpawn(...)`, and
`QueueAddWorkshopRecipe(...)` should cross the App facade; concrete command
construction belongs in Runtime session port implementations/Runtime command code.

Runtime concrete command classes, command factories, command target interfaces,
and `SimulationRuntimeCommandTargets` are implementation details. Keep them
internal and let tests use the Runtime friend bridge when they need direct
command-stage coverage. Do not make them public again just to simplify App
input code; add a semantic Runtime session port method or request DTO
instead.

Commands should also depend on the narrowest runtime target context they need.
Use `IRuntimeOrderCommandTargetContext`, `IRuntimeZoneCommandTargetContext`,
`IRuntimeWorkshopCommandTargetContext`, or the matching spawn/profession/
stockpile role instead of reintroducing a single all-target command context.
The tick pipeline should pass only `ISimulationContext` plus the clock role into
the command stage; individual commands should use `RuntimeCommandContext.Require<T>()`
for their specific role so a missing runtime role fails visibly instead of
silently no-oping.

Runtime session construction options and `FortressRuntimeSessionCore` are Runtime-internal helpers. App should create sessions through `FortressRuntimeSessionFactory`, keep App-specific logger/content callbacks at the `GameStateRuntimeCoordinator` composition boundary, store only `IFortressRuntimeSessionPorts`, then hand the rest of App only narrow `FortressRuntimeAccess` role interfaces.

### Workshop input should read snapshot DTOs and write commands

The workshop panel needs queue entry ids and current worker-slot state to enqueue `UpdateWorkshopQueueCommand`, but App input code should not resolve `WorkshopState` by scanning live placeables.

Current shape:

```text
Runtime snapshot builder
  -> WorkshopSummaryView / WorkshopQueueEntryView.EntryId

FortressWorkshopPanelKeyboardInput
  -> reads DTO state through FortressRuntimeAccess.GetWorkshopPanelData(...)
  -> enqueues UpdateWorkshopQueueCommand
```

Do not recreate an App-side `World.GetAllChunks()` / `PlaceableInstance.Workshop` resolver for panel input. If more mutable workshop operations are added, expose the read side as DTO fields and keep writes command-driven.

## Content Boundary Pitfalls

### Prefer one Content load result over split loaders

`HumanFortress.Content` now coordinates runtime content bootstrap plus data/core catalog loading. Runtime should consume the Content-owned entry points instead of independently calling:

```text
ItemDefinitionCatalogLoader.Load(...)
CreatureDefinitionCatalogLoader.Load(...)
CoreDataRegistryLoader.Load(...)
RuntimeContentRegistryLoader.Load(...)
```

Current first-pass shape:

```text
HumanFortress.Content
  resolves published/source content paths
  resolves registry files under content/registries
  loads legacy + structured runtime registries through FortressContentLoader / RuntimeContentRegistryLoader
  loads item/creature definitions
  loads construction/recipe core data
  returns immutable catalog snapshots and diagnostics

Runtime composition
  applies snapshots to world managers
  captures runtime catalog/tuning dependencies through FortressRuntimeContentSnapshotLoader
  exposes construction/recipe/material/terrain/geology/zone facts through FortressRuntimeContentSnapshot contract properties

App
  consumes Contracts-owned content load reports and file-resolution DTOs through Runtime/App wrappers
```

Do not introduce new App-local JSON traversal or a second content bootstrapper while this is being consolidated.

Single-purpose Content loaders and parsing helpers are implementation details. Cross-App public entry points should stay centered on Runtime facades and Contracts reports:

```text
Runtime.FortressRuntimeContentLoader
Contracts.Content.Loading.FortressContentLoadReport
```

`FortressContentLoader`, `CoreContentCatalogLoader`, `FortressRuntimeContentSnapshotLoader`,
`ProfessionRegistryLoader`, item/creature catalog result types, `RuntimeContentRegistryLoader`,
and material/registry parser helpers are internal/friend surfaces for Content, Runtime,
and tests. Contracts-owned `FortressContentLoadReport` should expose issues and summary
counts rather than full mutable or runtime catalog objects.

Concrete Content registry helpers are also implementation details. External code should depend on Contracts catalog interfaces surfaced by Content loader/snapshot facades, such as `IRuntimeMaterialCatalog`, `IRuntimeTerrainKindCatalog`, `IRuntimeGeologyCatalog`, `IConstructionCatalog`, `IRecipeCatalog`, and `IProfessionRegistry`, not on `ContentRegistry`, `MaterialRegistry`, `TerrainKindRegistry`, `GeologyRegistry`, `BiomeTemplateRegistry`, `AliasResolver`, or the concrete profession registry implementation.

### Resolve App registry files through Runtime/App wrappers

App-side convenience registries still exist for UI/input/profession presentation, but they should not hard-code the published output layout. Use:

```csharp
AppContentFileLocator.ResolveRegistryFile(baseDir, "some.registry.json")
```

instead of:

```csharp
Path.Combine(baseDir, "content", "registries", "some.registry.json")
```

`AppContentFileLocator` delegates to Runtime's content file-location facade, and Runtime delegates to the internal Content loader. This keeps published builds and source-checkout runs on the same path resolution rules without giving ordinary App UI/input code a `HumanFortress.Content` project dependency. Current App-side migrated call sites include input bindings, order display names, and workshop category mapping.

### Keep data/core JSON traversal out of App

Construction/workshop and recipe loading now enters through the Content-owned core catalog loader:

```csharp
CoreContentCatalogLoader.Load(dataCorePath)
```

Do not reintroduce App-local parsing for:

```text
data/core/workshops/core_workshop_*.json
data/core/placeable/workshops.json
data/core/recipes/*.json
```

Runtime's `SimulationWorldContentLoader` should not locate the runtime content directory itself or call App logging directly. It now calls `FortressContentLoader` and receives logging/content-issue callbacks from App; schema compatibility plus registry population belong behind the Content/structured registry boundary.

Important compatibility behavior preserved by the Content-owned core-data loader:

- new workshop files and legacy `placeable/workshops.json` are both loaded;
- duplicate construction ids are skipped and counted instead of failing startup;
- recipe files may be root arrays or `{ "recipes": [...] }` documents;
- legacy recipe aliases such as `workshop_id`, `workshop`, `duration_ticks`, and `primary_skill` still parse.

Keep `CoreDataRegistryLoader` split by catalog family while preserving those
compatibility rules. The main loader should only orchestrate `data/core`
directories; construction/workshop JSON, recipe JSON, and shared JSON helpers
belong in separate `HumanFortress.Content.Definitions` partials. Do not solve a
new construction or recipe edge case by growing one mixed data/core parser file
again, and do not move the parsing into Runtime/App just because the load path
starts during session bootstrap.

### Construction and recipe catalogs should be snapshots, not singletons

`ConstructionRegistry` and `RecipeRegistry` singleton compatibility classes have been deleted. `CoreDataRegistryLoader.Load(...)` parses core data into fresh immutable snapshots, and the internal structured registry applies those snapshots behind `FortressRuntimeContentSnapshotLoader`.

Runtime/gameplay reads should use the read-only catalog surface from the Content runtime snapshot or explicit constructor-injected interfaces:

```csharp
FortressRuntimeContentSnapshot.Constructions.GetConstruction(id)
FortressRuntimeContentSnapshot.Recipes.GetRecipe(id)
```

For long-lived systems and Jobs-owned code, prefer constructor-injected interfaces:

```csharp
IConstructionCatalog
IRecipeCatalog
```

Do not add new `ConstructionRegistry.Instance.Get...`, `RecipeRegistry.Instance.Get...`, or external `ContentRegistry.Instance` reads in runtime systems. The normal read path is now:

```text
FortressContentLoader
  -> internal CoreContentCatalogLoader / FortressRuntimeContentSnapshotLoader
  -> FortressRuntimeContentSnapshot.Constructions / Recipes
```

Those properties expose immutable snapshots through read-only interfaces. Keep it that way; do not recreate the old singleton classes or public registry singleton for convenience.

Preferred direction:

```text
ContentRegistry
  owns load/validation/indexing
  swaps immutable construction/recipe catalog snapshots
  exposes read-only construction/recipe catalog interfaces

Runtime/App
  request definitions through catalog interfaces
  do not parse content files directly
```

Do not move Jobs-owned code back to `RecipeRegistry.Instance`. Craft already uses `ICraftRecipeCatalog`; future construction/craft/runtime seams should follow that pattern.

Keep the structured `ContentRegistry` split by responsibility. Load
orchestration and query/snapshot compatibility can stay in the main partial,
but JSON parser families, deterministic geology handle/index construction,
tuning/zone/alias loading, validation, and content hash generation should live
in focused `ContentRegistry.*.cs` partials under
`HumanFortress.Content.Registry`. Recombining those helpers into the main file
makes Content look like a singleton service locator again and hides which data
family owns a compatibility rule.

Runtime command targets should not keep "helpful" global fallbacks. `SimulationRuntimeContext` now requires explicit `IRecipeCatalog` and `IConstructionCatalog` dependencies for workshop queue commands. If a test or tool needs a context, pass `RecipeCatalogStore.Empty`, `ConstructionCatalogStore.Empty`, or a small in-memory catalog explicitly.

App UI helpers should also consume the active runtime session catalog facade instead of reaching for the global structured registry. Current migrated paths include:

- workshop panel keyboard default recipe lookup;
- workshop panel context resolution;
- map-click workshop detection;
- build-menu workshop category selection;
- workshop category mapping;
- workshop overlays and workshop panel title/footprint lookup.

Runtime geology display/application reads follow the same rule. Use the active `IRuntimeGeologyCatalog` from the runtime session facade for map rendering, tile popups, and terrain diff application. Do not add new direct geology lookups from `ContentRegistry.Instance` inside Runtime, Jobs, Simulation diff application, or App rendering helpers.

Construction planning/execution also uses explicit dependencies now:

```text
ConstructionSystem
  -> IConstructionTerrainMaterialResolver
  -> ConstructionTuning

ConstructionJobExecutor
  -> ConstructionTuning
  -> PlaceableTuning
```

Runtime/Content composition may bridge those calls inside the Content snapshot loader, but Simulation/JOBS-owned construction logic should not call `ConstructionTuning.LoadFromContent()` or `ContentRegistry.Instance` directly.

Runtime tuning objects should enter through the Content-owned runtime snapshot:

```text
FortressRuntimeContentSnapshot
  -> materials / terrain kinds
  -> constructions / recipes
  -> runtime geology catalog
  -> zone definitions
  -> ConstructionTuning.LoadFromJson(...)
  -> mining tuning JSON for MiningDropResolver
  -> NavigationTuning.LoadFromJson(...)
  -> PlaceableTuning.LoadFromJson(...)
  -> SchedulerTunings.LoadFromJson(...)
  -> WorkshopTunings.LoadFromJson(...)
```

Do not add new `LoadFromContent(...)` or `LoadFromRegistry(...)` convenience paths for runtime tuning. Those helpers tend to recreate hidden global-content reads and make hot reload/content reload behavior inconsistent.

Structured core-data application should also stay behind the Content-owned snapshot loader:

```text
FortressRuntimeContentSnapshotLoader.ApplyCoreData(...)
  -> internal structured registry applies core-data snapshots
  -> CaptureLoaded()
  -> returns materials / terrain / constructions / recipes / geology / zones / tuning JSON
```

Runtime's `SimulationWorldContentLoader` may inject snapshots into the active `World`, but it should not call `ContentRegistry.Instance.ApplyCoreData(...)` or read `ContentRegistry.Instance.Zones` directly. Use the returned `FortressRuntimeContentSnapshot.ZoneDefinitions` when registering zone definitions with the simulation world, and keep App-specific logging behind callbacks.

App adapter seams should also consume the active runtime snapshot instead of the singleton registry:

```text
MiningDropResolver
  -> IRuntimeGeologyCatalog
  -> tuning.mining JSON

ConstructionTerrainMaterialResolver
  -> IRuntimeGeologyCatalog
```

Do not reintroduce direct `ContentRegistry.Instance` reads in these adapters just because they live in App. They are composition edges and must reflect the same active-session snapshot as Runtime/JOBS-owned systems.

WorldGen follows the same rule. Fortress generation should receive explicit content:

```text
FortressGenerationContent
  -> IRuntimeGeologyCatalog
  -> tuning.mapgen JSON
  -> tuning.ore JSON
  -> tuning.cavern JSON

FortressGenerator / FortressMap / FortressChunk
  -> consume FortressGenerationContent or its geology catalog
  -> never read ContentRegistry.Instance directly
```

Keep `FortressGenerator` split by generation phase. The main generator should
show the ordered pipeline, while cavern carving, strata/surface filling, ore
placement, and tuning JSON helpers live in focused WorldGen implementation
partials. Do not move those helpers into Runtime just because Runtime starts
fortress generation, and do not let a new mapgen tuning feature turn the main
generator file back into a large mixed policy object.

`FortressRuntimeSessionCore` caches the active `FortressRuntimeContentSnapshot` returned by session content loading. Reuse that snapshot for navigation tuning, runtime dependency composition, and fortress generation. Runtime owns the `FortressGenerationContent` adaptation and the generate+fill operation; App should pass a primitive `RuntimeFortressGenerationRequest` from session/embark data rather than adapting content snapshots, holding `FortressMap`, or calling a separate world-fill operation. `FortressSessionInitializer`, `GameStateManager`, and WorldGen must not recapture the global registry just because the registry is already loaded.

Repeated core-data loads must replace construction/recipe snapshots, not append to mutable indexes. The current smoke tests verify construction count, recipe count, construction category queries, and workshop recipe queries stay stable after reload.

### Do not depend on full managers for static definitions

`ItemManager` and `CreatureManager` still expose definition lookup alongside runtime instances, but they no longer parse content files. Static definition storage is supplied as immutable catalog snapshots from the Content boundary.

When a system only needs definition metadata, use the read-only catalog seams:

```csharp
IItemDefinitionCatalog
ICreatureDefinitionCatalog
```

Examples already migrated:

- construction material matching in `ConstructionMaterialTracker`;
- material source planning in `ConstructionMaterialsPlanner`;
- profession roster display-name lookup in `ProfessionAssignments`.

Do not add new gameplay systems that call `world.Items.GetDefinition(...)` or `world.Creatures.GetDefinition(...)` just because a `World` is nearby. Prefer explicit catalog injection. App UI/render/debug code may still read from managers for presentation until those surfaces get their own view-model/catalog cleanup.

### Rebuild definition indexes through fresh snapshots

Repeated content loads must be idempotent. `HumanFortress.Content.Definitions` loaders parse files into fresh immutable catalog snapshots, and managers replace their current snapshot through `SetDefinitionCatalog(...)`.

Keep static item definition loading split by responsibility while preserving
that idempotence. `ItemDefinitionCatalogLoader` should keep deterministic file
enumeration and JSON options in the main partial, legacy `{ "items": [...] }`
furniture parsing plus stack/placeable/effects mapping in the parsing partial,
and normalization/name enrichment/validation in the validation partial. Do not
move parsing helpers into `ItemManager` just because Simulation stores item
instances, and do not add Runtime startup shortcuts that parse item JSON
outside the Content loader.

Do not append to existing indexes during reload. This creates hidden duplication bugs where:

```text
DefinitionCount remains stable
GetByKind(...) / GetByTag(...) grows after every reload
```

That kind of bug is easy to miss because normal startup loads once, while tests, hot reload, content validation tools, and future mod reload flows may load repeatedly.

Current direction:

```text
loader
  parses and validates JSON into a fresh result

catalog snapshot
  builds definition/id/tag/kind indexes from the fresh result

manager
  swaps to the fresh catalog snapshot
  does not parse files or incrementally append stale static definitions
```

Do not reintroduce file IO or JSON validation into `ItemManager` or `CreatureManager`. App/runtime composition should load content snapshots and inject them.

Stockpile filtering should stay on explicit content/runtime/simulation seams. Preset JSON is loaded by Content into `StockpilePresetDefinition` contracts, Runtime maps those presets into Simulation `StockpileFilter` values, and Simulation projects `ItemInstance + ItemDefinition` into `ItemStackRef` for filter matching. Do not wire stockpile filters back to the legacy `HumanFortress.Core.Content.ContentRegistry`, and do not parse stockpile preset JSON in App or Runtime command targets.

### Move contracts by assembly before rewriting namespaces

Navigation has completed the follow-up namespace cleanup: shared navigation contracts now compile from `HumanFortress.Contracts` under `HumanFortress.Contracts.Navigation`. Use it as the example for later compatibility cleanup batches: move assembly ownership first, then rewrite namespaces once the dependency graph is stable.

Static item/creature definitions now follow the same pattern. These types compile from `HumanFortress.Contracts`:

```text
ItemDefinition
CreatureDefinition
IItemDefinitionCatalog
ICreatureDefinitionCatalog
ItemDefinitionCatalogStore
CreatureDefinitionCatalogStore
PlaceableProfile
Footprint
PassabilityMode
EffectsBlock
```

Do not move them back into Simulation/Core just because their namespace still looks old. Those namespaces are intentionally transitional until the owning modules are stable enough for a focused cleanup pass.

Preferred direction:

```text
first pass
  move assembly ownership to Contracts
  preserve namespaces
  keep builds/tests stable

later cleanup
  rename namespaces once content/runtime ownership is stable
```

Avoid making `HumanFortress.Contracts` depend on `HumanFortress.Core` or `HumanFortress.Simulation`. If a shared DTO needs a Core type, the shared type should move down into Contracts with it.

## Build and SDK Pitfalls

### Use the .NET 8 executable explicitly on macOS

On this machine, the stable build path is:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet
```

Do not assume plain `dotnet` points to the right SDK/runtime. We saw a mismatch where the default CLI used newer .NET tooling while the app targeted `net8.0`, and the runtime failed until .NET 8 was correctly available.

Recommended build command:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:quiet -p:RunAnalyzers=false
```

### Do not parallel-build overlapping project graphs

Avoid running App build, Content build, and test-project build at the same time. Overlapping project graphs can touch the same `obj` files, including:

```text
src/HumanFortress.Content/obj/Debug/net8.0/HumanFortress.Content.dll
src/HumanFortress.Content/obj/Debug/net8.0/ref/HumanFortress.Content.dll
src/HumanFortress.App/obj/Debug/net8.0/apphost
```

On macOS this caused file-lock/copy/signing races:

- `HumanFortress.Content.dll` could not be opened for writing
- `ref/HumanFortress.Content.dll` could not be copied
- `apphost` not found during copy
- `apphost: is already signed`

Use sequential build/test commands.

When running a built DLL with `dotnet exec`, do not add the `dotnet run`-style argument separator:

```bash
# Correct
/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors

# Wrong: passes a literal "--" to the app and may skip init-only parsing
/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll -- --init-only --strict-content --content-warnings-as-errors
```

### Audit apparent hangs before assuming build is still running

When the Codex turn appears to be waiting for many minutes, first determine whether a backend command is actually still running. We have seen apparent long waits where no build/game process existed; only VS Code's Roslyn language server was alive. That means the delay was a session/front-end wait or an interrupted turn, not a `.NET` compile.

Use a bracketed `pgrep` pattern so the search command does not match itself:

```bash
pgrep -fl "[d]otnet|[H]umanFortress|[M]SBuild|[V]BCSCompiler"
```

Interpretation:

- only `Microsoft.CodeAnalysis.LanguageServer` / Roslyn: normal editor background process, not a stuck game/build;
- `dotnet build`, `MSBuild`, or `VBCSCompiler` lasting longer than expected: investigate build output, then stop/retry sequentially if needed;
- `dotnet exec ... HumanFortress.App.dll` without `--init-only`: likely a normal game loop, not a test/init command.

In the managed sandbox, broad `ps` may fail with `operation not permitted`; prefer `pgrep -fl` for this check.

Operational rule: if a build/run command has no output for about 30 seconds, check the process list and report the state instead of waiting indefinitely.

Important agent limitation: Codex does not have an independent wall-clock timer that wakes it up while a tool call is still pending. The "30 seconds" rule only works if commands are launched with short wait windows and the agent regains control at the tool boundary. Do not rely on the agent to notice elapsed time while a long-running tool call is hung at the session/front-end layer.

Preferred mitigation:

- split verification into short, sequential commands instead of one long command chain;
- prefer build/test commands that exit on their own;
- avoid interactive or normal game-loop commands unless explicitly testing the UI;
- if a command returns no output in the first wait window, immediately run the `pgrep` audit before continuing;
- when doing broad mechanical refactors, batch several source edits first, then run one bounded verification pass instead of compiling after every tiny edit.

For very large refactors, it is acceptable to ask the human to run the full local compile manually while the agent continues reading/designing. The agent should still run lightweight checks it can finish reliably, such as `rg` scans and `git diff --check`.

### Avoid `dotnet run` for the test script

`dotnet run --project tests/...` can silently spend time in build/analyzer paths before producing output. This looked like a hang.

Current `RunTests.sh` avoids that by doing:

1. explicit build with analyzers disabled;
2. direct DLL execution.

Current test command:

```bash
./RunTests.sh
```

### Analyzer runs are separate hygiene work

During feature/refactor verification, use:

```bash
-p:RunAnalyzers=false
```

Historical analyzer warnings and errors still exist. They should be fixed in a dedicated build-hygiene pass, not mixed into gameplay/system refactors.

## Test Architecture Pitfalls

### App `--test` and `--validate` are compatibility pointers

The preferred test entry is now:

```bash
./RunTests.sh
```

`HumanFortress.App --test` and `HumanFortress.App --validate` no longer host tests. They print compatibility messages pointing to the formal test runner:

```text
tests/HumanFortress.App.Tests
```

The legacy App `PhaseTests` harness now lives in `tests/HumanFortress.App.Tests`, and `./RunTests.sh` runs it after the focused regression/smoke batches.

### `InternalsVisibleTo` is temporary

`HumanFortress.App.Tests` currently uses:

```csharp
InternalsVisibleTo("HumanFortress.App.Tests")
```

This is acceptable while job systems still live in App. It should disappear as systems move into `HumanFortress.Jobs`, `HumanFortress.Simulation`, or focused testable assemblies.

### Avoid duplicating migrated regression tests

When a regression moves from `src/HumanFortress.App/TestRunner.cs` into `tests/`, remove the old App copy. Otherwise we get duplicate runtime, duplicate logs, and unclear ownership.

First migrated batch:

- transport finalizer reservation cleanup;
- transport no-path rollback;
- transport invalid-destination rollback;
- transport moved pickup target replan;
- transport active-slot backlog preservation;
- construction terrain completion cleanup;
- craft missing-input queue preservation;
- craft workshop input-ring consumption.

Second migrated batch:

- mining channel reservation full-footprint cleanup;
- item consumption diffs;
- split-stack pre-simulation diffs;
- item move relocation and stack merge;
- carry/un-carry diff merge behavior.

Final migrated batches:

- tick scheduler smoke checks;
- deterministic RNG and runtime ID checks;
- diff target encoding smoke checks;
- world/chunk and reservation smoke checks;
- command queue ordering/clear behavior;
- runtime command stage execution before system `ReadTick`.
- Phase A-D validation coverage for platform, world generation, fortress bootstrap, and navigation.

### Do not let timing budgets make deterministic tests flaky

The legacy Phase D concurrent pathfinder test originally used the production `NavigationTuning.Default` budget:

```text
MaxMsPerTickPathing = 3
```

`PathService.Solve` is allowed to return `Path.Invalid` and queue work for a later tick when that per-tick budget is exceeded. Under slower thread scheduling, the concurrent test could report:

```text
Only 8/10 paths were found
```

That was not proof of an unreachable path; it was normal budget deferral being treated as a failure. Tests that assert pathfinding correctness should use a test-specific tuning budget large enough to avoid scheduler noise. Separate tests should cover production budget/queue behavior explicitly.

## Runtime and Diagnostics Pitfalls

### Command execution belongs at the pre-read tick boundary

Do not call `CommandQueue.ExecuteCommands` from UI, game states, screen update code, or render-thread services. UI/App code should enqueue commands only.

Current runtime path:

```text
TickScheduler.PreTick
  -> Runtime-owned SimulationTickPipeline
  -> Runtime-owned SimulationCommandStage.Execute
  -> CommandQueue.ExecuteCommands
  -> system ReadTick
```

The regression coverage now proves a due command sees the real `SimulationRuntimeContext.CurrentTick` and is visible to systems during the same tick's `ReadTick`.

Profession allocation changes also go through this path now:

```text
UI work allocation input
  -> GameStateManager.EnqueueCurrentTickCommand
  -> SetProfessionWeightCommand
  -> IProfessionAssignmentCommandTarget
  -> ProfessionAssignments.SetWeight
```

`IProfessionAssignmentCommandTarget` is now a Runtime-owned seam. Runtime keeps `HumanFortress.Core.Commands.ISimulationContext` free of job-system details, while App composition supplies a weight-write callback from the Jobs-owned `ProfessionAssignments` instance during host composition.

Debug item spawning also goes through this path now:

```text
SpawnItemCommand
  -> IItemSpawnCommandTarget
  -> ItemsDiffLog.Add(AddItem)
  -> ItemsDiffApplicator.ApplyAdditions
```

Debug creature spawning also goes through this path now:

```text
SpawnCreatureCommand
  -> ICreatureSpawnCommandTarget
  -> CreaturesDiffLog.AddSpawnCreature
  -> CreaturesDiffApplicator.ApplyAll
```

The creature diff path is intentionally narrow: it supports spawn-only command migration and should not be treated as a complete creature mutation system yet.

Core order commands also no longer cast `context.World` to the concrete `World` type, and they should not directly mutate `OrdersManager`:

```text
CreateMiningOrderCommand / CreateAdvancedMiningOrderCommand / CreateHaulOrderCommand
CreateConstructionOrderCommand / CreateBuildableConstructionOrderCommand
  -> IOrderCommandTarget
  -> OrderDiffLog
  -> OrderDiffApplicator.ApplyAll
```

Runtime owns command target routing; Simulation owns the typed order diff applicator.

Zone commands also no longer cast `context.World` to `World`:

```text
CreateZoneCommand / UpdateZoneCellsCommand / DeleteZoneCommand
  -> IZoneCommandTarget
  -> ZoneDiffLog
  -> ZoneDiffApplicator.ApplyAll
```

Use `ZoneCoordinator` rather than calling `ZoneManager` directly. Deleting a zone must remove chunk shards as well as the global zone instance; otherwise stale per-chunk zone data remains behind.

Workshop queue commands also no longer cast `context.World` to `World`:

```text
UpdateWorkshopQueueCommand
  -> IWorkshopQueueCommandTarget
  -> WorkshopDiffLog
  -> WorkshopDiffApplicator.ApplyAll
```

Keep recipe lookup and placeable lookup out of the command implementation. The command should dispatch the requested operation only; Runtime owns validation/context routing and Simulation applies worker-slot/automation/queue mutations at the post-tick boundary.

Stockpile creation/deletion commands also no longer cast `context.World` to `World`:

```text
CreateStockpileCommand / DeleteStockpileCommand
  -> IStockpileCommandTarget
  -> StockpileDiffLog
  -> StockpileDiffApplicator.ApplyAll
```

Stockpile create/delete now use the active typed diff path, and create diffs carry preset-derived filter/priority data. Stockpile filter matching uses item projection and is covered by smoke tests. Stockpile item-index updates for transport pickup/delivery/cancel and construction/craft full-stack consumption now enter through Jobs-owned emitters that queue `StockpileDiffLog` operations. Those item-index diffs must carry projected `ItemStackRef` payloads so `RemoveItem` can clear tag indexes after `ItemsDiffApplicator` has already deleted the item.

Stockpile slot reservations are also a typed diff concern. `HaulingSystem` should track same-tick planned reservations before choosing more stockpile destinations and should queue `ReserveSlot` beside `TransportRequest` enqueueing. Do not call `ChunkStockpileData.TryReserveSlot(...)` from planners or App code. Transport failure/cancel paths release through `StockpileDiffLog.AddReleaseSlot(...)`.

Stockpile reservations must follow accepted transport requests. `ITransportIntake.Enqueue(...)` reports whether a new pending transport entry was added; stockpile planners should only write `ReserveSlot` after a true return. `TransportRequestQueue` owns duplicate pending-item protection, so a merged or rejected duplicate request must not get a second stockpile slot reservation.

Only `TransportReason.ToStockpile` should short-circuit pickup when an item is already in a stockpile. Construction, craft, refuel, and other non-stockpile jobs must be able to pick up stockpiled items and emit stockpile remove-index diffs, otherwise stockpiles become a dead end for the broader jobs pipeline.

The richer workshop and stockpile target behavior now lives in Runtime-owned helper classes rather than directly in `SimulationRuntimeContext`. Item spawning, creature spawning, order enqueueing, and zone mutation also now live behind Runtime-owned target helpers.

`SimulationRuntimeContext` itself is Runtime-owned and should stay a runtime command clock/target context, not a dumping ground for every target interface. Commands should resolve mutations through the target-context role; do not reintroduce `context is IOrderCommandTarget` / `context is IZoneCommandTarget` style casts or make the context implement every target interface again.

Profession assignment updates now queue through `ProfessionAssignmentDiffLog` and apply after the typed Simulation diffs. Runtime still stores the bound handler supplied by concrete system composition, but command execution should not call the handler directly.

Do not reintroduce stockpile-local haul-job diffs. `CreateZone` and `DeleteZone` are authoritative active-path operations, and item placement/removal/release paths are now integrated for Transport plus construction/craft consumption. The legacy broker `CreateHaulJob` diff was removed because reserving a slot without enqueueing a `TransportRequest` leaks capacity. New stockpile hauling behavior should enter through Jobs/Transport planning and queue stockpile reservation/index diffs beside transport requests.

### Do not let GameStateManager recreate the runtime graph by hand

`GameStateManager` previously stored separate `World`, `NavigationManager`, and `SimulationRuntimeHost` fields and assembled them directly inside `InitializeWorld`. That made it both a state machine and an implicit composition root.

Current first pass:

```text
GameStateManager
  -> Runtime-owned SimulationRuntimeSessionFactory<SimulationRuntimeHost<SimulationRuntimeSystems>>.CreateNew(...)
  -> App content-loading callback
  -> Runtime-owned FortressRuntimeHostFactory through an App callback that supplies logging/content inputs
  -> Runtime-owned SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>>(World, Navigation, Host)
  -> Runtime-owned generic SimulationRuntimeHost<TSystems>
  -> Runtime-owned SimulationRuntimeHostCore
```

Keep future runtime construction behind this seam. UI/state code should not directly reset schedulers, clear command queues, create navigation managers, or new up runtime hosts. Content loading and concrete system construction are still App callbacks because they depend on current content registries, job adapters, logger callbacks, UI hooks, and SadConsole-facing lifetime.

`SimulationRuntimeHostCore` owns scheduler restart, tick-system registration, pipeline attachment, and stop-time pipeline detachment. `SimulationRuntimeHost<TSystems>` owns the generic lifecycle shell. `SimulationRuntimeSystems`, `FortressRuntimeDependencies`, `FortressRuntimePlanningSystems`, `FortressRuntimeJobSystems`, `FortressRuntimeSystemsFactory`, `FortressRuntimeHostFactory`, and `FortressRuntimeStartup` now compile from Runtime. `FortressRuntimeDependencies` is split into `FortressRuntimeCatalogs`, `FortressRuntimeTunings`, and `FortressRuntimeWorkforce`. App still supplies logger callbacks and optional command delegates, such as auto-dig, because those remain App-specific.

Do not collapse those groups back into one long factory method. They are migration handles: dependencies point toward Content/Runtime, planners point toward Simulation/Jobs boundaries, and job-system shells point toward Jobs/App adapter cleanup.

`FortressRuntimeHostFactory` should create `FortressRuntimeDependencies` once and use that same instance for both `SimulationRuntimeContext` catalog injection and concrete system creation. If host construction reads content separately from systems creation, command targets and gameplay systems can accidentally observe different catalog snapshots after a future hot-reload/content-reload pass. It should receive logging as callbacks; do not reintroduce direct App `Logger` calls into Runtime source.

For runtime composition, structured registry reads should stay behind the Content-owned `FortressRuntimeContentSnapshotLoader`. `FortressRuntimeDependencies.Load(...)` should consume that snapshot, then split it into catalog/tuning/geology/workforce groups. Do not add new direct `ContentRegistry.Instance` / `JObject` tuning reads to host factory, systems factory, job-system group creation, runtime command targets, App rendering helpers, App workshop/build UI helper code, or Navigation.

Scheduler/workshop tuning loaders no longer keep direct file/registry compatibility paths. Runtime composition should use:

```text
FortressRuntimeContentSnapshot
  -> ConstructionTuning.LoadFromJson(...)
  -> NavigationTuning.LoadFromJson(...)
  -> PlaceableTuning.LoadFromJson(...)
  -> SchedulerTunings.LoadFromJson(...)
  -> WorkshopTunings.LoadFromJson(...)
```

The debug cache remains in `GameStateManager` for now because it is UI-facing state, not simulation session state. Do not move it into the runtime host until there is a structured diagnostics surface.

## Navigation Boundary Pitfalls

### Contracts assembly owns shared navigation contracts

`HumanFortress.Contracts` now contains the stable navigation DTO/interface surface:

```text
IPathService
IWorldNavigationView
INavigationWorldSource
NavigationChunkSnapshot
NavigationTile
PathRequest / Path / PathNode / Point3 / ChunkKey
MoveMode / PathFlags / NavCapability
IMovementExecutor / MovementStatus / MovementUpdate
```

These contract types live in the `HumanFortress.Contracts.Navigation` namespace. Concrete navigation implementation types such as `NavigationManager`, `NavigationTuning`, `PathService`, `WorldNavigationView`, `ChunkNavData`, `DeterministicAStar`, and `MovementExecutor` live in `HumanFortress.Navigation.Implementation` as internal implementation types.

Be careful with `ChunkKey`: Simulation world chunks and navigation contracts both define a `ChunkKey`. Files that import both `HumanFortress.Contracts.Navigation` and `HumanFortress.Simulation.World` should use explicit namespaces or aliases where both meanings appear.

Jobs should depend on navigation contracts only. Runtime job-system wrappers are responsible for creating concrete `PathService`, `WorldNavigationView`, and `MovementExecutor` instances and injecting `IPathService`, `IWorldNavigationView`, and `IMovementExecutor` into Jobs-owned executors.

Any project that implements test doubles or directly consumes these contracts should reference `HumanFortress.Contracts` explicitly. Do not rely on transitive references through App.

### Keep Simulation types out of Navigation

`HumanFortress.Navigation` no longer references `HumanFortress.Simulation` or `HumanFortress.Core`. Do not pass `World`, `Chunk`, `TileBase`, `TerrainKind`, content registries, or Core tuning loaders into Navigation internals.

Use the Contracts-owned source/snapshot contracts instead:

```text
INavigationWorldSource
NavigationChunkSnapshot
NavigationTile
NavigationTileKind
```

The current Simulation adapter lives in `HumanFortress.Runtime` as `SimulationNavigationSource`. Keep it there unless a more explicit world-navigation adapter package is introduced; do not move Simulation type knowledge back into Navigation.

Navigation tuning follows the same dependency rule:

```text
Content snapshot
  -> NavigationTuning.LoadFromJson(...)
  -> SimulationNavigationFactory.Create(world, rebuildAll, tuning)
  -> NavigationManager(...)
  -> PathService(...)
```

Do not reintroduce `NavigationTuning.LoadFromContent()` or a Core/Content project reference from `HumanFortress.Navigation`.

## App Composition Pitfalls

### Avoid null-forgiving callback cycles in state/input composition

When a state-level composition factory needs callbacks into a controller that is created later in the same method, do not capture `controller!` in lambdas. Use an explicit callback hub or binder that fails with a clear "used before binding" error. This keeps initialization order auditable and prevents hidden null-reference traps during future constructor changes.

### Keep GameState wrappers as adapters, not runtime service locators

State wrappers should create their screen and call narrow state-transition collaborators. Do not pass the whole `GameStateManager` into a state just to reach runtime init/access methods, and do not repeat direct `GameHost.Instance.Screen` writes in every wrapper. Centralize SadConsole presentation behind an App-owned presenter.

### Do not keep no-op frame hooks as architectural placeholders

The App state-machine wrappers are not the SadConsole screens themselves. If no `GameState` wrapper overrides a frame `Update(...)`, do not keep a `Program -> GameStateManager.Update -> GameState.Update` hook as a placeholder. It suggests the wrong owner for frame work and makes future UI changes harder to reason about. Let SadConsole drive `ScreenObject.Update(TimeSpan)` and keep the state machine focused on transitions.

### Centralize session-size rules

Fortress session size is used by embark UI, runtime world initialization, viewport math, and placement bounds. Do not duplicate `2..8` checks in each caller. Use `FortressSessionSizeRules` so logging, session storage, runtime initialization, and UI viewport sizing stay consistent.

### Query-time rebuild must stay removed

`NavigationManager.GetNavDataAt` is read-only. Do not reintroduce stale-cache rebuilds from path queries.

The intended ownership is:

```text
simulation commit -> collect dirty chunks -> rebuild navigation -> path queries read cache only
```

For isolated job-system tests or temporary private navigation managers, rebuild explicitly at composition time instead of rebuilding from `GetNavDataAt`.

### Content loading diagnostics must fail loudly and specifically

We saw startup output like:

```text
[ContentRegistry] Loaded: 0 materials, 17 geology entries, 19 zone definitions
[ContentRegistry] 18 errors during loading
```

Root causes found and fixed in the first pass:

- old `HumanFortress.Core.Content.ContentRegistry` only looked for legacy `materials.json`, while the repo now ships `materials.authoring.json`;
- summarized errors hid the actual missing references;
- content loading happened before file logging was initialized;
- `geology.json` referenced four ore material ids that did not exist;
- construction loaded both new workshop files and legacy `placeable/workshops.json`, causing duplicate ids;
- recipe loading treated legacy root-array files as parse errors.

Current first-pass behavior:

- content loading is logged to both console and the async diagnostic pipeline;
- App creates a full timeline log at `fortress_debug.log` plus category logs under `logs/`;
- the old registry material-loading bug was removed with the old registry source; the structured registry currently reports 83 materials in startup logs;
- `RuntimeContentRegistryLoader` now loads only the structured runtime registry behind `FortressContentLoader`;
- structured registry loading now supports top-level array material files and resets validation state before reload;
- geology cross-reference errors are clear and currently clean;
- construction duplicates are explicitly skipped and counted;
- recipe loading reports `errors=0`.

Remaining architecture risk: there is now one normal runtime registry source model, the structured registry, and production direct reads are concentrated in Content bootstrap/snapshot capture/application. The old legacy registry source has been deleted, and the structured registry implementation now compiles from `HumanFortress.Content.Registry`. The remaining risk is policy and compatibility: strict content-load failure rules, richer diagnostics/debug surfaces, and cleanup of the few remaining non-registry content DTO compatibility namespaces should be handled without adding new singleton reads.

Runtime geology and zone JSON DTOs have moved to `HumanFortress.Contracts` while keeping their old namespace. The zone loader now uses explicit `System.Text.Json` property mappings, which prevents `zones.json` snake_case fields such as `display_name`, `ui_hints`, and `default_policies` from silently deserializing to defaults.

Item and creature definition loading has now moved into `HumanFortress.Content.Definitions`, and Simulation managers consume snapshots instead of reading files. Construction/recipe loading now also produces immutable snapshots owned by the structured registry and exposed through `FortressRuntimeContentSnapshot`. Construction/recipe definitions and catalog interfaces/stores compile from Contracts. The remaining registry-unification risk is strict-mode diagnostics and compatibility naming, not Core-owned registry implementation or App-local loading orchestration.

### Diagnostics should be async, categorized, and non-authoritative

The first structured diagnostics pass added:

```text
module code
  -> IDiagnosticSink / Logger compatibility facade
  -> async dispatcher
  -> fortress_debug.log
  -> logs/content.log, runtime.log, simulation.log, jobs.log, navigation.log, ui.log, core.log
```

Do not let simulation systems write files directly. Emit a diagnostic event or use a temporary compatibility callback. The dispatcher owns file IO and sequence assignment.

`HumanFortress.Simulation` now has a small `SimulationDiagnostics` helper for transitional systems that still expose static `LogCallback` bridges. Use that helper instead of adding new `Console.WriteLine` calls inside Simulation code.

Do not use diagnostics for authoritative replay. Logs are for observability and debugging; deterministic replay still belongs to command/event/save streams.

`DiagnosticHub` is a transitional bridge for Core systems that are not yet constructed with dependencies. New runtime-owned services should prefer an injected `IDiagnosticSink`.

`Contracts` should stay log-free.

### Embarkability UI needs rule-level diagnostics

The world map showed every tile as `NOT EMBARKABLE`. The UI did not expose which rule failed.

First pass implemented:

- `WorldTile.GetEmbarkabilityFailures()` exposes the current rule failures;
- WorldMap side panel shows the first failure reasons under `NOT EMBARKABLE`;
- regression coverage verifies low elevation, high elevation, and river-class failures.

Before changing world-gen or embark rules further, keep diagnostics showing:

- selected tile coordinates;
- terrain/biome/geology summary;
- each embarkability rule;
- pass/fail reason.

### Console logging is too noisy for tests

Current tests print a lot of `ItemManager` and creature spawn logs. That is acceptable short-term, but it slows reading and hides failures.

Long-term fix:

- structured logger;
- categories and levels;
- test mode default level;
- capture logs only on failure.

## Refactor Process Pitfalls

### Move job systems in slices, not whole executors

`HumanFortress.Jobs` now exists, but the first transport extraction deliberately moved only low-risk state/helper types:

```text
ActiveJob
JobStage
TransportBacklogBuffer
TransportJobFinalizer
TransportJobStatsSnapshot
TransportStatsTracker
JobStats
TransportIntakeFilter
ITransportJobLogger
ITransportWorkerCandidateSource
TransportAssignmentHandler
ITransportMovementDiffEmitter
TransportReplanHandler
ITransportItemDiffEmitter
ITransportJobCompletionSink
TransportPickupHandler
TransportDeliveryHandler
TransportActiveJobRunner
TransportActiveJobView
TransportActiveJobDebugView
TransportDebugSnapshot
TransportJobExecutor
```

This is intentional. Moving the full transport executor in one pass would drag pathing, world access, diffs, professions, logging, and debug snapshots across the boundary at once.

Use this order for future transport movement:

```text
state/contracts -> stats snapshots -> intake/filtering -> assignment/replan -> pickup/delivery -> active runner -> debug DTOs -> executor core -> App composition shell
```

The stats snapshot is now a top-level `TransportJobStatsSnapshot` in Jobs. Avoid reintroducing nested DTOs on `TransportJobSystem`; nested public DTOs make later assembly movement harder.

Transport active/debug snapshot DTOs now live in Jobs (`TransportActiveJobView`, `TransportActiveJobDebugView`, `TransportDebugSnapshot`). Keep public debug contracts near the executor that owns the data; do not put them back as nested App types.

`TransportIntakeFilter` now owns request readiness/de-dup filtering in Jobs. Keep it focused on domain state checks (`item exists`, `item on ground`, `not reserved`) and do not add UI logging or executor side effects to it.

`TransportAssignmentHandler` now lives in Jobs. Profession weighting is behind `ITransportWorkerCandidateSource`, and logging is behind `ITransportJobLogger`. Keep those seams narrow; do not pass App globals or UI-facing services back into Jobs-owned handlers.

`TransportReplanHandler` now lives in Jobs. It only depends on `ITransportMovementDiffEmitter.MoveCreature` instead of the full App `TransportDiffEmitter`. Preserve that narrow dependency: replan should not learn how to split stacks, mark carry state, or move items.

`TransportPickupHandler` and `TransportDeliveryHandler` now live in Jobs. They depend on `ITransportItemDiffEmitter` for item/carry/split diffs and `ITransportJobCompletionSink` for profession progress. Keep destination validation in Simulation and keep App-specific profession objects behind the completion sink.

`TransportActiveJobRunner` now lives in Jobs. It should remain a coordinator over movement update, replan, pickup, delivery, and missing-worker cleanup. It depends on separate movement and item/carry diff interfaces; do not collapse those back into a monolithic concrete emitter.

`TransportJobExecutor` now owns the transport tick core in Jobs: request drain/backlog, assignment throttle, active write tick, scheduling hints, and debug snapshots. The Runtime-owned `TransportJobSystem` should stay a composition shell over narrow Jobs interfaces.

### Mining extraction now follows the same shell/core pattern

The mining executor has the same ownership rule as transport: Jobs owns the tick core and concrete job adapters/emitters; Runtime owns the tick-facing wrapper; App only supplies composition-time logger/content/UI callbacks.

Jobs-owned mining slices now include:

```text
ActiveMiningJob
MiningStage
MiningBacklogBuffer
MiningDeferredStairwellBuffer
MiningTileReservationTracker
MiningPathSeed
MiningJobStatsSnapshot
MiningDebugSnapshot
MiningDebugSnapshotBuilder
MiningDigOrdering
MiningAdjacencyFinder
MiningIntakeCoordinator
MiningStairwellGate
MiningReadJobProcessor
MiningAssignmentHandler
MiningResultApplier
MiningActiveJobRunner
MiningJobExecutor
```

Keep App dependencies behind the narrow mining seams:

```text
IMiningJobLogger
IMiningWorkerCandidateSource
IMiningWorkCostResolver
IMiningDropResolver
IMiningDiffEmitter
IMiningJobCompletionSink
```

### UI state should not store Simulation enum/DTO types

App UI state can mirror a Simulation concept, but the stored type should be an
App-owned presentation option unless it is already a stable contract DTO. Map to
Simulation enums and command DTOs only at the command factory or runtime query
boundary. This keeps menu rendering, input selection, and debug highlight labels
from becoming accidental Simulation API consumers.

The same rule applies to simple menu DTOs: stockpile preset menu options are UI
options until the command boundary only needs a preset id.

### App supplies log callbacks; Runtime owns lower-layer callback targets

App lifetime code can decide which logger implementation to use, but it should
not maintain the list of every lower-layer Simulation/Navigation static
callback target. Keep that list in Runtime composition (`FortressRuntimeLogBindings`)
and let App pass a category-to-callback factory.

Do not make Jobs-owned mining code depend directly on App globals or App-owned concrete services. The tick-facing `MiningJobSystem` wrapper should remain only a composition shell, and executor dependencies should continue crossing through the narrow mining interfaces above. Source-owned Jobs/Runtime helpers may still have the old namespace until the compatibility cleanup pass, but they should not regain App assembly ownership.

### Construction extraction uses callback-only App ownership

Construction is simpler than transport/mining because it does not own pathing or worker assignment. Diff emission and logging bridges are now Jobs-owned, while App ownership is limited to binding the UI workshop-completion callback during bootstrap.

Jobs-owned construction slices now include:

```text
ConstructionRequirementMatcher
ConstructionTargetMapper
ConstructionFootprintCells
ConstructionMaterialTracker
ConstructionSiteProgress
ConstructionSiteSafety
ConstructionCompletionApplier
ConstructionCompletionCoordinator
ConstructionJobExecutor
```

Keep App dependencies behind the narrow construction seams:

```text
IConstructionJobLogger
IConstructionDiffEmitter
IConstructionWorkshopCompletionSink
```

Do not pass `Logger`, concrete UI services, or a static `ConstructionJobSystem` UI hook directly into Jobs-owned construction code. App should bind UI notifications through `IFortressRuntimeBootstrapAccess.SetWorkshopCompletionHandler(...)`; Runtime should route that through `FortressRuntimeWorkshopCompletionNotifier`, while the Runtime-owned `ConstructionJobSystem` remains only a composition shell over narrow Jobs interfaces and callback injection.

The former `InternalsVisibleTo("HumanFortress.App")` bridge in `HumanFortress.Jobs` has been removed. Do not add it back; App should use Runtime facades and Contracts snapshot DTOs, not Jobs internals.

Jobs implementation types should stay internal. Runtime and tests currently use
friend access for executor cores, tunings, orchestration probes, debug snapshots,
and adapters. If a shape must become a long-term public cross-module contract,
move that shape to `HumanFortress.Contracts` first instead of exposing the Jobs
implementation type.

### Craft extraction needs three explicit seams

Craft looks small from the outside, but it crosses planning, content lookup, material logistics, worker assignment, movement, item diffs, and workshop queue state. Moving it safely required separating those concerns instead of dragging App types into Jobs.

Jobs-owned craft slices now include:

```text
PlannedCraftJob
ActiveCraftJob
CraftJobStatsSnapshot
ActiveCraftJobView
CraftWorkshopLocator
CraftInputCounter
CraftMaterialReadinessChecker
CraftTransportRequestEmitter
CraftPlanner
CraftMaterialConsumer
CraftOutputEmitter
CraftAssignmentHandler
CraftActiveJobRunner
CraftJobFinalizer
CraftJobExecutor
```

Keep App dependencies behind the narrow craft seams:

```text
ICraftJobPlanner
ICraftRecipeCatalog
ICraftDiffEmitter
ICraftWorkerCandidateSource
```

Do not pass `RecipeRegistry` or App globals deeper into Jobs-owned craft code. `CraftJobSystem` is now a Runtime-owned composition shell, while `CraftDiffEmitter`, `ProfessionAssignments`, and `WorkerSelectionStrategy` are Jobs-owned compatibility-namespace types.

`CraftPlanner` now lives in Jobs. The important lesson is that Planner could move only after recipe lookup was hidden behind `ICraftRecipeCatalog`; otherwise Jobs-owned craft code would keep reaching into the global `RecipeRegistry.Instance` singleton.

### Diff-based systems need regression tests before movement

Any direct world mutation replaced by diffs must have a small regression first or immediately after.

Useful examples already added:

- construction material consumption emits item remove diff;
- construction residual relocation emits `MoveItem`;
- transport split-stack rollback is non-mutating on failure;
- craft input failure preserves queue entry.

### Do not rederive chunk/local targets in job emitters

Job diff emitters should not hand-roll:

```text
chunkX = worldX / Chunk.SIZE_XY
localX = worldX % Chunk.SIZE_XY
localIndex = Chunk.LocalIndex(localX, localY)
```

Use `WorldCellTargetEncoding` instead, then pass the resulting `WorldCellTarget` directly to `ItemsDiffLog` where possible. The older `ChunkKey + localIndex` item-diff overloads remain for compatibility, while general `DiffLog` still uses `DiffTarget`.

### Planner and executor must share domain rules

Craft exposed a real bug:

- planner treated workshop footprint plus adjacent ring as available input area;
- consumer only consumed from the footprint.

Result: jobs could be planned as ready but fail at consumption time.

Rule: if planner and executor both reason about the same gameplay concept, extract that rule into shared helper code.

### Do not move projects before dependencies are inverted

Transport, mining, construction, and craft executor cores now live in `HumanFortress.Jobs`, and craft planning has also moved there. Runtime now owns the tick-facing job wrappers. App still owns concrete session/runtime glue that depends on UI lifetime, logger callbacks, and bootstrap wiring.

Moving them too early would drag App/runtime/navigation dependencies into the wrong assemblies. Invert Navigation and stabilize contracts first.

### UI snapshots are not App-owned just because UI consumes them

The App may own SadConsole rendering, input routing, and session glue, but UI/debug read models that aggregate live Runtime/Simulation/Jobs data should not live in `HumanFortress.App.Runtime`.

Current rule:

```text
Contracts.Runtime.Snapshots
  owns public snapshot DTO contracts consumed by App/UI

Runtime.Snapshots
  owns builders/facades that aggregate Runtime/Simulation/Jobs state into
  the contract DTOs

App.Runtime
  calls snapshot facades and adapts them to SadConsole UI/input flows
```

Do not move job/workforce/order/workshop/build/debug/tile-inspection DTOs or aggregation helpers back into App for convenience. If a snapshot builder starts growing into a God Object, split it by read-model domain inside `HumanFortress.Runtime.Snapshots` instead of making `GameStateManager` or `FortressRuntimeAccess` responsible for the mapping.

When a read model needs multiple live session parts, add a Runtime-owned session facade such as `FortressRuntimeSessionSnapshotFacade` instead of unpacking `_runtimeSession.World`, `.Navigation`, `.Host.Geology`, `.Host.Constructions`, or `.Host.Recipes` in App. Keep live world use in App limited to scoped bootstrap operations like fortress-map fill and optional startup debug seeding.

`HumanFortress.Contracts` should stay renderer-agnostic. Runtime snapshot DTOs
use project-owned `SnapshotColor` and `SnapshotPoint` primitives; Runtime maps
from SadRogue types while building read models, and App maps back to SadRogue
types at drawing boundaries. Do not add `TheSadRogue.Primitives` back to
Contracts just because a DTO is consumed by SadConsole.

### Architecture boundaries should be executable checks

Manual source scans are useful during refactor, but the stable rule should live
in tests once it becomes a boundary. The current smoke runner enforces that App
does not import lower implementation modules, App.Runtime imports stay confined
to adapter/port composition files, old mixed runtime facade names do not return,
and production project references match the approved module graph.

When changing project references, update the architecture smoke test in the same
patch as the csproj change. A new reference should represent an intentional
module-boundary decision, not a convenient compile fix.

The same applies to public types. Content, Jobs, Navigation, Simulation, and
WorldGen are internal/friend implementation assemblies; App should expose only
`Program`; Runtime should expose only the approved factories/bootstrap helpers
and App-facing session port interfaces; Core should expose only approved
foundation types for commands, events, ticks, deterministic RNG, replay hashes,
diffs, and world primitives. If a new public type is genuinely needed, update
the smoke test allowlist with a boundary explanation in the same patch.

### Cross-module diagnostics are contracts

Diagnostic event DTOs, severity levels, sink interfaces, and the transitional
process-wide diagnostic hub are consumed by App, Core, Content, Simulation, and
WorldGen. Keeping them in Core forced App and Content to reference Core for a
logging concern. These types now live in `HumanFortress.Contracts.Diagnostics`.

Do not add gameplay, command, random, or scheduler dependencies to the
diagnostic contract namespace. App should implement sinks and UI presentation
against Contracts; lower modules should only emit diagnostic events through the
contract sink/hub.

### App-facing content load data is a contract, not Content implementation

App needs to present content load failures, strict-mode blocking issues, and a
few UI registry file paths. It should not reference `HumanFortress.Content` for
those jobs. Content owns actual JSON loading and registry/catalog assembly;
Runtime owns the public startup/file-location facade; App consumes
`HumanFortress.Contracts.Content.Loading` report/path/issue DTOs.

If a new App feature needs a content-backed UI file, route file location through
an App-owned wrapper such as `AppContentFileLocator` instead of importing
`FortressContentLoader` directly. If it needs gameplay/content facts, add a
Runtime snapshot/query DTO rather than parsing Content JSON in App.

### Runtime root is not the internal helper bucket

The Runtime project is allowed to compose lower implementation modules, but its
root namespace should not become a dumping ground for every helper created
during that composition. Public factories/ports and `FortressRuntimeSessionCore`
facade partials can stay in `HumanFortress.Runtime`; internal helper families
should use focused namespaces and folders.

Current examples:

```text
HumanFortress.Runtime.Composition     -> system/dependency/logging composition
HumanFortress.Runtime.Host            -> host, tick pipeline, command clock context
HumanFortress.Runtime.Session         -> session handles and session services
HumanFortress.Runtime.Diff            -> Runtime-owned mutation log bundles
HumanFortress.Runtime.Content         -> content bootstrap adapters and stockpile presets
HumanFortress.Runtime.Geometry        -> Runtime internal geometry adapters
HumanFortress.Runtime.WorldGeneration -> fortress generation runner glue
HumanFortress.Runtime.Navigation      -> Simulation-backed navigation adapters
HumanFortress.Runtime.Startup         -> startup/autodig helpers
HumanFortress.Runtime.Commands        -> command implementations, execution, targets
```

When moving a helper out of the root namespace, move the physical file in the
same batch and add/update architecture smoke coverage for both path and
namespace. If a helper needs `RuntimeMutationDiffLogs`,
`RuntimeSessionServices`, or the stockpile preset catalog, make that dependency
explicit with `HumanFortress.Runtime.Diff`, `.Session`, or `.Content` imports
instead of leaving the helper in the root namespace for convenience.

### Keep build verification short and explicit

Fast checks:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet build src/HumanFortress.App/HumanFortress.App.csproj --no-restore --no-dependencies -m:1 -v:quiet -p:RunAnalyzers=false
./RunTests.sh
```

Full check:

```bash
/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:quiet -p:RunAnalyzers=false
git diff --check
```

If a command produces no output for too long, investigate instead of waiting indefinitely.
