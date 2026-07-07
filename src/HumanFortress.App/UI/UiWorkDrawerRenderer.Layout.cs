using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.UI;

internal static partial class UiWorkDrawerRenderer
{
    private readonly struct WorkPanelLayout
    {
        public WorkPanelLayout(Rectangle left, Rectangle center, Rectangle right)
        {
            Left = left;
            Center = center;
            Right = right;
        }

        public Rectangle Left { get; }
        public Rectangle Center { get; }
        public Rectangle Right { get; }
    }

    private static WorkPanelLayout BuildWorkPanelLayout(ICellSurface surf, int startY, int maxHeight)
    {
        int panelHeight = Math.Max(12, maxHeight);
        int leftWidth = Math.Max(18, surf.Width / 5);
        int rightWidth = Math.Max(22, surf.Width / 4);
        int centerWidth = surf.Width - leftWidth - rightWidth - 4;
        if (centerWidth < 24)
        {
            int deficit = 24 - centerWidth;
            centerWidth = 24;
            rightWidth = Math.Max(18, rightWidth - deficit);
        }

        var left = new Rectangle(1, startY, leftWidth, panelHeight);
        var center = new Rectangle(left.X + left.Width + 1, startY, centerWidth, panelHeight);
        int rightX = center.X + center.Width + 1;
        var right = new Rectangle(rightX, startY, Math.Max(18, surf.Width - rightX - 1), panelHeight);
        return new WorkPanelLayout(left, center, right);
    }

    private static void DecorateWorkPanel(ICellSurface surf, WorkPanelLayout layout)
    {
        FillArea(surf, layout.Left, new Color(32, 32, 32));
        FillArea(surf, layout.Center, new Color(18, 18, 18));
        FillArea(surf, layout.Right, new Color(32, 32, 32));
    }

    private static void FillArea(ICellSurface surf, Rectangle rect, Color bg)
    {
        int maxY = Math.Min(rect.Y + rect.Height, surf.Height);
        int maxX = Math.Min(rect.X + rect.Width, surf.Width);
        for (int y = rect.Y; y < maxY; y++)
        {
            for (int x = rect.X; x < maxX; x++)
            {
                surf.SetGlyph(x, y, ' ', Color.Black, bg);
            }
        }
    }

    private static string Truncate(string value, int max)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= max) return value;
        return value.Substring(0, Math.Max(0, max - 1)) + "...";
    }
}
