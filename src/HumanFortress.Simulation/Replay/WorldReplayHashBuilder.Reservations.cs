using HumanFortress.Core.Determinism;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Replay;

internal static partial class WorldReplayHashBuilder
{
    private static string BuildReservationsHash(SimulationWorld world)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.reservations.snapshot.v1");
            AddReservationsHash(hash, world);
        });
    }

    private static void AddReservationsHash(ReplayHashBuilder hash, SimulationWorld world)
    {
        var itemReservations = world.Reservations.GetItemReservationsSnapshot()
            .OrderBy(reservation => reservation.itemId)
            .ThenBy(reservation => reservation.holderId)
            .ToArray();
        hash.AddInt32(itemReservations.Length);
        foreach (var reservation in itemReservations)
        {
            hash.AddGuid(reservation.itemId);
            hash.AddGuid(reservation.holderId);
            hash.AddUInt64(reservation.expireTick);
        }

        var creatureReservations = world.Reservations.GetCreatureReservationsSnapshot()
            .OrderBy(reservation => reservation.workerId)
            .ThenBy(reservation => reservation.holderSystem, StringComparer.Ordinal)
            .ThenBy(reservation => reservation.jobId, StringComparer.Ordinal)
            .ToArray();
        hash.AddInt32(creatureReservations.Length);
        foreach (var reservation in creatureReservations)
        {
            hash.AddGuid(reservation.workerId);
            hash.AddString(reservation.holderSystem);
            hash.AddNullableString(reservation.jobId);
            hash.AddUInt64(reservation.expireTick);
        }
    }
}
