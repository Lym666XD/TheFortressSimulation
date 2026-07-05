using HumanFortress.App.Runtime;
using HumanFortress.Contracts.Runtime;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal sealed class FortressPlacementRuntimePorts
{
    private readonly IFortressRuntimePlacementQueryAccess _queries;
    private readonly IFortressRuntimePlacementCommandAccess _commands;

    internal FortressPlacementRuntimePorts(
        IFortressRuntimePlacementQueryAccess queries,
        IFortressRuntimePlacementCommandAccess commands)
    {
        _queries = queries ?? throw new ArgumentNullException(nameof(queries));
        _commands = commands ?? throw new ArgumentNullException(nameof(commands));
    }

    internal SimulationWorldAvailabilityData GetWorldAvailabilityData() =>
        _queries.GetWorldAvailabilityData();

    internal ZoneHitData FindZoneAt(Point worldPosition, int z) =>
        _queries.FindZoneAt(worldPosition, z);

    internal StockpileHitData FindStockpileAt(Point worldPosition, int z) =>
        _queries.FindStockpileAt(worldPosition, z);

    internal void QueueHaulOrder(Rectangle rect, int z, int priority = 50) =>
        _commands.QueueHaulOrder(rect, z, priority);

    internal void QueueAdvancedMiningOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeMiningAction action,
        int priority = 50) =>
        _commands.QueueAdvancedMiningOrder(rect, zMin, zMax, action, priority);

    internal void QueueConstructionOrder(
        Rectangle rect,
        int zMin,
        int zMax,
        RuntimeConstructionShape shape,
        string? preferredMaterialId,
        string[] materialTags,
        int priority = 50) =>
        _commands.QueueConstructionOrder(
            rect,
            zMin,
            zMax,
            shape,
            preferredMaterialId,
            materialTags,
            priority);

    internal void QueueBuildableConstructionOrder(
        string constructionId,
        Point anchor,
        int z,
        int priority = 50) =>
        _commands.QueueBuildableConstructionOrder(constructionId, anchor, z, priority);

    internal void QueueCreateZone(string defId, Rectangle rect, int z) =>
        _commands.QueueCreateZone(defId, rect, z);

    internal void QueueDeleteZone(int zoneId) =>
        _commands.QueueDeleteZone(zoneId);

    internal void QueueCreateStockpile(Rectangle rect, int z, string presetId) =>
        _commands.QueueCreateStockpile(rect, z, presetId);

    internal void QueueDeleteStockpile(int zoneId) =>
        _commands.QueueDeleteStockpile(zoneId);
}
