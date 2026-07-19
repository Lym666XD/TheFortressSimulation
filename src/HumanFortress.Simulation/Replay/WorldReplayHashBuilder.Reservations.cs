using HumanFortress.Core.Determinism;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Replay;

internal static partial class WorldReplayHashBuilder
{
    private static string BuildReservationsHash(SimulationWorld world)
    {
        return ReplayHashBuilder.Compute(hash =>
        {
            hash.AddString("world.reservations.snapshot.v2");
            AddReservationsHash(hash, world);
        });
    }

    private static void AddReservationsHash(ReplayHashBuilder hash, SimulationWorld world)
    {
        hash.AddUInt64(world.Reservations.GetGenerationHighWatermark());
        var itemReservations = world.Reservations.GetItemReservationsSnapshot()
            .OrderBy(reservation => reservation.itemId)
            .ThenBy(reservation => reservation.holderId)
            .ThenBy(reservation => reservation.systemId, StringComparer.Ordinal)
            .ThenBy(reservation => reservation.jobId, StringComparer.Ordinal)
            .ThenBy(reservation => reservation.generation)
            .ToArray();
        hash.AddInt32(itemReservations.Length);
        foreach (var reservation in itemReservations)
        {
            hash.AddGuid(reservation.itemId);
            hash.AddGuid(reservation.holderId);
            hash.AddString(reservation.systemId);
            hash.AddString(reservation.jobId);
            hash.AddUInt64(reservation.generation);
            hash.AddUInt64(reservation.expireTick);
            hash.AddBoolean(reservation.stagedTransfer);
            hash.AddGuid(reservation.transferSourceId);
            hash.AddUInt64(reservation.transferSourceGeneration);
        }

        var creatureReservations = world.Reservations.GetCreatureReservationsSnapshot()
            .OrderBy(reservation => reservation.workerId)
            .ThenBy(reservation => reservation.holderSystem, StringComparer.Ordinal)
            .ThenBy(reservation => reservation.jobId, StringComparer.Ordinal)
            .ThenBy(reservation => reservation.generation)
            .ToArray();
        hash.AddInt32(creatureReservations.Length);
        foreach (var reservation in creatureReservations)
        {
            hash.AddGuid(reservation.workerId);
            hash.AddString(reservation.holderSystem);
            hash.AddString(reservation.jobId);
            hash.AddUInt64(reservation.generation);
            hash.AddUInt64(reservation.expireTick);
        }
    }
}
