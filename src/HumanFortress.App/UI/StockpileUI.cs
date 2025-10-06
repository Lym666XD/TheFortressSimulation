using System;
using System.Collections.Generic;
using System.Linq;
using SadConsole;
using SadRogue.Primitives;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Stockpile;
using HumanFortress.Core.Commands;

namespace HumanFortress.App.UI;

/// <summary>
/// UI handler for stockpile zones.
/// Manages creation, editing, deletion, and visualization.
/// </summary>
public sealed class StockpileUI
{
    private readonly StockpileManager _stockpileManager;
    private readonly Dictionary<int, StockpileVisual> _visuals = new();
    private StockpilePreset[]? _presets;
    private int _selectedPresetIndex = 0;

    // Edit popup state
    private bool _editPopupOpen = false;
    private int? _editingZoneId = null;
    private Point _editPopupPos;

    public StockpileUI(StockpileManager stockpileManager)
    {
        _stockpileManager = stockpileManager ?? throw new ArgumentNullException(nameof(stockpileManager));
        LoadPresets();
    }

    /// <summary>
    /// Draw zone menu (after X -> Z).
    /// </summary>
    public void DrawZoneMenu(ScreenSurface surface, int x, int y)
    {
        var bg = Color.Black.SetAlpha(200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        // Menu box - compact height
        DrawBox(surface, x, y, 20, 4, fg, bg);
        surface.Print(x + 1, y, " ZONES ", highlight);

        surface.Print(x + 2, y + 1, "[Z] Stockpile", fg);
        surface.Print(x + 2, y + 2, "[R] Room", Color.Gray); // Future
    }

    /// <summary>
    /// Draw stockpile submenu (after X -> Z -> Z).
    /// </summary>
    public void DrawStockpileMenu(ScreenSurface surface, int x, int y)
    {
        var bg = Color.Black.SetAlpha(200);
        var fg = Color.White;
        var highlight = Color.Yellow;

        // Menu box - compact height
        DrawBox(surface, x, y, 25, 5, fg, bg);
        surface.Print(x + 1, y, " STOCKPILE ", highlight);

        surface.Print(x + 2, y + 1, "[Z] Create new", fg);
        surface.Print(x + 2, y + 2, "[,] Delete area", fg);
        surface.Print(x + 2, y + 3, "[X] Copy settings", fg);
    }

    /// <summary>
    /// Draw placement mode UI.
    /// </summary>
    public void DrawPlacementMode(ScreenSurface surface, UiStore ui, Point mouseWorld)
    {
        var statusY = surface.Height - 2;

        switch (ui.PlaceMode)
        {
            case PlacementMode.StockpileFirstCorner:
                surface.Print(2, statusY, "[STOCKPILE] Click first corner - ESC to cancel", Color.Yellow);
                break;

            case PlacementMode.StockpileSecondCorner:
                if (ui.PlaceFirstCorner.HasValue)
                {
                    var size = CalculateRectSize(ui.PlaceFirstCorner.Value, mouseWorld);
                    surface.Print(2, statusY,
                        $"[STOCKPILE] Click opposite corner - {size.x}x{size.y} = {size.x * size.y} tiles - ESC to cancel",
                        Color.Yellow);
                }
                break;

            case PlacementMode.StockpilePresetSelect:
                DrawPresetSelection(surface);
                break;

            case PlacementMode.StockpileDelete:
                surface.Print(2, statusY, "[DELETE] Click and drag to remove stockpile area - ESC to cancel", Color.Red);
                break;

            case PlacementMode.StockpileCopy:
                surface.Print(2, statusY, "[COPY] Click a stockpile to copy its settings - ESC to cancel", Color.Cyan);
                break;
        }
    }

    /// <summary>
    /// Draw preset selection menu.
    /// </summary>
    private void DrawPresetSelection(ScreenSurface surface)
    {
        if (_presets == null || _presets.Length == 0)
            return;

        int x = surface.Width / 2 - 15;
        int y = surface.Height / 2 - 8;

        var bg = Color.Black.SetAlpha(220);
        var fg = Color.White;
        var highlight = Color.Yellow;

        // Draw box
        DrawBox(surface, x, y, 30, 12, fg, bg);
        surface.Print(x + 1, y, " SELECT PRESET ", highlight);

        // List presets
        for (int i = 0; i < Math.Min(_presets.Length, 9); i++)
        {
            var preset = _presets[i];
            var key = (i + 1).ToString();
            var selected = i == _selectedPresetIndex;

            var lineColor = selected ? highlight : fg;
            surface.Print(x + 2, y + 2 + i, $"[{key}] {preset.Name}", lineColor);
        }

        surface.Print(x + 2, y + 11, "Enter/Click to confirm", Color.Gray);
    }

    /// <summary>
    /// Draw edit popup for a stockpile.
    /// </summary>
    public void DrawEditPopup(ScreenSurface surface)
    {
        if (!_editPopupOpen || !_editingZoneId.HasValue)
            return;

        var zone = _stockpileManager.GetZone(_editingZoneId.Value);
        if (zone == null)
            return;

        int x = Math.Max(2, Math.Min(_editPopupPos.X, surface.Width - 32));
        int y = Math.Max(2, Math.Min(_editPopupPos.Y, surface.Height - 12));

        var bg = Color.Black.SetAlpha(230);
        var fg = Color.White;
        var highlight = Color.Cyan;

        // Draw popup
        DrawBox(surface, x, y, 30, 10, fg, bg);
        surface.Print(x + 1, y, $" {zone.Name} ", highlight);

        // Zone info
        var visual = GetVisual(zone.ZoneId);
        surface.Print(x + 2, y + 2, $"Capacity: {visual.UsedCells}/{visual.TotalCells}", fg);
        surface.Print(x + 2, y + 3, $"Priority: {GetPriorityName(zone.Priority)}", fg);
        surface.Print(x + 2, y + 4, $"Filter: {GetFilterSummary(zone.Filter)}", fg);

        // Actions
        surface.Print(x + 2, y + 6, "[R] Rename", fg);
        surface.Print(x + 2, y + 7, "[P] Change Priority", fg);
        surface.Print(x + 2, y + 8, "[F] Change Filter", fg);
        surface.Print(x + 16, y + 6, "[D] Delete Zone", Color.Red);
        surface.Print(x + 16, y + 8, "[ESC] Close", Color.Gray);
    }

    /// <summary>
    /// Render stockpile overlays on the map.
    /// </summary>
    public void RenderOverlay(ScreenSurface mapSurface, World world, int currentZ, Rectangle viewport)
    {
        foreach (var zone in _stockpileManager.GetAllZones())
        {
            var visual = GetVisual(zone.ZoneId);
            RenderZoneOverlay(mapSurface, zone, visual, world, currentZ, viewport);
        }
    }

    /// <summary>
    /// Render preview rectangle during placement.
    /// </summary>
    public void RenderPlacementPreview(ScreenSurface mapSurface, Point corner1, Point corner2,
        Rectangle viewport, bool valid)
    {
        var rect = CreateRectangle(corner1, corner2);
        var color = valid ? Color.Green.SetAlpha(100) : Color.Red.SetAlpha(100);

        for (int x = rect.X; x < rect.X + rect.Width; x++)
        {
            for (int y = rect.Y; y < rect.Y + rect.Height; y++)
            {
                var screenX = x - viewport.X;
                var screenY = y - viewport.Y;

                if (screenX >= 0 && screenX < mapSurface.Width &&
                    screenY >= 0 && screenY < mapSurface.Height)
                {
                    // Draw border or fill
                    bool isBorder = x == rect.X || x == rect.X + rect.Width - 1 ||
                                   y == rect.Y || y == rect.Y + rect.Height - 1;

                    if (isBorder)
                    {
                        mapSurface.SetGlyph(screenX, screenY, '+', color);
                    }
                    else
                    {
                        // Tint the existing tile
                        mapSurface.SetGlyph(screenX, screenY, ' ', color.SetAlpha(50));
                    }
                }
            }
        }
    }

    /// <summary>
    /// Handle click on stockpile for editing.
    /// </summary>
    public bool HandleStockpileClick(Point worldPos, int z, World world)
    {
        foreach (var zone in _stockpileManager.GetAllZones())
        {
            if (IsPointInZone(zone, worldPos, z, world))
            {
                OpenEditPopup(zone.ZoneId, worldPos);
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Handle preset selection input.
    /// </summary>
    public string? HandlePresetSelection(int key)
    {
        if (_presets == null || key < 1 || key > 9)
            return null;

        int index = key - 1;
        if (index < _presets.Length)
        {
            _selectedPresetIndex = index;
            return _presets[index].Id;
        }
        return null;
    }

    #region Private Helpers

    private void LoadPresets()
    {
        // TODO: Load from stockpile_presets.json
        _presets = new[]
        {
            new StockpilePreset { Id = "all", Name = "All Items" },
            new StockpilePreset { Id = "wood", Name = "Wood" },
            new StockpilePreset { Id = "stone", Name = "Stone" },
            new StockpilePreset { Id = "metal", Name = "Metal" },
            new StockpilePreset { Id = "food", Name = "Food" },
            new StockpilePreset { Id = "refuse", Name = "Refuse" }
        };
    }

    private void OpenEditPopup(int zoneId, Point worldPos)
    {
        _editPopupOpen = true;
        _editingZoneId = zoneId;
        _editPopupPos = worldPos;
    }

    public void CloseEditPopup()
    {
        _editPopupOpen = false;
        _editingZoneId = null;
    }

    private StockpileVisual GetVisual(int zoneId)
    {
        if (!_visuals.TryGetValue(zoneId, out var visual))
        {
            visual = new StockpileVisual { ZoneId = zoneId };
            _visuals[zoneId] = visual;
        }
        return visual;
    }

    private void RenderZoneOverlay(ScreenSurface mapSurface, StockpileZone zone,
        StockpileVisual visual, World world, int z, Rectangle viewport)
    {
        // 获取储存区所在的chunk并渲染
        foreach (var chunkKey in zone.MemberChunks)
        {
            if (chunkKey.Z != z) continue; // 只显示当前层

            var chunk = world.GetChunk(chunkKey);
            if (chunk == null) continue;

            var stockpileData = chunk.GetStockpileData();
            if (stockpileData == null) continue;

            var shard = stockpileData.GetShard(zone.ZoneId);
            if (shard == null) continue;

            // 渲染该chunk中的储存区格子
            for (int cellIndex = 0; cellIndex < shard.MemberCells.Length; cellIndex++)
            {
                if (!shard.MemberCells[cellIndex]) continue;

                int localX = cellIndex % 32;
                int localY = cellIndex / 32;
                int worldX = chunkKey.ChunkX * 32 + localX;
                int worldY = chunkKey.ChunkY * 32 + localY;

                int screenX = worldX - viewport.X;
                int screenY = worldY - viewport.Y;

                if (screenX >= 0 && screenX < mapSurface.Width &&
                    screenY >= 0 && screenY < mapSurface.Height)
                {
                    // 用绿色的 'S' 标记储存区
                    mapSurface.SetGlyph(screenX, screenY, 'S', Color.Green.SetAlpha(150));
                }
            }
        }
    }

    private bool IsPointInZone(StockpileZone zone, Point worldPos, int z, World world)
    {
        int chunkX = worldPos.X / 32;
        int chunkY = worldPos.Y / 32;
        var chunkKey = new HumanFortress.Simulation.World.ChunkKey(chunkX, chunkY, z);

        if (!zone.MemberChunks.Contains(chunkKey))
            return false;

        // 检查具体格子
        var chunk = world.GetChunk(chunkKey);
        if (chunk == null) return false;

        var stockpileData = chunk.GetStockpileData();
        if (stockpileData == null) return false;

        int localX = worldPos.X % 32;
        int localY = worldPos.Y % 32;
        int cellIndex = localY * 32 + localX;

        return stockpileData.GetZoneAtCell(cellIndex) == zone.ZoneId;
    }

    private Rectangle CreateRectangle(Point p1, Point p2)
    {
        int x = Math.Min(p1.X, p2.X);
        int y = Math.Min(p1.Y, p2.Y);
        int w = Math.Abs(p2.X - p1.X) + 1;
        int h = Math.Abs(p2.Y - p1.Y) + 1;
        return new Rectangle(x, y, w, h);
    }

    private (int x, int y) CalculateRectSize(Point p1, Point p2)
    {
        return (Math.Abs(p2.X - p1.X) + 1, Math.Abs(p2.Y - p1.Y) + 1);
    }

    private void DrawBox(ScreenSurface surface, int x, int y, int width, int height,
        Color fg, Color bg)
    {
        // Clear background
        for (int i = 0; i < width; i++)
        {
            for (int j = 0; j < height; j++)
            {
                surface.SetGlyph(x + i, y + j, ' ', fg, bg);
            }
        }

        // Draw border
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

    private string GetPriorityName(int priority)
    {
        return priority switch
        {
            0 => "Low",
            1 => "Normal",
            2 => "High",
            3 => "Critical",
            _ => "Unknown"
        };
    }

    private string GetFilterSummary(StockpileFilter filter)
    {
        if (filter.Tags.Count == 0 && filter.ItemIds.Count == 0)
            return "All Items";

        if (filter.Tags.Count > 0)
            return string.Join(", ", filter.Tags.Take(3));

        return "Custom";
    }

    #endregion

    /// <summary>
    /// Visual state for a stockpile zone.
    /// </summary>
    private class StockpileVisual
    {
        public int ZoneId { get; init; }
        public int TotalCells { get; set; }
        public int UsedCells { get; set; }
        public Color BaseColor { get; set; } = Color.Green;
    }
}
