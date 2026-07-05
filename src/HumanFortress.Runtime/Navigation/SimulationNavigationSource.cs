using HumanFortress.Contracts.Navigation;
using HumanFortress.Simulation.World;

namespace HumanFortress.Runtime.Navigation;

/// <summary>
/// Runtime adapter that exposes Simulation.World through navigation-owned
/// snapshot contracts.
/// </summary>
internal sealed partial class SimulationNavigationSource : INavigationWorldSource
{
    private readonly World _world;

    internal SimulationNavigationSource(World world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
    }

    bool INavigationWorldSource.IsValid(Point3 position)
    {
        return IsValidPosition(position);
    }

    private bool IsValidPosition(Point3 position)
    {
        return _world.IsValidPosition(position.X, position.Y, position.Z);
    }

    bool INavigationWorldSource.TryGetTile(Point3 position, out NavigationTile tile)
    {
        var source = _world.GetTile(position.X, position.Y, position.Z);
        if (!source.HasValue)
        {
            tile = default;
            return false;
        }

        tile = ToNavigationTile(source.Value);
        return true;
    }

    bool INavigationWorldSource.TryGetChunk(HumanFortress.Contracts.Navigation.ChunkKey key, out NavigationChunkSnapshot chunk)
    {
        var source = _world.GetChunk(new HumanFortress.Simulation.World.ChunkKey(key.ChunkX, key.ChunkY, key.Z));
        if (source == null)
        {
            chunk = default;
            return false;
        }

        chunk = ToNavigationChunk(source);
        return true;
    }

    IEnumerable<NavigationChunkSnapshot> INavigationWorldSource.GetAllChunks()
    {
        foreach (var chunk in _world.GetAllChunks())
            yield return ToNavigationChunk(chunk);
    }
}
