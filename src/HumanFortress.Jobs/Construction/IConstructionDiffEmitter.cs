using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.Tiles;
using SadRogue.Primitives;

namespace HumanFortress.Jobs.Construction;

internal interface IConstructionDiffEmitter
{
    bool CanEmitWorldDiffs { get; }

    void SetTerrain(Point cell, int z, TerrainKind kind);

    void RemoveItem(Guid itemGuid, Point cell, int z, int quantity);

    void MoveItem(Guid itemId, Point dest, int z);

    void MoveCreature(Guid creatureId, Point3 dest);
}
