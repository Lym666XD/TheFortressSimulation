using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Transport;
using HumanFortress.Runtime.Replay;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Runtime.Save;

internal static class RuntimeSaveSnapshotEmptyJobState
{
    internal static MiningJobReplaySnapshot CreateMiningReplaySnapshot()
    {
        return RuntimeEmptyJobReplayState.CreateMining();
    }

    internal static TransportRequestQueueStateSnapshot CreateTransportQueueSnapshot()
    {
        return RuntimeEmptyJobReplayState.CreateTransportQueue();
    }

    internal static TransportJobReplaySnapshot CreateTransportReplaySnapshot()
    {
        return RuntimeEmptyJobReplayState.CreateTransport();
    }

    internal static CraftJobReplaySnapshot CreateCraftReplaySnapshot()
    {
        return RuntimeEmptyJobReplayState.CreateCraft();
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
