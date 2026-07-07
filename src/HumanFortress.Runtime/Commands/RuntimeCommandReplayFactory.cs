using System.IO;
using HumanFortress.Core.Commands;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal sealed partial class RuntimeCommandReplayFactory : ICommandReplayFactory
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
