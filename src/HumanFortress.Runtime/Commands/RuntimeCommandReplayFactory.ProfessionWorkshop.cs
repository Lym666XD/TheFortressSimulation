using System.IO;
using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime.Commands;

internal sealed partial class RuntimeCommandReplayFactory
{
    private static ICommand DecodeSetProfessionWeight(ulong tick, BinaryReader reader)
    {
        var workerId = ReadGuid(reader, "worker id");
        var professionId = reader.ReadString();
        var weight = reader.ReadInt32();
        return new SetProfessionWeightCommand(tick, workerId, professionId, weight);
    }

    private static ICommand DecodeUpdateWorkshopQueue(ulong tick, BinaryReader reader)
    {
        var workshopGuid = ReadGuid(reader, "workshop id");
        var operation = ReadByteEnum<WorkshopQueueOperation>(reader, "workshop queue operation");
        var recipeId = reader.ReadString();
        var entryId = ReadOptionalGuid(reader, "workshop queue entry id");
        var intValue = ReadOptionalInt32(reader);
        var moveOffset = ReadOptionalInt32(reader);
        var boolValue = ReadOptionalBoolean(reader);

        return new UpdateWorkshopQueueCommand(
            tick,
            workshopGuid,
            operation,
            recipeId: string.IsNullOrEmpty(recipeId) ? null : recipeId,
            entryId: entryId,
            intValue: intValue,
            moveOffset: moveOffset,
            boolValue: boolValue);
    }
}
