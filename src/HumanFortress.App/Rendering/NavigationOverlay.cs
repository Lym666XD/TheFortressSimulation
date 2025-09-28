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
        Walkability,    // Show walkable/blocked tiles
        MovementCost,   // Show movement costs as gradient
        Traffic,        // Show traffic designations
        Connectivity,   // Show connectivity versions
        PathDisplay,    // Show computed paths
        FlowField,      // Show flow field to selected point
    }

    private OverlayMode _currentMode = OverlayMode.None;
    private HumanFortress.Navigation.Path? _currentPath;
    private Point? _selectedTarget;
    private Dictionary<Point, char> _overlayChars;
    private Dictionary<Point, Color> _overlayColors;
    private NavigationManager? _navManager;

    public NavigationOverlay()
    {
        _overlayChars = new Dictionary<Point, char>();
        _overlayColors = new Dictionary<Point, Color>();
    }

    /// <summary>
    /// Set the navigation manager for accessing nav data.
    /// </summary>
    public void SetNavigationManager(NavigationManager navManager)
    {
        _navManager = navManager;
    }

    public OverlayMode CurrentMode
    {
        get => _currentMode;
        set
        {
            _currentMode = value;
            RefreshOverlay();
        }
    }

    public void SetPath(HumanFortress.Navigation.Path path)
    {
        _currentPath = path;
        if (_currentMode == OverlayMode.PathDisplay)
            RefreshOverlay();
    }

    public void ClearPath()
    {
        _currentPath = null;
        if (_currentMode == OverlayMode.PathDisplay)
            RefreshOverlay();
    }

    public void SetTarget(Point target)
    {
        _selectedTarget = target;
        if (_currentMode == OverlayMode.FlowField)
            RefreshOverlay();
    }

    /// <summary>
    /// Render navigation overlay on top of the game view.
    /// </summary>
    public void RenderOverlay(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        if (_currentMode == OverlayMode.None)
            return;

        switch (_currentMode)
        {
            case OverlayMode.Walkability:
                RenderWalkability(surface, world, z, viewport);
                break;
            case OverlayMode.MovementCost:
                RenderMovementCost(surface, world, z, viewport);
                break;
            case OverlayMode.Traffic:
                RenderTraffic(surface, world, z, viewport);
                break;
            case OverlayMode.Connectivity:
                RenderConnectivity(surface, world, z, viewport);
                break;
            case OverlayMode.PathDisplay:
                RenderPath(surface, viewport);
                break;
            case OverlayMode.FlowField:
                RenderFlowField(surface, world, z, viewport);
                break;
        }

        // Render legend
        RenderLegend(surface);
    }

    private void RenderWalkability(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        int worldSizeInTiles = world.SizeInTiles;

        for (int y = 0; y < viewport.Height && y < surface.Surface.Height; y++)
        {
            for (int x = 0; x < viewport.Width && x < surface.Surface.Width; x++)
            {
                var worldX = viewport.X + x;
                var worldY = viewport.Y + y;

                // Skip if outside world bounds
                if (worldX < 0 || worldX >= worldSizeInTiles || worldY < 0 || worldY >= worldSizeInTiles)
                    continue;

                var chunk = world.GetChunkAt(worldX / 32, worldY / 32, z);
                if (chunk == null) continue;

                var localIdx = (worldY % 32) * 32 + (worldX % 32);
                if (localIdx < 0 || localIdx >= ChunkNavData.TilesPerChunk)
                    continue;

                var navData = GetNavDataForChunk(chunk);

                if (navData != null && navData.NavMask != null && localIdx < navData.NavMask.Length)
                {
                    var caps = (NavCapability)navData.NavMask[localIdx];

                    if ((caps & NavCapability.Walk) != 0)
                    {
                        // Walkable - show as green dot
                        surface.Surface.SetGlyph(x, y, '·', Color.Green);
                    }
                    else if ((caps & NavCapability.Swim) != 0)
                    {
                        // Swimmable - show as blue wave
                        surface.Surface.SetGlyph(x, y, '~', Color.Blue);
                    }
                    else if ((caps & NavCapability.Fly) != 0)
                    {
                        // Flyable only - show as light gray
                        surface.Surface.SetGlyph(x, y, '°', Color.Gray);
                    }
                    else
                    {
                        // Blocked - show as red X
                        surface.Surface.SetGlyph(x, y, '█', Color.DarkRed);
                    }
                }
            }
        }
    }

    private void RenderMovementCost(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        int worldSizeInTiles = world.SizeInTiles;

        for (int y = 0; y < viewport.Height && y < surface.Surface.Height; y++)
        {
            for (int x = 0; x < viewport.Width && x < surface.Surface.Width; x++)
            {
                var worldX = viewport.X + x;
                var worldY = viewport.Y + y;

                // Skip if outside world bounds
                if (worldX < 0 || worldX >= worldSizeInTiles || worldY < 0 || worldY >= worldSizeInTiles)
                    continue;

                var chunk = world.GetChunkAt(worldX / 32, worldY / 32, z);
                if (chunk == null) continue;

                var localIdx = (worldY % 32) * 32 + (worldX % 32);
                if (localIdx < 0 || localIdx >= ChunkNavData.TilesPerChunk)
                    continue;

                var navData = GetNavDataForChunk(chunk);

                if (navData != null && navData.NavCost != null && localIdx < navData.NavCost.Length)
                {
                    var cost = navData.NavCost[localIdx];

                    // Map cost to digit 0-9 and color gradient
                    char costChar;
                    Color costColor;

                    if (cost == ushort.MaxValue)
                    {
                        costChar = '█';
                        costColor = Color.DarkRed;
                    }
                    else if (cost <= 10)
                    {
                        costChar = '0';
                        costColor = Color.Green;
                    }
                    else if (cost <= 20)
                    {
                        costChar = (char)('0' + (cost - 10) / 2);
                        costColor = Color.YellowGreen;
                    }
                    else if (cost <= 30)
                    {
                        costChar = (char)('5' + (cost - 20) / 2);
                        costColor = Color.Yellow;
                    }
                    else
                    {
                        costChar = '9';
                        costColor = Color.Red;
                    }

                    surface.Surface.SetGlyph(x, y, costChar, costColor);
                }
            }
        }
    }

    private void RenderTraffic(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        int worldSizeInTiles = world.SizeInTiles;

        for (int y = 0; y < viewport.Height && y < surface.Surface.Height; y++)
        {
            for (int x = 0; x < viewport.Width && x < surface.Surface.Width; x++)
            {
                var worldX = viewport.X + x;
                var worldY = viewport.Y + y;

                // Skip if outside world bounds
                if (worldX < 0 || worldX >= worldSizeInTiles || worldY < 0 || worldY >= worldSizeInTiles)
                    continue;

                // Get traffic level from tile metadata
                var chunk = world.GetChunkAt(worldX / 32, worldY / 32, z);
                if (chunk == null) continue;

                var localIdx = (worldY % 32) * 32 + (worldX % 32);
                var localX = (worldX % 32);
                var localY = (worldY % 32);
                var tile = chunk.GetTile(localX, localY);

                var trafficLevel = (tile.MetaBits >> 4) & 0x3;

                switch (trafficLevel)
                {
                    case 0: // Normal
                        // Don't overlay normal traffic
                        break;
                    case 1: // Low/Preferred
                        surface.Surface.SetGlyph(x, y, '+', Color.Green);
                        break;
                    case 2: // High
                        surface.Surface.SetGlyph(x, y, '-', Color.Yellow);
                        break;
                    case 3: // Restricted
                        surface.Surface.SetGlyph(x, y, 'R', Color.Red);
                        break;
                }
            }
        }
    }

    private void RenderConnectivity(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        var chunkVersions = new Dictionary<(int, int), int>();

        // First pass - collect all chunk versions
        for (int cy = viewport.Y / 32; cy <= (viewport.Y + viewport.Height) / 32; cy++)
        {
            for (int cx = viewport.X / 32; cx <= (viewport.X + viewport.Width) / 32; cx++)
            {
                var chunk = world.GetChunkAt(cx, cy, z);
                if (chunk != null)
                {
                    var navData = GetNavDataForChunk(chunk);
                    if (navData != null)
                    {
                        chunkVersions[(cx, cy)] = navData.ConnectivityVersion;
                    }
                }
            }
        }

        // Second pass - render chunk boundaries with version numbers
        for (int y = 0; y < viewport.Height; y++)
        {
            for (int x = 0; x < viewport.Width; x++)
            {
                var worldX = viewport.X + x;
                var worldY = viewport.Y + y;

                // Check if we're at chunk boundary
                if (worldX % 32 == 0 || worldY % 32 == 0)
                {
                    surface.Surface.SetGlyph(x, y, '┼', Color.DarkGray);
                }

                // Show version number at chunk corner
                if (worldX % 32 == 16 && worldY % 32 == 16)
                {
                    var cx = worldX / 32;
                    var cy = worldY / 32;
                    if (chunkVersions.TryGetValue((cx, cy), out var version))
                    {
                        var versionStr = version.ToString();
                        for (int i = 0; i < versionStr.Length && x + i < viewport.Width; i++)
                        {
                            surface.Surface.SetGlyph(x + i, y, versionStr[i], Color.Cyan);
                        }
                    }
                }
            }
        }
    }

    private void RenderPath(ScreenSurface surface, Rectangle viewport)
    {
        if (_currentPath == null || _currentPath.Value.Steps.IsEmpty)
            return;

        if (!_currentPath.Value.Steps.IsEmpty)
        {
            var pathNodes = _currentPath.Value.Steps.Span;

            for (int i = 0; i < pathNodes.Length; i++)
            {
                var node = pathNodes[i];
            var screenX = node.Position.X - viewport.X;
            var screenY = node.Position.Y - viewport.Y;

            if (screenX >= 0 && screenX < viewport.Width &&
                screenY >= 0 && screenY < viewport.Height)
            {
                char pathChar;
                Color pathColor;

                if (i == 0)
                {
                    // Start
                    pathChar = 'S';
                    pathColor = Color.Green;
                }
                else if (i == pathNodes.Length - 1)
                {
                    // Goal
                    pathChar = 'G';
                    pathColor = Color.Red;
                }
                else
                {
                    // Path segment - use directional arrows
                    if (i > 0 && i < pathNodes.Length - 1)
                    {
                        var prev = pathNodes[i - 1].Position;
                        var next = pathNodes[i + 1].Position;
                        var dx = next.X - prev.X;
                        var dy = next.Y - prev.Y;

                        if (dx > 0) pathChar = '→';
                        else if (dx < 0) pathChar = '←';
                        else if (dy > 0) pathChar = '↓';
                        else if (dy < 0) pathChar = '↑';
                        else pathChar = '•';
                    }
                    else
                    {
                        pathChar = '•';
                    }
                    pathColor = Color.Yellow;
                }

                    surface.Surface.SetGlyph(screenX, screenY, pathChar, pathColor);
                }
            }
        }
    }

    private void RenderFlowField(ScreenSurface surface, World world, int z, Rectangle viewport)
    {
        if (_selectedTarget == null)
            return;

        // Simple flow field visualization - arrows pointing toward target
        for (int y = 0; y < viewport.Height; y++)
        {
            for (int x = 0; x < viewport.Width; x++)
            {
                var worldX = viewport.X + x;
                var worldY = viewport.Y + y;

                // Check if walkable
                var chunk = world.GetChunkAt(worldX / 32, worldY / 32, z);
                if (chunk == null) continue;

                var localIdx = (worldY % 32) * 32 + (worldX % 32);
                var navData = GetNavDataForChunk(chunk);

                if (navData != null && (navData.NavMask[localIdx] & (byte)NavCapability.Walk) != 0)
                {
                    // Calculate direction to target
                    var dx = _selectedTarget.Value.X - worldX;
                    var dy = _selectedTarget.Value.Y - worldY;

                    char arrow;
                    if (Math.Abs(dx) > Math.Abs(dy))
                    {
                        arrow = dx > 0 ? '→' : '←';
                    }
                    else if (dy != 0)
                    {
                        arrow = dy > 0 ? '↓' : '↑';
                    }
                    else
                    {
                        arrow = '●'; // At target
                    }

                    var distance = Math.Abs(dx) + Math.Abs(dy);
                    var color = distance < 10 ? Color.Green :
                               distance < 20 ? Color.Yellow :
                               distance < 30 ? Color.Orange : Color.Red;

                    surface.Surface.SetGlyph(x, y, arrow, color);
                }
            }
        }
    }

    private void RenderLegend(ScreenSurface surface)
    {
        int legendY = surface.Surface.Height - 5;
        int legendX = 2;

        surface.Surface.Print(legendX, legendY, $"Navigation: {_currentMode}", Color.White);

        switch (_currentMode)
        {
            case OverlayMode.Walkability:
                surface.Surface.Print(legendX, legendY + 1, "· Walk", Color.Green);
                surface.Surface.Print(legendX + 10, legendY + 1, "~ Swim", Color.Blue);
                surface.Surface.Print(legendX + 20, legendY + 1, "° Fly", Color.Gray);
                surface.Surface.Print(legendX + 30, legendY + 1, "█ Block", Color.DarkRed);
                break;

            case OverlayMode.MovementCost:
                surface.Surface.Print(legendX, legendY + 1, "0-9: Cost (Green=Low, Red=High)", Color.White);
                break;

            case OverlayMode.Traffic:
                surface.Surface.Print(legendX, legendY + 1, "+ Preferred", Color.Green);
                surface.Surface.Print(legendX + 15, legendY + 1, "- High", Color.Yellow);
                surface.Surface.Print(legendX + 25, legendY + 1, "R Restricted", Color.Red);
                break;

            case OverlayMode.PathDisplay:
                surface.Surface.Print(legendX, legendY + 1, "S Start", Color.Green);
                surface.Surface.Print(legendX + 10, legendY + 1, "→ Path", Color.Yellow);
                surface.Surface.Print(legendX + 20, legendY + 1, "G Goal", Color.Red);
                break;
        }

        surface.Surface.Print(legendX, legendY + 2, "F9: Mode  |  F10: Set/Path  |  Ctrl+F10: Clear", Color.DarkGray);
    }

    private ChunkNavData? GetNavDataForChunk(Chunk chunk)
    {
        return _navManager?.GetNavData(chunk.Key);
    }

    private void RefreshOverlay()
    {
        _overlayChars.Clear();
        _overlayColors.Clear();
    }

    public void CycleMode()
    {
        var modes = Enum.GetValues<OverlayMode>();
        var currentIndex = Array.IndexOf(modes, _currentMode);
        currentIndex = (currentIndex + 1) % modes.Length;
        CurrentMode = modes[currentIndex];
    }
}
