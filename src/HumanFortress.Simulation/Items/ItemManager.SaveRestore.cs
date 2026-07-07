using HumanFortress.Contracts.Simulation.Save;
using SadRogue.Primitives;

namespace HumanFortress.Simulation.Items;

internal sealed partial class ItemManager
{
    internal IReadOnlyList<string> RestoreItemsSnapshot(IReadOnlyList<WorldSaveItemPayloadData>? items)
    {
        var issues = new List<string>();
        if (items == null)
        {
            issues.Add("World item payload is missing.");
            return issues;
        }

        var seen = new HashSet<Guid>();
        for (var i = 0; i < items.Count; i++)
        {
            ValidateItemPayload(items[i], i, seen, issues);
        }

        if (issues.Count > 0)
            return issues;

        var restored = new Dictionary<Guid, ItemInstance>(items.Count);
        foreach (var payload in items.OrderBy(item => item.Guid))
        {
            var instance = ToItemInstance(payload);
            restored.Add(instance.Guid, instance);
        }

        lock (_instanceLock)
        {
            _instances.Clear();
            _posIndex.Clear();
            foreach (var instance in restored.Values.OrderBy(item => item.Guid))
            {
                _instances[instance.Guid] = instance;
                IndexAdd(instance.Guid, instance.Position, instance.Z);
            }

            _nextInstanceSequence = (ulong)_instances.Count;
        }

        return Array.Empty<string>();
    }

    private void ValidateItemPayload(
        WorldSaveItemPayloadData payload,
        int index,
        ISet<Guid> seen,
        ICollection<string> issues)
    {
        var prefix = $"World item payload[{index}]";

        if (payload.Guid == Guid.Empty)
        {
            issues.Add($"{prefix} has an empty item guid.");
        }
        else if (!seen.Add(payload.Guid))
        {
            issues.Add($"{prefix} contains duplicate item guid {payload.Guid}.");
        }

        if (string.IsNullOrWhiteSpace(payload.DefinitionId))
        {
            issues.Add($"{prefix} has a blank definition id.");
        }

        if (payload.StackCount <= 0)
        {
            issues.Add($"{prefix} has non-positive stack count {payload.StackCount}.");
        }

        if (_world == null)
        {
            issues.Add($"{prefix} cannot validate position because the item manager has no world.");
        }
        else if (!_world.IsValidPosition(payload.Position.X, payload.Position.Y, payload.Z))
        {
            issues.Add($"{prefix} position ({payload.Position.X},{payload.Position.Y},{payload.Z}) is outside world bounds.");
        }

        if (!Enum.IsDefined(typeof(UsePolicy), payload.UsePolicy))
        {
            issues.Add($"{prefix} has unsupported use policy value {payload.UsePolicy}.");
        }

        if (string.IsNullOrWhiteSpace(payload.ConditionState))
        {
            issues.Add($"{prefix} has a blank condition state.");
        }

        if (payload.DurabilityCurrent.HasValue && payload.DurabilityCurrent.Value < 0)
        {
            issues.Add($"{prefix} has negative current durability.");
        }

        if (payload.DurabilityMax.HasValue && payload.DurabilityMax.Value < 0)
        {
            issues.Add($"{prefix} has negative max durability.");
        }

        if (payload.DurabilityCurrent.HasValue
            && payload.DurabilityMax.HasValue
            && payload.DurabilityCurrent.Value > payload.DurabilityMax.Value)
        {
            issues.Add($"{prefix} current durability exceeds max durability.");
        }

        var locationOwners = 0;
        if (payload.ContainedBy.HasValue) locationOwners++;
        if (payload.CarriedBy.HasValue) locationOwners++;
        if (payload.EquippedBy.HasValue) locationOwners++;
        if (payload.InstalledAt.HasValue) locationOwners++;
        if (locationOwners > 1)
        {
            issues.Add($"{prefix} has multiple non-ground location owners.");
        }

        ValidatePlacementPayload(payload.InstalledAt, prefix, issues);
        ValidateReservationTokenPayload(payload.ReservationTokens, prefix, issues);
        ValidateImprovementPayload(payload.Improvements, prefix, issues);
        ValidatePerishablePayload(payload.Perishable, prefix, issues);
    }

    private void ValidatePlacementPayload(
        WorldSavePlacementData? placement,
        string prefix,
        ICollection<string> issues)
    {
        if (!placement.HasValue)
            return;

        var value = placement.Value;
        if (_world != null && !_world.IsValidPosition(value.AnchorWorld.X, value.AnchorWorld.Y, value.Z))
        {
            issues.Add($"{prefix} placement anchor ({value.AnchorWorld.X},{value.AnchorWorld.Y},{value.Z}) is outside world bounds.");
        }

        if (value.Rotation is < 0 or > 3)
        {
            issues.Add($"{prefix} placement rotation {value.Rotation} is outside the supported range.");
        }
    }

