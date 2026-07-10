# Refactor Batch Progress

This document tracks the current multi-step refactor batches so progress is visible without relying on chat history.

## Current Status Snapshot - 2026-07-10

- Follow-up source-only completion audit re-ran the high-signal architecture
  scans without build/test: active App source has no direct Core, Content,
  Jobs, Simulation, Navigation, WorldGen, Runtime.Commands, Runtime.Save, or
  Runtime.Replay imports; legacy Core.Content/root Navigation/root WorldGen/root
  Jobs namespaces are absent from active source except for architecture smoke
  forbidden-pattern constants; Simulation/Jobs/WorldGen ordinary public surface
  is locked down to internal/friend-only implementation members, with only the
  required `Chunk` object overrides left public; Runtime ordinary public surface
  is limited to App-facing session ports/factories/content/logging bootstrap;
  Content ordinary public surface is limited to the approved JSON DTO setter;
  save/replay authority paths showed no dictionary view iteration, unstable
  object hash calls, or random command identity generation; concrete
  Navigation service creation remains centralized behind Runtime seams; and
  `git diff --check` passed. `WORK_AND_JOBS_SYSTEM.md` was updated to remove
  stale statements that App owns job shells/tunings/debug-state reads and to
  mark Runtime-authored job/debug snapshot DTOs as current. Follow-up document
  sync also changed active architecture/transport/planning guidance from
  "clean remaining compatibility namespaces" to "keep compatibility namespaces
  from returning", matching current source scans and smoke-test guards. This
  audit also removed the stale `HumanFortress.Core.Tests`
  `InternalsVisibleTo` bridge from `HumanFortress.Core`; no such project exists
  in the repository, and architecture smoke now expects Core to have no friend
  assemblies. The same source-only audit closed the remaining Navigation
  wall-clock budget contract leak: `NavigationTuning` and
  `content/registries/tuning.navigation.json` no longer expose the old
  `max_ms_per_tick_pathing` / `MaxMsPerTickPathing` field; active tuning now
  uses deterministic node/request budgets (`max_nodes_per_search` and
  `max_paths_per_tick`), and deterministic smoke guards both the execution
  path and tuning/content contract from reintroducing wall-clock path budgets.
  `PathService` queued requests now use an explicit deterministic FIFO
  `Queue<PathRequest>` and drain without rotating the oldest queued request
  behind newer requests when the per-tick path budget is exhausted.
  `DeterministicGuidGenerator.Generate(...)` now writes its four RNG words with
  explicit little-endian `BinaryPrimitives` calls instead of host-endian
  `BitConverter.GetBytes`, and deterministic smoke guards that portable GUID
  encoding path beside the existing diff-target endian checks. The same
  deterministic ID cleanup removed the unscoped position-based GUID overload:
  position-derived GUIDs now require an explicit scope salt, placeable creation
  scopes live in `HumanFortress.Simulation.Placeables`, construction completion
  derives the finished placeable from the construction-site GUID, and installed
  / uninstalled item placeables derive IDs from the source placeable/item GUID
  instead of relying on same-tick same-position seeds. Contracts cleanup also
  removed the unused `MaterialDistribution.SelectMaterial(System.Random)` DTO
  helper; architecture smoke now forbids Contracts from reintroducing
  RNG/time/`Guid.NewGuid()` runtime-authority helpers, and
  `PLACEABLE_SPEC.md` now matches the deterministic new-GUID uninstall rule.
  Reservation ownership was hardened as well: `ReservationManager` now uses
  Simulation-owned dictionaries with explicit write-phase reserve/release
  mutation, pure read checks, and stable GUID-ordered snapshots instead of
  `ConcurrentDictionary.AddOrUpdate` callbacks that mutate shared reservation
  objects. Navigation movement pacing also now comes from deterministic
  `NavigationTuning.MovementStepDelayTicks` and `tuning.navigation.json`
  (`movement.step_delay_ticks`) instead of a hardcoded MovementExecutor
  display-pacing delay. Fortress session size limits now live in Contracts as
  `FortressSessionSizeLimits`; App size rules and Simulation `World` both
  consume the shared 2..16 chunk range, and world save payload validation uses
  the same Simulation world limits. This raises the lazy world ceiling to
  512x512 tiles while keeping one boundary-owned size contract. Scheduler
  tuning also dropped the unused wall-clock `ms` budget field from code and
  content; `SchedulerTunings.Budget` now carries only deterministic
  `PlanPerTick` work counts, with smoke coverage preventing the old field from
  returning. Orders authority state also no longer relies on unordered
  concurrent collection enumeration: `OrdersManager` now owns ingress queues,
  recent previews, and active designation lists as guarded `Queue<T>`/`List<T>`
  state, exposes stable sorted active snapshots at the source, and has smoke
  coverage preventing `ConcurrentBag`/`ConcurrentQueue` from returning to the
  manager. The same cleanup moved Simulation order planner outboxes away from
  concurrent collection ordering: Mining and buildable construction now use
  deterministic owner queues for their read/write handoffs, while the dead
  structural construction planned-build outbox and unused dequeue seam were
  removed. Jobs-owned retry/backlog queues were then aligned with the same
  deterministic owner-state rule: mining and transport backlog buffers, craft
  planner outbox, and craft executor backlog now use ordinary FIFO `Queue<T>`
  storage with snapshot/restore order preserved, and deterministic smoke locks
  these queues away from `ConcurrentQueue`. Core command ingress was aligned as
  well: `CommandQueue` now keeps pending commands as lock-owned FIFO
  `Queue<QueuedCommand>` state, with explicit sequence tie-breaks for due/future
  execution and pending replay-record snapshots instead of `ConcurrentQueue`
  enumeration. Core `EventBus` now follows the same owner-state pattern:
  handler lists are lock-owned, copied in registration order for publish, and
  guarded from `ConcurrentDictionary.AddOrUpdate` callback mutation. The
  remaining concurrent infrastructure cleanup then moved session RNG streams,
  Simulation world chunk storage, concrete Navigation chunk nav-data storage,
  and `PathCache` cache/reverse-index state to owner-lock dictionaries with
  stable stream-name, spatial, or cache-key ordering where snapshots,
  invalidation, or LRU eviction can affect behavior. Runtime command identity
  sequence advancement and transport request queue counters were also moved
  under their owning session/queue guards instead of `Interlocked` authority
  updates. Deterministic smoke now guards these infra dictionaries and
  authority counters from regressing to concurrent callback/atomic state. The
  follow-up diagnostics boundary pass also moved Core infrastructure failures
  (`CommandQueue`, `EventBus`, and `TickScheduler`) behind optional
  `IDiagnosticSink` injection, with `DiagnosticHub` kept only as the
  compatibility fallback for manually constructed Core objects. Runtime
  session services now pass the active diagnostic sink into their default Core
  infrastructure, and architecture smoke guards that Core no longer calls
  `DiagnosticHub.Error(...)` directly from those infrastructure types.
  Runtime-created WorldGen services and fortress generation/fill maps now
  follow the same direction: `WorldGenerationService`, `WorldGenerator`,
  `FortressGenerator`, and `FortressMap` accept `IDiagnosticSink` injection,
  Runtime composition passes the active sink, and WorldGen implementation code
  is guarded from direct `DiagnosticHub.Sink.*` emission. Content registry
  and Simulation diagnostics helpers now also expose module-local injectable
  sinks, with Runtime content/session composition assigning the active sink
  before registry load or simulation log-callback binding. Architecture smoke
  now rejects direct `DiagnosticHub.Sink.*` / `DiagnosticHub.Error(...)`
  emission in lower implementation modules.
  Transport debug/scheduler stats were also made session-owned: the old static
  `JobStats` counter carrier was deleted, pickup/delivery handlers now record
  no-path/completion totals through the executor-owned `TransportStatsTracker`,
  backlog retry totals are recorded from the actual requeue path, and
  deterministic smoke rejects static transport stat counters from returning.
  Runtime full/world save restore now composes restored worlds through a
  staging Runtime session/services pair and only commits the active session
  after content, Jobs, RNG, and pending-command restore slices succeed; smoke
  coverage includes a validation-passing transport-job restore failure and
  checks that the current session checkpoint remains unchanged.
  This audit has not yet been build/test verified.
- Follow-up source-only Stockpile implementation-surface hardening removed
  the last ordinary `public` member from the internal
  `HumanFortress.Simulation.Stockpile` implementation directory.
  `StockpileWorldQueries` now keeps its destination-candidate comparison
  private to the Simulation stockpile query seam, and architecture smoke now
  scans the Stockpile directory beside Zones to reject new misleading public
  members in internal implementation modules. The same boundary pass narrowed
  `ItemInstance`, `CreatureInstance`, and item support state carriers
  (`ReservationToken`, `PlacementData`, `Improvement`, `PerishableState`) so
  Simulation runtime entity state exposes internal/friend-only members rather
  than looking like Contracts DTOs. The pass then narrowed the remaining
  ordinary `public` members under `HumanFortress.Simulation.Items` and
  `HumanFortress.Simulation.Creatures`: `ItemManager` and `CreatureManager`
  now keep runtime query/mutation/spawn/setup seams internal/friend-only, while
  the cross-module catalog view is preserved through explicit
  `IItemDefinitionCatalog` / `ICreatureDefinitionCatalog` implementations.
  Architecture smoke now scans the full Items, Creatures, Stockpile, and Zones
  implementation directories and rejects new ordinary public members there.
  World/Chunk implementation surfaces were then narrowed as well:
  `World`, `Chunk`, chunk keys, furniture/field/item-stack refs, lifecycle
  manager state, and world-cell target helpers now expose ordinary members as
  internal/friend-only; `World` keeps `IWorldReader` through explicit
  implementation and `ChunkKey` keeps `IEquatable<ChunkKey>` through an
  explicit bridge while required `object` overrides remain public. Architecture
  smoke now scans the World directory and allows only those required public
  overrides. Placeable and tile runtime implementation surfaces were narrowed
  in the same direction: `TileBase` and terrain bit accessors, `PlaceableInstance`,
  chunk placeable storage/sync, construction-site/door/workshop state, and
  workshop queue entries now expose ordinary members internal/friend-only.
  Architecture smoke now scans Placeables and Tiles alongside the other
  Simulation implementation directories. Simulation.Jobs was then brought into
  the same shape: `ReservationManager` and transport queue state expose
  internal/friend-only members, `TransportRequestQueue` preserves
  `ITransportIntake` / `ITransportRequestQueue` through explicit interface
  implementations, and `ConstructionMaterialsPlanner` preserves `ITick`
  through explicit read/write/system-id/priority bridges rather than ordinary
  public implementation methods. Architecture smoke now scans Simulation.Jobs
  for public-member drift as well. Orders followed the same explicit-interface
  pattern: buildable construction, structural construction, hauling, and mining
  tick systems now keep their ordinary members internal/friend-only while
  preserving `ITick` through explicit bridges, and order designation/value
  objects plus the construction terrain material resolver no longer expose
  ordinary public members. Architecture smoke now scans Simulation.Orders too,
  and a final Simulation-wide guard rejects ordinary public members anywhere in
  `HumanFortress.Simulation`, allowing only required `object` overrides. The
  Jobs implementation project was already clean of ordinary public members, so
  architecture smoke now locks `HumanFortress.Jobs` with a project-wide
  internal/friend-only member-surface guard too. `HumanFortress.WorldGen` now
  follows the same rule: world-generation services/data keep Contracts-facing
  behavior through explicit `IWorldGenerationService` / `IGeneratedWorldData`
  bridges, stage execution keeps `IWorldGenStage` explicit, and
  fortress-generation maps/chunks/content expose ordinary members
  internal/friend-only. Architecture smoke now scans WorldGen for ordinary
  public-member drift.
- Follow-up source-only Runtime implementation-surface hardening narrowed the
  remaining ordinary `public` members from internal Runtime save and command
  replay helpers. `RuntimeCommandReplayFactory` now keeps its direct
  `TryCreateCommand(...)` helper internal while preserving
  `ICommandReplayFactory` through explicit implementation, and Runtime save
  snapshot data/document codec/document store helpers now expose their ordinary
  members internal/friend-only. Runtime's public surface is now limited to the
  approved App-facing ports/factories/content/logging bootstrap files, with
  architecture smoke rejecting ordinary public members elsewhere in
  `HumanFortress.Runtime`. This batch has not yet been build/test verified.
- Follow-up source-only Content implementation-surface hardening narrowed the
  remaining non-serializer public members from Content-owned profession
  registry and runtime content loader/result helpers. `ProfessionRegistry`
  preserves `IProfessionRegistry` through explicit implementation, while
  Runtime still consumes Content loader helpers through friend access and App
  continues to see only Contracts/Runtime-facing reports. The only remaining
  ordinary public member in `HumanFortress.Content` is the approved
  `System.Text.Json`-bound `RuntimeZoneDefinitionData` list setter, and
  architecture smoke now rejects any other Content public-member drift. This
  batch has not yet been build/test verified.
- Follow-up source-only Simulation implementation-surface hardening narrowed
  the remaining ordinary `public` members inside the internal
  `HumanFortress.Simulation.Zones` implementation types. `ZoneInstance`,
  zone `ZoneShard`, `ChunkZoneData`, `ZoneDefinition`/`ZoneUiHints`/
  `ZonePolicies`, `ZoneManager`, and `ZoneCoordinator` now expose internal
  /friend-only members instead of looking like a stable public zone API from
  inside an internal module. Architecture smoke now scans the Simulation Zones
  implementation directory and rejects new `public` members there, keeping
  zone runtime state behind Simulation/Runtime friend seams and Contracts
  read-model DTOs. This batch has not yet been build/test verified.
- Follow-up source-only Runtime navigation seam hardening moved debug path
  overlay path queries behind `RuntimeNavigationServices` as well as job
  wrappers. `RuntimeNavigationServices` now exposes a reusable path-query
  bundle that owns `PathService` registration plus `WorldNavigationView`
  construction, and job services build movement execution from that same
  query bundle. The active session host now keeps the session-owned
  `RuntimePathServiceRegistry` visible to Runtime snapshot facades, so debug
  path overlay queries can create `RuntimeNavigationServices` with the same
  registry used by job wrappers. `NavigationOverlaySnapshotBuilder.FindPath(...)`
  now asks that Runtime navigation seam to solve the path instead of directly
  constructing `DeterministicAStar` and `WorldNavigationView`, and session
  debug path queries register their path service for the existing dirty-chunk
  invalidation path. Architecture smoke now scans all Runtime source outside
  `RuntimeNavigationServices` and rejects direct `PathService`,
  `MovementExecutor`, `WorldNavigationView`, or `DeterministicAStar`
  construction so future debug/presenter paths cannot bypass the Runtime-owned
  navigation creation boundary. This batch has not yet been build/test
  verified.
- Follow-up source-only deterministic view hardening removed the remaining
  broad `Dictionary.Keys`/`Dictionary.Values` snapshot patterns from active
  Simulation/Jobs/Runtime implementation source. Stockpile/zone managers,
  chunk stockpile/zone shards, item/creature manager snapshots, mining active
  designations, world/chunk lifecycle snapshots, stockpile preset menus,
  Runtime map-viewport presenter delta cells, stockpile command/applicator
  cell grouping, save-payload contained-item validation, construction material
  consumption, and stockpile diff quantity calculation now derive stable
  snapshots from explicitly ordered key/value rows or existing owner-specific
  ordering helpers instead of dictionary view enumeration. Source-only guard
  scans now report no `.Keys`/`.Values` hits in active Simulation/Jobs/Runtime
  implementation code, and the targeted save/replay/hash/command authority
  scan reports no `.Keys`/`.Values`/`GetHashCode`/`Guid.NewGuid` hits. This
  batch has not yet been build/test verified.
- Follow-up source-only determinism hardening removed wall-clock scheduler
  timing from `UnifiedJobsOrchestrator` and the Runtime/App jobs debug
  snapshot path. Jobs orchestration still reports deterministic plan/apply
  stage counts plus intake counts, but no longer stores or displays
  `Stopwatch`/`ElapsedMilliseconds`-derived `PlanMsTotal`/`ApplyMsTotal`
  values in Contracts-owned read models. Deterministic smoke now locks that the
  orchestrator, Runtime jobs snapshot builder, Contracts jobs debug DTO, and
  App Work drawer scheduler column use stage/intake counters rather than
  machine-speed-dependent elapsed milliseconds. The same cleanup removed a
  remaining `_shards.Keys` view from transport shard-id snapshots and derives
  construction material id snapshots from sorted owner rows rather than
  dictionary key views.
- Latest source-only Simulation hardening cleared the remaining active `src`
  placeholder markers without moving policy into App. `ChunkPlaceableData` now exposes
  stable external-reference snapshots and GUID lookup hooks, while the
  Simulation-owned `PlaceableManager` resolves cross-chunk external references
  through a world-level query seam and removes secondary external refs when the
  primary owned placeable is removed. Placeable placement now iterates affected
  chunks through the existing spatially ordered `GetAffectedChunks(...)` helper
  instead of `HashSet<Chunk>` enumeration. Smoke coverage locks cross-chunk
  lookup/removal behavior and static coverage prevents the old unresolved
  external-ref placeholder from returning. Construction site material
  requirements now preserve content-authored `def_id` costs as explicit
  `def:<itemId>` requirements rather than silently ignoring them; the
  Simulation planner, Jobs construction material tracker, and Runtime workshop
  material-progress snapshot now all share the same
  `ConstructionMaterialRequirement` matcher, while legacy tag requirements keep
  their existing unprefixed keys for save/read-model compatibility. The
  stairwell UI-Z to simulation-Z conversion was moved out of
  `OrdersManager.Mining` into `MiningZRangeMapper`, with smoke coverage locking
  the current `MININGSYSTEM_SPEC.md` behavior. Item/creature spawn comments now
  document the real boundary: gameplay commands emit typed item/creature diffs,
  and the manager spawn entrypoints are owner mutation seams for diff
  applicators, bootstrap, restore, and focused Simulation tests.
- Latest source-only TickScheduler failure-policy hardening replaced the old
  placeholder quarantine comment with executable Core behavior. A system that
  throws during read no longer runs its write phase for that tick; repeated
  system failures are tracked by `SystemId`; and systems are quarantined after
  three consecutive failures while healthy systems keep ticking. Smoke coverage
  now locks read-failure write skipping, quarantine behavior, non-empty unique
  tick `SystemId` registration, and the absence of the old placeholder marker,
  addressing the archived audit's concern that failed read phases could still
  commit half-initialized write state or leave ambiguous scheduler failure
  state. The `ITick` contract comment now matches the current deterministic
  registered-system read order instead of promising read-phase parallelism.
  The same source-only hardening removed `ItemManager.SpawnItem`'s
  same-cell full-table scans: stacking and diagnostic grouping now read the
  existing `_posIndex` tile list, and deterministic smoke coverage locks the
  indexed spawn path. `CreatureManager.SpawnCreature` now derives initial
  `MaxHP` from `CreatureDefinition.BaseToughness` instead of a hardcoded 100,
  with static smoke coverage preventing the placeholder from returning.
  Chunk-level dirty tile tracking now uses a deterministic `SortedSet<int>`
  plus `DrainDirtyTileIndices()`, and placeable placement marks actual footprint
  cells dirty instead of the old placeholder local index 0. This gives later
  cache/snapshot delta work a stable local-index seam rather than a comment-only
  DirtyTileSet placeholder.
  Simulation diagnostics also no longer fall back to `Console.WriteLine` when
  no callback or `DiagnosticHub` sink is configured; Content and WorldGen
  diagnostics likewise rely on `DiagnosticHub` instead of fallback console
  output. Architecture smoke now forbids direct console output fallbacks in
  implementation modules while leaving App command-line compatibility messages
  App-owned. Core deterministic RNG seeding now expands 64-bit seeds through
  SplitMix64 before xoshiro state initialization, and named RNG streams derive
  seeds through a stable UTF-16 FNV-1a hash plus SplitMix64 mixing instead of
  the older `seed * 31 + char` pattern; smoke coverage locks the improved seed
  diffusion and empty-stream rejection.
- Latest source-only CI/verification hardening added
  `.github/workflows/dotnet-ci.yml` as the first repository-level GitHub
  Actions gate for .NET 8 restore, solution build, smoke-runner build, and the
  executable architecture/determinism smoke runner on Linux and Windows with
  tiered compilation disabled for the smoke job.
  Architecture boundary smoke now also asserts that this workflow remains
  present and continues to run the solution build plus `HumanFortress.App.Tests`
  across both OS targets, closing the archived audit's "no CI configuration"
  gap without moving test logic back into App. The same smoke batch added a
  source-wide legacy namespace guard so active source cannot reintroduce
  `HumanFortress.Core.Content`, root Navigation, root WorldGen, or root Jobs
  using directives after the compatibility cleanup. Runtime full-loop
  determinism smoke now runs 500 manual ticks and forces a GC boundary between
  equivalent sessions before comparing world/checkpoint/RNG/command/job hashes.
  The v6 save content-catalog summary factory also now enumerates catalog
  dictionaries through key/value rows plus explicit ordinal sorting instead of
  dictionary `.Keys` views, keeping the new save authority path aligned with
  deterministic smoke rules.
- Latest source-only architecture-smoke hardening expanded the executable
  guardrails around the final save/movement refactor work. Deterministic
  authority smoke now locks `RuntimeSaveFormat.CurrentVersion = 6`, the
  Contracts manifest `ContentCatalog` field, the verifier's historical
  unavailable-catalog path, and adjacent runtime snapshot transform generation.
  Architecture boundary smoke now scans Runtime job wrappers and composition
  files so future code cannot recreate concrete `PathService` or
  `MovementExecutor` instances outside `RuntimeNavigationServices`.
- Follow-up source-only migration-order hardening changed
  `RuntimeSaveSlotMigrationTransformRegistry.BuildRequiredTransformIds(...)` to
  return transform ids in Runtime execution order instead of lexicographic
  order: adjacent runtime snapshot transforms first, then slot-manifest rebuild
  when needed. Migration execution now reports the transforms it actually
  applied in that order, and smoke coverage exercises the combined
  historical-v5-runtime + old-slot-manifest path.
- Latest source-only Runtime save content-catalog persistence hardening bumped
  `RuntimeSaveFormat.CurrentVersion` to `6`. New `runtime_snapshot.json`
  manifests now persist a saved content catalog summary beside the saved
  content signature, so Runtime-authored content mismatch rows can include
  exact saved-only/current-only material, terrain, construction, recipe,
  geology, and zone keys when both sides have catalogs. The migration registry
  now covers `runtime_snapshot:4->5` and `runtime_snapshot:5->6`; v4 documents
  can migrate through both transforms when the final document validates, while
  historical v5 documents that lack saved catalog payloads migrate by bumping
  the schema and preserving an unavailable saved catalog instead of inventing
  remap data. Restore authority remains the exact Runtime content signature
  gate and continues to fail closed with `slot.content` until a real
  missing-content/remap policy exists.
- Latest source-only Runtime navigation ownership hardening added
  `RuntimeNavigationServices` under `HumanFortress.Runtime.Navigation`.
  Runtime job-system wrappers for mining, transport, and craft now obtain
  `IPathService`, `IWorldNavigationView`, and `IMovementExecutor` bundles from
  that Runtime-owned seam instead of directly constructing concrete
  `PathService`, `WorldNavigationView`, and `MovementExecutor` instances. The
  same seam registers path services with `RuntimePathServiceRegistry` at
  creation time, keeping dirty-navigation-chunk cache invalidation attached to
  the service creation boundary. This is not yet the future centralized
  movement-intent/reservation system, but it removes another source of
  fragmented path/movement ownership from long-horizon Jobs wrappers.
- Latest source-only Runtime content-compatibility diagnostics hardening added
  a Runtime-authored current catalog summary to
  `RuntimeSaveSlotContentCompatibilityData`. `RuntimeSaveContentCatalogSummaryFactory`
  builds sorted material-name, terrain-kind-name, construction-id, recipe-id,
  geology-id, and zone-id arrays from the active Runtime content snapshot, and
  slot inspection exposes that read model beside the saved/current content
  signatures. A follow-up structured
  `RuntimeSaveContentCompatibilityDifferenceData` read model now records each
  content mismatch by field/kind/saved/current value. The later v6 format now
  persists the saved catalog summary for new saves, while migrated historical
  v5 saves may still report saved catalog keys as unavailable. This remains
  deliberately inspection/debug data for future missing-content/remap
  diagnostics: App may display it, but restore authority still comes from
  Runtime's exact signature gate and continues to fail closed with
  `slot.content` until a real remap policy exists.
- Latest source-only Runtime save-slot migration hardening added the first
  concrete runtime snapshot schema migration. `RuntimeSaveSlotMigrationTransformRegistry`
  now registers `slot:0->1`, `runtime_snapshot:4->5`, and
  `runtime_snapshot:5->6`; runtime snapshot
  directories that contain a current-format `runtime_snapshot.json` but have no
  `slot_manifest.json`, and slot-format-0 manifests over a current Runtime
  snapshot document, can migrate into a current slot directory by rebuilding
  the manifest through Runtime's existing `WriteAtomic(...)` path. Runtime save
  format 4 documents can migrate through 4->5 and 5->6 only when the final
  current-format verifier accepts the migrated document, which covers the safe
  empty-mining-checkpoint case and keeps non-empty mining checkpoint sections
  without mining payloads fail-closed. Runtime save format 5 documents can
  migrate to format 6 while preserving an unavailable saved content catalog for
  historical documents. App still only passes source/target directories plus
  displays Contracts result DTOs.
- Latest source-only Runtime save content-compatibility hardening added the
  first fail-closed missing-content policy seam without moving load decisions
  into App. Contracts now expose `RuntimeSaveSlotContentCompatibilityData` on
  slot inspection, Runtime builds current content signatures through
  `RuntimeSaveContentSignatureFactory`, and
  `RuntimeSaveSlotContentCompatibilityPolicy` compares saved/current content
  version, content hashes, material hash, and catalog shape. Slot inspection and
  restore-plan read models now report structured `slot.content` blockers when
  content cannot be bound, while pending-command, world, and full restore ports
  independently enforce the same gate before replay/RNG/job restore proceeds.
  The actual placeholder/missing-content remap policy is still future work; the
  current behavior is intentionally fail-closed.
- Latest source-only Runtime save-slot migration hardening added the first
  migration execution seam without moving save/load policy into App. Contracts
  now expose `RuntimeSaveSlotMigrationResultData`, the full internal Runtime
  save snapshot port exposes `MigrateSaveSnapshotDirectory(source, target)`,
  and `RuntimeSaveSlotMigrator` owns the execution preflight through
  `InspectDirectory(...)`. Compatible current slots return a successful no-op
  migration result. A later batch registered `slot:0->1` for current-format
  runtime snapshots missing current slot metadata; older Runtime snapshot
  document formats still fail closed with Runtime-authored `slot.migration`
  missing-transform issues. Smoke coverage now locks both behaviors plus the
  focused Runtime helper namespace.
- Latest source-only documentation hygiene re-read the active
  `docs/architecture` and `docs/planning` sets plus the broader non-archive
  documentation tree. No additional active architecture/planning document was
  archived: those files are still current implementation maps, normative target
  contracts, active refactor guidance, rules, lessons, or optimization backlog.
  The old workshop JSON draft copies formerly under `docs/other` were moved to
  `docs/archive/other`, and active content docs now point at
  `data/core/workshops` as the machine-readable workshop source of truth while
  treating those archived copies as historical until reconciled.
- Latest source-only App presenter-delta hardening added
  `FortressMapViewportPresenterCache` and `FortressUiOverlayPresenterCache`
  under App.Rendering. The map cache consumes Contracts-owned
  `SimulationMapViewportDeltaData` rows, regions, or changed cells from Runtime,
  composes them into a complete map-viewport cell list, and hands that full DTO
  back to the existing SadConsole renderer and overlay paths. The UI overlay
  cache consumes Runtime-authored section deltas and preserves unchanged
  build/jobs/workshop/stockpile/zone/drawer/debug sections from the previous
  overlay frame. This keeps frame diff authority in Runtime while giving App
  real presenter-side delta consumers instead of always trusting full section
  payloads. Smoke coverage now proves full row/section snapshots plus later
  row/cell/section deltas can rebuild complete presenter frames without
  live-world reads or lower-layer object comparisons. Remaining presenter work
  is broader packed world-chunk/panel payloads and panel-specific redraw
  specialization.
- Latest source-only mining save/replay hardening added the third real
  job-state payload restore slice. `runtime_snapshot.json` now carries
  Contracts-owned transport, mining, and craft jobs payloads: transport pending
  requests, active jobs, backlog entries, and scheduling hints; mining active,
  backlog, deferred stairwell, reserved-tile, and recent-completion rows; plus
  craft active and backlog jobs. Runtime maps those payloads through
  Runtime-owned document mappers/verifiers/restorers; Jobs owns the actual
  restore seams on `TransportJobExecutor`, `MiningJobExecutor`, and
  `CraftJobExecutor`, including rehydrating active worker movement paths where
  needed; and full restore now accepts supported transport/mining/craft
  payloads. `RuntimeSaveFormat.CurrentVersion` was bumped to `5` in that batch
  because the runtime snapshot document schema included mining job payload rows;
  the later content-catalog persistence batch bumped it again to `6`.
- Latest source-only documentation/progress audit re-read the active
  `docs/architecture` and `docs/planning` sets, the documentation index, and the
  archive index after the mining save/restore hardening. No additional active
  document was moved to archive: the remaining architecture/planning files are
  current implementation maps, normative target contracts, active refactor
  guidance, rules, lessons, or optimization backlog. Follow-up wording cleanup
  aligned the active save/replay docs with the current transport/mining/craft
  job-state restore support instead of the older "restore seam still missing"
  phrasing. The strict total refactor-program estimate is now about 97%
  source-only pending the next build/test verification.
