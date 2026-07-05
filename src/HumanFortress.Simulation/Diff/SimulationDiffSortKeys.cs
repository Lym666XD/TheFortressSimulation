namespace HumanFortress.Simulation.Diff;

internal static class SimulationDiffSortKeys
{
    internal static long ByLocalSequence(int localSeq) => localSeq;

    internal static long ByChunkCellPriorityDescending(
        int z,
        int chunkX,
        int chunkY,
        int localIndex,
        int priority,
        int localSeq)
    {
        long key = 0;
        key |= ((long)(z & 0x3FF)) << 54;
        key |= ((long)(chunkX & 0x3FF)) << 44;
        key |= ((long)(chunkY & 0x3FF)) << 34;
        key |= ((long)(localIndex & 0xFFFF)) << 18;
        key |= ((long)(255 - (priority & 0xFF))) << 10;
        key |= (long)(localSeq & 0x3FF);
        return key;
    }

    internal static long ByChunkCellPriorityAscending(
        int z,
        int chunkX,
        int chunkY,
        int localIndex,
        int priority,
        int localSeq)
    {
        long key = 0;
        key |= ((long)(z & 0xFF)) << 56;
        key |= ((long)(chunkX & 0xFF)) << 48;
        key |= ((long)(chunkY & 0xFF)) << 40;
        key |= ((long)(localIndex & 0xFFFF)) << 24;
        key |= ((long)(priority & 0xFF)) << 16;
        key |= (long)(localSeq & 0xFFFF);
        return key;
    }

    internal static long ByStockpileCellPriorityDescending(
        int cellIndex,
        int priority,
        int operation,
        int zoneId,
        int localSeq)
    {
        long key = 0;
        key |= ((long)(cellIndex & 0xFFFF)) << 48;
        key |= ((long)(255 - priority) & 0xFF) << 40;
        key |= ((long)operation & 0xFF) << 32;
        key |= ((long)(zoneId & 0xFFFF)) << 16;
        key |= (long)(localSeq & 0xFFFF);
        return key;
    }
}
