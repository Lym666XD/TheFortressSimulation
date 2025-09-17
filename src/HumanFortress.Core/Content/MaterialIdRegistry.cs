namespace HumanFortress.Core.Content;

/// <summary>
/// Registry of material IDs for use in TileBase.GeoMatId.
/// These IDs map to materials and geology entries in JSON files.
/// </summary>
public static class MaterialIdRegistry
{
    // Reserved IDs 0-10 for special cases
    public const ushort None = 0;
    public const ushort GenericStone = 1;
    public const ushort GenericSoil = 2;
    public const ushort Air = 3;

    // Stone materials (10-99)
    public const ushort Granite = 10;
    public const ushort Marble = 11;
    public const ushort Basalt = 12;
    public const ushort Sandstone = 13;
    public const ushort Limestone = 14;
    public const ushort Shale = 15;
    public const ushort Slate = 16;
    public const ushort Quartzite = 17;
    public const ushort Obsidian = 18;

    // Ores (100-149)
    public const ushort IronOre = 100;
    public const ushort CopperOre = 101;
    public const ushort SilverOre = 102;
    public const ushort GoldOre = 103;
    public const ushort TinOre = 104;
    public const ushort LeadOre = 105;
    public const ushort PlatinumOre = 106;

    // Soils (150-199)
    public const ushort Dirt = 150;
    public const ushort Clay = 151;
    public const ushort Sand = 152;
    public const ushort Loam = 153;
    public const ushort Peat = 154;

    // Vegetation/Organic (200-249)
    public const ushort Grass = 200;
    public const ushort Moss = 201;
    public const ushort Fungus = 202;

    // Special terrain (250-299)
    public const ushort Ice = 250;
    public const ushort Snow = 251;
    public const ushort Magma = 252;
    public const ushort Water = 253;
    public const ushort Mud = 254;

    /// <summary>
    /// Get display info for a material ID and terrain kind combination.
    /// </summary>
    public static (char glyph, ConsoleColor foreground) GetDisplay(ushort geoMatId, TerrainKind kind)
    {
        // Handle terrain shape first
        if (kind == TerrainKind.SolidWall)
        {
            // Walls use solid blocks
            return geoMatId switch
            {
                Granite => ('█', ConsoleColor.DarkRed),
                Marble => ('█', ConsoleColor.Gray),
                Basalt => ('█', ConsoleColor.DarkGray),
                Sandstone => ('█', ConsoleColor.Yellow),
                Limestone => ('█', ConsoleColor.White),
                Shale => ('█', ConsoleColor.DarkYellow),
                IronOre => ('█', ConsoleColor.DarkCyan),
                Ice => ('█', ConsoleColor.Cyan),
                _ => ('█', ConsoleColor.Gray)
            };
        }
        else if (kind == TerrainKind.OpenWithFloor)
        {
            // Floors use different symbols
            return geoMatId switch
            {
                Granite => ('·', ConsoleColor.DarkRed),
                Marble => ('·', ConsoleColor.Gray),
                Basalt => ('·', ConsoleColor.DarkGray),
                Sandstone => ('·', ConsoleColor.Yellow),
                Limestone => ('·', ConsoleColor.White),
                Shale => ('·', ConsoleColor.DarkYellow),
                Dirt => ('.', ConsoleColor.DarkYellow),
                Grass => (',', ConsoleColor.Green),
                Sand => ('·', ConsoleColor.Yellow),
                Snow => ('*', ConsoleColor.White),
                Ice => ('~', ConsoleColor.Cyan),
                Mud => ('~', ConsoleColor.DarkYellow),
                _ => ('·', ConsoleColor.Gray)
            };
        }
        else if (kind == TerrainKind.Ramp)
        {
            // Ramps use angled symbols
            return ('▲', ConsoleColor.Gray);
        }
        else if (kind == TerrainKind.StairsUp || kind == TerrainKind.StairsDown || kind == TerrainKind.StairsUD)
        {
            // Stairs
            char stairGlyph = kind == TerrainKind.StairsUp ? '<' :
                             kind == TerrainKind.StairsDown ? '>' : 'X';
            return (stairGlyph, ConsoleColor.Yellow);
        }
        else if (kind == TerrainKind.OpenNoFloor || kind == TerrainKind.Chasm)
        {
            // Empty space
            return (' ', ConsoleColor.Black);
        }

        // Default
        return ('?', ConsoleColor.Magenta);
    }

}

/// <summary>
/// Terrain types from TILE_SPEC.md.
/// Must match HumanFortress.Simulation.Tiles.TerrainKind
/// </summary>
public enum TerrainKind : byte
{
    SolidWall = 0,
    OpenWithFloor = 1,
    OpenNoFloor = 2,
    Ramp = 3,
    StairsUp = 4,
    StairsDown = 5,
    StairsUD = 6,
    Chasm = 7
}