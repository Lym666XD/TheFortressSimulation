namespace HumanFortress.Runtime.Snapshots;

internal static partial class NavigationOverlaySnapshotBuilder
{
    private static IReadOnlyList<NavigationOverlayCellView> BuildPathCells(HumanFortress.Contracts.Navigation.Path path)
    {
        if (path.Steps.IsEmpty)
            return Array.Empty<NavigationOverlayCellView>();

        var steps = path.Steps.Span;
        var cells = new List<NavigationOverlayCellView>(steps.Length);
        for (int i = 0; i < steps.Length; i++)
        {
            var node = steps[i];
            char glyph;
            string color;
            if (i == 0)
            {
                glyph = 'S';
                color = Green;
            }
            else if (i == steps.Length - 1)
            {
                glyph = 'G';
                color = Red;
            }
            else
            {
                var direction = i < steps.Length - 1
                    ? (
                        steps[i + 1].Position.X - node.Position.X,
                        steps[i + 1].Position.Y - node.Position.Y)
                    : (
                        node.Position.X - steps[i - 1].Position.X,
                        node.Position.Y - steps[i - 1].Position.Y);

                glyph = GetDirectionGlyph(direction.Item1, direction.Item2);
                color = Yellow;
            }

            cells.Add(new NavigationOverlayCellView(node.Position.X, node.Position.Y, glyph, color));
        }

        return cells;
    }

    private static char GetDirectionGlyph(int dx, int dy)
    {
        if (dx == 1 && dy == 0) return '>';
        if (dx == -1 && dy == 0) return '<';
        if (dx == 0 && dy == 1) return 'v';
        if (dx == 0 && dy == -1) return '^';
        if (dx == 1 && dy == -1) return '/';
        if (dx == 1 && dy == 1) return '\\';
        if (dx == -1 && dy == 1) return '/';
        if (dx == -1 && dy == -1) return '\\';
        return '.';
    }
}
