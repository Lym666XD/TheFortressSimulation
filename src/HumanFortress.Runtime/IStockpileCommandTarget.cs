using SadRogue.Primitives;

namespace HumanFortress.Runtime;

public interface IStockpileCommandTarget
{
    bool CreateStockpile(Rectangle worldRect, int z, string presetId, ulong currentTick);
}