- Latest source-only Runtime migration-plan hardening added
  `RuntimeSaveSlotMigrationTransformRegistry`. `RuntimeSaveSlotMigrationPlanBuilder`
  now delegates required transform-id construction and coverage checks to that
  Runtime-owned registry instead of hardcoding `CanMigrate: false` locally. The
  registry was intentionally empty in that pass, so older slots failed closed;
  the later `slot:0->1` batch registered the first concrete slot-metadata
  transform while keeping unsupported Runtime snapshot schema paths fail-closed.
- Latest source-only documentation/progress audit re-read the active
  `docs/architecture` and `docs/planning` sets, the documentation index, and the
  archive index after the save/restore hardening. No additional active document
  was moved to archive: the remaining architecture/planning files are current
  implementation maps, normative target contracts, active refactor guidance,
  rules, lessons, or optimization backlog. The strict total refactor-program
  estimate was about 95% source-only pending build/test verification,
  and the remaining save/replay wording now names non-empty job-state payload
  restore and migration transforms instead of already-completed item-location
  restore slices.
- Latest source-only Runtime restore-plan hardening made Jobs checkpoint policy
  record-count aware. Runtime save manifests now write transport/mining/craft
  job record counts beside the checkpoint hashes, and
  `RuntimeSaveJobStateRestorePolicy` no longer treats empty counted Jobs
  checkpoint sections as blockers for full world + RNG + pending-command
  restore. Later payload slices made `jobs.transport`, `jobs.mining`, and
  `jobs.craft` restorable when verified payloads are present.
- Latest source-only Simulation save/restore hardening added installed item
  placement and item-local reservation-token support to the non-ground
  item-location slice. `WorldSavePayloadRestorer` now validates installed item
  anchor/z/rotation plus token claimant/count rows through the Simulation payload
  boundary and allows restore when those values match the saved world. Smoke
  coverage now round-trips ground, contained, carried, equipped, installed, and
  token-reserved items by hash, while malformed installed placement and malformed
  item-local reservation tokens are rejected with structured world-payload issues.
- Latest architecture/planning progress audit re-read the active non-archive
  `docs/architecture` and `docs/planning` sets plus the documentation index and
  archive index. The active architecture/planning files now fall into current
  implementation maps, normative target contracts, active refactor guidance,
  rules, lessons, or optimization backlog. No further active document was moved
  to archive in this pass; the stale/completed planning, status, audit,
  reference, concurrency-research, and runtime-propagation documents had already
  been moved under `docs/archive`. The follow-up cleanup removed the obsolete
  `ItemInstance.IsReserved`, `ReservedBy`, and `IsCarried` compatibility
  properties entirely now that active business reads/writes use the newer
  carried/equipped/reservation-token paths.
- Latest source-only Runtime save-slot hardening added a Contracts-owned
  `RuntimeSaveSlotMigrationPlanData` read model plus Runtime-owned
  `RuntimeSaveSlotMigrationPlanBuilder`. Slot inspection now reports
  validation, compatibility, migration plan, restore plan, and readable
  manifest data together. Older slot/runtime snapshot versions still fail
  closed, but Runtime now reports required transform ids through the
  Runtime-owned transform registry and a structured `slot.migration` blocking
  issue instead of leaving save UI/debug surfaces to infer migration state from
  raw manifest versions.
- Latest documentation audit re-read the active `docs/architecture` and
  `docs/planning` sets plus the UI docs they point at. No additional active
  planning/architecture document was moved to archive: the remaining files are
  current implementation maps, normative target contracts, rules, or active
  backlogs. The pass corrected stale UI/optimization current-state wording in
  `docs/INDEX.md`, `docs/ui/UI_AND_INPUT_MODEL.md`,
  `docs/ui/UI_SPEC.md`, `docs/ui/UI_SYSTEM.md`, and
  `docs/planning/OPTIMIZATION_SUGGESTION.md` so their current-state notes now
  match the Runtime/Contracts snapshot boundary work.
- Latest source-only Simulation save/restore hardening added the first
  non-ground item-location restore slice. `WorldSavePayloadRestorer` now allows
  carried items when `CarriedBy` references a creature present in the same world
  payload, rejects empty or missing carrier references, and continues to fail
  closed for installed item locations and item-local reservation tokens.
  Existing payload round-trip smoke coverage now restores a carried item by
  hash and proves missing-carrier payloads are rejected at the Simulation save
  boundary.
- Latest source-only Runtime presenter-delta hardening extended the
  Contracts-owned map-viewport delta from changed cells to changed cells,
  changed rows, and changed screen regions. `SimulationMapViewportDeltaData` is
  now schema v3 and carries `MapViewportRowDeltaView` rows plus
  `MapViewportRegionDeltaView` fixed-size screen regions with Runtime-authored
  canonical payload hashes. `RuntimeFrameSnapshotPublisher.MapDelta` builds
  rows and regions from final screen-cell values after terrain/entity overlay
  collapse, publishes all rows/regions for a full snapshot/request change,
  publishes no rows/regions when a same-request base is unchanged, and compares
  row/region hashes at the Runtime publication boundary rather than asking App
  renderers to compare live world or lower-layer objects. Smoke coverage now
  locks changed-cell, changed-row, and changed-region behavior.
- Latest documentation hygiene archived the stale June interview briefing at
  `docs/archive/reference/HUMANFORTRESS_INTERVIEW_BRIEFING.md`. The active
  documentation index no longer exposes it as current reference material
  because its App/Jobs/Runtime directory examples and several architecture
  caveats were superseded by the later Runtime/App/Jobs/Simulation boundary
  hardening. Current ownership guidance remains in `GAME_ARCHITECTURE.md`, the
  master plan, rules, and this batch progress log.
- Latest source-only Simulation diff-applicator hardening split the general
  `SimulationDiffApplicator` by operation and lookup responsibility. The main
  file now owns only `ApplyAll` dispatch plus logging; terrain mutation,
  occupant ejection, and dirty propagation live in
  `SimulationDiffApplicator.Terrain.cs`; item move/carry/uncarry and stack
  merge behavior live in `SimulationDiffApplicator.Items.cs`; creature movement
  lives in `SimulationDiffApplicator.Creatures.cs`; and encoded target,
  entity-key-first lookup, guarded legacy fallback, and target decoding live in
  `SimulationDiffApplicator.Targets.cs`. Architecture smoke now locks this
  split, and deterministic authority smoke reads the target lookup partial
  directly so manager-owned entity-key lookup cannot drift back into a mixed
  applicator file.
- Latest architecture/planning documentation audit re-read the active
  `docs/architecture` and `docs/planning` sets after the archive cleanup. No
  additional active architecture/planning file was moved to archive: the
  remaining files are either current implementation maps, active refactor
  guidance, target contracts, or optimization backlogs. Stale current-state
  wording was corrected instead: `GAME_STATE_FLOW.md` now describes the
  Runtime-owned save/load vertical slice, `DIFF_LOG_AND_MERGE_STRATEGIES.md`
  now reflects explicit `SystemOrder`/`LocalSeq`/`EntityKey` diff ordering, and
  the master plan now rates strict total refactor-program progress at about
  93-94% with remaining work concentrated in Simulation diff applicator
  hardening, broader Runtime presenter deltas, save/replay migrations and
  unsupported restore slices, diagnostics/debug polish, movement ownership, and
  focused test-project extraction.
- Latest source-only Runtime frame snapshot hardening added
  `RuntimeFrameSnapshotPublisher` under `HumanFortress.Runtime.Snapshots`.
  `FortressRuntimeSessionCore.Snapshots.Frame` now creates viewport/UI request
  keys and delegates frame/overlay publication to that Runtime-owned publisher
  instead of directly authoring metadata and invoking aggregate snapshot facade
  builders. The publisher caches the latest matching tick/request frame DTOs,
  owns `SimulationSnapshotMetadata.Current(...)` for frame/overlay aggregates,
  and is invalidated on new session creation, fortress world fill, fortress
  play startup, and supported save-world restore. Architecture smoke tests now
  lock the publisher boundary and its focused Runtime snapshot namespace. A
  follow-up source-only hardening added Contracts-owned
  `SimulationSnapshotPublicationData` to frame/overlay DTOs. The publisher now
  attaches a Runtime-authored surface id plus stable `ReplayHashBuilder`
  request hash, and smoke coverage verifies same-request hash stability plus
  different-request hash changes for overlay and render frame DTOs. Because the
  current scheduler advances `CurrentTick` only after post-tick, the frame port
  disables request-cache reuse while the background scheduler is running; paused,
  stopped, and manual-tick reads can still reuse the latest matching request.
- Follow-up source-only Runtime frame publisher split keeps
  `RuntimeFrameSnapshotPublisher.cs` focused on frame publication entrypoints,
  with cache reads/writes, invalidation, and published-frame identity records
  in `RuntimeFrameSnapshotPublisher.State.cs`. Presenter frame payload identity
  now lives in `RuntimeFrameSnapshotPublisher.Presenter.cs`,
  UI overlay section deltas live in
  `RuntimeFrameSnapshotPublisher.OverlayDelta.cs`, map viewport changed-cell/
  row/region deltas live in `RuntimeFrameSnapshotPublisher.MapDelta.cs`, and request hash
  primitives live in `RuntimeFrameSnapshotPublisher.RequestHash.cs`.
  Architecture smoke locks the partial namespace split and checks that the main
  publisher file does not drift back into cache state,
  changed-cell/row/screen-region/section/payload hashing, or invalidation.
- Follow-up source-only Runtime presenter-frame hardening added
  Contracts-owned `SimulationSnapshotPresenterFrameData` to the frame/overlay
  aggregate DTOs. `RuntimeFrameSnapshotPublisher` now assigns a Runtime-owned
  publication sequence, computes a stable full-snapshot payload hash, exposes
  the current transfer mode as `full-snapshot`, and records a delta-base
  payload hash only when consecutive uncached publications have the same
  surface/request. Cache hits return the same presenter frame identity, and
  session/world/restore invalidation clears diff bases so App cannot
  accidentally compare frames across different runtime worlds. This established
  the Runtime/Contracts presenter identity boundary before the changed-cell
  slice below.
- Follow-up source-only Runtime map-viewport delta hardening added the first
  real changed-cell presenter slice, and later presenter-delta hardening
  extended it with changed rows and screen regions. `SimulationMapViewportData` now carries a
  Contracts-owned `SimulationMapViewportDeltaData` with canonical payload hash,
  base hash, `CanApplyToBase`, changed final screen cells, and changed final
  screen rows/regions.
  `RuntimeFrameSnapshotPublisher` collapses terrain/entity overlay cells to
  final screen-cell values, computes the canonical viewport payload hash, emits
  a full changed-cell set when the request/base changes, emits an empty delta
  for unchanged same-request frames, and clears the map delta base on
  invalidation. App can keep drawing the full `Cells` list while future
  presenters consume the Runtime-authored delta; App still does not compute
  frame hashes or live-world diffs.
- Follow-up source-only Runtime UI-overlay delta hardening broadened the same
  presenter boundary to overlay/panel sections. `SimulationUiOverlayFrameData`
  now carries a Contracts-owned `SimulationUiOverlayFrameDeltaData` with
  section ids, per-section payload hashes, an aggregate section payload hash,
  base hash, `CanApplyToBase`, and changed section ids. Runtime hashes the
  build catalog, jobs, workshop, stockpile, zone, management drawer, work
  drawer, and debug menu sections inside `RuntimeFrameSnapshotPublisher`, emits
  all section ids when the request/base changes, emits an empty section delta
  for unchanged same-request frames, and clears the section base on
  invalidation. App can continue drawing the full overlay DTOs while future
  presenters skip unchanged sections without comparing live Runtime objects.
- Latest source-only Runtime save-slot hardening added a minimal slot manifest
  layer without moving save/load authority into App. Contracts now define
  `RuntimeSaveSlotFormat` and `RuntimeSaveSlotManifestData` for the current
  `slot_manifest.json` + `runtime_snapshot.json` directory shape. Runtime owns
  the manifest builder/codec/verifier, writes both files through
  `RuntimeSaveSnapshotDocumentStore.WriteAtomic(...)`, validates both files
  through `ValidateDirectory(...)`, and routes directory restore preflight
  through that validation path so tampered or missing slot manifests produce
  structured `slot.manifest` issues. App still sees save/load as Runtime port
  directory operations rather than Runtime-internal codec/store/restorer types.
- Follow-up source-only save-slot compatibility hardening added
  Contracts-owned `RuntimeSaveSlotCompatibilityData` plus a Runtime-owned
  `RuntimeSaveSlotCompatibilityPolicy`. Current slot/runtime snapshot versions
  are classified as readable, older versions are reported as
  `MigrationRequired` and at that point failed closed until later batches added
  the Runtime migration transform registry and concrete `slot:0->1`,
  `runtime_snapshot:4->5`, and `runtime_snapshot:5->6` transforms. Future/
  unknown slot kinds or unsupported Runtime snapshot document names still fail
  closed. Directory validation now emits structured `slot.compatibility` issues
  from Runtime, keeping App out of version/migration policy.
- Follow-up source-only save-slot inspection hardening added Contracts-owned
  `RuntimeSaveSlotInspectionData` and Runtime-owned
  `RuntimeSaveSnapshotDocumentStore.InspectDirectory(...)`. The inspection path
  reads the slot document/manifest, applies the Runtime compatibility policy,
  computes Runtime migration and restore plans, and returns validation,
  compatibility, migration-plan, restore-plan, and manifest data as a read
  model for future save UI/debug surfaces. `ValidateDirectory(...)` now reuses
  inspection so validation and inspection cannot drift, and the internal full
  save session port exposes
  `InspectSaveSnapshotDirectory(...)` without making Runtime save codec/store
  helpers public or App-owned.
- Follow-up source-only restore-plan hardening added Contracts-owned
  `RuntimeSaveSlotRestorePlanData` plus Runtime-owned
  `RuntimeSaveSlotRestorePlanBuilder`. A valid compatible slot advertises the
  current pending-command/world restore modes and a guarded full-restore mode,
  while malformed,
  migration-required, future, incomplete, or job-state-payload-incomplete slots
  return blocked plans with structured issues. This keeps future save UI/debug
  surfaces from inferring loadability from raw validation fields or manifest
  sections.
- Follow-up source-only Runtime save store split kept
  `RuntimeSaveSnapshotDocumentStore.cs` as the public store entrypoint and moved
  slot inspection into `RuntimeSaveSnapshotDocumentStore.Inspection.cs` plus
  unchecked file reads/durable writes/failure helpers into
  `RuntimeSaveSnapshotDocumentStore.IO.cs`. Architecture smoke now locks the
  partial namespace split so the store does not become a mixed IO/validation
  /inspection God helper as save UI/read-model work grows.
- Latest documentation audit pass archived the dated external audit report at
  `docs/archive/plans/HumanFortress_审计报告_2026-07-07.md` and updated the
  current index/archive lists. Keep using the current architecture overview,
  master plan, rules, and this progress log as the live guidance; the archived
  audit remains historical evidence for why determinism, snapshot publication,
  save/replay, tests, and docs honesty are the remaining hardening priorities.
- Latest documentation hygiene archived three non-current documents:
  `docs/archive/plans/MILESTONE.md`,
  `docs/archive/architecture/CONCURRENCY_RESEARCH.md`, and
  `docs/archive/architecture/RUNTIME_PROPAGATION_REQUIREMENTS.md`.
  `docs/INDEX.md`, `docs/archive/README.md`, and
  optimization-source references now point at the archive paths so current
  architecture/planning directories contain only active status, rules, target
  contracts, and current backlog documents.
- Follow-up documentation hygiene moved the non-current status-snapshot pointer
  out of the active documentation tree to
  `docs/archive/status/STATUS_SNAPSHOT_POINTER.md`. Current docs now point
  directly at `docs/archive/status/README.md` for historical phase snapshots;
  `docs/planning` and `docs/architecture` were re-audited and remain active as
  the refactor plan, rules, current architecture map, or target contracts.
- Follow-up architecture/planning documentation audit kept the remaining
  current docs in place and corrected stale audit-era wording in
  `ARCHITECTURE_REFACTOR_MASTER_PLAN.md` and `GAME_ARCHITECTURE.md`: save/load
  is now documented as a Runtime-owned vertical slice rather than "not
  implemented", replay-facing Runtime command identity is documented as
  deterministic, and `FortressState` is documented as a mostly split shell with
  regression risk rather than the original all-owning God screen.
- Latest source-only Simulation replay hash split kept
  `WorldReplayHashBuilder.cs` focused on aggregate and section-hash
  orchestration. Terrain hashing, item/creature entity hashing, reservation
  hashing, stockpile-zone hashing, and shared primitive helpers now live in
  focused `WorldReplayHashBuilder.*.cs` partials under
  `HumanFortress.Simulation.Replay`. Architecture smoke locks the namespace and
  partial split, and the deterministic stockpile ordering smoke now reads the
  stockpile replay-hash partial directly.
- Follow-up source-only Simulation mining planner split kept `MiningSystem.cs`
  focused on planner state, scheduler identity, constructor, and the
  `PlannedDig` DTO. Tick/drain/outbox behavior, scanner/cursor advancement,
  cancellation queries, planner helpers, and active designation cursor state
  now live in focused `MiningSystem.*.cs` / `MiningActiveDesignation.cs` files.
  Architecture smoke locks the split so mining designation scans do not drift
  back into one mixed planner file.
- Latest build/test verified determinism hardening added the first full
  Runtime simulation-loop determinism smoke. `CoreRuntimeSmokeTests` now creates
  two equivalent Runtime-composed fortress sessions, loads content, fills the
  same deterministic terrain, attaches the real Runtime systems without
  starting the background scheduler thread, seeds initial workers plus one
  startup auto-dig command, advances 200 manual ticks, and compares sampled
  `WorldReplayHashBuilder` plus `RuntimeReplayCheckpointHashBuilder` hashes.
  `SimulationRuntimeHost` gained an internal `AttachForManualTicks(...)` seam
  so deterministic tests can reuse production Runtime composition and pipeline
  attachment without wall-clock scheduler timing.
- The same determinism batch changed `TickScheduler` read phase from
  `Parallel.ForEach` to deterministic system-order iteration and added a
  `SystemId` tie-break for equal-priority systems. This follows the audit report
  recommendation to prefer construction-level determinism over the current
  near-zero read-phase parallelism until the project has real chunk-partitioned
  scheduling. `DeterministicAuthoritySmokeTests` now locks both the scheduler
  ordering policy and the manual Runtime tick host seam. Non-archive
  architecture/spec/reference docs now distinguish the current deterministic
  coarse system read order from the future chunk-partitioned read parallelism
  target. The deterministic authority smoke also checks that the default
  `tests/HumanFortress.App.Tests` runner still executes the full Runtime
  simulation-loop determinism gate and that the gate compares both world and
  Runtime checkpoint replay hashes. The sampled checkpoint rows also expose
  RNG hashes and executed/pending command journal hashes/counts, so the smoke
  explicitly verifies that the startup auto-dig command entered the executed
  replay journal and left no pending command drift by each sample point.
- Verification passed with
  `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false`
  and
  `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll`.
- The same verified run covered restore hardening that moved more supported world
  payload validation to the `HumanFortress.Simulation.Save` boundary before a
  new `World` is created. `WorldSavePayloadRestorer.Validation` now rejects
  duplicate stockpile zone ids, duplicate stockpile member chunks, missing
  stockpile filter arrays, duplicate mining order ids, and obvious out-of-bounds
  chunk/rectangle/Z payloads before manager-level restore runs. Manager restore
  methods still keep their own atomic validation, but the payload contract now
  fails closed at the authoritative Simulation save boundary instead of relying
  on partially restored manager handoff behavior. Placeable restore validation
  is now split into a terrain-dependent preflight and a validated restore pass,
  so bad placeable payloads are rejected after terrain reconstruction but before
  item/creature/reservation/stockpile manager restore work starts.
  Item/creature/reservation/stockpile restore issues now also stop the flow
  before validated placeables and active orders are written into the restored
  world.
  `CoreRuntimeSmokeTests` covers duplicate stockpile/order/placeable payloads,
  and the deterministic architecture smoke test locks the new payload-boundary
  validators and placeable preflight order.
- Latest source-only/no-build Simulation save cleanup split the large
  `WorldSavePayloadRestorer.Validation` partial into focused validation
  partials for entity/reservation checks, stockpile zone/filter checks, active
  order checks, and shared world geometry/string helpers. The main validation
  file now owns payload/count/chunk validation plus supported-section
  orchestration, and architecture/determinism smoke coverage was updated to
  lock the new focused file set under `HumanFortress.Simulation.Save`.
- Follow-up source-only/no-build Jobs transport cleanup split the Jobs-owned
  `TransportJobExecutor` into focused partials. The main file now keeps
  constructor state and dependency assembly, while read/intake, write ticks,
  debug/replay snapshots, scheduling hints, and small helper lookups live in
  separate `HumanFortress.Jobs.Transport` files. Architecture smoke coverage
  now locks that split so long-horizon transport state ownership stays in Jobs
  without turning the executor back into a single mixed file.
- Latest source-only/no-build boundary hardening split Runtime command replay
  decoding by command domain. `RuntimeCommandReplayFactory.cs` now keeps the
  replay dispatch, payload version validation, and primitive binary readers,
  while orders, zones/stockpiles, profession/workshop, and debug spawn decoders
  live in focused `RuntimeCommandReplayFactory.*.cs` partials under
  `HumanFortress.Runtime.Commands`. Architecture smoke coverage now locks those
  replay factory files to the focused Runtime command namespace.
- The same source-only/no-build batch split the structured
  `ContentRegistry` God Object into responsibility-focused Content partials:
  the main file keeps load orchestration, registry state, catalog queries,
  schema loading, and save snapshot compatibility; material/terrain parsing,
  biome/geology parsing plus deterministic geology indexing, and
  tuning/zones/alias/validation/hash behavior live in separate
  `ContentRegistry.*.cs` files under `HumanFortress.Content.Registry`.
  Architecture smoke coverage now also locks these Content partials so the
  structured registry does not drift back into one large implementation file.
- Follow-up source-only/no-build Content definition hardening split
  `CoreDataRegistryLoader` by catalog family. The main loader now only owns
  core-data directory orchestration and shared JSON document options, while
  construction/workshop parsing, recipe parsing, and common JSON helper methods
  live in focused `CoreDataRegistryLoader.*.cs` partials under
  `HumanFortress.Content.Definitions`. Architecture smoke coverage now locks
  those partials separately from `ContentRegistry`, preserving legacy workshop
  and recipe compatibility without concentrating every data/core parser in one
  Content implementation file.
- The same source-only/no-build hardening split the concrete
  `FortressGenerator` by generation phase while keeping generation authority in
  `HumanFortress.WorldGen.Implementation`. The main file now keeps constructor
  state and the ordered fortress generation pipeline; cavern carving,
  strata/surface filling, ore placement, and tuning JSON helper behavior live
  in focused `FortressGenerator.*.cs` partials. Architecture smoke coverage now
  locks those files so future world-generation tuning changes do not grow a new
  monolithic generator or move terrain-generation policy into Runtime/App.
- Follow-up source-only/no-build Content definition hardening split
  `ItemDefinitionCatalogLoader` by responsibility. The main loader now keeps
  load result/options and deterministic file traversal; legacy furniture/items
  JSON parsing plus stack/placeable/effects mapping lives in
  `ItemDefinitionCatalogLoader.Furniture.cs`, and item normalization/name
  enrichment/validation lives in `ItemDefinitionCatalogLoader.Validation.cs`.
  Architecture smoke coverage now locks the item definition loader split so
  static item catalog compatibility does not grow another mixed Content parser.
- The same Content definition hardening split
  `CreatureDefinitionCatalogLoader` so file traversal/load result handling
  stays separate from creature stat validation. Architecture smoke coverage now
  locks both static item and creature definition loaders as Content-owned
  partials.
- Follow-up source-only/no-build Simulation save hardening split
  `WorldSavePayloadBuilder` and `WorldSavePayloadRestorer` by authoritative
  payload section. The builder main file now only orchestrates snapshot and
  section assembly, with metadata/terrain, entities, stockpiles, placeables,
  orders, and common conversion helpers in focused partials. The restorer main
  file now only owns the restore flow, terrain reconstruction, supported-section
  restore order, and final replay-hash check, with payload validation,
  placeable restore/validation, and conversion/failure helpers in focused
  partials. Architecture smoke coverage now locks these files under
  `HumanFortress.Simulation.Save` so world save authority stays in Simulation
  and does not collapse back into a single save/load God Object.
- Follow-up source-only/no-build Simulation manager hardening split two
  authoritative managers that were still acting as God Objects.
  `ItemManager` now keeps only state, dependency binding, deterministic item
  GUID allocation, counts, and logging in the main file; catalog access,
  position indexing, queries, stack/move/remove mutations, spawn behavior, and
  save/restore validation/conversion live in focused partials under
  `HumanFortress.Simulation.Items`. `CreatureManager` now keeps only
  definition snapshot state, runtime instance state, deterministic creature
  GUID allocation, world binding, counts, and logging in the main file;
  creature catalog access, runtime instance queries, spawn behavior, and
  save/restore validation/conversion live in focused partials under
  `HumanFortress.Simulation.Creatures`. `OrdersManager` now keeps only queue
  state and logging in the main file; haul, mining, construction/buildable, and
  save/restore validation/conversion behavior live in focused partials under
  `HumanFortress.Simulation.Orders`. Architecture smoke coverage now locks
  these managers as partials so authoritative Simulation state owners do not
  regress into single-file service locators.
- Follow-up source-only/no-build Simulation placeable hardening split
  `PlaceableInstance` into state plus item-install/uninstall and construction
  factory partials, with `PlaceableKind`, `DoorState`, and
  `ConstructionSiteState` in focused type files under
  `HumanFortress.Simulation.Placeables`. `PlaceableManager` now separates
  collision checks, cross-chunk placement, removal, and affected-chunk queries
  into focused partials. `ChunkPlaceableData` now keeps authoritative
  owned/external-ref storage separate from derived `FurnitureCell` sync/
  unsync behavior. Architecture smoke coverage locks these files so placeable
  authority, derived cache rebuild policy, and cross-chunk world mutation stay
  in Simulation instead of drifting into Runtime/App or collapsing back into one
  mixed placeable file.
- Follow-up source-only/no-build Runtime session cleanup introduced an
  internal `FortressRuntimeSession` handle under `HumanFortress.Runtime.Session`
  for the active fortress-specific `SimulationRuntimeSession` generic
  composition. `FortressRuntimeSessionCore`, replay checkpoint hashing, and the
  Runtime snapshot facade now pass that named internal handle instead of
  repeating the long constructed generic type or relying on a fragile
  namespace-colliding `Session` alias. Architecture smoke coverage locks the
  handle to the focused Runtime session namespace.
- Follow-up source-only/no-build determinism hardening addressed the audit's
  Navigation P0s. `DeterministicAStar` and `PathService` no longer use
  `Stopwatch`/wall-clock milliseconds to decide path results; pathfinding now
  relies on deterministic node and per-tick path request budgets. Runtime also
  owns a `RuntimePathServiceRegistry` under `HumanFortress.Runtime.Navigation`
  and invalidates registered path caches whenever post-tick dirty navigation
  chunks are rebuilt. Deterministic smoke coverage now fails if pathfinding
  reintroduces wall-clock budget checks or if the Runtime post-tick rebuild
  path stops invalidating active path caches.
- Follow-up source-only/no-build determinism hardening removed the hidden
  `SanitizeSystem` write counter and derives the low-frequency safety-net
  schedule from the authoritative tick. `DiffTargetEncoding` now reads GUID
  projections with explicit little-endian byte order for cross-platform replay
  stability. General `DiffLog` operations now carry explicit numeric
  `SystemOrder` and `LocalSeq` fields, precompute their coarse sort key at
  construction time, and no longer hardcode `Jobs.*` string precedence inside
  Core. Entity-scoped `DiffTarget` operations now also carry an optional
  64-bit `EntityKey`; merge/apply paths prefer that wider key while retaining
  the legacy 32-bit `EntityId` field for compatibility. Jobs-owned diff
  emitters provide the current job system order and wider entity keys from
  `HumanFortress.Jobs.Diff`, preserving the old conflict policy without making
  Core depend on Jobs names. The Navigation movement executor contract and
  concrete movement state map now also use the wider `ulong` entity key, and
  mining/transport/craft assignment/active-runner paths derive that key from
  worker GUIDs instead of truncating to `uint`. Remaining work is to remove the
  legacy 32-bit handle from old compatibility surfaces and non-stockpile typed
  item handles once their callers are ready.
- Follow-up source-only/no-build determinism hardening moved stockpile
  item-index identity to the same wider entity-key model. `StockpileDiff`,
  `StockpileMessage`, `ItemStackRef`, and `ChunkStockpileData` now carry
  `ulong` item handles; transport delivery/pickup, construction consumption,
  and craft consumption stockpile diffs emit `DiffTargetEncoding.EntityKey(...)`
  instead of truncated `SignedEntityId(...)`; and `StockpileDiffApplicator`
  projects items through `ItemManager.GetInstanceByEntityKey(...)`.
  Stockpile diff merging now uses an explicit comparer over cell, priority,
  operation, zone, item key, system id, and local sequence rather than treating
  the old packed sort key as authoritative. Smoke coverage now locks the
  stockpile item-index entity-key path and a legacy 32-bit collision regression.
