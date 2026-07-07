using SadRogue.Primitives;

namespace HumanFortress.App.Input;

internal static partial class FortressMapClickController
{
    private static void LogTileInfo(FortressMapClickControllerContext context, Point worldPos)
    {
        try
        {
            var tile = context.Runtime.GetTileInspectionData(worldPos, context.CurrentZ);
            if (!tile.HasTile)
                return;

            Logger.Log($"[TILE] L0 geology={tile.GeologyLabel} kind={tile.TerrainKind} nat={(tile.IsNatural ? 1 : 0)} mod={(tile.IsModifiable ? 1 : 0)}");
            Logger.Log($"[TILE] L1 surface mud={tile.HasMud} grass={tile.HasGrass} snow={tile.HasSnow} fert={tile.Fertility}");
            Logger.Log($"[TILE] L3 fluid kind={tile.FluidKind} depth={tile.FluidDepth}");
            Logger.Log($"[TILE] L7 meta revealed={tile.IsRevealed} forbid={tile.IsForbidden} traffic={tile.TrafficLevel} blood={tile.HasBlood}");
        }
        catch
        {
        }
    }
}
