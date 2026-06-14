using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HumanFortress.Simulation.World;

namespace HumanFortress.Simulation.Stockpile;

/// <summary>
/// Single arbitration point for matching stockpile pull requests with available items.
/// Per STOCKPILE_SPEC.md section 3: Broker ensures deterministic matching and prevents ping-pong.
/// </summary>
public sealed class StockpileHaulingBroker
{
    private readonly World.World _world;
    private readonly StockpileTuning _tuning;
    private int _nextJobId = 1;
    private int _localSeq = 0;

    public StockpileHaulingBroker(World.World world, StockpileTuning tuning)
    {
        _world = world ?? throw new ArgumentNullException(nameof(world));
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
    }

    /// <summary>
    /// Main broker entry point called during Read phase.
    /// Collects requests and items, performs matching, outputs haul job diffs.
    /// </summary>
    public async Task<List<StockpileDiff>> GenerateHaulJobs(ulong currentTick)
    {
        var startTime = DateTime.UtcNow;

        // Step 1: Collect pull requests from all chunks (parallel)
        var pullRequests = await CollectPullRequests();

        // Step 2: Collect available items from all chunks (parallel)
        var availableItems = await CollectAvailableItems(currentTick);

        // Check time budget
        if ((DateTime.UtcNow - startTime).TotalMilliseconds > _tuning.BrokerTimeBudgetMs)
        {
            // Time exceeded, return empty to try next tick
            return new List<StockpileDiff>();
        }

        // Step 3: Match requests to items (deterministic)
        var haulJobs = MatchRequestsToItems(pullRequests, availableItems, currentTick);

        // Step 4: Convert to diffs
        return ConvertToHaulJobDiffs(haulJobs);
    }

    /// <summary>
    /// Collect pull requests from all active chunks.
    /// </summary>
    private async Task<List<PullRequest>> CollectPullRequests()
    {
        var tasks = new List<Task<List<PullRequest>>>();

        foreach (var chunk in _world.GetActiveChunks())
        {
            tasks.Add(Task.Run(() => GenerateChunkPullRequests(chunk)));
        }

        var results = await Task.WhenAll(tasks);
        var allRequests = results.SelectMany(r => r).ToList();

        // Sort by deterministic key for stable processing
        allRequests.Sort((a, b) =>
        {
            var cmp = b.Priority.CompareTo(a.Priority); // Higher priority first
            if (cmp != 0) return cmp;
            cmp = a.ZoneId.CompareTo(b.ZoneId);
            if (cmp != 0) return cmp;
            return a.RequestId.CompareTo(b.RequestId);
        });

        return allRequests.Take(_tuning.MaxPullRequestsPerTick).ToList();
    }

    /// <summary>
    /// Generate pull requests for a single chunk.
    /// </summary>
    private List<PullRequest> GenerateChunkPullRequests(Chunk chunk)
    {
        var requests = new List<PullRequest>();
        var stockpileData = chunk.GetStockpileData();
        if (stockpileData == null)
            return requests;

        foreach (var shard in stockpileData.GetAllShards())
        {
            var zone = GetZone(shard.ZoneId);
            if (zone == null)
                continue;

            int currentStacks = shard.UsedSlots + shard.ReservedSlots + shard.IncomingCount;

            // Check hysteresis threshold
            if (currentStacks <= zone.HysteresisLow)
            {
                int desiredStacks = Math.Min(
                    zone.TargetStacks - currentStacks,
                    _tuning.MaxPullRequestsPerZone
                );

                if (desiredStacks > 0)
                {
                    requests.Add(new PullRequest
                    {
                        ZoneId = zone.ZoneId,
                        TargetChunk = chunk.Key,
                        Filter = zone.Filter,
                        Priority = zone.Priority,
                        DesiredStacks = desiredStacks,
                        RequestId = GenerateRequestId()
                    });
                }
            }
        }

        return requests;
    }