- The same source-only/no-build cleanup added GUID overloads for
  `DiffTargetEncoding.ForWorldCell(...)`, `DiffTargetEncoding.ForChunkLocal(...)`,
  and `WorldCellTarget.ToDiffTarget(...)`. Jobs diff producers now pass the
  source GUID once and let the encoding seam populate both the legacy
  compatibility `EntityId` and the wider `EntityKey`, reducing the chance of
  new producers filling only one side of the transition.
- The same Simulation-owned entity lookup hardening added `ItemManager` and
  `CreatureManager` entity-key indexes. Item keys are maintained across spawn,
  split, merge, remove, and world payload restore paths; creature keys are
  maintained across spawn and world payload restore paths. Stockpile and general
  diff applicators can now resolve wider item/creature entity keys without
  scanning every live instance, and smoke coverage locks both behavioral index
  sync and the indexed lookup source shape. The legacy 32-bit entity-id fallback
  now also uses manager-owned deterministic indexes that sort GUID collisions
  instead of depending on dictionary enumeration order; this remains a
  compatibility path, not new authoritative identity. The manager-level
  `GetAllInstances()` snapshots for items and creatures now also return
  GUID-ordered lists, so ordinary read callers do not accidentally inherit
  dictionary value order.
- The same deterministic snapshot hardening extended stable owner ordering to
  zone and stockpile read models. `ZoneManager` now returns zone definitions by
  content id and zone instances by zone id; chunk zone/stockpile shard snapshots
  return shard id order; `StockpileManager.GetAllZones()` returns zone id order;
  and `ChunkStockpileData` returns item entity-key indexes in numeric key order.
  `ZoneInstance` and `StockpileZone` also expose stable Z/Y/X member-chunk
  snapshots so delete/apply, save/replay, stockpile destination lookup, and
  Runtime overlay/detail builders no longer enumerate immutable hash sets
  directly. A smoke regression now feeds intentionally out-of-order zone, shard,
  stockpile, member-chunk, and item-index data through these owner APIs, while
  deterministic authority coverage blocks broad dictionary-value snapshots from
  returning unordered lists on those paths.
- Transport request queue shard snapshots now expose shard ids and shard counts
  in stable shard-id order. This keeps scheduler/debug read models from
  inheriting `_shards` dictionary order when showing or hashing shard-level
  transport state.
- Chunk lifecycle heat-score decay now iterates heat-score chunk keys in stable
  Z/Y/X order instead of raw dictionary key order, keeping the transitional LOD
  manager from growing another nondeterministic owner loop as it becomes more
  behavior-affecting.
- World and placeable owner snapshots now sort at the source. `World.GetAllChunks()`,
  `GetActiveChunks()`, dirty-navigation chunk drains, and `UpdateLOD(...)`
  use stable Z/Y/X chunk order; `ChunkPlaceableData.GetAllOwnedPlaceables()`
  and `GetOwnedPlaceableSnapshot()` use authoritative local-cell order; and
  `PlaceableManager.GetAffectedChunks(...)` returns stable Z/Y/X chunk keys.
  Stockpile create-zone application now applies accepted cells in the same
  stable chunk order used during validation.
- Construction material requirement/delivery dictionaries now expose
  Simulation-owned sorted snapshots through `ConstructionSiteState`. Jobs
  construction progress, material counting/consumption, the legacy
  construction-materials planner, and Runtime workshop material progress now use
  those snapshots instead of enumerating `MaterialsRequired.Keys` directly.
  Construction nearby-item diagnostics and Runtime stockpile filter summaries
  now also sort tag/id/material sets before formatting preview text.
- Follow-up source-only/no-build hardening extended that construction material
  rule to completion consumption, completion/progress diagnostics, world save
  payload mapping, and placeable replay hashing. `ConstructionCompletionCoordinator`,
  `ConstructionSiteProgress`, `ConstructionCompletionApplier`,
  `WorldSavePayloadBuilder.Placeables`, `PlaceablesReplayHashBuilder`, and the
  Runtime workshop material progress path now read material requirement/delivery
  data through `ConstructionSiteState` snapshot rows, leaving direct
  `MaterialsRequired`/`MaterialsDelivered` access to Simulation owner,
  restore, and factory initialization paths.
- Content-owned runtime zone definition snapshots now materialize
  `ZoneDefinitions` in stable zone-id order before Runtime applies them into the
  Simulation world. This avoids depending on the structured registry dictionary
  enumeration order during session content bootstrap. `BiomeTemplateRegistry`
  also now uses stable template-id tie-breaks for equal-priority templates and
  stable id ordering for template id/broad template snapshots.
- Cross-module catalog and compatibility RNG snapshots now also sort at their
  source boundaries. Item/creature/construction/recipe catalog stores expose
  broad definition lists and index arrays in content-id/category order;
  `MaterialRegistry` returns material category/tag/name snapshots in stable
  material-id/name order; and `RngStreamManager.SaveStates()` /
  dictionary-restore compatibility paths iterate streams by stream name.
- Runtime save snapshot packages now also canonicalize RNG stream rows by
  stream name at the `RuntimeSaveSnapshotData` boundary. World save payload
  restore applies terrain chunks in stable Z/Y/X order and normalizes
  string-int material rows plus item/placeable improvement rows during
  conversion. Runtime save document verification now rejects missing, blank, or
  duplicate manifest section names before section lookups, so malformed
  external save documents cannot depend on first-match manifest enumeration.
- Follow-up source-only/no-build save hardening made Runtime manifest sections
  schema-strict instead of just structurally present. The save document verifier
  now owns the recognized section set, validates fortress-mode requirement flags,
  requires every known section to be present, and rejects unknown section names,
  negative record counts, present sections with blank hashes, and absent
  sections that still carry hash/count metadata. Runtime save smoke coverage
  now tampers those cases explicitly so future save UI work cannot accidentally
  accept ambiguous external documents through App.
- The same save hardening extended `RuntimeSaveSnapshotData` construction
  checks from RNG-only to command replay journals. Executed and pending command
  record counts and replay hashes now must match the manifest checkpoint before
  a Runtime save snapshot package can be created, so malformed in-memory
  snapshot assembly fails before document serialization.
- Follow-up source-only/no-build cleanup centralized Runtime save manifest
  section names and fortress-mode requirements in `RuntimeSaveManifestSections`.
  `RuntimeSaveManifestBuilder` now creates sections through that schema, and the
  verifier validates external documents against the same definitions instead of
  maintaining a duplicate known-section table.
- Follow-up source-only/no-build Simulation save hardening added pre-restore
  duplicate GUID validation for item and creature payload rows. This keeps
  ambiguous external world payloads from partially restoring terrain before
  manager-level entity restore rejects duplicate identities.
- Transport debug shard data now crosses Jobs -> Runtime -> Contracts as
  explicit shard-count row DTOs rather than dictionary snapshots. The transport
  request queue still owns shard-count lookup internally, but Runtime/UI
  diagnostics consume stable shard-id ordered rows and no longer depend on
  dictionary enumeration for display previews.
- Item position indexes now sort GUIDs at insertion, and `MergeStacksAt(...)`
  groups definitions plus merge targets in stable key/GUID order. This keeps
  post-move stack consolidation from depending on position-index insertion
  order when several same-definition stacks exist on a tile.
- Jobs profession assignment now explicitly sorts creature initialization,
  roster construction, and candidate-selection ties by worker GUID. This keeps
  same-weight/same-distance worker choice from inheriting the snapshot order of
  the Simulation creature manager, and deterministic smoke coverage locks the
  tie-breaks.
- The same source-only/no-build batch added a conservative live-tile safety
  guard while the larger frame-snapshot/SoA work remains pending. `Chunk`
  now synchronizes `GetTile(...)`, `SetTile(...)`, and `GetTilesCopy()` through
  the same write lock so readers cannot observe torn multi-field `TileBase`
  values during live world reads. Deterministic smoke coverage now locks this
  temporary guard until the tile store is replaced by packed storage,
  seqlocks, or an immutable frame snapshot pipeline.
- App world-generation random seed creation now converts random bytes with
  explicit little-endian encoding instead of `BitConverter.ToUInt32`, keeping
  the chosen seed portable once the byte stream has been generated.
- Latest source-only/no-build Movement/EntityKey hardening made entity-scoped
  generic diff ordering prefer the wider `EffectiveEntityKey` before the legacy
  32-bit `EntityId` compatibility tie-break. Core now owns the encoded-target
  GUID helper so production `SignedEntityId(...)` projection stays scoped to
  `DiffTargetEncoding`; direct `DiffTarget` construction is likewise
  concentrated behind Core encoding helpers, while Simulation `WorldCellTarget`
  callers can build GUID-backed diff targets without manually composing legacy
  ids. The unused mixed `ToDiffTarget(int entityId, ulong entityKey)` helper was
  removed so new Simulation/Jobs code either creates non-entity cell targets or
  passes the authoritative GUID. Regression
  coverage now proves entity-scoped diff sorting follows the 64-bit key when
  legacy id order conflicts with the wider key, and deterministic smoke tests
  lock the compatibility boundary. Simulation diff application now also routes
  item/creature lookup through entity-key-first helper methods and only uses
  guarded legacy id fallback when a diff target does not carry an entity key.
- Latest source-only/no-build hardening centralized typed mutation diff sort-key
  packing in `SimulationDiffSortKeys`, keeping enqueue-ordered command-edit
  diffs distinct from spatial/priority item/stockpile-style diffs without
  changing existing ordering semantics. The same batch moved App runtime session
  composition settings/callbacks into `GameStateRuntimeConfiguration`, so
  `GameStateRuntimeCoordinator` is responsible for binding Runtime ports while
  App startup/headless callers pass one explicit configuration object instead of
  duplicating logger/content callback wiring.
- The latest no-build batch also added executable architecture guardrails to the
  formal test runner: `ArchitectureBoundarySmokeTests` scans active App source
  for forbidden lower implementation module imports and old mixed runtime facade
  names, enforces that ordinary App.Runtime imports only appear in adapter/port
  composition files, and verifies the approved production project-reference
  graph. `DeterministicAuthoritySmokeTests` scans save/replay/hash authority
  paths for object `GetHashCode()`, dictionary `Keys`/`Values` view iteration,
  and production `Guid.NewGuid()` use, turning recurring manual `rg` checks into
  regression coverage.
- Follow-up source-only/no-build diagnostics boundary hardening moved
  diagnostic event/sink/level contracts and the transitional process-wide
  `DiagnosticHub` from `HumanFortress.Core.Diagnostics` to
  `HumanFortress.Contracts.Diagnostics`. App and Content now depend on the
  diagnostics contract surface without referencing `HumanFortress.Core`; Content
  now references only Contracts, and the follow-up App/Content boundary hardening
  below removes App's remaining Content project reference as well.
- Follow-up source-only/no-build App/Content boundary hardening moved the
  App-facing content load issue/path/report DTOs into
  `HumanFortress.Contracts.Content.Loading` and added
  `FortressRuntimeContentLoader` as the Runtime-owned content startup/file
  location facade. App startup now validates content through Runtime and UI
  registry-file lookup enters through an App-owned `AppContentFileLocator`
  wrapper instead of importing `HumanFortress.Content.Loading`. The App project
  reference graph is now Contracts + Runtime only; the architecture smoke test
  forbids App source/project references to Content as well as Core, Simulation,
  Jobs, Navigation, and WorldGen, and it allowlists direct `HumanFortress.Runtime`
  imports to startup/adapter/content-location boundaries. The Content-owned
  `FortressContentLoader` and its full load package are now internal/friend
  implementation surfaces for Runtime/tests; external callers use Runtime's
  facade and Contracts-owned load reports.
- The same no-build hardening made `RuntimeSaveSnapshotDocumentCodec` an
  internal/friend Runtime implementation detail and extended architecture smoke
  coverage so the codec cannot become App-facing API again. Save document DTOs
  remain Contracts-owned, while JSON codec/store/restore behavior stays behind
  Runtime ports.
- Follow-up source-only/no-build Runtime session port hardening split the
  public App-facing session aggregate from the full internal Runtime session
  aggregate. `FortressRuntimeSessionFactory.Create(...)` now returns
  `IFortressRuntimeAppSessionPorts`, which excludes save/replay checkpoint
  ports; full save/replay ports remain internal/friend-only through
  `CreateFull(...)` for Runtime tests. `FortressRuntimeAccess` and
  `GameStateRuntimeCoordinator` consume only the App-facing aggregate, so App no
  longer receives save/load/replay methods just because the session core
  implements them internally.
- The architecture smoke test now also locks public surface: Content, Jobs,
  Navigation, Simulation, and WorldGen must expose no public implementation
  types; App may expose only `Program`; Runtime's public surface is restricted
  to the approved factories/bootstrap helpers plus App-facing session port
  interfaces; and Core's public foundation surface is restricted to approved
  command, event, tick, deterministic RNG, replay hash, diff, and world
  primitive types. Contracts is also locked as a
  dependency-free project with no package, framework, or project references.
  Contracts source plus Runtime's public session port/factory files are also
  scanned for presentation primitive leaks such as SadRogue, SadConsole,
  MonoGame, and XNA names.
  This turns the current "internal/friend by default unless explicitly
  foundational or contractual" rule into executable coverage.
- Follow-up source-only/no-build architecture hardening extended the smoke
  runner with an explicit source-import direction matrix for every production
  project and an exact `InternalsVisibleTo` friend-assembly matrix. Project
  references alone no longer define the boundary: the test now also fails if,
  for example, Contracts imports implementation namespaces, lower
  implementation projects import App/Runtime, App imports lower implementation
  namespaces, or a new friend assembly is added without changing the approved
  access model. The same cleanup moved `FixedPoint.cs` into the physical
  `Contracts/Content/Registry` folder to match its
  `HumanFortress.Contracts.Content.Registry` namespace.
- Follow-up source-only/no-build Navigation namespace cleanup moved concrete
  pathfinding/cache/navigation-manager implementation sources under
  `HumanFortress.Navigation.Implementation`. Public navigation DTOs and
  interfaces remain in `HumanFortress.Contracts.Navigation`; Runtime is the
  only production composition boundary importing the concrete implementation
  namespace. The architecture smoke test now fails if concrete Navigation
  implementation files drift back to the compatibility root namespace.
- Follow-up source-only/no-build WorldGen namespace cleanup moved concrete
  generated-world service/data/factory, world generator, fortress generator/map,
  and stage implementation sources under
  `HumanFortress.WorldGen.Implementation`. Public world-generation DTOs and
  service contracts remain in `HumanFortress.Contracts.WorldGen`; Runtime is
  still the ordinary production composition boundary, with architecture smoke
  coverage preventing concrete WorldGen sources from drifting back to the root
  namespace and limiting Runtime imports of the concrete implementation to the
  world-generation composition files.
- Follow-up source-only/no-build Jobs namespace alignment moved Jobs
  implementation sources out of the root `HumanFortress.Jobs` namespace and
  into module-directory namespaces: `HumanFortress.Jobs.Configuration`,
  `.Diff`, `.Logging`, `.Orchestration`, `.Profession`, `.Safety`, plus the
  existing `.Mining`, `.Construction`, `.Craft`, `.Transport`, and `.Replay`
  slices. Runtime job wrappers/composition and friend tests now import the
  focused Jobs implementation namespaces explicitly. The architecture smoke
  runner now fails if any active Jobs implementation source drifts back to the
  root Jobs namespace or if source files import the root Jobs namespace instead
  of focused Jobs module namespaces.
- Follow-up source-only/no-build Runtime namespace and physical module
  alignment split internal Runtime helpers out of the root Runtime namespace
  and root folder. Runtime composition helpers now live under
  `HumanFortress.Runtime.Composition` and `Runtime/Composition`, active-session
  content bootstrap adapters under `HumanFortress.Runtime.Content` and
  `Runtime/Content`, Simulation-backed navigation adapters under
  `HumanFortress.Runtime.Navigation` and `Runtime/Navigation`, startup/autodig
  helpers under `HumanFortress.Runtime.Startup` and `Runtime/Startup`, and
  command execution/target interfaces and implementations under
  `HumanFortress.Runtime.Commands` in `Runtime/Commands/Execution` and
  `Runtime/Commands/Targets`. A follow-up in the same no-build line moved the
  host/tick pipeline into `HumanFortress.Runtime.Host`, runtime session handles
  and services into `HumanFortress.Runtime.Session`, mutation log bundles into
  `HumanFortress.Runtime.Diff`, Runtime geometry adapters into
  `HumanFortress.Runtime.Geometry`, fortress-generation runner glue into
  `HumanFortress.Runtime.WorldGeneration`, and stockpile preset mapping into
  `HumanFortress.Runtime.Content`. The Runtime root namespace is now closer to
  the public session factories/ports plus `FortressRuntimeSessionCore` facade
  partials, while architecture smoke coverage locks the new helper paths and
  namespaces.
- Source-only verification for the latest namespace hardening passed:
  `git diff --check`; active App source still has no lower implementation
  imports; implementation projects still expose no public implementation types;
  Contracts remains dependency-free; Contracts plus Runtime public session
  port/factory files still have no presentation primitive tokens; WorldGen root
  namespace/project-name replacement side effects were not present; Runtime
  imports concrete WorldGen implementation only from the approved composition
  files; active source/test files no longer import the root Jobs namespace; and
  the new Runtime composition/content/navigation/startup/command/host/session/
  diff/geometry/world-generation helper files no longer use the root Runtime
  namespace. Build/test were intentionally not run in this batch.
- Latest source-only/no-build hardening moved nearest-embark lookup behind the
  `IGeneratedWorldData` contract instead of letting `WorldMapState` scan
  generated-world tile views directly. `GeneratedWorldData` now owns the
  nearest embarkable tile query, `FortressSessionContext` exposes it as a
  session query, and the generated-world payload remains hidden behind
  session-owned methods; `CurrentWorld` is no longer exposed to session
  initializer code. The same batch lowered the current low-elevation embark
  threshold from `0.30` to `0.25`, preserving lake/ocean exclusion while allowing
  low grassland sites, and added Phase B/core smoke coverage for the query and
  threshold.
- The same source-only/no-build hardening split
  `RuntimeSaveSnapshotDocumentVerifier` into partial validators for root
  orchestration, world payload validation, command journal validation, and RNG
  section validation. This keeps Runtime save document validation in Runtime
  while reducing a large mixed verifier without changing the save document
  contract or validation behavior. The world payload validator is further split
  between payload identity, row/count integrity, and manifest-section hash/count
  checks so future save slices do not enlarge a single mixed world verifier file.
- Follow-up source-only/no-build Runtime boundary hardening split
  `FortressRuntimeSessionCore.Replay.cs` so replay checkpointing, save document
  port methods, save snapshot document/manifest building, and world/full restore
  sequencing now live in focused Runtime partials. It also split
  `FortressRuntimeSessionPorts.cs` into lifecycle/bootstrap, read, snapshot,
  replay, save, and command port files while keeping the aggregate
  `IFortressRuntimeSessionPorts` composition unchanged. Directory-based save
  snapshot validation/restore now share one Runtime helper for read-failure
  conversion, so each port method maps the same validation failure DTO instead
  of carrying duplicate file-read exception handling.
- Follow-up App runtime facade hardening removed the old broad fortress-play
  runtime aggregate from the screen boundary. `IFortressPlayRuntimeHost` now
  creates `FortressStateRuntimePorts` directly, `FortressState` receives only
  that role-grouped port package, and the package is split into
  input/view/bootstrap runtime roles. The unused App-level save facade was
  removed from `FortressRuntimeAccess`; Runtime still owns save snapshot ports
  internally, but App no longer exposes save/load operations to fortress-play UI
  until a real save UI boundary exists.
- Follow-up source-only/no-build App composition hardening moved the concrete
  view/input/session runtime-port group shapes out of `States` and into the
  modules that consume them: `App.Rendering` owns `FortressViewRuntimePorts`,
  `App.Input` owns keyboard/map input runtime port groups, and `App.Session`
  owns `FortressSessionRuntimePorts`. `FortressStateRuntimePorts` is now only a
  state-level bundle of those module-owned port groups, while
  `GameStateRuntimeCoordinator` remains the single creation point that binds the
  concrete `FortressRuntimeAccess` adapter to each role.
- Latest source-only/no-build App facade hardening split mixed placement,
  debug-spawn, and workshop-panel runtime interfaces into separate query and
  command roles. `App.Input` now wraps those roles behind module-owned
  keyboard/map/placement runtime ports, named by
  `FortressInputRuntimePortDependencies`; placement, debug-spawn,
  map-click/tile-inspection, workshop-panel, navigation-debug, simulation
  control, and build-catalog input controllers consume Input-owned ports rather
  than `App.Runtime` interfaces directly. `App.Rendering` also wraps read and
  UI-input runtime roles behind view-owned runtime ports, so ordinary renderers
  no longer receive raw runtime facade interfaces. Startup logging callback
  binding now lives in `App.Startup` instead of `App.Runtime`, so `Program`
  no longer imports the runtime facade namespace.
- Follow-up source-only/no-build session boundary hardening made
  `FortressSessionRuntimePorts` the only `App.Session` type that directly wraps
  the bootstrap runtime facade. `FortressSessionLoader`,
  `FortressSessionInitializer`, and `FortressSessionRuntimeBootstrapper` now use
  session-owned semantic methods/DTOs for generation, world availability,
  startup auto-dig, and workshop completion notifications instead of importing
  `App.Runtime` role interfaces directly.
- Follow-up source-only/no-build App.Runtime adapter cleanup split
  `FortressRuntimeAccess.Queries.cs` into role-specific query partials for
  build catalog, debug spawn, map inspection, navigation debug, UI-input,
  workshop panel, and world availability. The concrete adapter still delegates
  to Runtime session ports, but query methods now sit beside the App role that
  consumes them instead of one mixed facade file.
- Follow-up source-only/no-build Runtime command-context hardening split
  `SimulationCommandExecutionContext` into focused partials for clock updates,
  read-only `ISimulationContext` forwarding, and command-target role exposure.
  The command-stage model is unchanged, but the transitional all-target runtime
  command context is now physically organized around its actual roles instead of
  one mixed implementation file.
- Latest build/test-verified Runtime/world restore batch extends the Runtime
  save document from hash-only RNG summary to primitive RNG stream payload rows,
  binds the `rng` manifest section to checkpoint hash/count validation, and adds
  a Runtime-owned staged restore entrypoint that composes supported world
  payload restore, RNG stream restore, and pending command replay restore in the
  correct session-reset order; later job-state policy hardening now blocks
  full-restore claims when present Jobs checkpoint sections have no payload
  restorer. The same batch extends `Contracts.Simulation.Save` world
  payloads to owned placeables/workshop state and restores those rows through
  Simulation-owned validation plus `PlaceableManager.PlacePlaceable(...)`, so
  derived furniture cells/cross-chunk refs are rebuilt rather than persisted as
  authority. App still only sees narrow directory-level save access and
  structured Contracts result DTOs; it does not map RNG rows, world payload rows,
  or Core command replay records. `RuntimeSaveFormat.CurrentVersion` was bumped
  to `2` in that batch because RNG stream rows became a required document
  payload, and
  `WorldSavePayloadFormat.CurrentVersion` is now `2` because placeable/workshop
  rows are required world payload data.
- Verification passed: `git diff --check`; App boundary scan found no Runtime
  save internals, Core command records, or Core RNG types in active App source;
  save/replay scan found no `GetHashCode()` or dictionary enumeration hazards in
  active save/replay builders; `/opt/homebrew/opt/dotnet@8/bin/dotnet build
  HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false
  -p:UseAppHost=false` passed with `0 Warning(s), 0 Error(s)`;
  `/opt/homebrew/opt/dotnet@8/bin/dotnet exec
  tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll`
  passed, including RNG document mapper/restorer, Runtime full restore coverage,
  and owned placeable/workshop world payload hash round-trip coverage; and
  strict headless init with `/opt/homebrew/opt/dotnet@8/bin/dotnet
  exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only
  --strict-content --content-warnings-as-errors` passed.
