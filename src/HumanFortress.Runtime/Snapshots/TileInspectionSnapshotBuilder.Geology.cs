using HumanFortress.Contracts.Content.Registry;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class TileInspectionSnapshotBuilder
{
    private static string GetGeologyLabel(ushort geoMatId, IRuntimeGeologyCatalog? geologyCatalog)
    {
        var geology = geologyCatalog?.GetGeologyByHandle(geoMatId);
        return (geology?.Id ?? $"#{geoMatId}")
            .Replace("core_geology_", string.Empty, StringComparison.Ordinal)
            .Replace("core_terrain_", string.Empty, StringComparison.Ordinal);
    }
}