    private static void ValidateReservationTokenPayload(
        IReadOnlyList<WorldSaveItemReservationTokenData>? reservations,
        string prefix,
        ICollection<string> issues)
    {
        if (reservations == null)
        {
            issues.Add($"{prefix} reservation token payload is missing.");
            return;
        }

        for (var i = 0; i < reservations.Count; i++)
        {
            var token = reservations[i];
            if (token.JobGuid == Guid.Empty)
            {
                issues.Add($"{prefix} reservation token[{i}] has an empty job guid.");
            }

            if (token.ReservedCount <= 0)
            {
                issues.Add($"{prefix} reservation token[{i}] has non-positive reserved count {token.ReservedCount}.");
            }

            if (string.IsNullOrWhiteSpace(token.ReservationType))
            {
                issues.Add($"{prefix} reservation token[{i}] has a blank reservation type.");
            }
        }
    }

    private static void ValidateImprovementPayload(
        IReadOnlyList<WorldSaveItemImprovementData>? improvements,
        string prefix,
        ICollection<string> issues)
    {
        if (improvements == null)
            return;

        for (var i = 0; i < improvements.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(improvements[i].Type))
            {
                issues.Add($"{prefix} improvement[{i}] has a blank type.");
            }
        }
    }

    private static void ValidatePerishablePayload(
        WorldSaveItemPerishableData? perishable,
        string prefix,
        ICollection<string> issues)
    {
        if (!perishable.HasValue)
            return;

        var value = perishable.Value;
        if (value.FreshDurationTicks < 0 || value.SpoilDurationTicks < 0)
        {
            issues.Add($"{prefix} has negative perishable duration ticks.");
        }

        if (float.IsNaN(value.CurrentFreshness) || float.IsInfinity(value.CurrentFreshness))
        {
            issues.Add($"{prefix} has non-finite perishable freshness.");
        }
    }

    private static ItemInstance ToItemInstance(WorldSaveItemPayloadData payload)
    {
        return new ItemInstance(
            payload.Guid,
            payload.DefinitionId,
            ToPoint(payload.Position),
            payload.Z,
            payload.StackCount,
            payload.SpawnedAtTick)
        {
            MaterialId = payload.MaterialId,
            ContainedBy = payload.ContainedBy,
            CarriedBy = payload.CarriedBy,
            EquippedBy = payload.EquippedBy,
            InstalledAt = ToPlacement(payload.InstalledAt),
            OwnerFactionId = payload.OwnerFactionId,
            OwnerCreatureGuid = payload.OwnerCreatureGuid,
            UsePolicy = (UsePolicy)payload.UsePolicy,
            Forbidden = payload.Forbidden,
            ReservationTokens = payload.ReservationTokens.Select(ToReservationToken).ToList(),
            QualityTier = payload.QualityTier,
            Artifact = payload.Artifact,
            ArtifactName = payload.ArtifactName,
            ConditionState = payload.ConditionState,
            DurabilityCurrent = payload.DurabilityCurrent,
            DurabilityMax = payload.DurabilityMax,
            CraftedBy = payload.CraftedBy,
            MakerFactionId = payload.MakerFactionId,
            StyleTag = payload.StyleTag,
            Improvements = payload.Improvements?.Select(ToImprovement).ToList(),
            Perishable = ToPerishable(payload.Perishable)
        };
    }

    private static Point ToPoint(WorldSavePointData point)
    {
        return new Point(point.X, point.Y);
    }

    private static ReservationToken ToReservationToken(WorldSaveItemReservationTokenData payload)
    {
        return new ReservationToken
        {
            JobGuid = payload.JobGuid,
            ClaimantCreatureGuid = payload.ClaimantCreatureGuid,
            ReservedCount = payload.ReservedCount,
            ExpiresAtTick = payload.ExpiresAtTick,
            ReservationType = payload.ReservationType
        };
    }

    private static PlacementData? ToPlacement(WorldSavePlacementData? payload)
    {
        if (!payload.HasValue)
            return null;

        var value = payload.Value;
        return new PlacementData
        {
            AnchorWorld = ToPoint(value.AnchorWorld),
            Z = value.Z,
            Rotation = value.Rotation,
            StateId = value.StateId
        };
    }

    private static Improvement ToImprovement(WorldSaveItemImprovementData payload)
    {
        return new Improvement
        {
            Type = payload.Type,
            MaterialId = payload.MaterialId,
            QualityTier = payload.QualityTier,
            CreatedBy = payload.CreatedBy,
            Description = payload.Description
        };
    }

    private static PerishableState? ToPerishable(WorldSaveItemPerishableData? payload)
    {
        if (!payload.HasValue)
            return null;

        var value = payload.Value;
        return new PerishableState
        {
            CreatedAtTick = value.CreatedAtTick,
            FreshDurationTicks = value.FreshDurationTicks,
            SpoilDurationTicks = value.SpoilDurationTicks,
            CurrentFreshness = value.CurrentFreshness
        };
    }
}
