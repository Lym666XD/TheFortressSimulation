using HumanFortress.Simulation.Jobs;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningJobFinalizer
{
    private readonly MiningTileReservationTracker _tileReservations;
    private readonly ReservationManager _reservations;

    internal MiningJobFinalizer(MiningTileReservationTracker tileReservations, ReservationManager reservations)
    {
        _tileReservations = tileReservations ?? throw new ArgumentNullException(nameof(tileReservations));
        _reservations = reservations ?? throw new ArgumentNullException(nameof(reservations));
    }

    internal void Finish(ActiveMiningJob job, ICollection<ActiveMiningJob> finished)
    {
        _tileReservations.Release(job);
        _reservations.TryReleaseCreature(job.CreatureReservation);
        finished.Add(job);
    }
}
