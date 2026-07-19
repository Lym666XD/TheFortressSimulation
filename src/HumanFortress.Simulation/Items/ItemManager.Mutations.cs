using HumanFortress.Simulation.Stockpile;
using HumanFortress.Simulation.Jobs;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

internal sealed partial class ItemManager
{
    /// <summary>
    /// Update item position and maintain position index.
    /// </summary>
    internal bool UpdateItemPosition(Guid id, Point newPos, int newZ)
    {
        lock (_instanceLock)
        {
            if (!_instances.TryGetValue(id, out var inst))
                return false;

            IndexRemove(id, inst.Position, inst.Z);
            inst.Position = newPos;
            inst.Z = newZ;
            IndexAdd(id, newPos, newZ);
            return true;
        }
    }

    /// <summary>
    /// Merge stacks at a given world position (post-move consolidation).
    /// Compatible stacks transfer into the lowest stable GUID without exceeding
    /// the definition capacity. Unrelated position-index entries are untouched.
    /// </summary>
    internal ItemStackMergeResult MergeStacksAt(Point worldPos, int z, ulong currentTick = 0)
    {
        lock (_instanceLock)
        {
            if (_world != null
                && StockpileWorldQueries.TryGetStockpileCell(_world, worldPos.X, worldPos.Y, z, out _))
            {
                return ItemStackMergeResult.NoChange(
                    "Stockpile-cell stack identity is owned by the stockpile transaction.");
            }

            var key = KeyFor(worldPos, z);
            if (!_posIndex.TryGetValue(key, out var ids) || ids.Count <= 1)
                return ItemStackMergeResult.NoChange("The cell has fewer than two indexed items.");

            var candidates = ids
                .Distinct()
                .OrderBy(static id => id)
                .ToArray();

            var removed = 0;
            var transferred = 0;
            var transfers = new List<ItemStackTransfer>();
            for (var targetIndex = 0; targetIndex < candidates.Length; targetIndex++)
            {
                var targetId = candidates[targetIndex];
                if (!_instances.TryGetValue(targetId, out var target)
                    || !target.IsOnGround
                    || target.StackCount <= 0)
                {
                    continue;
                }

                var definition = _definitionCatalog.GetDefinition(target.DefinitionId);
                if (!ItemStackPolicy.TryGetCapacity(definition, out int capacity)
                    || target.StackCount >= capacity)
                {
                    continue;
                }

                for (var sourceIndex = targetIndex + 1;
                     sourceIndex < candidates.Length && target.StackCount < capacity;
                     sourceIndex++)
                {
                    var sourceId = candidates[sourceIndex];
                    if (!_instances.TryGetValue(sourceId, out var source)
                        || source.StackCount <= 0
                        || !CanMergeLocked(target, source, definition!, currentTick))
                    {
                        continue;
                    }

                    int transfer = Math.Min(capacity - target.StackCount, source.StackCount);
                    if (transfer <= 0)
                        continue;

                    target.StackCount += transfer;
                    source.StackCount -= transfer;
                    transferred += transfer;

                    bool sourceRemoved = source.StackCount == 0;
                    if (source.StackCount == 0)
                    {
                        RemoveInstanceLocked(sourceId, source, emit: false);
                        removed++;
                    }

                    transfers.Add(new ItemStackTransfer(
                        sourceId,
                        targetId,
                        transfer,
                        sourceRemoved));
                }
            }

            if (transferred == 0)
                return ItemStackMergeResult.NoChange("No compatible stack had available capacity.");

            Emit($"[ItemManager] MERGE: transferred={transferred} removed={removed} at ({worldPos.X},{worldPos.Y},{z})");
            return new ItemStackMergeResult(
                ItemMutationStatus.Applied,
                transferred,
                removed,
                transfers.ToArray(),
                string.Empty);
        }
    }

    /// <summary>
    /// Split a stack into a new instance with takeCount units.
    /// Reduces the original stack by takeCount and spawns a new item at the same position/Z.
    /// Returns the new item's Guid, or null if split cannot be performed.
    /// </summary>
    internal ItemStackSplitResult SplitStack(Guid sourceId, int takeCount, ulong currentTick = 0)
    {
        lock (_instanceLock)
        {
            if (!_instances.TryGetValue(sourceId, out var inst))
                return ItemStackSplitResult.Rejected(sourceId, takeCount, "Source item does not exist.");
            if (!CanSplitLocked(inst, takeCount, currentTick, hasCentralReservationAuthority: false, out string reason))
                return ItemStackSplitResult.Rejected(sourceId, takeCount, reason);

            var newGuid = CreateNextInstanceGuidLocked();
            return SplitStackLocked(sourceId, inst, takeCount, newGuid);
        }
    }

