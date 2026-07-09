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

    public RuntimeSaveSnapshotData(
        RuntimeSaveManifestData manifest,
        WorldSavePayloadData? worldPayload,
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
        _rngStreams = Array.AsReadOnly(rngRows);
        _commandReplayRecords = Array.AsReadOnly(records);
        _pendingCommandReplayRecords = Array.AsReadOnly(pendingRecords);
    }

    public RuntimeSaveManifestData Manifest { get; }
    public WorldSavePayloadData? WorldPayload { get; }
    public IReadOnlyList<RngStreamStateSnapshot> RngStreams => _rngStreams;
    public int RngStreamCount => _rngStreams.Count;
    public string RngReplayHash => Manifest.Checkpoint.RngHash;
    public IReadOnlyList<CommandReplayRecord> CommandReplayRecords => _commandReplayRecords;
    public IReadOnlyList<CommandReplayRecord> PendingCommandReplayRecords => _pendingCommandReplayRecords;
    public int CommandReplayRecordCount => _commandReplayRecords.Count;
    public int PendingCommandReplayRecordCount => _pendingCommandReplayRecords.Count;
    public string CommandReplayJournalHash => Manifest.Checkpoint.CommandLogHash;
    public string PendingCommandReplayJournalHash => Manifest.Checkpoint.PendingCommandLogHash;

    public RuntimeSaveSnapshotDocumentData ToDocumentData()
    {
        return RuntimeSaveSnapshotDocumentBuilder.Build(this);
    }
}
