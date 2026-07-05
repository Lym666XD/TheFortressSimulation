using HumanFortress.App.Diagnostics;
using HumanFortress.Contracts.Diagnostics;
using HumanFortress.Contracts.Runtime.Snapshots;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiDebugMenuRenderer
{
    private static void DrawStatusTab(
        ICellSurface surf,
        int x0,
        int y0,
        int width,
        Point cursor,
        int currentZ,
        int zoomLevel,
        Point camera,
        int fortressSize,
        SimulationDebugMenuData debugMenu,
        DiagnosticSnapshot? diagnostics)
    {
        surf.Print(x0 + 2, y0 + 2, "=== Fortress Status ===", Color.Yellow);
        int line = y0 + 4;
        var worldStatus = debugMenu.WorldStatus;
        if (worldStatus.HasWorld)
        {
            surf.Print(x0 + 2, line++, $"Chunks: {worldStatus.ChunksLoaded} / {fortressSize * fortressSize}", Color.Green);
            surf.Print(x0 + 2, line++, $"Items: {worldStatus.ItemInstances} (defs {worldStatus.ItemDefinitions})", Color.Green);
            surf.Print(x0 + 2, line++, $"Creatures: {worldStatus.CreatureInstances} (defs {worldStatus.CreatureDefinitions})", Color.Green);
        }
        else
        {
            surf.Print(x0 + 2, line++, "World: N/A", Color.DarkGray);
            surf.Print(x0 + 2, line++, "Items: N/A", Color.DarkGray);
            surf.Print(x0 + 2, line++, "Creatures: N/A", Color.DarkGray);
        }
        surf.Print(x0 + 2, line++, $"Cursor: {cursor.X},{cursor.Y}", Color.White);
        surf.Print(x0 + 2, line++, $"Z-Level: {currentZ}", Color.Cyan);
        surf.Print(x0 + 2, line++, $"Zoom: {zoomLevel}x", Color.White);
        surf.Print(x0 + 2, line++, $"Camera: {camera.X},{camera.Y}", Color.Gray);
        surf.Print(x0 + 2, line++, $"Map: {fortressSize}x{fortressSize} chunks", Color.Gray);

        if (diagnostics == null)
            return;

        line++;
        var diagColor = diagnostics.ErrorOrHigherCount > 0
            ? Color.Red
            : diagnostics.WarningOrHigherCount > 0 ? Color.Orange : Color.Green;
        surf.Print(x0 + 2, line++, "=== Diagnostics ===", Color.Yellow);
        surf.Print(
            x0 + 2,
            line++,
            $"Events: {diagnostics.TotalCount}  Warn+: {diagnostics.WarningOrHigherCount}  Err+: {diagnostics.ErrorOrHigherCount}",
            diagColor);

        var latestContentIssue = diagnostics.ContentIssues.LastOrDefault();
        if (latestContentIssue != null)
        {
            var maxIssueWidth = Math.Max(12, width - 6);
            surf.Print(
                x0 + 2,
                line++,
                Truncate($"Content: {latestContentIssue.Code} {latestContentIssue.Message}", maxIssueWidth),
                latestContentIssue.Level >= DiagnosticLevel.Error ? Color.Red : Color.Orange);
        }
        else
        {
            surf.Print(x0 + 2, line++, "Content: clean", Color.Green);
        }
    }
}
