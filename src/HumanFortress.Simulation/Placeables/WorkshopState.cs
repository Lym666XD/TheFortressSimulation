using System;
using System.Collections.Generic;
using System.Linq;
using HumanFortress.Contracts.Simulation.Save;
using HumanFortress.Core.Random;

namespace HumanFortress.Simulation.Placeables;

/// <summary>
/// Runtime state for a workshop placeable. Tracks queue, worker slots, and automation toggles.
/// </summary>
internal sealed class WorkshopState
{
    private const ulong WorkshopQueueEntryGuidScope = 0x574F524B51454E54UL;

    private readonly List<CraftQueueEntry> _queue = new();
    private ulong _nextEntrySequence;

    internal WorkshopState()
    {
        AllowedWorkers = 1;
        MaxWorkers = 1;
    }

    internal IReadOnlyList<CraftQueueEntry> Queue => _queue;
    internal bool AutoRequestMaterials { get; set; } = true;
    internal bool AutoStockpileOutputs { get; set; } = true;
    internal int AllowedWorkers { get; private set; }
    internal int MaxWorkers { get; private set; }
    internal int ActiveJobs { get; private set; }
    internal ulong NextEntrySequence => _nextEntrySequence;

    internal static WorkshopState RestoreSnapshot(WorldSaveWorkshopPayloadData payload)
    {
        var state = new WorkshopState
        {
            AutoRequestMaterials = payload.AutoRequestMaterials,
            AutoStockpileOutputs = payload.AutoStockpileOutputs,
            AllowedWorkers = payload.AllowedWorkers,
            MaxWorkers = payload.MaxWorkers,
            ActiveJobs = payload.ActiveJobs,
            _nextEntrySequence = payload.NextEntrySequence
        };

        foreach (var entryPayload in payload.Queue ?? Array.Empty<WorldSaveWorkshopQueueEntryPayloadData>())
        {
            state._queue.Add(new CraftQueueEntry(
                entryPayload.EntryId,
                entryPayload.RecipeId,
                entryPayload.DisplayName)
            {
                Status = (CraftQueueStatus)entryPayload.Status,
                HasPendingRequests = entryPayload.HasPendingRequests,
                LastRequestTick = entryPayload.LastRequestTick,
                ActiveWorkerId = entryPayload.ActiveWorkerId,
                IsScheduled = entryPayload.IsScheduled,
                BlockingReason = entryPayload.BlockingReason
            });
        }

        return state;
    }

    internal void ConfigureWorkers(int defaultAllowed, int maxWorkers)
    {
        MaxWorkers = Math.Max(1, maxWorkers);
        AllowedWorkers = Math.Clamp(defaultAllowed, 1, MaxWorkers);
    }

    internal void SetAllowedWorkers(int value)
    {
        AllowedWorkers = Math.Clamp(value, 1, MaxWorkers);
    }

    internal void RegisterJobStart()
    {
        ActiveJobs = Math.Clamp(ActiveJobs + 1, 0, MaxWorkers);
    }

    internal void RegisterJobComplete()
    {
        ActiveJobs = Math.Max(0, ActiveJobs - 1);
    }

    internal void ResetActiveJobs()
    {
        ActiveJobs = 0;
    }

    internal CraftQueueEntry AddEntry(string recipeId, string recipeName, Guid workshopGuid, ulong currentTick)
    {
        var sequence = ++_nextEntrySequence;
        var entryId = DeterministicGuidGenerator.GenerateFromGuid(WorkshopQueueEntryGuidScope ^ currentTick, workshopGuid, sequence);
        var entry = new CraftQueueEntry(entryId, recipeId, recipeName);
        _queue.Add(entry);
        return entry;
    }

    internal bool RemoveEntry(Guid entryId)
    {
        int idx = _queue.FindIndex(e => e.EntryId == entryId);
        if (idx >= 0)
        {
            _queue.RemoveAt(idx);
            return true;
        }
        return false;
    }

    internal bool MoveEntry(Guid entryId, int delta)
    {
        int idx = _queue.FindIndex(e => e.EntryId == entryId);
        if (idx < 0) return false;
        int target = Math.Clamp(idx + delta, 0, _queue.Count - 1);
        if (target == idx) return false;
        var entry = _queue[idx];
        _queue.RemoveAt(idx);
        _queue.Insert(target, entry);
        return true;
    }

    internal void ClearQueue() => _queue.Clear();

    internal CraftQueueEntry? GetEntry(Guid entryId) => _queue.FirstOrDefault(e => e.EntryId == entryId);
}

internal enum CraftQueueStatus
{
    Pending,
    AwaitingMaterials,
    Scheduled,
    InProgress
}

internal sealed class CraftQueueEntry
{
    internal CraftQueueEntry(Guid entryId, string recipeId, string recipeName)
    {
        EntryId = entryId;
        RecipeId = recipeId;
        DisplayName = recipeName;
    }

    internal Guid EntryId { get; }
    internal string RecipeId { get; }
    internal string DisplayName { get; }
    internal CraftQueueStatus Status { get; set; } = CraftQueueStatus.Pending;
    internal bool HasPendingRequests { get; set; }
    internal ulong LastRequestTick { get; set; }
    internal Guid? ActiveWorkerId { get; set; }
    internal bool IsScheduled { get; set; }
    internal string? BlockingReason { get; set; }
}
