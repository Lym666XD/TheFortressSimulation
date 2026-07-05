using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal interface IStockpileCommandTarget
{
    bool CreateStockpile(Rectangle worldRect, int z, string presetId, ulong currentTick);
    bool DeleteStockpile(int zoneId, ulong currentTick);
}