- Latest previously verified world payload/restore follow-up extended `Contracts.Simulation.Save.WorldSavePayloadData` beyond terrain chunks and ground item rows to include creature instances, global item/creature reservations, stockpile zone definitions, and active mining/haul/construction/buildable order designations. `WorldSavePayloadBuilder` now exports these slices in stable replay-hash order, and Simulation managers own internal restore entrypoints for rebuilding them from Contracts DTOs without involving App.
- Runtime world restore now calls the Simulation-owned supported-section restorer. The slice supports ground items, contained items with payload-local acyclic item container references, carried/equipped items with payload-local creature owners, installed items with valid placement anchors, item-local reservation tokens with valid claimant/count rows, creatures, reservations, stockpile zones, owned placeables/workshop state, and active orders. This is still a staged vertical slice toward the `SAVE_FORMAT.md` target, not full chunk-sharded persistence.
- Verification passed: `git diff --check`; App/Contracts boundary scan found no App use of Simulation save builder/restorer or world payload DTO types; replay/save scan found no `GetHashCode()` or dictionary enumeration hazards in active save/replay builders; `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` passed with `0 Warning(s), 0 Error(s)`; `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll` passed, including supported-section world payload hash restore; and strict headless init with `/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors` passed.
- The latest previously verified world payload/restore batch added the `Contracts.Simulation.Save.WorldSavePayloadData` DTO family, Simulation-owned `WorldSavePayloadBuilder` and first-pass `WorldSavePayloadRestorer`, and Runtime save document integration. Runtime save documents now carry manifest + world payload + command replay records; document validation checks world payload hash/counts against manifest sections; Runtime can restore the world payload from a document or save directory by composing a fresh Runtime session from the restored `World`.
- Verification passed: `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` with `0 Warning(s), 0 Error(s)`; and `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll` passed, including terrain world payload hash round-trip, unsupported non-terrain restore rejection, Runtime document/directory world restore, and existing transport/construction/craft, mining/items/diff, core runtime smoke, and Phase A-D coverage.
- Latest verified save-slot boundary follow-up extended the Runtime save snapshot port with directory-level validation, App-facing save access, and structured directory restore failures. `RuntimeSaveSnapshotDocumentStore` became Runtime-internal; App could pass a save slot directory through a narrow save facade without seeing the Runtime file store, document mapper, replay restorer, Runtime command helpers, or Core command replay records. Later App composition hardening removed that unused fortress-play save facade again; Runtime still owns the save ports internally until a real save UI boundary is introduced.
- The same verified batch removes a stockpile mailbox determinism hazard by replacing `SourceChunk.GetHashCode()` in the drain sort key with a stable primitive hash over explicit chunk coordinates, updates `STOCKPILE_SPEC.md` to avoid `GetHashCode()` as an ordering rule, and adds smoke coverage for stable stockpile message drain keys.
- Verification passed: `git diff --check`; App/Contracts boundary scan found no `HumanFortress.Runtime.Save`, `HumanFortress.Runtime.Commands`, `HumanFortress.Runtime.Replay`, `HumanFortress.Core.Commands`, `RuntimeSaveSnapshotDocumentStore`, or `RuntimeSaveSnapshotDocumentCodec` usage in active App/Contracts source; deterministic-order scan found no production `GetHashCode()` uses except the ordinary `ChunkKey` value-type override; `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` passed with `0 Warning(s), 0 Error(s)`; `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll` passed; and strict headless init with `/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors` passed.
- Latest save snapshot hardening is build/test verified. It added Core-owned command replay journal hashing plus a `CommandQueueReplaySnapshot` that captures pending and executed command records without exposing live `ICommand` objects. Runtime replay checkpoints now include both `commands.executed` and `commands.pending` hashes/counts, and the Runtime save manifest exposes those command sections without App touching `CommandQueue`.
- The same verified save snapshot hardening added Simulation-owned world save summary seams: `WorldReplayHashBuilder` now exposes section hashes, and `WorldSaveSnapshotBuilder` returns schema/version, world dimensions, section hashes, and section counts while keeping `World`/tile/item/placeable/order details inside Simulation. Runtime save manifests now include `world.terrain`, `world.items`, `world.creatures`, `world.reservations`, `world.stockpiles`, `world.placeables`, and `world.orders` sections with optional counts.
- Runtime now exposes save snapshot document ports for creating, validating, and restoring pending commands from a Contracts-owned document DTO. The internal package type can bridge Contracts-owned manifest data and Core-owned replay records, but App receives only the document shape; App remains responsible only for future slot selection/UI and user-facing errors, not for assembling save authority from live Runtime/Simulation objects. Runtime also owns the save document JSON codec, manifest/hash/count verifier, a first-pass durable document store for `runtime_snapshot.json`, and the internal document-to-`CommandReplayRecord` mapper/replay restorer for future load/replay restore.
- Verification passed: `git diff --check`; App boundary scan found no save/replay implementation usage in active App source; replay/save hash scan found no object `GetHashCode()`, dictionary key/value enumeration, dictionary declarations, or order-drain calls in production replay/save builders; `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` with `0 Warning(s), 0 Error(s)`; and `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll` passed the transport/construction/craft, mining/items/diff, core runtime smoke, and Phase A-D suites.
- Latest source-only replay registry batch added the Runtime-owned `RuntimeCommandReplayFactory` and shared Runtime command payload v1 header. Current Runtime command families now serialize versioned payloads, decode through the Runtime factory, reject unknown command types/unsupported versions/trailing bytes/malformed enum values/id mismatches, and preserve `RuntimeIdentifiedCommand` identity sequence through `CommandReplayRecord`.
- Latest source-only replay harness batch added `RuntimeCommandReplayRestorer` for batch-atomic Runtime replay restore and `ReplayHashBuilder` for stable canonical primitive checkpoint hashes. Restore now decodes all records before replacing pending commands and advances the Runtime command identity cursor after successful restore. Smoke coverage compares direct Runtime order commands against replay-restored command records using an authoritative order hash, and verifies failed restore leaves existing pending commands intact.
- Follow-up replay hash boundary hardening moved the order designation snapshot hash into Simulation as `OrdersReplayHashBuilder`, so field selection for authoritative order replay checkpoints is no longer private test code. Tests now consume the Simulation-owned hash seam while Core still owns only primitive encoding.
- Latest source-only world replay hash hardening added Simulation-owned `WorldReplayHashBuilder`, aggregating world dimensions, existing chunk terrain tile primitives, item instances, creature instances, item/creature reservations, global stockpile zone config, and active order designations while excluding rebuildable stockpile chunk indexes and other derived caches. Replay smoke coverage now compares direct command execution and replay-restored execution with the aggregate world hash and verifies equivalent hand-built worlds hash identically while terrain changes alter the hash.
- Follow-up source-only placeable replay hash hardening added Simulation-owned `PlaceablesReplayHashBuilder` and wired it into `WorldReplayHashBuilder`. The aggregate replay checkpoint now covers owned placeable identity/location/footprint/source/effects/condition, construction-site material/progress state, door state, workshop settings, workshop queue entries, and the workshop queue identity counter while still excluding derived furniture cells/cross-chunk external refs. Smoke coverage now mutates workshop queue state to prove the aggregate world hash changes and verifies repeated hashing does not consume queue state.
- Follow-up RNG replay hash hardening added canonical `RngStreamStateSnapshot` rows on `RngStreamManager`, restore-from-snapshot validation, `ReplayHashBuilder.AddUInt32(...)`, and Core-owned `RngReplayHashBuilder`. Runtime session services now own a session RNG stream manager, session reset clears materialized streams, and Runtime replay checkpoint hashing includes the session RNG stream hash. RNG smoke coverage verifies sorted stream snapshots, order-independent stream hash input, hash changes after RNG consumption, restore-to-hash equivalence, and Runtime checkpoint hash changes when session RNG streams advance.
- Follow-up source-only transport job replay hardening added canonical pending-request snapshots in Simulation.Jobs, Jobs-owned transport active/backlog replay snapshots, Runtime wrapper pass-through, and Jobs-owned `TransportReplayHashBuilder`. Transport regression smoke coverage now verifies stable pending/active/backlog/hint hashes and proves the hash changes when pending work moves into active/backlog ownership or scheduling hints change.
- Follow-up source-only mining job replay hardening added Jobs-owned mining active/backlog/deferred-stairwell/reserved-tile/recent-completion replay snapshots, Runtime wrapper pass-through, and Jobs-owned `MiningReplayHashBuilder`. Core runtime smoke coverage now verifies stable mining job replay hashes and hash changes when active mining progress changes.
- Follow-up source-only craft job replay hardening added Jobs-owned craft active/backlog replay snapshots, Runtime wrapper pass-through, and Jobs-owned `CraftReplayHashBuilder`. Core runtime smoke coverage now verifies stable craft job replay hashes and hash changes when active craft work ticks change. Construction job long-horizon progress remains covered through placeable construction-site state in `PlaceablesReplayHashBuilder` because the current construction executor is scan-based and owns no active/backlog queue.
- Latest Runtime checkpoint/snapshot boundary hardening added Contracts-owned `SimulationSnapshotMetadata`/schema version fields to the aggregate frame/UI overlay DTOs and a Contracts-owned `RuntimeReplayCheckpointData` DTO for structured replay checkpoints. App no longer passes UI tick into Runtime overlay snapshot construction; `FortressRuntimeSessionCore` authors snapshot metadata from the session tick scheduler. Runtime now exposes a replay-checkpoint port backed by `RuntimeReplayCheckpointHashBuilder`, which returns an aggregate hash plus section hashes for authoritative world state, session RNG streams, transport pending/executor state, mining job state, and craft job state.
- Latest replay/jobs checkpoint verification passed: `git diff --check`; App boundary scan found no replay/hash implementation usage in active App source; replay hash builder scan found no object `GetHashCode()`, dictionary key/value enumeration, dictionary declarations, or order-drain calls in production replay builders; `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` with `0 Warning(s), 0 Error(s)`; and `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll` passed including transport, mining, craft, Runtime replay checkpoint, and aggregate snapshot metadata smoke coverage.
- Latest world replay hash verification passed: `git diff --check`; App boundary scan found no Runtime command/replay implementation usage in active App source; replay hash builder scan found no object `GetHashCode()`, dictionary key/value enumeration, or order-drain calls in production replay builders; `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` with `0 Warning(s), 0 Error(s)`; `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll`; and strict headless init with `/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors`.
- Follow-up replay hash boundary verification passed after the Simulation-owned order hash move: `git diff --check`; App boundary scan found no replay/command implementation usage in active App source; `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` with `0 Warning(s), 0 Error(s)`; `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll`; and strict headless init with `/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors`.
- Latest replay harness batch verification passed: `git diff --check`; App boundary scan found no `HumanFortress.Runtime.Commands`, replay restorer/factory, payload helper, or `Core.Commands` usage in active App source; Runtime command payload scan found no old `new BinaryWriter(ms)` or empty payload serializers; `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` with `0 Warning(s), 0 Error(s)`; `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll`; and strict headless init with `/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors`.
- Latest replay registry batch verification passed: `git diff --check`; App boundary scan found no `HumanFortress.Runtime.Commands`, `RuntimeCommandReplayFactory`, `RuntimeCommandPayload`, or `Core.Commands` usage in active App source; Runtime command payload scan found no old `new BinaryWriter(ms)` or empty payload serializers; `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` with `0 Warning(s), 0 Error(s)`; `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll`; and strict headless init with `/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors`.
- Latest source-only save/replay design batch added `docs/architecture/SAVE_REPLAY_ARCHITECTURE.md`, clarifying that full save/load should not be implemented until replay and authoritative world snapshot seams are stable. The document positions `SAVE_FORMAT.md` as the long-term on-disk target and defines the staged path: command replay log MVP, replay harness, minimal world snapshot, hash round trip, then atomic slot UI. Core now exposes `ICommandReplayFactory` as the replay decode seam; Runtime should own concrete command replay registries.
- Latest replay boundary batch verification passed: `git diff --check`; `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` with `0 Warning(s), 0 Error(s)`; `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll`; and strict headless init with `/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors`.
- Latest source-only replay boundary hardening added Core `CommandReplayRecord` and `CommandQueue.GetExecutedCommandRecords()` as the immutable save/replay persistence view over executed commands. The existing `GetExecutedCommands()` remains an in-memory compatibility/diagnostic view. Smoke coverage now verifies record field preservation, payload defensive copies, and executed-record clearing.
- Latest source-only replay hardening corrected `CommandQueue.RestoreCommands(...)`: restored commands now re-enter pending replay order without being added to executed history until they actually run. Restore validation now happens before clearing existing pending state, so invalid restore input cannot partially wipe the queue. `CoreRuntimeSmokeTests` covers restored due/future command behavior, duplicate executed-history pollution, and atomic invalid-restore rejection.
- Current architecture-hardening batch verification passed: `git diff --check`; `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` with `0 Warning(s), 0 Error(s)`; `/opt/homebrew/opt/dotnet@8/bin/dotnet exec tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll`; and strict headless init with `/opt/homebrew/opt/dotnet@8/bin/dotnet exec src/HumanFortress.App/bin/Debug/net8.0/HumanFortress.App.dll --init-only --strict-content --content-warnings-as-errors`.
- Latest source-only/no-build replay/boundary hardening removed production `Guid.NewGuid()` use from Runtime/App/Simulation/Jobs/Content/WorldGen paths. Runtime command ids are now derived deterministically from tick/type/payload, and normal Runtime session enqueue wraps commands in `RuntimeIdentifiedCommand` with a session-owned deterministic command identity sequence reset by `RuntimeSessionServices`. `UpdateWorkshopQueueCommand.Serialize()` now writes its real mutation payload instead of returning an empty byte array.
- Follow-up replay hardening fixed construction order command identity so material tag filters are serialized into the command payload hash. The smoke suite now proves identical construction filters with different tag order produce the same id, while different material tags produce different ids.
- Follow-up command-context hardening deleted the remaining internal all-target `IRuntimeCommandExecutionContext` aggregate. `SimulationCommandStage`, `SimulationTickPipeline`, and `SimulationRuntimeHostCore` now depend on `ISimulationContext` plus the separate clock role, while individual Runtime commands request only their precise target role through `RuntimeCommandContext.Require<T>()`.
- Follow-up diagnostics hardening adds the command queue sequence to command failure logs so deterministic/stable command ids can still be disambiguated when duplicate payload commands fail. The App work/zone UI also no longer renders visible placeholder gameplay settings when no Runtime snapshot data exists.
- The same source-only/no-build batch fixed mining rectangle scans to treat SadRogue `Rectangle.MaxExtentX/MaxExtentY` as inclusive in both `MiningOrderRules` and `MiningSystem`, with smoke coverage for one-cell mining rectangles. A follow-up static scan found no remaining active exclusive `MaxExtent*` rectangle loops.
- The same source-only/no-build batch narrowed additional false-public implementation members inside internal Simulation/App types: order manager enqueue/drain/snapshot helpers, mining/construction planner handoff helpers, terrain/placeable/world safety helpers, App startup runners, and App world-generation factory access now sit behind internal/friend or App-owned provider surfaces. The old dormant `WorkDrawerOverlay` compatibility hook was deleted.
- Latest stockpile/transport hardening is build verified: `/opt/homebrew/opt/dotnet@8/bin/dotnet build HumanFortress.sln --no-restore -m:1 -v:minimal -p:RunAnalyzers=false -p:UseAppHost=false` passed with `0 Warning(s), 0 Error(s)`. This batch connected stockpile item indexes to the Jobs/Transport pipeline instead of stockpile-local mutation. Runtime now injects the session-owned `StockpileDiffLog` into transport, construction, and craft diff emitters. Transport delivery/pickup/cancellation queues stockpile place/remove/release diffs, construction/craft full-stack consumption queues stockpile remove diffs, and those item-index diffs carry projected `ItemStackRef` payloads so tag indexes can be cleaned even after `ItemsDiffApplicator` deletes the item. `StockpileWorldQueries` centralizes stockpile cell/destination lookup, and `TransportDestinationValidator` revalidates destination zone filters against item projections.
- Latest stockpile reservation follow-up is build verified with the same fast solution build command. `HaulingSystem` now receives the session-owned `StockpileDiffLog`, tracks same-tick planned reservations when selecting destinations, and queues `ReserveSlot` diffs next to `TransportRequest` enqueueing. Transport pickup was narrowed so only `ToStockpile` jobs short-circuit when the item is already stored; non-stockpile jobs can now pick up from stockpile cells and emit stockpile remove-index diffs. Smoke/regression coverage now locks capacity-capped hauling reservation and non-stockpile pickup from stockpiles.
- Latest stockpile transport/UI/rendering hardening is build/test verified: the fast solution build passed with `0 Warning(s), 0 Error(s)`, and `/opt/homebrew/opt/dotnet@8/bin/dotnet tests/HumanFortress.App.Tests/bin/Debug/net8.0/HumanFortress.App.Tests.dll` passed the Transport/Construction/Craft, Mining/Items/Diff, Core Runtime Smoke, and Phase A-D suites. This batch removed the unused legacy `StockpileHaulingBroker`, removed the `CreateHaulJob` diff op, and made `ITransportIntake.Enqueue(...)` report whether a new pending request was added. `TransportRequestQueue` now keeps one pending transport intent per item, updates its shard index when same-destination requests merge, and preserves the earlier destination when competing destinations target the same item. `HaulingSystem` now only writes `ReserveSlot` when the queue accepts a new pending request and tracks planned item ids inside a read tick; smoke/regression coverage locks duplicate pending transport and queue shard-index behavior.
- The same build/test-verified stockpile UI boundary hardening now exposes content-backed stockpile preset menu options through `SimulationUiOverlayFrameData` as Contracts DTOs. Runtime builds the menu from `FortressRuntimeStockpilePresetCatalog`, and App `StockpileUI` applies that DTO each frame instead of hardcoding preset ids or reading `stockpile_presets.json`.
- The same verified batch removed the older `HumanFortress.Simulation.Rendering.RenderSnapshot` / `RenderSnapshotBuilder` implementation after active App rendering moved to Runtime/Contracts snapshot DTOs. PhaseTests now validate the Runtime frame snapshot port instead of constructing a second Simulation-owned render snapshot model.
- The same verified batch fixed inclusive rectangle scans in `ItemManager.GetGroundItemsIn(...)`; SadRogue `Rectangle.MaxExtentX/MaxExtentY` is treated as inclusive, and the hauling reservation smoke now covers one-cell-high haul rectangles.
- Latest source-only/no-build stockpile/content hardening moved stockpile preset JSON parsing behind `HumanFortress.Content.Definitions.StockpilePresetLoader` and `HumanFortress.Contracts.Content.Registry.StockpilePresetDefinition`. Runtime now maps those contract presets into Simulation `StockpileFilter` instances, and stockpile create-zone diffs carry filter/priority data through `StockpileCreateZoneData` so the post-tick `StockpileDiffApplicator` applies preset rules authoritatively.
- The same source-only/no-build stockpile item hardening added a Simulation-owned `StockpileItemProjection` seam. Stockpile filters, the active `HaulingSystem` destination selection path, and stockpile item/tag index updates now use projected item definition id/tags/materials instead of local placeholder item definitions or empty tag placeholders; smoke coverage locks filter matching and idempotent stockpile item index updates.
- Latest source-only/no-build determinism hardening replaced `System.Random` usage in `MiningDropResolver` with `DeterministicRng`, removed direct `new Random()` calls from world-generation UI actions, and changed `WorldParams.Default` to a fixed seed. App's explicit random world seed/parameter button now uses a centralized `WorldGenerationSettingsDefaults` cryptographic seed source, while simulation/job random branches should continue to use deterministic RNG streams.
- Latest source-only/no-build module-boundary hardening removed App's direct `HumanFortress.WorldGen` project reference. App world-generation screens now create `IWorldGenerationService` through `FortressRuntimeWorldGenerationFactory`, while the concrete `WorldGenerationServiceFactory` in `HumanFortress.WorldGen` is internal/friend-only and Runtime remains the composition boundary for WorldGen-backed services.
- The same source-only/no-build diff-pipeline cleanup removed the unused chunk-local `StockpileDiffApplicator.ApplyDiffs(...)` entry and made stockpile application enter through `ApplyAll(world, diffs)` only. `SimulationDiffApplicator` now exposes internal apply/logging members rather than public members on an internal implementation type.
- Latest source-only/no-build Runtime session hardening grouped the per-session scheduler, command queue, event bus, main diff log, items diff log, and typed command mutation bundle into `RuntimeSessionServices`. The active Runtime path now creates `SimulationRuntimeSessionFactory` and `SimulationRuntimeHost` from that service bundle, and session reset clears the entire `RuntimeMutationDiffLogs` bundle rather than only the item diff log.
- The same source-only/no-build batch moved fortress generation request mapping/seed/content assembly into `RuntimeFortressGenerationRunner`, leaving `FortressRuntimeSessionCore` as a thin Runtime session port implementation for generation/fill.
- The same source-only/no-build batch tightened the stockpile command pipeline: stockpile delete diffs now carry only the zone id, and `StockpileDiffApplicator` resolves current member chunks from authoritative world state at post-tick apply time. Stockpile, item, creature, order, workshop, zone, and profession mutation diff logs/applicators now expose internal implementation members instead of public-looking APIs on internal types.
- Added source-only smoke coverage for typed diff ordering policy: order/zone/workshop command-edit diffs preserve enqueue sequence, while item/stockpile diffs keep spatial/priority ordering. This locks the current replay semantics so future cleanup does not accidentally normalize all typed diffs to one ordering rule.
- Latest source-only/no-build Runtime diff-pipeline hardening grouped command-side mutation logs into `RuntimeMutationDiffLogs`. `SimulationRuntimeCommandTargets`, `SimulationCommandExecutionContext`, `SimulationRuntimeHostCore`, and `SimulationTickPipeline` now share one mutation-log bundle instead of passing `ItemsDiffLog`, `CreaturesDiffLog`, `ProfessionAssignmentDiffLog`, `OrderDiffLog`, `WorkshopDiffLog`, `ZoneDiffLog`, and `StockpileDiffLog` as a long constructor chain. This reduces the chance that a command target queues into one log while the post-tick applicator drains another.
- Latest source-only/no-build FortressState hardening moved per-frame update sequencing into `FortressStateUpdateLoop` and UI tick ownership into `FortressUiTickCounter`. `FortressState.Update(...)` now delegates lifecycle work instead of directly mixing diagnostics, delayed initialization, focus recovery, mouse-wheel polling, and redraw orchestration.
- Latest source-only/no-build stockpile applicator hardening rechecks terrain eligibility after earlier post-tick terrain diffs before accepting queued stockpile cells. The smoke coverage now includes a same-tick terrain-change regression so command preflight cannot be the only source of truth.
- Latest source-only/no-build Runtime port hardening replaced SadRogue `Point`/`Rectangle` usage on public Runtime session ports with `HumanFortress.Contracts.Runtime` primitives (`RuntimePoint`, `RuntimeRect`) and a `RuntimeWorkshopCompletionNotification` DTO. App keeps SadRogue geometry inside App runtime/UI access interfaces and maps at `FortressRuntimeAccess`; Runtime core maps contract geometry back to its current internal SadRogue implementation details. The App bootstrap completion hook was also narrowed from a six-argument primitive delegate to an App-owned `FortressWorkshopCompletionNotification` DTO.
- Latest source-only/no-build architecture hardening removed the App-local `FortressRuntimeSessionController` passthrough. `GameStateRuntimeCoordinator` now creates the active Runtime session through `FortressRuntimeSessionFactory` and holds only `IFortressRuntimeSessionPorts`; `FortressRuntimeAccess` is an App-owned role adapter over Runtime session ports rather than concrete core methods. The App-local `FortressSimulationStatus` DTO/mapper was deleted; UI chrome now consumes `HumanFortress.Contracts.Runtime.SimulationStatus`.
- The same no-build hardening made `FortressRuntimeSessionCore` an internal Runtime implementation and moved its App-facing capabilities behind explicit Runtime session port interfaces. Runtime core command/snapshot/lifecycle methods are explicit interface implementations; ordinary public Runtime session construction is the factory plus ports.
- Simulation implementation types are now internal/friend by default. Runtime, Jobs, WorldGen, and tests retain friend access, while the stable external surface remains Contracts DTOs/interfaces plus Runtime snapshot/command ports; App still has no direct Simulation project reference.
- The same no-build batch narrowed Content's public API: `FortressRuntimeContentSnapshot`, `FortressRuntimeContentSnapshotLoader`, `CoreContentCatalogLoader`, core catalog load results, item/creature catalog load results, `RuntimeContentRegistryLoadResult`, and `ProfessionRegistryLoader` are now internal/friend surfaces for Content, Runtime, and tests. Public `FortressContentLoadResult` exposes summary counts and issues instead of the full core catalog snapshot.
- WorldGen public surface was tightened again: concrete world-generation service/data, the concrete WorldGen service factory, and fortress-map generation internals are internal/friend-only. The App-facing generated-world read path is the contract `IGeneratedWorldData`, App creates world generation through Runtime's `FortressRuntimeWorldGenerationFactory` returning `IWorldGenerationService`, and default world-generation seed/settings policy lives in App rather than Contracts.
- Runtime command execution no longer has the transitional `IRuntimeCommandTargetContext` aggregate. Host core, tick pipeline, and command stage pass only `ISimulationContext` plus the clock role, while individual commands require their precise role context through `RuntimeCommandContext.Require<T>()` and fail visibly instead of silently no-oping when a role is missing.
- Latest source-only/no-build command-context hardening split target-role aggregation out of `SimulationRuntimeContext`. `SimulationRuntimeContext` now owns only the simulation clock/read context, while the internal `SimulationCommandExecutionContext` composes `ISimulationContext` with narrow command target roles for command execution. A later follow-up removed the all-target aggregate interface entirely; the stage, pipeline, and host core depend only on `ISimulationContext`, and individual commands request their exact role context.
- Latest source-only/no-build profession command hardening moved profession weight updates onto a Runtime-owned `ProfessionAssignmentDiffLog` post-tick applicator path. `ProfessionAssignmentCommandTarget` no longer invokes the profession assignment callback during command execution; it queues weight diffs, and `SimulationTickPipeline.PostTick` applies them through the bound handler.
- Latest source-only/no-build order command hardening moved mining/haul/construction/buildable-construction order commands onto an `OrderDiffLog` post-tick applicator path. `OrderCommandTarget` no longer holds a concrete `World` or calls `world.Orders.Enqueue*`; it queues order diffs, and `SimulationTickPipeline.PostTick` applies them through `OrderDiffApplicator`.
- Latest source-only/no-build workshop command hardening moved workshop queue/settings commands onto a `WorkshopDiffLog` post-tick applicator path. `WorkshopQueueCommandTarget` no longer holds a concrete `World` or mutates `WorkshopState`; it validates recipe ids, queues workshop diffs, and `SimulationTickPipeline.PostTick` applies them through `WorkshopDiffApplicator` using the active construction catalog for worker-slot bounds.
- Latest source-only/no-build zone command hardening moved zone create/update/delete commands onto a `ZoneDiffLog` post-tick applicator path. `ZoneCommandTarget` no longer holds a concrete `World` or directly calls `world.Zones.*`; it queues zone diffs, and `SimulationTickPipeline.PostTick` applies them through `ZoneDiffApplicator`.
- Latest source-only/no-build stockpile delete hardening extends the same authoritative path to stockpile deletion. `DeleteStockpileCommand` now routes through `IStockpileCommandTarget`, queues `StockpileDiffLog.AddDeleteZone(...)`, and the post-tick `StockpileDiffApplicator` removes chunk shards plus the global zone. App stockpile remove input now enters `PlacementMode.StockpileDelete`, hit-tests through Runtime snapshot DTOs, and queues `QueueDeleteStockpile(...)` instead of touching live stockpile managers.
- Previous stockpile pipeline hardening is build/test verified. Stockpile creation is now command-driven through `StockpileDiffLog` and the post-tick `StockpileDiffApplicator`. `StockpileCommandTarget` now queues a create-zone diff instead of directly mutating `World.Stockpiles` or chunk shards; the applicator creates the global zone, assigns accepted cells, updates member chunks, clears the diff log, and rechecks overlap authoritatively so same-tick overlapping stockpile creates resolve deterministically.
- Latest source-only/no-build boundary hardening made the concrete structured `ContentRegistry` an internal Content implementation detail. External tests/App/Runtime no longer reference `ContentRegistry.Instance`; bootstrap/debug reads use `FortressRuntimeContentSnapshotLoader` and contract catalog interfaces, while pure construction/craft tests use explicit in-memory/empty catalogs where appropriate.
- The same no-build batch moved generated-world stable DTOs/settings/service contracts (`WorldGenerationSettings`, `WorldGenerationDifficulty`, `WorldMapTileView`, `WorldTileSnapshot`, `IWorldGenerationService`) into `HumanFortress.Contracts.WorldGen`, kept concrete `WorldGenerationService`/`GeneratedWorldData` internal in `HumanFortress.WorldGen`, and added an App-owned world-generation port/adapter so App screens/session flow no longer depend directly on concrete WorldGen service/data types outside `App.WorldGeneration`.
- Runtime command execution was narrowed again: `IRuntimeCommandClockContext` no longer inherits `ISimulationContext`, and the former all-target-sounding command binding was renamed to profession-specific `IRuntimeProfessionCommandBindings`.
- Latest source-only/no-build module-surface hardening made `HumanFortress.Jobs` and concrete `HumanFortress.Navigation` implementation sources expose no ordinary `public` implementation members: Jobs callback/profession adapters, diff emitters, mining assignment/drop resolution, craft planning, sanitizer, executor helpers, buffers, trackers, and Navigation view/path/movement/cache/heap internals now use internal or explicit-interface surfaces. Cross-module access remains through Contracts interfaces plus `InternalsVisibleTo` for Runtime/tests.
- The same no-build hardening moved `SimulationStatus` to `HumanFortress.Contracts.Runtime`, moved runtime geology/zone DTOs to `HumanFortress.Contracts.Content.Registry`, and split Runtime command execution context into clock and target ports so the tick pipeline does not require command-target access while commands cast only to the target context.
- Follow-up no-build hardening narrowed additional App/Content implementation surfaces: Content registry helpers (`MaterialRegistry`, `TerrainKindRegistry`, `GeologyRegistry`, `BiomeTemplateRegistry`, `AliasResolver`, and registry diagnostics) now expose internal/explicit-interface implementation members; `GameStateRuntimeCoordinator` no longer constructs Runtime options directly; the old App-local `FortressRuntimeSessionController` passthrough has since been removed; `FortressRuntimeAccess`, GameStateManager/state wrappers, navigator, lifecycle, registry, and screen presenter are App-internal surfaces with explicit role-interface entrypoints where needed.
- Latest source-only/no-build App/Runtime boundary hardening made App implementation modules internal by default: App screen states, session context/load/bootstrap helpers, input services, NavigationOverlay, UI types/commands/selection helpers, Logger, and diagnostic snapshots no longer expose public application implementation APIs. `Program` remains the App entry point.
- The same no-build batch moved public Runtime snapshot DTOs from `HumanFortress.Runtime.Snapshots` to `HumanFortress.Contracts.Runtime.Snapshots`. Runtime keeps the snapshot builders/facades under `HumanFortress.Runtime.Snapshots`, App now consumes snapshot DTO contracts, and snapshot DTOs now use project-owned `SnapshotColor` / `SnapshotPoint` primitives so Contracts does not reference `TheSadRogue.Primitives`.
- Latest source-only/no-build boundary cleanup moved static item/creature definition contracts to `HumanFortress.Contracts.Simulation.Items` and `HumanFortress.Contracts.Simulation.Creatures`, replacing the old compatibility `HumanFortress.Simulation.*` namespaces for those contract DTOs/catalog interfaces. Simulation managers still own runtime item/creature instances and consume the contract catalog snapshots.
- Runtime command dispatch no longer uses the old `IRuntimeCommandTargets` aggregate. `SimulationRuntimeCommandTargets` remains an internal implementation holder, `SimulationRuntimeContext` exposes explicit clock/execution roles, and individual Runtime commands cast only to narrow target contexts such as `IRuntimeOrderCommandTargetContext`, `IRuntimeZoneCommandTargetContext`, `IRuntimeWorkshopCommandTargetContext`, or spawn/profession/stockpile equivalents.
- Runtime command execution no longer uses the transitional `IRuntimeCommandContext` or `IRuntimeCommandTargetContext` aggregate interfaces. `SimulationRuntimeHostCore`, `SimulationTickPipeline`, and `SimulationCommandStage` receive an explicit clock role plus `ISimulationContext`, while `SimulationRuntimeContext` implements narrow command-target roles directly.
- Latest source-only/no-build cleanup further narrowed App runtime role injection and split focused files without changing module ownership: UI enum files, main-menu rendering panels, App input/view context factories, session world-map queries, Orders submenu rendering data, GameStateManager transition/shutdown files, Runtime placement-command mapping/material policy, world-content loader logging, async diagnostic lifecycle/worker code, map-viewport terrain glyph policy, legacy log category prefixes, runtime job-system groups, auto-dig command seeding, zone overlay detail/hit-test builders, tile-inspection builders, workshop snapshot material matching rules, tick-pipeline post-tick steps, and Runtime session core lifecycle/auto-dig/world-fill/workshop-notification files.
- Latest source-only/no-build Runtime bootstrap boundary cleanup moved fortress-map generation/fill out of App.Runtime and into Runtime's session implementation: `HumanFortress.Runtime` now references `HumanFortress.WorldGen`, Runtime/Simulation/WorldGen align on `TheSadRogue.Primitives` 1.6.0, App passes `RuntimeFortressGenerationRequest`/receives `RuntimeFortressGenerationResult`, the old App-facing `GenerateFortressMap(...)` + `FillRuntimeWorld(...)` facade was removed, the Runtime content snapshot getter and public world-fill method were closed, and Program's low-level Runtime log binding now goes through an App.Runtime bridge.
- The same no-build cleanup tightened generated-world presentation DTOs: `WorldMapTileView` and `EmbarkSiteSummary` no longer expose `BiomeType` directly, World-map biome glyph/color mapping moved to `HumanFortress.App.Rendering.WorldMapTileDisplayMapper`, and WorldGen screen state no longer stores the last `WorldGenResult` after generation.
- Earlier source-only/no-build module-boundary cleanup moved generated-world UI-facing service/data types out of App.Session into `HumanFortress.WorldGen`; the latest follow-up moved the stable generated-world DTO/settings contracts into `HumanFortress.Contracts.WorldGen` and hid concrete generated-world data behind an App-owned adapter. App.Session now stores/query-wraps the generated-world port and maps a contract `WorldTileSnapshot` into `RuntimeFortressGenerationRequest`; active App source no longer references raw `WorldTile`, `BiomeType`, `WorldParams`, `WorldGenerator`, `DifficultyPreset`, or `WorldGenResult`.
- The same no-build cleanup reduced Runtime public surface: Runtime-only composition helpers (`FortressRuntimeStartup`, host/system factories, dependency/catalog/tuning/workforce/planning/job-system groups, navigation factories), Runtime snapshot builder/facade helpers, and Runtime command factory helpers are now internal. Public DTOs and session semantic command entrypoints remain the external boundary.
- Latest source-only/no-build Navigation boundary cleanup moved navigation DTO/interface contracts into `HumanFortress.Contracts.Navigation`, while concrete pathfinding/cache implementations now live in `HumanFortress.Navigation.Implementation` as internal implementation types. Jobs now consumes movement through `IMovementExecutor`, Runtime job-system wrappers create concrete `MovementExecutor` instances, and `HumanFortress.Jobs` no longer references the `HumanFortress.Navigation` project.
- Latest source-only/no-build API hardening made concrete Navigation implementation types, Jobs implementation/orchestration/tuning/debug types, Runtime concrete command classes/factories, Runtime command-target interfaces/aggregation, and additional Runtime composition/logging helper members internal. `HumanFortress.Jobs` no longer exposes internals to App, and active App source still has no direct Jobs/Simulation/Navigation/Runtime.Commands references.
- Latest source-only/no-build boundary hardening continued without running a build: Content single-purpose loaders/parsers and concrete registries are now internal implementation details where possible, `ContentRegistry.Materials`/`TerrainKinds` expose Contracts read-only catalog interfaces, profession JSON loading is an internal Content facade returning `IProfessionRegistry`, Runtime session core options are internal construction helpers, `FortressState` composes App runtime access through named runtime ports, keyboard input no longer receives one broad runtime interface for workshop/navigation/simulation/build-catalog operations, and App UI consumes the Contracts `SimulationStatus` DTO directly.
- Latest source-only/no-build Runtime API hardening made internal command targets, concrete Runtime commands, job-system wrappers, the Simulation-to-Navigation adapter, Runtime helper loaders, auto-dig seeding, command factories, and snapshot builder/facade methods use internal or explicit-interface surfaces. The same pass normalized low-risk Jobs configuration/profession/orchestrator helper surfaces. Public Runtime shape is now concentrated on `FortressRuntimeSessionFactory`, Runtime session port interfaces, Runtime request/status types, and logging bootstrap rather than concrete implementation helpers; public snapshot DTOs live in Contracts.
- Previous architecture-hardening build verified the pre-hardening batch after `SimulationWorldContentLoader`, `RuntimeAutoDigSeeder`, Runtime logging/workshop-completion helper types, and concrete command target implementations were internalized. Fast solution build passed with `0 Warning(s), 0 Error(s)` before the latest source-only API hardening.
- `HumanFortress.App/Jobs` no longer contains active source files.
- Profession contracts now compile from `HumanFortress.Contracts` under `HumanFortress.Contracts.Jobs`.
- Profession assignment state now lives in `HumanFortress.Jobs`; the old App-owned Jobs namespace has been removed from active source.
- Profession registry JSON loading now lives in `HumanFortress.Content.Definitions`.
- Content registry contracts now compile from `HumanFortress.Contracts.Content.Registry`.
- The structured runtime content registry implementation now compiles from `HumanFortress.Content.Registry`.
- `CoreDataRegistryLoader` now compiles from `HumanFortress.Content.Definitions`.
- Tick-facing transport/mining/construction/craft job wrappers now live in `HumanFortress.Runtime/Jobs`.
- Runtime dependency grouping (`FortressRuntimeDependencies`, catalogs, tunings, and workforce composition) now lives in `HumanFortress.Runtime`.
- Runtime concrete system composition (`SimulationRuntimeSystems`, `FortressRuntimeSystemsFactory`, planning groups, and job-system groups) now lives in `HumanFortress.Runtime`.
- Jobs owns the executor cores, diff emitters, callback loggers, profession/craft adapters, scheduler/workshop tuning types, worker-selection strategy, unified jobs orchestrator, sanitizer, mining drop resolver, and construction terrain-material resolver.
- Runtime host factory, generic startup orchestration, and optional startup auto-dig command seeding now live in `HumanFortress.Runtime`; App injects logging and the auto-dig setting.
- Active fortress world content application now lives in Runtime's `SimulationWorldContentLoader`; App injects logging/content-issue callbacks instead of owning the loader.
- Runtime command source files now live in `HumanFortress.Runtime/Commands` under the `HumanFortress.Runtime.Commands` namespace.
- Runtime command request entrypoints now live behind Runtime session ports implemented by the internal `FortressRuntimeSessionCore`; App-facing input/access paths issue semantic queue methods such as `QueueHaulOrder(...)`, `QueueCreatureSpawn(...)`, and `QueueAddWorkshopRecipe(...)` instead of passing `Func<ulong, ICommand>` or constructing Runtime command factories.
- App active source no longer references `HumanFortress.Core.Commands` or `HumanFortress.Runtime.Commands`; concrete command factory usage is contained inside Runtime.
- Placement request enums/DTOs now live under `HumanFortress.Contracts.Runtime`; App passes placement shape/material preferences/tags as semantic request parameters, while material-filter defaults/category mapping and Simulation order enum/material DTO conversion remain inside Runtime command code.
- Runtime composition types now use the `HumanFortress.Runtime` namespace, and tick-facing job wrappers now use `HumanFortress.Runtime.Jobs`.
- App still owns the SadConsole/platform host, logger callback binding, UI bootstrap, construction completion UI notification binding through the Runtime notification bridge, and session/bootstrap glue.
- App no longer references concrete Runtime job-system wrappers for construction completion binding; `FortressRuntimeWorkshopCompletionNotifier` is injected through Runtime composition and App sets only the UI handler through `IFortressRuntimeBootstrapAccess`.
- Optional auto-dig mining command construction now lives in Runtime's `RuntimeAutoDigSeeder`; App no longer owns the startup/after-fill mining command implementation.
- Work/jobs/profession UI paths now consume Runtime-built jobs/workforce snapshot DTOs from `HumanFortress.Contracts.Runtime.Snapshots` instead of reading concrete runtime job systems or `ProfessionAssignments` through `FortressRuntimeAccess`.
- Workshop drawer list/queue/status paths, detailed workshop panel rendering, workshop overlay/material-progress rendering, and workshop map-click hit-testing now consume Runtime-owned workshop snapshots instead of scanning live placeables and construction definitions inside the former `UiRenderer` paths or `FortressMapClickController`.
- Work drawer labor/order summary paths now consume Runtime-owned workforce/order snapshots instead of reading live creatures or order designations inside the former `UiRenderer` paths.
- Build quick-menu workshop item browsing and buildable placement preview now consume a Runtime-owned build catalog snapshot DTO instead of passing construction catalog contracts into UI/keyboard rendering paths.
- Debug menu status/items and tile inspection popups now consume Runtime-owned debug/tile inspection snapshots instead of reading item definitions, world counts, tile data, geology, ground items, or creature definitions inside UI render/input helpers.
- F1/F2/F4 management drawer lists now consume a Runtime-owned management drawer snapshot instead of scanning live creature, item, zone, or stockpile managers inside the former `UiRenderer` paths.
- Zone menu overlay/detail popup rendering and zone click hit-testing now consume Runtime-owned zone snapshots/queries instead of scanning visible chunks, zone shards, zone instances, zone definitions, or zone cells inside App UI helpers.
- Stockpile overlay rendering, stockpile click hit-testing, and stockpile edit-popup rendering now consume Runtime-owned stockpile snapshots/queries instead of holding a `StockpileManager` in `StockpileUI` or scanning stockpile chunk shards inside App UI helpers.
- Navigation debug overlay drawing modes and F10 path-debug queries now consume Runtime-owned navigation DTOs built from `NavigationManager.Source` and cached nav data instead of letting App overlay/debug controllers read live `World`, chunks, tiles, `NavigationManager`, `NavigationTuning`, `WorldNavigationView`, or `Path` objects.
- Tile click debug logging now reuses the Runtime-owned tile inspection DTO instead of reading chunks, tiles, or fortress-map geology directly in `FortressMapClickController`.
- Haul/mining/construction placement previews now consume Runtime-owned placement preview DTOs instead of reading live ground items or terrain tiles inside `OrdersUI`.
- Order highlight mining/construction legal-cell dots now reuse Runtime-owned placement preview DTOs instead of reading live terrain inside the former `UiRenderer` paths.
- Debug spawn readiness/count logging now consumes a Runtime-owned debug spawn DTO instead of carrying `LoadedSession.World` into the map-click debug controller.
- Workshop panel keyboard editing now reads queue entry ids and worker-slot state from the Runtime-owned workshop snapshot facade; App no longer resolves or mutates `WorkshopState` through a live placeable scan.
- Overlay, keyboard, mouse, map-click, and placement input contexts now carry explicit `UiServices`, `NavigationOverlay`, or `HasFortressMap` dependencies instead of the full loaded-session snapshot that contains live `World`.
- Main map terrain/entity rendering now consumes Runtime-owned `SimulationMapViewportData`; `FortressMapRenderer` only clears/draws DTO cells and no longer reads live `World`, chunks, tiles, items, creatures, `FortressMap`, or geology catalogs.
- Frame rendering now consumes a Runtime-owned `SimulationFrameRenderData` aggregate for map viewport cells, navigation overlay data, and tile inspection data instead of issuing separate frame-time Runtime queries from `FortressFrameRenderer`.
- UI overlay rendering now consumes a Runtime-owned `SimulationUiOverlayFrameData` aggregate for build catalog, jobs, workshops, stockpile overlay/detail, zone overlay/detail, management drawer, Work drawer, and Debug menu data instead of issuing separate overlay-time Runtime queries for each panel/overlay.
- Work drawer rendering now consumes a Runtime-owned `SimulationWorkDrawerData` aggregate for jobs/workforce/orders/workshops instead of letting App UI helpers call separate runtime facade methods from each work panel helper.
- App/SadConsole presentation code is now split by surface: frame overlay orchestration, chrome/topbar/dock drawing, management drawer drawing, Debug menu drawing, debug unit overlay drawing, map overlay glyph drawing, placement overlay preview, quick menus, Work drawer panels, and workshop modal rendering no longer sit inside one `UiRenderer` god class.
- App state-machine registration/navigation now lives under `HumanFortress.App.GameStates`; fortress session context/load/bootstrap result state now lives under `HumanFortress.App.Session`; keyboard/mouse/placement/debug input routing now lives under `HumanFortress.App.Input`; SadConsole viewport/layout/view bootstrap helpers live under `HumanFortress.App.Rendering`; and UI service factories live under `HumanFortress.App.UI`.
- Loaded-session state/load results no longer carry live `World` or `FortressMap` objects into frame/input code; they expose only readiness flags plus render/UI/navigation presentation state.
- `FortressRuntimeAccess` no longer exposes public `HasWorld`, a live `World` property, the old bootstrap-world getter, or a two-step fortress-map generation/fill operation; UI readiness goes through `SimulationWorldAvailabilityData`, and session initialization requests a Runtime-owned fortress generation/fill operation through `RuntimeFortressGenerationRequest`.
- `GameStateManager` no longer owns the active `SimulationRuntimeSession`, tick scheduler, command queue, event bus, diff logs, runtime content snapshot, generation content, live `World` helper, render snapshot builder creation, navigation rebuild calls, or concrete runtime session controller directly; runtime lifetime is delegated through `GameStateRuntimeCoordinator`, which now creates the Runtime session through `FortressRuntimeSessionFactory`, stores `IFortressRuntimeSessionPorts`, and hands App code only narrow `FortressRuntimeAccess` role adapters.
- Rendering, input, placement, map-click, debug-spawn, workshop-panel, build-catalog, navigation-debug, simulation-control, UI-input, and session-bootstrap paths now sit behind progressively narrower module-owned ports. Runtime role interfaces are split by read/query/command where useful: placement, debug-spawn, and workshop-panel no longer use mixed query+command facades. `FortressStateRuntimePorts` is now only the state-level bundle of module-owned view/input/session port groups; ordinary helpers receive view/input/session ports instead of raw App.Runtime role interfaces. The concrete `FortressRuntimeAccess` remains only as the App facade created by the fortress play state.
- `FortressRuntimeAccess` and Runtime session ports are split by role/capability partial files. The Access layer now delegates semantic App role methods to Runtime port interfaces, while the internal Runtime core owns command construction, snapshot/query methods, lifecycle, and content/host composition in separate partials.
- `FortressState` no longer owns input-context construction or fortress load orchestration; `FortressInputContextFactory` and `FortressSessionLoadCoordinator` keep those App concerns out of the state object.
- Game-state SadConsole screen presentation is centralized behind `IGameScreenPresenter` / `ScreenGameState<TScreen>`; individual state wrappers now create their screen and no longer write `GameHost.Instance.Screen` directly.
- `FortressPlayGameState` now depends on `IFortressPlayRuntimeHost` instead of the whole `GameStateManager`; runtime init/access for fortress play is a narrow state-transition collaborator.
- `GameStateManager` delegates FortressPlay runtime start/stop policy to `GameStateRuntimeLifecycle`, keeping transition ordering separate from simulation lifecycle policy.
- App startup concerns now live under `HumanFortress.App.Startup`: command-line option parsing, native library preload, strict content gate, unhandled exception binding, headless init, crash-test runner, and SadConsole lifetime runner are no longer embedded in `Program`.
- `Program` no longer holds a static `GameStateManager` or SadConsole frame-update hook; `SadConsoleGameApp` owns the running state manager and `SadConsoleGameRunner` owns SadConsole create/run/dispose.
- `GameStateManager` no longer exposes the old public `InitializeWorld(...)`, `ChangeState(...)`, `CurrentState`, no-op frame `Update(...)`, `Render(...)`, or `HandleInput(...)` surface. External navigation uses `TransitionTo(...)`; the state map lives in `GameStateRegistry`.
- `UiChromeRenderer` now consumes the Contracts-owned `SimulationStatus` snapshot directly; the App-owned `FortressSimulationStatus` wrapper and mapper have been deleted.
- Fortress view/bootstrap wiring now passes a `FortressUiInteractionDataSource` to UI component setup instead of handing the rendering bootstrapper an `IFortressRuntimeUiInputAccess` facade.
- Fortress input composition now uses `FortressInputCallbackHub` for controller callback binding instead of `inputController!` null-forgiving closure cycles.
- Fortress session size validation now lives behind the shared Contracts/App size rules; session storage, embark prep, fortress runtime initialization, and fortress viewport initialization use the same normalized size rule instead of separate hard-coded range checks.
- `InputHandlerComponent` has been split so Debug overlay clicks live in `DebugMenuInputHandler` and Work/Job Allocation keyboard/mouse handling lives in `WorkAllocationInputHandler`; the SadConsole component now acts as the top-level UI input dispatcher. `FortressScreenMouseInput` is also split by chrome button hit handling, root quick-menu hit testing, and mining submenu hit handling.
- `FortressInputContextFactory` is split by context family: constructor/dependency capture, keyboard/mouse/overlay presentation contexts, and map/placement/debug-spawn contexts are separate partial files.
- `UiStore` is now split by transient UI state domain: navigation/cancel flow, drawer state, quick-menu/submenu state, selection, build/material state, workshop panel state, placement state, Debug menu state, and toast/highlight feedback live in separate partial files instead of one state god object.
- `UiManagementDrawerRenderer`, `UiWorkDrawerRenderer`, and `UiDebugMenuRenderer` have been split into focused partial renderers by chrome, content dispatch, tab content, and drawer/debug surface while staying in App.UI because they depend on SadConsole surfaces and `UiStore`.
- `UiChromeRenderer` is split by chrome surface: simulation topbar, dock/quick buttons, help/pause modals, and toast drawing.
- Quick-menu feature UI helpers are split by App presentation concern: `ZonesUI`, `OrdersUI`, and `StockpileUI` now separate menu drawing, placement/preset handling, overlay/preview drawing, detail/edit popups, and local drawing helpers instead of mixing all surfaces in one file.
- `ZonesUI` menu rendering now also separates root/submenu orchestration from individual third-level zone submenu panels.
- `BuildUI` and `FortressBuildKeyboardInput` are split by App UI concern: root/submenu rendering, construction material dialog drawing, structural keyboard handling, and workshop category/item selection now live in focused App.UI/App.Input partial files.
- `FortressMapOverlayGlyphRenderer` is split by overlay type: workshop footprint/placement previews, mining job highlights, and order highlight/preview glyph rules now live in separate App.Rendering partial files.
- `NavigationOverlay` is split into state/mode mapping, cell/path rendering, legend rendering, and color parsing. `FortressPlacementOverlayRenderer` is split into top-level placement overlay orchestration, anchored previews, workshop previews, and preview-mode mapping.
- `FortressPlacementController` is split by command family: stockpile placement/copy/create, haul order placement, and buildable/zone placement operations.
- World-map and embark-prep screens now read generated-world map information through `FortressSessionContext.TryGetWorldSize(...)` and `TryGetWorldTileView(...)` returning a primitive/name-based WorldGen `WorldMapTileView`; App no longer reads `WorldGenResult.Tiles` or raw `WorldTile`, and biome display policy sits in App.Rendering rather than state screens.
- `WorldMapState` now clamps its initial cursor and camera movement against Session-provided world dimensions instead of assuming the generated world is larger than the map viewport.
- `WorldMapState` is split into main state setup, rendering, and keyboard-input partial files so world-map UI presentation can evolve without re-growing a single state-object file.
- `WorldGenState` is split into main state setup, rendering/progress drawing, separate mouse/keyboard input, name-edit input, option mutation, drawing controls, and generation action partial files; the screen still owns world-generation UI flow, but no longer combines all UI/input/generation logic in one 550-line state file.
- `MainMenuState` is split into main state setup, rendering, input, and menu action partial files so menu presentation and transition actions no longer share one large state file.
- `MainMenuState` rendering is further split by page rendering and decorative/menu-art drawing so menu state flow stays separate from SadConsole presentation details.
- `EmbarkPrepState` is split into setup, rendering, keyboard input, and embark action partials. App UI command classes under `HumanFortress.App.UI.Commands` are now one command per file instead of one mixed command file.
- Runtime snapshot builders have also been split by read-model concern: `NavigationOverlaySnapshotBuilder` now separates public entrypoints, basic/structural overlay-mode builders, path-cell mapping, and grid/bit helpers; `FortressRuntimeSnapshotBuilder` now separates base/debug/catalog, frame aggregates, map/query snapshots, and Work/workshop snapshots; `MapViewportSnapshotBuilder` now separates viewport orchestration, terrain glyph policy, and entity glyph policy; `WorkshopSnapshotBuilder` now separates workshop scanning, summary mapping, queue mapping, and construction-material progress; `ManagementDrawerSnapshotBuilder`, `StockpileSnapshotBuilder`, `JobsDebugSnapshotBuilder`, and `FortressRuntimeSessionSnapshotFacade` are split by their own read-model/session-query families instead of growing as new god-object facades.
- Runtime concrete system assembly is split by group owner: `FortressRuntimePlanningSystems` and `FortressRuntimeJobSystems` now live in separate Runtime composition files instead of sharing one mixed system-groups file.
- Runtime dependency grouping is split by group owner: root `FortressRuntimeDependencies`, `FortressRuntimeCatalogs`, `FortressRuntimeTunings`, and `FortressRuntimeWorkforce` now live in separate Runtime files.
- Runtime session snapshot entrypoints are exposed through Runtime read/snapshot ports and implemented by split `FortressRuntimeSessionCore` partials by facade family: base/debug/catalog availability, frame/overlay data, map/navigation/placement queries, and Work/workshop queries.
- App input routing is further split by input family: keyboard router context/navigation helpers, mouse router contracts/click handling, overlay-click context/right-click/map pass-through, global shortcut drawers/tabs/debug-key detection, orders keyboard submenu/mining/haul/WIP handling, build workshop category/item selection, screen quick-menu root clicks, world-map cursor/camera movement, and main-menu mouse input now live in focused App.Input/App.States partial files.
- App state/rendering presentation splits now include compact orders menus, Work drawer workshop queue/directory/standing-order/construction-status columns, workshop placement preview rendering, World-map tile glyph/info-panel/camera-bound helpers, and FortressState focus/update/input/render lifecycle partials as separate SadConsole-facing files instead of mixing every UI surface in one renderer/state file.
- Runtime read-model facade splits now include stockpile, navigation, tile-inspection, placement-preview, and map-viewport entrypoint files under `FortressRuntimeSnapshotBuilder`, plus separate placement-preview eligibility/geometry, debug-item taxonomy, management-drawer item naming, map-viewport creature/item glyph policies, navigation overlay flow-field/ramp-mask policies, and navigation-source mapping/construction-site adapter helpers.
- Runtime command-target helpers are narrower: `StockpileCommandTarget` now separates stockpile cell collection and naming from the create command entrypoint, and `WorkshopQueueCommandTarget` separates workshop state lookup/worker-slot initialization from queue command methods.
- `SimulationRuntimeHostCore` lifecycle methods are split from system/pipeline configuration, and generic `SimulationRuntimeHost<TSystems>` accessors/start-stop lifecycle are split from constructor composition so host startup/shutdown policy is not mixed with Runtime tick-pipeline attachment.
- App diagnostics logging has a smaller root facade: the legacy log category resolver is split out of `Logger`, keeping the public App logger surface separate from compatibility message-classification policy.
- App diagnostics logging now also splits Logger initialization/close lifecycle and level-specific helper methods from the core diagnostic write path.
- App no longer has direct project references to `HumanFortress.Core`, `HumanFortress.Content`, `HumanFortress.Jobs`, `HumanFortress.Simulation`, `HumanFortress.Navigation`, or `HumanFortress.WorldGen`; those modules are reached through Runtime, Contracts, or DTO/query boundaries.
- UI placement command creation now maps App UI intents to Runtime request DTOs; command construction and Simulation order enum/material DTO conversion are inside Runtime rather than App input code.
- Runtime command dispatch now uses narrow target-context roles backed by `SimulationRuntimeCommandTargets`; `SimulationRuntimeContext` no longer directly implements the individual profession/item/creature/order/zone/workshop/stockpile command target interfaces, and commands no longer receive an all-target aggregate.
- Runtime command target handler binding now goes through profession-specific `IRuntimeProfessionCommandBindings`; `SimulationRuntimeContext` no longer exposes a broad all-target binding or a direct profession-specific handler setter.
- Non-registry runtime content DTOs such as `GeologyData` and `ZoneDefinitionData` now compile from `HumanFortress.Contracts.Content`; active source/test scans no longer find the historical `HumanFortress.Core.Content` namespaces.
- Current source-only static scans found no active App references to `HumanFortress.Core`, `HumanFortress.Content`, direct Jobs/Simulation/Navigation/WorldGen project namespaces, `HumanFortress.Runtime.Commands`, `HumanFortress.Runtime.Save`, `HumanFortress.Runtime.Replay`, App command factories, the old `HumanFortress.Runtime.Requests` namespace, the old `HumanFortress.Core.Content` namespace, or the old navigation contract namespace. Remaining direct `HumanFortress.Runtime` App usings are allowlisted to startup, adapter, world-generation provider, and App content-file-location wrapper boundaries; direct `HumanFortress.Navigation.Implementation` references outside Runtime are limited to friend tests for concrete Navigation internals.
- Remaining high-priority architecture work is reducing transitional internal bootstrap bridges and friend-access scaffolding, richer long-horizon stockpile reservation/maintenance policy, normalizing diff priority/replay semantics, broadening versioned Runtime/Contracts snapshot metadata beyond the aggregate frame DTOs where useful, and keeping session/bootstrap glue from growing new gameplay/domain logic.

