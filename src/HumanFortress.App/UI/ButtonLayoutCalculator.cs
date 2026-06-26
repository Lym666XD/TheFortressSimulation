using SadRogue.Primitives;

namespace HumanFortress.App.UI;

/// <summary>
/// Single source of truth for UI button positions and hit-testing.
/// </summary>
internal static partial class ButtonLayoutCalculator
{
    public const int DockButtonWidth = 5;
    public const int DockButtonGap = 1;
    public const int QuickButtonWidth = 5;
    public const int QuickButtonGap = 2;

    internal readonly struct ButtonInfo
    {
        public readonly Rectangle Bounds;
        public readonly string Label;
        public readonly int Index;

        public ButtonInfo(Rectangle bounds, string label, int index)
        {
            Bounds = bounds;
            Label = label;
            Index = index;
        }

        public bool Contains(Point point) => Bounds.Contains(point);
    }

    private static int? HitTest(Point screenPos, ButtonInfo[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i].Contains(screenPos))
                return i;
        }

        return null;
    }
}
