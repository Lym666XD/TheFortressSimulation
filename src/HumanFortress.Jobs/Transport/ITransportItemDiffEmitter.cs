using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport;

internal interface ITransportItemDiffEmitter
{
    Guid GenerateSplitStackItemGuid(Guid sourceItemId, Guid creatureId, ulong tick, int quantity);

    bool SplitStack(
        Guid sourceItemId,
        Guid newItemId,
        int sourceX,
        int sourceY,
        int sourceZ,
        int quantity,
        ReservationManager.ItemToken sourceReservation,
        ReservationManager.ItemToken stagedReservation);

    void MarkCarried(Guid itemId, Guid carrierId, Point3 at);

    void UnmarkCarried(Guid itemId, Point3 at);

    void MoveItem(Guid itemId, Point3 dest);
}