## Verified Batch: UI/Debug Snapshot Facades

Status: build verified after latest UI presentation/session-controller/runtime-access-interface split, bootstrap/play access split, command-target aggregate split, Content DTO namespace cleanup, Runtime-owned world-content loader migration, Runtime-owned auto-dig/notification bridge extraction, App module boundary cleanup, App direct Jobs/Simulation/Navigation reference removal, Runtime placement command factory extraction, and caller-role runtime facade narrowing.

### Completed

- Added Runtime-built jobs/workforce/workshop/build/debug/tile-inspection read models; DTO contracts now live in `HumanFortress.Contracts.Runtime.Snapshots` and builders live in `HumanFortress.Runtime.Snapshots`:
  - normalized job stat rows for hauling, mining, crafting, and construction status
  - scheduler stats and optional transport queue debug rows
  - active job rows and mining overlay point lists
  - profession definition and roster rows for the job allocation UI
  - order summary/recent-designation rows for Work drawer order panels
  - workshop summary and queue rows for Work drawer workshop lists/status panels, detailed workshop panel rendering, and workshop click hit-testing
  - build catalog rows for build quick-menu workshop browsing and buildable footprint preview
  - debug menu status/item rows and tile inspection popup rows
  - management drawer rows for F1 creature lists, F2 ground-item lists/kind filters, F4 zone lists, and stockpile drawer lists
  - zone overlay cells for the visible viewport/z-layer and zone detail popup rows
  - stockpile overlay cells, stockpile detail popup rows, and stockpile/zone hit-test query DTOs
  - navigation overlay cells for walkability, movement cost, traffic, connectivity, flow-field, ramp-mask debug draw modes, and F10 path-debug path cells
- Added Runtime-owned map viewport and work drawer aggregate read models in `HumanFortress.Runtime.Snapshots`:
  - screen-space map glyph/color cells for terrain, cursor, visible creatures, and visible ground items
  - world availability rows for UI/input readiness checks
  - work drawer aggregate rows bundling jobs, workforce, order summaries, and workshop summaries for the Work drawer
- Added Runtime-owned frame/overlay aggregate read models in `HumanFortress.Runtime.Snapshots`:
  - frame render aggregate rows bundling map viewport, navigation overlay, and tile inspection data
  - UI overlay frame rows bundling build catalog, jobs, workshop overlays/panels, zone/stockpile overlay/detail data, management drawer data, Work drawer data, and Debug menu data
