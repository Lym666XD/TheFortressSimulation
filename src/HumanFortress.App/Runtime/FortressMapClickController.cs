using HumanFortress.App.UI;
using HumanFortress.Contracts.Content.Registry;
using HumanFortress.Simulation.World;
using HumanFortress.WorldGen;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal readonly record struct FortressMapClickControllerContext(
    UiStore Ui,
    FortressLoadedSessionSnapshot LoadedSession,
    int CurrentZ,
    ulong UiTick,
    IConstructionCatalog? Constructions,
    Action<Point, int> OpenTilePanel,
    Action Redraw);

internal static class FortressMapClickController
{
    public static bool TryHandleWorkshopCellClick(FortressMapClickControllerContext context, Point worldPos)
    {
        if (context.LoadedSession.World == null)
            return false;

        int worldZ = context.CurrentZ;
        int chunkX = worldPos.X / Chunk.SIZE_XY;
        int chunkY = worldPos.Y / Chunk.SIZE_XY;
        int localX = worldPos.X % Chunk.SIZE_XY;
        int localY = worldPos.Y % Chunk.SIZE_XY;
        var chunk = context.LoadedSession.World.GetChunk(new ChunkKey(chunkX, chunkY, worldZ));
        if (chunk == null)
            return false;

        var placeableData = chunk.GetPlaceableData();
        if (placeableData == null)
            return false;

        if (!placeableData.TryGetOwnedAt(Chunk.LocalIndex(localX, localY), out var placeable))
            return false;

        if (!IsWorkshop(context.Constructions, placeable.ConstructionSite?.TargetId ?? placeable.DefinitionId))
            return false;

        context.Ui.OpenWorkshopPanel(placeable.Guid, new Point(placeable.Position.X, placeable.Position.Y), placeable.Z);
        context.Redraw();
        return true;
    }

    public static bool TryHandleNormalClick(FortressMapClickControllerContext context, Point worldPos)
    {
        var ui = context.Ui;
        if (ui.Context != UiContext.PlacingTool
            && context.LoadedSession.UiServices?.StockpileUI != null
            && context.LoadedSession.World != null
            && context.LoadedSession.UiServices.StockpileUI.HandleStockpileClick(worldPos, context.CurrentZ, context.LoadedSession.World))
        {
            context.Redraw();
            return true;
        }

        if (ui.Context == UiContext.Global && ui.QuickMenu == QuickMenuKind.Zones)
        {
            int zoneId = context.LoadedSession.World?.Zones.GetZoneAtPosition(worldPos.X, worldPos.Y, context.CurrentZ) ?? 0;
            if (zoneId > 0)
            {
                context.LoadedSession.UiServices?.ZonesUI.OpenDetailPopup(zoneId);
                context.Redraw();
                return true;
            }
        }

        if (context.LoadedSession.World != null)
        {
            var workshop = FindWorkshopAt(context.LoadedSession.World, worldPos, context.CurrentZ, context.Constructions);
            if (workshop != null)
            {
                ui.OpenWorkshopPanel(workshop.Value.placeable.Guid, workshop.Value.placeable.Position, workshop.Value.placeable.Z);
                ui.AddToast("Workshop details", context.UiTick + 120);
                context.Redraw();
                return true;
            }
        }

        context.OpenTilePanel(worldPos, context.CurrentZ);
        Logger.Log($"[CLICK] Open TilePanel at world=({worldPos.X},{worldPos.Y},{context.CurrentZ})");
        LogTileInfo(context, worldPos);
        context.Redraw();
        return true;
    }

    private static (HumanFortress.Simulation.Placeables.PlaceableInstance placeable, Chunk chunk)? FindWorkshopAt(
        World world,
        Point worldPos,
        int z,
        IConstructionCatalog? constructions)
    {
        foreach (var chunk in world.GetAllChunks())
        {
            var placeableData = chunk.GetPlaceableData();
            if (placeableData == null)
                continue;

            foreach (var placeable in placeableData.GetAllOwnedPlaceables())
            {
                if (placeable.Z != z)
                    continue;

                if (!IsWorkshop(constructions, placeable.DefinitionId))
                    continue;

                var footprint = placeable.Footprint;
                int x0 = placeable.Position.X;
                int y0 = placeable.Position.Y;
                if (worldPos.X >= x0 && worldPos.X < x0 + footprint.W && worldPos.Y >= y0 && worldPos.Y < y0 + footprint.D)
                    return (placeable, chunk);
            }
        }

        return null;
    }

    private static bool IsWorkshop(IConstructionCatalog? constructions, string constructionId)
    {
        var definition = constructions?.GetConstruction(constructionId);
        if (definition == null)
            return false;

        return string.Equals(definition.Category, "workshop", StringComparison.OrdinalIgnoreCase)
            || (definition.PlaceableProfile.Tags != null && Array.IndexOf(definition.PlaceableProfile.Tags, "workshop") >= 0);
    }

    private static void LogTileInfo(FortressMapClickControllerContext context, Point worldPos)
    {
        try
        {
            if (context.LoadedSession.World == null)
                return;

            int chunkX = worldPos.X / Chunk.SIZE_XY;
            int chunkY = worldPos.Y / Chunk.SIZE_XY;
            int localX = worldPos.X % Chunk.SIZE_XY;
            int localY = worldPos.Y % Chunk.SIZE_XY;
            var key = new ChunkKey(chunkX, chunkY, context.CurrentZ);
            var simChunk = context.LoadedSession.World.GetChunk(key);
            if (simChunk == null)
                return;

            var tile = simChunk.GetTile(localX, localY);
            string geoIdWorld = context.LoadedSession.FortressMap?.GetChunk(chunkX, chunkY)?.GetGeologyId(localX, localY, context.CurrentZ) ?? "?";
            Logger.Log($"[TILE] L0 geology={geoIdWorld} kind={tile.Kind} nat={(tile.IsNatural ? 1 : 0)} mod={(tile.IsModifiable ? 1 : 0)}");
            Logger.Log($"[TILE] L1 surface mud={tile.HasMud} grass={tile.HasGrass} snow={tile.HasSnow} fert={tile.Fertility}");
            Logger.Log($"[TILE] L3 fluid kind={tile.FluidKind} depth={tile.FluidDepth}");
            Logger.Log($"[TILE] L7 meta revealed={tile.IsRevealed} forbid={tile.IsForbidden} traffic={tile.TrafficLevel} blood={tile.HasBlood}");
        }
        catch
        {
        }
    }
}
