using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime;

internal sealed class ZoneCommandTarget : IZoneCommandTarget
{
    private readonly World _world;

    internal ZoneCommandTarget(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    int IZoneCommandTarget.CreateZone(string defId, string name, Rectangle worldRect, int z, ulong createdTick)
    {
        return _world.Zones.CreateZoneFromRect(defId, name, worldRect, z, createdTick);
    }

    void IZoneCommandTarget.AddZoneCells(int zoneId, Rectangle worldRect, int z)
    {
        _world.Zones.AddCellsToZone(zoneId, worldRect, z);
    }

    void IZoneCommandTarget.RemoveZoneCells(int zoneId, Rectangle worldRect, int z)
    {
        _world.Zones.RemoveCellsFromZone(zoneId, worldRect, z);
    }

    void IZoneCommandTarget.DeleteZone(int zoneId)
    {
        _world.Zones.DeleteZone(zoneId);
    }
}
