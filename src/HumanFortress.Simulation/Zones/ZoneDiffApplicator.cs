using HumanFortress.Simulation.Diagnostics;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Zones;

internal static class ZoneDiffApplicator
{
    internal static void ApplyAll(SimulationWorld world, IReadOnlyList<ZoneDiff> diffs)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diffs);

        if (diffs.Count == 0)
            return;

        foreach (var diff in diffs.OrderBy(static d => d.GetSortKey()))
        {
            try
            {
                Apply(world, diff);
            }
            catch (Exception ex)
            {
                SimulationDiagnostics.Error(
                    world.Diagnostics,
                    "Simulation.ZoneDiff",
                    $"[ZoneDiffApplicator] Failed to apply diff {diff.Op}: {ex.Message}",
                    ex);
                throw;
            }
        }
    }

    private static void Apply(SimulationWorld world, ZoneDiff diff)
    {
        if (diff.Op != ZoneDiffOp.CreateZone
            && world.Zones.Manager.GetZone(diff.ZoneId) == null)
        {
            throw new InvalidOperationException($"Zone {diff.ZoneId} does not exist.");
        }

        switch (diff.Op)
        {
            case ZoneDiffOp.CreateZone:
                world.Zones.CreateZoneFromRect(diff.DefId, diff.Name, diff.WorldRect, diff.Z, diff.CreatedTick);
                break;

            case ZoneDiffOp.AddCells:
                world.Zones.AddCellsToZone(diff.ZoneId, diff.WorldRect, diff.Z);
                break;

            case ZoneDiffOp.RemoveCells:
                world.Zones.RemoveCellsFromZone(diff.ZoneId, diff.WorldRect, diff.Z);
                break;

            case ZoneDiffOp.DeleteZone:
                world.Zones.DeleteZone(diff.ZoneId);
                break;
        }
    }
}
