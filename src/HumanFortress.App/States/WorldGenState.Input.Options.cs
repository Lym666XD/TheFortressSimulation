using HumanFortress.Contracts.WorldGen;

namespace HumanFortress.App.States;

internal sealed partial class WorldGenState
{
    private void ModifySelectedOption(bool increase)
    {
        switch (_selectedElement)
        {
            case UIElement.Name:
                _isEditingName = true;
                _nameBuffer = _settings.Name;
                break;

            case UIElement.Seed:
                if (increase)
                    _settings.Seed++;
                else
                    _settings.Seed--;
                break;

            case UIElement.Size:
                ModifyWorldSize(increase);
                break;

            case UIElement.Difficulty:
                ModifyDifficulty(increase);
                break;
        }
    }

    private void ModifyWorldSize(bool increase)
    {
        int[] sizes = { 128, 256, 512 };
        int currentIndex = Array.IndexOf(sizes, _settings.Width);
        if (currentIndex == -1) currentIndex = 1;

        if (increase)
            currentIndex = Math.Min(2, currentIndex + 1);
        else
            currentIndex = Math.Max(0, currentIndex - 1);

        _settings.Width = sizes[currentIndex];
        _settings.Height = sizes[currentIndex];
    }

    private void ModifyDifficulty(bool increase)
    {
        int diffValue = (int)_settings.Difficulty;
        if (increase)
            diffValue = Math.Min(3, diffValue + 1);
        else
            diffValue = Math.Max(0, diffValue - 1);
        _settings.Difficulty = (WorldGenerationDifficulty)diffValue;
    }
}
