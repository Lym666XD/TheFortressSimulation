using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class MainMenuState
{
    private void OnMouseMoved(Point local)
    {
        if (_currentPage != PageMode.MainMenu)
            return;

        var newHoveredItem = GetMenuItemAtPosition(local);
        if (newHoveredItem != _hoveredItem)
        {
            _hoveredItem = newHoveredItem;
            DrawMenu();
        }
    }

    private void OnMouseClicked(Point local)
    {
        if (_currentPage != PageMode.MainMenu)
            return;

        var clickedItem = GetMenuItemAtPosition(local);
        if (clickedItem.HasValue)
        {
            _selectedItem = clickedItem.Value;
            ExecuteMenuItem(_selectedItem);
        }
    }

    private void OnRightClicked(Point local)
    {
        if (_currentPage != PageMode.MainMenu)
        {
            _currentPage = PageMode.MainMenu;
            DrawMenu();
        }
        else
        {
            Environment.Exit(0);
        }
    }

    private MenuItem? GetMenuItemAtPosition(Point mousePos)
    {
        if (_currentPage != PageMode.MainMenu)
            return null;

        int centerX = _menuSurface.Surface.Width / 2;
        int boxWidth = MENU_WIDTH;
        int boxX = centerX - boxWidth / 2;

        for (int i = 0; i < 5; i++)
        {
            int itemY = MENU_START_Y + i * MENU_ITEM_HEIGHT;
            if (mousePos.X >= boxX && mousePos.X < boxX + boxWidth &&
                mousePos.Y >= itemY && mousePos.Y < itemY + MENU_ITEM_HEIGHT)
            {
                return (MenuItem)i;
            }
        }

        return null;
    }
}
