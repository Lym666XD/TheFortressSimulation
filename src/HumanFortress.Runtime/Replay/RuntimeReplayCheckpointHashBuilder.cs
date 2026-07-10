using HumanFortress.Core.Determinism;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Random;
using HumanFortress.Contracts.Runtime.Replay;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Jobs.Craft;
using HumanFortress.Jobs.Mining;
using HumanFortress.Jobs.Replay;
using HumanFortress.Jobs.Transport;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;
using HumanFortress.Runtime.Save;
using HumanFortress.Runtime.Session;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Replay;

namespace HumanFortress.Runtime.Replay;

internal static class RuntimeReplayCheckpointHashBuilder
{
    internal static string Build(
        RuntimeSessionServices services,
        FortressRuntimeSession? session)
    {
        return BuildData(services, session).AggregateHash;
    }

    internal static RuntimeReplayCheckpointData BuildData(
        RuntimeSessionServices services,
        FortressRuntimeSession? session,
        CommandQueueReplaySnapshot? commandQueueSnapshot = null,
        IReadOnlyList<RngStreamStateSnapshot>? rngStreamSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var metadata = SimulationSnapshotMetadata.Current(services.TickScheduler.CurrentTick);
        var worldHash = session?.World == null
            ? null
            : WorldReplayHashBuilder.Build(session.World);
        var rngSnapshot = rngStreamSnapshot ?? services.RngStreams.GetStateSnapshot();
        var rngHash = RngReplayHashBuilder.Build(rngSnapshot);
        commandQueueSnapshot ??= services.CommandQueue.GetReplaySnapshot();
        var commandLogHash = CommandReplayJournalHashBuilder.Build(commandQueueSnapshot.ExecutedRecords);
        var pendingCommandLogHash = CommandReplayJournalHashBuilder.Build(commandQueueSnapshot.PendingRecords);
        var systems = session?.Host.Systems;
        string? transportHash = null;
        var transportRecordCount = 0;
        string? miningHash = null;
        var miningRecordCount = 0;
        string? craftHash = null;
        var craftRecordCount = 0;
        if (session != null && systems == null)
        {
            var transportQueueSnapshot = RuntimeSaveSnapshotEmptyJobState.CreateTransportQueueSnapshot();
            var transportJobSnapshot = RuntimeSaveSnapshotEmptyJobState.CreateTransportReplaySnapshot();
            transportHash = TransportReplayHashBuilder.Build(
                transportQueueSnapshot,
                transportJobSnapshot);
            transportRecordCount = CountTransportRecords(
                transportQueueSnapshot,
                transportJobSnapshot);

            var miningSnapshot = RuntimeSaveSnapshotEmptyJobState.CreateMiningReplaySnapshot();
            miningHash = MiningReplayHashBuilder.Build(miningSnapshot);
            miningRecordCount = CountMiningRecords(miningSnapshot);

            var craftSnapshot = RuntimeSaveSnapshotEmptyJobState.CreateCraftReplaySnapshot();
            craftHash = CraftReplayHashBuilder.Build(craftSnapshot);
            craftRecordCount = CountCraftRecords(craftSnapshot);
        }
        else if (systems != null)
        {
            var transportQueueSnapshot = systems.TransportQueue.GetStateSnapshot();
            var transportJobSnapshot = systems.TransportJobs.GetReplaySnapshot();
            transportHash = TransportReplayHashBuilder.Build(
                transportQueueSnapshot,
                transportJobSnapshot);
            transportRecordCount = CountTransportRecords(
                transportQueueSnapshot,
                transportJobSnapshot);

            var miningSnapshot = systems.MiningJobs.GetReplaySnapshot();
            miningHash = MiningReplayHashBuilder.Build(miningSnapshot);
            miningRecordCount = CountMiningRecords(miningSnapshot);

            var craftSnapshot = systems.CraftJobs.GetReplaySnapshot();
            craftHash = CraftReplayHashBuilder.Build(craftSnapshot);
            craftRecordCount = CountCraftRecords(craftSnapshot);
        }

        var aggregateHash = ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("runtime.replay.checkpoint.v1");
            hash.AddInt32(metadata.SchemaVersion);
            hash.AddUInt64(metadata.RuntimeTick);
            AddSectionHash(hash, "world", worldHash);
            AddSectionHash(hash, "rng", rngHash);
            AddSectionHash(hash, "commands.executed", commandLogHash);
            hash.AddInt32(commandQueueSnapshot.ExecutedRecords.Count);
            AddSectionHash(hash, "commands.pending", pendingCommandLogHash);
            hash.AddInt32(commandQueueSnapshot.PendingRecords.Count);
            AddSectionHash(hash, "jobs.transport", transportHash);
            AddSectionHash(hash, "jobs.mining", miningHash);
            AddSectionHash(hash, "jobs.craft", craftHash);
        });

        return new RuntimeReplayCheckpointData(
            metadata,
            aggregateHash,
            worldHash,
            rngHash,
            rngSnapshot.Count,
            commandLogHash,
            commandQueueSnapshot.ExecutedRecords.Count,
            pendingCommandLogHash,
            commandQueueSnapshot.PendingRecords.Count,
            transportHash,
            transportRecordCount,
            miningHash,
            miningRecordCount,
            craftHash,
            craftRecordCount);
    }

    private static int CountTransportRecords(
        TransportRequestQueueStateSnapshot queue,
        TransportJobReplaySnapshot executor)
    {
        var schedulingHintCount = executor.IntakeCapHint.HasValue
            || executor.MaxActiveCapHint.HasValue
            || executor.ReserveSlotsHint != 0
            ? 1
            : 0;

        return queue.PendingRequests.Count
            + executor.ActiveJobs.Count
            + executor.BacklogEntries.Count
            + schedulingHintCount;
    }

    private static int CountMiningRecords(MiningJobReplaySnapshot snapshot)
    {
        return snapshot.ActiveJobs.Count
            + snapshot.BacklogEntries.Count
            + snapshot.DeferredStairwells.Count
            + snapshot.ReservedTiles.Count
            + snapshot.RecentCompletions.Count;
    }

    private static int CountCraftRecords(CraftJobReplaySnapshot snapshot)
    {
        return snapshot.ActiveJobs.Count
            + snapshot.BacklogEntries.Count;
    }

    private static void AddSectionHash(ReplayHashBuilder hash, string sectionName, string? sectionHash)
    {
        hash.AddString(sectionName);
        hash.AddBoolean(sectionHash != null);
        if (sectionHash != null)
            hash.AddString(sectionHash);
    }
}
