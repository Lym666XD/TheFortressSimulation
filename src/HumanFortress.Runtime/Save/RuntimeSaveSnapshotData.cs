using System.Collections.ObjectModel;
using HumanFortress.Contracts.Runtime.Save;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Determinism;
using HumanFortress.Core.Random;

namespace HumanFortress.Runtime.Save;

internal sealed class RuntimeSaveSnapshotData
{
    private readonly ReadOnlyCollection<CommandReplayRecord> _commandReplayRecords;
    private readonly ReadOnlyCollection<CommandReplayRecord> _pendingCommandReplayRecords;
    private readonly ReadOnlyCollection<RngStreamStateSnapshot> _rngStreams;

    internal RuntimeSaveSnapshotData(
        RuntimeSaveManifestData manifest,
        WorldSavePayloadData? worldPayload,
        RuntimeSaveMiningJobsData? miningJobs,
        RuntimeSaveTransportJobsData? transportJobs,
        RuntimeSaveCraftJobsData? craftJobs,
        IEnumerable<RngStreamStateSnapshot> rngStreams,
        IEnumerable<CommandReplayRecord> commandReplayRecords,
        IEnumerable<CommandReplayRecord> pendingCommandReplayRecords)
    {
        ArgumentNullException.ThrowIfNull(rngStreams);
        ArgumentNullException.ThrowIfNull(commandReplayRecords);
        ArgumentNullException.ThrowIfNull(pendingCommandReplayRecords);

        var rngRows = rngStreams
            .OrderBy(static stream => stream.StreamName, StringComparer.Ordinal)
            .ToArray();
        var records = commandReplayRecords.ToArray();
        var pendingRecords = pendingCommandReplayRecords.ToArray();
        var seenStreams = new HashSet<string>(StringComparer.Ordinal);
        foreach (var stream in rngRows)
        {
            if (string.IsNullOrWhiteSpace(stream.StreamName))
                throw new ArgumentException("Runtime save snapshot RNG stream name must not be blank.", nameof(rngStreams));
            if (!seenStreams.Add(stream.StreamName))
                throw new ArgumentException($"Runtime save snapshot contains duplicate RNG stream '{stream.StreamName}'.", nameof(rngStreams));
        }

        if (manifest.Checkpoint.RngStreamCount != rngRows.Length)
            throw new InvalidOperationException("Runtime save snapshot RNG stream count does not match the manifest checkpoint.");

        if (!string.Equals(RngReplayHashBuilder.Build(rngRows), manifest.Checkpoint.RngHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime save snapshot RNG stream hash does not match the manifest checkpoint.");

        foreach (var record in records)
        {
            ArgumentNullException.ThrowIfNull(record);
        }

        foreach (var record in pendingRecords)
        {
            ArgumentNullException.ThrowIfNull(record);
        }

        if (manifest.Checkpoint.CommandLogRecordCount != records.Length)
            throw new InvalidOperationException("Runtime save snapshot executed command count does not match the manifest checkpoint.");

        if (!string.Equals(CommandReplayJournalHashBuilder.Build(records), manifest.Checkpoint.CommandLogHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime save snapshot executed command hash does not match the manifest checkpoint.");

        if (manifest.Checkpoint.PendingCommandLogRecordCount != pendingRecords.Length)
            throw new InvalidOperationException("Runtime save snapshot pending command count does not match the manifest checkpoint.");

        if (!string.Equals(CommandReplayJournalHashBuilder.Build(pendingRecords), manifest.Checkpoint.PendingCommandLogHash, StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime save snapshot pending command hash does not match the manifest checkpoint.");

        Manifest = manifest;
        WorldPayload = worldPayload;
        MiningJobs = miningJobs;
        TransportJobs = transportJobs;
        CraftJobs = craftJobs;
        _rngStreams = Array.AsReadOnly(rngRows);
        _commandReplayRecords = Array.AsReadOnly(records);
        _pendingCommandReplayRecords = Array.AsReadOnly(pendingRecords);

        ValidateMiningJobs();
        ValidateTransportJobs();
        ValidateCraftJobs();
    }

    internal RuntimeSaveManifestData Manifest { get; }
    internal WorldSavePayloadData? WorldPayload { get; }
    internal RuntimeSaveMiningJobsData? MiningJobs { get; }
    internal RuntimeSaveTransportJobsData? TransportJobs { get; }
    internal RuntimeSaveCraftJobsData? CraftJobs { get; }
    internal IReadOnlyList<RngStreamStateSnapshot> RngStreams => _rngStreams;
    internal int RngStreamCount => _rngStreams.Count;
    internal string RngReplayHash => Manifest.Checkpoint.RngHash;
    internal IReadOnlyList<CommandReplayRecord> CommandReplayRecords => _commandReplayRecords;
    internal IReadOnlyList<CommandReplayRecord> PendingCommandReplayRecords => _pendingCommandReplayRecords;
    internal int CommandReplayRecordCount => _commandReplayRecords.Count;
    internal int PendingCommandReplayRecordCount => _pendingCommandReplayRecords.Count;
    internal string CommandReplayJournalHash => Manifest.Checkpoint.CommandLogHash;
    internal string PendingCommandReplayJournalHash => Manifest.Checkpoint.PendingCommandLogHash;

    internal RuntimeSaveSnapshotDocumentData ToDocumentData()
    {
        return RuntimeSaveSnapshotDocumentBuilder.Build(this);
    }

    private void ValidateMiningJobs()
    {
        var section = Manifest.Sections
            .FirstOrDefault(static section => string.Equals(
                section.Name,
                RuntimeSaveManifestSections.JobsMining,
                StringComparison.Ordinal));
        if (!section.Present)
        {
            if (MiningJobs.HasValue && RuntimeSaveSnapshotDocumentMiningMapper.CountRecords(MiningJobs.Value) > 0)
                throw new InvalidOperationException("Runtime save snapshot contains mining job payload for an absent manifest section.");

            return;
        }

        if (!MiningJobs.HasValue)
        {
            if (section.RecordCount.GetValueOrDefault() > 0)
                throw new InvalidOperationException("Runtime save snapshot mining job payload is missing.");

            return;
        }

        var actualCount = RuntimeSaveSnapshotDocumentMiningMapper.CountRecords(MiningJobs.Value);
        if (section.RecordCount != actualCount)
            throw new InvalidOperationException("Runtime save snapshot mining job payload count does not match the manifest checkpoint.");

        var actualHash = RuntimeSaveSnapshotDocumentMiningMapper.BuildReplayHash(MiningJobs.Value);
        if (!string.Equals(actualHash, Manifest.Checkpoint.MiningHash, StringComparison.Ordinal)
            || !string.Equals(actualHash, section.Hash, StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime save snapshot mining job payload hash does not match the manifest checkpoint.");
    }

    private void ValidateTransportJobs()
    {
        var section = Manifest.Sections
            .FirstOrDefault(static section => string.Equals(
                section.Name,
                RuntimeSaveManifestSections.JobsTransport,
                StringComparison.Ordinal));
        if (!section.Present)
        {
            if (TransportJobs.HasValue && RuntimeSaveSnapshotDocumentTransportMapper.CountRecords(TransportJobs.Value) > 0)
                throw new InvalidOperationException("Runtime save snapshot contains transport job payload for an absent manifest section.");

            return;
        }

        if (!TransportJobs.HasValue)
        {
            if (section.RecordCount.GetValueOrDefault() > 0)
                throw new InvalidOperationException("Runtime save snapshot transport job payload is missing.");

            return;
        }

        var actualCount = RuntimeSaveSnapshotDocumentTransportMapper.CountRecords(TransportJobs.Value);
        if (section.RecordCount != actualCount)
            throw new InvalidOperationException("Runtime save snapshot transport job payload count does not match the manifest checkpoint.");

        var actualHash = RuntimeSaveSnapshotDocumentTransportMapper.BuildReplayHash(TransportJobs.Value);
        if (!string.Equals(actualHash, Manifest.Checkpoint.TransportHash, StringComparison.Ordinal)
            || !string.Equals(actualHash, section.Hash, StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime save snapshot transport job payload hash does not match the manifest checkpoint.");
    }

    private void ValidateCraftJobs()
    {
        var section = Manifest.Sections
            .FirstOrDefault(static section => string.Equals(
                section.Name,
                RuntimeSaveManifestSections.JobsCraft,
                StringComparison.Ordinal));
        if (!section.Present)
        {
            if (CraftJobs.HasValue && RuntimeSaveSnapshotDocumentCraftMapper.CountRecords(CraftJobs.Value) > 0)
                throw new InvalidOperationException("Runtime save snapshot contains craft job payload for an absent manifest section.");

            return;
        }

        if (!CraftJobs.HasValue)
        {
            if (section.RecordCount.GetValueOrDefault() > 0)
                throw new InvalidOperationException("Runtime save snapshot craft job payload is missing.");

            return;
        }

        var actualCount = RuntimeSaveSnapshotDocumentCraftMapper.CountRecords(CraftJobs.Value);
        if (section.RecordCount != actualCount)
            throw new InvalidOperationException("Runtime save snapshot craft job payload count does not match the manifest checkpoint.");

        var actualHash = RuntimeSaveSnapshotDocumentCraftMapper.BuildReplayHash(CraftJobs.Value);
        if (!string.Equals(actualHash, Manifest.Checkpoint.CraftHash, StringComparison.Ordinal)
            || !string.Equals(actualHash, section.Hash, StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime save snapshot craft job payload hash does not match the manifest checkpoint.");
    }
}
