using HumanFortress.Simulation.Orders;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Mining;

internal sealed class MiningTileReservationTracker
{
    private readonly HashSet<(int X, int Y, int Z)> _reservedTiles = new();

    internal int Count => _reservedTiles.Count;

    internal bool Contains(Point cell, int z)
    {
        return _reservedTiles.Contains((cell.X, cell.Y, z));
    }

    internal void Reserve(in MiningSystem.PlannedDig dig)
    {
        _reservedTiles.Add((dig.Cell.X, dig.Cell.Y, dig.Z));
        if (dig.Action == MiningAction.DigChannel && dig.Z > 0)
        {
            _reservedTiles.Add((dig.Cell.X, dig.Cell.Y, dig.Z - 1));
        }
    }

    internal void Release(ActiveMiningJob job)
    {
        _reservedTiles.Remove((job.Target.X, job.Target.Y, job.Z));
        if (job.Action == MiningAction.DigChannel && job.Z > 0)
        {
            _reservedTiles.Remove((job.Target.X, job.Target.Y, job.Z - 1));
        }
    }

    internal IReadOnlyList<MiningReservedTileSnapshot> GetStateSnapshot()
    {
        return _reservedTiles
            .Select(tile => new MiningReservedTileSnapshot(tile.X, tile.Y, tile.Z))
            .ToArray();
    }
}

internal readonly record struct MiningReservedTileSnapshot(int X, int Y, int Z);
