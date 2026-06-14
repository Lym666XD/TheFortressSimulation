using HumanFortress.Navigation;
using HumanFortress.Simulation.World;
using SadConsole;
using SadRogue.Primitives;

namespace HumanFortress.App.Rendering;

/// <summary>
/// Debug overlay for visualizing navigation data.
/// </summary>
public class NavigationOverlay
{
    public enum OverlayMode
    {
        None,
        Walkability,
        MovementCost,
        Traffic,
        Connectivity,
        PathDisplay,
        FlowField,
        RampMask,
    }

    private OverlayMode _currentMode = OverlayMode.None;
    private HumanFortress.Navigation.Path? _currentPath;
    private Point? _selectedTarget;
    private readonly Dictionary<Point, char> _overlayChars;
    private readonly Dictionary<Point, Color> _overlayColors;
    private NavigationManager? _navManager;
    private readonly HumanFortress.Navigation.NavigationTuning _tuning;

    public NavigationOverlay(HumanFortress.Navigation.NavigationTuning? tuning = null)
    {
        _overlayChars = new Dictionary<Point, char>();
        _overlayColors = new Dictionary<Point, Color>();
        _tuning = tuning ?? HumanFortress.Navigation.NavigationTuning.Default;
    }

    public void SetNavigationManager(NavigationManager navManager) => _navManager = navManager;

    public OverlayMode CurrentMode
    {
        get => _currentMode;
        set { _currentMode = value; RefreshOverlay(); }
    }

    public void SetPath(HumanFortress.Navigation.Path path)
    {
        _currentPath = path;
        if (_currentMode == OverlayMode.PathDisplay) RefreshOverlay();
    }

    public void ClearPath()
    {
        _currentPath = null;
        if (_currentMode == OverlayMode.PathDisplay) RefreshOverlay();
    }

    public void SetTarget(Point target)
    {
        _selectedTarget = target;
        if (_currentMode == OverlayMode.FlowField) RefreshOverlay();
    }

    public void RenderOverlay(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        if (_currentMode == OverlayMode.None) return;
        switch (_currentMode)
        {
            case OverlayMode.Walkability:  RenderWalkability(surface, world, z, viewport); break;
            case OverlayMode.MovementCost: RenderMovementCost(surface, world, z, viewport); break;
            case OverlayMode.Traffic:      RenderTraffic(surface, world, z, viewport); break;
            case OverlayMode.Connectivity: RenderConnectivity(surface, world, z, viewport); break;
            case OverlayMode.PathDisplay:  RenderPath(surface, viewport); break;
            case OverlayMode.FlowField:    RenderFlowField(surface, world, z, viewport); break;
            case OverlayMode.RampMask:     RenderRampMask(surface, world, z, viewport); break;
        }
        RenderLegend(surface);
    }

    private void RenderWalkability(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        int worldSize = world.SizeInTiles;
        for (int y = 0; y < viewport.Height && y < surface.Surface.Height; y++)
        for (int x = 0; x < viewport.Width  && x < surface.Surface.Width;  x++)
        {
            int wx = viewport.X + x, wy = viewport.Y + y;
            if (wx < 0 || wx >= worldSize || wy < 0 || wy >= worldSize) continue;
            var chunk = world.GetChunkAt(wx / 32, wy / 32, z); if (chunk == null) continue;
            int idx = (wy % 32) * 32 + (wx % 32);
            var nav = GetNavDataForChunk(chunk); if (nav == null) continue;
            var caps = (NavCapability)nav.NavMask[idx];
            if ((caps & NavCapability.Walk) != 0) surface.Surface.SetGlyph(x, y, '.', Color.Green);
            else if ((caps & NavCapability.Swim) != 0) surface.Surface.SetGlyph(x, y, '~', Color.Blue);
            else if ((caps & NavCapability.Fly)  != 0) surface.Surface.SetGlyph(x, y, 'o', Color.Gray);
            else surface.Surface.SetGlyph(x, y, 'X', Color.DarkRed);
        }
    }

    private void RenderMovementCost(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        int worldSize = world.SizeInTiles;
        for (int y = 0; y < viewport.Height && y < surface.Surface.Height; y++)
        for (int x = 0; x < viewport.Width  && x < surface.Surface.Width;  x++)
        {
            int wx = viewport.X + x, wy = viewport.Y + y;
            if (wx < 0 || wx >= worldSize || wy < 0 || wy >= worldSize) continue;
            var chunk = world.GetChunkAt(wx / 32, wy / 32, z); if (chunk == null) continue;
            int idx = (wy % 32) * 32 + (wx % 32);
            var nav = GetNavDataForChunk(chunk); if (nav == null) continue;
            var cost = nav.NavCost[idx];
            if (cost == ushort.MaxValue)
            {
                surface.Surface.SetGlyph(x, y, 'X', Color.DarkRed);
                continue;
            }

            // Scale to fixed-point ratio against BaseCost for finer bins
            // ratio_fp = (cost * 10) / BaseCost  -> 0.0, 1.0, 1.2, ...
            double ratio = (double)cost / Math.Max(1, (int)_tuning.BaseCost);
            double fp = ratio * 10.0; // FP=10 view

            // Quantize into 36 bins: 0-9 -> '0'..'9', 10-35 -> 'A'..'Z'
            int bin = (int)Math.Clamp(Math.Round(fp), 0, 35);
            char glyph = bin < 10 ? (char)('0' + bin) : (char)('A' + (bin - 10));

            // Color gradient by ratio
            Color col = ratio <= 1.0 ? Color.Green : ratio <= 1.5 ? Color.YellowGreen : ratio <= 2.0 ? Color.Yellow : ratio <= 3.0 ? Color.Orange : Color.Red;
            surface.Surface.SetGlyph(x, y, glyph, col);
        }
    }

