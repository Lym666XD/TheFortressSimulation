using SadRogue.Primitives;

namespace HumanFortress.App.UI
{
    /// <summary>
    /// Single source of truth for all UI button positions and hit-testing.
    /// Eliminates duplication between rendering and click detection logic.
    /// </summary>
    public static class ButtonLayoutCalculator
    {
        // Constants - single definition for entire UI
        public const int DockButtonWidth = 5;
        public const int DockButtonGap = 1;
        public const int QuickButtonWidth = 5;
        public const int QuickButtonGap = 2;

        /// <summary>
        /// Button information returned by layout calculations
        /// </summary>
        public readonly struct ButtonInfo
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

        /// <summary>
        /// Calculate F1-F8 dock button positions (bottom-left of screen)
        /// </summary>
        public static ButtonInfo[] CalculateDockButtons(int screenWidth, int screenHeight)
        {
            int y = screenHeight - 1; // Bottom row
            int x = 1; // Left margin

            var buttons = new ButtonInfo[8];
            string[] labels = { "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8" };

            for (int i = 0; i < 8; i++)
            {
                int buttonX = x + i * (DockButtonWidth + DockButtonGap);
                var bounds = new Rectangle(buttonX, y, DockButtonWidth, 1);
                buttons[i] = new ButtonInfo(bounds, labels[i], i);
            }

            return buttons;
        }

        /// <summary>
        /// Calculate Z/X/C/V quick menu button positions (bottom-center of screen)
        /// </summary>
        public static ButtonInfo[] CalculateQuickButtons(int screenWidth, int screenHeight)
        {
            int y = screenHeight - 1; // Bottom row (same as dock buttons)
            int center = screenWidth / 2;

            // 4 buttons: Z X C V
            int totalWidth = (QuickButtonWidth * 4) + (QuickButtonGap * 3);
            int startX = center - totalWidth / 2;

            var buttons = new ButtonInfo[4];
            string[] labels = { "Z", "X", "C", "V" };

            for (int i = 0; i < 4; i++)
            {
                int buttonX = startX + i * (QuickButtonWidth + QuickButtonGap);
                var bounds = new Rectangle(buttonX, y, QuickButtonWidth, 1);
                buttons[i] = new ButtonInfo(bounds, labels[i], i);
            }

            return buttons;
        }

        /// <summary>
        /// Hit-test for dock buttons (F1-F8). Returns button index (0-7) or null.
        /// </summary>
        public static int? HitTestDockButtons(Point screenPos, int screenWidth, int screenHeight)
        {
            var buttons = CalculateDockButtons(screenWidth, screenHeight);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].Contains(screenPos))
                    return i;
            }
            return null;
        }

        /// <summary>
        /// Hit-test for quick menu buttons (Z/X/C/V). Returns button index (0-3) or null.
        /// </summary>
        public static int? HitTestQuickButtons(Point screenPos, int screenWidth, int screenHeight)
        {
            var buttons = CalculateQuickButtons(screenWidth, screenHeight);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].Contains(screenPos))
                    return i;
            }
            return null;
        }

        /// <summary>
        /// Calculate drawer tab button positions
        /// </summary>
        public static ButtonInfo[] CalculateDrawerTabs(int screenWidth, int screenHeight, string[] tabLabels, int drawerTopY)
        {
            int y = drawerTopY;
            int startX = 24; // Matches UiRenderer.DrawDrawer

            var buttons = new ButtonInfo[tabLabels.Length];
            int currentX = startX;

            for (int i = 0; i < tabLabels.Length; i++)
            {
                int width = tabLabels[i].Length + 2; // +2 for padding
                var bounds = new Rectangle(currentX, y, width, 1);
                buttons[i] = new ButtonInfo(bounds, tabLabels[i], i);
                currentX += width + 1; // +1 for gap
            }

            return buttons;
        }

        /// <summary>
        /// Hit-test for drawer tabs. Returns tab index or null.
        /// </summary>
        public static int? HitTestDrawerTabs(Point screenPos, int screenWidth, int screenHeight, string[] tabLabels, int drawerTopY)
        {
            var buttons = CalculateDrawerTabs(screenWidth, screenHeight, tabLabels, drawerTopY);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].Contains(screenPos))
                    return i;
            }
            return null;
        }
    }
}
