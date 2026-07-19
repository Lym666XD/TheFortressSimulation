using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Transport;

internal sealed class TransportJobFinalizer
{
    private readonly ReservationManager _reservations;

    internal TransportJobFinalizer(ReservationManager reservations)
    {
        _reservations = reservations;
    }

    internal void Finish(ActiveJob job, ICollection<ActiveJob> finished)
    {
        if (job.PendingSplitReservation.IsValid)
            _reservations.TryCancelStagedItemTransfer(job.PendingSplitReservation);
        _reservations.TryReleaseItem(job.ItemReservation);
        _reservations.TryReleaseCreature(job.CreatureReservation);
        finished.Add(job);
    }
}
