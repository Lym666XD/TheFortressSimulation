using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class ButtonLayoutCalculator
{
    /// <summary>
    /// Calculate drawer tab button positions.
    /// </summary>
    public static ButtonInfo[] CalculateDrawerTabs(int screenWidth, int screenHeight, string[] tabLabels, int drawerTopY)
    {
        int y = drawerTopY;
        int startX = 24;

        var buttons = new ButtonInfo[tabLabels.Length];
        int currentX = startX;

        for (int i = 0; i < tabLabels.Length; i++)
        {
            int width = tabLabels[i].Length + 2;
            var bounds = new Rectangle(currentX, y, width, 1);
            buttons[i] = new ButtonInfo(bounds, tabLabels[i], i);
            currentX += width + 1;
        }

        return buttons;
    }

    public static int? HitTestDrawerTabs(Point screenPos, int screenWidth, int screenHeight, string[] tabLabels, int drawerTopY)
    {
        return HitTest(screenPos, CalculateDrawerTabs(screenWidth, screenHeight, tabLabels, drawerTopY));
    }
}
