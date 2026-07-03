using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal sealed class RuntimeCommandReplayFactory : ICommandReplayFactory
{
    private const int MaxDecodedStringArrayLength = 1024;

    public bool TryCreateCommand(
        CommandReplayRecord record,
        out ICommand? command,
        out string? errorMessage)
    {
        ArgumentNullException.ThrowIfNull(record);

        command = null;
        errorMessage = null;

        try
        {
            if (!TryDecodeCommand(record, out var decodedCommand, out errorMessage))
                return false;

            if (record.CommandIdentitySequence.HasValue)
            {
                decodedCommand = new RuntimeIdentifiedCommand(
                    decodedCommand,
                    record.CommandIdentitySequence.Value);
            }

            if (decodedCommand.CommandId != record.CommandId)
            {
                errorMessage = $"Replay command id mismatch for '{record.CommandType}'.";
                return false;
            }

            command = decodedCommand;
            return true;
        }
        catch (Exception ex) when (
            ex is EndOfStreamException
            || ex is IOException
            || ex is InvalidDataException
            || ex is ArgumentException)
        {
            errorMessage = $"Replay command decode failed for '{record.CommandType}': {ex.Message}";
            return false;
        }
    }

    private static bool TryDecodeCommand(
        CommandReplayRecord record,
        out ICommand command,
        out string? errorMessage)
    {
        using var stream = new MemoryStream(record.ToPayloadArray(), writable: false);
        using var reader = new BinaryReader(stream);

        if (!IsSupportedCommandType(record.CommandType))
        {
            command = null!;
            errorMessage = $"Unknown replay command type '{record.CommandType}'.";
            return false;
        }

        RuntimeCommandPayload.ReadAndValidateVersion(reader, record.CommandType);

        command = record.CommandType switch
        {
            "orders.mining.rect" => DecodeMiningOrder(record.Tick, reader),
            "orders.mining.advanced_rect" => DecodeAdvancedMiningOrder(record.Tick, reader),
            "orders.haul.rect" => DecodeHaulOrder(record.Tick, reader),
            "orders.construction.rect" => DecodeConstructionOrder(record.Tick, reader),
            "orders.construction.buildable.anchor" => DecodeBuildableConstructionOrder(record.Tick, reader),
            "zones.create" => DecodeCreateZone(record.Tick, reader),
            "zones.update_cells" => DecodeUpdateZoneCells(record.Tick, reader),
            "zones.delete" => DecodeDeleteZone(record.Tick, reader),
            "stockpiles.create" => DecodeCreateStockpile(record.Tick, reader),
            "stockpiles.delete" => DecodeDeleteStockpile(record.Tick, reader),
            "professions.set_weight" => DecodeSetProfessionWeight(record.Tick, reader),
            "workshops.queue.update" => DecodeUpdateWorkshopQueue(record.Tick, reader),
            "debug.spawn.item" => DecodeSpawnItem(record.Tick, reader),
            "debug.spawn.creature" => DecodeSpawnCreature(record.Tick, reader),
            _ => throw new InvalidOperationException($"Unsupported replay command type '{record.CommandType}'.")
        };

        EnsureFullyRead(reader);
        errorMessage = null;
        return true;
    }

    private static bool IsSupportedCommandType(string commandType)
    {
        return commandType switch
        {
            "orders.mining.rect"
            or "orders.mining.advanced_rect"
            or "orders.haul.rect"
            or "orders.construction.rect"
            or "orders.construction.buildable.anchor"
            or "zones.create"
            or "zones.update_cells"
            or "zones.delete"
            or "stockpiles.create"
            or "stockpiles.delete"
            or "professions.set_weight"
            or "workshops.queue.update"
            or "debug.spawn.item"
            or "debug.spawn.creature" => true,
            _ => false
        };
    }

    private static ICommand DecodeMiningOrder(ulong tick, BinaryReader reader)
    {
        var rect = ReadRectangle(reader);
        var z = reader.ReadInt32();
        var priority = reader.ReadInt32();
        return new CreateMiningOrderCommand(tick, rect, z, priority);
    }

    private static ICommand DecodeAdvancedMiningOrder(ulong tick, BinaryReader reader)
    {
        var rect = ReadRectangle(reader);
        var zMin = reader.ReadInt32();
        var zMax = reader.ReadInt32();
        var action = ReadByteEnum<MiningAction>(reader, "mining action");
        var priority = reader.ReadInt32();
        return new CreateAdvancedMiningOrderCommand(tick, rect, zMin, zMax, action, priority);
    }

    private static ICommand DecodeHaulOrder(ulong tick, BinaryReader reader)
    {
        var rect = ReadRectangle(reader);
        var z = reader.ReadInt32();
        var priority = reader.ReadInt32();
        return new CreateHaulOrderCommand(tick, rect, z, priority);
    }

    private static ICommand DecodeConstructionOrder(ulong tick, BinaryReader reader)
    {
        var rect = ReadRectangle(reader);
        var zMin = reader.ReadInt32();
        var zMax = reader.ReadInt32();
        var shape = ReadByteEnum<ConstructionShape>(reader, "construction shape");
        var preferredMaterialId = reader.ReadString();
        var categoryKey = reader.ReadString();
        var tags = ReadStringArray(reader, "construction material tags");
        var priority = reader.ReadInt32();

        return new CreateConstructionOrderCommand(
            tick,
            rect,
            zMin,
            zMax,
            shape,
            new MaterialFilterSpec
            {
                PreferredMaterialId = string.IsNullOrEmpty(preferredMaterialId) ? null : preferredMaterialId,
                CategoryKey = categoryKey,
                Tags = tags
            },
            priority);
    }

    private static ICommand DecodeBuildableConstructionOrder(ulong tick, BinaryReader reader)
    {
        var constructionId = reader.ReadString();
        var anchor = new Point(reader.ReadInt32(), reader.ReadInt32());
        var z = reader.ReadInt32();
        var priority = reader.ReadInt32();
        return new CreateBuildableConstructionOrderCommand(tick, constructionId, anchor, z, priority);
    }

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

    private static ICommand DecodeSpawnItem(ulong tick, BinaryReader reader)
    {
        var itemId = reader.ReadString();
        var position = new Point(reader.ReadInt32(), reader.ReadInt32());
        var z = reader.ReadInt32();
        var quantity = reader.ReadInt32();
        return new SpawnItemCommand(tick, itemId, position, z, quantity);
    }

    private static ICommand DecodeSpawnCreature(ulong tick, BinaryReader reader)
    {
        var creatureId = reader.ReadString();
        var position = new Point(reader.ReadInt32(), reader.ReadInt32());
        var z = reader.ReadInt32();
        var factionId = reader.ReadString();
        return new SpawnCreatureCommand(tick, creatureId, position, z, factionId);
    }

    private static Rectangle ReadRectangle(BinaryReader reader)
    {
        return new Rectangle(
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32(),
            reader.ReadInt32());
    }

    private static TEnum ReadByteEnum<TEnum>(BinaryReader reader, string fieldName)
        where TEnum : struct, Enum
    {
        var rawValue = reader.ReadByte();
        var value = (TEnum)Enum.ToObject(typeof(TEnum), rawValue);
        if (!Enum.IsDefined(typeof(TEnum), value))
            throw new InvalidDataException($"Invalid {fieldName} value '{rawValue}'.");

        return value;
    }

    private static Guid ReadGuid(BinaryReader reader, string fieldName)
    {
        var bytes = reader.ReadBytes(16);
        if (bytes.Length != 16)
            throw new InvalidDataException($"Invalid {fieldName}; expected 16 bytes.");

        return new Guid(bytes);
    }

    private static Guid? ReadOptionalGuid(BinaryReader reader, string fieldName)
    {
        return reader.ReadBoolean() ? ReadGuid(reader, fieldName) : null;
    }

    private static int? ReadOptionalInt32(BinaryReader reader)
    {
        return reader.ReadBoolean() ? reader.ReadInt32() : null;
    }

    private static bool? ReadOptionalBoolean(BinaryReader reader)
    {
        return reader.ReadBoolean() ? reader.ReadBoolean() : null;
    }

    private static string[] ReadStringArray(BinaryReader reader, string fieldName)
    {
        var count = reader.ReadInt32();
        if (count < 0 || count > MaxDecodedStringArrayLength)
            throw new InvalidDataException($"Invalid {fieldName} count '{count}'.");

        var values = new string[count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = reader.ReadString();
        }

        return values;
    }

    private static void EnsureFullyRead(BinaryReader reader)
    {
        if (reader.BaseStream.Position != reader.BaseStream.Length)
            throw new InvalidDataException("Command payload contains trailing bytes.");
    }
}
