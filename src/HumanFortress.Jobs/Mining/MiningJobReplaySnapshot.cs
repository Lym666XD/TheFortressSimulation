using HumanFortress.Simulation.Orders;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Mining;

internal readonly record struct MiningActiveJobStateSnapshot(
    int Order,
    Guid WorkerId,
    Point Target,
    int Z,
    Point Adjacent,
    MiningStage Stage,
    int ProgressTicks,
    int RequiredTicks,
    ushort GeologyHandle,
    TerrainKind TerrainKind,
    int Priority,
    ulong AssignedTick,
    int ReplanFailCount,
    MiningAction Action,
    MiningSegment Segment,
    int DesignationId,
    byte PathSearchAttempt = 0);

internal readonly record struct MiningRecentCompletionSnapshot(
    int Order,
    Point Cell,
    int Z,
    ulong ExpireTick);

internal readonly record struct MiningJobReplaySnapshot(
    IReadOnlyList<MiningActiveJobStateSnapshot> ActiveJobs,
    IReadOnlyList<MiningBacklogEntrySnapshot> BacklogEntries,
    IReadOnlyList<MiningDeferredStairwellSnapshot> DeferredStairwells,
    IReadOnlyList<MiningReservedTileSnapshot> ReservedTiles,
    IReadOnlyList<MiningRecentCompletionSnapshot> RecentCompletions);
