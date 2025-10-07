using SadConsole;
using SadRogue.Primitives;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Zones;
using System.Linq;
using System.Collections.Generic;

namespace HumanFortress.App.UI;

/// <summary>
/// Renders the Zones quick menu with L2 and L3 submenus, zone overlay, and detail popup.
/// </summary>
public sealed class ZonesUI
{
    private int? _detailPopupZoneId = null;

    // Zone definition ID mappings for keyboard shortcuts
    private readonly Dictionary<ZoneSubmenu, Dictionary<char, string>> _zoneKeyMappings = new()
    {
        [ZoneSubmenu.Production] = new()
        {
            ['z'] = "lumbering",
            ['x'] = "gather_plants",
            ['c'] = "fishing",
            ['v'] = "sand_clay",
            ['r'] = "pasture"
        },
        [ZoneSubmenu.Civil] = new()
        {
            ['z'] = "bedroom",
            ['x'] = "dormitory",
            ['c'] = "dining_hall",
            ['v'] = "bathhouse",
            ['g'] = "tomb"
        },
        [ZoneSubmenu.Public] = new()
        {
            ['z'] = "assembly",
            ['c'] = "temple",
            ['v'] = "tavern",
            ['t'] = "hospital",
            ['f'] = "office",
            ['g'] = "library"
        },
        [ZoneSubmenu.Military] = new()
        {
            ['z'] = "military_grounds"
        },
        [ZoneSubmenu.Management] = new()
        {
            ['z'] = "burrow",
            ['x'] = "restricted_traffic"
        }
    };

