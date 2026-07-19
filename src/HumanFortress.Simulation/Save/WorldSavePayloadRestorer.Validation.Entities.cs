using HumanFortress.Contracts.Simulation.Save;

namespace HumanFortress.Simulation.Save;

internal static partial class WorldSavePayloadRestorer
{
    private static void ValidateSupportedItemSlice(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.Items == null)
            return;

        var creatureIds = payload.Creatures == null
            ? new HashSet<Guid>()
            : payload.Creatures.Select(static creature => creature.Guid).ToHashSet();
        var itemIds = payload.Items.Select(static item => item.Guid).ToHashSet();

        for (var i = 0; i < payload.Items.Length; i++)
        {
            var item = payload.Items[i];
            ValidateInstalledItemLocation(item.InstalledAt, payload, i, issues);
            ValidateContainedItemLocation(
                item.ContainedBy,
                item.Guid,
                itemIds,
                i,
                issues);
            ValidateCreatureOwnedItemLocation(
                item.CarriedBy,
                "carrier",
                creatureIds,
                i,
                issues);
            ValidateCreatureOwnedItemLocation(
                item.EquippedBy,
                "equipped",
                creatureIds,
                i,
                issues);

            ValidateItemReservationTokens(item, creatureIds, i, issues);
        }

        ValidateContainedItemGraph(payload.Items, issues);
    }

    private static void ValidateInstalledItemLocation(
        WorldSavePlacementData? placement,
        WorldSavePayloadData payload,
        int itemIndex,
        ICollection<string> issues)
    {
        if (!placement.HasValue)
            return;

        var value = placement.Value;
        var prefix = $"World item payload[{itemIndex}] installed";
        ValidateWorldPoint(value.AnchorWorld, $"{prefix} anchor", payload, issues);
        ValidateWorldZ(value.Z, $"{prefix} z", payload, issues);
        if (value.Rotation is < 0 or > 3)
        {
            issues.Add($"{prefix} rotation {value.Rotation} is outside the supported range.");
        }
    }

    private static void ValidateItemReservationTokens(
        WorldSaveItemPayloadData item,
        ISet<Guid> creatureIds,
        int itemIndex,
        ICollection<string> issues)
    {
        if (item.ReservationTokens == null)
        {
            issues.Add($"World item payload[{itemIndex}] reservation token payload is missing.");
            return;
        }

        var totalReserved = 0;
        var seenTokens = new HashSet<(Guid JobGuid, Guid? ClaimantCreatureGuid, string ReservationType)>();
        for (var i = 0; i < item.ReservationTokens.Length; i++)
        {
            var token = item.ReservationTokens[i];
            var prefix = $"World item payload[{itemIndex}] reservation token[{i}]";
            if (token.JobGuid == Guid.Empty)
            {
                issues.Add($"{prefix} has an empty job guid.");
            }

            if (token.ReservedCount <= 0)
            {
                issues.Add($"{prefix} has non-positive reserved count {token.ReservedCount}.");
            }
            else
            {
                totalReserved += token.ReservedCount;
            }

            if (string.IsNullOrWhiteSpace(token.ReservationType))
            {
                issues.Add($"{prefix} has a blank reservation type.");
            }
            else if (!seenTokens.Add((token.JobGuid, token.ClaimantCreatureGuid, token.ReservationType)))
            {
                issues.Add($"{prefix} duplicates reservation token identity.");
            }

            if (token.ClaimantCreatureGuid.HasValue)
            {
                if (token.ClaimantCreatureGuid.Value == Guid.Empty)
                {
                    issues.Add($"{prefix} has an empty claimant creature guid.");
                }
                else if (!creatureIds.Contains(token.ClaimantCreatureGuid.Value))
                {
                    issues.Add($"{prefix} references missing claimant creature {token.ClaimantCreatureGuid.Value}.");
                }
            }
        }

        if (totalReserved > item.StackCount)
        {
            issues.Add($"World item payload[{itemIndex}] reservation tokens reserve {totalReserved} items from stack count {item.StackCount}.");
        }
    }

    private static void ValidateContainedItemLocation(
        Guid? containerId,
        Guid itemId,
        ISet<Guid> itemIds,
        int itemIndex,
        ICollection<string> issues)
    {
        if (!containerId.HasValue)
            return;

        if (containerId.Value == Guid.Empty)
        {
            issues.Add($"World item payload[{itemIndex}] has an empty containing item guid.");
            return;
        }

        if (containerId.Value == itemId)
        {
            issues.Add($"World item payload[{itemIndex}] is contained by itself.");
            return;
        }

        if (!itemIds.Contains(containerId.Value))
        {
            issues.Add($"World item payload[{itemIndex}] references missing containing item {containerId.Value}.");
        }
    }

    private static void ValidateContainedItemGraph(
        IReadOnlyList<WorldSaveItemPayloadData> items,
        ICollection<string> issues)
    {
        var containerByItem = new Dictionary<Guid, Guid>();
        foreach (var item in items)
        {
            if (item.Guid == Guid.Empty
                || !item.ContainedBy.HasValue
                || item.ContainedBy.Value == Guid.Empty
                || containerByItem.ContainsKey(item.Guid))
            {
                continue;
            }

            containerByItem.Add(item.Guid, item.ContainedBy.Value);
        }

        var checkedItems = new HashSet<Guid>();
        foreach (var start in containerByItem
            .OrderBy(static entry => entry.Key)
            .Select(static entry => entry.Key))
        {
            if (checkedItems.Contains(start))
                continue;

            var path = new HashSet<Guid>();
            var current = start;
            while (containerByItem.TryGetValue(current, out var next))
            {
                if (!path.Add(current))
                {
                    issues.Add($"World item payload contains a contained-item cycle involving item {current}.");
                    break;
                }

                if (checkedItems.Contains(current))
                    break;

                current = next;
            }

            foreach (var itemId in path)
            {
                checkedItems.Add(itemId);
            }
        }
    }

    private static void ValidateCreatureOwnedItemLocation(
        Guid? creatureId,
        string roleName,
        ISet<Guid> creatureIds,
        int itemIndex,
        ICollection<string> issues)
    {
        if (!creatureId.HasValue)
            return;

        if (creatureId.Value == Guid.Empty)
        {
            issues.Add($"World item payload[{itemIndex}] has an empty {roleName} creature guid.");
            return;
        }

        if (!creatureIds.Contains(creatureId.Value))
        {
            issues.Add($"World item payload[{itemIndex}] references missing {roleName} creature {creatureId.Value}.");
        }
    }

    private static void ValidateReservationReferences(
        WorldSavePayloadData payload,
        ICollection<string> issues)
    {
        if (payload.Items != null && payload.ItemReservations != null)
        {
            var itemIds = payload.Items.Select(item => item.Guid).ToHashSet();
            for (var i = 0; i < payload.ItemReservations.Length; i++)
            {
                if (!itemIds.Contains(payload.ItemReservations[i].ItemId))
                {
                    issues.Add($"World item reservation payload[{i}] references missing item {payload.ItemReservations[i].ItemId}.");
                }
            }
        }

        if (payload.Creatures != null && payload.CreatureReservations != null)
        {
            var creatureIds = payload.Creatures.Select(creature => creature.Guid).ToHashSet();
            for (var i = 0; i < payload.CreatureReservations.Length; i++)
            {
                if (!creatureIds.Contains(payload.CreatureReservations[i].WorkerId))
                {
                    issues.Add($"World creature reservation payload[{i}] references missing creature {payload.CreatureReservations[i].WorkerId}.");
                }
            }
        }
    }
}
