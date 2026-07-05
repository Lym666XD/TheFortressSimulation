using HumanFortress.Core.Determinism;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Random;
using HumanFortress.Contracts.Runtime.Replay;
using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Jobs.Replay;
using HumanFortress.Runtime.Composition;
using HumanFortress.Runtime.Host;
using HumanFortress.Runtime.Session;
using HumanFortress.Simulation.Replay;

namespace HumanFortress.Runtime.Replay;

internal static class RuntimeReplayCheckpointHashBuilder
{
    internal static string Build(
        RuntimeSessionServices services,
        SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>>? session)
    {
        return BuildData(services, session).AggregateHash;
    }

    internal static RuntimeReplayCheckpointData BuildData(
        RuntimeSessionServices services,
        SimulationRuntimeSession<SimulationRuntimeHost<SimulationRuntimeSystems>>? session,
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
        var transportHash = systems == null
            ? null
            : TransportReplayHashBuilder.Build(
                systems.TransportQueue.GetStateSnapshot(),
                systems.TransportJobs.GetReplaySnapshot());
        var miningHash = systems == null
            ? null
            : MiningReplayHashBuilder.Build(systems.MiningJobs.GetReplaySnapshot());
        var craftHash = systems == null
            ? null
            : CraftReplayHashBuilder.Build(systems.CraftJobs.GetReplaySnapshot());

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
            miningHash,
            craftHash);
    }

    private static void AddSectionHash(ReplayHashBuilder hash, string sectionName, string? sectionHash)
    {
        hash.AddString(sectionName);
        hash.AddBoolean(sectionHash != null);
        if (sectionHash != null)
            hash.AddString(sectionHash);
    }
}