    internal ItemStackSplitResult SplitStackWithGuid(
        Guid sourceId,
        int takeCount,
        Guid newGuid,
        ulong currentTick = 0)
    {
        lock (_instanceLock)
        {
            if (newGuid == Guid.Empty)
                return ItemStackSplitResult.Rejected(sourceId, takeCount, "The new item ID is empty.");
            var identityValidation = _identityIndex.ValidateNew(newGuid);
            if (!identityValidation.Success)
            {
                return ItemStackSplitResult.Rejected(
                    sourceId,
                    takeCount,
                    identityValidation.Describe("item"));
            }
            if (!_instances.TryGetValue(sourceId, out var inst))
                return ItemStackSplitResult.Rejected(sourceId, takeCount, "Source item does not exist.");
            if (!CanSplitLocked(inst, takeCount, currentTick, hasCentralReservationAuthority: false, out string reason))
                return ItemStackSplitResult.Rejected(sourceId, takeCount, reason);

            return SplitStackLocked(sourceId, inst, takeCount, newGuid);
        }
    }

    internal ItemStackSplitResult SplitReservedStackWithGuid(
        Guid sourceId,
        int takeCount,
        Guid newGuid,
        ReservationManager.ItemToken sourceReservation,
        ReservationManager.ItemToken stagedReservation,
        ulong currentTick)
    {
        lock (_instanceLock)
        {
            var identityValidation = _identityIndex.ValidateNew(newGuid);
            if (!identityValidation.Success)
            {
                return ItemStackSplitResult.Rejected(
                    sourceId,
                    takeCount,
                    identityValidation.Describe("item"));
            }

            if (!_instances.TryGetValue(sourceId, out var instance))
                return ItemStackSplitResult.Rejected(sourceId, takeCount, "Source item does not exist.");
            if (sourceReservation.ResourceId != sourceId
                || stagedReservation.ResourceId != newGuid
                || _world == null
                || !_world.Reservations.ValidateStagedItemTransfer(
                    sourceReservation,
                    stagedReservation,
                    currentTick))
            {
                return ItemStackSplitResult.Rejected(
                    sourceId,
                    takeCount,
                    "The staged item reservation transfer is not current.");
            }

            if (!CanSplitLocked(
                    instance,
                    takeCount,
                    currentTick,
                    hasCentralReservationAuthority: true,
                    out string reason))
            {
                return ItemStackSplitResult.Rejected(sourceId, takeCount, reason);
            }

            return SplitStackLocked(sourceId, instance, takeCount, newGuid);
        }
    }

    private ItemStackSplitResult SplitStackLocked(Guid sourceId, ItemInstance inst, int takeCount, Guid newGuid)
    {
        var clone = new ItemInstance(newGuid, inst.DefinitionId, inst.Position, inst.Z, takeCount, inst.SpawnedAtTick)
        {
            MaterialId = inst.MaterialId,
            OwnerFactionId = inst.OwnerFactionId,
            OwnerCreatureGuid = inst.OwnerCreatureGuid,
            UsePolicy = inst.UsePolicy,
            Forbidden = inst.Forbidden,
            QualityTier = inst.QualityTier,
            Artifact = inst.Artifact,
            ArtifactName = inst.ArtifactName,
            ConditionState = inst.ConditionState,
            DurabilityCurrent = inst.DurabilityCurrent,
            DurabilityMax = inst.DurabilityMax,
            CraftedBy = inst.CraftedBy,
            MakerFactionId = inst.MakerFactionId,
            StyleTag = inst.StyleTag,
            Improvements = CloneImprovements(inst.Improvements),
            Perishable = ClonePerishable(inst.Perishable)
        };

        var identityClaim = _identityIndex.TryAdd(newGuid);
        if (!identityClaim.Success)
        {
            return ItemStackSplitResult.Rejected(
                sourceId,
                takeCount,
                identityClaim.Describe("item"));
        }

        _instances.Add(newGuid, clone);
        LegacyEntityIdIndexAdd(newGuid);
        IndexAdd(newGuid, clone.Position, clone.Z);
        inst.StackCount -= takeCount;
        string msg = $"[ItemManager] SPLIT: {sourceId} -> new={newGuid} take={takeCount} remain={inst.StackCount} at ({clone.Position.X},{clone.Position.Y},{clone.Z})";
        Emit(msg);
        return new ItemStackSplitResult(
            ItemMutationStatus.Applied,
            sourceId,
            newGuid,
            takeCount,
            takeCount,
            string.Empty);
    }

