using HumanFortress.Simulation.Tiles;

namespace HumanFortress.Jobs.Construction;

internal static class ConstructionTargetMapper
{
    internal static bool IsTerrainTarget(string targetId)
    {
        return targetId.StartsWith("l0:", StringComparison.OrdinalIgnoreCase);
    }

    internal static TerrainKind ToTerrainKind(string targetId)
    {
        string shape = targetId;
        int idx = targetId.IndexOf(':');
        if (idx >= 0 && idx + 1 < targetId.Length)
        {
            shape = targetId.Substring(idx + 1);
        }

        if (shape.Equals("Wall", StringComparison.OrdinalIgnoreCase))
        {
            return TerrainKind.SolidWall;
        }

        if (shape.Equals("Floor", StringComparison.OrdinalIgnoreCase))
        {
            return TerrainKind.OpenWithFloor;
        }

        if (shape.Equals("Ramp", StringComparison.OrdinalIgnoreCase))
        {
            return TerrainKind.Ramp;
        }

        if (shape.Equals("Stairs", StringComparison.OrdinalIgnoreCase))
        {
            return TerrainKind.StairsUD;
        }

        return TerrainKind.OpenNoFloor;
    }
}
