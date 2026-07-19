using System.Text.Json;
using HumanFortress.Core.Commands;
using HumanFortress.Core.Determinism;
using HumanFortress.Runtime.Commands;
using SadRogue.Primitives;

namespace HumanFortress.Scenarios;

internal static class ScenarioJournalTemplate
{
    internal static void Write(
        string path,
        string id,
        ulong tick,
        Point position,
        int z)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (position.X < 0 || position.Y < 0 || z < 0)
            throw new ArgumentOutOfRangeException(nameof(position), "Journal spawn coordinates must be non-negative.");

        var command = RuntimeDebugCommandFactory.CreateSpawnItem(
            "core_item_ingot_iron_wrought",
            position,
            z,
            quantity: 1)(tick);
        var identified = new RuntimeIdentifiedCommand(command, sequence: 1);
        var record = CommandReplayRecord.FromCommand(identified);
        var records = new[] { record };
        var document = new ScenarioCommandJournalDocument(
            SchemaVersion: 1,
            Id: id,
            HashAlgorithm: ReplayHashBuilder.Algorithm,
            JournalHash: CommandReplayJournalHashBuilder.Build(records),
            Records: Array.AsReadOnly(records.Select(ToDocument).ToArray()));

        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(
            path,
            JsonSerializer.SerializeToUtf8Bytes(document, ScenarioJson.Strict));
    }

    private static ScenarioCommandRecordDocument ToDocument(CommandReplayRecord record)
    {
        return new ScenarioCommandRecordDocument(
            record.Tick,
            record.CommandId.ToString("D"),
            record.CommandType,
            record.CommandIdentitySequence,
            Convert.ToBase64String(record.Payload.Span));
    }
}
