using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Simulation.Diagnostics;
using HumanFortress.Simulation.Tiles;
using HumanFortress.Simulation.World;
using SimulationWorld = HumanFortress.Simulation.World.World;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Applies stockpile diffs during Write phase.
/// Per STOCKPILE_SPEC.md section 7: deterministic merge and atomic application.
/// </summary>
internal sealed class StockpileDiffApplicator
{
    internal static Action<string>? LogCallback { get; set; }

    private readonly SimulationWorld _world;
    private readonly StockpileManager _zoneManager;

    private StockpileDiffApplicator(SimulationWorld world)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _zoneManager = world.Stockpiles;
    }

    internal static void ApplyAll(SimulationWorld world, IReadOnlyList<StockpileDiff> diffs)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(diffs);

        if (diffs.Count == 0)
            return;

        var sortedDiffs = diffs.ToList();
        sortedDiffs.Sort(StockpileDiff.CompareDeterministic);

        var applicator = new StockpileDiffApplicator(world);
        foreach (var diff in sortedDiffs)
        {
            try
            {
                applicator.ApplyWorldDiff(world, diff);
            }
            catch (Exception ex)
            {
                SimulationDiagnostics.Error(
                    LogCallback,
                    "Simulation.StockpileDiff",
                    $"[StockpileDiffApplicator] Failed to apply diff {diff.Op}: {ex.Message}",
                    ex);
            }
        }
    }

    /// <summary>
    /// Apply a single chunk-scoped stockpile diff after world-scoped diffs were routed.
    /// </summary>
    private void ApplyDiff(Chunk chunk, StockpileDiff diff)
    {
        var stockpileData = chunk.GetStockpileData();
        if (stockpileData == null)
        {
            chunk.EnsureStockpileData();
            stockpileData = chunk.GetStockpileData();
            if (stockpileData == null)
                return;
        }

        switch (diff.Op)
        {
            case StockpileDiffOp.AddCells:
                HandleAddCells(stockpileData, diff);
                break;

            case StockpileDiffOp.RemoveCells:
                HandleRemoveCells(stockpileData, diff);
                break;

            case StockpileDiffOp.ReserveSlot:
                HandleReserveSlot(stockpileData, diff);
                break;

            case StockpileDiffOp.ReleaseSlot:
                HandleReleaseSlot(stockpileData, diff);
                break;

            case StockpileDiffOp.PlaceItem:
                HandlePlaceItem(chunk, stockpileData, diff);
                break;

            case StockpileDiffOp.RemoveItem:
                HandleRemoveItem(chunk, stockpileData, diff);
                break;

            case StockpileDiffOp.UpdateFilter:
                HandleUpdateFilter(diff);
                break;
        }
    }

    private void ApplyWorldDiff(SimulationWorld world, StockpileDiff diff)
    {
        if (diff.Op == StockpileDiffOp.CreateZone && diff.Data is StockpileCreateZoneData request)
        {
            HandleCreateZone(world, request);
            return;
        }

        if (diff.Op == StockpileDiffOp.DeleteZone)
        {
            HandleDeleteZone(world, diff);
            return;
        }

        var chunk = world.GetChunk(diff.TargetChunk);
        if (chunk == null)
            return;

        ApplyDiff(chunk, diff);
    }

    private void HandleCreateZone(SimulationWorld world, StockpileCreateZoneData request)
    {
        var acceptedCells = new Dictionary<ChunkKey, List<int>>();

        foreach (var (chunkKey, requestedCells) in request.CellsByChunk
            .OrderBy(static entry => entry.Key.Z)
            .ThenBy(static entry => entry.Key.ChunkY)
            .ThenBy(static entry => entry.Key.ChunkX))
        {
            var chunk = world.GetChunk(chunkKey);
            if (chunk == null)
                continue;

            chunk.EnsureStockpileData();
            var stockpileData = chunk.GetStockpileData();
            if (stockpileData == null)
                continue;

            foreach (int cellIndex in requestedCells.Distinct().OrderBy(static cell => cell))
            {
                if (!IsAcceptableStockpileCell(chunk, cellIndex))
                    continue;

                if (stockpileData.GetZoneAtCell(cellIndex) != 0)
                    continue;

                if (!acceptedCells.TryGetValue(chunkKey, out var cells))
                {
                    cells = new List<int>();
                    acceptedCells.Add(chunkKey, cells);
                }

                cells.Add(cellIndex);
            }
        }

        int acceptedCount = acceptedCells.Values.Sum(static cells => cells.Count);
        if (acceptedCount == 0)
            return;

        var homeChunk = acceptedCells.ContainsKey(request.HomeChunk)
            ? request.HomeChunk
            : acceptedCells.Keys
                .OrderBy(static key => key.Z)
                .ThenBy(static key => key.ChunkY)
                .ThenBy(static key => key.ChunkX)
                .First();
        int zoneId = _zoneManager.CreateZone(request.Name, homeChunk, request.CreatedTick);

        foreach (var (chunkKey, cells) in acceptedCells
            .OrderBy(static entry => entry.Key.Z)
            .ThenBy(static entry => entry.Key.ChunkY)
            .ThenBy(static entry => entry.Key.ChunkX)
            .Select(static entry => (entry.Key, entry.Value)))
        {
            var chunk = world.GetChunk(chunkKey);
            if (chunk == null)
                continue;

            chunk.EnsureStockpileData();
            var stockpileData = chunk.GetStockpileData();
            stockpileData?.CreateOrUpdateShard(zoneId, chunkKey);
            stockpileData?.AddCellsToZone(zoneId, cells);
        }

        var zone = _zoneManager.GetZone(zoneId);
        if (zone != null)
        {
            zone.Filter = request.Filter;
            zone.Priority = request.ZonePriority;
            zone.UpdateMemberChunks(acceptedCells.Keys);
        }

        SimulationDiagnostics.Information(
            LogCallback,
            "Simulation.StockpileDiff",
            $"[STOCKPILE] Applied zone {zoneId} name={request.Name} cells={acceptedCount}");
    }

    private static bool IsAcceptableStockpileCell(Chunk chunk, int cellIndex)
    {
        if (cellIndex < 0 || cellIndex >= Chunk.CELLS_PER_LAYER)
            return false;

        var (localX, localY) = Chunk.IndexToLocal(cellIndex);
        return chunk.GetTile(localX, localY).Kind == TerrainKind.OpenWithFloor;
    }

    private void HandleDeleteZone(SimulationWorld world, StockpileDiff diff)
    {
        var zone = _zoneManager.GetZone(diff.ZoneId);
        if (zone == null)
            return;

        foreach (var chunkKey in zone.GetMemberChunksSnapshot())
        {
            var chunk = world.GetChunk(chunkKey);
            chunk?.GetStockpileData()?.DeleteShard(diff.ZoneId);
        }

        _zoneManager.DeleteZone(diff.ZoneId);
        SimulationDiagnostics.Information(
            LogCallback,
            "Simulation.StockpileDiff",
            $"[STOCKPILE] Deleted zone {diff.ZoneId}");
    }

    private void HandleAddCells(ChunkStockpileData stockpileData, StockpileDiff diff)
    {
        if (diff.Data is IEnumerable<int> cells)
        {
            stockpileData.AddCellsToZone(diff.ZoneId, cells);
        }
    }

    private void HandleRemoveCells(ChunkStockpileData stockpileData, StockpileDiff diff)
    {
        if (diff.Data is IEnumerable<int> cells)
        {
            stockpileData.RemoveCellsFromZone(diff.ZoneId, cells);
        }
    }

    private void HandleReserveSlot(ChunkStockpileData stockpileData, StockpileDiff diff)
    {
        stockpileData.TryReserveSlot(diff.ZoneId);
    }

    private void HandleReleaseSlot(ChunkStockpileData stockpileData, StockpileDiff diff)
    {
        stockpileData.ReleaseSlot(diff.ZoneId);
    }

    private void HandlePlaceItem(Chunk chunk, ChunkStockpileData stockpileData, StockpileDiff diff)
    {
        // Validate cell still accepts
        if (!ValidateCellAccepts(stockpileData, diff))
        {
            return;
        }

        var tags = GetItemTags(diff);

        // Update stockpile data
        stockpileData.OnItemPlaced(
            diff.ItemHandle,
            diff.CellIndex,
            diff.ZoneId,
            tags
        );

        // Item position movement is owned by the Items/Transport pipeline.
    }

    private void HandleRemoveItem(Chunk chunk, ChunkStockpileData stockpileData, StockpileDiff diff)
    {
        var tags = GetItemTags(diff);

        // Update stockpile data
        stockpileData.OnItemRemoved(
            diff.ItemHandle,
            diff.CellIndex,
            diff.ZoneId,
            tags
        );

        // Item position movement is owned by the Items/Transport pipeline.
    }

    private void HandleUpdateFilter(StockpileDiff diff)
    {
        if (diff.Data is StockpileFilter filter)
        {
            _zoneManager.UpdateZone(diff.ZoneId, zone =>
            {
                zone.Filter = filter;
            });
        }
    }

    #region Validation Helpers

    private bool ValidateCellAccepts(ChunkStockpileData stockpileData, StockpileDiff diff)
    {
        var zoneId = stockpileData.GetZoneAtCell(diff.CellIndex);
        if (zoneId != diff.ZoneId)
            return false;

        var zone = _zoneManager.GetZone(diff.ZoneId);
        if (zone == null)
            return false;

        return TryProjectItem(diff.ItemHandle, out var stack) && zone.Filter.Accepts(stack);
    }

    private List<string> GetItemTags(StockpileDiff diff)
    {
        if (diff.Data is StockpileItemIndexData itemData)
        {
            return itemData.Stack.Tags
                .OrderBy(static tag => tag, StringComparer.Ordinal)
                .ToList();
        }

        return TryProjectItem(diff.ItemHandle, out var stack)
            ? stack.Tags.OrderBy(static tag => tag, StringComparer.Ordinal).ToList()
            : new List<string>();
    }

    private bool TryProjectItem(ulong itemHandle, out ItemStackRef stack)
    {
        var item = _world.Items.GetInstanceByEntityKey(itemHandle);
        if (item == null)
        {
            stack = new ItemStackRef(itemHandle);
            return false;
        }

        var definition = _world.Items.GetDefinition(item.DefinitionId);
        stack = StockpileItemProjection.FromItem(item, definition);
        return true;
    }

    #endregion
}
