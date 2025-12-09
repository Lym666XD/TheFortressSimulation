using SadRogue.Primitives;

namespace HumanFortress.App.UI
{
    /// <summary>
    /// Centralized geometry + hit-test for the Debug overlay (Items tab).
    /// Mirrors UiRenderer.DrawDebug layout so drawing and interaction stay in sync.
    /// </summary>
    public static class DebugLayoutCalculator
    {
        public readonly struct Hit
        {
            public readonly Rectangle Bounds;
            public readonly int Index;
            public Hit(Rectangle bounds, int index) { Bounds = bounds; Index = index; }
            public bool Contains(Point p) => Bounds.Contains(p);
        }

        /// <summary>
        /// Compute the debug window rectangle (70% width, 60% height, centered).
        /// </summary>
        public static Rectangle CalculateWindow(int screenW, int screenH)
        {
            int width = (int)System.Math.Min((int)(screenW * 0.7), screenW - 4);
            int height = (int)System.Math.Min((int)(screenH * 0.6), screenH - 4);
            int x0 = (screenW - width) / 2;
            int y0 = (screenH - height) / 2;
            return new Rectangle(x0, y0, width, height);
        }

        /// <summary>
        /// Calculate hit rectangles for the six category pills (row at y0+3 starting from x0+2).
        /// </summary>
        public static Hit[] CalculateCategoryPills(Rectangle window, string[] labels)
        {
            var hits = new Hit[labels.Length];
            int x = window.X + 2;
            int y = window.Y + 3;
            for (int i = 0; i < labels.Length; i++)
            {
                int w = labels[i].Length + 2; // symmetric with WritePill padding
                hits[i] = new Hit(new Rectangle(x, y, w, 1), i);
                x += w + 1; // +1 gap
            }
            return hits;
        }

        /// <summary>
        /// Calculate hit rectangles for up to 10 list rows (start at x0+4, y0+5).
        /// </summary>
        public static Hit[] CalculateItemRows(Rectangle window, int maxRows = 10)
        {
            var hits = new Hit[maxRows];
            int x = window.X + 4;
            int y = window.Y + 5;
            int w = System.Math.Max(1, window.Width - 8);
            for (int i = 0; i < maxRows; i++)
            {
                hits[i] = new Hit(new Rectangle(x, y + i, w, 1), i);
            }
            return hits;
        }

        /// <summary>
        /// Calculate hit rectangles for the three tab labels shown in the header.
        /// Layout matches UiRenderer.DrawDebug: "Status | Creatures | Items" starting at x0+22.
        /// </summary>
        public static Hit[] CalculateTabs(Rectangle window)
        {
            int y = window.Y; // header row
            int x = window.X + 22;
            var labels = new[] { "Status", "Creatures", "Items" };
            var hits = new Hit[labels.Length];
            for (int i = 0; i < labels.Length; i++)
            {
                int w = labels[i].Length;
                hits[i] = new Hit(new Rectangle(x, y, w, 1), i);
                x += w + 3; // add spacing for " | "
            }
            return hits;
        }

        /// <summary>
        /// Category display labels in order. Shared by renderer and hit-testing to avoid drift.
        /// </summary>
        public static string[] GetCategoryLabels() => new[] { "Boulders", "Blocks", "Logs", "Planks", "Tools", "Weapons", "Ammo", "Siege" };

        /// <summary>
        /// Calculate page navigation buttons (Prev/Next) near Page indicator line.
        /// </summary>
        public static Hit[] CalculatePageButtons(Rectangle window)
        {
            int y = window.Y + 4;
            int xPrev = window.X + 2;
            int xNext = window.X + window.Width - 10;
            return new[]
            {
                new Hit(new Rectangle(xPrev, y, 6, 1), 0),  // "< Prev"
                new Hit(new Rectangle(xNext, y, 8, 1), 1)   // "Next >"
            };
        }
    }
}