- Added focused Runtime-owned snapshot builders for build catalog, debug menu, tile inspection, jobs, workforce, orders, workshops, management drawers, zone overlay/detail data, and stockpile overlay/detail data; `FortressRuntimeSnapshotBuilder` is now only a thin facade over those builders.
- Changed `GameStateManager` to delegate UI/debug snapshot construction to Runtime-owned builders instead of aggregating that logic in App or exposing concrete job wrappers, `ProfessionAssignments`, `UnifiedJobsOrchestrator`, or scheduler/workshop tunings as public App-facing properties.
- Removed the old jobs debug cache so mining overlay and active-job panels read a fresh per-frame snapshot rather than a stale cached debug bundle.
- Changed `FortressRuntimeAccess` to expose snapshot/query DTO methods such as `GetJobsDebugData(...)`, `GetWorkDrawerData(...)`, `GetWorldAvailabilityData()`, and `GetWorkshopDebugData()` while no longer exposing concrete transport/mining/construction/craft job systems, `ProfessionAssignments`, or live world readiness properties.
- Changed Work drawer rendering, active jobs rendering, construction status rendering, scheduler diagnostics, mining job/completion highlights, and job allocation keyboard/mouse input to consume the DTO facade.
- Changed Work drawer workshop list, active queue notes, workshop directory, and construction status panels to consume workshop snapshot DTOs instead of calling `World.GetAllChunks()` / `GetAllOwnedPlaceables()` inside the former `UiRenderer` paths.
- Changed Work drawer labor overview, dwarf roster, order summary, and active jobs recent-designation sections to consume workforce/order DTOs instead of reading `world.Creatures` or `world.Orders` inside the former `UiRenderer` paths.
- Changed Work drawer panels to consume a single `SimulationWorkDrawerData` aggregate supplied by the overlay renderer, removing separate App UI calls to workforce/order/job/workshop facade methods while rendering Work tabs.
- Changed `FortressUiOverlayRenderer` to fetch one `SimulationUiOverlayFrameData` per frame and pass the relevant DTOs down to focused App renderers (`UiManagementDrawerRenderer`, `UiDebugMenuRenderer`, `UiWorkDrawerRenderer`, stockpile UI, zone UI, workshop overlay/panel rendering, and quick-menu rendering).
- Changed detailed workshop panel rendering, workshop overlay/material-progress rendering, and workshop map-click hit-testing to consume workshop snapshot DTOs instead of walking live placeables and construction definitions.
- Changed build quick-menu workshop item browsing, build keyboard workshop selection, and buildable placement preview to consume a Runtime-owned build catalog DTO instead of passing `IConstructionCatalog`/`ConstructionDefinition` into UI/keyboard rendering paths.
- Changed Debug menu status/items rendering, Debug item paging, and Debug item mouse selection to consume a Runtime-owned debug DTO instead of reading item definitions or world metric counts from UI code; Debug item paging now renders directly in `UiDebugMenuRenderer` instead of a post-render overlay.
- Changed tile inspection popup rendering to consume a Runtime-owned tile inspection DTO instead of passing live `World`, `FortressMap`, or `IRuntimeGeologyCatalog` into `FortressTilePopupRenderer`.
- Changed F1 creature list, F2 ground-item list/kind filters, F4 zone list, and stockpile drawer list rendering to consume a Runtime-owned management drawer DTO instead of passing live `World` or `StockpileManager` into `UiManagementDrawerRenderer.DrawDrawer`.
- Changed zone overlay/detail popup rendering and zone click/delete hit-testing to consume Runtime-owned zone DTO/query methods instead of passing live `World` into `ZonesUI` or reading `World.Zones` in App click helpers.
- Changed stockpile overlay/edit popup rendering and stockpile click/copy hit-testing to consume Runtime-owned stockpile DTO/query methods instead of passing live `World` or `StockpileManager` into `StockpileUI`.
- Changed navigation debug overlay drawing and F10 path-debug queries to consume Runtime-owned navigation DTOs instead of passing live `World` into `NavigationOverlay`, storing `NavigationManager`/`NavigationTuning` inside the overlay renderer, or creating `WorldNavigationView`/`DeterministicAStar` inside the App debug controller. The snapshot builder uses the existing Navigation-owned world source interface and nav cache, so Runtime owns the read/query path while App only maps DTO cells to SadConsole glyphs/colors.
- Removed `NavigationManager` from the loaded-session UI snapshot/result path; `FortressLoadedSessionState` no longer carries navigation internals for debug input handling.
- Added tile meta flags to `SimulationTileInspectionData` and changed tile click debug logging to consume `GetTileInspectionData(...)` instead of reading live `World`, `Chunk`, `TileBase`, or `FortressMap` data in the click controller.
- Added Runtime-owned placement preview DTOs/builders for haul ground-item previews and mining terrain eligibility previews; `OrdersUI.RenderPlacementPreview(...)` now draws DTO cells instead of reading live `World`, ground items, tiles, or simulation terrain kinds.
- Extended Runtime-owned placement preview DTOs/builders to cover construction wall/floor/ramp eligibility and changed construction placement preview plus construction order highlights to read those DTO cells instead of using UI-side terrain checks.
- Removed live `World` from `FortressPlacementControllerContext`; zone/stockpile placement guards now use the runtime facade's world-availability predicate instead of carrying the world through placement input code.
- Removed the old `WorldProvider` dependency from the view/interaction bootstrap path that only existed for Debug item selection.
- Added `SimulationDebugSpawnData` so debug spawn input checks world availability and logs definition counts through a Runtime snapshot facade.
- Changed workshop panel keyboard editing to read `WorkshopSummaryView` via `FortressRuntimeAccess.GetWorkshopPanelData(...)`; `WorkshopQueueEntryView` now carries `EntryId`, and the App-owned live `FortressWorkshopPanelContextResolver` was removed.
- Changed overlay, keyboard, mouse, map-click, and placement input contexts to depend on explicit UI/navigation/map-availability values instead of passing `FortressLoadedSessionSnapshot` through input controllers.
- Removed live `World` from `FortressUiOverlayRenderContext`; overlay rendering now uses Runtime snapshots/queries and the same-frame map viewport DTO for map-readiness checks.
- Added Runtime-owned `SimulationMapViewportData` and moved main map terrain/entity display selection into Runtime snapshot builders. `FortressMapRenderer` is now a pure App drawer over DTO cells plus navigation overlay rendering.
- Added Runtime-owned `SimulationFrameRenderData` and changed `FortressFrameRenderer` to fetch map viewport, navigation overlay, and tile inspection read models through one frame query.
- Removed old single-purpose overlay/frame facade methods from `FortressRuntimeAccess` after the aggregate frame/overlay DTOs replaced them: management drawer, zone overlay/detail, stockpile overlay/detail, jobs debug, Work drawer, navigation overlay, and map viewport.
- Removed live `World` and `FortressMap` objects from `FortressLoadedSessionState`, `FortressLoadedSessionSnapshot`, and `FortressSessionLoadResult`; App frame/input paths now receive readiness flags instead of simulation/worldgen objects.
- Replaced public `FortressRuntimeAccess.HasWorld`, the live `World` facade property, and the internal bootstrap-world getter with `SimulationWorldAvailabilityData` for UI readiness. A later cleanup moved the remaining fortress-map generation/fill step behind a Runtime-owned `RuntimeFortressGenerationRequest`.
- Removed unused live `World`/camera/z parameters from the former quick-menu renderer path and the dormant `WorkDrawerOverlay.DrawWorkSchedulerOverlay(...)` hook.
- Split `FortressUiOverlayRenderer` into focused App.Rendering helpers for map overlays, placement previews, tool popups, and modal/debug rendering; the coordinator now fetches the overlay-frame DTO and delegates presentation work.
- Split the former `UiRenderer` Work drawer, quick menu, workshop panel, map overlay glyph, chrome/topbar/dock, management drawer, Debug menu, and debug unit overlay methods into `UiWorkDrawerRenderer`, `UiQuickMenuRenderer`, `UiWorkshopPanelRenderer`, `FortressMapOverlayGlyphRenderer`, `UiChromeRenderer`, `UiManagementDrawerRenderer`, `UiDebugMenuRenderer`, and `FortressDebugUnitOverlayRenderer`. These remain in App/UI or App/Rendering because they depend on SadConsole surfaces and `UiStore`, while simulation facts still enter as Runtime DTOs.
- Routed dock/quick-button drawing, keyboard shortcuts, and mouse hit-testing through `UiChromeSlots` plus `ButtonLayoutCalculator`, and centralized Debug item category label/enum mapping in `DebugLayoutCalculator` so rendering and input stay aligned.
- Added `FortressRuntimeSessionSnapshotFacade` in Runtime so App.Runtime session glue passes the active runtime session to Runtime-owned read-model builders instead of unpacking live `World`, navigation, geology, construction, or recipe catalog objects for each UI/debug query.
- Removed `Geology`, `GenerationContent`, construction catalog, recipe catalog, live bootstrap-world, render snapshot builder creation, and navigation rebuild exposure from `FortressRuntimeAccess`; session initialization now sends a `RuntimeFortressGenerationRequest` and Runtime generates/fills the fortress world internally.
- Added `FortressRuntimeSessionController` in App.Runtime as an intermediate session-controller adapter over Runtime core, Runtime snapshot facade calls, bootstrap request forwarding, and fortress-play startup. Later hardening removed that passthrough and then hid the concrete core; `GameStateRuntimeCoordinator` now creates Runtime session ports through `FortressRuntimeSessionFactory` and `FortressRuntimeAccess` remains the App role adapter.
- Added App runtime access interfaces for the remaining facade boundary: render contexts depend on `IFortressRuntimeReadAccess`, while input, placement, map-inspection, debug-spawn, workshop-panel, navigation-debug, simulation-control, and session-bootstrap paths use narrower role interfaces. Later hardening removed the temporary aggregate interface entirely; creation now flows through module-owned view/input/session port groups, and the old broad play facade has been removed.
- Changed `FortressState` composition so ordinary frame/input calls receive only the role interfaces they need while `IFortressRuntimeBootstrapAccess` is reserved for the session loader/initializer path.
- Moved active world content application from App to Runtime's `SimulationWorldContentLoader`; App now injects logging and content issue callbacks instead of owning the loader.
- Moved optional startup/after-fill auto-dig mining command construction into Runtime's `RuntimeAutoDigSeeder`.
- Added `FortressRuntimeWorkshopCompletionNotifier` so App binds construction completion UI notifications without referencing concrete Runtime job-system wrappers or a static `ConstructionJobSystem` hook.
- Removed the legacy App `FortressRenderSnapshotService` / `OverlayFromSnapshot` bridge; loaded-session state/load results no longer carry `RenderSnapshotBuilder` or `RenderSnapshot`, and workshop overlays render from Runtime workshop DTOs.
- Removed the older `HumanFortress.Simulation.Rendering.RenderSnapshot` / `RenderSnapshotBuilder` implementation after active App rendering moved to Runtime/Contracts snapshot DTOs. PhaseTests now validate the Runtime frame snapshot port instead of constructing a second Simulation-owned render snapshot model.
- Moved tick scheduler, command queue, event bus, and diff logs out of `GameStateManager` and into Runtime's session implementation; `FortressRuntimeAccess` now delegates simulation status, command enqueue, pause, and speed controls through Runtime session ports.
- Moved navigation cache rebuild mutation out of `FortressRuntimeSessionSnapshotFacade` into Runtime's `SimulationRuntimeSessionNavigation`, keeping the session snapshot facade read/query focused.
- Split Runtime command target dispatch behind `SimulationRuntimeCommandTargets` plus narrow target-context role interfaces. Runtime commands now resolve order, zone, stockpile, workshop, spawn, and profession targets through role-specific command contexts instead of relying on `SimulationRuntimeContext` implementing every target interface directly or exposing an all-target aggregate.
- Narrowed `FortressRuntimeAccess`/`GameStateManager` construction, geology, recipe, navigation, and runtime-session read-model properties so UI-facing paths no longer expose those concrete contracts publicly.
- Added App-owned `UiConstructionShape` and moved stockpile preset menu options out of Simulation DTOs; App UI state/rendering now maps construction shape to Simulation only at the command/placement-preview boundary.
- Added Runtime-owned `FortressRuntimeLogBindings` so App still supplies logger callbacks but no longer owns the list of lower-layer Simulation/Navigation static callback targets.
- Split App diagnostics out of the runtime facade: `IFortressRuntimeReadAccess` no longer exposes `DiagnosticSnapshot`, and frame/modal rendering receives an App diagnostics provider separately.
- Logging, category callback creation, and content-issue logging are injected by App composition into Runtime core; the removed App controller no longer exists as a logging bridge.
- Moved App-only diagnostics helpers into `App.Diagnostics`, screen layout/viewport/view/tile-inspection presentation state into `App.Rendering`, placement geometry into `App.UI.Placement`, and App state-machine navigator/registration into `App.GameStates` as part of keeping App.Runtime focused on runtime access/session adapter boundaries.
- Added Runtime-owned `FortressRuntimeSessionCore` for active session ownership, scheduler/queue/event/diff services, Runtime content snapshot capture, snapshot facade calls, runtime startup, auto-dig seeding, workshop completion notification binding, and fortress-map generation/fill. The old App `FortressRuntimeSessionController` thin adapter was later deleted, and a later hardening pass made the core internal behind `FortressRuntimeSessionFactory`/session ports.
- Moved App fortress session context/load/bootstrap state into `HumanFortress.App.Session`, input controllers into `HumanFortress.App.Input`, UI service factories into `HumanFortress.App.UI`, and view/layout/bootstrap helpers into `HumanFortress.App.Rendering`; `HumanFortress.App.Runtime` now contains only runtime access interfaces/facades plus the session-controller adapter.
- Added `GameStateRuntimeCoordinator` so `GameStateManager` delegates runtime session-controller construction, runtime access creation, fortress world initialization, and fortress-play start/stop instead of owning the concrete controller directly.
- Added Runtime-owned placement command intent types and `RuntimePlacementCommandFactory`; App placement input now maps UI enums/options to Runtime command intents without referencing `HumanFortress.Simulation.Orders`.
- Removed App's direct project references to Jobs, Simulation, and Navigation after static scans found no App source using those namespaces.
- Split the play-time runtime facade into smaller caller-role interfaces for keyboard input, UI input callbacks, placement, map inspection, debug spawn, workshop panel editing, navigation debug, simulation controls, and command enqueueing.
- Fixed the build-exposed Runtime workshop completion delegate binding by avoiding null-propagation over a method group.
- Removed the build-exposed stale `FortressState.RefreshSnapshot()` helper from the old render-snapshot path; loaded-session state now only exposes readiness/presentation state through `Capture()`.
- Fixed the duplicated Craft stats lines in the scheduler diagnostics column while touching that panel.
- Left session/bootstrap glue, optional auto-dig startup, and construction completion UI callback binding for later batches. Later no-build cleanup moved auto-dig seeding and construction completion notification bridging into Runtime.

### Verification

- Source scan found no UI-facing `FortressRuntimeAccess` reads of concrete transport/mining/construction/craft job systems, `ProfessionAssignments`, `UnifiedJobsOrchestrator`, or scheduler tunings.
- Source scan found no former `UiRenderer` workshop drawer helper using the deleted `CollectWorkshops` live-placeable scan; workshop drawer data now enters through `GetWorkshopDebugData()`.
- Source scan found no Work drawer helper reading active/recent order snapshots or live creature lists directly from App UI renderers; those reads now enter through `GetOrdersDebugData()` and `GetWorkforceDebugData()`.
- Source scan found `FortressMapClickController` no longer depends on construction catalogs or live placeable scans for workshop hit-testing.
- Source scan found no UI/keyboard/overlay path under the former `UiRenderer` paths, `WorkshopCategoryMapper`, `FortressUiOverlayRenderer`, `FortressBuildKeyboardInput`, `FortressContextKeyboardInput`, `FortressKeyboardInputRouter`, or `FortressState` still consuming `IConstructionCatalog`/`ConstructionDefinition`.
- Source scan found no former `UiRenderer` workshop overlay path using `World`, `PlaceableInstance`, `GetAllOwnedPlaceables()`, or the old material-progress helper methods; overlay material progress now enters through `GetWorkshopDebugData()`.
- Source scan found no `WorldProvider` path in view/interaction bootstrap or `InputHandlerComponent`, and no Debug menu item/status code reading `world.Items.GetAllDefinitions()`, world chunk counts, or item/creature definition counts outside Runtime snapshot builders.
- Source scan found no `World`, `FortressMap`, geology catalog, item definition, or creature definition reads in `FortressTileInspectionController` / `FortressTilePopupRenderer`; tile popup data now enters through `GetTileInspectionData(...)`.
- Source scan found no `UiManagementDrawerRenderer.DrawDrawer` caller passing live `World` or `StockpileManager`, and no F1/F2/F4 drawer helper scanning `world.Creatures`, `world.Items`, `world.Zones`, or `stockpileManager.GetAllZones()`.
- Source scan found `ZonesUI` no longer references live `World`, scans chunks/zone shards, or reads zone manager data; zone overlay/detail/hit data now enter through `GetZoneOverlayData(...)`, `GetZoneDetailData(...)`, and `FindZoneAt(...)`.
- Source scan found `StockpileUI` no longer references live `World`, `StockpileManager`, stockpile zones, or chunk stockpile data; stockpile overlay/detail/hit data now enter through `GetStockpileOverlayData(...)`, `GetStockpileDetailData(...)`, and `FindStockpileAt(...)`.
- Source scan found `NavigationOverlay` no longer references live `World`, `Chunk`, `NavigationManager`, `ChunkNavData`, `NavCapability`, `NavigationTuning`, or `HumanFortress.Contracts.Navigation.Path`, and no caller passes `World` into `NavigationOverlay.RenderOverlay(...)`.
- Source scan found `FortressNavigationDebugController`, `FortressKeyboardInputRouter`, `FortressLoadedSessionState`, `FortressSessionLoadResult`, and `FortressSessionRuntimeBootstrapper` no longer reference `NavigationManager`, `NavigationTuning`, `WorldNavigationView`, `DeterministicAStar`, or `PathRequest`; F10 path-debug now enters through `FindNavigationDebugPath(...)`.
- Source scan found `FortressMapClickController` no longer references live `World`, `Chunk`, `FortressMap`, `GetChunk(...)`, or `GetTile(...)`; tile click logging now enters through `GetTileInspectionData(...)`.
- Source scan found `OrdersUI.RenderPlacementPreview(...)` no longer references live `World`, `GetTile(...)`, `TerrainKind`, or ground item managers; haul/mining/construction preview eligibility now enters through `GetPlacementPreviewData(...)`.
- Source scan found the old order-highlight renderer path no longer has an App-side construction terrain helper; mining/construction highlight dots now enter through `GetPlacementPreviewData(...)`.
- Source scan found no App references to `FortressWorkshopPanelContext`, `FortressWorkshopPanelContextResolver`, or `WorkshopState`; workshop keyboard input reads snapshot DTOs and enqueues commands by DTO `EntryId`.
- Source scan found the loaded-session snapshot is no longer carried through mouse, keyboard, overlay-click, map-click, or placement controller contexts; the full snapshot remains isolated to frame rendering.
- Source scan found `FortressUiOverlayRenderContext` no longer carries live `World`, and `UiQuickMenuRenderer.Draw(...)` / `WorkDrawerOverlay.DrawWorkSchedulerOverlay(...)` no longer accept live `World`.
- Source scan found `FortressMapRenderer` no longer references live `World`, `FortressMap`, chunks, tiles, item/creature managers, terrain kinds, or geology catalogs; main map terrain/entity rendering enters App as `SimulationMapViewportData`.
- Source scan found `FortressFrameRenderer` now uses `GetFrameRenderData(...)` instead of separate navigation overlay, map viewport, and tile-inspection Runtime calls.
- Source scan found `FortressUiOverlayRenderer` now uses `GetUiOverlayFrameData(...)` for overlay/drawer/debug read models; remaining direct Runtime queries are placement preview queries driven by active drag/highlight state and App diagnostics.
- Source scan found `FortressLoadedSessionState`, `FortressLoadedSessionSnapshot`, and `FortressSessionLoadResult` no longer carry live `World` or `FortressMap` objects into frame/input code.
- Source scan found Work drawer helpers no longer call separate workforce/order/job/workshop runtime facade methods; Work drawer rendering consumes `SimulationWorkDrawerData`.
- Source scan found old `UiRenderer.DrawQuickMenu`, `DrawWorkshopPanel`, `DrawWorkshopsOverlay`, `DrawWorkshopPlacementPreview`, `DrawMiningJobHighlights`, `DrawMiningCompletedHighlights`, and `DrawOrderHighlights` call paths no longer exist; App presentation has been split into focused App.UI/App.Rendering helpers while continuing to consume Runtime DTOs.
- Source scan found active source no longer contains a `UiRenderer` class, `DebugPageOverlayRenderer`, or `DrawDebugUnits` path; presentation now delegates to `UiChromeRenderer`, `UiManagementDrawerRenderer`, `UiDebugMenuRenderer`, `UiWorkDrawerRenderer`, `UiQuickMenuRenderer`, `UiWorkshopPanelRenderer`, `FortressMapOverlayGlyphRenderer`, and `FortressDebugUnitOverlayRenderer`.
- Source scan found App no longer references `FortressRuntimeSnapshotBuilder` directly and `FortressRuntimeAccess` no longer exposes `GetBootstrapWorld()`, render snapshot builder creation, navigation rebuild methods, or a two-step fortress-map fill operation. Active runtime session ownership, scheduler/queue/event/diff services, content injection, auto-dig seeding, construction-completion notification bridging, navigation rebuilds, and WorldGen-backed fortress-map generation/fill now sit behind Runtime session ports implemented by the internal core.
- Source scan found `FortressRuntimeAccess` concrete references are now limited to its implementation and the fortress-play creation/session-bootstrap path; App rendering uses `IFortressRuntimeReadAccess`, while input/placement/map-click/debug/workshop controllers use smaller caller-role runtime access interfaces instead of the full play facade.
- Source scan found no active App source or App project reference to `HumanFortress.Simulation`, `HumanFortress.Jobs`, or `HumanFortress.Navigation`; placement command translation now enters Runtime through `RuntimePlacementCommandFactory`.
- Source scan found `IFortressRuntimeReadAccess` no longer references App diagnostics types or exposes logger snapshots.
- Source scan found App UI no longer uses `HumanFortress.Simulation.Orders.ConstructionShape` or `HumanFortress.Simulation.Stockpile.StockpilePreset`; those are now App UI types/options until command/runtime query mapping.
- Source scan found no Runtime command still casting `ISimulationContext` to individual command target interfaces, and `SimulationRuntimeContext` implements explicit clock/target command context roles rather than seven command target interfaces.
- Source scan found concrete Runtime command target implementations, Runtime auto-dig seeding, world-content loading, Runtime logging helper state, Runtime composition helpers, Runtime command factories, and Runtime snapshot builder/facade helpers are no longer public API.
- `git diff --check` passed after moving the snapshot DTOs/builders out of App, splitting the Runtime snapshot builder, splitting App presentation helpers by UI surface, adding `FortressRuntimeSessionController`, narrowing App runtime access interfaces, removing the legacy App render-snapshot bridge, moving scheduler/command/event/diff ownership into Runtime's session core, moving world-content loading/auto-dig seeding to Runtime, hiding construction completion binding behind a Runtime notifier, splitting App diagnostics from runtime reads, moving lower-layer log binding knowledge into Runtime, moving UI-only geometry/layout helpers out of App.Runtime, moving session/input/view/UI-service helpers into their App submodules, removing App direct Jobs/Simulation/Navigation references, and adding Runtime placement command intent mapping.
- First fast solution build caught `FortressRuntimeSystemGroups` using null propagation on the `WorkshopCompletion.Notify` method group; this was fixed by capturing the notifier and passing an explicit nullable delegate.
- Second fast solution build caught a stale `FortressState.RefreshSnapshot()` call path after loaded-session state was narrowed; the unused helper was removed.
- `HumanFortress.sln` fast build then passed with `0 Warning(s), 0 Error(s)` using .NET 8, `--no-restore`, single MSBuild worker, analyzers disabled, and `UseAppHost=false`.
- Follow-up architecture-hardening fast build passed with `0 Warning(s), 0 Error(s)` after generated-world DTOs moved to WorldGen and Runtime implementation helpers were internalized.

## Verified Batch: Content Registry Namespace Cleanup

Status: build verified

### Completed

- Changed content registry contract DTOs, catalog interfaces/stores, tuning types, `IRuntimeGeologyCatalog`, `Footprint`, and the material `FixedPoint` helper from the old Core registry/content namespaces to `HumanFortress.Contracts.Content.Registry`.
- Changed the structured runtime `ContentRegistry` implementation and its helper registries from the old Core registry namespace to `HumanFortress.Content.Registry`.
- Changed `CoreDataRegistryLoader` from the old Core registry namespace to `HumanFortress.Content.Definitions`.
- Updated App, Runtime, Jobs, Simulation, WorldGen, Content, and regression-test call sites to use `HumanFortress.Contracts.Content.Registry` for contract types.
- Updated `ContentRegistry.Instance` call sites and structured registry aliases to use `HumanFortress.Content.Registry.ContentRegistry`.
- Added a direct `HumanFortress.Contracts` project reference to `HumanFortress.WorldGen` because WorldGen now directly consumes Contracts content types.
- Updated active architecture/rules/planning docs so current ownership no longer claims that the structured registry preserves the old Core registry namespace.

### Verification

- First fast build caught a namespace follow-up bug: `MaterialDefinition` had moved to `HumanFortress.Contracts.Content.Registry` while `FixedPoint` still lived under the old Core content namespace.
- Moved `FixedPoint` into `HumanFortress.Contracts.Content.Registry`.
- `HumanFortress.sln` fast build then passed with `0 Warning(s), 0 Error(s)` using .NET 8, `--no-restore`, single MSBuild worker, analyzers disabled, and `UseAppHost=false`.
- Source scans found no active source or test files using the old Core content registry namespace.
- `git diff --check` passed after the source namespace migration.

## Verified Batch: Runtime and Jobs Namespace Cleanup

Status: build verified

### Completed

- Changed Runtime composition source files that had already moved into `src/HumanFortress.Runtime` from the transitional `HumanFortress.App.Runtime` namespace to `HumanFortress.Runtime`.
- Changed tick-facing transport/mining/construction/craft wrapper systems in `src/HumanFortress.Runtime/Jobs` to the `HumanFortress.Runtime.Jobs` namespace.
- Changed Jobs-owned diff emitters, callback loggers, profession assignment/adapters, scheduler/workshop tunings, worker selection, unified jobs orchestration, sanitizer, mining drop resolver, and construction terrain-material resolver from the old App-owned namespace to `HumanFortress.Jobs`.
- Kept App UI/session glue under `HumanFortress.App.Runtime`; only non-UI Runtime composition source moved to the Runtime namespace.
- Split the profession namespace cleanup into its real module owners:
  - profession contracts now use `HumanFortress.Contracts.Jobs`
  - profession registry JSON loading now uses `HumanFortress.Content.Definitions`
  - profession assignment state remains in `HumanFortress.Jobs`
- Updated App, Runtime, and regression-test call sites to reference `HumanFortress.Runtime.Jobs`, `HumanFortress.Jobs`, and the Content/Contracts profession namespaces.
- Updated active architecture and simulation docs so they no longer describe the old App-owned Jobs namespace as live compatibility surface.

### Verification

- `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)` using .NET 8, `--no-restore`, single MSBuild worker, analyzers disabled, and `UseAppHost=false`.
- Source scans found no active source files using the old App-owned Jobs namespace.
- Source scans found no moved Runtime source files still declaring `HumanFortress.App.Runtime`.
- `git diff --check` passed before the build.
- Content registry compatibility namespaces were handled in the follow-up content registry namespace cleanup batch.

## Current Batch: Runtime Generic Host and App Factory Cleanup

Status: completed

### Completed

- Added `HumanFortress.Runtime.SimulationRuntimeHost<TSystems>` as a Runtime-owned generic runtime host over `SimulationRuntimeHostCore`.
- Deleted the old App-owned `SimulationRuntimeHost` wrapper.
- Moved `SimulationRuntimeSystems` into `HumanFortress.Runtime`; it is now a Runtime-owned system collection and tick-registration surface.
- Added `FortressRuntimeLogging` as a small injected logging callback bundle so Runtime composition no longer calls App `Logger` directly.
- Initially added `FortressRuntimeHostFactory` in App as a temporary App composition bridge:
  - creates `SimulationRuntimeHost<SimulationRuntimeSystems>`
  - passed recipe/construction catalogs into the then-current runtime command-target composition path
  - registers the profession-weight callback without making Runtime depend on App professions
- Moved `FortressRuntimeHostFactory` into `HumanFortress.Runtime`; App now supplies `FortressRuntimeLogging` and the active content snapshot instead of letting the factory call App `Logger`.
- Added then moved `FortressRuntimeStartup` into `HumanFortress.Runtime`; initial-worker/profession setup is Runtime-owned, while optional auto-dig remains an App-provided command delegate.
- Updated `GameStateManager` so it no longer directly constructs the runtime host or owns initial-worker/auto-dig startup details. It now delegates host creation to `FortressRuntimeHostFactory` and startup hooks to `FortressRuntimeStartup`.
- Split concrete system assembly out of `SimulationRuntimeSystems` into `FortressRuntimeSystemsFactory`, leaving `SimulationRuntimeSystems` as a system collection plus tick-registration surface.
- Split `FortressRuntimeSystemsFactory` into explicit Runtime-owned runtime system groups:
  - `FortressRuntimeDependencies` for content catalogs, scheduler/workshop tunings, craft recipe adapter, and profession assignments
  - `FortressRuntimePlanningSystems` for mining/hauling/construction/craft planners and the shared transport request queue
  - `FortressRuntimeJobSystems` for mining/transport/construction/craft job executor shells
- Changed `FortressRuntimeHostFactory` to create one `FortressRuntimeDependencies` instance and pass the same construction/recipe catalogs into both `SimulationRuntimeContext` and `FortressRuntimeSystemsFactory`, removing duplicate content-registry reads from the runtime composition path.
- Split `FortressRuntimeDependencies` into smaller dependency groups:
  - `FortressRuntimeCatalogs` for construction/recipe catalogs and the craft recipe adapter
  - `FortressRuntimeTunings` for scheduler/workshop tunings
  - `FortressRuntimeWorkforce` for composing the Content-loaded profession registry with Jobs-owned profession assignment state
