using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport;

internal enum JobStage
{
    ToItem,
    ToDest
}

internal sealed class ActiveJob
{
    internal Guid CreatureId { get; set; }
    internal Guid ItemId { get; set; }
    internal Point3 Dest { get; set; }
    internal JobStage Stage { get; set; }
    internal int Quantity { get; set; }
    internal int InvalidReplanCount { get; set; }
    internal TransportReason Reason { get; set; }
    internal byte PathSearchAttempt { get; set; }
    internal ReservationManager.CreatureToken CreatureReservation { get; set; }
    internal ReservationManager.ItemToken ItemReservation { get; set; }
    internal ReservationManager.ItemToken PendingSplitReservation { get; set; }
    internal ulong PendingSplitIssuedTick { get; set; }
}
