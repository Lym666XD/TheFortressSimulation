using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadBuilder
{
    private static WorldSavePointData ToPayloadPoint(SadRogue.Primitives.Point point)
    {
        return new WorldSavePointData(point.X, point.Y);
    }

    private static WorldSaveRectangleData ToPayloadRectangle(SadRogue.Primitives.Rectangle rectangle)
    {
        return new WorldSaveRectangleData(rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
    }

    private static WorldSaveChunkKeyData ToPayloadChunkKey(ChunkKey key)
    {
        return new WorldSaveChunkKeyData(key.ChunkX, key.ChunkY, key.Z);
    }

    private static WorldSaveStringIntData[] ToPayloadStringIntMap(IEnumerable<KeyValuePair<string, int>> values)
    {
        return values
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new WorldSaveStringIntData(entry.Key, entry.Value))
            .ToArray();
    }

    private static string[] ToSortedArray(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }
}
