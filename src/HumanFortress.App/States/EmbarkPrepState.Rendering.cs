using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.States
{
    internal sealed partial class EmbarkPrepState
    {
        private void DrawUI()
        {
            _mainConsole.Clear();
            _mainConsole.Print(0, 0, "=== EMBARK PREPARATION ===", Color.Yellow);

            _mainConsole.Print(0, 2, $"Selected Location: {SelectedTile.X},{SelectedTile.Y}", Color.Cyan);

            if (_session.TryGetWorldTileView(SelectedTile, out var tile))
            {
                _mainConsole.Print(0, 3, $"Biome: {tile.BiomeName}", Color.Green);
            }

            DrawFortressSettings();
            DrawStartingResources();
            DrawControls();
            DrawGenerationNote();
        }

        private void DrawFortressSettings()
        {
            _mainConsole.Print(0, 5, "Fortress Settings:", Color.Cyan);

            var sizeColor = _selectedOption == 0 ? Color.White : Color.Gray;
            _mainConsole.Print(2, 7, $"Size: {_fortressSize}x{_fortressSize} chunks", sizeColor);
            _mainConsole.Print(2, 8, $"Total tiles: {_fortressSize * 32}x{_fortressSize * 32}", Color.DarkGray);
        }

        private void DrawStartingResources()
        {
            _mainConsole.Print(0, 10, "Starting Resources:", Color.Cyan);
            _mainConsole.Print(2, 11, "7 Dwarves (placeholder)", Color.Gray);
            _mainConsole.Print(2, 12, "Food: 100 units", Color.Gray);
            _mainConsole.Print(2, 13, "Drink: 50 units", Color.Gray);
            _mainConsole.Print(2, 14, "Seeds: 20 units", Color.Gray);
            _mainConsole.Print(2, 15, "Tools: Basic set", Color.Gray);
        }

        private void DrawControls()
        {
            _mainConsole.Print(0, 18, "Controls:", Color.Cyan);
            _mainConsole.Print(2, 19, "↑/↓ - Select option", Color.Gray);
            _mainConsole.Print(2, 20, "←/→ - Modify value", Color.Gray);
            _mainConsole.Print(2, 21, "Enter - Embark!", Color.Green);
            _mainConsole.Print(2, 22, "ESC - Back to World Map", Color.Red);
        }

        private void DrawGenerationNote()
        {
            _mainConsole.Print(0, 25, "Note: Fortress generation will create:", Color.DarkCyan);
            _mainConsole.Print(2, 26, $"- {_fortressSize}x{_fortressSize} chunks", Color.DarkGray);
            _mainConsole.Print(2, 27, "- 50 Z-levels", Color.DarkGray);
            _mainConsole.Print(2, 28, "- Surface terrain based on biome", Color.DarkGray);
            _mainConsole.Print(2, 29, "- One cavern layer", Color.DarkGray);
            _mainConsole.Print(2, 30, "- Ore veins and resources", Color.DarkGray);
        }
    }
}
