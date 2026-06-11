using HumanFortress.Simulation.Orders;

namespace HumanFortress.Jobs.Mining;

internal static class MiningDigOrdering
{
    public static List<MiningSystem.PlannedDig> Sort(IEnumerable<MiningSystem.PlannedDig> digs)
    {
        return digs
            .OrderByDescending(p => p.Priority)
            .ThenBy(p => p.Action == MiningAction.DigStairwell ? SegmentRank(p.Segment) : 0)
            .ThenBy(p => p.Cell.Y)
            .ThenBy(p => p.Cell.X)
            .ThenBy(p => p.Action == MiningAction.DigStairwell ? int.MaxValue - p.Z : p.Z)
            .ToList();
    }

    private static int SegmentRank(MiningSegment segment)
        => segment == MiningSegment.Top ? 0
         : segment == MiningSegment.Middle ? 1
         : segment == MiningSegment.Bottom ? 2
         : 3;
}
