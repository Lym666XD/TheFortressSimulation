using SadConsole;
using SadRogue.Primitives;
using HumanFortress.App.WorldGeneration;
using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.App.States;

internal sealed partial class WorldGenState
{
    private void HandleElementClick(UIElement element)
    {
        switch (element)
        {
            case UIElement.Name:
                _selectedElement = UIElement.Name;
                _isEditingName = true;
                _nameBuffer = _settings.Name;
                DrawUI();
                break;

            case UIElement.Seed:
            case UIElement.Size:
            case UIElement.Difficulty:
                _selectedElement = element;
                _isEditingName = false;
                DrawUI();
                break;

            case UIElement.PresetBeginner:
                ApplyPreset(WorldGenerationDifficulty.Easy, 128);
                break;

            case UIElement.PresetStandard:
                ApplyPreset(WorldGenerationDifficulty.Normal, 256);
                break;

            case UIElement.PresetChallenge:
                ApplyPreset(WorldGenerationDifficulty.Hard, 512);
                break;

            case UIElement.ButtonGenerate:
                StartGeneration();
                break;

            case UIElement.ButtonRandomAll:
                RandomizeAll();
                break;

            case UIElement.ButtonBack:
                _navigator.ShowMainMenu();
                break;
        }
    }

    private void ApplyPreset(WorldGenerationDifficulty difficulty, int size)
    {
        _settings.Difficulty = difficulty;
        _settings.Width = size;
        _settings.Height = size;
        _settings.Seed = WorldGenerationSettingsDefaults.NewSeed();
        _isEditingName = false;
        DrawUI();
    }

    private void RandomizeAll()
    {
        _settings.Seed = WorldGenerationSettingsDefaults.NewSeed();
        int[] sizes = { 128, 256, 512 };
        _settings.Width = sizes[new Random().Next(sizes.Length)];
        _settings.Height = _settings.Width;
        _settings.Difficulty = (WorldGenerationDifficulty)new Random().Next(4);
        DrawUI();
    }

    private void StartGeneration()
    {
        _isGenerating = true;
        _progressConsole.Clear();
        _progressConsole.Print(0, 0, "Starting world generation...", Color.Yellow);

        var result = _worldGeneration.Generate(_settings);

        if (result.Success)
        {
            _session.SetGeneratedWorld(result);
            _navigator.ShowWorldMap();
        }
        else
        {
            _progressConsole.Print(0, 6, "Generation failed!", Color.Red);
            _progressConsole.Print(0, 7, result.ErrorMessage, Color.Red);
            _isGenerating = false;
        }
    }
}
