using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport;

internal readonly record struct TransportActiveJobView(Guid CreatureId, Guid ItemId, Point3 FromOrCurrent, Point3 Dest, string Stage);

internal readonly record struct TransportActiveJobDebugView(Guid CreatureId, Guid ItemId, Point3 FromOrCurrent, Point3 Dest, string Stage, uint Seed);

internal readonly record struct TransportShardCountDebugView(int ShardId, int Count);

internal readonly record struct TransportDebugSnapshot(
    TransportJobStatsSnapshot Stats,
    List<TransportActiveJobDebugView> Active,
    List<TransportRequest> PendingPeek,
    IReadOnlyList<TransportShardCountDebugView> ShardCounts,
    int BacklogCount,
    int IntakeBudget,
    int AllowedActive,
    int ReservedSlots,
    bool SeedsIncluded);
