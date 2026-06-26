using HumanFortress.Contracts.Navigation;

namespace HumanFortress.Runtime;

internal sealed partial class SimulationNavigationSource
{
    bool INavigationWorldSource.IsConstructionSiteAnchor(Point3 position)
    {
        if (!IsValidPosition(position))
            return false;

        var chunkKey = new HumanFortress.Simulation.World.ChunkKey(
            position.X / HumanFortress.Simulation.World.Chunk.SIZE_XY,
            position.Y / HumanFortress.Simulation.World.Chunk.SIZE_XY,
            position.Z);

        var chunk = _world.GetChunk(chunkKey);
        var placeables = chunk?.GetPlaceableData();
        if (placeables == null)
            return false;

        int localX = PositiveModulo(position.X, HumanFortress.Simulation.World.Chunk.SIZE_XY);
        int localY = PositiveModulo(position.Y, HumanFortress.Simulation.World.Chunk.SIZE_XY);
        int localIndex = HumanFortress.Simulation.World.Chunk.LocalIndex(localX, localY);

        return placeables.TryGetOwnedAt(localIndex, out var owned)
            && owned.ConstructionSite != null;
    }

    private static int PositiveModulo(int value, int divisor)
    {
        return ((value % divisor) + divisor) % divisor;
    }
}
