using System.IO;
using HumanFortress.Core.Commands;

namespace HumanFortress.Runtime.Commands;

internal sealed partial class RuntimeCommandReplayFactory
{
    private static ICommand DecodeCreateZone(ulong tick, BinaryReader reader)
    {
        var definitionId = reader.ReadString();
        var name = reader.ReadString();
        var rect = ReadRectangle(reader);
        var z = reader.ReadInt32();
        return new CreateZoneCommand(tick, definitionId, name, rect, z);
    }

    private static ICommand DecodeUpdateZoneCells(ulong tick, BinaryReader reader)
    {
        var zoneId = reader.ReadInt32();
        var rect = ReadRectangle(reader);
        var z = reader.ReadInt32();
        var isAdding = reader.ReadBoolean();
        return new UpdateZoneCellsCommand(tick, zoneId, rect, z, isAdding);
    }

    private static ICommand DecodeDeleteZone(ulong tick, BinaryReader reader)
    {
        var zoneId = reader.ReadInt32();
        return new DeleteZoneCommand(tick, zoneId);
    }

    private static ICommand DecodeCreateStockpile(ulong tick, BinaryReader reader)
    {
        var rect = ReadRectangle(reader);
        var z = reader.ReadInt32();
        var presetId = reader.ReadString();
        return new CreateStockpileCommand(tick, rect, z, presetId);
    }

    private static ICommand DecodeDeleteStockpile(ulong tick, BinaryReader reader)
    {
        var zoneId = reader.ReadInt32();
        return new DeleteStockpileCommand(tick, zoneId);
    }
}
