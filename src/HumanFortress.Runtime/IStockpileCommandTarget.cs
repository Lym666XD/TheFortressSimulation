using SadRogue.Primitives;

namespace HumanFortress.Runtime;

internal interface IStockpileCommandTarget
{
    bool CreateStockpile(Rectangle worldRect, int z, string presetId, ulong currentTick);
}