    private void RenderRampMask(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        int worldSize = world.SizeInTiles;
        for (int y = 0; y < viewport.Height && y < surface.Surface.Height; y++)
        for (int x = 0; x < viewport.Width  && x < surface.Surface.Width;  x++)
        {
            int wx = viewport.X + x, wy = viewport.Y + y;
            if (wx < 0 || wx >= worldSize || wy < 0 || wy >= worldSize) continue;
            var chunk = world.GetChunkAt(wx / 32, wy / 32, z); if (chunk == null) continue;
            int idx = (wy % 32) * 32 + (wx % 32);
            var nav = GetNavDataForChunk(chunk); if (nav == null) continue;

            byte mask = nav.UpRampMask[idx];
            if (mask == 0) continue; // not a ramp base or no directions

            // If single direction -> draw arrow. Multiple -> '*'
            int count = CountBits(mask);
            char ch;
            if (count == 1)
            {
                byte dir = FirstBit(mask);
                ch = dir switch
                {
                    0 => '^',  // N
                    1 => '/',  // NE
                    2 => '>',  // E
                    3 => '\\', // SE
                    4 => 'v',  // S
                    5 => '/',  // SW
                    6 => '<',  // W
                    7 => '\\', // NW
                    _ => '+'
                };
            }
            else
            {
                ch = '*';
            }

            surface.Surface.SetGlyph(x, y, ch, Color.Yellow);
        }
    }

    private void RenderTraffic(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        int worldSize = world.SizeInTiles;
        for (int y = 0; y < viewport.Height && y < surface.Surface.Height; y++)
        for (int x = 0; x < viewport.Width  && x < surface.Surface.Width;  x++)
        {
            int wx = viewport.X + x, wy = viewport.Y + y;
            if (wx < 0 || wx >= worldSize || wy < 0 || wy >= worldSize) continue;
            var chunk = world.GetChunkAt(wx / 32, wy / 32, z); if (chunk == null) continue;
            int lx = wx % 32, ly = wy % 32; var tile = chunk.GetTile(lx, ly);
            var level = (tile.MetaBits >> 4) & 0x3;
            if (level == 1) surface.Surface.SetGlyph(x, y, '+', Color.Green);
            else if (level == 2) surface.Surface.SetGlyph(x, y, '-', Color.Yellow);
            else if (level == 3) surface.Surface.SetGlyph(x, y, 'R', Color.Red);
        }
    }

    private void RenderConnectivity(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        // Draw chunk boundaries and version dots at centers
        for (int y = 0; y < viewport.Height; y++)
        for (int x = 0; x < viewport.Width;  x++)
        {
            int wx = viewport.X + x, wy = viewport.Y + y;
            if (wx % 32 == 0 || wy % 32 == 0) surface.Surface.SetGlyph(x, y, ':', Color.DarkGray);
            if (wx % 32 == 16 && wy % 32 == 16)
            {
                var ck = world.GetChunkAt(wx / 32, wy / 32, z);
                if (ck != null)
                {
                    var nav = GetNavDataForChunk(ck);
                    if (nav != null)
                    {
                        var s = nav.ConnectivityVersion.ToString();
                        for (int i = 0; i < s.Length && x + i < viewport.Width; i++)
                            surface.Surface.SetGlyph(x + i, y, s[i], Color.Cyan);
                    }
                }
            }
        }
    }

