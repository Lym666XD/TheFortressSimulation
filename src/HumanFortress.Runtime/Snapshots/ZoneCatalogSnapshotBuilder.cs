using HumanFortress.Contracts.Runtime.Snapshots;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Snapshots;

internal static class ZoneCatalogSnapshotBuilder
{
    internal static SimulationZoneCatalogData Build(World? world)
    {
        if (world == null)
            return SimulationZoneCatalogData.Empty;

        var options = world.Zones.Manager.GetAllDefinitions()
            .Where(static definition =>
                !string.IsNullOrWhiteSpace(definition.Id)
                && !string.IsNullOrWhiteSpace(definition.Category)
                && !string.IsNullOrWhiteSpace(definition.DisplayName)
                && !string.IsNullOrWhiteSpace(definition.UiHints.Keybind))
            .Select(static definition => new ZoneMenuOptionView(
                definition.Id,
                definition.Category,
                definition.DisplayName,
                definition.UiHints.Keybind))
            .OrderBy(static option => option.Category, StringComparer.Ordinal)
            .ThenBy(static option => option.Keybind, StringComparer.Ordinal)
            .ThenBy(static option => option.Id, StringComparer.Ordinal)
            .ToArray();
        return new SimulationZoneCatalogData(Array.AsReadOnly(options));
    }
}
