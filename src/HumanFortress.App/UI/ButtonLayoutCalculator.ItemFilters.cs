using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class ButtonLayoutCalculator
{
    /// <summary>
    /// Calculate F2 Items tab filter pills.
    /// </summary>
    public static ButtonInfo[] CalculateItemKindFilterPills(int screenWidth, int screenHeight, string[] kindLabels, int filterRowY)
    {
        int startX = 18;
        var pills = new ButtonInfo[kindLabels.Length];
        int currentX = startX;

        for (int i = 0; i < kindLabels.Length; i++)
        {
            int width = kindLabels[i].Length;
            var bounds = new Rectangle(currentX, filterRowY, width, 1);
            pills[i] = new ButtonInfo(bounds, kindLabels[i], i);
            currentX += width + 1;
        }

        return pills;
    }

    public static int? HitTestItemKindFilterPills(Point screenPos, int screenWidth, int screenHeight, string[] kindLabels, int filterRowY)
    {
        return HitTest(screenPos, CalculateItemKindFilterPills(screenWidth, screenHeight, kindLabels, filterRowY));
    }
}
