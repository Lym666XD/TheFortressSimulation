using HumanFortress.App.UI;
using HumanFortress.App.UI.Selection;
using SadRogue.Primitives;

namespace HumanFortress.App.Runtime;

internal static class FortressPlacementClickInput
{
    public static bool TryHandleFirstCorner(
        UiStore ui,
        Point worldPosition,
        int currentZ,
        int fortressSize,
        ulong uiTick,
        ISelectionTool? selectionTool)
    {
        ArgumentNullException.ThrowIfNull(ui);

        switch (ui.PlaceMode)
        {
            case PlacementMode.StockpileFirstCorner:
                ui.PlaceFirstCorner = worldPosition;
                ui.PlaceMode = PlacementMode.StockpileSecondCorner;
                Logger.Log($"[STOCKPILE] First corner at ({worldPosition.X},{worldPosition.Y},{currentZ})");
                return true;

            case PlacementMode.HaulFirstCorner:
                ui.PlaceFirstCorner = worldPosition;
                ui.PlaceMode = PlacementMode.HaulSecondCorner;
                Logger.Log($"[HAUL] First corner at ({worldPosition.X},{worldPosition.Y},{currentZ})");
                return true;

            case PlacementMode.MiningFirstCorner:
                var miningCorner = ClampToWorld(worldPosition, fortressSize);
                ui.PlaceFirstCorner = miningCorner;
                ui.PlaceZMin = currentZ;
                ui.PlaceZMax = currentZ;
                ui.PlaceMode = PlacementMode.MiningSecondCorner;
                selectionTool?.Begin(miningCorner, currentZ);
                Logger.Log($"[MINING] First corner at ({miningCorner.X},{miningCorner.Y},{currentZ}), zMin={currentZ}, zMax={currentZ}");
                return true;

            case PlacementMode.ZoneFirstCorner:
                ui.PlaceFirstCorner = ClampToWorld(worldPosition, fortressSize);
                ui.PlaceMode = PlacementMode.ZoneSecondCorner;
                ui.AddToast("Select second corner", uiTick + 100);
                return true;

            case PlacementMode.ConstructionFirstCorner:
                ui.PlaceFirstCorner = ClampToWorld(worldPosition, fortressSize);
                ui.PlaceMode = PlacementMode.ConstructionSecondCorner;
                ui.AddToast("[BUILD] Select opposite corner", uiTick + 100);
                return true;

            case PlacementMode.BuildableFirstAnchor:
                ui.PlaceFirstCorner = ClampToWorld(worldPosition, fortressSize);
                ui.PlaceMode = PlacementMode.BuildableConfirmAnchor;
                ui.AddToast("[WORKSHOP] Click to confirm", uiTick + 100);
                return true;

            default:
                return false;
        }
    }

    private static Point ClampToWorld(Point point, int fortressSize)
    {
        int max = fortressSize * 32 - 1;
        int x = Math.Clamp(point.X, 0, max);
        int y = Math.Clamp(point.Y, 0, max);
        return new Point(x, y);
    }
}