- Added a Content-owned `FortressRuntimeContentSnapshot` and loader so runtime composition captures construction/recipe catalogs plus scheduler/workshop tuning JSON through `HumanFortress.Content` instead of directly walking the structured registry from App.
- Added `ContentRegistry.GetTuningJson(...)` as the transitional structured-registry JSON export used by the Content snapshot loader.
- Changed `SchedulerTunings` and `WorkshopTunings` to load from JSON strings supplied by the runtime content snapshot, removing App runtime composition's direct dependency on `JObject` tuning reads.
- Changed `FortressRuntimeDependencies.Load(...)` to consume the Content-owned runtime snapshot; direct structured-registry access for runtime catalog/tuning capture is now behind `HumanFortress.Content.Loading`.
- Removed the Runtime `SimulationRuntimeContext` fallback to `ContentRegistry.Instance`; recipe and construction catalogs are now required constructor dependencies for workshop queue command targets.
- Exposed the active runtime session's recipe/construction catalogs through `SimulationRuntimeHost<TSystems>`, `GameStateManager`, and `FortressRuntimeAccess`.
- Changed workshop UI helpers, build keyboard workshop selection, workshop category mapping, workshop overlays, and workshop panel rendering to consume the active runtime construction/recipe catalogs instead of directly reading `ContentRegistry.Instance`.
- Added `IRuntimeGeologyCatalog` as a read-only geology catalog seam over the structured registry and included it in the runtime content snapshot.
- Exposed the active runtime geology catalog through `SimulationRuntimeHost<TSystems>`, `GameStateManager`, and `FortressRuntimeAccess`.
- Changed map terrain rendering, tile popups, and the simulation diff applicator to consume the injected runtime geology catalog instead of directly reading `ContentRegistry.Instance`.
- Earlier changed `RenderSnapshotBuilder` to receive an explicit construction catalog, removing its direct construction-registry read from `HumanFortress.Simulation.Rendering`; the older Simulation rendering snapshot implementation was later removed after Runtime/Contracts frame DTOs became the active path.
- Changed `ConstructionSystem` to receive explicit construction tuning plus an `IConstructionTerrainMaterialResolver`; App now provides the current Content-backed terrain-material resolver.
- Changed Jobs-owned construction execution to receive construction tuning from App composition instead of calling `ConstructionTuning.LoadFromContent()` internally.
- Moved mining channel air-geology lookup behind the mining drop resolver seam, so Jobs-owned mining result application no longer reads the global content registry.
- Changed `NavigationTuning` to parse injected JSON and removed `HumanFortress.Navigation`'s dependency on `HumanFortress.Core`.
- Changed runtime session creation to load content before creating the shared `NavigationManager`, then build navigation with the runtime snapshot's navigation tuning.
- Exposed `NavigationTuning` through the generic runtime host and runtime facade so Runtime job wrappers, navigation overlay, and debug path tooling use one active-session tuning source.
- Added `tuning.placeable` to the runtime content snapshot and injected `PlaceableTuning` into construction completion so completed placeables no longer implicitly use hard-coded defaults when content provides tuning.
- Removed unused scheduler/workshop direct file/registry tuning loaders; runtime composition now consumes scheduler/workshop tuning JSON only through the Content-owned snapshot.
- Removed unused `ConstructionTuning.LoadFromContent()` and replaced `PlaceableTuning.LoadFromContent()` with `LoadFromJson(...)`, preventing new Core-side global registry reads for tuning.
- Added smoke coverage for navigation and placeable tuning JSON parsing.
- Added mining tuning JSON to the Content-owned runtime snapshot and changed App `MiningDropResolver` to use injected `IRuntimeGeologyCatalog` plus that JSON instead of reading `ContentRegistry.Instance`.
- Changed App `ConstructionTerrainMaterialResolver` to use injected `IRuntimeGeologyCatalog`, removing the remaining construction planner adapter reads from `ContentRegistry.Instance`.
- Added mapgen/ore/cavern tuning JSON to the Content-owned runtime snapshot.
- Added `FortressGenerationContent` and injected geology plus mapgen/ore/cavern tuning into `FortressGenerator`, `FortressMap`, and `FortressChunk`, removing WorldGen production reads from `ContentRegistry.Instance`.
- Cached the active runtime content snapshot in `GameStateManager` during session content loading, then reused that same snapshot for navigation tuning, runtime dependencies, and fortress generation content.
- Changed `FortressSessionInitializer` to consume generation content through `FortressRuntimeAccess` instead of recapturing content from the global registry while generating the fortress map.
- Added zone definitions to `FortressRuntimeContentSnapshot`.
- Moved structured core-data application behind `FortressRuntimeContentSnapshotLoader.ApplyCoreData(...)`, so App no longer calls `ContentRegistry.Instance.ApplyCoreData(...)`.
- Changed `SimulationWorldContentLoader.LoadCoreContent(...)` to return the active runtime content snapshot and register zones from `snapshot.ZoneDefinitions` instead of reading `ContentRegistry.Instance.Zones.Values`.
- Simplified `ItemManager.SetDependencies(...)` to require only `World`, removing unused content-registry plumbing from App/test item-manager initialization.
- Changed `RuntimeContentRegistryLoader` to load only the structured runtime registry; the legacy `HumanFortress.Core.Content.ContentRegistry` is no longer part of normal bootstrap.
- Removed App startup and content smoke-test diagnostics wiring for the legacy registry.
- Updated content bootstrap smoke coverage to assert structured registry loading and runtime snapshot core-data application instead of legacy registry counts.
- Deleted the old `HumanFortress.Core.Content.ContentRegistry` source after splitting the still-used runtime geology JSON DTOs into `GeologyData.cs`; the structured registry is now the only runtime registry source model.
- Moved runtime geology and zone JSON DTOs to `HumanFortress.Contracts`; a later namespace cleanup moved them under `HumanFortress.Contracts.Content` and removed active `HumanFortress.Core.Content` source/test references.
- Changed structured registry geology and zone loading to use explicit `System.Text.Json` mappings instead of Newtonsoft DTO attributes/`JToken.ToObject`.
- Added smoke coverage for `zones.json` snake_case field mapping (`display_name`, `ui_hints`, and `default_policies`).
- Moved construction and recipe definitions, read-only catalog interfaces, and immutable catalog stores to `HumanFortress.Contracts`; they now use `HumanFortress.Contracts.Content.Registry` after the namespace cleanup pass.
- Moved `CoreDataLoadResult`, `ConstructionContentLoadResult`, and `RecipeContentLoadResult` to `HumanFortress.Contracts`; they now use the content contract namespace after the namespace cleanup pass.
- Deleted the unused `ConstructionRegistry` and `RecipeRegistry` singleton compatibility classes after source scans found no external `*.Instance` consumers.
- Moved `CoreDataRegistryLoader` from Core to `HumanFortress.Content.Definitions`, leaving Core with `ContentRegistry.ApplyCoreData(...)` but no construction/recipe JSON parsing ownership.
- Removed the unused `ContentRegistry.LoadCoreData(...)` compatibility method; App/tests now enter core-data loading through `CoreContentCatalogLoader`.
- Moved `IRuntimeGeologyCatalog`, `ConstructionTuning`, and `PlaceableTuning` to `HumanFortress.Contracts` while preserving their old namespace.
- Changed construction/placeable tuning JSON parsing to use `System.Text.Json`, so these cross-module contract types no longer require Newtonsoft from Core.
- Added smoke coverage for `ConstructionTuning.LoadFromJson(...)`, matching the existing navigation/placeable tuning parser coverage.
- Moved `ContentVersion`, `ContentValidationResult`, and `ContentSnapshot` to `HumanFortress.Contracts` while preserving their old namespace.
- Moved `FixedPoint`, material definitions, terrain kind definitions, geology definitions, and biome template definitions to `HumanFortress.Contracts` while preserving their old namespaces.
- Moved terrain bit-layout DTOs and alias/migration DTOs to `HumanFortress.Contracts` while preserving their old namespace.
- Deleted unused `MaterialIdRegistry`, an obsolete hard-coded material/terrain display table superseded by content-driven geology/rendering.
- Deleted unused `MaterialSelectionService`; it was a Core-owned global material preference cache with no write call sites.
- Moved the structured runtime registry implementation (`ContentRegistry`, concrete material/terrain/geology/biome registries, alias resolver, material parser, and registry diagnostics bridge) from Core into `HumanFortress.Content/Registry` while preserving the historical namespace.
- Removed Core's `Newtonsoft.Json` package reference; Content now explicitly owns the remaining `JObject` tuning-store dependency.

### Verification

- Content fast build: passed with `0 Warning(s), 0 Error(s)`
- App fast build: passed with `0 Warning(s), 0 Error(s)`
- Test project fast build: passed with `0 Warning(s), 0 Error(s)`
- Full regression test entry: passed
- App `--init-only`: passed; startup loaded 83 materials, 17 geology entries, and 19 zone definitions
- `HumanFortress.sln` fast build: passed with `0 Warning(s), 0 Error(s)`
- App analyzer build: passed with the existing 41 historical analyzer warnings and `0 Error(s)`
- `git diff --check`: passed
- Latest 2026-06-13 sub-batch:
  - Core/Jobs/App/test fast builds passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed, including the new navigation/placeable tuning JSON smoke checks
