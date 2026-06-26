using SadRogue.Primitives;

namespace HumanFortress.App.UI
{
    internal static partial class DebugLayoutCalculator
    {
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
                new Hit(new Rectangle(xPrev, y, 6, 1), 0),
                new Hit(new Rectangle(xNext, y, 8, 1), 1)
            };
        }
    }
}
