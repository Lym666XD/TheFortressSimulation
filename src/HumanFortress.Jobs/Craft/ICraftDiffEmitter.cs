using SadRogue.Primitives;

namespace HumanFortress.Jobs.Craft;

internal interface ICraftDiffEmitter
{
    void AddItem(Point cell, int z, string itemId, int quantity);

    void RemoveItem(Guid itemGuid, Point cell, int z, int quantity);
}
