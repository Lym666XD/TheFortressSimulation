using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class ButtonLayoutCalculator
{
    /// <summary>
    /// Calculate F1-F8 dock button positions.
    /// </summary>
    public static ButtonInfo[] CalculateDockButtons(int screenWidth, int screenHeight)
    {
        int y = screenHeight - 1;
        int x = 1;

        var slots = UiChromeSlots.DockSlots;
        var buttons = new ButtonInfo[slots.Count];

        for (int i = 0; i < slots.Count; i++)
        {
            int buttonX = x + i * (DockButtonWidth + DockButtonGap);
            var bounds = new Rectangle(buttonX, y, DockButtonWidth, 1);
            buttons[i] = new ButtonInfo(bounds, slots[i].Label, i);
        }

        return buttons;
    }

    /// <summary>
    /// Calculate Z/X/C/V quick menu button positions.
    /// </summary>
    public static ButtonInfo[] CalculateQuickButtons(int screenWidth, int screenHeight)
    {
        int y = screenHeight - 1;
        int center = screenWidth / 2;

        int totalWidth = (QuickButtonWidth * 4) + (QuickButtonGap * 3);
        int startX = center - totalWidth / 2;

        var slots = UiChromeSlots.QuickSlots;
        var buttons = new ButtonInfo[slots.Count];

        for (int i = 0; i < slots.Count; i++)
        {
            int buttonX = startX + i * (QuickButtonWidth + QuickButtonGap);
            var bounds = new Rectangle(buttonX, y, QuickButtonWidth, 1);
            buttons[i] = new ButtonInfo(bounds, slots[i].Label, i);
        }

        return buttons;
    }

    public static int? HitTestDockButtons(Point screenPos, int screenWidth, int screenHeight)
    {
        return HitTest(screenPos, CalculateDockButtons(screenWidth, screenHeight));
    }

    public static int? HitTestQuickButtons(Point screenPos, int screenWidth, int screenHeight)
    {
        return HitTest(screenPos, CalculateQuickButtons(screenWidth, screenHeight));
    }
}
