using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class WorldGenState
{
    private void OnMouseMoved(Point local)
    {
        if (_isGenerating)
            return;

        var newHoveredElement = GetElementAtPosition(local);
        if (newHoveredElement != _hoveredElement)
        {
            _hoveredElement = newHoveredElement;
            DrawUI();
        }
    }

    private void OnMouseClicked(Point local)
    {
        if (_isGenerating)
            return;

        var clickedElement = GetElementAtPosition(local);
        if (clickedElement.HasValue)
        {
            HandleElementClick(clickedElement.Value);
        }
    }

    private void OnRightClicked(Point local)
    {
        if (_isGenerating)
            return;

        _navigator.ShowMainMenu();
    }

    private UIElement? GetElementAtPosition(Point pos)
    {
        int centerX = _menuSurface.Surface.Width / 2;

        if (pos.Y == 10 && pos.X >= centerX - 25 && pos.X < centerX + 25)
            return UIElement.Name;

        if (pos.Y == 12 && pos.X >= centerX - 25 && pos.X < centerX + 25)
            return UIElement.Seed;

        if (pos.Y == 14 && pos.X >= centerX - 25 && pos.X < centerX + 25)
            return UIElement.Size;

        if (pos.Y == 16 && pos.X >= centerX - 25 && pos.X < centerX + 25)
            return UIElement.Difficulty;

        if (pos.Y >= 20 && pos.Y < 22)
        {
            if (pos.X >= centerX - 30 && pos.X < centerX - 20)
                return UIElement.PresetBeginner;
            if (pos.X >= centerX - 10 && pos.X < centerX + 2)
                return UIElement.PresetStandard;
            if (pos.X >= centerX + 12 && pos.X < centerX + 24)
                return UIElement.PresetChallenge;
        }

        if (pos.Y >= 24 && pos.Y < 26)
        {
            if (pos.X >= centerX - 30 && pos.X < centerX - 12)
                return UIElement.ButtonGenerate;
            if (pos.X >= centerX - 8 && pos.X < centerX + 8)
                return UIElement.ButtonRandomAll;
            if (pos.X >= centerX + 12 && pos.X < centerX + 20)
                return UIElement.ButtonBack;
        }

        return null;
    }
}
