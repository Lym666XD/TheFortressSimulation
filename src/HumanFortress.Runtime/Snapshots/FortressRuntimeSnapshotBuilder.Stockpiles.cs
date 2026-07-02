using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class FortressRuntimeSnapshotBuilder
{
    internal static SimulationStockpileOverlayData BuildStockpileOverlaySnapshot(
        World? world,
        int currentZ,
        Rectangle viewport)
    {
        return StockpileSnapshotBuilder.BuildOverlay(world, currentZ, viewport);
    }

    internal static SimulationStockpileDetailData BuildStockpileDetailSnapshot(World? world, int zoneId)
    {
        return StockpileSnapshotBuilder.BuildDetail(world, zoneId);
    }

    internal static SimulationStockpilePresetMenuData BuildStockpilePresetMenuSnapshot(
        FortressRuntimeStockpilePresetCatalog? presets)
    {
        if (presets == null)
            return SimulationStockpilePresetMenuData.Default;

        var options = presets.GetMenuPresets()
            .Select(static preset => new StockpilePresetMenuOptionView(
                preset.Id,
                preset.Name,
                preset.Priority))
            .ToArray();

        return options.Length == 0
            ? SimulationStockpilePresetMenuData.Default
            : new SimulationStockpilePresetMenuData(options);
    }

    internal static StockpileHitData FindStockpileAt(World? world, Point worldPosition, int z)
    {
        return StockpileSnapshotBuilder.FindAt(world, worldPosition, z);
    }
}
