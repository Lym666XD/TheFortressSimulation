using HumanFortress.Core.Simulation;
using SadRogue.Primitives;
using SimulationChunk = HumanFortress.Simulation.World.Chunk;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Diff;

internal static partial class SimulationDiffApplicator
{
    private static void ApplyMoveCreature(SimulationWorld world, DiffOp op)
    {
        var (ck, lx, ly) = DecodeTarget(op.Target);
        int worldX = ck.ChunkX * SimulationChunk.SIZE_XY + lx;
        int worldY = ck.ChunkY * SimulationChunk.SIZE_XY + ly;
        int worldZ = ck.Z;

        var creature = FindCreatureByTarget(world, op.Target);
        if (creature == null)
            throw new InvalidOperationException("MoveCreature target does not resolve to a live creature.");

        creature.Position = new Point(worldX, worldY);
        creature.Z = worldZ;
    }
}
