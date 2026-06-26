using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.States;

internal sealed partial class WorldGenState
{
    private void DrawBorder(int x, int y, int width, int height, Color color)
    {
        for (int i = 0; i < width; i++)
        {
            _menuSurface.Surface.SetGlyph(x + i, y, '-', color);
            _menuSurface.Surface.SetGlyph(x + i, y + height - 1, '-', color);
        }

        for (int i = 0; i < height; i++)
        {
            _menuSurface.Surface.SetGlyph(x, y + i, '|', color);
            _menuSurface.Surface.SetGlyph(x + width - 1, y + i, '|', color);
        }

        _menuSurface.Surface.SetGlyph(x, y, '+', color);
        _menuSurface.Surface.SetGlyph(x + width - 1, y, '+', color);
        _menuSurface.Surface.SetGlyph(x, y + height - 1, '+', color);
        _menuSurface.Surface.SetGlyph(x + width - 1, y + height - 1, '+', color);
    }

    private void DrawParameterField(UIElement element, int centerX, int y, string label, string value, bool editable, bool hasArrows = false)
    {
        bool isSelected = _selectedElement == element;
        bool isHovered = _hoveredElement == element;
        bool isActive = isSelected || isHovered;
        bool isEditing = _isEditingName && element == UIElement.Name;

        var labelColor = Color.White;
        var valueColor = isActive ? Color.Yellow : Color.Gray;
        var bgColor = isActive ? new Color(40, 40, 30) : Color.Transparent;

        if (isActive)
        {
            for (int x = centerX - 25; x < centerX + 25; x++)
            {
                _menuSurface.Surface.SetGlyph(x, y, ' ', Color.White, bgColor);
            }
        }

        _menuSurface.Surface.Print(centerX - 24, y, label, labelColor);

        string displayValue = isEditing ? _nameBuffer + "_" : value;
        if (hasArrows && isActive)
        {
            _menuSurface.Surface.Print(centerX - 2, y, "<", Color.Cyan);
            _menuSurface.Surface.Print(centerX, y, displayValue, valueColor);
            _menuSurface.Surface.Print(centerX + displayValue.Length, y, ">", Color.Cyan);
        }
        else
        {
            _menuSurface.Surface.Print(centerX, y, displayValue, valueColor);
        }

        if (isSelected && !isEditing)
        {
            _menuSurface.Surface.Print(centerX - 26, y, ">", Color.Gold);
        }
    }

    private void DrawButton(UIElement element, int x, int y, string text, int width, Color? customColor = null)
    {
        bool isHovered = _hoveredElement == element;
        bool isSelected = _selectedElement == element;
        bool isActive = isHovered || isSelected;

        var bgColor = isActive ? new Color(60, 60, 40) : new Color(20, 20, 20);
        var textColor = isActive ? Color.White : (customColor ?? Color.Gray);
        var borderColor = isActive ? (customColor ?? Color.Gold) : Color.DarkGray;

        for (int i = 0; i < width; i++)
        {
            _menuSurface.Surface.SetGlyph(x + i, y, ' ', Color.White, bgColor);
            _menuSurface.Surface.SetGlyph(x + i, y + 1, ' ', Color.White, bgColor);
        }

        for (int i = 0; i < width; i++)
        {
            _menuSurface.Surface.SetGlyph(x + i, y, '-', borderColor);
            _menuSurface.Surface.SetGlyph(x + i, y + 1, '-', borderColor);
        }

        int textX = x + (width - text.Length) / 2;
        _menuSurface.Surface.Print(textX, y, text, textColor);
    }
}
