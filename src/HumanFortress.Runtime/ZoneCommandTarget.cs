using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime;

public sealed class ZoneCommandTarget : IZoneCommandTarget
{
    private readonly World _world;

    public ZoneCommandTarget(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    public int CreateZone(string defId, string name, Rectangle worldRect, int z, ulong createdTick)
    {
        return _world.Zones.CreateZoneFromRect(defId, name, worldRect, z, createdTick);
    }

    public void AddZoneCells(int zoneId, Rectangle worldRect, int z)
    {
        _world.Zones.AddCellsToZone(zoneId, worldRect, z);
    }

    public void RemoveZoneCells(int zoneId, Rectangle worldRect, int z)
    {
        _world.Zones.RemoveCellsFromZone(zoneId, worldRect, z);
    }

    public void DeleteZone(int zoneId)
    {
        _world.Zones.DeleteZone(zoneId);
    }
}