- Latest 2026-06-14 sub-batch:
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)` after rerunning sequentially; an attempted parallel App/test build reproduced the documented `obj/ref/*.dll` file-lock issue
  - Full regression test entry passed
- Latest 2026-06-14 WorldGen/content-injection sub-batch:
  - WorldGen fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed, including fortress generation determinism and FillWorld coverage
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - App `--init-only` passed; startup loaded 79 materials, 17 geology entries, and 19 zone definitions
  - App analyzer build passed with the existing 41 historical analyzer warnings and `0 Error(s)`
- Latest 2026-06-18 no-compile runtime-composition sub-batch:
  - Moved `SimulationRuntimeSystems`, `FortressRuntimeSystemsFactory`, `FortressRuntimePlanningSystems`, and `FortressRuntimeJobSystems` from App source into `HumanFortress.Runtime`.
  - Added `FortressRuntimeLogging` so Runtime composition receives App logging callbacks without calling App `Logger` directly.
  - Moved `FortressRuntimeHostFactory` and `FortressRuntimeStartup` from App source into `HumanFortress.Runtime`.
  - Changed `GameStateManager` to pass explicit runtime logging and the then App-owned auto-dig command delegate into Runtime. A later no-build cleanup moved the auto-dig command seeding into Runtime's `RuntimeAutoDigSeeder`.
  - Moved all App command source files into `HumanFortress.Runtime/Commands`, changed their namespace to `HumanFortress.Runtime.Commands`, and removed the UI enum dependency from `CreateAdvancedMiningOrderCommand`; App now maps UI mining actions before constructing the command.
  - First fast build caught two migrated debug spawn commands still calling App `Logger`; those calls were removed so Runtime commands no longer depend on App logging.
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)` after the fix.
  - Tests and headless `--init-only` were not run in this sub-batch.
  - `git diff --check` passed
- Latest 2026-06-14 SimulationWorldContentLoader content-boundary sub-batch:
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - Simulation fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - App `--init-only` passed with exit code 0; structured content log reports 83 materials, 17 runtime geology entries, 19 zone definitions, and 0 validation errors
  - App analyzer build with `--no-incremental` passed with the existing 41 historical analyzer warnings and `0 Error(s)`
- Latest 2026-06-14 structured-only bootstrap sub-batch:
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - App `--init-only` passed with exit code 0; structured content log reports 83 materials, 17 runtime geology entries, 19 zone definitions, and 0 validation errors
  - App analyzer build with `--no-incremental` passed with the existing 41 historical analyzer warnings and `0 Error(s)`
  - `git diff --check` passed
- Latest 2026-06-14 geology/zone DTO contracts sub-batch:
  - Contracts fast build passed with `0 Warning(s), 0 Error(s)`
  - Core fast build passed with `0 Warning(s), 0 Error(s)`
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - Simulation fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed, including the new zone snake_case mapping assertions
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - App `--init-only` passed with exit code 0; structured content log reports 83 materials, 17 runtime geology entries, 19 zone definitions, and 0 validation errors
  - App analyzer build with `--no-incremental` passed with the existing 41 historical analyzer warnings and `0 Error(s)`
  - `git diff --check` passed
- Latest 2026-06-15 construction/recipe contracts sub-batch:
  - Moved `ConstructionDefinition`, `MaterialCost`, `WorkshopIo`, `WorkshopAttachment`, `IConstructionCatalog`, and `ConstructionCatalogStore` into Contracts
  - Moved `RecipeDefinition`, `RecipeIngredient`, `RecipeOutput`, `IRecipeCatalog`, and `RecipeCatalogStore` into Contracts
  - Moved core-data load result DTOs into Contracts
  - Deleted unused `ConstructionRegistry` and `RecipeRegistry` singleton compatibility classes
  - Contracts fast build passed with `0 Warning(s), 0 Error(s)`
  - Core fast build passed with `0 Warning(s), 0 Error(s)`
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed
- Latest 2026-06-15 core-data loader ownership sub-batch:
  - Moved `CoreDataRegistryLoader` into `HumanFortress.Content.Definitions`
  - Removed unused `ContentRegistry.LoadCoreData(...)`; only `ApplyCoreData(...)` remains on the structured registry
  - Source scan found no remaining production/test `LoadCoreData(...)` calls
  - Core fast build passed with `0 Warning(s), 0 Error(s)`
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - App `--init-only` passed with exit code 0
  - App analyzer build passed with the existing 41 historical analyzer warnings and `0 Error(s)`
  - `git diff --check` passed
- Latest 2026-06-16 runtime content contracts sub-batch:
  - Moved `IRuntimeGeologyCatalog`, `ConstructionTuning`, and `PlaceableTuning` from Core to Contracts
  - Moved `ContentVersion`, `ContentValidationResult`, and `ContentSnapshot` from Core to Contracts
  - Moved `FixedPoint`, material, terrain kind, geology, and biome template definitions from Core to Contracts
  - Moved `TerrainBitLayout`, `BitFieldDefinition`, `AliasDefinition`, `ContentMigration`, and `MigrationRule` from Core to Contracts
  - Deleted unused `MaterialIdRegistry`
  - Deleted unused `MaterialSelectionService`
  - Replaced tuning parser Newtonsoft usage with `System.Text.Json`
  - Added `ConstructionTuning` JSON smoke coverage
  - Contracts/Core/Simulation/Jobs/App fast builds passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project run passed with `--no-build --no-restore`, including navigation/construction/placeable tuning JSON smoke checks and Phase A-D validation
- Latest 2026-06-16 structured registry implementation ownership sub-batch:
  - Moved structured registry implementation files from `src/HumanFortress.Core/Content/Registry` to `src/HumanFortress.Content/Registry`
  - Removed the Core `Newtonsoft.Json` package reference and added the explicit package reference to Content
  - Ran solution restore after the package move; this exposed a real transitive-dependency bug where WorldGen had been relying on Core's Newtonsoft package
  - Converted WorldGen mapgen/ore/cavern tuning parsing from `JObject`/`JArray` to `System.Text.Json.Nodes`, keeping Newtonsoft isolated to the Content registry compatibility layer
  - Source scan confirmed Core no longer contains structured registry implementation files or Newtonsoft usage
  - Source scan confirmed WorldGen no longer uses Newtonsoft
  - Core fast build passed with `0 Warning(s), 0 Error(s)`
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - WorldGen fast build passed with `0 Warning(s), 0 Error(s)`
  - Solution restore passed with `--ignore-failed-sources`
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project run passed with `--no-build --no-restore`, including the transport/construction/craft, mining/items/diff, core smoke, and Phase A-D suites
- Latest 2026-06-16 strict content diagnostics sub-batch:
  - Added `FortressContentLoadException`, `FortressContentLoadResult.ThrowIfInvalid(...)`, and `FortressContentLoader.LoadStrict(...)`
  - Added regression coverage for strict missing-content failures and warning promotion through `treatWarningsAsErrors`
  - Added App CLI flags `--strict-content` and `--content-warnings-as-errors`
  - Wired strict mode through `Program`, `GameStateManager`, and `SimulationWorldContentLoader` so startup registry loading and fortress session core catalog loading can fail fast
  - Documented the strict CI smoke command in `docs/operations/README-RUN.md`
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Strict `--init-only --strict-content --content-warnings-as-errors` run passed with exit code 0
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project run passed with `--no-build --no-restore`
- Latest 2026-06-16 diagnostics debug-surface sub-batch:
  - Added `DiagnosticSnapshot` and `DiagnosticIssueSummary` over the existing in-memory diagnostic ring buffer
  - Changed `Logger.Close()` to preserve the final in-memory diagnostic snapshot after flushing the async dispatcher
  - Exposed diagnostics snapshots through `GameStateManager` and `FortressRuntimeAccess`
  - Extended the Debug Status tab to show event/warning/error counts and the latest Content issue when the debug menu is open
  - Kept snapshot construction gated behind `ui.DebugOpen` so normal frame rendering does not rebuild diagnostic summaries every frame
  - Extended async diagnostic smoke coverage for snapshot counts, category counts, and parsed Content issue codes
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project run passed with `--no-build --no-restore`
- Latest 2026-06-16 runtime/jobs boundary cleanup sub-batch:
  - Moved initial worker seeding into `HumanFortress.Runtime.SimulationInitialWorkerSpawner`, with App now passing only the logger callback
  - Added `HumanFortress.Runtime.StartupDigTargetFinder` and routed both App auto-dig bootstrap paths through it, removing duplicated dig-target search logic from App runtime helpers
  - Added smoke coverage for startup dig-target lookup and one-time initial-worker seeding
  - Moved `SchedulerTunings`, `WorkshopTunings`, and `WorkerSelectionStrategy` source ownership into `HumanFortress.Jobs/Configuration`; a later namespace cleanup moved these to `HumanFortress.Jobs`
  - Changed scheduler/workshop tuning parse failures to use injected log callbacks instead of depending on App `Logger`
  - Moved `SanitizeSystem` source ownership into `HumanFortress.Jobs/Safety`, with App composition passing the logger callback
  - Moved profession contracts into `HumanFortress.Contracts`, moved `ProfessionAssignments` source ownership into `HumanFortress.Jobs/Profession`, and moved `ProfessionRegistry` file loading into `HumanFortress.Content/Definitions`
  - Added Jobs-owned `IUnified*JobExecutor` orchestration interfaces and moved `UnifiedJobsOrchestrator` source ownership into `HumanFortress.Jobs/Orchestration`
  - Runtime-owned job-system wrappers now implement the small orchestration interfaces instead of being concrete dependencies of the orchestrator
  - Moved concrete transport/mining/construction/craft diff emitters into `HumanFortress.Jobs/Diff` while preserving transitional namespace compatibility
  - Moved transport/mining/craft profession and recipe adapters into `HumanFortress.Jobs/Profession`
  - Moved callback-backed transport/mining/construction job loggers into `HumanFortress.Jobs/Logging`
  - Moved construction terrain-material resolution into `HumanFortress.Jobs/Construction`
  - Moved mining drop/tuning resolution into `HumanFortress.Jobs/Mining` and converted it from Newtonsoft `JObject`/`JArray` parsing to `System.Text.Json.Nodes`
  - Moved tick-facing transport/mining/construction/craft job-system wrappers into `HumanFortress.Runtime/Jobs`; a later namespace cleanup moved these to `HumanFortress.Runtime.Jobs`
  - Collapsed the construction workshop-completion sink into the Runtime-owned construction wrapper as a callback bridge; App now only binds the UI callback during bootstrap
  - Added a transitional `HumanFortress.Runtime` internals bridge to `HumanFortress.Jobs` so Runtime composition can consume Jobs-owned internal diff emitters/adapters while namespace cleanup remains pending
  - `HumanFortress.App/Jobs` no longer contains active source files; later namespace cleanup removed the old App-owned Jobs namespace from active source
  - Added smoke coverage for unified job orchestration order, mining-backlog hauling hints, and intake-stat propagation
  - Added smoke coverage for mining tuning JSON parsing, geology alias drop lookup, air-handle lookup, and wall/ramp tick resolution
  - Runtime fast build passed with `0 Warning(s), 0 Error(s)`
  - Jobs fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project run passed with `--no-build --no-restore`
- Latest 2026-06-14 legacy-registry-source deletion sub-batch:
  - Deleted `src/HumanFortress.Core/Content/ContentRegistry.cs`
  - Added `src/HumanFortress.Core/Content/GeologyData.cs` for the runtime geology DTOs still consumed by the structured registry
  - Source scan found no remaining production/test references to `HumanFortress.Core.Content.ContentRegistry`, `MaterialData`, or the old registry diagnostics fields
  - Core fast build passed with `0 Warning(s), 0 Error(s)`
  - Content fast build passed with `0 Warning(s), 0 Error(s)`
  - Simulation fast build passed with `0 Warning(s), 0 Error(s)`
  - WorldGen fast build passed with `0 Warning(s), 0 Error(s)`
  - App fast build passed with `0 Warning(s), 0 Error(s)`
  - Test project fast build passed with `0 Warning(s), 0 Error(s)`
  - Full regression test entry passed
  - `HumanFortress.sln` fast build passed with `0 Warning(s), 0 Error(s)`
  - App `--init-only` passed with exit code 0; structured content log reports 83 materials, 17 runtime geology entries, 19 zone definitions, and 0 validation errors

### Important Notes

- This removes the App-owned host wrapper but does not yet move concrete gameplay system composition fully out of App.
- The next runtime boundary target is the new runtime system group layer: move concrete session/system group composition out of App when Runtime has clean UI/log/content callback seams.
- Runtime context and runtime systems should continue receiving the same catalog snapshot references from `FortressRuntimeDependencies`; do not reintroduce independent `ContentRegistry.Instance` reads in host construction.
- Runtime composition should consume `FortressRuntimeContentSnapshot` through `FortressRuntimeDependencies.Load(...)`; keep structured-registry reads behind the Content snapshot loader rather than host/system factories.
- Runtime command targets and App UI helpers should consume active-session catalog facts through Runtime snapshot/query DTOs or explicit runtime dependencies; do not add fallback reads to `ContentRegistry.Instance` for construction/recipe UI convenience paths, and do not re-expose construction/recipe catalogs through `FortressRuntimeAccess`.
- Jobs/Runtime/Simulation execution paths should receive catalog/tuning/geology dependencies explicitly. `IRuntimeGeologyCatalog`, `ConstructionTuning`, and `PlaceableTuning` now compile from Contracts; App/Content may bridge to the transitional structured registry, but Jobs/Runtime/Simulation should not reach for it directly.
- Navigation and placeable tunings now follow the same rule as construction/scheduler/workshop tunings: Content captures JSON once, App composition parses it, and runtime systems receive explicit objects.
- Mining drop/tick tuning now follows the same snapshot rule. Do not add new mining resolver reads from `ContentRegistry.Instance`.
- WorldGen now follows the same active-session snapshot rule: fortress generation receives explicit `FortressGenerationContent` with geology plus mapgen/ore/cavern tuning. Do not add new `ContentRegistry.Instance` reads to `FortressGenerator`, `FortressMap`, or `FortressChunk`.
- Runtime world-content loading should call `FortressRuntimeContentSnapshotLoader.ApplyCoreData(...)` and consume the returned snapshot; do not add direct `ContentRegistry.Instance.ApplyCoreData(...)` or `ContentRegistry.Instance.Zones` reads back into `SimulationWorldContentLoader`, and do not add App logging calls directly to that Runtime helper.
- The remaining production direct structured-registry reads are now concentrated in Content bootstrap/snapshot capture/application and the Content-owned structured registry internals themselves.
- Normal startup now loads the structured runtime registry only. Do not re-add legacy registry loading to `RuntimeContentRegistryLoader`.
- The legacy `HumanFortress.Core.Content.ContentRegistry` source has been deleted. Do not reintroduce a second runtime registry source model or the old construction/recipe singleton registries.
- `HumanFortress.Navigation` must not regain a Core/Content reference for tuning convenience. Pass `NavigationTuning` into `NavigationManager`, `PathService`, overlay/debug helpers, or runtime factories.
- `GameStateManager` still owns UI-facing state transitions, but scheduler/command/diff/session controls now live behind Runtime session ports implemented by the internal session core, and runtime-facing UI/session controls flow through `GameStateRuntimeCoordinator`/`FortressRuntimeAccess` role interfaces plus Runtime-owned snapshot builders. Continue shrinking the remaining live World/navigation bootstrap bridges instead of adding gameplay reads back to App.

## Previous Batch: Content Load Boundary Consolidation

Status: completed

### Completed

- Add a `HumanFortress.Content`-owned load coordinator/result that loads the currently split content catalogs in one place:
  - item definition catalog
  - creature definition catalog
  - construction catalog
  - recipe catalog
- Keep `Simulation` independent from `Content`; runtime managers should still receive snapshots.
- Keep `ContentRegistry` as the transitional structured runtime registry for geology/tuning/zones while core-data snapshot loading is folded behind the Content assembly.
- Update App startup and tests to consume the unified Content load result instead of calling item/creature loaders and Core-data loading separately.
- Preserve current compatibility behavior and diagnostics.
- Added `CoreContentCatalogLoader` and `CoreContentCatalogLoadResult` in `HumanFortress.Content.Definitions`.
- Moved `CoreDataRegistryLoader` into `HumanFortress.Content.Definitions` so Content coordinates construction/recipe core-data loading without App parsing JSON.
- Added `ContentRegistry.ApplyCoreData(...)` so App/runtime composition can apply construction/recipe snapshots loaded through the unified Content result.
- Changed `SimulationWorldContentLoader` to call `CoreContentCatalogLoader.Load(...)` once, then apply item, creature, construction, and recipe snapshots from that result.
- Changed test support and content smoke tests to use `CoreContentCatalogLoader`.
- Removed the old `ContentRegistry.LoadCoreData(...)` compatibility API after App/tests moved to the Content-owned loader.
- Added `FortressContentLoader` in `HumanFortress.Content.Loading` as the Content-owned runtime bootstrap entry:
  - resolves published vs source-checkout `content/`
  - resolves published vs source-checkout `data/core`
  - loads the legacy/structured runtime registries when needed
  - optionally loads the unified core catalog snapshots
- Added `RuntimeContentRegistryLoader` in `HumanFortress.Content.Loading` and removed the old Core-owned `ContentLoadCoordinator`, so registry bootstrap orchestration now belongs to Content.
- Added `FortressContentIssue` diagnostics with severity/code/message, plus `FortressContentLoadResult.IsValid(...)`, `GetBlockingIssues(...)`, and `FormatBlockingIssues(...)`.
- Added App-side content issue logging through `FortressContentIssueLogger`.
- Added `ResolveRegistryFile(...)` so App-side UI/input/profession convenience registries no longer hard-code `baseDir/content/registries`.
- Removed unused old-registry parameters from the incomplete stockpile hauling/filter placeholder path; future stockpile filtering should depend on item definition catalog seams, not the legacy content registry.
- Changed `Program` and `SimulationWorldContentLoader` to enter content loading through `FortressContentLoader` instead of owning their own path discovery and coordinator calls.
- Changed scheduler/workshop tunings to load from the already-loaded structured registry during runtime composition instead of reading tuning JSON directly from App.
- Switched input bindings, orders display registry, profession registry, workshop category mapping, and legacy tuning compatibility loaders to use the Content-owned registry-file resolver.
- Confirmed App now has no direct `Path.Combine(baseDir, "content", "registries", ...)` call sites; that knowledge is centralized in `HumanFortress.Content`.

### Verification

- Content build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project run: passed, including content-load smoke, definition reload, transport/construction/craft, mining/items/diff, core runtime smoke checks, and Phase A-D validation
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed and still reports `79 materials`
- App analyzer build: passed with existing 41 analyzer warnings and `0 Error(s)`
- `git diff --check`: passed
- Path scan: no App/test call sites directly combine `baseDir/content/registries`; runtime registry bootstrap enters through `FortressContentLoader` / `RuntimeContentRegistryLoader`.

### Audit Context

- `docs/archive/plans/HUMANFORTRESS_MAIN_BRANCH_ARCHITECTURE_AUDIT_FOR_CODEX.md` was read on 2026-06-12.
- Its `HumanFortress.Content` build concern is no longer a current build blocker; the solution builds successfully.
- Its larger Content concern is now substantially reduced: item, creature, construction, recipe, runtime registry bootstrap, structured registry implementation, and App registry-file path resolution now enter through `HumanFortress.Content`.
- Remaining Content work is strict content-mode diagnostics, richer debug
  surfaces, and future compiled-pack support. Later namespace cleanup moved the
  formerly non-registry runtime DTO compatibility names into Contracts registry
  namespaces, and architecture smoke/source scans guard the old compatibility
  namespaces from returning.
- The agreed next priority after the remaining Content hygiene is moving concrete runtime composition out of App.

### Important Notes

- At that point, the legacy `HumanFortress.Core.Content.ContentRegistry` source still existed, but `RuntimeContentRegistryLoader` loaded only the structured runtime registry. A later sub-batch deleted the old source after splitting `GeologyData` into its own file.
- The structured `HumanFortress.Content.Registry.ContentRegistry` remains the runtime registry for geology handles, tuning, zones, construction catalogs, and recipe catalogs; its implementation now compiles from `HumanFortress.Content.Registry`.
- `FortressContentLoader` is a Content-owned facade over the structured registry and catalog snapshot loaders; do not add another App-side bootstrapper.
- Core no longer owns a legacy/structured registry coordinator, construction/recipe singleton registries, the construction/recipe core-data JSON loader, or the structured registry implementation. The remaining cleanup is policy/diagnostics, not a second runtime registry source model or compatibility-namespace migration.
- Remaining direct references to `HumanFortress.Core.Content.ContentRegistry` are now historical documentation/source-compatibility references, not normal bootstrap requirements.
- Do not run overlapping .NET project builds in parallel. A parallel Content/App/test build reproduced file-lock failures on `HumanFortress.Content/obj/Debug/net8.0/*.dll`.

## Previous Batch: Construction and Recipe Catalog Snapshots

Status: completed

### Completed

- Added immutable construction and recipe catalog snapshots:
  - `ConstructionCatalogStore`
  - `RecipeCatalogStore`
- Changed the core-data loading path so it parses construction/workshop and recipe JSON into fresh catalog snapshots instead of mutating `ConstructionRegistry.Instance` and `RecipeRegistry.Instance`.
- Changed `ContentRegistry` to own current construction/recipe snapshots as instance fields exposed through `IConstructionCatalog` and `IRecipeCatalog`.
- `ContentRegistry.ApplyCoreData(...)` now swaps the current construction/recipe snapshots from the Content-owned load result, matching the item/creature snapshot pattern.
- `ContentRegistry.LoadContent(...)` / `LoadContentAsync(...)` now resets construction/recipe snapshots to empty as part of runtime content clearing.
- Changed App craft composition so `CraftRecipeCatalogAdapter` receives an explicit `IRecipeCatalog` instead of reading `ContentRegistry.Instance.Recipes` internally.
- Added regression coverage proving repeated core-data loads keep construction/recipe counts and workshop/category queries stable instead of accumulating duplicate indexes.

### Verification

- Core build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project run: passed, including content-load idempotence, transport/construction/craft, mining/items/diff, core runtime smoke checks, and Phase A-D validation
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed and still reports `79 materials`
- App analyzer build: passed with existing 41 analyzer warnings and `0 Error(s)`
- `git diff --check`: passed

### Important Notes

- Later batches deleted `ConstructionRegistry` / `RecipeRegistry` singleton compatibility classes and moved construction/recipe definitions plus catalog stores to Contracts.
- Later batches also made the concrete structured `ContentRegistry` internal. UI/App/runtime callers should use `FortressRuntimeContentSnapshotLoader`, Content loader facades, Runtime snapshots, or explicit injected catalog interfaces rather than `ContentRegistry.Instance`.
- The remaining content work is richer diagnostics/debug surfaces, future compiled-pack support, and removing any leftover historical compatibility language from older docs.

## Previous Batch: Content-Owned Item and Creature Definition Loading

Status: completed

### Completed

- Added `HumanFortress.Content` as a real project and included it in `HumanFortress.sln`.
- Moved static item and creature JSON loading/parsing/validation into `HumanFortress.Content.Definitions`:
  - `ItemDefinitionCatalogLoader`
  - `CreatureDefinitionCatalogLoader`
- The Content loaders now produce immutable catalog snapshots:
  - `ItemDefinitionCatalogStore`
  - `CreatureDefinitionCatalogStore`
- Removed the old Simulation-owned definition loader files:
  - `Simulation/Items/ItemDefinitionLoader.cs`
  - `Simulation/Creatures/CreatureDefinitionLoader.cs`
- Removed `ItemManager.LoadDefinitions(...)` and `CreatureManager.LoadDefinitions(...)`; managers now consume prebuilt snapshots through `SetDefinitionCatalog(...)`.
- Kept Simulation independent from the Content project. The dependency direction is now `App/tests -> Content -> Contracts`, while Simulation only consumes contract/store types.
- Changed App startup composition so `SimulationWorldContentLoader` loads item/creature catalogs through `HumanFortress.Content`, logs loader diagnostics, and injects the snapshots into the active world managers.
- Added test-side `DefinitionCatalogTestSupport` so regression tests explicitly load catalog snapshots and inject them without relying on manager file IO.

### Verification

- Content build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Simulation build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project run: passed, including transport/construction/craft, mining/items/diff, core runtime smoke checks, and Phase A-D validation
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed and still reports `79 materials`
- App analyzer build: passed with existing 41 analyzer warnings and `0 Error(s)`
- `git diff --check`: passed

### Important Notes

- This batch deliberately avoided `Simulation -> Content`; file parsing now belongs to Content, runtime managers consume snapshots.
- The item/creature DTO namespaces are still transitional, but assembly ownership is now Contracts and loader ownership is now Content.
- Content loaders still preserve current compatibility behavior: root-array item files, `{ "items": [...] }` envelopes, furniture/placeable profile parsing, generic resource-name enrichment, and current validation messages.
- The following batch applied the same snapshot pattern to construction and recipe data. The next content unification step is folding all catalog loading into one coherent Content load result.

## Previous Batch: Static Definition Contracts Migration

Status: completed

### Completed

- Moved shared static definition DTOs into `HumanFortress.Contracts`:
  - `ItemDefinition`
  - `CreatureDefinition`
  - item feature blocks such as `StackBlock`, `EquipBlock`, `WeaponBlock`, `ContainerBlock`, and `UseBlock`
  - shared placeable DTOs `PlaceableProfile`, `Footprint`, `PassabilityMode`, and `EffectsBlock`
- Moved read-only item/creature definition catalog interfaces into `HumanFortress.Contracts`:
  - `IItemDefinitionCatalog`
  - `ICreatureDefinitionCatalog`
- Moved immutable definition catalog snapshot stores into `HumanFortress.Contracts`:
  - `ItemDefinitionCatalogStore`
  - `CreatureDefinitionCatalogStore`
- Added explicit project references:
  - `HumanFortress.Core -> HumanFortress.Contracts`
  - `HumanFortress.Simulation -> HumanFortress.Contracts`
- Preserved the existing namespaces as a transitional compatibility step, matching the earlier Navigation contracts migration pattern. This avoids a broad namespace rewrite while still moving assembly ownership to Contracts.
- Left `ItemDefinitionLoader` and `CreatureDefinitionLoader` in Simulation for that batch only; they were moved into `HumanFortress.Content` in the following batch.
- Left the local internal `StockpileFilter.ItemDefinition` placeholder untouched because it is a separate stockpile filtering stub, not the runtime item definition contract.

### Verification

- Contracts build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Core build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Simulation build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed
- App analyzer build: passed with existing 41 analyzer warnings and `0 Error(s)`

### Important Notes

- This is an assembly-boundary migration, not a namespace cleanup. The types now compile from Contracts but still use the old namespaces for compatibility.
- The next content step after this batch moved item/creature definition loading out of Simulation and behind the Content assembly.
- A later cleanup pass should rename these contracts into a true content/contracts namespace after runtime ownership is stable.

## Previous Batch: Immutable Item and Creature Definition Catalog Stores

Status: completed

### Completed

- Added internal immutable snapshot stores:
  - `ItemDefinitionCatalogStore`
  - `CreatureDefinitionCatalogStore`
- Changed `ItemManager` to replace its static definition catalog snapshot on `LoadDefinitions` instead of owning mutable definition/kind/tag dictionaries directly.
- Changed `CreatureManager` to replace its static definition catalog snapshot on `LoadDefinitions` instead of owning mutable definition/tag dictionaries directly.
- Kept `ItemManager` and `CreatureManager` implementing the existing read-only catalog interfaces, so external callers do not need a broad migration in this pass.
- Preserved duplicate-definition compatibility: loader `LoadedCount` still reflects loaded valid entries, while final `DefinitionCount` reflects the last-definition-wins catalog snapshot.
- Stabilized the legacy Phase D concurrent pathfinder test by giving that test a wider pathing time budget. The old test accidentally treated normal 3ms tick-budget deferral as pathfinding failure under slower thread scheduling.

### Verification

- Simulation build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed, including definition reload and Phase A-D validation
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed
- App analyzer build: passed with existing 41 analyzer warnings and `0 Error(s)`

### Important Notes

- Static item/creature data is now a snapshot inside the managers, but the content system still does not own those snapshots.
- `ItemDefinitionCatalogStore` and `CreatureDefinitionCatalogStore` are internal Simulation types because `ItemDefinition` and `CreatureDefinition` still live in Simulation.
- The next content boundary step is making these snapshots produced by the structured content registry/load coordinator, then passing them into the runtime managers.

## Previous Batch: Item and Creature Definition Loader Extraction

Status: completed

### Completed

- Extracted static item JSON loading/parsing/validation/normalization from `ItemManager` into `ItemDefinitionLoader`.
- Extracted static creature JSON loading/validation from `CreatureManager` into `CreatureDefinitionLoader`.
- Kept the existing public manager API compatible, so runtime callers can still use `LoadDefinitions`, `GetDefinition`, kind/tag queries, and runtime item/creature instance APIs without a broad migration in the same pass.
- Preserved current compatibility behavior for item content:
  - root-array item files still load;
  - `{ "items": [...] }` item envelopes still load;
  - legacy furniture/placeable profile parsing still works;
  - generic material names such as boulder/block/plank/log are still enriched from material ids;
  - construction/material validation still receives structured content registry context.
- Changed manager reload behavior to clear and rebuild definition/tag/kind indexes from the loaded definition set, instead of letting repeated loads accumulate duplicate index entries.
- Added regression coverage proving repeated item and creature definition loads keep definition counts and kind/tag query counts stable.

### Verification

- Simulation build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed, including `Definition catalog reload indexes`
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed
- App analyzer build: passed with existing analyzer warnings and `0 Error(s)`

### Important Notes

- This is a loader extraction, not the final content-ownership move.
- `ItemDefinition` and `CreatureDefinition` still live in `HumanFortress.Simulation`, so the loaders currently stay in Simulation too.
- Startup still invokes item/creature definition loading through `world.Items.LoadDefinitions(...)` and `world.Creatures.LoadDefinitions(...)` for compatibility.
- The next content step should introduce immutable item/creature definition catalog snapshots, then let managers consume those snapshots instead of owning the static definition dictionaries directly.

## Previous Batch: Item and Creature Definition Catalog Seams

Status: completed

### Completed

- Added Simulation-level read-only definition catalog interfaces:
  - `IItemDefinitionCatalog`
  - `ICreatureDefinitionCatalog`
- Made `ItemManager` implement `IItemDefinitionCatalog`, preserving its existing runtime instance ownership.
- Made `CreatureManager` implement `ICreatureDefinitionCatalog`, preserving its existing runtime instance ownership.
- Migrated construction material matching in `HumanFortress.Jobs.Construction.ConstructionMaterialTracker` to use `IItemDefinitionCatalog` for item definition lookup.
- Migrated `ConstructionMaterialsPlanner` to receive `IItemDefinitionCatalog` explicitly instead of reading definitions through the full item manager API.
- Migrated App runtime composition to pass `world.Items` as the item definition catalog for construction material planning.
- Migrated `ProfessionAssignments` roster naming to use an injected `ICreatureDefinitionCatalog`, with App runtime composition passing `world.Creatures`.
- Confirmed the remaining direct item/creature definition reads are primarily UI/render/debug presentation code and can be handled separately.

### Verification

- Simulation build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed
- App analyzer build: passed with existing analyzer warnings and `0 Error(s)`
- `git diff --check`: passed

### Important Notes

- Item and creature JSON loading still lives in `ItemManager.LoadDefinitions` and `CreatureManager.LoadDefinitions`. This batch only separates the read-only definition surface from the full runtime managers.
- Moving item/creature definitions into a structured content-owned immutable catalog is a larger step because the definition types currently live in `HumanFortress.Simulation`.
- Future work should split static definition loading/validation from runtime instance management, then move the definition types/catalogs toward the content boundary.
- Avoid mixing App/test build commands in parallel. During this batch, parallel App/test builds reproduced the known macOS `apphost` signing/copy race; sequential rebuilds passed immediately.

## Previous Batch: Content Catalog Boundary Hardening

Status: completed

### Completed

- Added read-only catalog interfaces in Core:
  - `IConstructionCatalog`
  - `IRecipeCatalog`
- Changed `HumanFortress.Content.Registry.ContentRegistry` to expose construction and recipe content through read-only catalog interfaces instead of concrete mutable registry types.
- Kept `ConstructionRegistry` and `RecipeRegistry` as internal compatibility stores for that batch. A later batch replaced the normal `ContentRegistry.LoadCoreData` path with immutable construction/recipe snapshots.
- Migrated runtime/gameplay read paths from direct singleton access to catalog access:
  - Runtime workshop queue commands
  - buildable construction planning
  - construction completion application
  - craft workshop lookup/planning/execution
  - craft recipe adapter
  - App workshop UI, map click, overlay, and workshop category helpers
  - content smoke assertions
- Injected `IConstructionCatalog` into Jobs-owned construction/craft executors and locators, so Jobs no longer directly grabs construction definitions from a global construction singleton.
- At that point, App runtime composition resolved `ContentRegistry.Instance.Constructions` once and passed the catalog to buildable construction, construction jobs, craft planner, and craft jobs. A later batch moved this capture behind `FortressRuntimeContentSnapshot`.
- Replaced remaining recipe-registry test fixture writes with small in-memory test catalogs. At that time the old singleton compatibility classes were still isolated behind the structured registry; later batches deleted those classes entirely.

### Verification

- Core build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed
- Solution build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- `--init-only`: passed
- App analyzer build: passed with existing analyzer warnings and `0 Error(s)`
- `git diff --check`: passed

### Important Notes

- This is a boundary hardening pass, not the final deletion of the concrete registry classes.
- The concrete construction/recipe registry singletons are still present, but direct references are now contained inside `ContentRegistry`.
- `SimulationRuntimeContext` later removed its fallback to `ContentRegistry.Instance`; runtime contexts now require explicit recipe/construction catalogs.
- Later batches moved runtime snapshot capture/application behind `HumanFortress.Content`; remaining production content-global reads are now concentrated in Content bootstrap/snapshot internals.

## Previous Batch: Core Data Registry Loading Unification

Status: completed

### Completed

- Moved construction/workshop and recipe JSON loading behind `HumanFortress.Content.Registry.ContentRegistry.LoadCoreData`.
- Added a Core-owned `CoreDataRegistryLoader` that parses:
  - `data/core/workshops/core_workshop_*.json`
  - legacy `data/core/placeable/workshops.json`
  - `data/core/recipes/*.json`
- Preserved the existing compatibility behavior while moving ownership out of App:
  - workshop definitions from new and legacy files are both loaded
  - duplicate construction ids are skipped and counted instead of failing the load
  - recipe root arrays and `{ "recipes": [...] }` documents are both supported
  - recipe aliases such as `workshops`, `workshop_id`, `workshop`, `work_time.duration_ticks`, `duration_ticks`, `skill.primary`, and `primary_skill` still parse
- Exposed `ContentRegistry.Constructions` and `ContentRegistry.Recipes` as transitional sub-registry accessors over the existing singleton registries.
- Simplified `SimulationWorldContentLoader`: App still locates the active `data/core` path and, at that time, still loaded creature/item managers directly, but no longer contained construction or recipe JSON parsing logic.
- Expanded content smoke coverage to prove `ContentRegistry.LoadCoreData` loads constructions and recipes without errors and populates known construction/recipe ids.

### Verification

- Core build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build with analyzers: passed with existing analyzer warnings and `0 Error(s)`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed, including the new construction/recipe content smoke assertions
- `--init-only`: passed
- `git diff --check`: passed before doc updates

### Important Notes

- This removes App-level file parsing for construction and recipe content, but it does not yet delete `ConstructionRegistry` or `RecipeRegistry`.
- `ConstructionRegistry` and `RecipeRegistry` are now better treated as compatibility sub-registries under the structured registry boundary. The next content pass should either fold their storage into `ContentRegistry` or hide them behind read-only catalog interfaces.
- At that point, creature and item definition loading still lived in their managers. This was completed later by moving their loaders into `HumanFortress.Content.Definitions` and injecting catalog snapshots.
- At that point, the legacy `HumanFortress.Core.Content.ContentRegistry` still existed for compatibility while material/geology migration was completed. Later batches deleted that legacy registry source.

## Previous Batch: Runtime Content Registry Unification

Status: completed

### Completed

- Promoted `HumanFortress.Content.Registry.ContentRegistry` toward the single authoritative content registry by adding runtime content capabilities that previously only existed in the legacy registry:
  - runtime `geology.json` loading
  - deterministic geology handle assignment
  - `GetGeologyHandle`
  - `GetGeologyByHandle`
  - `TryGetGeologyHandleByMaterialAndKind`
  - `tuning.*.json` loading and `GetTuning<T>`
  - `zones.json` loading and `GetZoneDefinition`
- Kept `content/registries/geology.json` as the current canonical runtime terrain prototype source because the active game still depends on `core_terrain_*` and `core_mat_*` ids.
- Added material alias indexing for geology lookups, so runtime calls such as `air + OpenNoFloor` can resolve to `core_terrain_air`.
- Migrated low-risk tuning and zone call sites to the structured registry:
  - `NavigationTuning`
  - `ConstructionTuning`
  - `PlaceableTuning`
  - active session zone registration
- Migrated core runtime geology readers to the structured registry:
  - `FortressGenerator`
  - `FortressMap`
  - `SimulationDiffApplicator`
  - `ConstructionSystem`
  - `MiningDropResolver`
  - `MiningResultApplier`
  - fortress map/tile renderers
- Expanded content smoke coverage to prove the structured registry can resolve runtime geology handles, tuning, and zones.

### Verification

- Core build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build with analyzers: passed with existing analyzer warnings and `0 Error(s)`
- Test project build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- Test DLL run: passed, including FortressMap-to-World conversion through the structured registry
- `--init-only`: passed
- `git diff --check`: passed

### Important Notes

- At that point, the legacy `HumanFortress.Core.Content.ContentRegistry` still existed and was loaded by the old `ContentLoadCoordinator` for compatibility. Later batches deleted that legacy source and retired the coordinator path.
- At that point, the console summary from the legacy registry still appeared during startup. Later batches moved normal startup to the structured registry behind `FortressContentLoader`.
- At that point, `ConstructionRegistry`, `RecipeRegistry`, item definitions, and creature definitions still had separate loading paths. Later batches moved item/creature loading into `HumanFortress.Content.Definitions` and changed construction/recipe loading to produce immutable snapshots.
- `geology_prototypes.json` remains present but should not override runtime `geology.json` until its ids and material references are aligned with the active `core_terrain_*` content model.

## Previous Batch: Content Registry Bootstrap Unification

Status: completed

### Completed

- Added the first shared loading entry point while the legacy and structured content registries still coexist. This was later superseded by `HumanFortress.Content.Loading.RuntimeContentRegistryLoader`.
- App startup now loads both:
  - legacy `HumanFortress.Core.Content.ContentRegistry`
  - structured `HumanFortress.Content.Registry.ContentRegistry`
- `SimulationWorldContentLoader` now has a headless/session safety check that loads the registries if a caller bypasses `Program`.
- Fixed structured registry reload hygiene by resetting `ValidationResult`, `ContentHash`, and `IsLoaded` before each load.
- Fixed structured material loading for top-level array files such as `materials.authoring.json`.
- Fixed content diagnostic severity so validation summaries with warnings are not misclassified as errors.
- Fixed material category inference so materials tagged `ore` satisfy terrain-kind `ore` category validation.
- Added smoke coverage proving the coordinator loads both registry models and resolves structured material/terrain lookups.

### Verification

- App build: passed with `0 Warning(s), 0 Error(s)` using `RunAnalyzers=false`
- App build with analyzers: passed with existing warnings and `0 Error(s)`
- `--init-only`: passed; content summary prints once and structured registry reports `0 warnings, 0 errors`
- Test project build: passed with `0 Warning(s), 0 Error(s)`
- Test DLL run: passed, including the new content load coordinator smoke test
- `HumanFortress.sln` build: passed with `0 Warning(s), 0 Error(s)`
- `git diff --check`: passed

### Important Notes

- This is bootstrap unification, not final content architecture. The two registry classes still exist.
- The structured registry treats the missing `content/templates/biomes` directory as optional because current WorldGen still uses enum/tuning based biomes, not biome templates.
- Recipes, construction definitions, item definitions, and creature definitions still have separate loading paths.
- The next content step should decide whether geology handles and tuning move into the structured registry or whether a new read-only content catalog facade should sit above both.

## Previous Batch: Structured Diagnostics First Pass

Status: completed

### Completed

- Added `HumanFortress.Contracts.Diagnostics` primitives:
  - `DiagnosticLevel`
  - `DiagnosticEvent`
  - `IDiagnosticSink`
  - `NullDiagnosticSink`
  - `DiagnosticSinkExtensions`
  - transitional `DiagnosticHub`
- Reworked App `Logger` into an async diagnostics facade while keeping the existing `Logger.Log(string)` compatibility API.
- Added an async background dispatcher so simulation/UI/runtime threads enqueue diagnostic events without writing files directly.
- Added a main timeline log plus category-routed logs:
  - `fortress_debug.log`
  - `logs/app.log`
  - `logs/content.log`
  - `logs/core.log`
  - `logs/jobs.log`
  - `logs/navigation.log`
  - `logs/runtime.log`
  - `logs/simulation.log`
  - `logs/ui.log`
- Added an in-memory ring-buffer sink for a later UI/debug diagnostics panel.
- Added a Simulation-local `SimulationDiagnostics` helper so Simulation systems can use the Contracts diagnostics bridge without depending on App.
- Routed startup `ContentRegistry` diagnostics through `IDiagnosticSink` while preserving its console summary for command-line visibility.
- Routed the secondary `HumanFortress.Content.Registry.ContentRegistry` and its material/terrain/geology/biome/alias helper registries through a shared content diagnostics helper with console fallback when App logging is not initialized.
- Bridged existing static lower-level callbacks into categorized diagnostics:
  - `NavigationManager`
  - `CreatureManager`
  - `ItemManager`
  - `SimulationDiffApplicator`
  - `OrdersManager`
  - `MiningSystem`
  - `ConstructionMaterialsPlanner`
- Replaced direct App UI/runtime initialization `Console.WriteLine` calls in the fortress setup/render-snapshot path with `Logger` calls.
- Routed Core `CommandQueue`, `TickScheduler`, and `EventBus` error paths through `DiagnosticHub`, so they can land in `core.log` once App initializes logging.
- Routed WorldGen fortress fill/conversion progress and generator-stage errors through `DiagnosticHub`, with console fallback only when App logging is not initialized.
- Routed Simulation diff, item diff, creature diff, stockpile diff, mining, hauling, orders, and construction-material planner diagnostics through the same diagnostics bridge.
- Converted `OrdersManager.LogCallback` and `MiningSystem.LogCallback` from visible static fields to properties while preserving current App wiring.
- Added smoke coverage proving async diagnostics flush and route to category files.

### Verification

- App build: passed with `0 Warning(s), 0 Error(s)`
- `./RunTests.sh`: passed with `0 Warning(s), 0 Error(s)`
- `--init-only`: passed and generated `fortress_debug.log` plus category logs under `logs/`
- `HumanFortress.sln` build: passed with `0 Warning(s), 0 Error(s)`
- `git diff --check`: passed

### Important Notes

- This is a first-pass logging architecture, not a total cleanup of every direct console write in the repository.
- `Contracts` should remain gameplay-free and should not own file sinks or UI presentation. Its diagnostics namespace defines event/sink contracts plus the transitional hub used by modules that are not yet fully constructor-injected.
- `HumanFortress.Contracts.Diagnostics.DiagnosticHub` is transitional. Prefer constructor-injected `IDiagnosticSink` for new long-lived systems once composition boundaries are stable.
- Existing test runs still print manager diagnostics when no App logger is initialized; that fallback is intentional for headless/local test visibility.
- Remaining direct console output is mostly command-line compatibility messages, diagnostic fallbacks when no App logger is initialized, startup content summary, and test output.

## Previous Batch: Runtime Assembly Expansion

Status: completed

### Completed

- Added the first real `HumanFortress.Runtime` assembly and wired it into `HumanFortress.sln`.
- Added a Runtime-owned command-context seam over `ISimulationContext` for command-stage tick ownership; this has since been split into explicit clock and target roles.
- Moved `SimulationCommandStage` out of App and into `HumanFortress.Runtime`.
- Moved `SimulationStatus` out of App and into `HumanFortress.Runtime` as a public runtime clock/control snapshot DTO.
- Moved `SimulationTickPipeline` out of App and into `HumanFortress.Runtime`, so pre-tick command execution, post-tick diff application, creature diff application, item diff application, and dirty-chunk navigation rebuilds now live in the runtime assembly.
- Moved the Simulation-to-Navigation adapter into Runtime:
  - `SimulationNavigationSource`
  - `SimulationNavigationFactory`
- Added Runtime-owned `IRuntimeTickSystems` and `SimulationRuntimeHostCore`, moving scheduler restart, system registration, tick-pipeline attachment, and stop-time pipeline detachment out of App.
- Moved the immutable session handle into Runtime as `SimulationRuntimeSession<THost>`, keeping the App-specific host wrapper type outside Runtime.
- Moved the new-session factory into Runtime as `SimulationRuntimeSessionFactory<THost>`. App now supplies content-loading and host-wrapper callbacks instead of owning the world/navigation/session reset logic directly.
- Moved all command target interfaces into Runtime:
  - profession weight assignment
  - item spawning
  - creature spawning
  - order enqueueing
  - zone mutation
  - workshop queue mutation
  - stockpile creation
- Moved `SimulationRuntimeContext` into Runtime. It now owns the command target aggregation and delegates profession weight writes through an injected callback instead of depending on App `ProfessionAssignments`.
- Moved concrete command target helpers into Runtime:
  - `ItemSpawnCommandTarget`
  - `CreatureSpawnCommandTarget`
  - `OrderCommandTarget`
  - `ZoneCommandTarget`
  - `WorkshopQueueCommandTarget`
  - `StockpileCommandTarget`
- Removed `HumanFortress.App.Commands` dependency on `HumanFortress.App.Runtime`; player/debug commands now depend on Runtime command target seams.
- Updated Runtime job wrappers, session creation, runtime host wiring, fortress initialization, and smoke tests to consume the Runtime-owned pipeline/navigation/session factory.
- Updated App to reference Runtime while keeping UI, SadConsole hosting, concrete job adapters, content loading implementation, and concrete host-wrapper composition callbacks in App for now.

### Verification

- App build: passed after adding the missing Runtime -> Contracts and Runtime -> SadRogue references.
- `./RunTests.sh`: passed with `0 Warning(s), 0 Error(s)`
- `--init-only`: passed; startup loaded 79 materials, 17 geology entries, 19 zone definitions, 322 item definitions, and 5 creature definitions
- `HumanFortress.sln` build: passed with `0 Warning(s), 0 Error(s)`
- `git diff --check`: passed

### Important Notes

- Runtime now references Contracts, Core, Navigation, and Simulation. It still does not reference App, UI, SadConsole, or Jobs.
- Runtime still has a direct `TheSadRogue.Primitives` package reference for internal command/snapshot/world geometry implementation details; public Runtime session ports now use Contracts runtime geometry primitives instead of exposing SadRogue types.
- The App `SimulationRuntimeHost` is now a thin wrapper over `SimulationRuntimeHostCore`, and `SimulationRuntimeSessionFactory<THost>` now lives in Runtime. Content loading implementation, concrete job adapter composition, initial worker spawning, auto-dig seeding, input/UI wiring, and SadConsole state ownership still live in App.
- Navigation composition is split: Runtime owns the Simulation-backed navigation source/factory, while App still decides when a session creates/rebuilds the shared navigation manager.
- App `ProfessionAssignments` remains App-owned. Runtime receives only a profession weight callback, which keeps Runtime free of Jobs/App references.
- `WorkshopQueueCommandTarget` still uses the current Core content registries directly as a transitional step until the Content split gives Runtime a cleaner recipe/construction catalog seam.

## Previous Batch: Diff Target Encoding Cleanup

Status: completed

### Completed

- Added `WorldCellTarget` and `WorldCellTargetEncoding` in `HumanFortress.Simulation.World` as the shared Simulation-level bridge between world coordinates, `ChunkKey + localIndex`, and `DiffTargetEncoding`.
- Replaced duplicated App-side chunk/local target encoding in:
  - `MiningDiffEmitter`
  - `TransportDiffEmitter`
  - `ConstructionDiffEmitter`
  - `CraftDiffEmitter`
  - `SanitizeSystem`
  - `ItemSpawnCommandTarget`
- Added `WorldCellTarget` overloads to `ItemsDiffLog` for add, remove, and split-stack operations.
- Updated App item diff emitters to pass `WorldCellTarget` directly instead of unpacking `ChunkKey + localIndex`.
- Added smoke coverage proving `WorldCellTargetEncoding` produces the same chunk/local/entity target as `DiffTargetEncoding`.

### Verification

- App build: passed with `0 Warning(s), 0 Error(s)`
- `./RunTests.sh`: passed with `0 Warning(s), 0 Error(s)`
- `--init-only`: passed; startup loaded 79 materials, 17 geology entries, 19 zone definitions, 322 item definitions, and 5 creature definitions
- `HumanFortress.sln` build: passed with `0 Warning(s), 0 Error(s)`
- `git diff --check`: passed

### Important Notes

- This is a target-encoding cleanup, not a full item diff representation migration.
- `ItemsDiffLog` now accepts `WorldCellTarget` as the preferred bridge, but the older `ChunkKey + localIndex` overloads remain for compatibility.
- General `DiffLog` operations still use `DiffTarget`.
- The new helper removes repeated arithmetic and gives the later full diff-target unification a single Simulation-owned migration point.

## Older Batch: Runtime Target Helper Split

Status: completed

### Completed

- Initially split the remaining concrete command-target behavior out of `SimulationRuntimeContext` into focused helpers:
  - profession weight command-target behavior
  - `ItemSpawnCommandTarget`
  - `CreatureSpawnCommandTarget`
  - `OrderCommandTarget`
  - `ZoneCommandTarget`
- Kept `SimulationRuntimeContext` as the transitional compatibility point implementing existing command target interfaces, but reduced it to session state plus delegation.
- Preserved the existing command boundary and command interfaces so UI/input and regression tests do not need broad call-site churn.
- Removed direct concrete `World` access from all files in `src/HumanFortress.App/Commands`.
- Added runtime command target seams:
  - `IOrderCommandTarget`
  - `IZoneCommandTarget`
  - `IWorkshopQueueCommandTarget`
  - `IStockpileCommandTarget`
  - `IItemSpawnCommandTarget`
  - `ICreatureSpawnCommandTarget`
  - `IProfessionAssignmentCommandTarget`
- Migrated command families to runtime targets:
  - mining, advanced mining, hauling, structural construction, and buildable construction orders
  - zone create/update/delete
  - workshop queue add/move/remove/clear and automation/worker settings
  - stockpile creation
  - debug item spawning
  - debug creature spawning
  - profession weight changes
- Added `WorkshopQueueCommandTarget` and `StockpileCommandTarget` helper classes so `SimulationRuntimeContext` delegates richer command behavior instead of becoming a large implementation dump.
- Added regression coverage for order, zone, workshop queue, stockpile, item spawn, creature spawn, profession weight, and command-stage execution paths.

### Verification

- App fast build: passed
- `./RunTests.sh`: passed with `0 Warning(s), 0 Error(s)`
- `--init-only`: passed; startup loaded 79 materials, 17 geology entries, 19 zone definitions, 322 item definitions, and 5 creature definitions
- `HumanFortress.sln` build: passed with `0 Warning(s), 0 Error(s)`
- `git diff --check`: passed

### Important Notes

- Profession weight commands now use a runtime seam and `ProfessionAssignmentDiffLog`; assignment weight mutation is applied post-tick through the bound profession handler.
- Order commands now use a runtime seam and `OrderDiffLog`; mining, haul, construction, and buildable-construction designations are applied by the post-tick `OrderDiffApplicator`.
- Zone create/update/delete commands now use a runtime seam and `ZoneDiffLog`; zone mutations are applied by the post-tick `ZoneDiffApplicator`.
- Workshop queue/settings commands now use a runtime seam and `WorkshopDiffLog`; workshop queue and automation mutations are applied by the post-tick `WorkshopDiffApplicator`.
- Stockpile creation and deletion now use a runtime seam and `StockpileDiffLog`; stockpile create/delete commands are applied by the post-tick `StockpileDiffApplicator`.
- `StockpileDiffApplicator` is now connected to the tick pipeline for stockpile create/delete diffs. Filter/item/job paths remained future stockpile work at that point and were later hardened through Simulation-owned stockpile item projections and Jobs/Transport stockpile diff integration.
- `SimulationRuntimeContext` no longer implements the command target roles directly; `SimulationCommandExecutionContext` composes the narrow target roles for command-stage execution.
- Spawn/item/job emitters still mix `ChunkKey + localIndex` and `DiffTargetEncoding.DiffTarget` target shapes. The follow-up cleanup introduced `WorldCellTargetEncoding` as a first shared migration point.

### Next Candidate Batch

1. Move the next pure runtime slice into `HumanFortress.Runtime` only after its App/UI dependencies are isolated.
2. Continue diff target unification by deciding whether `ItemsDiffLog` should eventually migrate from `WorldCellTarget` to `DiffTarget` directly.
3. Continue reducing temporary friend-access scaffolding now that `SimulationRuntimeContext` no longer owns command target roles.
4. Separately design the authoritative stockpile diff stage before migrating stockpile job/item placement behavior.
