using SadRogue.Primitives;

namespace HumanFortress.Jobs.Mining;

internal readonly record struct MiningActiveJobView(
    Guid WorkerId,
    Point Target,
    int Z,
    Point Adjacent,
    string Stage,
    int ProgressTicks,
    int RequiredTicks);

internal readonly record struct MiningActiveJobDebugView(
    Guid WorkerId,
    Point Target,
    int Z,
    Point Adjacent,
    string Stage,
    int ProgressTicks,
    int RequiredTicks,
    uint Seed);

internal readonly record struct MiningJobStatsSnapshot(int Intake, int Active, int Backlog, int Deferred, int ReservedTiles, int CarryoverOld);

internal readonly record struct MiningDebugSnapshot(
    MiningJobStatsSnapshot Stats,
    List<MiningActiveJobDebugView> Active,
    int BacklogCount,
    int DeferredCount,
    int ReservedTiles,
    bool SeedsIncluded);
