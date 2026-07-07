using HumanFortress.Simulation.World;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Snapshots;

internal static partial class ZoneOverlaySnapshotBuilder
{
    internal static SimulationZoneOverlayData Build(World? world, int currentZ, Rectangle viewport, bool showOverlay)
    {
        if (!showOverlay || world == null)
            return SimulationZoneOverlayData.Empty;

        var cells = new List<ZoneOverlayCellView>();

        for (int chunkX = viewport.X / Chunk.SIZE_XY; chunkX <= (viewport.X + viewport.Width) / Chunk.SIZE_XY; chunkX++)
        {
            for (int chunkY = viewport.Y / Chunk.SIZE_XY; chunkY <= (viewport.Y + viewport.Height) / Chunk.SIZE_XY; chunkY++)
            {
                var chunkKey = new ChunkKey(chunkX, chunkY, currentZ);
                var chunk = world.GetChunk(chunkKey);
                var zoneData = chunk?.GetZoneData();
                if (zoneData == null)
                    continue;

                foreach (var shard in zoneData.GetAllShards())
                {
                    var zone = world.Zones.Manager.GetZone(shard.ZoneId);
                    if (zone == null)
                        continue;

                    var definition = world.Zones.Manager.GetDefinition(zone.DefId);
                    if (definition == null)
                        continue;

                    for (int localIdx = 0; localIdx < Chunk.CELLS_PER_LAYER; localIdx++)
                    {
                        if (!shard.MemberCells[localIdx])
                            continue;

                        var (localX, localY) = Chunk.IndexToLocal(localIdx);
                        int worldX = chunkX * Chunk.SIZE_XY + localX;
                        int worldY = chunkY * Chunk.SIZE_XY + localY;

                        if (worldX < viewport.X
                            || worldX >= viewport.X + viewport.Width
                            || worldY < viewport.Y
                            || worldY >= viewport.Y + viewport.Height)
                        {
                            continue;
                        }

                        cells.Add(new ZoneOverlayCellView(
                            worldX,
                            worldY,
                            definition.UiHints.Glyph,
                            definition.UiHints.Color));
                    }
                }
            }
        }

        return cells.Count == 0
            ? SimulationZoneOverlayData.Empty
            : new SimulationZoneOverlayData(cells);
    }
}
