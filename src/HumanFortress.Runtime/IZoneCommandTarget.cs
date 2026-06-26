using SadRogue.Primitives;

namespace HumanFortress.Runtime;

internal interface IZoneCommandTarget
{
    int CreateZone(string defId, string name, Rectangle worldRect, int z, ulong createdTick);

    void AddZoneCells(int zoneId, Rectangle worldRect, int z);

    void RemoveZoneCells(int zoneId, Rectangle worldRect, int z);

    void DeleteZone(int zoneId);
}
