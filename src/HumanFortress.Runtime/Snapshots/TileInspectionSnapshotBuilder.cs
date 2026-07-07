using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class TileInspectionSnapshotBuilder
{
    internal static SimulationTileInspectionData Build(
        World? world,
        IRuntimeGeologyCatalog? geologyCatalog,
        Point tileWorldPosition,
        int tileZ)
    {
        if (world == null)
            return CreateEmpty(tileWorldPosition, tileZ);

        int chunkX = tileWorldPosition.X / 32;
        int chunkY = tileWorldPosition.Y / 32;
        int localX = tileWorldPosition.X % 32;
        int localY = tileWorldPosition.Y % 32;

        var key = new ChunkKey(chunkX, chunkY, tileZ);
        var chunk = world.GetChunk(key);
        if (chunk == null)
            return CreateEmpty(tileWorldPosition, tileZ);

        var tile = chunk.GetTile(localX, localY);
        var geologyLabel = GetGeologyLabel(tile.GeoMatId, geologyCatalog);
        var items = BuildItemViews(world, tileWorldPosition, tileZ);
        var creatures = BuildCreatureViews(world, tileWorldPosition, tileZ);

        return new SimulationTileInspectionData(
            HasTile: true,
            X: tileWorldPosition.X,
            Y: tileWorldPosition.Y,
            Z: tileZ,
            TerrainKind: tile.Kind.ToString(),
            GeologyLabel: geologyLabel,
            IsNatural: tile.IsNatural,
            IsModifiable: tile.IsModifiable,
            HasMud: tile.HasMud,
            HasGrass: tile.HasGrass,
            HasSnow: tile.HasSnow,
            Fertility: tile.Fertility,
            FluidKind: tile.FluidKind.ToString(),
            FluidDepth: tile.FluidDepth,
            IsRevealed: tile.IsRevealed,
            IsForbidden: tile.IsForbidden,
            TrafficLevel: tile.TrafficLevel,
            HasBlood: tile.HasBlood,
            Items: items,
            Creatures: creatures);
    }

}
