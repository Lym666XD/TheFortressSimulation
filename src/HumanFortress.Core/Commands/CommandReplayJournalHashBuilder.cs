using HumanFortress.Core.Determinism;

namespace HumanFortress.Core.Commands;

/// <summary>
/// Stable hash builder for persisted command replay journals.
/// </summary>
public static class CommandReplayJournalHashBuilder
{
    public static string Build(IEnumerable<CommandReplayRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);

        var orderedRecords = records.ToArray();
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("commands.replay_journal.v1");
            hash.AddInt32(orderedRecords.Length);
            for (var i = 0; i < orderedRecords.Length; i++)
            {
                var record = orderedRecords[i];
                ArgumentNullException.ThrowIfNull(record);

                hash.AddInt32(i);
                hash.AddUInt64(record.Tick);
                hash.AddGuid(record.CommandId);
                hash.AddString(record.CommandType);
                hash.AddBoolean(record.CommandIdentitySequence.HasValue);
                if (record.CommandIdentitySequence.HasValue)
                    hash.AddInt64(record.CommandIdentitySequence.Value);
                hash.AddBytes(record.Payload.Span);
            }
        });
    }
}
