using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class MainMenuState
{
    private void DrawMenuItems(int centerX, int startY)
    {
        DrawMenuItem(MenuItem.NewWorld, "NEW WORLD", centerX, startY);
        DrawMenuItem(MenuItem.LoadWorld, "LOAD WORLD", centerX, startY + MENU_ITEM_HEIGHT);
        DrawMenuItem(MenuItem.Settings, "SETTINGS", centerX, startY + MENU_ITEM_HEIGHT * 2);
        DrawMenuItem(MenuItem.Credits, "CREDITS", centerX, startY + MENU_ITEM_HEIGHT * 3);
        DrawMenuItem(MenuItem.Exit, "EXIT", centerX, startY + MENU_ITEM_HEIGHT * 4);
    }

    private void DrawMenuItem(MenuItem item, string text, int centerX, int y)
    {
        bool isSelected = _selectedItem == item;
        bool isHovered = _hoveredItem == item;
        bool isActive = isSelected || isHovered;

        int boxWidth = MENU_WIDTH;
        int boxX = centerX - boxWidth / 2;

        var bgColor = isActive ? new Color(60, 60, 40) : new Color(20, 20, 20);
        var borderColor = isActive ? Color.Gold : new Color(80, 80, 80);
        var textColor = isActive ? Color.White : new Color(150, 150, 150);

        for (int x = boxX; x < boxX + boxWidth; x++)
        {
            _menuSurface.Surface.SetGlyph(x, y, ' ', Color.White, bgColor);
        }

        for (int x = boxX; x < boxX + boxWidth; x++)
        {
            _menuSurface.Surface.SetGlyph(x, y, '-', borderColor);
        }

        if (isSelected)
        {
            _menuSurface.Surface.Print(boxX + 2, y, ">", Color.Gold);
        }

        int textX = centerX - text.Length / 2;
        _menuSurface.Surface.Print(textX, y, text, textColor);
    }
}
