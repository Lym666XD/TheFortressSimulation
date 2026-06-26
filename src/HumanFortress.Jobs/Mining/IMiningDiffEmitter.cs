using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Mining;

internal interface IMiningDiffEmitter
{
    void MoveCreature(uint entityId, Point3 position);

    void SetTerrain(Point cell, int z, TerrainKind kind, ushort overrideGeology);

    void AddItem(Point cell, int z, string itemId, int quantity);
}
