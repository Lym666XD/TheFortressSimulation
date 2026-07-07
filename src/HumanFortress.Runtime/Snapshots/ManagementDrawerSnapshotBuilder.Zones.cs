using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class ManagementDrawerSnapshotBuilder
{
    private static ZoneDrawerData BuildZones(World world)
    {
        var rows = world.Zones.Manager.GetAllZones()
            .OrderBy(zone => zone.ZoneId)
            .Select(zone =>
            {
                var definition = world.Zones.Manager.GetDefinition(zone.DefId);
                string displayName = definition?.DisplayName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(displayName))
                    displayName = zone.DefId;

                return new ZoneDrawerRowView(
                    zone.ZoneId,
                    zone.Name,
                    zone.DefId,
                    displayName,
                    zone.TotalCells,
                    zone.Priority,
                    zone.Enabled);
            })
            .ToList();

        return new ZoneDrawerData(rows);
    }

    private static StockpileDrawerData BuildStockpiles(World world)
    {
        var rows = world.Stockpiles.GetAllZones()
            .OrderBy(stockpile => stockpile.ZoneId)
            .Select(stockpile => new StockpileDrawerRowView(
                stockpile.ZoneId,
                stockpile.Name,
                stockpile.Priority,
                stockpile.TargetStacks,
                stockpile.HysteresisLow,
                stockpile.HysteresisHigh))
            .ToList();

        return new StockpileDrawerData(rows);
    }
}
