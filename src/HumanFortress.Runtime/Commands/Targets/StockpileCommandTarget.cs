using HumanFortress.Runtime.Content;
using HumanFortress.Simulation.World;
using HumanFortress.Simulation.Stockpile;
using SadRogue.Primitives;

namespace HumanFortress.Runtime.Commands;

internal sealed partial class StockpileCommandTarget : IStockpileCommandTarget
{
    private readonly World _world;
    private readonly StockpileDiffLog _stockpileDiffLog;
    private readonly FortressRuntimeStockpilePresetCatalog _presetCatalog;
    private readonly Action<string>? _log;

    internal StockpileCommandTarget(
        World world,
        StockpileDiffLog stockpileDiffLog,
        FortressRuntimeStockpilePresetCatalog? presetCatalog = null,
        Action<string>? log = null)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _stockpileDiffLog = stockpileDiffLog ?? throw new ArgumentNullException(nameof(stockpileDiffLog));
        _presetCatalog = presetCatalog ?? FortressRuntimeStockpilePresetCatalog.Empty;
        _log = log;
    }

    bool IStockpileCommandTarget.CreateStockpile(Rectangle worldRect, int z, string presetId, ulong currentTick)
    {
        string normalizedPreset = string.IsNullOrWhiteSpace(presetId) ? "all" : presetId;
        var cellsByChunk = new Dictionary<ChunkKey, List<int>>();
        int skippedInvalid = 0;
        int skippedOverlap = 0;

        for (int wx = worldRect.X; wx < worldRect.X + worldRect.Width; wx++)
        {
            for (int wy = worldRect.Y; wy < worldRect.Y + worldRect.Height; wy++)
            {
                TryCollectCell(wx, wy, z, cellsByChunk, ref skippedInvalid, ref skippedOverlap);
            }
        }

        int totalCells = cellsByChunk.Values.Sum(static list => list.Count);
        if (totalCells == 0)
        {
            _log?.Invoke($"[STOCKPILE] Skipped empty stockpile command preset={normalizedPreset} rect=({worldRect.X},{worldRect.Y},{worldRect.Width}x{worldRect.Height}) z={z} invalid={skippedInvalid} overlap={skippedOverlap}");
            return false;
        }

        var homeChunk = GetHomeChunk(cellsByChunk.Keys);
        var preset = _presetCatalog.Resolve(normalizedPreset);
        string zoneName = BuildZoneName(preset.Id, _stockpileDiffLog.PendingCreateZoneCount);
        _stockpileDiffLog.AddCreateZone(
            zoneName,
            homeChunk,
            cellsByChunk.ToDictionary(
                static entry => entry.Key,
                static entry => (IReadOnlyList<int>)entry.Value.ToArray()),
            currentTick,
            priority: 50,
            systemId: "Runtime.StockpileCommand",
            filter: preset.CreateFilter(),
            zonePriority: preset.Priority);
        _log?.Invoke($"[STOCKPILE] Queued create preset={preset.Id} cells={totalCells} invalid={skippedInvalid} overlap={skippedOverlap}");
        return true;
    }

    bool IStockpileCommandTarget.DeleteStockpile(int zoneId, ulong currentTick)
    {
        if (zoneId <= 0)
        {
            _log?.Invoke($"[STOCKPILE] Skipped delete for invalid zone {zoneId}");
            return false;
        }

        _stockpileDiffLog.AddDeleteZone(
            zoneId,
            priority: 50,
            systemId: "Runtime.StockpileCommand");
        _log?.Invoke($"[STOCKPILE] Queued delete zone {zoneId} tick={currentTick}");
        return true;
    }

}
