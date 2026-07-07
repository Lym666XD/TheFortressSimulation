using HumanFortress.Contracts.Navigation;
using HumanFortress.Core.Simulation;
using HumanFortress.Jobs.Transport;
using HumanFortress.Simulation.Jobs;
using HumanFortress.Simulation.Stockpile;
using WorldModel = HumanFortress.Simulation.World.World;

namespace HumanFortress.Jobs.Diff;

internal sealed class TransportStockpileIndexEmitter : ITransportStockpileIndexEmitter
{
    private readonly WorldModel _world;
    private readonly StockpileDiffLog _stockpileDiffs;
    private readonly int _priority;
    private readonly string _systemId;

    internal TransportStockpileIndexEmitter(
        WorldModel world,
        StockpileDiffLog stockpileDiffs,
        int priority,
        string systemId)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _stockpileDiffs = stockpileDiffs ?? throw new ArgumentNullException(nameof(stockpileDiffs));
        _priority = priority;
        _systemId = systemId ?? throw new ArgumentNullException(nameof(systemId));
    }

    internal void RecordPickup(Guid itemId, Point3 source)
    {
        if (!TryResolveStockpileCell(source, out var location))
            return;

        _stockpileDiffs.AddRemoveItem(
            DiffTargetEncoding.SignedEntityId(itemId),
            location.ChunkKey,
            location.CellIndex,
            location.ZoneId,
            quantity: 1,
            priority: _priority,
            systemId: _systemId,
            ProjectItem(itemId));
    }

    internal void RecordDelivery(Guid itemId, Point3 destination, TransportReason reason)
    {
        if (!CanWriteStockpileIndex(reason))
            return;

        if (!TryResolveStockpileCell(destination, out var location))
            return;

        _stockpileDiffs.AddPlaceItem(
            DiffTargetEncoding.SignedEntityId(itemId),
            location.ChunkKey,
            location.CellIndex,
            location.ZoneId,
            quantity: 1,
            priority: _priority,
            systemId: _systemId,
            ProjectItem(itemId));
    }

    internal void ReleaseDestinationReservation(Point3 destination, TransportReason reason)
    {
        if (!CanWriteStockpileIndex(reason))
            return;

        if (!TryResolveStockpileCell(destination, out var location))
            return;

        _stockpileDiffs.AddReleaseSlot(
            location.ChunkKey,
            location.ZoneId,
            priority: _priority,
            systemId: _systemId);
    }

    private bool TryResolveStockpileCell(Point3 position, out StockpileCellLocation location)
    {
        return StockpileWorldQueries.TryGetStockpileCell(_world, position.X, position.Y, position.Z, out location);
    }

    private ItemStackRef? ProjectItem(Guid itemId)
    {
        var item = _world.Items.GetInstance(itemId);
        if (item == null)
            return null;

        var definition = _world.Items.GetDefinition(item.DefinitionId);
        return StockpileItemProjection.FromItem(item, definition);
    }

    private static bool CanWriteStockpileIndex(TransportReason reason)
    {
        return reason is TransportReason.ToStockpile
            or TransportReason.ToWorkshopOutput
            or TransportReason.FromTradeDepot
            or TransportReason.ToArmory
            or TransportReason.ToAmmoCache;
    }

    void ITransportStockpileIndexEmitter.RecordPickup(Guid itemId, Point3 source) =>
        RecordPickup(itemId, source);

    void ITransportStockpileIndexEmitter.RecordDelivery(Guid itemId, Point3 destination, TransportReason reason) =>
        RecordDelivery(itemId, destination, reason);

    void ITransportStockpileIndexEmitter.ReleaseDestinationReservation(Point3 destination, TransportReason reason) =>
        ReleaseDestinationReservation(destination, reason);
}
