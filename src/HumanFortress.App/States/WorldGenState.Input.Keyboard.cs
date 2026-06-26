using SadConsole.Input;
using HumanFortress.App.WorldGeneration;

namespace HumanFortress.App.States;

internal sealed partial class WorldGenState
{
    public override bool ProcessKeyboard(Keyboard keyboard)
    {
        if (_isGenerating)
            return true;

        if (_isEditingName)
            return ProcessNameEditKeyboard(keyboard);

        if (keyboard.IsKeyPressed(Keys.Escape))
        {
            _navigator.ShowMainMenu();
            return true;
        }

        if (keyboard.IsKeyPressed(Keys.Up))
        {
            _selectedElement = (UIElement)(((int)_selectedElement - 1 + 10) % 10);
            DrawUI();
            return true;
        }
        else if (keyboard.IsKeyPressed(Keys.Down))
        {
            _selectedElement = (UIElement)(((int)_selectedElement + 1) % 10);
            DrawUI();
            return true;
        }
        else if (keyboard.IsKeyPressed(Keys.Left) || keyboard.IsKeyPressed(Keys.Right))
        {
            ModifySelectedOption(keyboard.IsKeyPressed(Keys.Right));
            DrawUI();
            return true;
        }
        else if (keyboard.IsKeyPressed(Keys.R))
        {
            _settings.Seed = WorldGenerationSettingsDefaults.NewSeed();
            DrawUI();
            return true;
        }
        else if (keyboard.IsKeyPressed(Keys.Enter))
        {
            HandleElementClick(_selectedElement);
            return true;
        }

        return false;
    }
}
