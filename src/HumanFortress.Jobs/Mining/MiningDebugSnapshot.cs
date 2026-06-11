using SadRogue.Primitives;

namespace HumanFortress.Jobs.Mining;

public readonly record struct MiningActiveJobView(
    Guid WorkerId,
    Point Target,
    int Z,
    Point Adjacent,
    string Stage,
    int ProgressTicks,
    int RequiredTicks);

public readonly record struct MiningActiveJobDebugView(
    Guid WorkerId,
    Point Target,
    int Z,
    Point Adjacent,
    string Stage,
    int ProgressTicks,
    int RequiredTicks,
    uint Seed);

public readonly record struct MiningJobStatsSnapshot(int Intake, int Active, int Backlog, int Deferred, int ReservedTiles, int CarryoverOld);

public readonly record struct MiningDebugSnapshot(
    MiningJobStatsSnapshot Stats,
    List<MiningActiveJobDebugView> Active,
    int BacklogCount,
    int DeferredCount,
    int ReservedTiles,
    bool SeedsIncluded);
