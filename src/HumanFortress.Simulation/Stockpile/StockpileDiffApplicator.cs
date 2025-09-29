using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Applies stockpile diffs during Write phase.
/// Per STOCKPILE_SPEC.md section 7: deterministic merge and atomic application.
/// </summary>
public sealed class StockpileDiffApplicator
{
    private readonly StockpileManager _zoneManager;

    public StockpileDiffApplicator(StockpileManager zoneManager)
    {
        _zoneManager = zoneManager ?? throw new ArgumentNullException(nameof(zoneManager));
    }

    /// <summary>
    /// Apply a batch of stockpile diffs to a chunk.
    /// Called during Write phase only.
    /// </summary>
    public void ApplyDiffs(Chunk chunk, List<StockpileDiff> diffs)
    {
        if (diffs.Count == 0)
            return;

        // Sort diffs by deterministic key
        // Per STOCKPILE_SPEC: cellIndex → priority(desc) → op → zoneId → itemHandle → systemId → localSeq
        var sortedDiffs = diffs.OrderBy(d => d.GetSortKey()).ToList();

        // Apply each diff
        foreach (var diff in sortedDiffs)
        {
            try
            {
                ApplyDiff(chunk, diff);
            }
            catch (Exception ex)
            {
                // Log error but continue processing
                // Per CONCURRENCY_MODEL: failures should not crash the loop
                Console.WriteLine($"[StockpileDiffApplicator] Failed to apply diff {diff.Op}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Apply a single diff to a chunk.
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
            case StockpileDiffOp.CreateZone:
                HandleCreateZone(chunk, stockpileData, diff);
                break;

            case StockpileDiffOp.DeleteZone:
                HandleDeleteZone(stockpileData, diff);
                break;

            case StockpileDiffOp.AddCells:
                HandleAddCells(stockpileData, diff);
                break;

            case StockpileDiffOp.RemoveCells:
                HandleRemoveCells(stockpileData, diff);
                break;

            case StockpileDiffOp.CreateHaulJob:
                HandleCreateHaulJob(chunk, stockpileData, diff);
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

    private void HandleCreateZone(Chunk chunk, ChunkStockpileData stockpileData, StockpileDiff diff)
    {
        stockpileData.CreateOrUpdateShard(diff.ZoneId, chunk.Key);
    }

    private void HandleDeleteZone(ChunkStockpileData stockpileData, StockpileDiff diff)
    {
        stockpileData.DeleteShard(diff.ZoneId);
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

    private void HandleCreateHaulJob(Chunk chunk, ChunkStockpileData stockpileData, StockpileDiff diff)
    {
        // CreateHaulJob is atomic with reservation
        // Validate item still available
        if (!ValidateItemStillAvailable(diff.ItemHandle))
        {
            // Silent discard per STOCKPILE_SPEC
            return;
        }

        // Reserve slot atomically
        if (!stockpileData.TryReserveSlot(diff.ZoneId))
        {
            // No capacity, discard
            return;
        }

        // Mark item as reserved (TODO: integrate with item system)
        // ReserveItem(diff.ItemHandle, diff.JobId);

        // TODO: Actually create the haul job in job system
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

        // Get item tags (TODO: from item system)
        var tags = GetItemTags(diff.ItemHandle);

        // Update stockpile data
        stockpileData.OnItemPlaced(
            diff.ItemHandle,
            diff.CellIndex,
            diff.ZoneId,
            tags
        );

        // TODO: Actually place item in L5 overlay
    }

    private void HandleRemoveItem(Chunk chunk, ChunkStockpileData stockpileData, StockpileDiff diff)
    {
        // Get item tags (TODO: from item system)
        var tags = GetItemTags(diff.ItemHandle);

        // Update stockpile data
        stockpileData.OnItemRemoved(
            diff.ItemHandle,
            diff.CellIndex,
            diff.ZoneId,
            tags
        );

        // TODO: Actually remove item from L5 overlay
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

    private bool ValidateItemStillAvailable(int itemHandle)
    {
        // TODO: Check with item system
        // For now, assume valid
        return true;
    }

    private bool ValidateCellAccepts(ChunkStockpileData stockpileData, StockpileDiff diff)
    {
        var zoneId = stockpileData.GetZoneAtCell(diff.CellIndex);
        return zoneId == diff.ZoneId;
    }

    private List<string> GetItemTags(int itemHandle)
    {
        // TODO: Get from item system
        return new List<string>();
    }

    #endregion
}