    /// <summary>
    /// Collect available items from all chunks.
    /// </summary>
    private async Task<List<AvailableItem>> CollectAvailableItems(ulong currentTick)
    {
        var tasks = new List<Task<List<AvailableItem>>>();

        foreach (var chunk in _world.GetActiveChunks())
        {
            tasks.Add(Task.Run(() => CollectChunkItems(chunk, currentTick)));
        }

        var results = await Task.WhenAll(tasks);
        return results.SelectMany(r => r).ToList();
    }

    /// <summary>
    /// Collect available items from a single chunk.
    /// </summary>
    private List<AvailableItem> CollectChunkItems(Chunk chunk, ulong currentTick)
    {
        var items = new List<AvailableItem>();
        var stockpileData = chunk.GetStockpileData();
        if (stockpileData == null)
            return items;

        // Get loose items not in any zone
        foreach (var handle in stockpileData.GetLooseItems())
        {
            var stack = GetItemStack(handle);
            if (stack.Reserved)
                continue;

            items.Add(new AvailableItem
            {
                Handle = handle,
                Stack = stack,
                SourceChunk = chunk.Key,
                IsLoose = true
            });
        }

        // Could also check items in wrong zones here (v2 feature)

        return items;
    }

    /// <summary>
    /// Match pull requests to available items using deterministic scoring.
    /// </summary>
    private List<HaulJob> MatchRequestsToItems(
        List<PullRequest> requests,
        List<AvailableItem> items,
        ulong currentTick)
    {
        var jobs = new List<HaulJob>();
        var usedItems = new HashSet<int>();

        foreach (var request in requests)
        {
            // Filter candidates
            var candidates = items
                .Where(i => !usedItems.Contains(i.Handle) &&
                           request.Filter.Accepts(i.Stack) &&
                           IsDwellTimeExpired(i.Stack, currentTick) &&
                           !IsSticky(i.Stack, request.ZoneId))
                .ToList();

            // Score and sort candidates
            var scored = candidates
                .Select(item => new
                {
                    Item = item,
                    Score = CalculateIntegerScore(item, request, currentTick)
                })
                .Where(s => s.Score > 0)
                .OrderByDescending(s => s.Score)
                .ThenBy(s => GetDistance(s.Item.SourceChunk, request.TargetChunk))
                .ThenBy(s => request.ZoneId)
                .ThenBy(s => s.Item.Handle)
                .ThenBy(s => request.TargetChunk.GetHashCode())
                .ToList();

            // Create jobs up to desired stacks
            int assigned = 0;
            foreach (var scoredItem in scored)
            {
                if (assigned >= request.DesiredStacks)
                    break;

                var job = new HaulJob
                {
                    JobId = _nextJobId++,
                    ItemHandle = scoredItem.Item.Handle,
                    Quantity = 1, // v1: single stack
                    SourceChunk = scoredItem.Item.SourceChunk,
                    DestChunk = request.TargetChunk,
                    ZoneId = request.ZoneId,
                    Priority = request.Priority
                };

                jobs.Add(job);
                usedItems.Add(scoredItem.Item.Handle);
                assigned++;
            }
        }

        // Limit to budget
        return jobs.Take(_tuning.MaxHaulJobsPerTick).ToList();
    }

    /// <summary>
    /// Calculate integer score for item-zone matching.
    /// Per STOCKPILE_SPEC section 4.
    /// </summary>
    private int CalculateIntegerScore(
        AvailableItem item,
        PullRequest request,
        ulong currentTick)
    {
        int basePriority = request.Priority * _tuning.PriorityMultiplier;
        int distance = GetDistance(item.SourceChunk, request.TargetChunk);
        int distanceCost = distance * _tuning.DistanceCostPerTile;
        int stickiness = (item.Stack.LastZoneId == request.ZoneId) ? _tuning.StickinessPenalty : 0;

        int dwellTicks = (int)(currentTick - item.Stack.PlacedTick);
        int dwellBonus = Math.Min(_tuning.DwellBonusMax, dwellTicks / 50);

        return basePriority - distanceCost + stickiness + dwellBonus;
    }

