using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport;

internal interface ITransportStockpileIndexEmitter
{
    void RecordPickup(Guid itemId, Point3 source);

    void RecordDelivery(Guid itemId, Point3 destination, TransportReason reason);

    void ReleaseDestinationReservation(Point3 destination, TransportReason reason);
}

internal sealed class NullTransportStockpileIndexEmitter : ITransportStockpileIndexEmitter
{
    internal static NullTransportStockpileIndexEmitter Instance { get; } = new();

    private NullTransportStockpileIndexEmitter()
    {
    }

    void ITransportStockpileIndexEmitter.RecordPickup(Guid itemId, Point3 source)
    {
    }

    void ITransportStockpileIndexEmitter.RecordDelivery(Guid itemId, Point3 destination, TransportReason reason)
    {
    }

    void ITransportStockpileIndexEmitter.ReleaseDestinationReservation(Point3 destination, TransportReason reason)
    {
    }
}