    public void DrawZonesRootPopup(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 30, 8, fg, bg);
        surface.Print(x + 1, y, " ZONES ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Production Zone", fg);
        surface.Print(x + 2, y + 2, "[X] Civil Zone", fg);
        surface.Print(x + 2, y + 3, "[C] Public Zone", fg);
        surface.Print(x + 2, y + 4, "[V] Military Zone", fg);
        surface.Print(x + 2, y + 5, "[F] Management Zone", fg);
        surface.Print(x + 2, y + 6, "ESC: Cancel", Color.Gray);
    }

    public void DrawZonesWithSubmenu(ScreenSurface surface, int centerX, int centerY, ZoneSubmenu activeSubmenu)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;
        var activeBg = new Color(40, 40, 0, 220);

        // L2 menu (left side)
        int l2Width = 26;
        int l2Height = 8;
        int l2X = centerX - l2Width - 2;
        int l2Y = centerY;

        DrawBox(surface, l2X, l2Y, l2Width, l2Height, fg, bg);
        surface.Print(l2X + 1, l2Y, " ZONES ", highlight);

        DrawMenuOption(surface, l2X + 2, l2Y + 1, "[Z] Production Zone", activeSubmenu == ZoneSubmenu.Production, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 2, "[X] Civil Zone", activeSubmenu == ZoneSubmenu.Civil, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 3, "[C] Public Zone", activeSubmenu == ZoneSubmenu.Public, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 4, "[V] Military Zone", activeSubmenu == ZoneSubmenu.Military, fg, activeBg);
        DrawMenuOption(surface, l2X + 2, l2Y + 5, "[F] Management Zone", activeSubmenu == ZoneSubmenu.Management, fg, activeBg);
        surface.Print(l2X + 2, l2Y + 6, "ESC: Cancel", Color.Gray);

        // L3 menu (right side)
        int l3X = centerX + 2;
        int l3Y = centerY;

        switch (activeSubmenu)
        {
            case ZoneSubmenu.Production:
                DrawProductionL3(surface, l3X, l3Y);
                break;
            case ZoneSubmenu.Civil:
                DrawCivilL3(surface, l3X, l3Y);
                break;
            case ZoneSubmenu.Public:
                DrawPublicL3(surface, l3X, l3Y);
                break;
            case ZoneSubmenu.Military:
                DrawMilitaryL3(surface, l3X, l3Y);
                break;
            case ZoneSubmenu.Management:
                DrawManagementL3(surface, l3X, l3Y);
                break;
        }
    }

    private void DrawProductionL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 34, 9, fg, bg);
        surface.Print(x + 1, y, " PRODUCTION ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Lumbering", fg);
        surface.Print(x + 2, y + 2, "[X] Gather Plants", fg);
        surface.Print(x + 2, y + 3, "[C] Fishing", fg);
        surface.Print(x + 2, y + 4, "[V] Sand/Clay", fg);
        surface.Print(x + 2, y + 5, "[R] Pasture", fg);
        surface.Print(x + 2, y + 6, "[,] Remove Zone", fg);
    }

    private void DrawCivilL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 9, fg, bg);
        surface.Print(x + 1, y, " CIVIL ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Bedroom", fg);
        surface.Print(x + 2, y + 2, "[X] Dormitory", fg);
        surface.Print(x + 2, y + 3, "[C] Dining Hall", fg);
        surface.Print(x + 2, y + 4, "[V] Bathhouse", fg);
        surface.Print(x + 2, y + 5, "[G] Tomb", fg);
        surface.Print(x + 2, y + 6, "[,] Remove Zone", fg);
    }

    private void DrawPublicL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 32, 10, fg, bg);
        surface.Print(x + 1, y, " PUBLIC ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Assembly", fg);
        surface.Print(x + 2, y + 2, "[C] Temple", fg);
        surface.Print(x + 2, y + 3, "[V] Tavern/Inn", fg);
        surface.Print(x + 2, y + 4, "[F] Office", fg);
        surface.Print(x + 2, y + 5, "[G] Library", fg);
        surface.Print(x + 2, y + 6, "[T] Hospital", fg);
        surface.Print(x + 2, y + 7, "[,] Remove Zone", fg);
    }

    private void DrawMilitaryL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 6, fg, bg);
        surface.Print(x + 1, y, " MILITARY ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Military Grounds", fg);
        surface.Print(x + 2, y + 2, "[,] Remove Zone", fg);
    }

    private void DrawManagementL3(ScreenSurface surface, int x, int y)
    {
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        DrawBox(surface, x, y, 28, 6, fg, bg);
        surface.Print(x + 1, y, " MANAGEMENT ZONE ", highlight);
        surface.Print(x + 2, y + 1, "[Z] Burrow", fg);
        surface.Print(x + 2, y + 2, "[X] Restricted Traffic", fg);
        surface.Print(x + 2, y + 3, "[,] Remove Zone", fg);
    }

    /// <summary>
    /// Try to get zone definition ID from keyboard input.
    /// </summary>
    public string? GetZoneDefIdFromKey(ZoneSubmenu submenu, char key)
    {
        if (_zoneKeyMappings.TryGetValue(submenu, out var mapping))
        {
            if (mapping.TryGetValue(key, out var defId))
            {
                return defId;
            }
        }
        return null;
    }

    /// <summary>
    /// Draw placement mode prompt.
    /// </summary>
    public void DrawPlacementMode(ScreenSurface surface, UiStore ui, Point mouseWorld)
    {
        if (ui.PlaceMode != PlacementMode.ZoneFirstCorner &&
            ui.PlaceMode != PlacementMode.ZoneSecondCorner &&
            ui.PlaceMode != PlacementMode.ZoneDelete)
            return;

        var surf = surface.Surface;
        var bg = new Color(0, 0, 0, 200);
        var fg = Color.White;

        if (ui.PlaceMode == PlacementMode.ZoneFirstCorner)
        {
            int x = 2;
            int y = surf.Height - 5;
            DrawBox(surface, x, y, 50, 3, fg, bg);
            surface.Print(x + 2, y + 1, "Zone Placement: Select first corner", Color.Yellow);
        }
        else if (ui.PlaceMode == PlacementMode.ZoneSecondCorner)
        {
            int x = 2;
            int y = surf.Height - 5;
            DrawBox(surface, x, y, 50, 3, fg, bg);
            surface.Print(x + 2, y + 1, "Zone Placement: Select second corner", Color.Yellow);
        }
        else if (ui.PlaceMode == PlacementMode.ZoneDelete)
        {
            int x = 2;
            int y = surf.Height - 5;
            DrawBox(surface, x, y, 50, 3, fg, bg);
            surface.Print(x + 2, y + 1, "Click on a zone cell to delete it", Color.Red);
        }
    }

    /// <summary>
    /// Render placement preview on map (only when zone menu is open).
    /// </summary>
    public void RenderPlacementPreview(MapScreenSurface mapSurface, Point first, Point second, Rectangle viewport, bool show)
    {
        if (!show) return;

        var rect = Rectangle.GetUnion(new Rectangle(first, 1, 1), new Rectangle(second, 1, 1));
        var gold = new Color(255, 230, 0);
        for (int wx = rect.X; wx < rect.X + rect.Width; wx++)
        {
            for (int wy = rect.Y; wy < rect.Y + rect.Height; wy++)
            {
                if (wx >= viewport.X && wx < viewport.X + viewport.Width &&
                    wy >= viewport.Y && wy < viewport.Y + viewport.Height)
                {
                    int sx = wx - viewport.X;
                    int sy = wy - viewport.Y;
                    mapSurface.Surface.SetGlyph(sx, sy, '.', gold, Color.Transparent);
                }
            }
        }
    }

    /// <summary>
    /// Render zone overlay on map (only when zone menu is open).
    /// </summary>
    public void RenderOverlay(MapScreenSurface mapSurface, World world, int currentZ, Rectangle viewport, bool showOverlay)
    {
        if (!showOverlay) return;

        // Iterate through visible chunks and render zone shards
        for (int chunkX = viewport.X / 32; chunkX <= (viewport.X + viewport.Width) / 32; chunkX++)
        {
            for (int chunkY = viewport.Y / 32; chunkY <= (viewport.Y + viewport.Height) / 32; chunkY++)
            {
                var chunkKey = new ChunkKey(chunkX, chunkY, currentZ);
                var chunk = world.GetChunk(chunkKey);
                if (chunk == null) continue;

                var zoneData = chunk.GetZoneData();
                if (zoneData == null) continue;

                foreach (var shard in zoneData.GetAllShards())
                {
                    var zone = world.Zones.Manager.GetZone(shard.ZoneId);
                    if (zone == null) continue;

                    var zoneDef = world.Zones.Manager.GetDefinition(zone.DefId);
                    if (zoneDef == null) continue;

                    // Parse color from hex string
                    Color zoneColor = ParseColor(zoneDef.UiHints.Color);

                    // Render zone cells
                    for (int localIdx = 0; localIdx < Chunk.CELLS_PER_LAYER; localIdx++)
                    {
                        if (!shard.MemberCells[localIdx]) continue;

                        var (localX, localY) = Chunk.IndexToLocal(localIdx);
                        int worldX = chunkX * 32 + localX;
                        int worldY = chunkY * 32 + localY;

                        if (worldX >= viewport.X && worldX < viewport.X + viewport.Width &&
                            worldY >= viewport.Y && worldY < viewport.Y + viewport.Height)
                        {
                            int sx = worldX - viewport.X;
                            int sy = worldY - viewport.Y;

                            // Draw zone glyph with transparency
                            mapSurface.Surface.SetGlyph(sx, sy, zoneDef.UiHints.Glyph);
                            mapSurface.Surface.SetForeground(sx, sy, zoneColor);
                            mapSurface.Surface.SetBackground(sx, sy, new Color(zoneColor.R / 4, zoneColor.G / 4, zoneColor.B / 4, 80));
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Draw zone detail popup.
    /// </summary>
    public void DrawDetailPopup(ScreenSurface surface, World world)
    {
        if (!_detailPopupZoneId.HasValue) return;

        var zone = world.Zones.Manager.GetZone(_detailPopupZoneId.Value);
        if (zone == null)
        {
            _detailPopupZoneId = null;
            return;
        }

        var zoneDef = world.Zones.Manager.GetDefinition(zone.DefId);
        if (zoneDef == null)
        {
            _detailPopupZoneId = null;
            return;
        }

        var surf = surface.Surface;
        int w = 50;
        int h = 20;
        int x0 = (surf.Width - w) / 2;
        int y0 = (surf.Height - h) / 2;
        var bg = new Color(10, 10, 10, 220);
        var fg = Color.White;

        // Draw background
        for (int yy = y0; yy < y0 + h; yy++)
            for (int xx = x0; xx < x0 + w; xx++)
                surf.SetGlyph(xx, yy, ' ', fg, bg);

        DrawBox(surface, x0, y0, w, h, Color.Yellow, bg);

        // Title
        surf.Print(x0 + 2, y0, $" ZONE: {zoneDef.DisplayName} ", Color.Cyan);

        int line = y0 + 2;
        surf.Print(x0 + 2, line++, $"ID: {zone.ZoneId}", fg);
        surf.Print(x0 + 2, line++, $"Name: {zone.Name}", fg);
        surf.Print(x0 + 2, line++, $"Type: {zoneDef.DisplayName}", fg);
        surf.Print(x0 + 2, line++, $"Category: {zoneDef.Category}", Color.Gray);
        line++;

        surf.Print(x0 + 2, line++, $"Total Cells: {zone.TotalCells}", fg);
        surf.Print(x0 + 2, line++, $"Member Chunks: {zone.MemberChunks.Count}", fg);
        surf.Print(x0 + 2, line++, $"Enabled: {(zone.Enabled ? "Yes" : "No")}", zone.Enabled ? Color.Green : Color.Red);
        line++;

        // Placeholder settings
        surf.Print(x0 + 2, line++, "--- Settings (Placeholder) ---", Color.Yellow);
        surf.Print(x0 + 2, line++, "[TODO] Zone-specific settings", Color.DarkGray);
        surf.Print(x0 + 2, line++, "[TODO] Priority adjustment", Color.DarkGray);
        surf.Print(x0 + 2, line++, "[TODO] Enable/Disable toggle", Color.DarkGray);
        line++;

        surf.Print(x0 + 2, y0 + h - 2, "Press ESC to close", Color.Gray);
    }

    /// <summary>
    /// Open zone detail popup.
    /// </summary>
    public void OpenDetailPopup(int zoneId)
    {
        _detailPopupZoneId = zoneId;
    }

    /// <summary>
    /// Close zone detail popup.
    /// </summary>
    public void CloseDetailPopup()
    {
        _detailPopupZoneId = null;
    }

    /// <summary>
    /// Check if detail popup is open.
    /// </summary>
    public bool IsDetailPopupOpen()
    {
        return _detailPopupZoneId.HasValue;
    }

    private void DrawMenuOption(ScreenSurface surface, int x, int y, string text, bool active, Color fg, Color activeBg)
    {
        if (active)
        {
            for (int i = 0; i < 22; i++)
                surface.SetGlyph(x + i, y, ' ', fg, activeBg);
            surface.Print(x, y, text, Color.Yellow);
        }
        else
        {
            surface.Print(x, y, text, fg);
        }
    }

    private void DrawBox(ScreenSurface surface, int x, int y, int width, int height, Color fg, Color bg)
    {
        for (int i = 0; i < width; i++)
            for (int j = 0; j < height; j++)
                surface.SetGlyph(x + i, y + j, ' ', fg, bg);

        for (int i = 1; i < width - 1; i++)
        {
            surface.SetGlyph(x + i, y, '-', fg, bg);
            surface.SetGlyph(x + i, y + height - 1, '-', fg, bg);
        }
        for (int j = 1; j < height - 1; j++)
        {
            surface.SetGlyph(x, y + j, '|', fg, bg);
            surface.SetGlyph(x + width - 1, y + j, '|', fg, bg);
        }
        surface.SetGlyph(x, y, '+', fg, bg);
        surface.SetGlyph(x + width - 1, y, '+', fg, bg);
        surface.SetGlyph(x, y + height - 1, '+', fg, bg);
        surface.SetGlyph(x + width - 1, y + height - 1, '+', fg, bg);
    }

    private Color ParseColor(string hexColor)
    {
        try
        {
            if (hexColor.StartsWith("#"))
            {
                hexColor = hexColor.Substring(1);
            }

            if (hexColor.Length == 6)
            {
                int r = System.Convert.ToInt32(hexColor.Substring(0, 2), 16);
                int g = System.Convert.ToInt32(hexColor.Substring(2, 2), 16);
                int b = System.Convert.ToInt32(hexColor.Substring(4, 2), 16);
                return new Color(r, g, b);
            }
        }
        catch
        {
            // Fallback to white
        }

        return Color.White;
    }
}
