namespace HumanFortress.Simulation.Items;

internal enum ItemMutationStatus
{
    NoChange,
    Applied,
    Partial,
    Rejected
}

internal readonly record struct ItemStackTransfer(
    Guid SourceItemId,
    Guid TargetItemId,
    int Quantity,
    bool SourceRemoved);

internal readonly record struct ItemStackMergeResult(
    ItemMutationStatus Status,
    int TransferredQuantity,
    int RemovedInstanceCount,
    IReadOnlyList<ItemStackTransfer> Transfers,
    string Reason)
{
    internal bool Changed => TransferredQuantity > 0 || RemovedInstanceCount > 0;

    internal static ItemStackMergeResult NoChange(string reason)
    {
        return new ItemStackMergeResult(
            ItemMutationStatus.NoChange,
            0,
            0,
            Array.Empty<ItemStackTransfer>(),
            reason);
    }
}

internal readonly record struct ItemStackSplitResult(
    ItemMutationStatus Status,
    Guid SourceItemId,
    Guid? NewItemId,
    int RequestedQuantity,
    int AppliedQuantity,
    string Reason)
{
    internal bool Success => Status == ItemMutationStatus.Applied;

    internal static ItemStackSplitResult Rejected(Guid sourceItemId, int requestedQuantity, string reason)
    {
        return new ItemStackSplitResult(
            ItemMutationStatus.Rejected,
            sourceItemId,
            null,
            requestedQuantity,
            0,
            reason);
    }
}

internal readonly record struct ItemQuantityRemovalResult(
    ItemMutationStatus Status,
    Guid ItemId,
    int RequestedQuantity,
    int AppliedQuantity,
    bool InstanceRemoved,
    string Reason)
{
    internal bool Changed => AppliedQuantity > 0;

    internal static ItemQuantityRemovalResult Rejected(Guid itemId, int requestedQuantity, string reason)
    {
        return new ItemQuantityRemovalResult(
            ItemMutationStatus.Rejected,
            itemId,
            requestedQuantity,
            0,
            false,
            reason);
    }
}

internal readonly record struct ItemSpawnTransfer(
    Guid ItemId,
    int Quantity,
    bool Created);

internal readonly record struct ItemSpawnResult(
    ItemMutationStatus Status,
    int RequestedQuantity,
    int AppliedQuantity,
    IReadOnlyList<ItemSpawnTransfer> Transfers,
    string Reason)
{
    internal bool Success => Status == ItemMutationStatus.Applied;

    internal Guid? PrimaryItemId => Transfers.Count == 0 ? null : Transfers[0].ItemId;

    internal static ItemSpawnResult Rejected(int requestedQuantity, string reason)
    {
        return new ItemSpawnResult(
            ItemMutationStatus.Rejected,
            requestedQuantity,
            0,
            Array.Empty<ItemSpawnTransfer>(),
            reason);
    }
}