    private void RenderPath(ScreenSurface surface, Rectangle viewport)
    {
        if (_currentPath == null || _currentPath.Value.Steps.IsEmpty) return;
        var steps = _currentPath.Value.Steps.Span;
        for (int i = 0; i < steps.Length; i++)
        {
            var node = steps[i];
            int sx = node.Position.X - viewport.X;
            int sy = node.Position.Y - viewport.Y;
            if (sx < 0 || sx >= viewport.Width || sy < 0 || sy >= viewport.Height) continue;
            char ch; Color col;
            if (i == 0) { ch = 'S'; col = Color.Green; }
            else if (i == steps.Length - 1) { ch = 'G'; col = Color.Red; }
            else
            {
                int dx = 0, dy = 0;
                if (i < steps.Length - 1) { var n = steps[i + 1].Position; dx = n.X - node.Position.X; dy = n.Y - node.Position.Y; }
                else { var p = steps[i - 1].Position; dx = node.Position.X - p.X; dy = node.Position.Y - p.Y; }
                if (dx == 1 && dy == 0) ch = '>';
                else if (dx == -1 && dy == 0) ch = '<';
                else if (dx == 0 && dy == 1) ch = 'v';
                else if (dx == 0 && dy == -1) ch = '^';
                else if (dx == 1 && dy == -1) ch = '/';   // NE
                else if (dx == 1 && dy == 1) ch = '\\'; // SE
                else if (dx == -1 && dy == 1) ch = '/';   // SW
                else if (dx == -1 && dy == -1) ch = '\\'; // NW
                else ch = '.';
                col = Color.Yellow;
            }
            surface.Surface.SetGlyph(sx, sy, ch, col);
        }
    }

    private void RenderFlowField(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        if (_selectedTarget == null) return;
        for (int y = 0; y < viewport.Height; y++)
        for (int x = 0; x < viewport.Width;  x++)
        {
            int wx = viewport.X + x, wy = viewport.Y + y;
            var chunk = world.GetChunkAt(wx / 32, wy / 32, z); if (chunk == null) continue;
            int idx = (wy % 32) * 32 + (wx % 32);
            var nav = GetNavDataForChunk(chunk); if (nav == null) continue;
            if ((nav.NavMask[idx] & (byte)NavCapability.Walk) == 0) continue;
            int dx = _selectedTarget.Value.X - wx;
            int dy = _selectedTarget.Value.Y - wy;
            char arrow = Math.Abs(dx) > Math.Abs(dy) ? (dx > 0 ? '>' : '<') : (dy != 0 ? (dy > 0 ? 'v' : '^') : '.');
            var dist = Math.Abs(dx) + Math.Abs(dy);
            var col = dist < 10 ? Color.Green : dist < 20 ? Color.Yellow : dist < 30 ? Color.Orange : Color.Red;
            surface.Surface.SetGlyph(x, y, arrow, col);
        }
    }

    private void RenderLegend(ScreenSurface surface)
    {
        int legendY = surface.Surface.Height - 5, legendX = 2;
        surface.Surface.Print(legendX, legendY, $"Navigation: {_currentMode}", Color.White);
        if (_currentMode == OverlayMode.PathDisplay && _currentPath.HasValue && _currentPath.Value.Length > 0)
        {
            // TotalCost is fixed-point (FP=10)
            double totalCost = _currentPath.Value.TotalCost / 10.0;
            surface.Surface.Print(legendX + 24, legendY, $"Path len={_currentPath.Value.Length} cost={totalCost:F1}", Color.Gold);
        }
        switch (_currentMode)
        {
            case OverlayMode.Walkability:
                surface.Surface.Print(legendX, legendY + 1, ". Walk", Color.Green);
                surface.Surface.Print(legendX + 10, legendY + 1, "~ Swim", Color.Blue);
                surface.Surface.Print(legendX + 20, legendY + 1, "o Fly", Color.Gray);
                surface.Surface.Print(legendX + 30, legendY + 1, "X Block", Color.DarkRed);
                break;
            case OverlayMode.MovementCost:
                surface.Surface.Print(legendX, legendY + 1, "0-9,A-Z: FP cost bins (Green=Low, Red=High)", Color.White);
                surface.Surface.Print(legendX, legendY + 2, $"Base={_tuning.BaseCost} (tile cost shown as x{10}/Base)", Color.DarkGray);
                break;
            case OverlayMode.PathDisplay:
                surface.Surface.Print(legendX, legendY + 1, "S Start  ./>^\\ Path  G Goal", Color.White);
                break;
            case OverlayMode.RampMask:
                surface.Surface.Print(legendX, legendY + 1, "Ramp ascend: ^ > v < / \\ (*=multi)", Color.White);
                break;
        }
        surface.Surface.Print(legendX, legendY + 2, "F9: Mode  |  F10: Set/Path  |  Ctrl+F10: Clear", Color.DarkGray);
    }

    private ChunkNavData? GetNavDataForChunk(Chunk chunk)
    {
        var key = new HumanFortress.Navigation.ChunkKey(chunk.Key.ChunkX, chunk.Key.ChunkY, chunk.Key.Z);
        return _navManager?.GetNavData(key);
    }
    private void RefreshOverlay() { _overlayChars.Clear(); _overlayColors.Clear(); }
    public void CycleMode() { var modes = Enum.GetValues<OverlayMode>(); int i = Array.IndexOf(modes, _currentMode); CurrentMode = modes[(i + 1) % modes.Length]; }

    private static int CountBits(byte v)
    {
        int c = 0; while (v != 0) { v &= (byte)(v - 1); c++; } return c;
    }

    private static byte FirstBit(byte v)
    {
        for (byte i = 0; i < 8; i++) if ((v & (1 << i)) != 0) return i; return 0;
    }
}
