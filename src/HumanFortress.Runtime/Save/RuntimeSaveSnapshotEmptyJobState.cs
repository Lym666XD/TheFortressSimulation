using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Transport;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSnapshotEmptyJobState
{
    internal static MiningJobReplaySnapshot CreateMiningReplaySnapshot()
    {
        return new MiningJobReplaySnapshot(
            Array.Empty<MiningActiveJobStateSnapshot>(),
            Array.Empty<MiningBacklogEntrySnapshot>(),
            Array.Empty<MiningDeferredStairwellSnapshot>(),
            Array.Empty<MiningReservedTileSnapshot>(),
            Array.Empty<MiningRecentCompletionSnapshot>());
    }

    internal static TransportRequestQueueStateSnapshot CreateTransportQueueSnapshot()
    {
        return new TransportRequestQueueStateSnapshot(Array.Empty<TransportRequest>());
    }

    internal static TransportJobReplaySnapshot CreateTransportReplaySnapshot()
    {
        return new TransportJobReplaySnapshot(
            null,
            null,
            0,
            Array.Empty<TransportActiveJobStateSnapshot>(),
            Array.Empty<TransportBacklogEntrySnapshot>());
    }

    internal static CraftJobReplaySnapshot CreateCraftReplaySnapshot()
    {
        return new CraftJobReplaySnapshot(
            Array.Empty<CraftActiveJobStateSnapshot>(),
            Array.Empty<CraftBacklogEntrySnapshot>());
    }

    internal static RuntimeSaveMiningJobsData CreateMiningDocumentData()
    {
        return RuntimeSaveSnapshotDocumentMiningMapper.ToDocumentData(CreateMiningReplaySnapshot());
    }

    internal static RuntimeSaveTransportJobsData CreateTransportDocumentData()
    {
        return RuntimeSaveSnapshotDocumentTransportMapper.ToDocumentData(
            CreateTransportQueueSnapshot(),
            CreateTransportReplaySnapshot());
    }

    internal static RuntimeSaveCraftJobsData CreateCraftDocumentData()
    {
        return RuntimeSaveSnapshotDocumentCraftMapper.ToDocumentData(CreateCraftReplaySnapshot());
    }
}
