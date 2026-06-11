using HumanFortress.Navigation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport;

public readonly record struct TransportActiveJobView(Guid CreatureId, Guid ItemId, Point3 FromOrCurrent, Point3 Dest, string Stage);

public readonly record struct TransportActiveJobDebugView(Guid CreatureId, Guid ItemId, Point3 FromOrCurrent, Point3 Dest, string Stage, uint Seed);

public readonly record struct TransportDebugSnapshot(
    TransportJobStatsSnapshot Stats,
    List<TransportActiveJobDebugView> Active,
    List<TransportRequest> PendingPeek,
    Dictionary<int, int> ShardCounts,
    int BacklogCount,
    int IntakeBudget,
    int AllowedActive,
    int ReservedSlots,
    bool SeedsIncluded);
