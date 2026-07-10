using HumanFortress.Core.Simulation;
using HumanFortress.Simulation.Creatures;
using HumanFortress.Simulation.Items;
using HumanFortress.Simulation.World;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Diff;

internal static partial class SimulationDiffApplicator
{
    private static ItemInstance? FindItemByTarget(SimulationWorld world, DiffTarget target)
    {
        return target.HasEntityKey
            ? world.Items.GetInstanceByEntityKey(target.EntityKey)
            : TryLegacyEntityId(target.EntityId, out var entityId)
                ? world.Items.GetInstanceByEntityId(entityId)
                : null;
    }

    private static CreatureInstance? FindCreatureByTarget(SimulationWorld world, DiffTarget target)
    {
        return target.HasEntityKey
            ? world.Creatures.GetInstanceByEntityKey(target.EntityKey)
            : TryLegacyEntityId(target.EntityId, out var entityId)
                ? world.Creatures.GetInstanceByEntityId(entityId)
                : null;
    }

    private static CreatureInstance? FindCreatureByEntityArgument(SimulationWorld world, DiffOp op)
    {
        return op.Target.HasEntityKey
            ? world.Creatures.GetInstanceByEntityKey(op.Args)
            : TryLegacyEntityId(op.Args, out var entityId)
                ? world.Creatures.GetInstanceByEntityId(entityId)
                : null;
    }

    private static bool TryLegacyEntityId(int entityId, out uint legacyEntityId)
    {
        if (entityId < 0)
        {
            legacyEntityId = 0;
            return false;
        }

        legacyEntityId = (uint)entityId;
        return true;
    }

    private static bool TryLegacyEntityId(ulong entityId, out uint legacyEntityId)
    {
        if (entityId > uint.MaxValue)
        {
            legacyEntityId = 0;
            return false;
        }

        legacyEntityId = (uint)entityId;
        return true;
    }

    private static (ChunkKey ck, int lx, int ly) DecodeTarget(DiffTarget target)
    {
        var (chunkX, chunkY, z) = DiffTargetEncoding.DecodeChunkId(target.ChunkId);
        var (localX, localY) = DiffTargetEncoding.DecodeLocalIndex(target.LocalIndex);
        return (new ChunkKey(chunkX, chunkY, z), localX, localY);
    }
}
