using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Transport;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Runtime.Replay;

internal static class RuntimeEmptyJobReplayState
{
    internal static MiningJobReplaySnapshot CreateMining()
    {
        return new MiningJobReplaySnapshot(
            Array.Empty<MiningActiveJobStateSnapshot>(),
            Array.Empty<MiningBacklogEntrySnapshot>(),
            Array.Empty<MiningDeferredStairwellSnapshot>(),
            Array.Empty<MiningReservedTileSnapshot>(),
            Array.Empty<MiningRecentCompletionSnapshot>());
    }

    internal static TransportRequestQueueStateSnapshot CreateTransportQueue()
    {
        return new TransportRequestQueueStateSnapshot(Array.Empty<TransportRequest>());
    }

    internal static TransportJobReplaySnapshot CreateTransport()
    {
        return new TransportJobReplaySnapshot(
            null,
            null,
            0,
            Array.Empty<TransportActiveJobStateSnapshot>(),
            Array.Empty<TransportBacklogEntrySnapshot>());
    }

    internal static CraftJobReplaySnapshot CreateCraft()
    {
        return new CraftJobReplaySnapshot(
            Array.Empty<CraftActiveJobStateSnapshot>(),
            Array.Empty<CraftBacklogEntrySnapshot>());
    }
}
