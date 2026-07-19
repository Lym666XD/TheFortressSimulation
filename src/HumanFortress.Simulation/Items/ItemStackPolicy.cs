using HumanFortress.Contracts.Simulation.Items;

namespace HumanFortress.Simulation.Items;

internal static class ItemStackPolicy
{
    internal static bool TryGetCapacity(ItemDefinition? definition, out int capacity)
    {
        capacity = 0;
        if (definition?.Stack is not { Mode: not StackMode.None, MaxPerStack: > 0 } stack)
            return false;

        capacity = stack.MaxPerStack;
        return true;
    }

    internal static bool AreCompatible(
        ItemInstance first,
        ItemInstance second,
        ItemDefinition definition,
        bool firstContainerIsEmpty,
        bool secondContainerIsEmpty)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        ArgumentNullException.ThrowIfNull(definition);

        if (!TryGetCapacity(definition, out _)
            || !first.IsOnGround
            || !second.IsOnGround
            || !string.Equals(first.DefinitionId, second.DefinitionId, StringComparison.Ordinal)
            || first.ReservationTokens.Count != 0
            || second.ReservationTokens.Count != 0
            || first.Artifact
            || second.Artifact)
        {
            return false;
        }

        var stack = definition.Stack!;
        if (stack.RequiresPristine
            && (!string.Equals(first.ConditionState, "Pristine", StringComparison.Ordinal)
                || !string.Equals(second.ConditionState, "Pristine", StringComparison.Ordinal)))
        {
            return false;
        }

        if (stack.RequireNoMods
            && (HasImprovements(first) || HasImprovements(second) || first.Artifact || second.Artifact))
        {
            return false;
        }

        if (stack.RequiresEmpty && (!firstContainerIsEmpty || !secondContainerIsEmpty))
            return false;

        return string.Equals(first.MaterialId, second.MaterialId, StringComparison.Ordinal)
            && string.Equals(first.OwnerFactionId, second.OwnerFactionId, StringComparison.Ordinal)
            && first.OwnerCreatureGuid == second.OwnerCreatureGuid
            && first.UsePolicy == second.UsePolicy
            && first.Forbidden == second.Forbidden
            && first.QualityTier == second.QualityTier
            && first.Artifact == second.Artifact
            && string.Equals(first.ArtifactName, second.ArtifactName, StringComparison.Ordinal)
            && string.Equals(first.ConditionState, second.ConditionState, StringComparison.Ordinal)
            && first.DurabilityCurrent == second.DurabilityCurrent
            && first.DurabilityMax == second.DurabilityMax
            && first.CraftedBy == second.CraftedBy
            && string.Equals(first.MakerFactionId, second.MakerFactionId, StringComparison.Ordinal)
            && string.Equals(first.StyleTag, second.StyleTag, StringComparison.Ordinal)
            && ImprovementsEqual(first.Improvements, second.Improvements)
            && PerishableEqual(first.Perishable, second.Perishable);
    }

    private static bool HasImprovements(ItemInstance item)
    {
        return item.Improvements is { Count: > 0 };
    }

    private static bool ImprovementsEqual(
        IReadOnlyList<Improvement>? first,
        IReadOnlyList<Improvement>? second)
    {
        int firstCount = first?.Count ?? 0;
        int secondCount = second?.Count ?? 0;
        if (firstCount != secondCount)
            return false;

        for (var index = 0; index < firstCount; index++)
        {
            var left = first![index];
            var right = second![index];
            if (!string.Equals(left.Type, right.Type, StringComparison.Ordinal)
                || !string.Equals(left.MaterialId, right.MaterialId, StringComparison.Ordinal)
                || left.QualityTier != right.QualityTier
                || left.CreatedBy != right.CreatedBy
                || !string.Equals(left.Description, right.Description, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool PerishableEqual(PerishableState? first, PerishableState? second)
    {
        if (ReferenceEquals(first, second))
            return true;
        if (first == null || second == null)
            return false;

        return first.CreatedAtTick == second.CreatedAtTick
            && first.FreshDurationTicks == second.FreshDurationTicks
            && first.SpoilDurationTicks == second.SpoilDurationTicks
            && first.CurrentFreshness.Equals(second.CurrentFreshness);
    }
}
