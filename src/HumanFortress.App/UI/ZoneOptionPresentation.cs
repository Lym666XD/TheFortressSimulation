using HumanFortress.Contracts.Runtime.Snapshots;

namespace HumanFortress.App.UI;

internal static class ZoneOptionPresentation
{
    internal static IReadOnlyList<ZoneMenuOptionView> GetOptions(
        SimulationZoneCatalogData catalog,
        ZoneSubmenu submenu)
    {
        var category = submenu.ToString().ToLowerInvariant();
        return catalog.Options?
            .Where(option => string.Equals(option.Category, category, StringComparison.Ordinal))
            .OrderBy(static option => option.Keybind, StringComparer.Ordinal)
            .ThenBy(static option => option.Id, StringComparer.Ordinal)
            .ToArray()
        ?? Array.Empty<ZoneMenuOptionView>();
    }
}