    internal ItemQuantityRemovalResult RemoveQuantity(Guid guid, int quantity, ulong currentTick = 0)
    {
        if (quantity <= 0)
            return ItemQuantityRemovalResult.Rejected(guid, quantity, "Removal quantity must be positive.");

        lock (_instanceLock)
        {
            if (!_instances.TryGetValue(guid, out var inst))
                return ItemQuantityRemovalResult.Rejected(guid, quantity, "Item does not exist.");
            if (_world?.Reservations.IsItemReserved(guid, currentTick) == true)
            {
                return ItemQuantityRemovalResult.Rejected(
                    guid,
                    quantity,
                    "Centrally reserved items require an ownership-aware removal transaction.");
            }

            int removedQuantity = Math.Min(inst.StackCount, quantity);
            if (removedQuantity <= 0)
                return ItemQuantityRemovalResult.Rejected(guid, quantity, "Item stack is empty.");

            inst.StackCount -= removedQuantity;
            bool instanceRemoved = inst.StackCount == 0;
            if (instanceRemoved)
                RemoveInstanceLocked(guid, inst, emit: true);

            var status = removedQuantity == quantity
                ? ItemMutationStatus.Applied
                : ItemMutationStatus.Partial;
            return new ItemQuantityRemovalResult(
                status,
                guid,
                quantity,
                removedQuantity,
                instanceRemoved,
                status == ItemMutationStatus.Partial
                    ? "The requested quantity exceeded the available stack count."
                    : string.Empty);
        }
    }

    /// <summary>
    /// Remove an item instance by GUID, updating position index accordingly.
    /// Returns true if removed.
    /// </summary>
    internal bool RemoveInstance(Guid guid)
    {
        lock (_instanceLock)
        {
            if (!_instances.TryGetValue(guid, out var inst)) return false;
            RemoveInstanceLocked(guid, inst, emit: true);
            return true;
        }
    }

    private bool CanMergeLocked(
        ItemInstance first,
        ItemInstance second,
        HumanFortress.Contracts.Simulation.Items.ItemDefinition definition,
        ulong currentTick)
    {
        if (_world != null
            && (_world.Reservations.IsItemReserved(first.Guid, currentTick)
                || _world.Reservations.IsItemReserved(second.Guid, currentTick)))
        {
            return false;
        }

        bool requiresEmpty = definition.Stack?.RequiresEmpty == true;
        return ItemStackPolicy.AreCompatible(
            first,
            second,
            definition,
            !requiresEmpty || IsContainerEmptyLocked(first.Guid),
            !requiresEmpty || IsContainerEmptyLocked(second.Guid));
    }

    private bool IsContainerEmptyLocked(Guid containerId)
    {
        return !_instances.Values.Any(item => item.ContainedBy == containerId);
    }

    private bool CanSplitLocked(
        ItemInstance item,
        int takeCount,
        ulong currentTick,
        bool hasCentralReservationAuthority,
        out string reason)
    {
        if (takeCount <= 0)
        {
            reason = "Split quantity must be positive.";
            return false;
        }

        if (item.StackCount <= takeCount)
        {
            reason = "Split quantity must be smaller than the source stack.";
            return false;
        }

        if (!item.IsOnGround)
        {
            reason = "Only ground stacks can be split without an explicit location transfer.";
            return false;
        }

        if (item.ReservationTokens.Count != 0)
        {
            reason = "Reserved stacks require an ownership-aware split transaction.";
            return false;
        }

        if (!hasCentralReservationAuthority
            && _world?.Reservations.IsItemReserved(item.Guid, currentTick) == true)
        {
            reason = "Centrally reserved stacks require an ownership-aware split transaction.";
            return false;
        }

        if (!ItemStackPolicy.TryGetCapacity(_definitionCatalog.GetDefinition(item.DefinitionId), out _))
        {
            reason = "The item definition is not stackable.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private static List<Improvement>? CloneImprovements(IReadOnlyList<Improvement>? improvements)
    {
        if (improvements == null)
            return null;

        return improvements
            .Select(improvement => new Improvement
            {
                Type = improvement.Type,
                MaterialId = improvement.MaterialId,
                QualityTier = improvement.QualityTier,
                CreatedBy = improvement.CreatedBy,
                Description = improvement.Description
            })
            .ToList();
    }

    private static PerishableState? ClonePerishable(PerishableState? perishable)
    {
        if (perishable == null)
            return null;

        return new PerishableState
        {
            CreatedAtTick = perishable.CreatedAtTick,
            FreshDurationTicks = perishable.FreshDurationTicks,
            SpoilDurationTicks = perishable.SpoilDurationTicks,
            CurrentFreshness = perishable.CurrentFreshness
        };
    }

    private void RemoveInstanceLocked(Guid guid, ItemInstance inst, bool emit)
    {
        EntityKeyIndexRemove(guid);
        IndexRemove(guid, inst.Position, inst.Z);
        _instances.Remove(guid);
        if (emit)
        {
            Emit($"[ItemManager] REMOVE: Removed item guid={guid} id={inst.DefinitionId} at ({inst.Position.X},{inst.Position.Y},{inst.Z})");
        }
    }
}
