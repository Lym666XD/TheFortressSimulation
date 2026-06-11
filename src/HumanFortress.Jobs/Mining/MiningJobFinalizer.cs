using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningJobFinalizer
{
    private readonly MiningTileReservationTracker _tileReservations;
    private readonly ReservationManager _reservations;

    public MiningJobFinalizer(MiningTileReservationTracker tileReservations, ReservationManager reservations)
    {
        _tileReservations = tileReservations ?? throw new ArgumentNullException(nameof(tileReservations));
        _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
    }

    public void Finish(ActiveMiningJob job, ICollection<ActiveMiningJob> finished)
    {
        _tileReservations.Release(job);
        _reservations.ReleaseCreature(job.WorkerId);
        finished.Add(job);
    }
}
