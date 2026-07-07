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
        _reservations.ReleaseItem(job.ItemId);
        _reservations.ReleaseCreature(job.CreatureId);
        finished.Add(job);
    }
}
