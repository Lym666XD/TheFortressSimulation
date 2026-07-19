using HumanFortress.Contracts.Simulation.Save;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadRestorer
{
    private static void ValidateStringArray(
        string[]? values,
        string label,
        ICollection<string> issues)
    {
        if (values == null)
        {
            issues.Add($"{label} are missing.");
            return;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < values.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(values[i]))
            {
                issues.Add($"{label}[{i}] is blank.");
            }
            else if (!seen.Add(values[i]))
            {
                issues.Add($"{label}[{i}] duplicates '{values[i]}'.");
            }
        }
    }

    private static void ValidateWorldRectangle(
        WorldSaveRectangleData rectangle,
        string prefix,
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (rectangle.Width <= 0 || rectangle.Height <= 0)
        {
            issues.Add($"{prefix} has non-positive rectangle dimensions.");
            return;
        }

        var right = (long)rectangle.X + rectangle.Width;
        var bottom = (long)rectangle.Y + rectangle.Height;
        if (rectangle.X < 0
            || rectangle.Y < 0
            || right > payload.SizeInTiles
            || bottom > payload.SizeInTiles)
        {
            issues.Add($"{prefix} rectangle is outside world bounds.");
        }
    }

    private static void ValidateWorldPoint(
        WorldSavePointData point,
        string prefix,
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (point.X < 0
            || point.Y < 0
            || point.X >= payload.SizeInTiles
            || point.Y >= payload.SizeInTiles)
        {
            issues.Add($"{prefix} is outside world bounds.");
        }
    }

    private static void ValidateWorldChunkKey(
        WorldSaveChunkKeyData chunk,
        string prefix,
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (chunk.ChunkX < 0
            || chunk.ChunkY < 0
            || chunk.Z < 0
            || chunk.ChunkX >= payload.SizeInChunks
            || chunk.ChunkY >= payload.SizeInChunks
            || chunk.Z >= payload.MaxZ)
        {
            issues.Add($"{prefix} is outside world bounds.");
        }
    }

    private static void ValidateWorldZRange(
        int zMin,
        int zMax,
        string prefix,
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (zMin > zMax)
        {
            issues.Add($"{prefix} has zMin greater than zMax.");
            return;
        }

        ValidateWorldZ(zMin, $"{prefix} zMin", payload, issues);
        ValidateWorldZ(zMax, $"{prefix} zMax", payload, issues);
    }

    private static void ValidateWorldZ(
        int z,
        string prefix,
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (z < 0 || z >= payload.MaxZ)
        {
            issues.Add($"{prefix} is outside world z bounds.");
        }
    }
}
