namespace HumanFortress.Simulation.Placeables;

internal sealed class WorkshopDiffLog
{
    private readonly List<WorkshopDiff> _ops = new();
    private readonly object _lock = new();
    private int _localSeq;

    internal void AddRecipe(
        Guid workshopGuid,
        string recipeId,
        ulong currentTick,
        int priority,
        string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recipeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new WorkshopDiff
        {
            Op = WorkshopDiffOp.AddRecipe,
            WorkshopGuid = workshopGuid,
            RecipeId = recipeId,
            EntryId = null,
            IntValue = 0,
            MoveOffset = 0,
            BoolValue = null,
            CurrentTick = currentTick,
            Priority = priority,
            SystemId = systemId
        });
    }

    internal void RemoveEntry(Guid workshopGuid, Guid entryId, int priority, string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new WorkshopDiff
        {
            Op = WorkshopDiffOp.RemoveEntry,
            WorkshopGuid = workshopGuid,
            RecipeId = string.Empty,
            EntryId = entryId,
            IntValue = 0,
            MoveOffset = 0,
            BoolValue = null,
            CurrentTick = 0,
            Priority = priority,
            SystemId = systemId
        });
    }

    internal void MoveEntry(Guid workshopGuid, Guid entryId, int moveOffset, int priority, string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new WorkshopDiff
        {
            Op = WorkshopDiffOp.MoveEntry,
            WorkshopGuid = workshopGuid,
            RecipeId = string.Empty,
            EntryId = entryId,
            IntValue = 0,
            MoveOffset = moveOffset,
            BoolValue = null,
            CurrentTick = 0,
            Priority = priority,
            SystemId = systemId
        });
    }

    internal void ClearQueue(Guid workshopGuid, int priority, string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        AddSimple(workshopGuid, WorkshopDiffOp.ClearQueue, priority, systemId);
    }

    internal void SetWorkerSlots(Guid workshopGuid, int workerSlots, int priority, string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        Add(new WorkshopDiff
        {
            Op = WorkshopDiffOp.SetWorkerSlots,
            WorkshopGuid = workshopGuid,
            RecipeId = string.Empty,
            EntryId = null,
            IntValue = workerSlots,
            MoveOffset = 0,
            BoolValue = null,
            CurrentTick = 0,
            Priority = priority,
            SystemId = systemId
        });
    }

    internal void SetAutoStockpile(Guid workshopGuid, bool? value, int priority, string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        AddBool(workshopGuid, WorkshopDiffOp.SetAutoStockpile, value, priority, systemId);
    }

    internal void SetAutoSupply(Guid workshopGuid, bool? value, int priority, string systemId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(systemId);

        AddBool(workshopGuid, WorkshopDiffOp.SetAutoSupply, value, priority, systemId);
    }

    internal IReadOnlyList<WorkshopDiff> MergeAndSort()
    {
        lock (_lock)
        {
            _ops.Sort((a, b) => a.GetSortKey().CompareTo(b.GetSortKey()));
            return _ops.ToList();
        }
    }

    internal void Clear()
    {
        lock (_lock)
        {
            _ops.Clear();
            _localSeq = 0;
        }
    }

    private void AddSimple(Guid workshopGuid, WorkshopDiffOp op, int priority, string systemId)
    {
        Add(new WorkshopDiff
        {
            Op = op,
            WorkshopGuid = workshopGuid,
            RecipeId = string.Empty,
            EntryId = null,
            IntValue = 0,
            MoveOffset = 0,
            BoolValue = null,
            CurrentTick = 0,
            Priority = priority,
            SystemId = systemId
        });
    }

    private void AddBool(Guid workshopGuid, WorkshopDiffOp op, bool? value, int priority, string systemId)
    {
        Add(new WorkshopDiff
        {
            Op = op,
            WorkshopGuid = workshopGuid,
            RecipeId = string.Empty,
            EntryId = null,
            IntValue = 0,
            MoveOffset = 0,
            BoolValue = value,
            CurrentTick = 0,
            Priority = priority,
            SystemId = systemId
        });
    }

    private void Add(WorkshopDiff diff)
    {
        lock (_lock)
        {
            _ops.Add(diff with { LocalSeq = _localSeq++ });
        }
    }
}
