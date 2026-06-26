using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class WorldMapState
{
    private void UpdateInfoPanel()
    {
        _infoPanel.Clear();
        _infoPanel.Print(0, 0, "=== TILE INFO ===", Color.Yellow);

        if (!_session.TryGetWorldSize(out int worldWidth, out int worldHeight))
            return;

        if (_cursorPos.X >= 0 && _cursorPos.X < worldWidth &&
            _cursorPos.Y >= 0 && _cursorPos.Y < worldHeight &&
            _session.TryGetWorldTileView(_cursorPos, out var tile))
        {
            _infoPanel.Print(0, 2, $"Position: {_cursorPos.X},{_cursorPos.Y}", Color.White);
            _infoPanel.Print(0, 3, $"Biome: {tile.BiomeName}", Color.Cyan);
            _infoPanel.Print(0, 5, $"Elevation: {tile.Elevation:F2}", Color.Gray);
            _infoPanel.Print(0, 6, $"Temperature: {tile.Temperature:F2}", Color.Orange);
            _infoPanel.Print(0, 7, $"Rainfall: {tile.Rainfall:F2}", Color.Blue);
            _infoPanel.Print(0, 8, $"Drainage: {tile.Drainage:F2}", Color.Brown);

            if (tile.IsEmbarkable)
            {
                _infoPanel.Print(0, 10, "[EMBARKABLE]", Color.Green);
                _infoPanel.Print(0, 11, "Press Enter to embark", Color.Gray);
            }
            else
            {
                _infoPanel.Print(0, 10, "[NOT EMBARKABLE]", Color.Red);
                var failures = tile.EmbarkabilityFailures;
                if (failures.Count > 0)
                {
                    _infoPanel.Print(0, 11, "Reasons:", Color.Gray);
                    for (int i = 0; i < failures.Count && i < 3; i++)
                    {
                        _infoPanel.Print(0, 12 + i, $"- {failures[i]}", Color.Gray);
                    }
                }
            }
        }
    }
}