    /// <summary>
    /// Convert matched haul jobs to diffs.
    /// </summary>
    private List<StockpileDiff> ConvertToHaulJobDiffs(List<HaulJob> jobs)
    {
        var diffs = new List<StockpileDiff>();

        foreach (var job in jobs)
        {
            // CreateHaulJob diff (includes atomic reservation)
            diffs.Add(new StockpileDiff
            {
                Op = StockpileDiffOp.CreateHaulJob,
                TargetChunk = job.DestChunk,
                ZoneId = job.ZoneId,
                ItemHandle = job.ItemHandle,
                Quantity = job.Quantity,
                Priority = job.Priority,
                SystemId = "StockpileHaulingBroker",
                LocalSeq = _localSeq++,
                JobId = job.JobId,
                Data = job
            });
        }

        return diffs;
    }

    #region Helper Methods

    private bool IsDwellTimeExpired(ItemStackRef stack, ulong currentTick)
    {
        return (currentTick - stack.PlacedTick) >= _tuning.DwellTicksMin;
    }

    private bool IsSticky(ItemStackRef stack, int targetZoneId)
    {
        return stack.LastZoneId != 0 &&
               stack.LastZoneId != targetZoneId;
    }

    private int GetDistance(ChunkKey from, ChunkKey to)
    {
        // Manhattan distance in chunks * 32 (tiles per chunk)
        int dx = Math.Abs(from.ChunkX - to.ChunkX);
        int dy = Math.Abs(from.ChunkY - to.ChunkY);
        int dz = Math.Abs(from.Z - to.Z);
        return (dx + dy + dz) * Chunk.SIZE_XY;
    }

    private int GenerateRequestId()
    {
        return _localSeq++;
    }

    // TODO: These need proper integration with zone/item systems
    private StockpileZone? GetZone(int zoneId)
    {
        // Placeholder - needs integration with zone manager
        return null;
    }

    private ItemStackRef GetItemStack(int handle)
    {
        // Placeholder - needs integration with item system
        return new ItemStackRef(handle);
    }

    #endregion

    #region Internal Types

    /// <summary>
    /// Pull request from a zone.
    /// </summary>
    private class PullRequest
    {
        public int ZoneId { get; init; }
        public ChunkKey TargetChunk { get; init; }
        public StockpileFilter Filter { get; init; } = new();
        public int Priority { get; init; }
        public int DesiredStacks { get; init; }
        public int RequestId { get; init; }
    }

    /// <summary>
    /// Available item for hauling.
    /// </summary>
    private class AvailableItem
    {
        public int Handle { get; init; }
        public ItemStackRef Stack { get; init; }
        public ChunkKey SourceChunk { get; init; }
        public bool IsLoose { get; init; }
    }

    /// <summary>
    /// Haul job to be created.
    /// </summary>
    private class HaulJob
    {
        public int JobId { get; init; }
        public int ItemHandle { get; init; }
        public int Quantity { get; init; }
        public ChunkKey SourceChunk { get; init; }
        public ChunkKey DestChunk { get; init; }
        public int ZoneId { get; init; }
        public int Priority { get; init; }
    }

    #endregion
}

/// <summary>
/// Tuning parameters for stockpile system.
/// Loaded from tuning.stockpile.json per STOCKPILE_SPEC.md.
/// </summary>
public sealed class StockpileTuning
{
    // Budgets
    public int MaxZonesPerChunk { get; set; } = 32;
    public int MaxCellsScannedPerTick { get; set; } = 2048;
    public int MaxPullRequestsPerZone { get; set; } = 20;
    public int MaxPullRequestsPerTick { get; set; } = 100;
    public int MaxHaulJobsPerTick { get; set; } = 50;
    public int BrokerTimeBudgetMs { get; set; } = 2;

    // Thresholds
    public float DefaultHysteresisLow { get; set; } = 0.7f;
    public float DefaultHysteresisHigh { get; set; } = 0.9f;
    public ulong DwellTicksMin { get; set; } = 2000;
    public int StickinessPenalty { get; set; } = -5000;

    // Scoring
    public int PriorityMultiplier { get; set; } = 10000;
    public int DistanceCostPerTile { get; set; } = 10;
    public int DwellBonusMax { get; set; } = 100;
}
