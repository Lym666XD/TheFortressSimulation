using System.IO;
using HumanFortress.Core.Commands;
using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal sealed partial class RuntimeCommandReplayFactory
{
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
        var requirementCount = reader.ReadInt32();
        if (requirementCount < 0 || requirementCount > 4096)
            throw new InvalidDataException($"Invalid construction material requirement count: {requirementCount}.");
        var requirements = new MaterialRequirementSpec[requirementCount];
        for (var i = 0; i < requirementCount; i++)
        {
            var tag = reader.ReadString();
            var definitionId = reader.ReadString();
            requirements[i] = new MaterialRequirementSpec(
                string.IsNullOrEmpty(tag) ? null : tag,
                string.IsNullOrEmpty(definitionId) ? null : definitionId,
                reader.ReadInt32());
        }
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
                Tags = tags,
                Requirements = requirements
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
}
